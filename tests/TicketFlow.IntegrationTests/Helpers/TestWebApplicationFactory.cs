using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RabbitMQ.Client;
using TicketFlow.Application.Messaging;
using TicketFlow.Infrastructure.Messaging;
using TicketFlow.Infrastructure.Persistence;

namespace TicketFlow.IntegrationTests.Helpers;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Points to the correct API directory
        builder.UseContentRoot(Directory.GetCurrentDirectory());

        builder.ConfigureServices(services =>
        {
            // Remove PostgreSQL real
            var dbDesc = services.SingleOrDefault(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbDesc != null) services.Remove(dbDesc);
            // Creates a shared-memory SQLite connection that is kept open
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<AppDbContext>(o =>
                o.UseSqlite(_connection));

            // Remove IConnection real (RabbitMQ)
            var connDesc = services.SingleOrDefault(d =>
                d.ServiceType == typeof(IConnection));
            if (connDesc != null) services.Remove(connDesc);
            services.AddSingleton(new Mock<IConnection>().Object);

            // Remove publisher real
            var pubDesc = services.SingleOrDefault(d =>
                d.ServiceType == typeof(IMessagePublisher));
            if (pubDesc != null) services.Remove(pubDesc);
            services.AddSingleton<IMessagePublisher>(new FakeMessagePublisher());

            // Remove consumer background (implementation comes from Infrastructure)
            var consDesc = services.SingleOrDefault(d =>
                d.ImplementationType?.FullName == "TicketFlow.Infrastructure.Messaging.RabbitMqConsumer");
            if (consDesc != null) services.Remove(consDesc);

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Ensures the scheme is created using the open connection
            db.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection?.Close();
            _connection?.Dispose();
        }
        base.Dispose(disposing);
    }
}

public class FakeMessagePublisher : IMessagePublisher
{
    public List<(object Message, string Queue)> Published { get; } = new();

    public Task PublishAsync<T>(T message, string queueName, CancellationToken ct = default)
    {
        Published.Add((message!, queueName));
        return Task.CompletedTask;
    }
}