using TicketFlow.Domain.Enums;

namespace TicketFlow.Application.DTOs;

public record OrderResponse(
    Guid Id,
    Guid TicketId,
    Guid UserId,
    OrderStatus Status,
    string IdempotencyKey,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime? ProcessedAt
);
