using TicketFlow.Domain.Enums;

namespace TicketFlow.Domain.Entities;

public class Order
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid UserId { get; private set; }
    public OrderStatus Status { get; private set; }
    public string IdempotencyKey { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }

    public Ticket Ticket { get; private set; }
    public Payment Payment { get; private set; }

    protected Order() { }

    public Order(Guid ticketId, Guid userId, string idempotencyKey)
    {
        Id = Guid.NewGuid();
        TicketId = ticketId;
        UserId = userId;
        IdempotencyKey = idempotencyKey;
        Status = OrderStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public void MarkAsProcessing()
    {
        Status = OrderStatus.Processing;
    }

    public void MarkAsConfirmed()
    {
        Status = OrderStatus.Confirmed;
        ProcessedAt = DateTime.UtcNow;
    }

    public void MarkAsFailed(string reason)
    {
        Status = OrderStatus.Failed;
        FailureReason = reason;
        ProcessedAt = DateTime.UtcNow;
    }
}
