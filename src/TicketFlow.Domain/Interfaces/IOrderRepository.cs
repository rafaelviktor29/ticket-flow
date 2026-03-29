using TicketFlow.Domain.Entities;

namespace TicketFlow.Domain.Interfaces;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Order?> GetByIdempotencyKeyAsync(string key, CancellationToken ct = default);
    Task AddAsync(Order order, CancellationToken ct = default);
}
