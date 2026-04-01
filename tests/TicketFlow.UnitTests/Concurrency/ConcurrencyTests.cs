using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Microsoft.Data.Sqlite;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;
using TicketFlow.Infrastructure.Messaging;
using TicketFlow.Infrastructure.Persistence;
using TicketFlow.Infrastructure.Persistence.Repositories;
using Xunit;

namespace TicketFlow.UnitTests.Concurrency;
 
public class ConcurrencyTests : IClassFixture<DatabaseFixture>
{
    private readonly SqliteConnection _connection;
 
    // The constructor receives the DatabaseFixture which already initialized
    // the shared connection and schema
    public ConcurrencyTests(DatabaseFixture fixture)
    {
        _connection = fixture.Connection;
    }
 
    // Create a new DbContext using the shared connection
    private AppDbContext CriarContexto()
    {
        // No dbName is required because the connection already identifies the database
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        var ctx = new AppDbContext(options);
        // EnsureCreated() is called once in the DatabaseFixture constructor
        return ctx;
    }
 
    private static OrderProcessor CriarProcessor(AppDbContext ctx) =>
        new(new OrderRepository(ctx), new TicketRepository(ctx),
            new PaymentRepository(ctx), new UnitOfWork(ctx),
            NullLogger<OrderProcessor>.Instance);

    [Fact]
    public async Task ProcessAsync_QuandoMultiplosPedidosParaMesmoTicket_ApenasUmDeveSerConfirmado()
    {
        await using var setupCtx = CriarContexto(); // Uses the shared connection

        var evento  = new Event("Show", "Arena", DateTime.UtcNow.AddMonths(1), 1);
        var ticket  = new Ticket(evento.Id, "A001", 150m);
        setupCtx.Events.Add(evento);
        setupCtx.Tickets.Add(ticket);

        const int qtd = 5;
        var pedidos = Enumerable.Range(0, qtd)
            .Select(_ => new Order(ticket.Id, Guid.NewGuid(), $"chave-{Guid.NewGuid()}"))
            .ToList();
        setupCtx.Orders.AddRange(pedidos);
        await setupCtx.SaveChangesAsync();
 
        var tarefas = pedidos.Select(async pedido =>
        {
                // Using 'await using' ensures the task's context is disposed
                await using var ctx = CriarContexto(); // Each task gets its own context sharing the connection
            var processor = CriarProcessor(ctx);
            try
            {
                await processor.ProcessAsync(pedido.Id);
                var finalOrder = await ctx.Orders.FindAsync(pedido.Id);
                return finalOrder.Status == OrderStatus.Confirmed;
            }
                catch (DbUpdateConcurrencyException) { return false; } // Expected concurrency conflict
            catch (Exception ex)
            {
                throw new Xunit.Sdk.XunitException($"Exceção inesperada para o pedido {pedido.Id}: {ex.Message}", ex);
            }
        });

        var resultados = await Task.WhenAll(tarefas);

        resultados.Count(r => r).ShouldBe(1,
            "only one process should confirm — optimistic lock guarantees this");
        resultados.Count(r => !r).ShouldBe(qtd - 1,
            "the others should fail with DbUpdateConcurrencyException");
    }
 
    [Fact]
    public async Task ProcessAsync_AposConcorrencia_BancoDeveEstarConsistente()
    {
        // Arrange
        await using var setupCtx = CriarContexto();

        var evento = new Event("Concerto", "Teatro", DateTime.UtcNow.AddMonths(1), 1);
        var ticket = new Ticket(evento.Id, "PLAT01", 300m);
        setupCtx.Events.Add(evento);
        setupCtx.Tickets.Add(ticket);

        const int qtd = 10;
        var pedidos = Enumerable.Range(0, qtd)
            .Select(_ => new Order(ticket.Id, Guid.NewGuid(), $"chave-{Guid.NewGuid()}"))
            .ToList();
        setupCtx.Orders.AddRange(pedidos);
        await setupCtx.SaveChangesAsync();
 
        var tarefas = pedidos.Select(async pedido =>
        { 
            await using var ctx = CriarContexto();
            var processor = CriarProcessor(ctx);
            try { await processor.ProcessAsync(pedido.Id); }
            catch (DbUpdateConcurrencyException) { /* Expected competitive conflict */ }
            catch (Exception ex)
            {
                throw new Xunit.Sdk.XunitException($"Exceção inesperada para o pedido {pedido.Id}: {ex.Message}", ex);
            }
        });
        await Task.WhenAll(tarefas);

        // Assert
        await using var checkCtx = CriarContexto();
        var todosPedidos = await checkCtx.Orders.ToListAsync();
        var pagamentos   = await checkCtx.Payments.ToListAsync();
        var ticketFinal  = await checkCtx.Tickets.FindAsync(ticket.Id);

        todosPedidos.Count(o => o.Status == OrderStatus.Confirmed).ShouldBe(1);
        todosPedidos.Count(o => o.Status == OrderStatus.Failed).ShouldBe(qtd - 1);
        pagamentos.Count.ShouldBe(1);
        pagamentos.Single().OrderId.ShouldBe(
            todosPedidos.Single(o => o.Status == OrderStatus.Confirmed).Id);
        ticketFinal!.IsReserved.ShouldBeTrue();
        todosPedidos.ShouldNotContain(o => o.Status == OrderStatus.Processing);
    }
}