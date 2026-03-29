using Moq;
using Shouldly;
using TicketFlow.Application.DTOs;
using TicketFlow.Application.Messaging;
using TicketFlow.Application.Services;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;
using TicketFlow.Domain.Interfaces;
using Xunit;

namespace TicketFlow.UnitTests.Application;

public class OrderServiceTests
{
    private readonly Mock<IOrderRepository>  _orderRepo  = new();
    private readonly Mock<ITicketRepository> _ticketRepo = new();
    private readonly Mock<IUnitOfWork>       _uow        = new();
    private readonly Mock<IMessagePublisher> _publisher  = new();

    private OrderService CriarService() =>
        new(_orderRepo.Object, _ticketRepo.Object, _uow.Object, _publisher.Object);

    [Fact]
    public async Task CreateAsync_QuandoChaveJaExiste_DeveRetornarPedidoExistenteSemCriarNovo()
    {
        var chave        = "chave-duplicada";
        var pedidoExiste = new Order(Guid.NewGuid(), Guid.NewGuid(), chave);

        _orderRepo.Setup(r => r.GetByIdempotencyKeyAsync(chave, default))
                  .ReturnsAsync(pedidoExiste);

        var resultado = await CriarService().CreateAsync(new(Guid.NewGuid(), Guid.NewGuid(), chave));

        resultado.Id.ShouldBe(pedidoExiste.Id);
        _orderRepo.Verify(r => r.AddAsync(It.IsAny<Order>(), default), Times.Never);
        _publisher.Verify(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_QuandoChaveNova_DeveCriarPedidoEPublicarNaFila()
    {
        var ticketId = Guid.NewGuid();
        var chave    = "chave-nova";

        _orderRepo.Setup(r => r.GetByIdempotencyKeyAsync(chave, default))
                  .ReturnsAsync((Order?)null);
        _ticketRepo.Setup(r => r.GetByIdAsync(ticketId, default))
                   .ReturnsAsync(new Ticket(Guid.NewGuid(), "A001", 100m));
        _uow.Setup(u => u.CommitAsync(default)).ReturnsAsync(1);

        var resultado = await CriarService().CreateAsync(new(ticketId, Guid.NewGuid(), chave));

        resultado.Status.ShouldBe(OrderStatus.Pending);
        _orderRepo.Verify(r => r.AddAsync(It.IsAny<Order>(), default), Times.Once);
        _publisher.Verify(p => p.PublishAsync(It.IsAny<object>(), "orders", default), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_QuandoTicketNaoExiste_DeveLancarExcecao()
    {
        var ticketId = Guid.NewGuid();

        _orderRepo.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), default))
                  .ReturnsAsync((Order?)null);
        _ticketRepo.Setup(r => r.GetByIdAsync(ticketId, default))
                   .ReturnsAsync((Ticket?)null);

        var ex = await Should.ThrowAsync<Exception>(
            async () => await CriarService().CreateAsync(new(ticketId, Guid.NewGuid(), "chave")));

        ex.Message.ShouldContain(ticketId.ToString());
    }

    [Fact]
    public async Task GetByIdAsync_QuandoPedidoExiste_DeveRetornarPedido()
    {
        var orderId = Guid.NewGuid();
        var order   = new Order(Guid.NewGuid(), Guid.NewGuid(), "chave-001");

        _orderRepo.Setup(r => r.GetByIdAsync(orderId, default)).ReturnsAsync(order);

        var resultado = await CriarService().GetByIdAsync(orderId);

        resultado.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_QuandoPedidoNaoExiste_DeveRetornarNull()
    {
        _orderRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
                  .ReturnsAsync((Order?)null);

        var resultado = await CriarService().GetByIdAsync(Guid.NewGuid());

        resultado.ShouldBeNull();
    }
}