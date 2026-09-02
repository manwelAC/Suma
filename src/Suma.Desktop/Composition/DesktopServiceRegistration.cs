using Microsoft.Extensions.DependencyInjection;
using Suma.Desktop.Navigation;
using Suma.Desktop.Operations.Accounts;
using Suma.Desktop.Operations.Budgets;
using Suma.Desktop.Operations.Categories;
using Suma.Desktop.Operations.Transactions;
using Suma.Desktop.Operations.Recurring;
using Suma.Desktop.Operations.Savings;
using Suma.Desktop.Pages.Accounts;
using Suma.Desktop.Pages.Activity;
using Suma.Desktop.Pages.Overview;
using Suma.Desktop.Pages.Planning;
using Suma.Desktop.Pages.Settings;
using Suma.Desktop.Shell;
using Suma.Desktop.ViewModels;

namespace Suma.Desktop.Composition;

public static class DesktopServiceRegistration
{
    public static IServiceCollection AddDesktop(this IServiceCollection services)
    {
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<INavigationPageFactory, NavigationPageFactory>();
        services.AddSingleton<IAccountOperations, AccountOperations>();
        services.AddSingleton<ICategoryOperations, CategoryOperations>();
        services.AddSingleton<ITransactionOperations, TransactionOperations>();
        services.AddSingleton<IBudgetOperations, BudgetOperations>();
        services.AddSingleton<IRecurringOperations, RecurringOperations>();
        services.AddSingleton<ISavingsOperations, SavingsOperations>();
        services.AddSingleton<AccountsViewModel>();
        services.AddSingleton<CategoriesViewModel>();
        services.AddSingleton<ActivityViewModel>();
        services.AddSingleton<TransactionEditorViewModel>();
        services.AddSingleton<PlanningViewModel>();
        services.AddSingleton<BudgetEditorViewModel>();
        services.AddSingleton<RecurringViewModel>();
        services.AddSingleton<RecurringEditorViewModel>();
        services.AddSingleton<SavingsViewModel>();
        services.AddSingleton<SavingsGoalEditorViewModel>();
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<ShellPage>();
        services.AddSingleton<MainWindow>();

        services.AddSingleton<OverviewPage>();
        services.AddSingleton<AccountsPage>();
        services.AddSingleton<ActivityPage>();
        services.AddSingleton<PlanningPage>();
        services.AddSingleton<SettingsPage>();

        services.AddSingleton<Func<OverviewPage>>(provider => () => provider.GetRequiredService<OverviewPage>());
        services.AddSingleton<Func<AccountsPage>>(provider => () => provider.GetRequiredService<AccountsPage>());
        services.AddSingleton<Func<ActivityPage>>(provider => () => provider.GetRequiredService<ActivityPage>());
        services.AddSingleton<Func<PlanningPage>>(provider => () => provider.GetRequiredService<PlanningPage>());
        services.AddSingleton<Func<SettingsPage>>(provider => () => provider.GetRequiredService<SettingsPage>());

        return services;
    }
}
