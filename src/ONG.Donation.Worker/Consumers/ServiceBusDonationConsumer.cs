using System.Text.Json;
using Azure.Messaging.ServiceBus;
using ONG.Donation.Worker.Domain.Events;
using ONG.Donation.Worker.Domain.Interfaces;
using ONG.Donation.Worker.Infrastructure.Persistence.Context;
using ONG.Donation.Worker.Infrastructure.Persistence.Repositories;
using ONG.Donation.Worker.Infrastructure.ServiceBus;
using WorkerPayment = ONG.Donation.Worker.Domain.Entities.Payment;

namespace ONG.Donation.Worker.Consumers;

public class ServiceBusDonationConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ServiceBusDonationConsumer> _logger;
    private readonly ServiceBusOptions _serviceBusOptions;
    private ServiceBusProcessor _processor = null!;
    private ServiceBusClient _client = null!;

    public ServiceBusDonationConsumer(IServiceProvider serviceProvider, ILogger<ServiceBusDonationConsumer> logger, ServiceBusOptions serviceBusOptions)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _serviceBusOptions = serviceBusOptions;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _client = new ServiceBusClient(_serviceBusOptions.ConnectionString);

        _processor = _client.CreateProcessor(_serviceBusOptions.QueueName, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false
        });

        _processor.ProcessMessageAsync += async args =>
        {
            try
            {
                var body = args.Message.Body.ToString();
                _logger.LogInformation("Donation event received: {Message}", body);

                using var scope = _serviceProvider.CreateScope();
                var paymentRepository = scope.ServiceProvider.GetRequiredService<PaymentRepository>();
                var dbContext = scope.ServiceProvider.GetRequiredService<WorkerDbContext>();
                var eventPublisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

                var donationEvent = JsonSerializer.Deserialize<DonationCreatedEvent>(body);
                if (donationEvent is null)
                {
                    _logger.LogWarning("Failed to deserialize donation event.");
                    await args.CompleteMessageAsync(args.Message, stoppingToken);
                    return;
                }

                var existingPayment = await paymentRepository.GetByDonationIdAsync(donationEvent.DonationId);
                if (existingPayment is not null)
                {
                    _logger.LogInformation("Payment for donation {Id} already processed.", donationEvent.DonationId);
                    await args.CompleteMessageAsync(args.Message, stoppingToken);
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

                await args.CompleteMessageAsync(args.Message, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing donation event.");
                await args.AbandonMessageAsync(args.Message, cancellationToken: stoppingToken);
            }
        };

        _processor.ProcessErrorAsync += args =>
        {
            _logger.LogError(args.Exception, "ServiceBus processor error");
            return Task.CompletedTask;
        };

        await _processor.StartProcessingAsync(stoppingToken);
        _logger.LogInformation("ServiceBusDonationConsumer started, waiting for messages...");

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
        _logger.LogInformation("ServiceBusDonationConsumer stopping...");
        if (_processor is not null) await _processor.CloseAsync(cancellationToken);
        if (_client is not null) await _client.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }
}
