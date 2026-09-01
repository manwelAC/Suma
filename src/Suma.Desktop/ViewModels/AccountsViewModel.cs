using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Suma.Application.Accounts.CreateAccount;
using Suma.Application.Accounts.UpdateAccount;
using Suma.Application.Common.Exceptions;
using Suma.Desktop.Operations.Accounts;
using Suma.Domain.Accounts;

namespace Suma.Desktop.ViewModels;

public sealed partial class AccountsViewModel(IAccountOperations operations) : ViewModelBase
{
    private bool isLoading;
    private bool showArchived;
    private string? errorMessage;

    public ObservableCollection<AccountRowViewModel> Items { get; } = [];

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

    public string ActiveFilterLabel => ShowArchived ? "Viewing archived accounts" : "Viewing active accounts";

    public Visibility LoadingVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyVisibility => !IsLoading && Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ErrorVisibility => string.IsNullOrEmpty(ErrorMessage) ? Visibility.Collapsed : Visibility.Visible;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var results = await operations.GetAsync(ShowArchived, cancellationToken);
            Items.Clear();
            foreach (var account in results)
            {
                Items.Add(new AccountRowViewModel(
                    account.Id,
                    account.Name,
                    account.Type,
                    DisplayType(account.Type),
                    MoneyText.Format(account.BalanceMinor, account.CurrencyCode),
                    account.CurrencyCode,
                    account.IncludeInAvailableToSpend,
                    ShowArchived));
            }
        }
        catch (Exception exception)
        {
            ErrorMessage = UserMessage(exception);
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(EmptyVisibility));
        }
    }

    public async Task SetArchivedViewAsync(bool archived, CancellationToken cancellationToken = default)
    {
        ShowArchived = archived;
        await LoadAsync(cancellationToken);
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
                    input.IncludeInAvailableToSpend),
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
                    input.IncludeInAvailableToSpend),
                cancellationToken),
            cancellationToken);
    }

    [RelayCommand]
    private async Task ArchiveAsync(Guid accountId)
    {
        await ExecuteWriteAsync(() => operations.ArchiveAsync(accountId), CancellationToken.None);
    }

    [RelayCommand]
    private async Task RestoreAsync(Guid accountId)
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

    private static string DisplayType(AccountType type) => type == AccountType.EWallet ? "E-Wallet" : type.ToString();

    private static string UserMessage(Exception exception) => exception switch
    {
        ApplicationValidationException or ConflictException or NotFoundException => exception.Message,
        _ => "Suma could not complete that account change. Try again."
    };
}
