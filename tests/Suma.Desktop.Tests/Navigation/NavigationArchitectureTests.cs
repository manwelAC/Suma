using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Suma.Desktop;
using Suma.Desktop.Composition;
using Suma.Desktop.Navigation;
using Suma.Desktop.Operations.Accounts;
using Suma.Desktop.Operations.Budgets;
using Suma.Desktop.Operations.Categories;
using Suma.Desktop.Operations.Transactions;
using Suma.Desktop.Operations.Recurring;
using Suma.Desktop.Operations.Savings;
using Suma.Desktop.Operations.Overview;
using Suma.Desktop.Operations.Reports;
using Suma.Desktop.Pages.Accounts;
using Suma.Desktop.Pages.Reports;
using Suma.Desktop.Shell;
using Suma.Desktop.ViewModels;
using Xunit;

namespace Suma.Desktop.Tests.Navigation;

public sealed class NavigationArchitectureTests
{
    [Fact]
    public void Every_route_maps_to_one_distinct_known_page_type()
    {
        var routes = Enum.GetValues<NavigationRoute>();

        Assert.Equal(routes.Length, NavigationRouteMap.All.Count);
        Assert.Equal(routes.Length, NavigationRouteMap.All.Values.Distinct().Count());
        foreach (var route in routes)
        {
            Assert.True(typeof(Page).IsAssignableFrom(NavigationRouteMap.GetPageType(route)));
        }
    }

    [Fact]
    public void Unsupported_route_is_rejected_explicitly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NavigationRouteMap.GetPageType((NavigationRoute)999));
    }

    [Fact]
    public void Accounts_route_maps_explicitly_to_accounts_page()
    {
        Assert.Equal(typeof(AccountsPage), NavigationRouteMap.GetPageType(NavigationRoute.Accounts));
    }

    [Fact]
    public void Desktop_registration_graph_is_root_safe_and_validated()
    {
        var services = new ServiceCollection();
        services.AddDesktop();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        Assert.NotNull(provider.GetRequiredService<INavigationService>());
        Assert.NotNull(provider.GetRequiredService<ShellViewModel>());
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(INavigationPageFactory));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAccountOperations));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ICategoryOperations));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ITransactionOperations));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IBudgetOperations));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRecurringOperations));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ISavingsOperations));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IOverviewOperations));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(OverviewViewModel));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IReportOperations));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ReportsViewModel));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ReportsPage));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(AccountsViewModel));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(CategoriesViewModel));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ActivityViewModel));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(PlanningViewModel));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(RecurringViewModel));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(SavingsViewModel));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(AccountsPage));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ShellPage));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(MainWindow));
    }

    [Fact]
    public void Navigation_service_is_one_UI_only_singleton()
    {
        var services = new ServiceCollection();
        services.AddDesktop();
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<INavigationService>();
        var second = provider.GetRequiredService<INavigationService>();

        Assert.Same(first, second);
        var dependencyTypes = typeof(NavigationService)
            .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Select(field => field.FieldType)
            .ToArray();
        Assert.DoesNotContain(dependencyTypes, type => type.Name.Contains("DbContext", StringComparison.Ordinal));
        Assert.DoesNotContain(dependencyTypes, type => type.Name.Contains("IServiceScope", StringComparison.Ordinal));
        Assert.DoesNotContain(dependencyTypes, type => type.Namespace?.StartsWith("Suma.Application", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void Shell_view_model_retains_only_navigation_state()
    {
        var constructor = Assert.Single(typeof(ShellViewModel).GetConstructors());
        var parameter = Assert.Single(constructor.GetParameters());

        Assert.Equal(typeof(INavigationService), parameter.ParameterType);
    }

    [Theory]
    [InlineData(typeof(AccountsViewModel), typeof(IAccountOperations))]
    [InlineData(typeof(CategoriesViewModel), typeof(ICategoryOperations))]
    [InlineData(typeof(ActivityViewModel), typeof(ITransactionOperations))]
    [InlineData(typeof(PlanningViewModel), typeof(IBudgetOperations))]
    [InlineData(typeof(RecurringViewModel), typeof(IRecurringOperations))]
    [InlineData(typeof(SavingsViewModel), typeof(ISavingsOperations))]
    [InlineData(typeof(OverviewViewModel), typeof(IOverviewOperations))]
    public void Finance_view_models_retain_only_focused_root_safe_operations(Type viewModelType, Type operationType)
    {
        var constructor = Assert.Single(viewModelType.GetConstructors());
        var parameter = Assert.Single(constructor.GetParameters());

        Assert.Equal(operationType, parameter.ParameterType);
        Assert.DoesNotContain(
            viewModelType.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic),
            field => field.FieldType == typeof(IServiceProvider)
                || field.FieldType == typeof(IServiceScopeFactory)
                || field.FieldType.Namespace?.StartsWith("Suma.Application", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void Reports_view_model_depends_only_on_report_operations_and_retains_no_services()
    {
        var constructor = Assert.Single(typeof(ReportsViewModel).GetConstructors());
        Assert.Equal(typeof(IReportOperations), Assert.Single(constructor.GetParameters()).ParameterType);
        Assert.DoesNotContain(typeof(ReportsViewModel).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic),
            field => field.FieldType == typeof(IServiceProvider) || field.FieldType == typeof(IServiceScopeFactory) || field.FieldType.Name.EndsWith("UseCase", StringComparison.Ordinal) || field.FieldType.Name.EndsWith("Store", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(typeof(AccountOperations))]
    [InlineData(typeof(CategoryOperations))]
    [InlineData(typeof(TransactionOperations))]
    [InlineData(typeof(BudgetOperations))]
    [InlineData(typeof(RecurringOperations))]
    [InlineData(typeof(SavingsOperations))]
    [InlineData(typeof(OverviewOperations))]
    [InlineData(typeof(ReportOperations))]
    public void Finance_operation_adapters_hold_scope_factory_not_scoped_finance_services(Type adapterType)
    {
        var fields = adapterType.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.Contains(fields, field => field.FieldType == typeof(IServiceScopeFactory));
        Assert.DoesNotContain(
            fields,
            field => field.FieldType.Namespace?.StartsWith("Suma.Application", StringComparison.Ordinal) == true);
    }
}
