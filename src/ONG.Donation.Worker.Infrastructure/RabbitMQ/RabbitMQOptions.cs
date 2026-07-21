namespace ONG.Donation.Worker.Infrastructure.RabbitMQ;

public class RabbitMQOptions
{
    public string ConnectionString { get; set; } = "amqp://guest:guest@localhost:5672/";
    public string ExchangeName { get; set; } = "donation.events";
    public string QueueName { get; set; } = "donation.payment";
}
