using Microsoft.UI.Xaml.Controls;
using Suma.Desktop.Pages.Activity;
using Suma.Desktop.Pages.Accounts;
using Suma.Desktop.Pages.Overview;
using Suma.Desktop.Pages.Planning;
using Suma.Desktop.Pages.Settings;

namespace Suma.Desktop.Navigation;

public static class NavigationRouteMap
{
    private static readonly IReadOnlyDictionary<NavigationRoute, Type> Routes =
        new Dictionary<NavigationRoute, Type>
        {
            [NavigationRoute.Overview] = typeof(OverviewPage),
            [NavigationRoute.Accounts] = typeof(AccountsPage),
            [NavigationRoute.Activity] = typeof(ActivityPage),
            [NavigationRoute.Planning] = typeof(PlanningPage),
            [NavigationRoute.Settings] = typeof(SettingsPage)
        };

    public static IReadOnlyDictionary<NavigationRoute, Type> All => Routes;

    public static Type GetPageType(NavigationRoute route)
    {
        if (!Routes.TryGetValue(route, out var pageType) || !typeof(Page).IsAssignableFrom(pageType))
        {
            throw new ArgumentOutOfRangeException(nameof(route), route, "Navigation route is not supported.");
        }

        return pageType;
    }
}
