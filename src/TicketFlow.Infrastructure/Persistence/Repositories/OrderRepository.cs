using Microsoft.EntityFrameworkCore;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Interfaces;

namespace TicketFlow.Infrastructure.Persistence.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _context;

    public OrderRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<Order?> GetByIdempotencyKeyAsync(string key, CancellationToken ct = default)
        => await _context.Orders.FirstOrDefaultAsync(o => o.IdempotencyKey == key, ct);

    public async Task AddAsync(Order order, CancellationToken ct = default)
        => await _context.Orders.AddAsync(order, ct);
}
