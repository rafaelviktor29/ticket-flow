using TicketFlow.Domain.Enums;

namespace TicketFlow.Domain.Entities;

public class Payment
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public Order Order { get; private set; }

    protected Payment() { }

    public Payment(Guid orderId, decimal amount)
    {
        Id = Guid.NewGuid();
        OrderId = orderId;
        Amount = amount;
        Status = PaymentStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public void Approve()
    {
        Status = PaymentStatus.Approved;
    }

    public void Reject()
    {
        Status = PaymentStatus.Rejected;
    }
}
