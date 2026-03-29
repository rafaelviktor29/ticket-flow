namespace TicketFlow.Domain.Entities;

public class Ticket
{
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public string SeatNumber { get; private set; } // TODO: Conferir se pode iniciar ""
    public decimal Price { get; private set; }
    public bool IsReserved { get; private set; }
    public Guid? OrderId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Token de concorrência: atualizado a cada reserva.
    // EF Core inclui no WHERE do UPDATE — se outro processo
    // já atualizou, o valor mudou e 0 linhas são afetadas.
    public Guid ConcurrencyToken { get; private set; } = Guid.NewGuid();

    public Event Event { get; private set; }  // TODO: Conferir se pode ser nullable

    protected Ticket() { }

    public Ticket(Guid eventId, string seatNumber, decimal price)
    {
        Id = Guid.NewGuid();
        EventId = eventId;
        SeatNumber = seatNumber;
        Price = price;
        IsReserved = false;
        CreatedAt = DateTime.UtcNow;
    }

    public void Reserve(Guid orderId)
    {
        if (IsReserved)
            throw new InvalidOperationException($"Ticket {Id} já está reservado.");

        IsReserved = true;
        OrderId = orderId;
        ConcurrencyToken = Guid.NewGuid(); // muda o token a cada reserva
    }

    public void Release()
    {
        IsReserved = false;
        OrderId = null;
        ConcurrencyToken = Guid.NewGuid();
    }

    // Método para definir o ID, útil para testes
    public void SetId(Guid id)
    {
        Id = id;
    }
}