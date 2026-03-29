namespace TicketFlow.Application.Messaging;

public interface IMessagePublisher
{
    Task PublishAsync<T>(T message, string queueName, CancellationToken ct = default);
}
