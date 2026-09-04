using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Suma.Application.Accounts.CreateAccount;
using Suma.Application.Accounts.UpdateAccount;
using Suma.Application.Common.Exceptions;
using Suma.Application.Transactions.GetTransactions;
using Suma.Desktop.Navigation;
using Suma.Desktop.Operations.Accounts;
using Suma.Desktop.Operations.Transactions;
using Suma.Domain.Accounts;
using Suma.Domain.Transactions;
using Windows.UI;

namespace Suma.Desktop.ViewModels;

public sealed record AccountTransactionItem(
    Guid Id,
    string Title,
    string DateDisplay,
    string AmountDisplay,
    Brush AmountForeground,
    Brush IconBackground,
    Brush IconForeground,
    string IconGlyph);

public sealed partial class AccountsViewModel(IAccountOperations operations) : ViewModelBase
{
    private bool isLoading;
    private bool showArchived;
    private string? errorMessage;
    private AccountRowViewModel? selectedAccount;
    private string selectedCurrency = "PHP";
    private int selectedThemeIndex = 0;

    public ObservableCollection<AccountRowViewModel> Items { get; } = [];

    public ObservableCollection<AccountRowViewModel> ActiveAccounts { get; } = [];

    public ObservableCollection<AccountRowViewModel> ArchivedAccounts { get; } = [];

    public ObservableCollection<AccountTransactionItem> RecentTransactions { get; } = [];

    public ObservableCollection<string> Currencies { get; } = ["PHP", "USD"];

    public bool IsLoading
    {
        get => isLoading;
        private set
        {
            if (SetProperty(ref isLoading, value))
            {
                OnPropertyChanged(nameof(LoadingVisibility));
                OnPropertyChanged(nameof(EmptyVisibility));
            }
        }
    }

    public bool ShowArchived
    {
        get => showArchived;
        private set
        {
            if (SetProperty(ref showArchived, value))
            {
                OnPropertyChanged(nameof(ActiveFilterLabel));
            }
        }
    }

    public string? ErrorMessage
    {
        get => errorMessage;
        private set
        {
            if (SetProperty(ref errorMessage, value))
            {
                OnPropertyChanged(nameof(ErrorVisibility));
            }
        }
    }

    public AccountRowViewModel? SelectedAccount
    {
        get => selectedAccount;
        set
        {
            if (selectedAccount != value)
            {
                if (selectedAccount is not null) selectedAccount.IsSelected = false;
                if (SetProperty(ref selectedAccount, value))
                {
                    if (selectedAccount is not null) selectedAccount.IsSelected = true;
                    OnPropertyChanged(nameof(HasSelectedAccount));
                    OnPropertyChanged(nameof(AccountDetailsVisibility));
                    OnPropertyChanged(nameof(AccountDetailsEmptyVisibility));
                    OnPropertyChanged(nameof(SelectedAccountName));
                    OnPropertyChanged(nameof(SelectedAccountDetailSubtitle));
                    OnPropertyChanged(nameof(SelectedAccountCurrency));
                    OnPropertyChanged(nameof(SelectedAccountOpeningBalance));
                    OnPropertyChanged(nameof(SelectedAccountCurrentBalance));
                    OnPropertyChanged(nameof(SelectedAccountIsAtsIncluded));
                    OnPropertyChanged(nameof(SelectedAccountAccountNumber));
                    OnPropertyChanged(nameof(SelectedAccountHasAccountNumberVisibility));
                    OnPropertyChanged(nameof(SelectedAccountIdentifierLabel));
                    _ = LoadRecentTransactionsForSelectedAsync();
                }
            }
        }
    }

    public bool HasSelectedAccount => SelectedAccount is not null;

    public Visibility AccountDetailsVisibility => HasSelectedAccount ? Visibility.Visible : Visibility.Collapsed;

    public Visibility AccountDetailsEmptyVisibility => HasSelectedAccount ? Visibility.Collapsed : Visibility.Visible;

    public string SelectedAccountName => SelectedAccount?.Name ?? "No account selected";

