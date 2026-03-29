using System.Collections.Concurrent;
using TicketFlow.Application.Messaging;
using System.Threading;
using System.Threading.Tasks;

namespace TicketFlow.IntegrationTests.Fakes;

public class FakeMessagePublisher : IMessagePublisher
{
    public ConcurrentBag<object> PublishedMessages { get; } = new();

    public Task PublishAsync<T>(T message, string queueName, CancellationToken ct = default)
    {
        PublishedMessages.Add(message!);
        return Task.CompletedTask;
    }

    public void ClearMessages() => PublishedMessages.Clear();
}
