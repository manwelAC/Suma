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
    private readonly object loadSync = new();
    private bool isLoading;
    private bool isSaving;
    private bool reloadRequested;
    private string? errorMessage;
    private TransactionType? selectedType;
    private long loadRequestVersion;
    private Task? activeLoad;
    private CancellationToken pendingLoadToken;

    public ObservableCollection<ActivityRowViewModel> Items { get; } = [];

    public ObservableCollection<RefundableExpenseOption> RefundableExpenses { get; } = [];

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

    public bool IsLoading { get => isLoading; private set { if (SetProperty(ref isLoading, value)) { NotifyState(); } } }

    public bool IsSaving { get => isSaving; private set => SetProperty(ref isSaving, value); }

    public string? ErrorMessage { get => errorMessage; private set { if (SetProperty(ref errorMessage, value)) OnPropertyChanged(nameof(ErrorVisibility)); } }

    public string EmptyTitle => SelectedType is null ? "No activity yet" : $"No {SelectedType.ToString()!.ToLowerInvariant()}s yet";

    public Visibility EmptyVisibility => !IsLoading && Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

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
        return new(item.Id, item.Type, type, title!, context, prefix + MoneyText.Format(item.AmountMinor, item.CurrencyCode), item.TransactionDate, DateLabel(item.TransactionDate), startsGroup);
    }

    private void ReplaceItems(IReadOnlyList<TransactionHistoryResult> results)
    {
        Items.Clear();
        DateOnly? previousDate = null;
        foreach (var item in results)
        {
            var startsGroup = previousDate != item.TransactionDate;
            Items.Add(Map(item, startsGroup));
            previousDate = item.TransactionDate;
        }

        OnPropertyChanged(nameof(EmptyVisibility));
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
    }

    private static string UserMessage(Exception exception, string action) => exception switch
    {
        ApplicationValidationException or ConflictException or NotFoundException => exception.Message,
        _ => $"Suma could not {action}. Try again."
    };
}
