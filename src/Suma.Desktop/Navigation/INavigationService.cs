using Microsoft.UI.Xaml.Controls;

namespace Suma.Desktop.Navigation;

public interface INavigationService
{
    NavigationRoute? CurrentRoute { get; }

    event EventHandler<NavigationRoute>? Navigated;

    void Attach(Frame frame);

    bool Navigate(NavigationRoute route);
}
