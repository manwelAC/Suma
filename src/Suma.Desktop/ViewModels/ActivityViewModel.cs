using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Suma.Application.Common.Exceptions;
using Suma.Application.Transactions.CreateExpense;
using Suma.Application.Transactions.CreateIncome;
using Suma.Application.Transactions.CreateRefund;
using Suma.Application.Transactions.CreateTransfer;
using Suma.Application.Transactions.GetRefundableExpenses;
using Suma.Application.Transactions.GetTransactions;
using Suma.Desktop.Operations.Transactions;
using Suma.Domain.Transactions;

namespace Suma.Desktop.ViewModels;

public sealed class ActivityViewModel(ITransactionOperations operations) : ViewModelBase
{
    private const string AllAccounts = "All accounts";
    private const string AllCategories = "All categories";
    private readonly object loadSync = new();
    private readonly List<TransactionHistoryResult> loadedItems = [];
    private bool isLoading;
    private bool isSaving;
    private bool reloadRequested;
    private string? errorMessage;
    private TransactionType? selectedType;
    private long loadRequestVersion;
    private Task? activeLoad;
    private CancellationToken pendingLoadToken;
    private string searchText = string.Empty;
    private string selectedCurrency = string.Empty;
    private string selectedAccount = AllAccounts;
    private string selectedCategory = AllCategories;
    private string selectedDateRange = "All time";
    private bool isSortDescending = true;
    private ActivityRowViewModel? selectedItem;

    public ObservableCollection<ActivityRowViewModel> Items { get; } = [];

    public ObservableCollection<RefundableExpenseOption> RefundableExpenses { get; } = [];

    public ObservableCollection<string> Currencies { get; } = [];

    public ObservableCollection<string> Accounts { get; } = [AllAccounts];

    public ObservableCollection<string> Categories { get; } = [AllCategories];

    public ObservableCollection<string> DateRanges { get; } =
    [
        "All time",
        "This month",
        "Last month",
        "Last 30 days",
        "Last 90 days",
        "This year"
    ];

    public string SearchText { get => searchText; private set => SetProperty(ref searchText, value); }

    public string SelectedCurrency { get => selectedCurrency; private set => SetProperty(ref selectedCurrency, value); }

    public string SelectedAccount { get => selectedAccount; private set => SetProperty(ref selectedAccount, value); }

    public string SelectedCategory { get => selectedCategory; private set => SetProperty(ref selectedCategory, value); }

    public string SelectedDateRange
    {
        get => selectedDateRange;
        set
        {
            if (SetProperty(ref selectedDateRange, value))
            {
                ApplyPresentationFilters();
            }
        }
    }

    public bool IsSortDescending
    {
        get => isSortDescending;
        set
        {
            if (SetProperty(ref isSortDescending, value))
            {
                OnPropertyChanged(nameof(SortOrderDisplay));
                ApplyPresentationFilters();
            }
        }
    }

    public string SortOrderDisplay => IsSortDescending ? "Newest first" : "Oldest first";

    public void ToggleSortOrder() => IsSortDescending = !IsSortDescending;

    public bool HasActiveFilters =>
        SelectedType.HasValue
        || (!string.IsNullOrWhiteSpace(SelectedAccount) && SelectedAccount != AllAccounts)
        || (!string.IsNullOrWhiteSpace(SelectedCategory) && SelectedCategory != AllCategories)
        || (!string.IsNullOrWhiteSpace(SelectedDateRange) && SelectedDateRange != "All time")
        || !string.IsNullOrWhiteSpace(SearchText);

