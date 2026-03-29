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
    /// Cria um pedido e o enfileira para processamento assíncrono.
    /// Retorna 202 imediatamente — consulte GET /orders/{id} para acompanhar o status.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]  // ticket não encontrado
    [ProducesResponseType(StatusCodes.Status400BadRequest)] // dados inválidos
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request, CancellationToken ct)
    {
        var order = await _orderService.CreateAsync(request, ct);
        return AcceptedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    /// <summary>
    /// Consulta o status de um pedido.
    /// Status possíveis: Pending → Processing → Confirmed ou Failed.
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
