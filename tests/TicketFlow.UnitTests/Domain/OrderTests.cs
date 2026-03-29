using Shouldly;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;
using Xunit;

namespace TicketFlow.UnitTests.Domain;

public class OrderTests
{
    [Fact]
    public void NovoPedido_DeveIniciarComStatusPending()
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), "chave-001");

        order.Status.ShouldBe(OrderStatus.Pending);
        order.ProcessedAt.ShouldBeNull();
        order.FailureReason.ShouldBeNull();
    }

    [Fact]
    public void MarkAsProcessing_DeveAlterarStatusParaProcessing()
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), "chave-001");

        order.MarkAsProcessing();

        order.Status.ShouldBe(OrderStatus.Processing);
    }

    [Fact]
    public void MarkAsConfirmed_DeveRegistrarDataProcessamento()
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), "chave-001");
        var antes = DateTime.UtcNow;

        order.MarkAsConfirmed();

        order.Status.ShouldBe(OrderStatus.Confirmed);
        order.ProcessedAt.ShouldNotBeNull();
        order.ProcessedAt!.Value.ShouldBeGreaterThanOrEqualTo(antes);
    }

    [Fact]
    public void MarkAsFailed_DeveRegistrarMotivoEDataProcessamento()
    {
        var order  = new Order(Guid.NewGuid(), Guid.NewGuid(), "chave-001");
        var motivo = "Conflito de concorrência.";

        order.MarkAsFailed(motivo);

        order.Status.ShouldBe(OrderStatus.Failed);
        order.FailureReason.ShouldBe(motivo);
        order.ProcessedAt.ShouldNotBeNull();
    }

    [Fact]
    public void IdempotencyKey_DeveSerArmazenadoCorretamente()
    {
        var chave = "chave-unica-123";

        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), chave);

        order.IdempotencyKey.ShouldBe(chave);
    }
}