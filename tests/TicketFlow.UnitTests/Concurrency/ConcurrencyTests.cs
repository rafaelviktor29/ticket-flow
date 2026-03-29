using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Microsoft.Data.Sqlite; // Necessário para SqliteConnection
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
 
    // O construtor recebe o DatabaseFixture, que já inicializou a conexão e o esquema
    public ConcurrencyTests(DatabaseFixture fixture)
    {
        _connection = fixture.Connection;
    }
 
    // Método para criar um novo contexto usando a conexão compartilhada
    private AppDbContext CriarContexto()
    {
        // Não precisamos mais de dbName, pois a conexão já define o banco de dados
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection) // Usa a conexão aberta e compartilhada
            .Options;
        var ctx = new AppDbContext(options);
        // EnsureCreated() é chamado uma única vez no construtor do DatabaseFixture
        return ctx;
    }
 
    private static OrderProcessor CriarProcessor(AppDbContext ctx) =>
        new(new OrderRepository(ctx), new TicketRepository(ctx),
            new PaymentRepository(ctx), new UnitOfWork(ctx),
            NullLogger<OrderProcessor>.Instance);

    [Fact]
    public async Task ProcessAsync_QuandoMultiplosPedidosParaMesmoTicket_ApenasUmDeveSerConfirmado()
    {
        await using var setupCtx = CriarContexto(); // Usa a conexão compartilhada

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
            // Usar 'await using' garante o dispose do contexto da tarefa
            await using var ctx = CriarContexto(); // Cada tarefa obtém seu próprio contexto, mas com a mesma conexão
            var processor = CriarProcessor(ctx);
            try
            {
                await processor.ProcessAsync(pedido.Id);
                // Se ProcessAsync completa sem exceção, o pedido foi Confirmado ou Falhou graciosamente.
                // Precisamos verificar o status final do pedido no DB para determinar o sucesso.
                var finalOrder = await ctx.Orders.FindAsync(pedido.Id);
                return finalOrder.Status == OrderStatus.Confirmed; // Retorna true se confirmado, false caso contrário
            }
            catch (DbUpdateConcurrencyException) { return false; } // Conflito de concorrência esperado
            catch (Exception ex)
            {
                // Exceção inesperada, re-lança para falhar o teste imediatamente
                throw new Xunit.Sdk.XunitException($"Exceção inesperada para o pedido {pedido.Id}: {ex.Message}", ex);
            }
        });

        var resultados = await Task.WhenAll(tarefas);

        resultados.Count(r => r).ShouldBe(1,
            "apenas um processo deve confirmar — lock otimista garante isso");
        resultados.Count(r => !r).ShouldBe(qtd - 1,
            "os demais devem falhar com DbUpdateConcurrencyException");
    }
 
    [Fact]
    public async Task ProcessAsync_AposConcorrencia_BancoDeveEstarConsistente()
    {
        // Arrange
        await using var setupCtx = CriarContexto(); // Usa a conexão compartilhada

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
            await using var ctx = CriarContexto(); // Cada tarefa obtém seu próprio contexto, mas com a mesma conexão
            var processor = CriarProcessor(ctx);
            try { await processor.ProcessAsync(pedido.Id); }
            catch (DbUpdateConcurrencyException) { /* Conflito de concorrência esperado */ }
            catch (Exception ex)
            {
                // Exceção inesperada, re-lança para falhar o teste imediatamente
                throw new Xunit.Sdk.XunitException($"Exceção inesperada para o pedido {pedido.Id}: {ex.Message}", ex);
            }
        });
        await Task.WhenAll(tarefas);

        // Assert
        await using var checkCtx = CriarContexto(); // Contexto para verificar o estado final
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