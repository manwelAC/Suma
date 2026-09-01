using Microsoft.UI.Xaml.Controls;

namespace Suma.Desktop.Navigation;

public sealed class NavigationService : INavigationService
{
    private Frame? frame;

    public NavigationRoute? CurrentRoute { get; private set; }

    public event EventHandler<NavigationRoute>? Navigated;

    public void Attach(Frame navigationFrame)
    {
        ArgumentNullException.ThrowIfNull(navigationFrame);

        if (frame is not null && !ReferenceEquals(frame, navigationFrame))
        {
            throw new InvalidOperationException("Navigation is already attached to a different shell Frame.");
        }

        frame = navigationFrame;
    }

    public bool Navigate(NavigationRoute route)
    {
        var pageType = NavigationRouteMap.GetPageType(route);
        var targetFrame = frame ?? throw new InvalidOperationException("Navigation must be attached to the shell Frame first.");
        if (CurrentRoute == route && targetFrame.Content?.GetType() == pageType)
        {
            return false;
        }

        if (!targetFrame.Navigate(pageType))
        {
            return false;
        }

        CurrentRoute = route;
        Navigated?.Invoke(this, route);
        return true;
    }
}
