using ONG.Donation.Worker.Domain.Interfaces;

namespace ONG.Donation.Worker.Domain.Events;

public record DonationPaymentFailedEvent(
    int DonationId,
    string Reason,
    DateTime OccurredAt) : IDomainEvent;
