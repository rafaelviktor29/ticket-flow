namespace TicketFlow.Domain.Entities;

public class Ticket
{
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public string SeatNumber { get; private set; }
    public decimal Price { get; private set; }
    public bool IsReserved { get; private set; }
    public Guid? OrderId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Concurrency token: updated on each reservation.
    // EF Core includes this in the WHERE clause of UPDATE — if another
    // process updated the row, the value changes and the UPDATE affects 0 rows.
    public Guid ConcurrencyToken { get; private set; } = Guid.NewGuid();

    public Event? Event { get; private set; }

    protected Ticket() { }

    public Ticket(Guid eventId, string seatNumber, decimal price)
    {
        if (eventId == Guid.Empty) throw new ArgumentException("eventId is required", nameof(eventId));
        if (string.IsNullOrWhiteSpace(seatNumber)) throw new ArgumentException("seatNumber is required", nameof(seatNumber));
        if (price < 0) throw new ArgumentException("price cannot be negative", nameof(price));

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
            throw new InvalidOperationException($"Ticket {Id} is already reserved.");

        IsReserved = true;
        OrderId = orderId;
        ConcurrencyToken = Guid.NewGuid(); // change the token on each reservation
    }

    public void Release()
    {
        IsReserved = false;
        OrderId = null;
        ConcurrencyToken = Guid.NewGuid();
    }

    // Method for setting the ID, useful for testing.
    public void SetId(Guid id)
    {
        Id = id;
    }
}