    public string SelectedAccountDetailSubtitle => SelectedAccount?.AccountDetailSubtitle ?? "Select an account to view details";

    public string SelectedAccountCurrency => SelectedAccount?.CurrencyCode ?? SelectedCurrency;

    public string SelectedAccountOpeningBalance => SelectedAccount?.OpeningBalanceDisplay ?? MoneyText.Format(0, SelectedCurrency);

    public string SelectedAccountCurrentBalance => SelectedAccount?.BalanceDisplay ?? MoneyText.Format(0, SelectedCurrency);

    public bool SelectedAccountIsAtsIncluded => SelectedAccount?.IncludeInAvailableToSpend ?? false;

    public string SelectedAccountAccountNumber => SelectedAccount?.AccountNumberDisplay ?? string.Empty;

    public Visibility SelectedAccountHasAccountNumberVisibility => SelectedAccount?.AccountNumberRowVisibility ?? Visibility.Collapsed;

    public string SelectedAccountIdentifierLabel => SelectedAccount?.AccountIdentifierLabel ?? "Account number";

    public string SelectedCurrency
    {
        get => selectedCurrency;
        set
        {
            if (SetProperty(ref selectedCurrency, value))
            {
                RecalculateMetrics();
            }
        }
    }

    public int SelectedThemeIndex
    {
        get => selectedThemeIndex;
        set
        {
            if (SetProperty(ref selectedThemeIndex, value))
            {
                if (SelectedAccount is not null)
                {
                    SelectedAccount.ThemeIndex = value;
                }
            }
        }
    }

    public string IncludedBalanceDisplay => MoneyText.Format(
        ActiveAccounts.Where(a => a.IncludeInAvailableToSpend && string.Equals(a.CurrencyCode, SelectedCurrency, StringComparison.OrdinalIgnoreCase))
                      .Sum(a => a.BalanceMinor), SelectedCurrency);

    public string ExcludedBalanceDisplay => MoneyText.Format(
        ActiveAccounts.Where(a => !a.IncludeInAvailableToSpend && string.Equals(a.CurrencyCode, SelectedCurrency, StringComparison.OrdinalIgnoreCase))
                      .Sum(a => a.BalanceMinor), SelectedCurrency);

    public int ActiveAccountsCount => ActiveAccounts.Count;

    public int ArchivedAccountsCount => ArchivedAccounts.Count;

    public string ArchivedAccountsHeader => $"Archived accounts ({ArchivedAccounts.Count})";

    public Visibility ArchivedCardVisibility => ArchivedAccounts.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RecentActivityEmptyVisibility => RecentTransactions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RecentActivityListVisibility => RecentTransactions.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public string ActiveFilterLabel => ShowArchived ? "Viewing archived accounts" : "Viewing active accounts";

    public Visibility LoadingVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyVisibility => !IsLoading && ActiveAccounts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ErrorVisibility => string.IsNullOrEmpty(ErrorMessage) ? Visibility.Collapsed : Visibility.Visible;

    public string LastSyncDisplay => $"Last sync: {DateTime.Now:MMM d, yyyy h:mm tt}";

    public void SelectAccount(AccountRowViewModel? account)
    {
        if (account is not null)
        {
            SelectedAccount = account;
        }
    }

    public void SetCardTheme(int index)
    {
        SelectedThemeIndex = index;
    }

    public event Action? RequestNavigateToActivity;

