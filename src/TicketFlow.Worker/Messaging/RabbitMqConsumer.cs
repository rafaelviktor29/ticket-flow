using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using TicketFlow.Infrastructure.Messaging; // OrderProcessor remains in Infrastructure

namespace TicketFlow.Worker.Messaging;

public class RabbitMqConsumer : BackgroundService
{
    private const string QueueName   = "orders";
    private const string DlxExchange = "orders.dlx";
    private const string DlqName     = "orders.dead-letter";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RabbitMqConsumer> _logger;
    private readonly IConnection _connection;
    private IModel? _channel;

    private readonly Polly.Retry.AsyncRetryPolicy _retryPolicy;

    public RabbitMqConsumer(
        IServiceScopeFactory scopeFactory,
        ILogger<RabbitMqConsumer> logger,
        IConnection connection)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
        _connection   = connection;

        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (ex, delay, attempt, _) =>
                    _logger.LogWarning(ex, "Retry {Attempt}/3 waiting for {Delay}s.", attempt, delay.TotalSeconds)
            );
    }

    protected override Task ExecuteAsync(CancellationToken ct)
    {
        _channel = _connection.CreateModel();

        // Dead-Letter Exchange
        _channel.ExchangeDeclare(DlxExchange, ExchangeType.Direct, durable: true);
        _channel.QueueDeclare(DlqName, durable: true, exclusive: false, autoDelete: false, arguments: null);
        _channel.QueueBind(DlqName, DlxExchange, routingKey: DlqName);

        _channel.QueueDeclare(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object>
            {
                { "x-dead-letter-exchange",    DlxExchange },
                { "x-dead-letter-routing-key", DlqName }
            }
        );

        _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.Received += async (_, ea) =>
        {
            var body    = Encoding.UTF8.GetString(ea.Body.ToArray());
            var payload = JsonSerializer.Deserialize<OrderMessage>(body);

            if (payload is null)
            {
                _logger.LogWarning("Invalid message. Sending to dead-letter.");
                _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                return;
            }

            _logger.LogInformation("Received order {OrderId}.", payload.OrderId);

            var result = await _retryPolicy.ExecuteAndCaptureAsync(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                // Resolve the OrderProcessor implementation from Infrastructure so we keep single implementation
                var processor = scope.ServiceProvider.GetRequiredService<TicketFlow.Infrastructure.Messaging.OrderProcessor>();
                await processor.ProcessAsync(payload.OrderId, ct);
            });

            if (result.Outcome == OutcomeType.Successful)
            {
                _channel.BasicAck(ea.DeliveryTag, multiple: false);
            }
            else
            {
                _logger.LogError(result.FinalException,
                    "Order {OrderId} failed after retries. Sending to dead-letter.", payload.OrderId);
                _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
            }
        };

        _channel.BasicConsume(queue: QueueName, autoAck: false, consumer: consumer);

        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _channel?.Close();
        _channel?.Dispose();
        base.Dispose();
    }

    private record OrderMessage(Guid OrderId);
}
