namespace ONG.Donation.Worker.Infrastructure.ServiceBus;

public class ServiceBusOptions
{
    public string ConnectionString { get; set; } = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true";
    public string QueueName { get; set; } = "donation.payment";
    public string ResultQueueName { get; set; } = "donation.payment.result";
}