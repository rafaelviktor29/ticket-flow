using TicketFlow.Domain.Interfaces;

namespace TicketFlow.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    // Persiste todas as mudanças rastreadas pelo DbContext em uma única transação
    public async Task<int> CommitAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);

    public void Dispose()
        => _context.Dispose();
}
