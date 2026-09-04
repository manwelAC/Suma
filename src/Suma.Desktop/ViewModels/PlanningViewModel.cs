using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Suma.Application.Budgets.AddBudgetAllocation;
using Suma.Application.Budgets.CreateBudget;
using Suma.Application.Budgets.GetBudgetDetails;
using Suma.Application.Common.Exceptions;
using Suma.Desktop.Operations.Budgets;

namespace Suma.Desktop.ViewModels;

public sealed class PlanningViewModel(IBudgetOperations operations) : ViewModelBase
{
    private readonly object loadSync = new();
    private bool isLoading;
    private bool isDetailsLoading;
    private bool isSaving;
    private bool showArchived;
    private bool reloadRequested;
    private long loadVersion;
    private long detailVersion;
    private Task? activeLoad;
    private CancellationToken pendingToken;
    private BudgetRowViewModel? selectedBudget;
    private string? errorMessage;
    private string expectedIncomeDisplay = string.Empty;
    private string allocatedDisplay = string.Empty;
    private string spentDisplay = string.Empty;
    private string remainingDisplay = string.Empty;

    public ObservableCollection<BudgetRowViewModel> Budgets { get; } = [];

    public ObservableCollection<BudgetAllocationRowViewModel> Allocations { get; } = [];

    public bool IsLoading { get => isLoading; private set { if (SetProperty(ref isLoading, value)) NotifyVisibility(); } }

    public bool IsDetailsLoading { get => isDetailsLoading; private set { if (SetProperty(ref isDetailsLoading, value)) NotifyVisibility(); } }

    public bool IsSaving { get => isSaving; private set => SetProperty(ref isSaving, value); }

    public bool ShowArchived
    {
        get => showArchived;
        private set
        {
            if (SetProperty(ref showArchived, value))
            {
                OnPropertyChanged(nameof(EmptyTitle));
                OnPropertyChanged(nameof(NewBudgetVisibility));
            }
        }
    }

