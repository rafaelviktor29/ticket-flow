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
        // Resolução do Erro
            var args = new Dictionary<string, object>
        {
            { "x-dead-letter-exchange", "orders.dlx" },
            { "x-dead-letter-routing-key", "orders.dead-letter" }
        };

        // Declara a fila de forma idempotente:
        // se já existir com os mesmos parâmetros, não faz nada.
        // durable: true  — fila sobrevive a reinicialização do RabbitMQ
        // exclusive: false — pode ser usada por múltiplas conexões
        // autoDelete: false — não exclui quando o consumer desconecta
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
