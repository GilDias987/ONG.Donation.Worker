using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkerPayment = ONG.Donation.Worker.Domain.Entities.Payment;

namespace ONG.Donation.Worker.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<WorkerPayment>
{
    public void Configure(EntityTypeBuilder<WorkerPayment> builder)
    {
        builder.ToTable("Payments");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedOnAdd();
        builder.Property(d => d.DonationId).IsRequired();
        builder.HasIndex(d => d.DonationId).IsUnique();
        builder.Property(d => d.CampaignId).IsRequired();
        builder.Property(d => d.DonorId).IsRequired();
        builder.Property(d => d.Amount).IsRequired().HasColumnType("decimal(18,2)");
        builder.Property(d => d.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.CreatedAt).IsRequired();
        builder.Property(d => d.UpdatedAt);
    }
}
