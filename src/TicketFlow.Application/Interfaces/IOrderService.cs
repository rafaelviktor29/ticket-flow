using TicketFlow.Application.DTOs;

namespace TicketFlow.Application.Interfaces;

public interface IOrderService
{
    Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken ct = default);
    Task<OrderResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
