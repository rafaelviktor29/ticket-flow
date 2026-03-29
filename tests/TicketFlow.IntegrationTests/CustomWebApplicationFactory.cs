using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TicketFlow.Application.Messaging;
using TicketFlow.Infrastructure.Persistence;
using TicketFlow.IntegrationTests.Fakes; // Corrigido para o namespace correto
using System;
using System.Linq;

namespace TicketFlow.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove o DbContext de produção
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Remove RabbitMQ registrations que conectam recursos externos durante testes
            var connDesc = services.SingleOrDefault(d => d.ServiceType.FullName == "RabbitMQ.Client.IConnection");
            if (connDesc != null) services.Remove(connDesc);

            var publisherDesc = services.SingleOrDefault(d => d.ImplementationType?.FullName == "TicketFlow.Infrastructure.Messaging.RabbitMqPublisher");
            if (publisherDesc != null) services.Remove(publisherDesc);

            // Remove qualquer registro de IMessagePublisher para que possamos substituí-lo
            var msgDesc = services.SingleOrDefault(d => d.ServiceType == typeof(IMessagePublisher));
            if (msgDesc != null) services.Remove(msgDesc);

            // Remove serviços em segundo plano (IHostedService) para evitar o início de consumidores durante os testes
            var hosted = services.Where(d => d.ServiceType.FullName == "Microsoft.Extensions.Hosting.IHostedService").ToList();
            foreach (var h in hosted) services.Remove(h);

            // Do not remove OrderProcessor registration; only remove concrete messaging pieces that connect to external resources.

            // Cria e abre uma conexão SQLite em memória
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            // Adiciona o DbContext usando a conexão SQLite em memória
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            // Substitui o IMessagePublisher pelo FakeMessagePublisher
            services.AddSingleton<IMessagePublisher, FakeMessagePublisher>();

            // Garante que o esquema do banco de dados seja criado
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Database.EnsureCreated();
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

    // Método auxiliar para criar um DbContext para asserções no teste
    public AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new AppDbContext(options);
    }
}