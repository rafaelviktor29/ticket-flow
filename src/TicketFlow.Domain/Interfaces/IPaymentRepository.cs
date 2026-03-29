using TicketFlow.Domain.Entities;

namespace TicketFlow.Domain.Interfaces;

public interface IPaymentRepository
{
    Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);
    Task AddAsync(Payment payment, CancellationToken ct = default);
}
