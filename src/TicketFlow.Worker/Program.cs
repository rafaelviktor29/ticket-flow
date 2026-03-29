using Microsoft.EntityFrameworkCore;
using TicketFlow.Infrastructure;
using TicketFlow.Worker.Messaging;
using TicketFlow.Infrastructure.Persistence;

// Instrui o Npgsql a aceitar DateTime sem Kind definido tratando-os como UTC
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Infrastructure registrations (repos, rabbit connection, publisher)
        services.AddInfrastructureServices(context.Configuration);

        // DbContext pool for high-throughput workers
        services.AddDbContextPool<AppDbContext>(options =>
            options.UseNpgsql(context.Configuration.GetConnectionString("DefaultConnection")));

        // Application/processing layer hosted in Worker
        services.AddScoped<TicketFlow.Infrastructure.Messaging.OrderProcessor>();
        // Worker-local background service (consumer) uses the implementation in Worker.Messaging
        services.AddHostedService<TicketFlow.Worker.Messaging.RabbitMqConsumer>();
    });

var host = builder.Build();
await host.RunAsync();
