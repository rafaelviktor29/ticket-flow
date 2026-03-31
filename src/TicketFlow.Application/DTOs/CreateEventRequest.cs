namespace TicketFlow.Application.DTOs;

public record CreateEventRequest(
    string Name,
    string Venue,
    DateTime Date,
    int TotalTickets,
    decimal TicketPrice
);