using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ONG.Donation.Worker.Infrastructure.DependencyInjection;
using ONG.Donation.Worker.Infrastructure.Persistence.Context;
using ONG.Donation.Worker.Consumers;
using Serilog;
using Serilog.Sinks.Grafana.Loki;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

var lokiUrl = config["Loki:Url"] ?? "http://localhost:3100";

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.WithProperty("Application", "ONG.Donation.Worker")
    .WriteTo.Console()
    .WriteTo.GrafanaLoki(lokiUrl,
        new[]
        {
            new LokiLabel { Key = "app", Value = "ong-donation-worker" },
        })
    .CreateLogger();

var startupConnectionStrings = config.GetSection("ConnectionStrings")
    .GetChildren()
    .Where(child => !string.IsNullOrWhiteSpace(child.Value))
    .ToDictionary(child => child.Key, child => child.Value);

Log.Information("Startup connection strings: {@ConnectionStrings}", startupConnectionStrings);
Log.Information("RabbitMQ connection string present: {HasRabbitMq}", !string.IsNullOrWhiteSpace(config["RabbitMQ:ConnectionString"] ?? config["RabbitMQ__ConnectionString"]));

var builder = Host.CreateDefaultBuilder(args)
    .UseSerilog()
    .ConfigureServices((context, services) =>
    {
        services.AddWorkerInfrastructure(context.Configuration);
        services.AddHostedService<DonationConsumer>();
    });

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WorkerDbContext>();
    await db.Database.MigrateAsync();
}

await host.RunAsync();
