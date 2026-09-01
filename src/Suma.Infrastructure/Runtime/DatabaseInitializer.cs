using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Suma.Infrastructure.Persistence;

namespace Suma.Infrastructure.Runtime;

public sealed class DatabaseInitializer(
    IServiceScopeFactory scopeFactory,
    ILogger<DatabaseInitializer> logger) : IDatabaseInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting Suma database migration.");

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<SumaDbContext>();
            await context.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("Suma database migration completed successfully.");
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "Suma database migration failed. Application startup cannot continue.");
            throw;
        }
    }
}
