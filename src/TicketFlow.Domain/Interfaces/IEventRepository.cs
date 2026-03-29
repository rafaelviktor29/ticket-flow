using TicketFlow.Domain.Entities;

namespace TicketFlow.Domain.Interfaces;

public interface IEventRepository
{
    Task<Event?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Event>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Event @event, CancellationToken ct = default);
}
