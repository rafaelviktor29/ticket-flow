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
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name is required", nameof(name));
        if (string.IsNullOrWhiteSpace(venue)) throw new ArgumentException("venue is required", nameof(venue));
        if (totalTickets <= 0) throw new ArgumentException("totalTickets must be greater than zero", nameof(totalTickets));

        Id = Guid.NewGuid();
        Name = name;
        Venue = venue;
        Date = date;
        TotalTickets = totalTickets;
        CreatedAt = DateTime.UtcNow;
    }

    // Method for setting the ID, useful for testing.
    public void SetId(Guid id)
    {
        Id = id;
    }
}
