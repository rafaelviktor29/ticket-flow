using Microsoft.EntityFrameworkCore;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Interfaces;

namespace TicketFlow.Infrastructure.Persistence.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly AppDbContext _context;

    public PaymentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
        => await _context.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId, ct);

    public async Task AddAsync(Payment payment, CancellationToken ct = default)
        => await _context.Payments.AddAsync(payment, ct);
}
