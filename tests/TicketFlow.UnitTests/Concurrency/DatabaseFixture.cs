using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TicketFlow.Infrastructure.Persistence;
using System;

namespace TicketFlow.UnitTests.Concurrency;

public class DatabaseFixture : IDisposable
{
    public SqliteConnection Connection { get; private set; }

    public DatabaseFixture()
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(Connection).Options;
        using (var context = new AppDbContext(options))
        {
            context.Database.EnsureCreated(); // Cria o esquema do banco de dados uma única vez
        }
    }

    public void Dispose() => Connection.Dispose();
}