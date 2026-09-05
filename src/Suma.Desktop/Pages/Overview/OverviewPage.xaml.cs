using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Suma.Desktop.Navigation;
using Suma.Desktop.ViewModels;

namespace Suma.Desktop.Pages.Overview;

public sealed partial class OverviewPage : Page
{
    private readonly INavigationService? navigationService;

    public OverviewPage(OverviewViewModel viewModel, INavigationService? navigationService = null)
    {
        ViewModel = viewModel;
        this.navigationService = navigationService;
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    public OverviewViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateResponsiveLayout(ActualWidth);
        await ViewModel.LoadAsync();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdateResponsiveLayout(e.NewSize.Width);

    private void UpdateResponsiveLayout(double availableWidth)
    {
        if (availableWidth <= 0) availableWidth = ActualWidth;
        if (availableWidth <= 0) return;

        RootGrid.MaxWidth = availableWidth;
        HeaderTitleColDef.Width = new GridLength(1, GridUnitType.Star);
        HeaderControlsColDef.Width = availableWidth < 880 ? new GridLength(0) : GridLength.Auto;

        if (availableWidth >= 1020)
        {
            // Header: Side-by-side
            Grid.SetRow(HeaderControlsStack, 0);
            Grid.SetColumn(HeaderControlsStack, 1);
            HeaderControlsStack.HorizontalAlignment = HorizontalAlignment.Right;
            HeaderControlsStack.Margin = new Thickness(0);

            // Hero Artwork visible
            HeroArtColDef.Width = GridLength.Auto;
            HeroArtContainer.Visibility = Visibility.Visible;

            // Mid Dashboard: 3 columns, 1 row
            MidCol0.Width = new GridLength(1, GridUnitType.Star);
            MidCol1.Width = new GridLength(1, GridUnitType.Star);
            MidCol2.Width = new GridLength(1, GridUnitType.Star);

            Grid.SetColumn(AccountsCard, 0);
            Grid.SetRow(AccountsCard, 0);
            Grid.SetColumnSpan(AccountsCard, 1);

            Grid.SetColumn(BudgetCard, 1);
            Grid.SetRow(BudgetCard, 0);
            Grid.SetColumnSpan(BudgetCard, 1);

            Grid.SetColumn(SavingsCard, 2);
            Grid.SetRow(SavingsCard, 0);
            Grid.SetColumnSpan(SavingsCard, 1);

            // Lower Dashboard: 2 columns, 1 row
            LowerCol0.Width = new GridLength(1, GridUnitType.Star);
            LowerCol1.Width = new GridLength(1, GridUnitType.Star);

            Grid.SetColumn(UpcomingCard, 0);
            Grid.SetRow(UpcomingCard, 0);
            Grid.SetColumnSpan(UpcomingCard, 1);

            Grid.SetColumn(RecentActivityCard, 1);
            Grid.SetRow(RecentActivityCard, 0);
            Grid.SetColumnSpan(RecentActivityCard, 1);
        }
        else if (availableWidth >= 880)
        {
            // Header: Side-by-side
            Grid.SetRow(HeaderControlsStack, 0);
            Grid.SetColumn(HeaderControlsStack, 1);
            HeaderControlsStack.HorizontalAlignment = HorizontalAlignment.Right;
            HeaderControlsStack.Margin = new Thickness(0);

            // Hero Artwork
            bool showArt = availableWidth >= 1000;
            HeroArtColDef.Width = showArt ? GridLength.Auto : new GridLength(0);
            HeroArtContainer.Visibility = showArt ? Visibility.Visible : Visibility.Collapsed;

            // Mid Dashboard: 2 columns (Accounts & Budget row 0, Savings spans row 1)
            MidCol0.Width = new GridLength(1, GridUnitType.Star);
            MidCol1.Width = new GridLength(1, GridUnitType.Star);
            MidCol2.Width = new GridLength(0);

            Grid.SetColumn(AccountsCard, 0);
            Grid.SetRow(AccountsCard, 0);
            Grid.SetColumnSpan(AccountsCard, 1);

            Grid.SetColumn(BudgetCard, 1);
            Grid.SetRow(BudgetCard, 0);
            Grid.SetColumnSpan(BudgetCard, 1);

            Grid.SetColumn(SavingsCard, 0);
            Grid.SetRow(SavingsCard, 1);
            Grid.SetColumnSpan(SavingsCard, 2);

            // Lower Dashboard: 2 columns, 1 row
            LowerCol0.Width = new GridLength(1, GridUnitType.Star);
            LowerCol1.Width = new GridLength(1, GridUnitType.Star);

            Grid.SetColumn(UpcomingCard, 0);
            Grid.SetRow(UpcomingCard, 0);
            Grid.SetColumnSpan(UpcomingCard, 1);

            Grid.SetColumn(RecentActivityCard, 1);
            Grid.SetRow(RecentActivityCard, 0);
            Grid.SetColumnSpan(RecentActivityCard, 1);
        }
        else
        {
            // Header: Wrapped
            Grid.SetRow(HeaderControlsStack, 1);
            Grid.SetColumn(HeaderControlsStack, 0);
            HeaderControlsStack.HorizontalAlignment = HorizontalAlignment.Left;
            HeaderControlsStack.Margin = new Thickness(0, 8, 0, 0);

            // Hero Artwork hidden
            HeroArtColDef.Width = new GridLength(0);
            HeroArtContainer.Visibility = Visibility.Collapsed;

            // Mid Dashboard: 1 column, stacked
            MidCol0.Width = new GridLength(1, GridUnitType.Star);
            MidCol1.Width = new GridLength(0);
            MidCol2.Width = new GridLength(0);

            Grid.SetColumn(AccountsCard, 0);
            Grid.SetRow(AccountsCard, 0);
            Grid.SetColumnSpan(AccountsCard, 1);

            Grid.SetColumn(BudgetCard, 0);
            Grid.SetRow(BudgetCard, 1);
            Grid.SetColumnSpan(BudgetCard, 1);

            Grid.SetColumn(SavingsCard, 0);
            Grid.SetRow(SavingsCard, 2);
            Grid.SetColumnSpan(SavingsCard, 1);

            // Lower Dashboard: 1 column, stacked
            LowerCol0.Width = new GridLength(1, GridUnitType.Star);
            LowerCol1.Width = new GridLength(0);

            Grid.SetColumn(UpcomingCard, 0);
            Grid.SetRow(UpcomingCard, 0);
            Grid.SetColumnSpan(UpcomingCard, 1);

            Grid.SetColumn(RecentActivityCard, 0);
            Grid.SetRow(RecentActivityCard, 1);
            Grid.SetColumnSpan(RecentActivityCard, 1);
        }
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e) => await ViewModel.LoadAsync();

    private async void OnCurrencySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CurrencySelector.SelectedItem is string currency
            && !string.Equals(currency, ViewModel.SelectedCurrency, StringComparison.Ordinal))
        {
            await ViewModel.SelectCurrencyAsync(currency);
        }
    }

    private void OnViewAccountsClick(object sender, RoutedEventArgs e) =>
        navigationService?.Navigate(NavigationRoute.Accounts);

    private void OnViewPlanningClick(object sender, RoutedEventArgs e) =>
        navigationService?.Navigate(NavigationRoute.Planning);

    private void OnViewActivityClick(object sender, RoutedEventArgs e) =>
        navigationService?.Navigate(NavigationRoute.Activity);
}
