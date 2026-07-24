using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using ONG.Donation.Worker.Domain.Interfaces;

namespace ONG.Donation.Worker.Infrastructure.ServiceBus;

public class ServiceBusEventPublisher : IEventPublisher, IAsyncDisposable
{
    private readonly ServiceBusSender _sender;
    private readonly ServiceBusClient _client;
    private readonly ILogger<ServiceBusEventPublisher> _logger;

    public ServiceBusEventPublisher(ILogger<ServiceBusEventPublisher> logger, ServiceBusOptions options)
    {
        _logger = logger;

        _client = new ServiceBusClient(options.ConnectionString);
        _sender = _client.CreateSender(options.ResultQueueName);

        _logger.LogInformation("ServiceBus connection established, result queue {QueueName}", options.ResultQueueName);
    }

    public async Task PublishAsync<T>(T domainEvent) where T : IDomainEvent
    {
        var body = JsonSerializer.Serialize(domainEvent);
        var message = new ServiceBusMessage(body)
        {
            Subject = typeof(T).Name,
            ContentType = "application/json"
        };

        await _sender.SendMessageAsync(message);

        _logger.LogInformation("Event {EventType} published to ServiceBus queue", typeof(T).Name);
    }

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync();
        await _client.DisposeAsync();
        _logger.LogInformation("ServiceBus connection closed");
    }
}