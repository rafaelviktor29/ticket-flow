using Microsoft.EntityFrameworkCore;
using TicketFlow.API.Middleware;
using TicketFlow.Application.Interfaces;
using TicketFlow.Application.Services;
using TicketFlow.Infrastructure;
using TicketFlow.Infrastructure.Messaging;
using TicketFlow.Infrastructure.Persistence;

// Increase the thread pool minimum to better handle concurrent load
ThreadPool.SetMinThreads(200, 200);

// Instruct Npgsql to treat DateTime without Kind as UTC
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// During tests the WebApplicationFactory may validate service scope
// relationships. Disable scope validation so the test host does not fail
// when test code replaces or removes services.
builder.Host.UseDefaultServiceProvider(opts => opts.ValidateScopes = false);

// Infrastructure shared registrations (DbContext, repositories, RabbitMQ connection/publisher)
// Note: RabbitMqConsumer is intentionally NOT registered here so the API does not host the consumer.
builder.Services.AddInfrastructureServices(builder.Configuration);

// DbContext pool for the API (smaller scale than workers but still pooled)
builder.Services.AddDbContextPool<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Application layer registrations
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<OrderProcessor>();

// ── API ───────────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
// Configure JSON options to serialize enums as strings globally
builder.Services.AddControllers().AddJsonOptions(opts =>
{
    opts.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// Automatic migrations on startup.
// In production prefer running migrations separately. For the thesis/demo this
// simplifies initial setup.
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