    public BudgetRowViewModel? SelectedBudget
    {
        get => selectedBudget;
        private set
        {
            if (SetProperty(ref selectedBudget, value))
            {
                OnPropertyChanged(nameof(SelectedBudgetVisibility));
                OnPropertyChanged(nameof(ArchiveActionVisibility));
                OnPropertyChanged(nameof(RestoreActionVisibility));
                OnPropertyChanged(nameof(AddAllocationVisibility));
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

    public string ExpectedIncomeDisplay { get => expectedIncomeDisplay; private set => SetProperty(ref expectedIncomeDisplay, value); }

    public string AllocatedDisplay { get => allocatedDisplay; private set => SetProperty(ref allocatedDisplay, value); }

    public string SpentDisplay { get => spentDisplay; private set => SetProperty(ref spentDisplay, value); }

    public string RemainingDisplay { get => remainingDisplay; private set => SetProperty(ref remainingDisplay, value); }

    public long ProtectedAllocationsMinor => Allocations.Where(a => a.ReserveFromAvailable).Sum(a => a.AmountMinor);

    public string ProtectedAllocationsTotalDisplay => SelectedBudget is not null
        ? MoneyText.Format(ProtectedAllocationsMinor, SelectedBudget.CurrencyCode)
        : "PHP 0.00";

    public int ProtectedCategoriesCount => Allocations.Count(a => a.ReserveFromAvailable);
    public string ProtectedCategoriesCountDisplay => ProtectedCategoriesCount.ToString();

    public string AvailableToSpendDisplay
    {
        get
        {
            if (SelectedBudget is null) return "PHP 0.00";
            var income = SelectedBudget.ExpectedIncomeMinor;
            var protectedMinor = ProtectedAllocationsMinor;
            var available = Math.Max(0, income - protectedMinor);
            return MoneyText.Format(available, SelectedBudget.CurrencyCode);
        }
    }

    public string IncludedBalanceDisplay => SelectedBudget is not null
        ? MoneyText.Format(SelectedBudget.ExpectedIncomeMinor, SelectedBudget.CurrencyCode)
        : "PHP 0.00";

    public string ProtectedCategoriesPercentDisplay
    {
        get
        {
            var total = Allocations.Sum(a => a.AmountMinor);
            if (total <= 0) return "0% of total budget";
            var pct = (ProtectedAllocationsMinor * 100m) / total;
            return $"{pct:0.#}% of total budget";
        }
    }

    public double ProtectedCategoriesPercentValue
    {
        get
        {
            var total = Allocations.Sum(a => a.AmountMinor);
            if (total <= 0) return 0;
            return (double)Math.Clamp((ProtectedAllocationsMinor * 100m) / total, 0m, 100m);
        }
    }

    public double BudgetAvailablePercent
    {
        get
        {
            var total = Allocations.Sum(a => a.AmountMinor);
            if (total <= 0) return 100;
            var spent = Allocations.Sum(a => a.SpentMinor);
            var remaining = Math.Max(0, total - spent);
            return (double)Math.Clamp((remaining * 100m) / total, 0m, 100m);
        }
    }

    public string BudgetAvailablePercentDisplay => $"{BudgetAvailablePercent:0.#}% available";

    public string TotalIncomeDisplay => SelectedBudget is not null
        ? MoneyText.Format(SelectedBudget.ExpectedIncomeMinor, SelectedBudget.CurrencyCode)
        : "PHP 0.00";

    public string TotalExpensesDisplay => string.IsNullOrEmpty(AllocatedDisplay) ? "PHP 0.00" : AllocatedDisplay;

    public string ProjectedSavingsDisplay
    {
        get
        {
            if (SelectedBudget is null) return "PHP 0.00";
            var income = SelectedBudget.ExpectedIncomeMinor;
            var allocated = Allocations.Sum(a => a.AmountMinor);
            var savings = income - allocated;
            return MoneyText.Format(savings, SelectedBudget.CurrencyCode);
        }
    }

    public string ProjectedEndBalanceDisplay => ProjectedSavingsDisplay;

    public Visibility EmptyAllocationsVisibility => Allocations.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility HasAllocationsVisibility => Allocations.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public string EmptyTitle => ShowArchived ? "No archived budgets" : "No budgets yet";

    public Visibility LoadingVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyVisibility => !IsLoading && Budgets.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SelectedBudgetVisibility => SelectedBudget is null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility DetailsLoadingVisibility => IsDetailsLoading ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DetailsContentVisibility => SelectedBudget is not null && !IsDetailsLoading ? Visibility.Visible : Visibility.Collapsed;

    public Visibility AllocationsEmptyVisibility => SelectedBudget is not null && !IsDetailsLoading && Allocations.Count == 0
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility ErrorVisibility => string.IsNullOrEmpty(ErrorMessage) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility NewBudgetVisibility => ShowArchived ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ArchiveActionVisibility => SelectedBudget is { IsArchived: false } ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RestoreActionVisibility => SelectedBudget is { IsArchived: true } ? Visibility.Visible : Visibility.Collapsed;

    public Visibility AddAllocationVisibility => SelectedBudget is { IsArchived: false } ? Visibility.Visible : Visibility.Collapsed;

    public Task LoadAsync(CancellationToken cancellationToken = default)
    {
        lock (loadSync)
        {
            loadVersion++;
            Interlocked.Increment(ref detailVersion);
            reloadRequested = true;
            pendingToken = cancellationToken;
            activeLoad ??= ProcessLoadsAsync();
            return activeLoad;
        }
    }

    public async Task SetArchivedViewAsync(bool archived, CancellationToken cancellationToken = default)
    {
        ShowArchived = archived;
        await LoadAsync(cancellationToken);
    }

    public async Task SelectBudgetAsync(Guid? budgetId, CancellationToken cancellationToken = default)
    {
        await SelectBudgetAsync(budgetId, expectedLoadVersion: null, cancellationToken: cancellationToken);
    }

    private async Task SelectBudgetAsync(Guid? budgetId, long? expectedLoadVersion, CancellationToken cancellationToken)
    {
        if (expectedLoadVersion.HasValue && expectedLoadVersion.Value != Interlocked.Read(ref loadVersion)) return;
        var version = Interlocked.Increment(ref detailVersion);
        SelectedBudget = budgetId.HasValue ? Budgets.SingleOrDefault(item => item.Id == budgetId.Value) : null;
        Allocations.Clear();
        ClearTotals();
        if (SelectedBudget is null)
        {
            IsDetailsLoading = false;
            NotifyVisibility();
            return;
        }

        IsDetailsLoading = true;
        ErrorMessage = null;
        try
        {
            var details = await operations.GetDetailsAsync(SelectedBudget.Id, cancellationToken);
            if (version != Interlocked.Read(ref detailVersion)
                || SelectedBudget?.Id != details.Summary.Id
                || (expectedLoadVersion.HasValue && expectedLoadVersion.Value != Interlocked.Read(ref loadVersion)))
            {
                return;
            }

            ApplyDetails(details);
        }
        catch (Exception exception)
        {
            if (version == Interlocked.Read(ref detailVersion))
            {
                ErrorMessage = UserMessage(exception, "load that budget");
            }
        }
        finally
        {
            if (version == Interlocked.Read(ref detailVersion))
            {
                IsDetailsLoading = false;
                NotifyVisibility();
            }
        }
    }

    public async Task<bool> CreateAsync(BudgetEditorInput input, CancellationToken cancellationToken = default)
    {
        if (IsSaving) return false;
        IsSaving = true;
        ErrorMessage = null;
        try
        {
            var result = await operations.CreateAsync(new CreateBudgetRequest(
                input.Name,
                input.PeriodStart,
                input.PeriodEnd,
                input.ExpectedIncomeMinor,
                input.CurrencyCode), cancellationToken);
            ShowArchived = false;
            await LoadAsync(cancellationToken);
            await SelectBudgetAsync(result.Id, cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            ErrorMessage = UserMessage(exception, "create that budget");
            return false;
        }
        finally
        {
            IsSaving = false;
        }
    }

    public async Task<bool> AddAllocationAsync(BudgetAllocationEditorInput input, CancellationToken cancellationToken = default)
    {
        if (IsSaving || SelectedBudget is null) return false;
        IsSaving = true;
        ErrorMessage = null;
        try
        {
            _ = await operations.AddAllocationAsync(new AddBudgetAllocationRequest(
                SelectedBudget.Id,
                input.CategoryId,
                input.AmountMinor,
                SelectedBudget.CurrencyCode,
                input.ReserveFromAvailable), cancellationToken);
            await SelectBudgetAsync(SelectedBudget.Id, cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            ErrorMessage = UserMessage(exception, "add that allocation");
            return false;
        }
        finally
        {
            IsSaving = false;
        }
    }

    public async Task ArchiveAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedBudget is null || IsSaving) return;
        IsSaving = true;
        ErrorMessage = null;
        try
        {
            await operations.ArchiveAsync(SelectedBudget.Id, cancellationToken);
            await LoadAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            ErrorMessage = UserMessage(exception, "archive that budget");
        }
        finally
        {
            IsSaving = false;
        }
    }

    public async Task RestoreAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedBudget is null || IsSaving) return;
        var restoredId = SelectedBudget.Id;
        IsSaving = true;
        ErrorMessage = null;
        try
        {
            await operations.RestoreAsync(restoredId, cancellationToken);
            ShowArchived = false;
            await LoadAsync(cancellationToken);
            await SelectBudgetAsync(restoredId, cancellationToken);
        }
        catch (Exception exception)
        {
            ErrorMessage = UserMessage(exception, "restore that budget");
        }
        finally
        {
            IsSaving = false;
        }
    }

    public void SetError(string message) => ErrorMessage = message;

    private async Task ProcessLoadsAsync()
    {
        await Task.Yield();
        IsLoading = true;
        while (true)
        {
            long version;
            bool archived;
            CancellationToken cancellationToken;
            lock (loadSync)
            {
                version = loadVersion;
                archived = ShowArchived;
                cancellationToken = pendingToken;
                reloadRequested = false;
            }

            IReadOnlyList<Suma.Application.Budgets.GetBudgets.BudgetSummary>? results = null;
            try
            {
                results = await operations.GetAsync(archived, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                lock (loadSync)
                {
                    if (version == loadVersion) ErrorMessage = UserMessage(exception, "load budgets");
                }
            }

            Guid? selectionId = null;
            lock (loadSync)
            {
                if (version == loadVersion && results is not null)
                {
                    ErrorMessage = null;
                    var previousId = SelectedBudget?.Id;
                    Budgets.Clear();
                    foreach (var budget in results)
                    {
                        Budgets.Add(new(
                            budget.Id,
                            budget.Name,
                            budget.PeriodStart,
                            budget.PeriodEnd,
                            budget.CurrencyCode,
                            budget.ExpectedIncomeMinor,
                            budget.IsArchived));
                    }

                    selectionId = Budgets.Any(item => item.Id == previousId) ? previousId : Budgets.FirstOrDefault()?.Id;
                    NotifyVisibility();
                }
            }

            if (version == Interlocked.Read(ref loadVersion))
            {
                await SelectBudgetAsync(selectionId, version, cancellationToken);
            }

            lock (loadSync)
            {
                if (reloadRequested || version != loadVersion) continue;
                IsLoading = false;
                activeLoad = null;
                return;
            }
        }
    }

    private void ApplyDetails(BudgetDetails details)
    {
        var currency = details.Summary.CurrencyCode;
        ExpectedIncomeDisplay = MoneyText.Format(details.Summary.ExpectedIncomeMinor, currency);
        AllocatedDisplay = MoneyText.Format(details.AllocatedMinor, currency);
        SpentDisplay = MoneyText.Format(details.SpentMinor, currency);
        RemainingDisplay = MoneyText.Format(details.RemainingMinor, currency);
        Allocations.Clear();
        foreach (var allocation in details.Allocations)
        {
            Allocations.Add(new(
                allocation.Id,
                allocation.CategoryId,
                allocation.CategoryName,
                allocation.CategoryArchived,
                allocation.AmountMinor,
                allocation.SpentMinor,
                allocation.RemainingMinor,
                allocation.UtilizationPercent,
                allocation.ReserveFromAvailable,
                currency));
        }

        NotifyVisibility();
    }

    private void ClearTotals()
    {
        ExpectedIncomeDisplay = string.Empty;
        AllocatedDisplay = string.Empty;
        SpentDisplay = string.Empty;
        RemainingDisplay = string.Empty;
    }

    private void NotifyVisibility()
    {
        OnPropertyChanged(nameof(LoadingVisibility));
        OnPropertyChanged(nameof(EmptyVisibility));
        OnPropertyChanged(nameof(SelectedBudgetVisibility));
        OnPropertyChanged(nameof(DetailsLoadingVisibility));
        OnPropertyChanged(nameof(DetailsContentVisibility));
        OnPropertyChanged(nameof(AllocationsEmptyVisibility));
        OnPropertyChanged(nameof(EmptyAllocationsVisibility));
        OnPropertyChanged(nameof(HasAllocationsVisibility));
        OnPropertyChanged(nameof(ProtectedAllocationsMinor));
        OnPropertyChanged(nameof(ProtectedAllocationsTotalDisplay));
        OnPropertyChanged(nameof(ProtectedCategoriesCount));
        OnPropertyChanged(nameof(ProtectedCategoriesCountDisplay));
        OnPropertyChanged(nameof(AvailableToSpendDisplay));
        OnPropertyChanged(nameof(IncludedBalanceDisplay));
        OnPropertyChanged(nameof(ProtectedCategoriesPercentDisplay));
        OnPropertyChanged(nameof(ProtectedCategoriesPercentValue));
        OnPropertyChanged(nameof(BudgetAvailablePercent));
        OnPropertyChanged(nameof(BudgetAvailablePercentDisplay));
        OnPropertyChanged(nameof(TotalIncomeDisplay));
        OnPropertyChanged(nameof(TotalExpensesDisplay));
        OnPropertyChanged(nameof(ProjectedSavingsDisplay));
        OnPropertyChanged(nameof(ProjectedEndBalanceDisplay));
    }

    private static string UserMessage(Exception exception, string action) => exception switch
    {
        ApplicationValidationException or ConflictException or NotFoundException => exception.Message,
        ArgumentException => $"Suma could not {action}. Check the entered values.",
        _ => $"Suma could not {action}. Try again."
    };
}
