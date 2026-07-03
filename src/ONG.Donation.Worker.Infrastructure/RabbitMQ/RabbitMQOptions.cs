namespace ONG.Donation.Worker.Infrastructure.RabbitMQ;

public class RabbitMQOptions
{
    public string HostName { get; set; } = "localhost";
    public string UserName { get; set; } = "owng";
    public string Password { get; set; } = "owong";
    public string ExchangeName { get; set; } = "donation.events";
    public string QueueName { get; set; } = "donation.payment";
}
