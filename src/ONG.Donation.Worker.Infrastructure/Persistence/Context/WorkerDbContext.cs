using Microsoft.EntityFrameworkCore;

namespace ONG.Donation.Worker.Infrastructure.Persistence.Context;

public class WorkerDbContext : DbContext
{
    public WorkerDbContext(DbContextOptions<WorkerDbContext> options) : base(options) { }

    public DbSet<global::ONG.Donation.Worker.Domain.Entities.Payment> Payments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkerDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
