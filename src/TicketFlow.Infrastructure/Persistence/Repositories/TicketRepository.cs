using Microsoft.EntityFrameworkCore;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Interfaces;

namespace TicketFlow.Infrastructure.Persistence.Repositories;

public class TicketRepository : ITicketRepository
{
    private readonly AppDbContext _context;

    public TicketRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Ticket?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct);

    // Busca um ingresso disponível para o evento.
    // Não usa AsNoTracking aqui pois precisamos que o EF Core
    // rastreie o objeto para detectar conflitos de RowVersion.
    public async Task<Ticket?> GetAvailableByEventAsync(Guid eventId, CancellationToken ct = default)
        => await _context.Tickets
            .Where(t => t.EventId == eventId && !t.IsReserved)
            .FirstOrDefaultAsync(ct);

    public async Task<IEnumerable<Ticket>> GetByEventIdAsync(Guid eventId, CancellationToken ct = default)
        => await _context.Tickets
            .AsNoTracking()
            .Where(t => t.EventId == eventId)
            .ToListAsync(ct);

    public async Task AddAsync(Ticket ticket, CancellationToken ct = default)
        => await _context.Tickets.AddAsync(ticket, ct);

    public async Task AddRangeAsync(IEnumerable<Ticket> tickets, CancellationToken ct = default)
        => await _context.Tickets.AddRangeAsync(tickets, ct);
}
