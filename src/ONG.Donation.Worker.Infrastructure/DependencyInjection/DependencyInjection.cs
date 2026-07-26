using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ONG.Donation.Worker.Domain.Interfaces;
using ONG.Donation.Worker.Infrastructure.Persistence.Context;
using ONG.Donation.Worker.Infrastructure.Persistence.Repositories;
using ONG.Donation.Worker.Infrastructure.ServiceBus;

namespace ONG.Donation.Worker.Infrastructure.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddWorkerInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("WorkerConnection");
        services.AddDbContext<WorkerDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<PaymentRepository>();

        var serviceBusSection = configuration.GetSection("ServiceBus");
        Console.WriteLine(connectionString);
        Console.WriteLine(serviceBusSection);
        var serviceBusOptions = new ServiceBusOptions
        {
            ConnectionString = serviceBusSection["ConnectionString"] ?? "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true",
            QueueName = serviceBusSection["QueueName"] ?? "donation.payment",
            ResultQueueName = serviceBusSection["ResultQueueName"] ?? "donation.payment.result"
        };
        services.AddSingleton(serviceBusOptions);
        services.AddSingleton<IEventPublisher, ServiceBusEventPublisher>();

        return services;
    }
}
