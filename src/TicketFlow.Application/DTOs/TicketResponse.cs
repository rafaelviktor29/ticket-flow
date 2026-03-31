namespace TicketFlow.Application.DTOs;

public record TicketResponse(
    Guid Id,
    string SeatNumber,
    decimal Price,
    bool IsReserved
);
