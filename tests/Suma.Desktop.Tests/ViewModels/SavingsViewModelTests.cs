using Suma.Application.Abstractions.Persistence;
using Suma.Application.Savings.AddGoalContribution;
using Suma.Application.Savings.CreateSavingsGoal;
using Suma.Application.Savings.GetGoalContributionCandidates;
using Suma.Application.Savings.GetSavingsGoalDetails;
using Suma.Application.Savings.GetSavingsGoals;
using Suma.Desktop.Operations.Savings;
using Suma.Desktop.ViewModels;
using Suma.Domain.Savings;
using Xunit;

namespace Suma.Desktop.Tests.ViewModels;

public sealed class SavingsViewModelTests
{
    [Fact]
    public async Task Overlapping_loads_are_serialized_and_latest_archived_filter_wins()
    {
        var operations = new FakeSavingsOperations { DelayFirstLoad = true };
        var viewModel = new SavingsViewModel(operations);
        var first = viewModel.LoadAsync(Token); await operations.FirstLoadStarted.Task;
        var second = viewModel.SetArchivedAsync(true, Token);
        Assert.Equal(1, operations.GoalLoadCount); Assert.Equal(1, operations.MaxConcurrentGoalLoads);
        operations.ReleaseFirstLoad.SetResult(); await Task.WhenAll(first, second);
        Assert.Equal(2, operations.GoalLoadCount); Assert.Equal(1, operations.MaxConcurrentGoalLoads);
        Assert.True(viewModel.ShowArchived); Assert.True(Assert.Single(viewModel.Goals).Value.IsArchived); Assert.False(viewModel.IsLoading);
    }

    [Fact]
    public async Task Latest_goal_selection_wins_when_earlier_detail_finishes_last()
    {
        var operations = new FakeSavingsOperations(); var viewModel = new SavingsViewModel(operations); await viewModel.LoadAsync(Token);
        var first = viewModel.Goals[0]; var second = viewModel.Goals[1]; operations.DelayedDetailId = first.Id;
        var slow = viewModel.SelectGoalAsync(first.Id, Token); await operations.DetailStarted.Task;
        await viewModel.SelectGoalAsync(second.Id, Token); operations.ReleaseDetail.SetResult(); await slow;
        Assert.Equal(second.Id, viewModel.SelectedGoal?.Id);
    }

    [Fact]
    public async Task Duplicate_create_is_guarded_and_post_write_refresh_is_authoritative()
    {
        var operations = new FakeSavingsOperations { DelayCreate = true }; var viewModel = new SavingsViewModel(operations); await viewModel.LoadAsync(Token);
        var request = new CreateSavingsGoalRequest("Created", 1_000, "PHP");
        var first = viewModel.CreateAsync(request, Token); await operations.CreateStarted.Task;
        Assert.False(await viewModel.CreateAsync(request, Token)); Assert.Equal(1, operations.CreateCount);
        operations.ReleaseCreate.SetResult(); Assert.True(await first);
        Assert.Contains(viewModel.Goals, item => item.Name == "Created");
    }

    [Fact]
    public async Task Duplicate_contribution_is_guarded_and_refreshes_progress()
    {
        var operations = new FakeSavingsOperations { DelayContribution = true }; var viewModel = new SavingsViewModel(operations); await viewModel.LoadAsync(Token);
        var first = viewModel.AddContributionAsync(Guid.NewGuid(), GoalContributionType.Deposit, 100, Token); await operations.ContributionStarted.Task;
        Assert.False(await viewModel.AddContributionAsync(Guid.NewGuid(), GoalContributionType.Deposit, 100, Token)); Assert.Equal(1, operations.ContributionCount);
        operations.ReleaseContribution.SetResult(); Assert.True(await first); Assert.Equal(100, viewModel.SelectedGoal?.Value.ProgressMinor);
    }

