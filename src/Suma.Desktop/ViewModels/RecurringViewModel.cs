using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Suma.Application.Common.Exceptions;
using Suma.Application.Recurring.CreateRecurringTransaction;
using Suma.Desktop.Operations.Recurring;

namespace Suma.Desktop.ViewModels;

public sealed class RecurringViewModel(IRecurringOperations operations) : ViewModelBase
{
    private readonly object loadSync = new();
    private bool isLoading;
    private bool isSaving;
    private bool showHistory;
    private string? errorMessage;
    private long loadVersion;
    private bool reloadRequested;
    private Task? activeLoad;
    private CancellationToken pendingToken;

    public ObservableCollection<RecurringScheduleRowViewModel> Schedules { get; } = [];
    public ObservableCollection<RecurringOccurrenceRowViewModel> Occurrences { get; } = [];
    public bool IsLoading { get => isLoading; private set { if (SetProperty(ref isLoading, value)) Notify(); } }
    public bool IsSaving { get => isSaving; private set => SetProperty(ref isSaving, value); }
    public bool ShowHistory { get => showHistory; private set => SetProperty(ref showHistory, value); }
    public string? ErrorMessage { get => errorMessage; private set { if (SetProperty(ref errorMessage, value)) Notify(); } }
    public Visibility LoadingVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EmptyOccurrencesVisibility => !IsLoading && Occurrences.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EmptySchedulesVisibility => !IsLoading && Schedules.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ErrorVisibility => string.IsNullOrEmpty(ErrorMessage) ? Visibility.Collapsed : Visibility.Visible;
    public int UpcomingCount => Occurrences.Count;
    public string UpcomingCountDisplay => UpcomingCount.ToString();
    public long UpcomingTotalDueMinor => Occurrences.Sum(x => x.Value.AmountMinor);
    public string UpcomingTotalDueDisplay => MoneyText.Format(UpcomingTotalDueMinor, Occurrences.FirstOrDefault()?.Value.CurrencyCode ?? "PHP");
    public string NextDueDisplay => Occurrences.FirstOrDefault()?.DueLabel ?? "None";
    public Visibility HasOccurrencesVisibility => Occurrences.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

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

    public async Task SetHistoryAsync(bool history, CancellationToken cancellationToken = default)
    {
        ShowHistory = history;
        await LoadAsync(cancellationToken);
    }

    public async Task<bool> CreateExpenseAsync(CreateRecurringExpenseRequest request, CancellationToken cancellationToken = default) => await SaveAsync(() => operations.CreateExpenseAsync(request, cancellationToken), "create that recurring expense", cancellationToken);
    public async Task<bool> CreateIncomeAsync(CreateRecurringIncomeRequest request, CancellationToken cancellationToken = default) => await SaveAsync(() => operations.CreateIncomeAsync(request, cancellationToken), "create that recurring income", cancellationToken);
    public async Task<bool> CreateTransferAsync(CreateRecurringTransferRequest request, CancellationToken cancellationToken = default) => await SaveAsync(() => operations.CreateTransferAsync(request, cancellationToken), "create that recurring transfer", cancellationToken);

    public async Task MarkPaidAsync(RecurringOccurrenceRowViewModel occurrence, CancellationToken cancellationToken = default)
    {
        if (IsSaving || !occurrence.CanMarkPaid) return;
        await SaveAsync(() => operations.MarkPaidAsync(occurrence.Id, cancellationToken), "mark that occurrence paid", cancellationToken);
    }

    public async Task SkipAsync(RecurringOccurrenceRowViewModel occurrence, CancellationToken cancellationToken = default)
    {
        if (IsSaving || occurrence.Value.Status != Domain.Recurring.RecurringOccurrenceStatus.Pending) return;
        await SaveAsync(async () => { await operations.SkipAsync(occurrence.Id, cancellationToken); return true; }, "skip that occurrence", cancellationToken);
    }

    public void SetError(string message) => ErrorMessage = message;

    private async Task ProcessLoadsAsync()
    {
        await Task.Yield();
        IsLoading = true;
        while (true)
        {
            long version;
            bool history;
            CancellationToken cancellationToken;
            lock (loadSync)
            {
                version = loadVersion;
                history = ShowHistory;
                cancellationToken = pendingToken;
                reloadRequested = false;
            }

            Suma.Application.Recurring.GetRecurringOverview.RecurringOverview? overview = null;
            Exception? failure = null;
            try
            {
                overview = await operations.GetOverviewAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            lock (loadSync)
            {
                if (version == loadVersion)
                {
                    if (overview is not null)
                    {
                        Schedules.Clear();
                        foreach (var item in overview.Schedules) Schedules.Add(new(item));
                        ApplyOccurrences(
                            overview.Occurrences.Select(item => new RecurringOccurrenceRowViewModel(item, overview.Today)),
                            history);
                        ErrorMessage = null;
                    }
                    else if (failure is not null)
                    {
                        ErrorMessage = UserMessage(failure, "load recurring transactions");
                    }
                }

                if (reloadRequested || version != loadVersion) continue;
                IsLoading = false;
                activeLoad = null;
                Notify();
                return;
            }
        }
    }

    private async Task<bool> SaveAsync<T>(Func<Task<T>> action, string label, CancellationToken cancellationToken)
    {
        if (IsSaving) return false;
        IsSaving = true;
        ErrorMessage = null;
        try { _ = await action(); await LoadAsync(cancellationToken); return true; }
        catch (Exception exception) { ErrorMessage = UserMessage(exception, label); return false; }
        finally { IsSaving = false; }
    }

    private void ApplyOccurrences(IEnumerable<RecurringOccurrenceRowViewModel> source, bool history)
    {
        Occurrences.Clear();
        foreach (var item in source.Where(item => history ? item.Value.Status != Domain.Recurring.RecurringOccurrenceStatus.Pending : item.Value.Status == Domain.Recurring.RecurringOccurrenceStatus.Pending).OrderBy(item => item.Value.DueDate)) Occurrences.Add(item);
        Notify();
    }

    private void Notify()
    {
        OnPropertyChanged(nameof(LoadingVisibility));
        OnPropertyChanged(nameof(EmptyOccurrencesVisibility));
        OnPropertyChanged(nameof(EmptySchedulesVisibility));
        OnPropertyChanged(nameof(ErrorVisibility));
        OnPropertyChanged(nameof(UpcomingCount));
        OnPropertyChanged(nameof(UpcomingCountDisplay));
        OnPropertyChanged(nameof(UpcomingTotalDueMinor));
        OnPropertyChanged(nameof(UpcomingTotalDueDisplay));
        OnPropertyChanged(nameof(NextDueDisplay));
        OnPropertyChanged(nameof(HasOccurrencesVisibility));
    }

    private static string UserMessage(Exception exception, string action) => exception switch
    {
        ApplicationValidationException or ConflictException or NotFoundException => exception.Message,
        ArgumentException => $"Suma could not {action}. Check the entered values.",
        _ => $"Suma could not {action}. Try again."
    };
}
