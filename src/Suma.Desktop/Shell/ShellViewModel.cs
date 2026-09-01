using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Suma.Desktop.Navigation;
using Suma.Desktop.ViewModels;

namespace Suma.Desktop.Shell;

public sealed partial class ShellViewModel : ViewModelBase
{
    private readonly INavigationService navigationService;

    public ShellViewModel(INavigationService navigationService)
    {
        this.navigationService = navigationService;
        this.navigationService.Navigated += OnNavigated;
    }

    public string ApplicationTitle => "Suma";

    public string ApplicationTagline => "Money, made clear.";

    [ObservableProperty]
    public partial NavigationRoute CurrentRoute { get; private set; } = NavigationRoute.Overview;

    [RelayCommand]
    private void Navigate(NavigationRoute route) => navigationService.Navigate(route);

    private void OnNavigated(object? sender, NavigationRoute route) => CurrentRoute = route;
}