    public void NavigateToActivity()
    {
        RequestNavigateToActivity?.Invoke();
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var activeResults = await operations.GetAsync(false, cancellationToken);
            var archivedResults = await operations.GetAsync(true, cancellationToken);

            var previousSelectedId = SelectedAccount?.Id;

            ActiveAccounts.Clear();
            Items.Clear();
            for (var i = 0; i < activeResults.Count; i++)
            {
                var account = activeResults[i];
                var rowVm = new AccountRowViewModel(
                    account.Id,
                    account.Name,
                    account.Type,
                    DisplayType(account.Type),
                    account.BalanceMinor,
                    MoneyText.Format(account.BalanceMinor, account.CurrencyCode),
                    account.OpeningBalanceMinor,
                    MoneyText.Format(account.OpeningBalanceMinor, account.CurrencyCode),
                    account.CurrencyCode,
                    account.IncludeInAvailableToSpend,
                    false,
                    themeIndex: i % 6,
                    accountNumber: account.AccountNumber);

                ActiveAccounts.Add(rowVm);
                Items.Add(rowVm);
            }

            ArchivedAccounts.Clear();
            foreach (var account in archivedResults)
            {
                ArchivedAccounts.Add(new AccountRowViewModel(
                    account.Id,
                    account.Name,
                    account.Type,
                    DisplayType(account.Type),
                    account.BalanceMinor,
                    MoneyText.Format(account.BalanceMinor, account.CurrencyCode),
                    account.OpeningBalanceMinor,
                    MoneyText.Format(account.OpeningBalanceMinor, account.CurrencyCode),
                    account.CurrencyCode,
                    account.IncludeInAvailableToSpend,
                    true,
                    themeIndex: 0,
                    accountNumber: account.AccountNumber));
            }

            // Update currency options
            foreach (var currency in ActiveAccounts.Select(a => a.CurrencyCode).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!Currencies.Contains(currency, StringComparer.OrdinalIgnoreCase))
                {
                    Currencies.Add(currency);
                }
            }

            // Restore selection or default to first
            if (previousSelectedId.HasValue)
            {
                SelectedAccount = ActiveAccounts.FirstOrDefault(a => a.Id == previousSelectedId.Value) ?? ActiveAccounts.FirstOrDefault();
            }
            else
            {
                SelectedAccount = ActiveAccounts.FirstOrDefault();
            }

