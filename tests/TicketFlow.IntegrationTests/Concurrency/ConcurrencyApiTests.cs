using System.Net;
using System.Net.Http.Json;
using Shouldly;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;
using TicketFlow.Infrastructure.Persistence;
using TicketFlow.Infrastructure.Messaging; // Necessário para OrderProcessor (se OrderProcessor estiver em Infrastructure.Messaging)
using TicketFlow.Application.Messaging; // IMessagePublisher, OrderCreatedMessage
using TicketFlow.IntegrationTests.Fakes; // FakeMessagePublisher usado pela CustomWebApplicationFactory
using Microsoft.Extensions.DependencyInjection; // GetRequiredService / CreateScope
using Microsoft.EntityFrameworkCore; // ToListAsync, SingleOrDefaultAsync
using Xunit;

namespace TicketFlow.IntegrationTests.Concurrency;

public class ConcurrencyApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ConcurrencyApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task PostOrder_WhenMultipleOrdersForSameTicket_OnlyOneShouldBeConfirmed()
    {
        // Arrange: create an event and a ticket in the test database
        var eventId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var createEventRequest = new CreateEventRequest(
            "Show de Teste", "Local Teste", DateTime.UtcNow.AddDays(30), 1, 100m);

        // Use a separate context for initial setup
        await using (var setupCtx = _factory.CreateDbContext())
        {
            var @event = new Event(createEventRequest.Name, createEventRequest.Venue, createEventRequest.Date, createEventRequest.TotalTickets);
            @event.SetId(eventId); // Forçar o ID para ser consistente
            setupCtx.Events.Add(@event);

            var ticket = new Ticket(@event.Id, "A001", createEventRequest.TicketPrice);
            ticket.SetId(ticketId); // Forçar o ID para ser consistente
            setupCtx.Tickets.Add(ticket);
            await setupCtx.SaveChangesAsync();
        }

        const int numberOfConcurrentRequests = 5;
        var userIds = Enumerable.Range(0, numberOfConcurrentRequests)
            .Select(i => Guid.NewGuid())
            .ToList();

        // Act: send multiple POST /api/orders requests concurrently
        var tasks = userIds.Select(async userId =>
        {
            var idempotencyKey = $"test-key-{userId}";
            var request = new
            {
                ticketId = ticketId,
                userId = userId,
                idempotencyKey = idempotencyKey
            };
            var response = await _client.PostAsJsonAsync("/api/orders", request);
            return response;
        }).ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert: ensure all requests were accepted (202 Accepted)
        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.Accepted, "Todas as requisições devem ser aceitas (202 Accepted)");

        // Act: process messages manually because the hosted service does not run in integration tests
        // Obtain the FakeMessagePublisher and OrderProcessor from the factory's service provider to simulate processing
        var fakePublisher = _factory.Services.GetRequiredService<IMessagePublisher>() as FakeMessagePublisher;
        fakePublisher.ShouldNotBeNull();

        var processingTasks = new List<Task>();
        foreach (var message in fakePublisher.PublishedMessages)
        {
            // O OrderProcessor é Scoped, então precisamos criar um novo scope para cada processamento
            using var scope = _factory.Services.CreateScope();
            var orderProcessor = scope.ServiceProvider.GetRequiredService<OrderProcessor>();
            var orderMessage = message as OrderCreatedMessage; // Assumindo que a mensagem é OrderCreatedMessage
            if (orderMessage != null)
                processingTasks.Add(orderProcessor.ProcessAsync(orderMessage.OrderId)); // Adiciona a tarefa para execução concorrente
        }
        await Task.WhenAll(processingTasks);
        fakePublisher.ClearMessages(); // Limpa as mensagens para o próximo teste

        // Assert: verify final database state
        await using var assertCtx = _factory.CreateDbContext();
        var orders = await assertCtx.Orders.Where(o => userIds.Contains(o.UserId)).ToListAsync();
        var ticketAfterProcessing = await assertCtx.Tickets.FindAsync(ticketId);

        // There must be exactly one confirmed order
        orders.Count(o => o.Status == OrderStatus.Confirmed).ShouldBe(1, "Only one order should be confirmed");

        // The other orders must have failed
        orders.Count(o => o.Status == OrderStatus.Failed).ShouldBe(numberOfConcurrentRequests - 1, "The remaining orders should be marked as failed");

        // The ticket should be reserved
        ticketAfterProcessing.ShouldNotBeNull();
        ticketAfterProcessing.IsReserved.ShouldBeTrue("The ticket should be reserved by one of the orders");

        // A payment must have been created for the confirmed order
        var confirmedOrder = orders.Single(o => o.Status == OrderStatus.Confirmed);
        var payment = await assertCtx.Payments.SingleOrDefaultAsync(p => p.OrderId == confirmedOrder.Id);
        payment.ShouldNotBeNull($"There must be a payment for the confirmed order: {confirmedOrder.Id}");
        payment.Status.ShouldBe(PaymentStatus.Approved, "The payment for the confirmed order must be approved");
    }

    // Helper para criar eventos, já que o controller de eventos não usa o OrderProcessor
    // e é mais simples para o setup.
    private record CreateEventRequest(
        string Name,
        string Venue,
        DateTime Date,
        int TotalTickets,
        decimal TicketPrice
    );
}