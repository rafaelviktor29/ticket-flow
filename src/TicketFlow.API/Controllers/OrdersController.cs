using Microsoft.AspNetCore.Mvc;
using TicketFlow.Application.DTOs;
using TicketFlow.Application.Interfaces;

namespace TicketFlow.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    /// <summary>
    /// Creates an order and enqueues it for asynchronous processing.
    /// Returns 202 Accepted immediately — use GET /orders/{id} to track status.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request, CancellationToken ct)
    {
        var order = await _orderService.CreateAsync(request, ct);
        return AcceptedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    /// <summary>
    /// Retrieves the status of an order.
    /// Possible statuses: Pending → Processing → Confirmed or Failed.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var order = await _orderService.GetByIdAsync(id, ct);
        return order is null ? NotFound() : Ok(order);
    }
}
