namespace TicketFlow.Domain.Entities;

public class Event
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Venue { get; private set; }
    public DateTime Date { get; private set; }
    public int TotalTickets { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public ICollection<Ticket> Tickets { get; private set; } = new List<Ticket>();

    protected Event() { }

    public Event(string name, string venue, DateTime date, int totalTickets)
    {
        Id = Guid.NewGuid();
        Name = name;
        Venue = venue;
        Date = date;
        TotalTickets = totalTickets;
        CreatedAt = DateTime.UtcNow;
    }

    // Método para definir o ID, útil para testes
    public void SetId(Guid id)
    {
        Id = id;
    }
}
