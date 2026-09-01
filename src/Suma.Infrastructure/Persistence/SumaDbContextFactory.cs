using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Suma.Infrastructure.Persistence;

public sealed class SumaDbContextFactory : IDesignTimeDbContextFactory<SumaDbContext>
{
    public SumaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SumaDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        return new SumaDbContext(options);
    }
}
