using TicketFlow.Domain.Enums;

namespace TicketFlow.Application.DTOs;

public record OrderResponse(
    Guid Id,
    Guid TicketId,
    Guid UserId,
    string Status,
    string IdempotencyKey,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime? ProcessedAt
);
