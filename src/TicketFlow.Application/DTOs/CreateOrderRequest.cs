namespace TicketFlow.Application.DTOs;

public record CreateOrderRequest(
    Guid TicketId,
    Guid UserId,
    // IdempotencyKey: gerado pelo cliente (ex: UUID v4).
    // Se o mesmo pedido chegar duas vezes, o segundo é rejeitado.
    string IdempotencyKey
);
