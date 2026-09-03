using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Suma.Desktop.Operations.Overview;

namespace Suma.Desktop.ViewModels;

public sealed class OverviewViewModel(IOverviewOperations operations) : ObservableObject
{
    private readonly object loadSync = new();
    private Task? activeLoad;
    private bool reloadRequested;
    private long loadVersion;
    private CancellationToken pendingToken;
    private string selectedCurrency = string.Empty;
    private bool isLoading;
    private string? errorMessage;

    public ObservableCollection<string> Currencies { get; } = [];
    public ObservableCollection<OverviewAccountRow> Accounts { get; } = [];
    public ObservableCollection<OverviewSavingsRow> Savings { get; } = [];
    public ObservableCollection<OverviewUpcomingDisplay> Upcoming { get; } = [];
    public ObservableCollection<OverviewActivityDisplay> RecentActivity { get; } = [];
    public string SelectedCurrency { get => selectedCurrency; private set => SetProperty(ref selectedCurrency, value); }
    public bool IsLoading { get => isLoading; private set { if (SetProperty(ref isLoading, value)) Notify(); } }
    public string? ErrorMessage { get => errorMessage; private set { if (SetProperty(ref errorMessage, value)) Notify(); } }
    public string AvailableToSpendDisplay { get; private set; } = "Unavailable";
    public string IncludedBalanceDisplay { get; private set; } = "Unavailable";
    public string ProtectedReserveDisplay { get; private set; } = "Unavailable";
    public string AccountTotalDisplay { get; private set; } = "Unavailable";
    public string BudgetTitle { get; private set; } = "No account currencies";
    public string BudgetDetail { get; private set; } = "Create an account to begin.";
    private bool hasCurrentBudget;
    public bool HasCurrentBudget { get => hasCurrentBudget; private set => SetProperty(ref hasCurrentBudget, value); }
    public Visibility LoadingVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ErrorVisibility => string.IsNullOrWhiteSpace(ErrorMessage) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility AccountsEmptyVisibility => !IsLoading && Accounts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility AccountsContentVisibility => !IsLoading && Accounts.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SavingsEmptyVisibility => !IsLoading && Savings.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SavingsContentVisibility => !IsLoading && Savings.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility UpcomingEmptyVisibility => !IsLoading && Upcoming.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility UpcomingContentVisibility => !IsLoading && Upcoming.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ActivityEmptyVisibility => !IsLoading && RecentActivity.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ActivityContentVisibility => !IsLoading && RecentActivity.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility CurrentBudgetEmptyVisibility => !IsLoading && !HasCurrentBudget ? Visibility.Visible : Visibility.Collapsed;
    public Visibility CurrentBudgetContentVisibility => !IsLoading && HasCurrentBudget ? Visibility.Visible : Visibility.Collapsed;

    public Task LoadAsync(CancellationToken cancellationToken = default)
    {
        lock (loadSync)
        {
            loadVersion++;
            reloadRequested = true;
            pendingToken = cancellationToken;
            activeLoad ??= ProcessLoadsAsync();
            return activeLoad;
        }
    }

    public Task SelectCurrencyAsync(string currencyCode, CancellationToken cancellationToken = default)
    {
        SelectedCurrency = currencyCode;
        return LoadAsync(cancellationToken);
    }

    private async Task ProcessLoadsAsync()
    {
        await Task.Yield();
        IsLoading = true;
        while (true)
        {
            long version;
            string? currency;
            CancellationToken token;
            lock (loadSync) { version = loadVersion; currency = SelectedCurrency; token = pendingToken; reloadRequested = false; }
            Application.Overview.GetOverview.OverviewResult? result = null;
            Exception? failure = null;
            try { result = await operations.GetAsync(currency, token); }
            catch (Exception exception) { failure = exception; }
            if (version == Interlocked.Read(ref loadVersion))
            {
                if (failure is null) Apply(result!);
                else
                {
                    ClearFinancialSnapshot();
                    ErrorMessage = "Suma could not load Overview. Try again.";
                }
            }

            lock (loadSync)
            {
                if (reloadRequested || version != loadVersion) continue;
                IsLoading = false;
                activeLoad = null;
                Notify();
                return;
            }
        }
    }

