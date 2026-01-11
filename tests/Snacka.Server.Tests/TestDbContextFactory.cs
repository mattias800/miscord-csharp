using Microsoft.EntityFrameworkCore;
using Snacka.Server.Data;

namespace Snacka.Server.Tests;

public static class TestDbContextFactory
{
    public static SnackaDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<SnackaDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var context = new SnackaDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
