using Microsoft.Extensions.DependencyInjection;
using Suma.Desktop.Navigation;
using Suma.Desktop.Pages.Activity;
using Suma.Desktop.Pages.Overview;
using Suma.Desktop.Pages.Planning;
using Suma.Desktop.Pages.Settings;
using Suma.Desktop.Shell;

namespace Suma.Desktop.Composition;

public static class DesktopServiceRegistration
{
    public static IServiceCollection AddDesktop(this IServiceCollection services)
    {
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<ShellPage>();
        services.AddSingleton<MainWindow>();

        services.AddTransient<OverviewPage>();
        services.AddTransient<ActivityPage>();
        services.AddTransient<PlanningPage>();
        services.AddTransient<SettingsPage>();

        return services;
    }
}
