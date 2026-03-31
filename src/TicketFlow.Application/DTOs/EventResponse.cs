namespace TicketFlow.Application.DTOs;

public record EventResponse(
    Guid Id,
    string Name,
    string Venue,
    DateTime Date,
    int TotalTickets
);
