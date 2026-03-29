using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TicketFlow.API.Controllers;
using TicketFlow.Application.DTOs;
using TicketFlow.Application.Messaging;
using TicketFlow.Domain.Enums;
using TicketFlow.IntegrationTests.Helpers;
using Xunit;

namespace TicketFlow.IntegrationTests.Controllers;

public class OrdersControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public OrdersControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    private async Task<Guid> CriarTicketAsync()
    {
        var res    = await _client.PostAsJsonAsync("/api/events",
            new CreateEventRequest("Evento", "Local", DateTime.UtcNow.AddMonths(1), 3, 100m));
        var evento = await res.Content.ReadFromJsonAsync<EventResponse>();
        var ticketsRes = await _client.GetAsync($"/api/events/{evento!.Id}/tickets");
        var tickets    = await ticketsRes.Content.ReadFromJsonAsync<List<TicketResponse>>();
        return tickets!.First().Id;
    }

    [Fact]
    public async Task Create_ComDadosValidos_DeveRetornar202EPedidoPending()
    {
        var ticketId = await CriarTicketAsync();
        var request  = new CreateOrderRequest(ticketId, Guid.NewGuid(), $"chave-{Guid.NewGuid()}");

        var response = await _client.PostAsJsonAsync("/api/orders", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();
        order.ShouldNotBeNull();
        order!.Status.ShouldBe(OrderStatus.Pending);
        order.TicketId.ShouldBe(ticketId);
    }

    [Fact]
    public async Task Create_ComMesmaChaveIdempotencia_DeveRetornarMesmoPedido()
    {
        var ticketId = await CriarTicketAsync();
        var chave    = $"chave-{Guid.NewGuid()}";
        var request  = new CreateOrderRequest(ticketId, Guid.NewGuid(), chave);

        var r1 = await _client.PostAsJsonAsync("/api/orders", request);
        var r2 = await _client.PostAsJsonAsync("/api/orders", request);
        var o1 = await r1.Content.ReadFromJsonAsync<OrderResponse>();
        var o2 = await r2.Content.ReadFromJsonAsync<OrderResponse>();

        r1.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        r2.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        o1!.Id.ShouldBe(o2!.Id);
    }

    [Fact]
    public async Task Create_ComTicketInexistente_DeveRetornar404()
    {
        var request = new CreateOrderRequest(Guid.NewGuid(), Guid.NewGuid(), $"chave-{Guid.NewGuid()}");

        var response = await _client.PostAsJsonAsync("/api/orders", request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_ComPedidoExistente_DeveRetornar200()
    {
        var ticketId     = await CriarTicketAsync();
        var createRes    = await _client.PostAsJsonAsync("/api/orders",
            new CreateOrderRequest(ticketId, Guid.NewGuid(), $"chave-{Guid.NewGuid()}"));
        var pedidoCriado = await createRes.Content.ReadFromJsonAsync<OrderResponse>();

        var response = await _client.GetAsync($"/api/orders/{pedidoCriado!.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();
        order!.Id.ShouldBe(pedidoCriado.Id);
    }

    [Fact]
    public async Task GetById_ComPedidoInexistente_DeveRetornar404()
    {
        var response = await _client.GetAsync($"/api/orders/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_DevePublicarMensagemNaFila()
    {
        var ticketId = await CriarTicketAsync();
        var request  = new CreateOrderRequest(ticketId, Guid.NewGuid(), $"chave-{Guid.NewGuid()}");

        await _client.PostAsJsonAsync("/api/orders", request);

        var publisher = _factory.Services
            .GetRequiredService<IMessagePublisher>() as FakeMessagePublisher;
        publisher!.Published.ShouldNotBeEmpty();
        publisher.Published.Last().Queue.ShouldBe("orders");
    }

    private record EventResponse(Guid Id, string Name);
    private record TicketResponse(Guid Id, string SeatNumber, decimal Price, bool IsReserved);
}