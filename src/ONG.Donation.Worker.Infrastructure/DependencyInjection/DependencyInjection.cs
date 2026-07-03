using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ONG.Donation.Worker.Domain.Interfaces;
using ONG.Donation.Worker.Infrastructure.Persistence.Context;
using ONG.Donation.Worker.Infrastructure.Persistence.Repositories;
using ONG.Donation.Worker.Infrastructure.RabbitMQ;

namespace ONG.Donation.Worker.Infrastructure.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddWorkerInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("WorkerConnection");
        services.AddDbContext<WorkerDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<PaymentRepository>();

        var rabbitSection = configuration.GetSection("RabbitMQ");
        var rabbitOptions = new RabbitMQOptions
        {
            HostName = rabbitSection["HostName"] ?? "localhost",
            UserName = rabbitSection["UserName"] ?? "owng",
            Password = rabbitSection["Password"] ?? "owong",
            ExchangeName = rabbitSection["ExchangeName"] ?? "donation.events",
            QueueName = rabbitSection["QueueName"] ?? "donation.payment"
        };
        services.AddSingleton(rabbitOptions);
        services.AddSingleton<IEventPublisher, EventPublisher>();

        return services;
    }
}
