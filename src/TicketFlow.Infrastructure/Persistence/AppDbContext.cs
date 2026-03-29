using Microsoft.EntityFrameworkCore;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Event> Events => Set<Event>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Event
        modelBuilder.Entity<Event>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Venue).IsRequired().HasMaxLength(200);
            e.HasMany(x => x.Tickets).WithOne(t => t.Event).HasForeignKey(t => t.EventId);
        });

        // Ticket
        modelBuilder.Entity<Ticket>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.SeatNumber).IsRequired().HasMaxLength(20);
            e.Property(x => x.Price).HasColumnType("decimal(10,2)");

            // ConcurrencyToken: EF Core inclui no WHERE do UPDATE.
            // Funciona tanto no PostgreSQL quanto no SQLite (testes).
            e.Property(x => x.ConcurrencyToken)
            .IsConcurrencyToken();
        });

        // Order
        modelBuilder.Entity<Order>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(100);
            // Índice único na IdempotencyKey — garante no banco que não existam dois pedidos com a mesma chave
            e.HasIndex(x => x.IdempotencyKey).IsUnique();
            e.Property(x => x.Status).HasConversion<string>();
            e.HasOne(x => x.Ticket).WithMany().HasForeignKey(x => x.TicketId);
        });

        // Payment
        modelBuilder.Entity<Payment>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasColumnType("decimal(10,2)");
            e.Property(x => x.Status).HasConversion<string>();
            e.HasOne(x => x.Order).WithOne(o => o.Payment)
             .HasForeignKey<Payment>(x => x.OrderId);
        });
    }
}
