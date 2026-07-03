using Microsoft.EntityFrameworkCore;
using WorkerPayment = ONG.Donation.Worker.Domain.Entities.Payment;
using ONG.Donation.Worker.Infrastructure.Persistence.Context;

namespace ONG.Donation.Worker.Infrastructure.Persistence.Repositories;

public class PaymentRepository
{
    private readonly WorkerDbContext _context;

    public PaymentRepository(WorkerDbContext context) => _context = context;

    public async Task<WorkerPayment?> GetByIdAsync(int id) =>
        await _context.Payments.FirstOrDefaultAsync(d => d.Id == id);

    public async Task<WorkerPayment?> GetByDonationIdAsync(int donationId) =>
        await _context.Payments.FirstOrDefaultAsync(d => d.DonationId == donationId);

    public async Task AddAsync(WorkerPayment payment) =>
        await _context.Payments.AddAsync(payment);

    public void Update(WorkerPayment payment) =>
        _context.Payments.Update(payment);
}
