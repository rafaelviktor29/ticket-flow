using TicketFlow.Application.DTOs;
using TicketFlow.Application.Exceptions;
using TicketFlow.Application.Interfaces;
using TicketFlow.Application.Messaging;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Interfaces;

namespace TicketFlow.Application.Services;

public class OrderService : IOrderService
{
    private const string OrdersQueue = "orders";

    private readonly IOrderRepository _orderRepository;
    private readonly ITicketRepository _ticketRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMessagePublisher _publisher;

    public OrderService(
        IOrderRepository orderRepository,
        ITicketRepository ticketRepository,
        IUnitOfWork unitOfWork,
        IMessagePublisher publisher)
    {
        _orderRepository = orderRepository;
        _ticketRepository = ticketRepository;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
    }

    public async Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken ct = default)
    {
        // Idempotency: if an order with this key already exists, return it without reprocessing
        var existing = await _orderRepository.GetByIdempotencyKeyAsync(request.IdempotencyKey, ct);
        if (existing is not null)
            return ToResponse(existing);

        // Validate that the ticket exists
        var ticket = await _ticketRepository.GetByIdAsync(request.TicketId, ct);
        if (ticket is null)
            throw new NotFoundException($"Ticket {request.TicketId} não encontrado.");

        // Create the order with Pending status
        var order = new Order(request.TicketId, request.UserId, request.IdempotencyKey);
        await _orderRepository.AddAsync(order, ct);
        await _unitOfWork.CommitAsync(ct);

        // Publish to the queue for asynchronous processing using a typed message
        await _publisher.PublishAsync(new OrderCreatedMessage { OrderId = order.Id }, OrdersQueue, ct);

        return ToResponse(order);
    }

    public async Task<OrderResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var order = await _orderRepository.GetByIdAsync(id, ct);
        return order is null ? null : ToResponse(order);
    }

    private static OrderResponse ToResponse(Order order) => new(
        order.Id,
        order.TicketId,
        order.UserId,
        order.Status.ToString(),
        order.IdempotencyKey,
        order.FailureReason,
        order.CreatedAt,
        order.ProcessedAt
    );
}
