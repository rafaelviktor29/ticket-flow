using Microsoft.AspNetCore.Mvc;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Interfaces;

namespace TicketFlow.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly IEventRepository _eventRepository;
    private readonly ITicketRepository _ticketRepository;
    private readonly IUnitOfWork _unitOfWork;

    public EventsController(
        IEventRepository eventRepository,
        ITicketRepository ticketRepository,
        IUnitOfWork unitOfWork)
    {
        _eventRepository = eventRepository;
        _ticketRepository = ticketRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>Lista todos os eventos.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Event>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var events = await _eventRepository.GetAllAsync(ct);
        return Ok(events);
    }

    /// <summary>Retorna um evento pelo ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Event), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var @event = await _eventRepository.GetByIdAsync(id, ct);
        return @event is null ? NotFound() : Ok(@event);
    }

    /// <summary>Cria um evento e gera os ingressos automaticamente.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Event), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateEventRequest request, CancellationToken ct)
    {
        var @event = new Event(request.Name, request.Venue, request.Date, request.TotalTickets);
        await _eventRepository.AddAsync(@event, ct);

        // Gera os ingressos automaticamente para o evento
        var tickets = Enumerable.Range(1, request.TotalTickets)
            .Select(i => new Ticket(@event.Id, $"A{i:D3}", request.TicketPrice));

        await _ticketRepository.AddRangeAsync(tickets, ct);
        await _unitOfWork.CommitAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = @event.Id }, @event);
    }

    /// <summary>Lista todos os ingressos de um evento.</summary>
    [HttpGet("{id:guid}/tickets")]
    [ProducesResponseType(typeof(IEnumerable<Ticket>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTickets(Guid id, CancellationToken ct)
    {
        var tickets = await _ticketRepository.GetByEventIdAsync(id, ct);
        return Ok(tickets);
    }
}

public record CreateEventRequest(
    string Name,
    string Venue,
    DateTime Date,
    int TotalTickets,
    decimal TicketPrice
);
