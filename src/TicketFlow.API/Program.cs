using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using TicketFlow.API.Middleware;
using TicketFlow.Application.Interfaces;
using TicketFlow.Application.Messaging;
using TicketFlow.Application.Services;
using TicketFlow.Domain.Interfaces;
using TicketFlow.Infrastructure;
using TicketFlow.Infrastructure.Persistence;
using TicketFlow.Infrastructure.Messaging;

// Aumenta o pool de threads para suportar carga concorrente
ThreadPool.SetMinThreads(200, 200);

// Instrui o Npgsql a aceitar DateTime sem Kind definido tratando-os como UTC
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// During tests the WebApplicationFactory builds the host and may validate
// service scope relationships. Disable scope validation here to keep the
// test host from failing when test code replaces or removes services.
builder.Host.UseDefaultServiceProvider(opts => opts.ValidateScopes = false);

// ── Infrastructure shared registrations (DbContext, repos, RabbitMQ connection/publisher)
// Note: RabbitMqConsumer is intentionally NOT registered here so API doesn't host the consumer.
builder.Services.AddInfrastructureServices(builder.Configuration);

// DbContext pool for API (smaller scale than workers but still pooled)
builder.Services.AddDbContextPool<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Application layer registrations
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<OrderProcessor>();

// ── API ───────────────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Ignora referências circulares ao serializar
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// ── Migrations automáticas ao iniciar ────────────────────────────────────────
// Em produção, prefira rodar migrations separadamente.
// Para o TCC, isso facilita o setup inicial.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // Só executa migrations automáticas para o provedor PostgreSQL.
    // Em ambientes de teste usamos SQLite em memória e as migrations geradas para Postgres
    // podem conter SQL incompatível com SQLite, causando falhas na inicialização.
    var provider = db.Database.ProviderName;
    if (provider == "Npgsql.EntityFrameworkCore.PostgreSQL")
    {
        await db.Database.MigrateAsync();
    }
    else
    {
        // Para outros provedores (ex.: SQLite em memória dos testes), garante o esquema
        await db.Database.EnsureCreatedAsync();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();
app.MapControllers();
app.Run();

public partial class Program { }