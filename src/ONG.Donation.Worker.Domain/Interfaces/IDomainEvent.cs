namespace ONG.Donation.Worker.Domain.Interfaces;

public interface IDomainEvent
{
    DateTime OccurredAt { get; }
}
