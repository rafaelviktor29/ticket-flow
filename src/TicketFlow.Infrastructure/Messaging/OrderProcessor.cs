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

    // No changes to business logic — remains the processing unit used by both API tests and Worker consumers.

    public async Task ProcessAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, ct);

        if (order is null)
        {
            _logger.LogWarning("Pedido {OrderId} não encontrado. Ignorando.", orderId);
            return;
        }

        // Idempotência no processamento:
        // se o pedido já foi confirmado ou falhou, não reprocessa.
        if (order.Status is OrderStatus.Confirmed or OrderStatus.Failed)
        {
            _logger.LogInformation("Pedido {OrderId} já processado ({Status}). Ignorando.", orderId, order.Status);
            return;
        }

        order.MarkAsProcessing();
        await _unitOfWork.CommitAsync(ct);

        try
        {
            // Busca o ticket diretamente pelo id registrado no pedido.
            // Não usa AsNoTracking: o EF Core precisa rastrear o objeto
            // para detectar o conflito de RowVersion no SaveChanges.
            var ticket = await _ticketRepository.GetByIdAsync(order.TicketId, ct);

            if (ticket is null || ticket.IsReserved)
            {
                order.MarkAsFailed("Ingresso não disponível.");
                await _unitOfWork.CommitAsync(ct);
                return;
            }

            // Reserva o ingresso — zona crítica do lock otimista.
            // Se dois consumers chegarem aqui ao mesmo tempo com o mesmo ticket,
            // o primeiro salva e o RowVersion muda no banco.
            // O segundo tenta salvar com o RowVersion antigo, nenhuma linha é afetada,
            // e o EF Core lança DbUpdateConcurrencyException.
            ticket.Reserve(order.Id);

            // Cria e persiste o pagamento (simulado)
            var payment = new Payment(order.Id, ticket.Price);
            payment.Approve();
            await _paymentRepository.AddAsync(payment, ct);

            order.MarkAsConfirmed();

            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation("Pedido {OrderId} confirmado com sucesso.", orderId);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Conflito de concorrência no pedido {OrderId}. Ticket já reservado.", orderId);
            order.MarkAsFailed("Conflito de concorrência — ingresso reservado por outro processo.");

            try { await _unitOfWork.CommitAsync(ct); }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Erro ao salvar falha do pedido {OrderId}.", orderId);
            }

            // Relança para o consumer decidir se faz retry ou envia para dead-letter
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao processar pedido {OrderId}.", orderId);
            order.MarkAsFailed(ex.Message);

            try { await _unitOfWork.CommitAsync(ct); }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Erro ao salvar falha do pedido {OrderId}.", orderId);
            }

            throw;
        }
    }
}
