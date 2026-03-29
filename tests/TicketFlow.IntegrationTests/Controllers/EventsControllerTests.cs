using System.Net;
using System.Net.Http.Json;
using Shouldly;
using TicketFlow.API.Controllers;
using TicketFlow.IntegrationTests.Helpers;
using Xunit;

namespace TicketFlow.IntegrationTests.Controllers;

public class EventsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public EventsControllerTests(TestWebApplicationFactory factory)
        => _client = factory.CreateClient();

    [Fact]
    public async Task GetAll_QuandoNaoHaEventos_DeveRetornarListaVazia()
    {
        var response = await _client.GetAsync("/api/events");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<object>>();
        body.ShouldNotBeNull();
    }

    [Fact]
    public async Task Create_ComDadosValidos_DeveRetornar201EEventoCriado()
    {
        var request = new CreateEventRequest(
            "Show de Rock", "Estádio", DateTime.UtcNow.AddMonths(3), 3, 150m);

        var response = await _client.PostAsJsonAsync("/api/events", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<EventResponse>();
        body.ShouldNotBeNull();
        body!.Name.ShouldBe("Show de Rock");
        body.TotalTickets.ShouldBe(3);
    }

    [Fact]
    public async Task GetTickets_AposCriarEvento_DeveRetornarTicketsGerados()
    {
        var request = new CreateEventRequest(
            "Festival", "Arena", DateTime.UtcNow.AddMonths(2), 2, 100m);
        var createResponse = await _client.PostAsJsonAsync("/api/events", request);
        var evento         = await createResponse.Content.ReadFromJsonAsync<EventResponse>();

        var response = await _client.GetAsync($"/api/events/{evento!.Id}/tickets");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tickets = await response.Content.ReadFromJsonAsync<List<TicketResponse>>();
        tickets.ShouldNotBeNull();
        tickets!.Count.ShouldBe(2);
        tickets.ShouldAllBe(t => !t.IsReserved);
    }

    [Fact]
    public async Task GetById_QuandoEventoNaoExiste_DeveRetornar404()
    {
        var response = await _client.GetAsync($"/api/events/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private record EventResponse(Guid Id, string Name, string Venue, DateTime Date, int TotalTickets);
    private record TicketResponse(Guid Id, string SeatNumber, decimal Price, bool IsReserved);
}