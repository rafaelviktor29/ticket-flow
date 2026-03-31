using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using TicketFlow.Application.Messaging;

namespace TicketFlow.Infrastructure.Messaging;

public class RabbitMqPublisher : IMessagePublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;

    public RabbitMqPublisher(IConnection connection)
    {
        _connection = connection;
        _channel = _connection.CreateModel();
    }

    public Task PublishAsync<T>(T message, string queueName, CancellationToken ct = default)
    {
        // Configure dead-lettering for the queue
        var args = new Dictionary<string, object>
        {
            { "x-dead-letter-exchange", "orders.dlx" },
            { "x-dead-letter-routing-key", "orders.dead-letter" }
        };

        // Declare the queue idempotently: if it already exists with the same
        // parameters, this is a no-op.
        // durable: true  — surviving RabbitMQ restart
        // exclusive: false — usable by multiple connections
        // autoDelete: false — not deleted when consumer disconnects
        _channel.QueueDeclare(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: args
        );

        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        var props = _channel.CreateBasicProperties();
        // persistent: true — mensagem sobrevive a reinicialização do RabbitMQ
        props.Persistent = true;

        _channel.BasicPublish(
            exchange: "",
            routingKey: queueName,
            basicProperties: props,
            body: body
        );

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
    }
}
