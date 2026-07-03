namespace ONG.Donation.Worker.Domain.Interfaces;

public interface IEventPublisher
{
    Task PublishAsync<T>(T domainEvent) where T : IDomainEvent;
}
