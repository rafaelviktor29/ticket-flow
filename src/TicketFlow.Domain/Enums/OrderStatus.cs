namespace TicketFlow.Domain.Enums;

public enum OrderStatus
{
    Pending,    // publicado na fila, aguardando processamento
    Processing, // sendo processado pelo consumer
    Confirmed,  // processado com sucesso
    Failed,     // falhou após retries
    Cancelled   // cancelado manualmente
}