    public void SetDateRange(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            SelectedDateRange = value;
        }
    }

    public void SetDateRange(DateOnly start, DateOnly end)
    {
        SelectedDateRange = $"{start:MMM d, yyyy} – {end:MMM d, yyyy}";
    }

    public async Task ResetFiltersAsync(CancellationToken cancellationToken = default)
    {
        searchText = string.Empty;
        selectedCategory = AllCategories;
        selectedAccount = AllAccounts;
        selectedDateRange = "All time";
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(SelectedCategory));
        OnPropertyChanged(nameof(SelectedAccount));
        OnPropertyChanged(nameof(SelectedDateRange));
        if (SelectedType.HasValue)
        {
            await SetFilterAsync(null, cancellationToken);
        }
        else
        {
            ApplyPresentationFilters();
        }
    }

    public ActivityRowViewModel? SelectedItem
    {
        get => selectedItem;
        set
        {
            if (SetProperty(ref selectedItem, value))
            {
                OnPropertyChanged(nameof(DetailVisibility));
                OnPropertyChanged(nameof(DetailEmptyVisibility));
            }
        }
    }

    public Visibility DetailVisibility => SelectedItem is null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility DetailEmptyVisibility => SelectedItem is null ? Visibility.Visible : Visibility.Collapsed;

    public string IncomeDisplay => MoneyText.Format(Items.Where(item => item.Type == TransactionType.Income).Sum(item => item.AmountMinor), SelectedCurrency);

    public string ExpenseDisplay => MoneyText.Format(Items.Where(item => item.Type == TransactionType.Expense).Sum(item => item.AmountMinor), SelectedCurrency);

    public string NetFlowDisplay => MoneyText.Format(checked(
        Items.Where(item => item.Type == TransactionType.Income).Sum(item => item.AmountMinor)
        - Items.Where(item => item.Type == TransactionType.Expense).Sum(item => item.AmountMinor)
        + Items.Where(item => item.Type == TransactionType.Refund).Sum(item => item.AmountMinor)), SelectedCurrency);
    public TransactionType? SelectedType
    {
        get => selectedType;
        private set
        {
            if (SetProperty(ref selectedType, value))
            {
                OnPropertyChanged(nameof(EmptyTitle));
            }
        }
    }

    private bool isSearching;

    public bool IsSearching { get => isSearching; private set { if (SetProperty(ref isSearching, value)) { NotifyState(); } } }

    public bool IsBusy => IsLoading || IsSearching;

    public Visibility BusyVisibility => IsBusy ? Visibility.Visible : Visibility.Collapsed;

    public double ListOpacity => IsBusy ? 0.35 : 1.0;

    public string LoadingStatusText => IsSearching ? "Searching transactions..." : "Loading activity...";

    public bool IsLoading { get => isLoading; private set { if (SetProperty(ref isLoading, value)) { NotifyState(); } } }

    public bool IsSaving { get => isSaving; private set => SetProperty(ref isSaving, value); }

    public string? ErrorMessage { get => errorMessage; private set { if (SetProperty(ref errorMessage, value)) OnPropertyChanged(nameof(ErrorVisibility)); } }

    public string EmptyTitle => SelectedType is null ? "No activity yet" : $"No {SelectedType.ToString()!.ToLowerInvariant()}s yet";

    public Visibility EmptyVisibility => !IsBusy && Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility LoadingVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ErrorVisibility => string.IsNullOrEmpty(ErrorMessage) ? Visibility.Collapsed : Visibility.Visible;

    public Task LoadAsync(CancellationToken cancellationToken = default)
    {
        lock (loadSync)
        {
            loadRequestVersion++;
            reloadRequested = true;
            pendingLoadToken = cancellationToken;
            activeLoad ??= ProcessLoadsAsync();
            return activeLoad;
        }
    }

    private async Task ProcessLoadsAsync()
    {
        await Task.Yield();
        IsLoading = true;
        while (true)
        {
            long requestVersion;
            TransactionType? requestedType;
            CancellationToken cancellationToken;
            lock (loadSync)
            {
                requestVersion = loadRequestVersion;
                requestedType = SelectedType;
                cancellationToken = pendingLoadToken;
                reloadRequested = false;
            }

            try
            {
                var results = await operations.GetAsync(
                    new GetTransactionsRequest(requestedType),
                    cancellationToken);
                lock (loadSync)
                {
                    if (requestVersion == loadRequestVersion)
                    {
                        ErrorMessage = null;
                        ReplaceItems(results);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                lock (loadSync)
                {
                    if (requestVersion == loadRequestVersion)
                    {
                        ErrorMessage = UserMessage(exception, "load activity");
                    }
                }
            }

            lock (loadSync)
            {
                if (reloadRequested || requestVersion != loadRequestVersion)
                {
                    continue;
                }

                IsLoading = false;
                activeLoad = null;
                return;
            }
        }
    }

    public async Task SetFilterAsync(TransactionType? type, CancellationToken cancellationToken = default)
    {
        SelectedType = type;
        await LoadAsync(cancellationToken);
    }

    public void SetSearch(string value) { SearchText = value ?? string.Empty; ApplyPresentationFilters(); }

    public async Task SearchAsync(string text, CancellationToken cancellationToken = default)
    {
        IsSearching = true;
        try
        {
            await Task.Delay(260, cancellationToken);
            SearchText = text?.Trim() ?? string.Empty;
            ApplyPresentationFilters();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            IsSearching = false;
        }
    }

    public void SetCurrency(string? value) { if (string.IsNullOrWhiteSpace(value) || value == SelectedCurrency) return; SelectedCurrency = value; ApplyPresentationFilters(); }

    public void SetAccount(string? value) { SelectedAccount = string.IsNullOrWhiteSpace(value) ? AllAccounts : value; ApplyPresentationFilters(); }

    public void SetCategory(string? value) { SelectedCategory = string.IsNullOrWhiteSpace(value) ? AllCategories : value; ApplyPresentationFilters(); }

    public async Task<bool> LoadRefundableExpensesAsync(CancellationToken cancellationToken = default)
    {
        ErrorMessage = null;
        try
        {
            var results = await operations.GetRefundableExpensesAsync(cancellationToken);
            RefundableExpenses.Clear();
            foreach (var item in results)
            {
                RefundableExpenses.Add(new(item));
            }

            return true;
        }
        catch (Exception exception)
        {
            RefundableExpenses.Clear();
            ErrorMessage = exception switch
            {
                ApplicationValidationException or ConflictException or NotFoundException => exception.Message,
                _ => "Suma could not load refundable expenses. Try again."
            };
            return false;
        }
    }

    public void SetError(string message) => ErrorMessage = message;

    public Task<bool> CreateExpenseAsync(ExpenseEditorInput input, CancellationToken cancellationToken = default) =>
        ExecuteWriteAsync(() => operations.CreateExpenseAsync(new(input.AccountId, input.CategoryId, input.AmountMinor, input.CurrencyCode, input.Date, input.Description, input.Notes), cancellationToken), cancellationToken);

    public Task<bool> CreateIncomeAsync(IncomeEditorInput input, CancellationToken cancellationToken = default) =>
        ExecuteWriteAsync(() => operations.CreateIncomeAsync(new(input.AccountId, input.CategoryId, input.AmountMinor, input.CurrencyCode, input.Date, input.Description, input.Notes), cancellationToken), cancellationToken);

    public Task<bool> CreateTransferAsync(TransferEditorInput input, CancellationToken cancellationToken = default) =>
        ExecuteWriteAsync(() => operations.CreateTransferAsync(new(input.SourceAccountId, input.DestinationAccountId, input.AmountMinor, input.CurrencyCode, input.Date, input.Description, input.Notes), cancellationToken), cancellationToken);

    public Task<bool> CreateRefundAsync(RefundEditorInput input, CancellationToken cancellationToken = default) =>
        ExecuteWriteAsync(() => operations.CreateRefundAsync(new(input.OriginalTransactionId, input.DestinationAccountId, input.CategoryId, input.AmountMinor, input.CurrencyCode, input.Date, input.Description, input.Notes), cancellationToken), cancellationToken);

    public Task<bool> DeleteTransactionAsync(Guid transactionId, CancellationToken cancellationToken = default) =>
        ExecuteDeleteAsync(() => operations.DeleteAsync(transactionId, cancellationToken), cancellationToken);

    private async Task<bool> ExecuteWriteAsync(Func<Task<Suma.Application.Transactions.TransactionResult>> write, CancellationToken cancellationToken)
    {
        if (IsSaving)
        {
            return false;
        }

        IsSaving = true;
        ErrorMessage = null;
        try
        {
            _ = await write();
            await LoadAsync(cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            ErrorMessage = UserMessage(exception, "record that transaction");
            return false;
        }
        finally
        {
            IsSaving = false;
        }
    }

    private async Task<bool> ExecuteDeleteAsync(Func<Task> delete, CancellationToken cancellationToken)
    {
        if (IsSaving)
        {
            return false;
        }

        IsSaving = true;
        ErrorMessage = null;
        try
        {
            await delete();
            await LoadAsync(cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            ErrorMessage = UserMessage(exception, "delete that transaction");
            return false;
        }
        finally
        {
            IsSaving = false;
        }
    }

    private static ActivityRowViewModel Map(TransactionHistoryResult item, bool startsGroup)
    {
        var type = item.Type.ToString();
        var title = string.IsNullOrWhiteSpace(item.Description) ? type : item.Description;
        var context = item.Type switch
        {
            TransactionType.Expense => $"{item.CategoryName ?? "Expense"} • {item.SourceAccountName ?? "Unknown account"}",
            TransactionType.Income => $"{item.CategoryName ?? "Income"} • {item.DestinationAccountName ?? "Unknown account"}",
            TransactionType.Transfer => $"{item.SourceAccountName ?? "Unknown account"} → {item.DestinationAccountName ?? "Unknown account"}",
            TransactionType.Refund => $"Refund • {item.CategoryName ?? "Expense"} • {item.DestinationAccountName ?? "Unknown account"}",
            _ => type
        };
        var prefix = item.Type switch
        {
            TransactionType.Expense => "- ",
            TransactionType.Income or TransactionType.Refund => "+ ",
            _ => string.Empty
        };
        return new(item.Id, item.Type, type, title!, context, prefix + MoneyText.Format(item.AmountMinor, item.CurrencyCode), item.AmountMinor, item.CurrencyCode,
            item.TransactionDate, item.SourceAccountId, item.SourceAccountName, item.DestinationAccountId, item.DestinationAccountName,
            item.CategoryId, item.CategoryName, item.Notes, DateLabel(item.TransactionDate), startsGroup);
    }

    private void ReplaceItems(IReadOnlyList<TransactionHistoryResult> results)
    {
        var previousAccount = SelectedAccount;
        var previousCategory = SelectedCategory;
        var previousCurrency = SelectedCurrency;

        loadedItems.Clear();
        loadedItems.AddRange(results);
        ReplaceOptions(Currencies, results.Select(item => item.CurrencyCode));
        if (string.IsNullOrEmpty(SelectedCurrency) || !Currencies.Contains(SelectedCurrency, StringComparer.OrdinalIgnoreCase))
        {
            SelectedCurrency = Currencies.FirstOrDefault() ?? string.Empty;
        }

        ReplaceOptions(Accounts, results.SelectMany(item => new[] { item.SourceAccountName, item.DestinationAccountName }).OfType<string>(), AllAccounts);
        ReplaceOptions(Categories, results.Select(item => item.CategoryName).OfType<string>(), AllCategories);

        if (!string.IsNullOrWhiteSpace(previousAccount) && Accounts.Contains(previousAccount, StringComparer.OrdinalIgnoreCase))
        {
            SelectedAccount = previousAccount;
        }
        else
        {
            SelectedAccount = AllAccounts;
        }

        if (!string.IsNullOrWhiteSpace(previousCategory) && Categories.Contains(previousCategory, StringComparer.OrdinalIgnoreCase))
        {
            SelectedCategory = previousCategory;
        }
        else
        {
            SelectedCategory = AllCategories;
        }

        OnPropertyChanged(nameof(SelectedCurrency));
        OnPropertyChanged(nameof(SelectedAccount));
        OnPropertyChanged(nameof(SelectedCategory));
        ApplyPresentationFilters();
    }

    public async Task<bool> DuplicateSelectedAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedItem is null) return false;
        var item = SelectedItem;
        return item.Type switch
        {
            TransactionType.Expense => await CreateExpenseAsync(new(item.SourceAccountId ?? Guid.NewGuid(), item.CategoryId ?? Guid.NewGuid(), item.AmountMinor, item.CurrencyCode, item.TransactionDate, $"{item.Title} (Copy)", item.Notes), cancellationToken),
            TransactionType.Income => await CreateIncomeAsync(new(item.DestinationAccountId ?? Guid.NewGuid(), item.CategoryId ?? Guid.NewGuid(), item.AmountMinor, item.CurrencyCode, item.TransactionDate, $"{item.Title} (Copy)", item.Notes), cancellationToken),
            TransactionType.Transfer => await CreateTransferAsync(new(item.SourceAccountId ?? Guid.NewGuid(), item.DestinationAccountId ?? Guid.NewGuid(), item.AmountMinor, item.CurrencyCode, item.TransactionDate, $"{item.Title} (Copy)", item.Notes), cancellationToken),
            _ => false
        };
    }

    private void ApplyPresentationFilters()
    {
        var selectedId = SelectedItem?.Id;
        ParseDateRange(SelectedDateRange, out var startDate, out var endDate);

        IEnumerable<TransactionHistoryResult> filtered = loadedItems.Where(item =>
            (string.IsNullOrEmpty(SelectedCurrency) || string.Equals(item.CurrencyCode, SelectedCurrency, StringComparison.OrdinalIgnoreCase))
            && (!SelectedType.HasValue || item.Type == SelectedType.Value)
            && (SelectedAccount == AllAccounts || string.Equals(item.SourceAccountName, SelectedAccount, StringComparison.OrdinalIgnoreCase) || string.Equals(item.DestinationAccountName, SelectedAccount, StringComparison.OrdinalIgnoreCase))
            && (SelectedCategory == AllCategories || string.Equals(item.CategoryName, SelectedCategory, StringComparison.OrdinalIgnoreCase))
            && (!startDate.HasValue || item.TransactionDate >= startDate.Value)
            && (!endDate.HasValue || item.TransactionDate <= endDate.Value)
            && (string.IsNullOrWhiteSpace(SearchText)
                || (item.Description?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
                || (item.Notes?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
                || (item.CategoryName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
                || (item.SourceAccountName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
                || (item.DestinationAccountName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
                || item.Type.ToString().Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || MoneyText.Format(item.AmountMinor, item.CurrencyCode).Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || (item.AmountMinor / 100.0).ToString("0.00").Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || (item.AmountMinor / 100.0).ToString().Contains(SearchText, StringComparison.OrdinalIgnoreCase)));

        var results = isSortDescending
            ? filtered.OrderByDescending(item => item.TransactionDate).ToArray()
            : filtered.OrderBy(item => item.TransactionDate).ToArray();

        Items.Clear();
        DateOnly? previousDate = null;
        foreach (var item in results)
        {
            var startsGroup = previousDate != item.TransactionDate;
            Items.Add(Map(item, startsGroup));
            previousDate = item.TransactionDate;
        }
        SelectedItem = selectedId.HasValue
            ? Items.FirstOrDefault(item => item.Id == selectedId) ?? Items.FirstOrDefault()
            : Items.FirstOrDefault();
        OnPropertyChanged(nameof(EmptyVisibility));
        OnPropertyChanged(nameof(IncomeDisplay));
        OnPropertyChanged(nameof(ExpenseDisplay));
        OnPropertyChanged(nameof(NetFlowDisplay));
        OnPropertyChanged(nameof(Items));
        OnPropertyChanged(nameof(HasActiveFilters));
    }

    private static void ParseDateRange(string? rangeText, out DateOnly? startDate, out DateOnly? endDate)
    {
        startDate = null;
        endDate = null;
        if (string.IsNullOrWhiteSpace(rangeText) || rangeText.Equals("All time", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);

        if (rangeText.Equals("This month", StringComparison.OrdinalIgnoreCase))
        {
            startDate = new DateOnly(today.Year, today.Month, 1);
            endDate = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
            return;
        }

        if (rangeText.Equals("Last month", StringComparison.OrdinalIgnoreCase))
        {
            var firstOfThisMonth = new DateOnly(today.Year, today.Month, 1);
            var firstOfLastMonth = firstOfThisMonth.AddMonths(-1);
            startDate = firstOfLastMonth;
            endDate = firstOfThisMonth.AddDays(-1);
            return;
        }

        if (rangeText.Equals("Last 30 days", StringComparison.OrdinalIgnoreCase))
        {
            startDate = today.AddDays(-30);
            endDate = today;
            return;
        }

        if (rangeText.Equals("Last 90 days", StringComparison.OrdinalIgnoreCase))
        {
            startDate = today.AddDays(-90);
            endDate = today;
            return;
        }

        if (rangeText.Equals("This year", StringComparison.OrdinalIgnoreCase))
        {
            startDate = new DateOnly(today.Year, 1, 1);
            endDate = new DateOnly(today.Year, 12, 31);
            return;
        }

        string[] separators = [" – ", " — ", " - ", " to ", "–", "—", "-"];
        foreach (var sep in separators)
        {
            var idx = rangeText.IndexOf(sep, StringComparison.Ordinal);
            if (idx > 0)
            {
                var part1 = rangeText[..idx].Trim();
                var part2 = rangeText[(idx + sep.Length)..].Trim();
                if (DateTime.TryParse(part2, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.None, out var d2)
                    || DateTime.TryParse(part2, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out d2))
                {
                    if (DateTime.TryParse(part1, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.None, out var d1)
                        || DateTime.TryParse(part1, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out d1))
                    {
                        if (d1.Year != d2.Year && !part1.Contains(d1.Year.ToString()))
                        {
                            d1 = new DateTime(d2.Year, d1.Month, d1.Day);
                        }
                        startDate = DateOnly.FromDateTime(d1);
                        endDate = DateOnly.FromDateTime(d2);
                        return;
                    }
                    if (DateTime.TryParse($"{part1}, {d2.Year}", System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.None, out var d1Adj)
                        || DateTime.TryParse($"{part1}, {d2.Year}", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out d1Adj))
                    {
                        startDate = DateOnly.FromDateTime(d1Adj);
                        endDate = DateOnly.FromDateTime(d2);
                        return;
                    }
                }
            }
        }

        if (DateTime.TryParse(rangeText, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.None, out var singleDate)
            || DateTime.TryParse(rangeText, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out singleDate))
        {
            startDate = new DateOnly(singleDate.Year, singleDate.Month, 1);
            endDate = new DateOnly(singleDate.Year, singleDate.Month, DateTime.DaysInMonth(singleDate.Year, singleDate.Month));
        }
    }

    private static void ReplaceOptions(ObservableCollection<string> target, IEnumerable<string> values, string? first = null)
    {
        var newValues = new List<string>();
        if (first is not null) newValues.Add(first);
        newValues.AddRange(values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase));

        if (target.SequenceEqual(newValues, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        target.Clear();
        foreach (var value in newValues) target.Add(value);
    }

    private static string DateLabel(DateOnly date)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (date == today) return "Today";
        if (date == today.AddDays(-1)) return "Yesterday";
        return date.ToString("MMMM d, yyyy");
    }

    private void NotifyState()
    {
        OnPropertyChanged(nameof(LoadingVisibility));
        OnPropertyChanged(nameof(EmptyVisibility));
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(BusyVisibility));
        OnPropertyChanged(nameof(ListOpacity));
        OnPropertyChanged(nameof(LoadingStatusText));
    }

    private static string UserMessage(Exception exception, string action) => exception switch
    {
        ApplicationValidationException or ConflictException or NotFoundException => exception.Message,
        _ => $"Suma could not {action}. Try again."
    };
}
