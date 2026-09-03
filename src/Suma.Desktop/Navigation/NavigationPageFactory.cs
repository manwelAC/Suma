using Microsoft.UI.Xaml.Controls;
using Suma.Desktop.Pages.Accounts;
using Suma.Desktop.Pages.Activity;
using Suma.Desktop.Pages.Overview;
using Suma.Desktop.Pages.Planning;
using Suma.Desktop.Pages.Reports;
using Suma.Desktop.Pages.Settings;

namespace Suma.Desktop.Navigation;

public sealed class NavigationPageFactory(
    Func<OverviewPage> overviewFactory,
    Func<AccountsPage> accountsFactory,
    Func<ActivityPage> activityFactory,
    Func<PlanningPage> planningFactory,
    Func<ReportsPage> reportsFactory,
    Func<SettingsPage> settingsFactory) : INavigationPageFactory
{
    private readonly Dictionary<NavigationRoute, Page> pages = [];

    public Page GetPage(NavigationRoute route)
    {
        if (pages.TryGetValue(route, out var existing))
        {
            return existing;
        }

        Page page = route switch
        {
            NavigationRoute.Overview => overviewFactory(),
            NavigationRoute.Accounts => accountsFactory(),
            NavigationRoute.Activity => activityFactory(),
            NavigationRoute.Planning => planningFactory(),
            NavigationRoute.Reports => reportsFactory(),
            NavigationRoute.Settings => settingsFactory(),
            _ => throw new ArgumentOutOfRangeException(nameof(route), route, "Navigation route is not supported.")
        };
        pages.Add(route, page);
        return page;
    }
}
