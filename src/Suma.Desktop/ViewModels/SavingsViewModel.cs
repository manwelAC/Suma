using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Suma.Application.Common.Exceptions;
using Suma.Application.Savings.AddGoalContribution;
using Suma.Application.Savings.CreateSavingsGoal;
using Suma.Desktop.Operations.Savings;
using Suma.Domain.Savings;

namespace Suma.Desktop.ViewModels;

public sealed class SavingsViewModel(ISavingsOperations operations) : ViewModelBase
{
    private readonly object loadSync = new();
    private bool isLoading, isDetailsLoading, isCandidatesLoading, isSaving, showArchived, reloadRequested;
    private long loadVersion, detailVersion, candidateVersion;
    private Task? activeLoad;
    private CancellationToken pendingToken;
    private SavingsGoalRowViewModel? selectedGoal;
    private string? errorMessage;

    public ObservableCollection<SavingsGoalRowViewModel> Goals { get; } = [];
    public ObservableCollection<GoalContributionRowViewModel> Contributions { get; } = [];
    public ObservableCollection<GoalCandidateRowViewModel> Candidates { get; } = [];
    public bool IsLoading { get => isLoading; private set { if (SetProperty(ref isLoading, value)) Notify(); } }
    public bool IsDetailsLoading { get => isDetailsLoading; private set { if (SetProperty(ref isDetailsLoading, value)) Notify(); } }
    public bool IsCandidatesLoading { get => isCandidatesLoading; private set => SetProperty(ref isCandidatesLoading, value); }
    public bool IsSaving { get => isSaving; private set => SetProperty(ref isSaving, value); }
    public bool ShowArchived { get => showArchived; private set { if (SetProperty(ref showArchived, value)) Notify(); } }
    public SavingsGoalRowViewModel? SelectedGoal { get => selectedGoal; private set { if (SetProperty(ref selectedGoal, value)) Notify(); } }
    public string? ErrorMessage { get => errorMessage; private set { if (SetProperty(ref errorMessage, value)) Notify(); } }
    public Visibility LoadingVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EmptyVisibility => !IsLoading && Goals.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DetailsVisibility => SelectedGoal is not null && !IsDetailsLoading ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ContributionsEmptyVisibility => SelectedGoal is not null && !IsDetailsLoading && Contributions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ErrorVisibility => string.IsNullOrEmpty(ErrorMessage) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility NewGoalVisibility => ShowArchived ? Visibility.Collapsed : Visibility.Visible;
    public Visibility AddContributionVisibility => SelectedGoal is { Value.IsArchived: false } ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ArchiveVisibility => SelectedGoal is { Value.IsArchived: false } ? Visibility.Visible : Visibility.Collapsed;
    public Visibility RestoreVisibility => SelectedGoal is { Value.IsArchived: true } ? Visibility.Visible : Visibility.Collapsed;

    public Task LoadAsync(CancellationToken cancellationToken = default)
    {
        lock (loadSync)
        {
            loadVersion++; Interlocked.Increment(ref detailVersion); reloadRequested = true; pendingToken = cancellationToken;
            activeLoad ??= ProcessLoadsAsync(); return activeLoad;
        }
    }

    public async Task SetArchivedAsync(bool archived, CancellationToken cancellationToken = default) { ShowArchived = archived; await LoadAsync(cancellationToken); }

    public Task SelectGoalAsync(Guid? goalId, CancellationToken cancellationToken = default) => SelectGoalAsync(goalId, null, cancellationToken);

    private async Task SelectGoalAsync(Guid? goalId, long? expectedLoadVersion, CancellationToken cancellationToken)
    {
        if (expectedLoadVersion.HasValue && expectedLoadVersion != Interlocked.Read(ref loadVersion)) return;
        var version = Interlocked.Increment(ref detailVersion);
        SelectedGoal = goalId.HasValue ? Goals.SingleOrDefault(item => item.Id == goalId) : null;
        Contributions.Clear(); Candidates.Clear(); Notify();
        if (SelectedGoal is null) return;
        IsDetailsLoading = true;
        try
        {
            var details = await operations.GetDetailsAsync(SelectedGoal.Id, cancellationToken);
            if (version != Interlocked.Read(ref detailVersion) || SelectedGoal?.Id != details.Summary.Id || (expectedLoadVersion.HasValue && expectedLoadVersion != Interlocked.Read(ref loadVersion))) return;
            SelectedGoal = new(details.Summary);
            Contributions.Clear(); foreach (var item in details.Contributions) Contributions.Add(new(item));
            ErrorMessage = null;
        }
        catch (Exception exception) { if (version == Interlocked.Read(ref detailVersion)) ErrorMessage = UserMessage(exception, "load that savings goal"); }
        finally { if (version == Interlocked.Read(ref detailVersion)) { IsDetailsLoading = false; Notify(); } }
    }

