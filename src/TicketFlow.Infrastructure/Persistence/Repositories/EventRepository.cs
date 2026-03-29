using Microsoft.EntityFrameworkCore;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Interfaces;

namespace TicketFlow.Infrastructure.Persistence.Repositories;

public class EventRepository : IEventRepository
{
    private readonly AppDbContext _context;

    public EventRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Event?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Events.Include(e => e.Tickets).FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IEnumerable<Event>> GetAllAsync(CancellationToken ct = default)
        => await _context.Events.AsNoTracking().ToListAsync(ct);

    public async Task AddAsync(Event @event, CancellationToken ct = default)
        => await _context.Events.AddAsync(@event, ct);
}
