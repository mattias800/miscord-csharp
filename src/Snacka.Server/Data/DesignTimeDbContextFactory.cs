using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Snacka.Server.Data;

/// <summary>
/// Factory for creating DbContext at design time for EF Core migrations.
/// This allows running migrations without starting the full application.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SnackaDbContext>
{
    public SnackaDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SnackaDbContext>();
        optionsBuilder.UseSqlite("Data Source=snacka.db");

        return new SnackaDbContext(optionsBuilder.Options);
    }
}
