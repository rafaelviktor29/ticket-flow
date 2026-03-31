using Shouldly;
using TicketFlow.Domain.Entities;
using Xunit;

namespace TicketFlow.UnitTests.Domain;

public class TicketTests
{
    [Fact]
    public void Reserve_MarksTicketAsReserved()
    {
        var ticket  = new Ticket(Guid.NewGuid(), "A001", 150m);
        var orderId = Guid.NewGuid();

        ticket.Reserve(orderId);

        ticket.IsReserved.ShouldBeTrue();
        ticket.OrderId.ShouldBe(orderId);
    }

    [Fact]
    public void Reserve_WhenAlreadyReserved_Throws()
    {
        var ticket = new Ticket(Guid.NewGuid(), "A001", 150m);
        ticket.Reserve(Guid.NewGuid());

        var ex = Should.Throw<InvalidOperationException>(() => ticket.Reserve(Guid.NewGuid()));

        ex.Message.ShouldBe($"Ticket {ticket.Id} is already reserved.");
    }

    [Fact]
    public void Release_ReleasesReservedTicket()
    {
        var ticket = new Ticket(Guid.NewGuid(), "A001", 150m);
        ticket.Reserve(Guid.NewGuid());

        ticket.Release();

        ticket.IsReserved.ShouldBeFalse();
        ticket.OrderId.ShouldBeNull();
    }

    [Fact]
    public void NewTicket_IsAvailableByDefault()
    {
        var ticket = new Ticket(Guid.NewGuid(), "B012", 200m);

        ticket.IsReserved.ShouldBeFalse();
        ticket.OrderId.ShouldBeNull();
        ticket.Price.ShouldBe(200m);
        ticket.SeatNumber.ShouldBe("B012");
    }
}