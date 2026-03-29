using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using TicketFlow.Infrastructure.Messaging;
using TicketFlow.Domain.Interfaces;
using TicketFlow.Infrastructure.Persistence;
using TicketFlow.Infrastructure.Persistence.Repositories;

namespace TicketFlow.Infrastructure;

public static class DependencyInjection
{
    // Registers infra services shared by API and Worker (repos, unit of work, messaging publisher, RabbitMQ connection)
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Repositories / UoW
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // RabbitMQ connection: single shared connection per process
        services.AddSingleton<IConnection>(_ =>
        {
            var cfg = configuration.GetSection("RabbitMQ");
            var factory = new ConnectionFactory
            {
                HostName = cfg["Host"]!,
                Port = int.Parse(cfg["Port"]!),
                UserName = cfg["Username"]!,
                Password = cfg["Password"]!,
                DispatchConsumersAsync = true
            };
            return factory.CreateConnection();
        });

        // Publisher lives in Infrastructure and depends on IConnection
        services.AddSingleton<RabbitMqPublisher>();
        services.AddSingleton<TicketFlow.Application.Messaging.IMessagePublisher>(sp => sp.GetRequiredService<RabbitMqPublisher>());

        return services;
    }
}