    [Fact]
    public async Task Failed_restore_keeps_archived_filter_and_selected_goal_authoritative()
    {
        var operations = new FakeSavingsOperations { RestoreException = new InvalidOperationException() };
        var viewModel = new SavingsViewModel(operations);
        await viewModel.SetArchivedAsync(true, Token);
        var selectedGoal = Assert.IsType<SavingsGoalRowViewModel>(viewModel.SelectedGoal);
        var loadCount = operations.GoalLoadCount;

        await viewModel.RestoreAsync(Token);

        Assert.True(viewModel.ShowArchived);
        Assert.Equal(selectedGoal.Id, viewModel.SelectedGoal?.Id);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
        Assert.Equal(loadCount, operations.GoalLoadCount);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class FakeSavingsOperations : ISavingsOperations
    {
        private readonly Guid firstId = Guid.NewGuid(), secondId = Guid.NewGuid();
        private bool created; private long progress; private int activeLoads;
        public bool DelayFirstLoad { get; set; }
        public bool DelayCreate { get; set; }
        public bool DelayContribution { get; set; }
        public Guid? DelayedDetailId { get; set; }
        public int GoalLoadCount { get; private set; }
        public int MaxConcurrentGoalLoads { get; private set; }
        public int CreateCount { get; private set; }
        public int ContributionCount { get; private set; }
        public Exception? RestoreException { get; set; }
        public TaskCompletionSource FirstLoadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously); public TaskCompletionSource ReleaseFirstLoad { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource DetailStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously); public TaskCompletionSource ReleaseDetail { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CreateStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously); public TaskCompletionSource ReleaseCreate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ContributionStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously); public TaskCompletionSource ReleaseContribution { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<IReadOnlyList<SavingsGoalSummary>> GetGoalsAsync(bool archived, CancellationToken cancellationToken = default)
        {
            var call = ++GoalLoadCount; MaxConcurrentGoalLoads = Math.Max(MaxConcurrentGoalLoads, Interlocked.Increment(ref activeLoads));
            var snapshot = Goals(archived);
            try { if (DelayFirstLoad && call == 1) { FirstLoadStarted.TrySetResult(); await ReleaseFirstLoad.Task; } return snapshot; }
            finally { Interlocked.Decrement(ref activeLoads); }
        }
        private IReadOnlyList<SavingsGoalSummary> Goals(bool archived)
        {
            if (archived) return [Goal(Guid.NewGuid(), "Archived", true)];
            var rows = new List<SavingsGoalSummary> { Goal(firstId, "First", false), Goal(secondId, "Second", false) };
            if (created) rows.Add(Goal(Guid.NewGuid(), "Created", false)); return rows;
        }
        private SavingsGoalSummary Goal(Guid id, string name, bool archived) => new(id, name, 1_000, "PHP", progress, 1_000 - progress, null, null, null, archived);
        public async Task<SavingsGoalDetails> GetDetailsAsync(Guid goalId, CancellationToken cancellationToken = default)
        {
            var snapshot = Goal(goalId, goalId == secondId ? "Second" : "First", false);
            if (DelayedDetailId == goalId) { DetailStarted.TrySetResult(); await ReleaseDetail.Task; }
            return new(snapshot, []);
        }
        public Task<IReadOnlyList<GoalContributionCandidate>> GetCandidatesAsync(Guid goalId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<GoalContributionCandidate>>([]);
        public async Task<CreateSavingsGoalResult> CreateAsync(CreateSavingsGoalRequest request, CancellationToken cancellationToken = default) { CreateCount++; created = true; CreateStarted.TrySetResult(); if (DelayCreate) await ReleaseCreate.Task; return new(Guid.NewGuid(), request.Name, request.TargetAmountMinor, request.CurrencyCode, request.TargetDate, request.DestinationAccountId); }
        public async Task<AddGoalContributionResult> AddContributionAsync(AddGoalContributionRequest request, CancellationToken cancellationToken = default) { ContributionCount++; progress += request.Type == GoalContributionType.Deposit ? request.AmountMinor : -request.AmountMinor; ContributionStarted.TrySetResult(); if (DelayContribution) await ReleaseContribution.Task; return new(Guid.NewGuid(), request.SavingsGoalId, request.TransactionId, request.Type, request.AmountMinor, request.CurrencyCode); }
        public Task ArchiveAsync(Guid goalId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RestoreAsync(Guid goalId, CancellationToken cancellationToken = default) =>
            RestoreException is null ? Task.CompletedTask : Task.FromException(RestoreException);
    }
}
