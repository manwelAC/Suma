using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Suma.Desktop.Navigation;

namespace Suma.Desktop.Shell;

public sealed partial class ShellPage : Page
{
    private readonly INavigationService navigationService;

    public ShellPage(ShellViewModel viewModel, INavigationService navigationService)
    {
        ViewModel = viewModel;
        this.navigationService = navigationService;
        InitializeComponent();
        DataContext = ViewModel;
        Loaded += OnLoaded;
    }

    public ShellViewModel ViewModel { get; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        navigationService.Attach(ContentFrame);
        SelectRoute(NavigationRoute.Overview);
        ViewModel.NavigateCommand.Execute(NavigationRoute.Overview);
    }

    private void OnNavigationItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        var route = args.IsSettingsInvoked
            ? NavigationRoute.Settings
            : ParseRoute(args.InvokedItemContainer?.Tag);
        SelectRoute(route);
        ViewModel.NavigateCommand.Execute(route);
    }

    private static NavigationRoute ParseRoute(object? tag)
    {
        if (tag is string value && Enum.TryParse<NavigationRoute>(value, out var route))
        {
            return route;
        }

        throw new InvalidOperationException("The selected navigation item has no supported route.");
    }

    private void SelectRoute(NavigationRoute route)
    {
        if (route == NavigationRoute.Settings)
        {
            ShellNavigation.SelectedItem = ShellNavigation.SettingsItem;
            return;
        }

        ShellNavigation.SelectedItem = ShellNavigation.MenuItems
            .OfType<NavigationViewItem>()
            .Single(item => string.Equals(item.Tag?.ToString(), route.ToString(), StringComparison.Ordinal));
    }
}
