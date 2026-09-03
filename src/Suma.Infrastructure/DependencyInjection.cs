using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Suma.Application.Abstractions.Persistence;
using Suma.Application.Abstractions.Time;
using Suma.Application.Abstractions.Security;
using Suma.Infrastructure.Persistence;
using Suma.Infrastructure.Persistence.Stores;
using Suma.Infrastructure.Runtime;
using Suma.Infrastructure.Security;
using Suma.Infrastructure.Time;

namespace Suma.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var connectionString = SqliteRuntimeConnection.Build(databasePath);
        services.AddSingleton(new SumaRuntimePaths(Path.GetFullPath(databasePath)));
        services.AddDbContext<SumaDbContext>(options => options.UseSqlite(connectionString));

        services.AddScoped<IAccountStore, AccountStore>();
        services.AddScoped<ICategoryStore, CategoryStore>();
        services.AddScoped<ITransactionStore, TransactionStore>();
        services.AddScoped<IBudgetStore, BudgetStore>();
        services.AddScoped<IBudgetAllocationStore, BudgetAllocationStore>();
        services.AddScoped<IRecurringTransactionStore, RecurringTransactionStore>();
        services.AddScoped<IRecurringOccurrenceStore, RecurringOccurrenceStore>();
        services.AddScoped<ISavingsGoalStore, SavingsGoalStore>();
        services.AddScoped<IOverviewStore, OverviewStore>();
        services.AddScoped<IReportStore, ReportStore>();
        services.AddScoped<IFinanceBackupStore, FinanceBackupStore>();
        services.AddSingleton<ISecuritySettingsStore, JsonSecuritySettingsStore>();
        services.AddScoped<IGoalContributionStore, GoalContributionStore>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        services.AddSingleton<IDateProvider, SystemDateProvider>();
        services.AddSingleton<IDatabaseInitializer, DatabaseInitializer>();
        services.AddSingleton<IPendingRestoreApplier, PendingRestoreApplier>();

        return services;
    }
}
