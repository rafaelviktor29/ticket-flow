using TicketFlow.Domain.Entities;

namespace TicketFlow.Domain.Interfaces;

public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Ticket?> GetAvailableByEventAsync(Guid eventId, CancellationToken ct = default);
    Task<IEnumerable<Ticket>> GetByEventIdAsync(Guid eventId, CancellationToken ct = default);
    Task AddAsync(Ticket ticket, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<Ticket> tickets, CancellationToken ct = default);
}
