using ONG.Donation.Worker.Domain.Common;
using ONG.Donation.Worker.Domain.Enums;

namespace ONG.Donation.Worker.Domain.Entities;

public class Payment : BaseEntity
{
    public int DonationId { get; private set; }
    public int CampaignId { get; private set; }
    public int DonorId { get; private set; }
    public decimal Amount { get; private set; }
    public DonationStatus Status { get; private set; }

    private Payment() { }

    public Payment(int donationId, int campaignId, int userId, decimal amount)
    {
        DonationId = donationId;
        CampaignId = campaignId;
        DonorId = userId;
        Amount = amount;
        Status = DonationStatus.Pendente;
    }

    public void MarkAsProcessed()
    {
        Status = DonationStatus.Processada;
        SetUpdatedAt();
    }

    public void MarkAsFailed()
    {
        Status = DonationStatus.Falhou;
        SetUpdatedAt();
    }
}
