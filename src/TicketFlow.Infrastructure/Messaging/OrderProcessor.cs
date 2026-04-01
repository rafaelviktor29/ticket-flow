using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;
using TicketFlow.Domain.Interfaces;

namespace TicketFlow.Infrastructure.Messaging;

public class OrderProcessor
{
    private readonly IOrderRepository _orderRepository;
    private readonly ITicketRepository _ticketRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OrderProcessor> _logger;

    public OrderProcessor(
        IOrderRepository orderRepository,
        ITicketRepository ticketRepository,
        IPaymentRepository paymentRepository,
        IUnitOfWork unitOfWork,
        ILogger<OrderProcessor> logger)
    {
        _orderRepository = orderRepository;
        _ticketRepository = ticketRepository;
        _paymentRepository = paymentRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // Processing unit shared by API tests and Worker consumers. Business logic is unchanged.
    public async Task ProcessAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, ct);

        if (order is null)
        {
            _logger.LogWarning("Order {OrderId} not found. Ignoring.", orderId);
            return;
        }

        // Processing idempotency: if order was already confirmed or failed, do not reprocess.
        if (order.Status is OrderStatus.Confirmed or OrderStatus.Failed)
        {
            _logger.LogInformation("Order {OrderId} already processed ({Status}). Ignoring.", orderId, order.Status);
            return;
        }

        order.MarkAsProcessing();
        await _unitOfWork.CommitAsync(ct);

        try
        {
            // Load the ticket by id registered on the order.
            // Do not use AsNoTracking: EF Core must track the entity to detect
            // concurrency token changes on SaveChanges.
            var ticket = await _ticketRepository.GetByIdAsync(order.TicketId, ct);

            if (ticket is null || ticket.IsReserved)
            {
                order.MarkAsFailed("Ticket not available.");
                await _unitOfWork.CommitAsync(ct);
                return;
            }

            // Reserve the ticket — optimistic concurrency critical section.
            // If two consumers reach this point for the same ticket, the first
            // will save and change the concurrency token. The second will attempt
            // to save with the old token, no rows will be affected and EF Core
            // throws DbUpdateConcurrencyException.
            ticket.Reserve(order.Id);

            // Create and persist the payment (simulated)
            var payment = new Payment(order.Id, ticket.Price);
            payment.Approve();
            await _paymentRepository.AddAsync(payment, ct);

            order.MarkAsConfirmed();

            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation("Order {OrderId} successfully confirmed.", orderId);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Conflict of interest in order {OrderId}. Ticket already reserved.", orderId);
            order.MarkAsFailed("Conflict of interest — ticket reserved by another process.");

            try { await _unitOfWork.CommitAsync(ct); }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Error saving order failure {OrderId}.", orderId);
            }

            // Re-throw to allow the consumer to decide whether to retry or send to dead-letter
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while processing order {OrderId}.", orderId);
            order.MarkAsFailed(ex.Message);

            try { await _unitOfWork.CommitAsync(ct); }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Error saving order failure {OrderId}.", orderId);
            }

            throw;
        }
    }
}
