using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Suma.Desktop.ViewModels;

namespace Suma.Desktop.Pages.Overview;

public sealed partial class OverviewPage : Page
{
    public OverviewPage(OverviewViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public OverviewViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e) => await ViewModel.LoadAsync();

    private async void OnRefreshClick(object sender, RoutedEventArgs e) => await ViewModel.LoadAsync();

    private async void OnCurrencySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CurrencySelector.SelectedItem is string currency
            && !string.Equals(currency, ViewModel.SelectedCurrency, StringComparison.Ordinal))
        {
            await ViewModel.SelectCurrencyAsync(currency);
        }
    }
}