    private void Apply(Application.Overview.GetOverview.OverviewResult result)
    {
        ErrorMessage = null;
        Replace(Currencies, result.AvailableCurrencies);
        SelectedCurrency = result.CurrencyCode;
        if (string.IsNullOrEmpty(result.CurrencyCode))
        {
            ClearFinancialSnapshot();
            return;
        }

        HasCurrentBudget = result.CurrentBudget is not null;
        AvailableToSpendDisplay = MoneyText.Format(result.AvailableToSpendMinor, result.CurrencyCode);
        IncludedBalanceDisplay = MoneyText.Format(result.IncludedAccountBalanceMinor, result.CurrencyCode);
        ProtectedReserveDisplay = MoneyText.Format(result.ProtectedBudgetRemainingMinor, result.CurrencyCode);
        AccountTotalDisplay = MoneyText.Format(result.AccountTotalMinor, result.CurrencyCode);
        BudgetTitle = result.CurrentBudget?.Name ?? "No current budget";
        BudgetDetail = result.CurrentBudget is null
            ? "No protected allocations for this currency."
            : $"Protected {MoneyText.Format(result.CurrentBudget.ProtectedRemainingMinor, result.CurrencyCode)} • Remaining {MoneyText.Format(result.CurrentBudget.RemainingMinor, result.CurrencyCode)}";
        Replace(Accounts, result.Accounts.Select(item => new OverviewAccountRow(item, result.CurrencyCode)));
        Replace(Savings, result.Savings.Select(item => new OverviewSavingsRow(item, result.CurrencyCode)));
        Replace(Upcoming, result.Upcoming.Select(item => new OverviewUpcomingDisplay(item, result.CurrencyCode)));
        Replace(RecentActivity, result.RecentActivity.Select(item => new OverviewActivityDisplay(item, result.CurrencyCode)));
        OnPropertyChanged(nameof(AvailableToSpendDisplay)); OnPropertyChanged(nameof(IncludedBalanceDisplay));
        OnPropertyChanged(nameof(ProtectedReserveDisplay)); OnPropertyChanged(nameof(AccountTotalDisplay));
        OnPropertyChanged(nameof(BudgetTitle)); OnPropertyChanged(nameof(BudgetDetail)); Notify();
    }

    private void ClearFinancialSnapshot()
    {
        HasCurrentBudget = false;
        AvailableToSpendDisplay = "Unavailable";
        IncludedBalanceDisplay = "Unavailable";
        ProtectedReserveDisplay = "Unavailable";
        AccountTotalDisplay = "Unavailable";
        BudgetTitle = Currencies.Count == 0 ? "No account currencies" : "Overview unavailable";
        BudgetDetail = Currencies.Count == 0 ? "Create an account to begin." : "Retry to load current financial values.";
        Accounts.Clear(); Savings.Clear(); Upcoming.Clear(); RecentActivity.Clear();
        OnPropertyChanged(nameof(AvailableToSpendDisplay)); OnPropertyChanged(nameof(IncludedBalanceDisplay));
        OnPropertyChanged(nameof(ProtectedReserveDisplay)); OnPropertyChanged(nameof(AccountTotalDisplay));
        OnPropertyChanged(nameof(BudgetTitle)); OnPropertyChanged(nameof(BudgetDetail)); Notify();
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values) { target.Clear(); foreach (var value in values) target.Add(value); }
    private void Notify()
    {
        OnPropertyChanged(nameof(LoadingVisibility)); OnPropertyChanged(nameof(ErrorVisibility));
        OnPropertyChanged(nameof(AccountsEmptyVisibility)); OnPropertyChanged(nameof(AccountsContentVisibility));
        OnPropertyChanged(nameof(SavingsEmptyVisibility)); OnPropertyChanged(nameof(SavingsContentVisibility));
        OnPropertyChanged(nameof(UpcomingEmptyVisibility)); OnPropertyChanged(nameof(UpcomingContentVisibility));
        OnPropertyChanged(nameof(ActivityEmptyVisibility)); OnPropertyChanged(nameof(ActivityContentVisibility));
        OnPropertyChanged(nameof(CurrentBudgetEmptyVisibility)); OnPropertyChanged(nameof(CurrentBudgetContentVisibility));
    }
}
