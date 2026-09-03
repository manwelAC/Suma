using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
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
        navigationService.Navigated += (_, route) => SelectRoute(route);
        SelectRoute(NavigationRoute.Overview);
        ViewModel.NavigateCommand.Execute(NavigationRoute.Overview);
    }

    private void OnNavigationClick(object sender, RoutedEventArgs e)
    {
        var route = ParseRoute(((FrameworkElement)sender).Tag);
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
        SetNavigationState(OverviewNavigationItem, route == NavigationRoute.Overview);
        SetNavigationState(AccountsNavigationItem, route == NavigationRoute.Accounts);
        SetNavigationState(ActivityNavigationItem, route == NavigationRoute.Activity);
        SetNavigationState(PlanningNavigationItem, route == NavigationRoute.Planning);
        SetNavigationState(ReportsNavigationItem, route == NavigationRoute.Reports);
        SetNavigationState(SettingsNavigationItem, route == NavigationRoute.Settings);
    }

    private static void SetNavigationState(ToggleButton item, bool isSelected)
    {
        item.IsChecked = isSelected;
        item.Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources[
            isSelected ? "SumaNavigationItemSelectedStyle" : "SumaNavigationItemStyle"];
    }
}