    public async Task<bool> LoadCandidatesAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedGoal is null) return false;
        var goalId = SelectedGoal.Id; var version = Interlocked.Increment(ref candidateVersion); IsCandidatesLoading = true;
        try
        {
            var results = await operations.GetCandidatesAsync(goalId, cancellationToken);
            if (version != Interlocked.Read(ref candidateVersion) || SelectedGoal?.Id != goalId) return false;
            Candidates.Clear(); foreach (var item in results) Candidates.Add(new(item)); ErrorMessage = null; return true;
        }
        catch (Exception exception) { if (version == Interlocked.Read(ref candidateVersion)) ErrorMessage = UserMessage(exception, "load contribution options"); return false; }
        finally { if (version == Interlocked.Read(ref candidateVersion)) IsCandidatesLoading = false; }
    }

    public async Task<bool> CreateAsync(CreateSavingsGoalRequest request, CancellationToken cancellationToken = default)
    {
        if (IsSaving) return false; IsSaving = true; ErrorMessage = null;
        try { var result = await operations.CreateAsync(request, cancellationToken); ShowArchived = false; await LoadAsync(cancellationToken); await SelectGoalAsync(result.Id, cancellationToken); return true; }
        catch (Exception exception) { ErrorMessage = UserMessage(exception, "create that savings goal"); return false; }
        finally { IsSaving = false; }
    }

    public async Task<bool> AddContributionAsync(Guid transactionId, GoalContributionType type, long amountMinor, CancellationToken cancellationToken = default)
    {
        if (IsSaving || SelectedGoal is null) return false; IsSaving = true; ErrorMessage = null; var goalId = SelectedGoal.Id;
        try { _ = await operations.AddContributionAsync(new(goalId, transactionId, type, amountMinor, SelectedGoal.Value.CurrencyCode), cancellationToken); await LoadAsync(cancellationToken); await SelectGoalAsync(goalId, cancellationToken); return true; }
        catch (Exception exception) { ErrorMessage = UserMessage(exception, "add that contribution"); return false; }
        finally { IsSaving = false; }
    }

    public async Task ArchiveAsync(CancellationToken cancellationToken = default) { if (IsSaving || SelectedGoal is null) return; IsSaving = true; try { await operations.ArchiveAsync(SelectedGoal.Id, cancellationToken); await LoadAsync(cancellationToken); } catch (Exception ex) { ErrorMessage = UserMessage(ex, "archive that savings goal"); } finally { IsSaving = false; } }
    public async Task RestoreAsync(CancellationToken cancellationToken = default) { if (IsSaving || SelectedGoal is null) return; var id = SelectedGoal.Id; IsSaving = true; try { await operations.RestoreAsync(id, cancellationToken); ShowArchived = false; await LoadAsync(cancellationToken); await SelectGoalAsync(id, cancellationToken); } catch (Exception ex) { ErrorMessage = UserMessage(ex, "restore that savings goal"); } finally { IsSaving = false; } }
    public void SetError(string message) => ErrorMessage = message;

    private async Task ProcessLoadsAsync()
    {
        await Task.Yield(); IsLoading = true;
        while (true)
        {
            long version; bool archived; CancellationToken token;
            lock (loadSync) { version = loadVersion; archived = ShowArchived; token = pendingToken; reloadRequested = false; }
            IReadOnlyList<Application.Savings.GetSavingsGoals.SavingsGoalSummary>? results = null; Exception? failure = null;
            try { results = await operations.GetGoalsAsync(archived, token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception exception) { failure = exception; }
            Guid? selection = null;
            lock (loadSync)
            {
                if (version == loadVersion)
                {
                    if (results is not null)
                    {
                        var previous = SelectedGoal?.Id; Goals.Clear(); foreach (var item in results) Goals.Add(new(item));
                        selection = Goals.Any(item => item.Id == previous) ? previous : Goals.FirstOrDefault()?.Id; ErrorMessage = null; Notify();
                    }
                    else if (failure is not null) ErrorMessage = UserMessage(failure, "load savings goals");
                }
            }
            if (version == Interlocked.Read(ref loadVersion) && results is not null) await SelectGoalAsync(selection, version, token);
            lock (loadSync) { if (reloadRequested || version != loadVersion) continue; IsLoading = false; activeLoad = null; Notify(); return; }
        }
    }

    private void Notify()
    {
        OnPropertyChanged(nameof(LoadingVisibility)); OnPropertyChanged(nameof(EmptyVisibility)); OnPropertyChanged(nameof(DetailsVisibility));
        OnPropertyChanged(nameof(ContributionsEmptyVisibility)); OnPropertyChanged(nameof(ErrorVisibility)); OnPropertyChanged(nameof(NewGoalVisibility));
        OnPropertyChanged(nameof(AddContributionVisibility)); OnPropertyChanged(nameof(ArchiveVisibility)); OnPropertyChanged(nameof(RestoreVisibility));
    }

    private static string UserMessage(Exception exception, string action) => exception switch
    {
        ApplicationValidationException or ConflictException or NotFoundException => exception.Message,
        ArgumentException => $"Suma could not {action}. Check the entered values.",
        _ => $"Suma could not {action}. Try again."
    };
}
