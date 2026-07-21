using ONG.Donation.Worker.Domain.Events;
using ONG.Donation.Worker.Domain.Interfaces;
using ONG.Donation.Worker.Infrastructure.Persistence.Context;
using ONG.Donation.Worker.Infrastructure.Persistence.Repositories;
using ONG.Donation.Worker.Infrastructure.RabbitMQ;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using WorkerPayment = ONG.Donation.Worker.Domain.Entities.Payment;

namespace ONG.Donation.Worker.Consumers;

public class DonationConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DonationConsumer> _logger;
    private readonly RabbitMQOptions _rabbitMqOptions;
    private IConnection _connection = null!;
    private IChannel _channel = null!;

    public DonationConsumer(IServiceProvider serviceProvider, ILogger<DonationConsumer> logger, RabbitMQOptions rabbitMqOptions)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _rabbitMqOptions = rabbitMqOptions;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(_rabbitMqOptions.ConnectionString)
        };

        _connection = await factory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(null, stoppingToken);

        await _channel.ExchangeDeclareAsync(_rabbitMqOptions.ExchangeName, ExchangeType.Topic, durable: true, cancellationToken: stoppingToken);
        await _channel.QueueDeclareAsync(_rabbitMqOptions.QueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
        await _channel.QueueBindAsync(_rabbitMqOptions.QueueName, _rabbitMqOptions.ExchangeName, "DonationCreatedEvent", cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                _logger.LogInformation("Donation event received: {Message}", message);

                using var scope = _serviceProvider.CreateScope();
                var paymentRepository = scope.ServiceProvider.GetRequiredService<PaymentRepository>();
                var dbContext = scope.ServiceProvider.GetRequiredService<WorkerDbContext>();
                var eventPublisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

                var donationEvent = JsonSerializer.Deserialize<DonationCreatedEvent>(body);
                if (donationEvent is null)
                {
                    _logger.LogWarning("Failed to deserialize donation event.");
                    return;
                }

                var existingPayment = await paymentRepository.GetByDonationIdAsync(donationEvent.DonationId);
                if (existingPayment is not null)
                {
                    _logger.LogInformation("Payment for donation {Id} already processed.", donationEvent.DonationId);
                    await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                    return;
                }

                var payment = new WorkerPayment(
                    donationEvent.DonationId,
                    donationEvent.CampaignId,
                    donationEvent.DonorId,
                    donationEvent.Amount);

                await paymentRepository.AddAsync(payment);

                var success = await ProcessPaymentAsync(payment);
                if (success)
                {
                    payment.MarkAsProcessed();
                    await dbContext.SaveChangesAsync(stoppingToken);
                    await eventPublisher.PublishAsync(new DonationPaymentProcessedEvent(
                        payment.DonationId, DateTime.UtcNow));
                    _logger.LogInformation("Payment for donation {Id} processed successfully.", payment.DonationId);
                }
                else
                {
                    payment.MarkAsFailed();
                    await dbContext.SaveChangesAsync(stoppingToken);
                    await eventPublisher.PublishAsync(new DonationPaymentFailedEvent(
                        payment.DonationId, "Payment processing failed.", DateTime.UtcNow));
                    _logger.LogWarning("Payment for donation {Id} processing failed.", payment.DonationId);
                }

                await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing donation event.");
                await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: stoppingToken);
            }
        };

        await _channel.BasicConsumeAsync(_rabbitMqOptions.QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
        _logger.LogInformation("DonationConsumer started, waiting for messages...");

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private static Task<bool> ProcessPaymentAsync(WorkerPayment payment)
    {
        try
        {
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DonationConsumer stopping...");
        if (_channel is not null) await _channel.CloseAsync(cancellationToken);
        if (_connection is not null) await _connection.CloseAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
