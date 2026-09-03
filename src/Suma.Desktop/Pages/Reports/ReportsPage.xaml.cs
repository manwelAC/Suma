using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Suma.Application.Reports.GetReportOptions;
using Suma.Desktop.ViewModels;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Suma.Desktop.Pages.Reports;

public sealed partial class ReportsPage : Page
{
    private bool synchronizingControls;
    public ReportsPage(ReportsViewModel viewModel) { ViewModel = viewModel; InitializeComponent(); DataContext = ViewModel; Loaded += OnLoaded; }
    public ReportsViewModel ViewModel { get; }
    public string NetCashFlowText => Money(ViewModel.Report?.CashFlow.NetCashFlowMinor, "Net Cash Flow"); public string GrossIncomeText => Money(ViewModel.Report?.CashFlow.GrossIncomeMinor, "Gross Income"); public string GrossExpenseText => Money(ViewModel.Report?.CashFlow.GrossExpenseMinor, "Gross Expense"); public string RefundsText => Money(ViewModel.Report?.CashFlow.RefundMinor, "Refunds"); public string NetExpenseText => Money(ViewModel.Report?.CashFlow.NetExpenseMinor, "Net Expense");
    public IEnumerable<string> CategoryRows => ViewModel.CategoryMode == ReportCategoryMode.Expense ? ViewModel.Report?.ExpenseCategories.Select(item => $"{item.CategoryName}{Archived(item.CategoryArchived)}  Gross Expense {MoneyText.Format(item.GrossExpenseMinor, ViewModel.Currency)}  •  Refunds {MoneyText.Format(item.RefundMinor, ViewModel.Currency)}  •  Net Expense {MoneyText.Format(item.NetExpenseMinor, ViewModel.Currency)}") ?? [] : ViewModel.Report?.IncomeCategories.Select(item => $"{item.CategoryName}{Archived(item.CategoryArchived)}  Income {MoneyText.Format(item.IncomeMinor, ViewModel.Currency)}") ?? [];
    public IEnumerable<string> AccountRows => ViewModel.Report?.AccountMovements.Select(item => $"{item.AccountName}{Archived(item.AccountArchived)}  Total Inflow {MoneyText.Format(item.TotalInflowMinor, ViewModel.Currency)}  •  Total Outflow {MoneyText.Format(item.TotalOutflowMinor, ViewModel.Currency)}  •  Net Movement {MoneyText.Format(item.NetMovementMinor, ViewModel.Currency)}\nIncome {MoneyText.Format(item.IncomeInMinor, ViewModel.Currency)}  •  Refund {MoneyText.Format(item.RefundInMinor, ViewModel.Currency)}  •  Transfer In {MoneyText.Format(item.TransferInMinor, ViewModel.Currency)}  •  Expense {MoneyText.Format(item.ExpenseOutMinor, ViewModel.Currency)}  •  Transfer Out {MoneyText.Format(item.TransferOutMinor, ViewModel.Currency)}") ?? [];
    public IEnumerable<string> DetailRows => ViewModel.AccountDetails.Select(item => $"{item.TransactionDate:MMM d}  {item.AccountName}  {item.Direction}  {MoneyText.Format(item.AmountMinor, item.CurrencyCode)}");
    public IEnumerable<string> BudgetRows => ViewModel.Budget?.Allocations.Select(item => $"{item.CategoryName}{Archived(item.CategoryArchived)}  Allocation {MoneyText.Format(item.AmountMinor, ViewModel.Budget.Summary.CurrencyCode)}  •  Spent {MoneyText.Format(item.SpentMinor, ViewModel.Budget.Summary.CurrencyCode)}  •  Remaining {MoneyText.Format(item.RemainingMinor, ViewModel.Budget.Summary.CurrencyCode)}  •  Utilization {item.UtilizationPercent:0.##}%  •  Reserve from Available: {(item.ReserveFromAvailable ? "Yes" : "No")}") ?? [];
    public string BudgetTitleText => ViewModel.Budget?.Summary.Name ?? "No Budget selected";
    public string BudgetPeriodText => ViewModel.Budget is null ? string.Empty : $"Period {ViewModel.Budget.Summary.PeriodStart:MMM d, yyyy}–{ViewModel.Budget.Summary.PeriodEnd:MMM d, yyyy}  •  Currency {ViewModel.Budget.Summary.CurrencyCode}";
    public string BudgetExpectedIncomeText => ViewModel.Budget is null ? string.Empty : $"Expected Income (planning context): {MoneyText.Format(ViewModel.Budget.Summary.ExpectedIncomeMinor, ViewModel.Budget.Summary.CurrencyCode)}";
    public string BudgetTotalsText => ViewModel.Budget is null ? string.Empty : $"Total Allocated {MoneyText.Format(ViewModel.Budget.AllocatedMinor, ViewModel.Budget.Summary.CurrencyCode)}  •  Total Spent {MoneyText.Format(ViewModel.Budget.SpentMinor, ViewModel.Budget.Summary.CurrencyCode)}  •  Total Remaining {MoneyText.Format(ViewModel.Budget.RemainingMinor, ViewModel.Budget.Summary.CurrencyCode)}";
    private async void OnLoaded(object sender, RoutedEventArgs e) { synchronizingControls = true; try { await ViewModel.InitializeAsync(); CurrencyBox.SelectedItem = ViewModel.Currency; SyncDates(); BudgetBox.SelectedItem = ViewModel.Budgets.FirstOrDefault(item => item.Id == ViewModel.Budget?.Summary.Id); } finally { synchronizingControls = false; RefreshBindings(); } }
    private async void OnPresetClick(object sender, RoutedEventArgs e) { await ViewModel.SetPresetAsync(Enum.Parse<ReportDatePreset>((string)((FrameworkElement)sender).Tag)); synchronizingControls = true; SyncDates(); synchronizingControls = false; RefreshBindings(); }
    private async void OnApplyClick(object sender, RoutedEventArgs e) { if (CurrencyBox.SelectedItem is string currency) await ViewModel.SetSelectionAsync(currency, DateOnly.FromDateTime(StartBox.Date.DateTime), DateOnly.FromDateTime(EndBox.Date.DateTime)); RefreshBindings(); }
    private void OnReportSelectionChanged(object sender, SelectionChangedEventArgs e) => MarkDraftDirty();
    private void OnReportDateChanged(object sender, DatePickerValueChangedEventArgs args) => MarkDraftDirty();
    private void MarkDraftDirty() { if (!synchronizingControls && CurrencyBox.SelectedItem is string currency) ViewModel.SetDraftSelection(currency, DateOnly.FromDateTime(StartBox.Date.DateTime), DateOnly.FromDateTime(EndBox.Date.DateTime)); }
    private async void OnSectionClick(object sender, RoutedEventArgs e) { var section = Enum.Parse<ReportSection>((string)((FrameworkElement)sender).Tag); await ViewModel.SetSectionAsync(section); CashFlowPanel.Visibility = section == ReportSection.CashFlow ? Visibility.Visible : Visibility.Collapsed; CategoriesPanel.Visibility = section == ReportSection.Categories ? Visibility.Visible : Visibility.Collapsed; AccountsPanel.Visibility = section == ReportSection.Accounts ? Visibility.Visible : Visibility.Collapsed; BudgetPanel.Visibility = section == ReportSection.Budget ? Visibility.Visible : Visibility.Collapsed; RefreshBindings(); }
    private void OnCategoryModeClick(object sender, RoutedEventArgs e) { ViewModel.SetCategoryMode(Enum.Parse<ReportCategoryMode>((string)((FrameworkElement)sender).Tag)); RefreshBindings(); }
    private async void OnBudgetChanged(object sender, SelectionChangedEventArgs e) { if (BudgetBox.SelectedItem is ReportBudgetOption item && ViewModel.Budget?.Summary.Id != item.Id) { await ViewModel.SelectBudgetAsync(item.Id); RefreshBindings(); } }
    private async void OnExportClick(object sender, RoutedEventArgs e) { await ViewModel.RunExportInteractionAsync(SaveExportAsync); RefreshBindings(); }
    private static async Task SaveExportAsync(ReportExport export, CancellationToken cancellationToken)
    {
        var picker = new FileSavePicker { SuggestedFileName = Path.GetFileNameWithoutExtension(export.FileName) }; picker.FileTypeChoices.Add("CSV", [".csv"]);
        var window = ((App)Microsoft.UI.Xaml.Application.Current).MainWindow; if (window is null) throw new InvalidOperationException("The Suma window is unavailable."); WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
        var file = await picker.PickSaveFileAsync(); if (file is not null) await FileIO.WriteBytesAsync(file, export.Content);
    }
    private void SyncDates() { StartBox.Date = new DateTimeOffset(ViewModel.StartDate.ToDateTime(TimeOnly.MinValue)); EndBox.Date = new DateTimeOffset(ViewModel.EndDate.ToDateTime(TimeOnly.MinValue)); }
    private void RefreshBindings() { Bindings.Update(); OnPropertyChanged(); }
    private void OnPropertyChanged() { Bindings.Update(); }
    private string Money(long? value, string label) => $"{label}: {(value.HasValue && !string.IsNullOrEmpty(ViewModel.Currency) ? MoneyText.Format(value.Value, ViewModel.Currency) : "Unavailable")}";
    private static string Archived(bool value) => value ? " (Archived)" : string.Empty;
}
