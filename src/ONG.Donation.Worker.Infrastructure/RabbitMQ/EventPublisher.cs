using System.Text.Json;
using Microsoft.Extensions.Logging;
using ONG.Donation.Worker.Domain.Interfaces;
using RabbitMQ.Client;

namespace ONG.Donation.Worker.Infrastructure.RabbitMQ;

public class EventPublisher : IEventPublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly ILogger<EventPublisher> _logger;
    private readonly string _exchangeName;

    public EventPublisher(ILogger<EventPublisher> logger, RabbitMQOptions rabbitMqOptions)
    {
        _logger = logger;

        var factory = new ConnectionFactory
        {
            HostName = rabbitMqOptions.HostName,
            UserName = rabbitMqOptions.UserName,
            Password = rabbitMqOptions.Password
        };

        _exchangeName = rabbitMqOptions.ExchangeName;

        _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
        _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

        _channel.ExchangeDeclareAsync(_exchangeName, ExchangeType.Topic, durable: true).GetAwaiter().GetResult();

        _logger.LogInformation("RabbitMQ connection established, exchange {ExchangeName} declared", _exchangeName);
    }

    public async Task PublishAsync<T>(T domainEvent) where T : IDomainEvent
    {
        var routingKey = typeof(T).Name;
        var body = JsonSerializer.SerializeToUtf8Bytes(domainEvent);

        await _channel.BasicPublishAsync(
            exchange: _exchangeName,
            routingKey: routingKey,
            body: body);

        _logger.LogInformation("Event {EventType} published to {Exchange}/{RoutingKey}",
            routingKey, _exchangeName, routingKey);
    }

    public void Dispose()
    {
        _channel?.CloseAsync().GetAwaiter().GetResult();
        _connection?.CloseAsync().GetAwaiter().GetResult();
        _logger.LogInformation("RabbitMQ connection closed");
    }
}
