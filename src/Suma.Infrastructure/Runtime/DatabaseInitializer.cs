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
            await context.Database.ExecuteSqlRawAsync(
                """
                UPDATE recurring_transactions SET frequency_unit = 'Month' WHERE frequency_unit = 'Monthly';
                UPDATE recurring_transactions SET frequency_unit = 'Day' WHERE frequency_unit = 'Daily';
                UPDATE recurring_transactions SET frequency_unit = 'Week' WHERE frequency_unit = 'Weekly';
                UPDATE recurring_transactions SET frequency_unit = 'Year' WHERE frequency_unit IN ('Yearly', 'Annual', 'Annually');
                """,
                cancellationToken);
            logger.LogInformation("Suma database migration completed successfully.");
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "Suma database migration failed. Application startup cannot continue.");
            throw;
        }
    }
}
