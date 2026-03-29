namespace TicketFlow.Domain.Interfaces;

// Unit of Work: garante que todas as operações de um fluxo
// sejam salvas juntas — ou nenhuma é salva.
public interface IUnitOfWork : IDisposable
{
    Task<int> CommitAsync(CancellationToken ct = default);
}
