using Shouldly;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;
using Xunit;

namespace TicketFlow.UnitTests.Domain;

public class PaymentTests
{
    [Fact]
    public void NovoPagamento_DeveIniciarComStatusPending()
    {
        var payment = new Payment(Guid.NewGuid(), 150m);

        payment.Status.ShouldBe(PaymentStatus.Pending);
        payment.Amount.ShouldBe(150m);
    }

    [Fact]
    public void Approve_DeveAlterarStatusParaApproved()
    {
        var payment = new Payment(Guid.NewGuid(), 150m);

        payment.Approve();

        payment.Status.ShouldBe(PaymentStatus.Approved);
    }

    [Fact]
    public void Reject_DeveAlterarStatusParaRejected()
    {
        var payment = new Payment(Guid.NewGuid(), 150m);

        payment.Reject();

        payment.Status.ShouldBe(PaymentStatus.Rejected);
    }
}