namespace TicketFlow.Domain.Interfaces;

// Unit of Work: ensures that all operations in a flow.
// are saved together — or none is saved.
public interface IUnitOfWork : IDisposable
{
    Task<int> CommitAsync(CancellationToken ct = default);
}