            RecalculateMetrics();
        }
        catch (Exception exception)
        {
            ErrorMessage = UserMessage(exception);
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(EmptyVisibility));
            OnPropertyChanged(nameof(LastSyncDisplay));
        }
    }

    private async Task LoadRecentTransactionsForSelectedAsync(CancellationToken cancellationToken = default)
    {
        RecentTransactions.Clear();
        if (SelectedAccount is null)
        {
            OnPropertyChanged(nameof(RecentActivityEmptyVisibility));
            OnPropertyChanged(nameof(RecentActivityListVisibility));
            return;
        }

        try
        {
            var matching = await operations.GetRecentTransactionsAsync(SelectedAccount.Id, cancellationToken);
            var accountId = SelectedAccount.Id;

            foreach (var t in matching)
            {
                var isIncome = t.Type == TransactionType.Income || (t.Type == TransactionType.Transfer && t.DestinationAccountId == accountId);
                var isExpense = t.Type == TransactionType.Expense || (t.Type == TransactionType.Transfer && t.SourceAccountId == accountId);
                var prefix = isIncome ? "+ " : (isExpense ? "- " : string.Empty);
                var amountDisplay = prefix + MoneyText.Format(t.AmountMinor, t.CurrencyCode);

                Brush amountForeground = isIncome
                    ? new SolidColorBrush(Color.FromArgb(255, 46, 125, 50))
                    : new SolidColorBrush(Color.FromArgb(255, 28, 28, 30));

                Brush iconBg;
                Brush iconFg;
                string iconGlyph;

                if (t.Type == TransactionType.Income)
                {
                    iconBg = new SolidColorBrush(Color.FromArgb(255, 237, 245, 238));
                    iconFg = new SolidColorBrush(Color.FromArgb(255, 46, 125, 50));
                    iconGlyph = "\uE896"; // Download / deposit arrow
                }
                else if (t.Type == TransactionType.Transfer)
                {
                    iconBg = new SolidColorBrush(Color.FromArgb(255, 238, 242, 246));
                    iconFg = new SolidColorBrush(Color.FromArgb(255, 74, 98, 119));
                    iconGlyph = "\uE8D4"; // Transfer arrows
                }
                else
                {
                    iconBg = new SolidColorBrush(Color.FromArgb(255, 253, 238, 236));
                    iconFg = new SolidColorBrush(Color.FromArgb(255, 199, 75, 62));
                    iconGlyph = "\uE719"; // Shopping bag / expense
                }

                var title = string.IsNullOrWhiteSpace(t.CategoryName) ? t.Type.ToString() : t.CategoryName;
                var dateDisplay = t.TransactionDate.ToString("MMM d, yyyy");

                RecentTransactions.Add(new AccountTransactionItem(t.Id, title, dateDisplay, amountDisplay, amountForeground, iconBg, iconFg, iconGlyph));
            }
        }
        catch
        {
            // Graceful fallback on recent transactions failure
        }
        finally
        {
            OnPropertyChanged(nameof(RecentActivityEmptyVisibility));
            OnPropertyChanged(nameof(RecentActivityListVisibility));
        }
    }

    private void RecalculateMetrics()
    {
        OnPropertyChanged(nameof(IncludedBalanceDisplay));
        OnPropertyChanged(nameof(ExcludedBalanceDisplay));
        OnPropertyChanged(nameof(ActiveAccountsCount));
        OnPropertyChanged(nameof(ArchivedAccountsCount));
        OnPropertyChanged(nameof(ArchivedAccountsHeader));
        OnPropertyChanged(nameof(ArchivedCardVisibility));
    }

    public async Task SetArchivedViewAsync(bool archived, CancellationToken cancellationToken = default)
    {
        ShowArchived = archived;
        await LoadAsync(cancellationToken);
    }

    public async Task<bool> ToggleAtsAsync(AccountRowViewModel? account, CancellationToken cancellationToken = default)
    {
        if (account is null) return false;
        var newInclusion = !account.IncludeInAvailableToSpend;
        var success = await ExecuteWriteAsync(
            () => operations.UpdateAsync(
                new UpdateAccountRequest(account.Id, account.Name, account.Type, newInclusion),
                cancellationToken),
            cancellationToken);

        if (success)
        {
            account.IncludeInAvailableToSpend = newInclusion;
            RecalculateMetrics();
            OnPropertyChanged(nameof(SelectedAccountIsAtsIncluded));
        }

        return success;
    }

    public async Task<bool> CreateAsync(AccountEditorInput input, CancellationToken cancellationToken = default)
    {
        return await ExecuteWriteAsync(
            () => operations.CreateAsync(
                new CreateAccountRequest(
                    input.Name,
                    input.Type,
                    input.OpeningBalanceMinor,
                    input.CurrencyCode,
                    input.IncludeInAvailableToSpend,
                    input.AccountNumber),
                cancellationToken),
            cancellationToken);
    }

    public async Task<bool> UpdateAsync(
        Guid accountId,
        AccountEditorInput input,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWriteAsync(
            () => operations.UpdateAsync(
                new UpdateAccountRequest(
                    accountId,
                    input.Name,
                    input.Type,
                    input.IncludeInAvailableToSpend,
                    input.AccountNumber,
                    input.OpeningBalanceMinor),
                cancellationToken),
            cancellationToken);
    }

    [RelayCommand]
    public async Task ArchiveAsync(Guid accountId)
    {
        await ExecuteWriteAsync(() => operations.ArchiveAsync(accountId), CancellationToken.None);
    }

    [RelayCommand]
    public async Task RestoreAsync(Guid accountId)
    {
        await ExecuteWriteAsync(() => operations.RestoreAsync(accountId), CancellationToken.None);
    }

    private async Task<bool> ExecuteWriteAsync(Func<Task> operation, CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        try
        {
            await operation();
            await LoadAsync(cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            ErrorMessage = UserMessage(exception);
            return false;
        }
    }

    private async Task<bool> ExecuteWriteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        return await ExecuteWriteAsync(async () => { _ = await operation(); }, cancellationToken);
    }

    private static string DisplayType(AccountType type) => type switch
    {
        AccountType.EWallet => "E-Wallet",
        AccountType.Savings => "Savings Account",
        AccountType.Bank => "Bank Account",
        AccountType.Cash => "Cash",
        _ => type.ToString()
    };

    private static string UserMessage(Exception exception) => exception switch
    {
        ApplicationValidationException or ConflictException or NotFoundException => exception.Message,
        _ => "Suma could not complete that account change. Try again."
    };
}
