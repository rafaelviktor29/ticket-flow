using Microsoft.AspNetCore.Mvc;
using TicketFlow.Application.DTOs;
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

    /// <summary>Lists all events.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EventResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var events = await _eventRepository.GetAllAsync(ct);
        var dtos = events.Select(e => new EventResponse(e.Id, e.Name, e.Venue, e.Date, e.TotalTickets));
        return Ok(dtos);
    }

    /// <summary>Returns an event by ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var evt = await _eventRepository.GetByIdAsync(id, ct);
        return evt is null ? NotFound() : Ok(new EventResponse(evt.Id, evt.Name, evt.Venue, evt.Date, evt.TotalTickets));
    }

    /// <summary>Creates an event and generates tickets automatically.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateEventRequest request, CancellationToken ct)
    {
        var evt = new Event(request.Name, request.Venue, request.Date, request.TotalTickets);
        await _eventRepository.AddAsync(evt, ct);

        // It automatically generates tickets for the event
        var tickets = Enumerable.Range(1, request.TotalTickets)
            .Select(i => new Ticket(evt.Id, $"A{i:D3}", request.TicketPrice));

        await _ticketRepository.AddRangeAsync(tickets, ct);
        await _unitOfWork.CommitAsync(ct);

        var dto = new EventResponse(evt.Id, evt.Name, evt.Venue, evt.Date, evt.TotalTickets);
        return CreatedAtAction(nameof(GetById), new { id = evt.Id }, dto);
    }

    /// <summary>Lists all tickets for an event.</summary>
    [HttpGet("{id:guid}/tickets")]
    [ProducesResponseType(typeof(IEnumerable<TicketResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTickets(Guid id, CancellationToken ct)
    {
        var tickets = await _ticketRepository.GetByEventIdAsync(id, ct);
        var dtos = tickets.Select(t => new TicketResponse(t.Id, t.SeatNumber, t.Price, t.IsReserved));
        return Ok(dtos);
    }
}