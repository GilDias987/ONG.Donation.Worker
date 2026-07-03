using ONG.Donation.Worker.Domain.Interfaces;

namespace ONG.Donation.Worker.Domain.Events;

public record DonationPaymentProcessedEvent(
    int DonationId,
    DateTime OccurredAt) : IDomainEvent;
