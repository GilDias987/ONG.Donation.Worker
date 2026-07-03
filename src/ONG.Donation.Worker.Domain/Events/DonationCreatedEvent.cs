using ONG.Donation.Worker.Domain.Interfaces;

namespace ONG.Donation.Worker.Domain.Events;

public record DonationCreatedEvent(
    int DonationId,
    int CampaignId,
    int DonorId,
    decimal Amount,
    DateTime OccurredAt) : IDomainEvent;
