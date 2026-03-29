using System;

namespace TicketFlow.Application.Messaging;

public class OrderCreatedMessage
{
    public Guid OrderId { get; set; }
}