using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Suma.Desktop;
using Suma.Desktop.Composition;
using Suma.Desktop.Navigation;
using Suma.Desktop.Shell;
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
    public void Desktop_registration_graph_is_root_safe_and_validated()
    {
        var services = new ServiceCollection();
        services.AddDesktop();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        Assert.NotNull(provider.GetRequiredService<INavigationService>());
        Assert.NotNull(provider.GetRequiredService<ShellViewModel>());
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
}
