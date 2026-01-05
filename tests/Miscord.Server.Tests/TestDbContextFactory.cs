using Microsoft.EntityFrameworkCore;
using Miscord.Server.Data;

namespace Miscord.Server.Tests;

public static class TestDbContextFactory
{
    public static MiscordDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<MiscordDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var context = new MiscordDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
