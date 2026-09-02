using Suma.Application.Abstractions.Persistence;
using Suma.Application.Recurring.CreateRecurringTransaction;
using Suma.Application.Recurring.GetRecurringOverview;
using Suma.Application.Transactions;
using Suma.Desktop.Operations.Recurring;
using Suma.Desktop.ViewModels;
using Suma.Domain.Recurring;
using Suma.Domain.Transactions;
using Xunit;

namespace Suma.Desktop.Tests.ViewModels;

public sealed class RecurringViewModelTests
{
    [Fact]
    public async Task Activation_loads_schedules_and_separates_pending_from_history()
    {
        var operations = new FakeRecurringOperations();
        var viewModel = new RecurringViewModel(operations);
        await viewModel.LoadAsync(Token);
        Assert.Single(viewModel.Schedules);
        Assert.Single(viewModel.Occurrences);
        Assert.Equal("Pending", viewModel.Occurrences[0].StatusDisplay);
        await viewModel.SetHistoryAsync(true, Token);
        Assert.Single(viewModel.Occurrences);
        Assert.Equal("Skipped", viewModel.Occurrences[0].StatusDisplay);
    }

    [Fact]
    public async Task Mark_paid_and_skip_refresh_the_authoritative_overview()
    {
        var operations = new FakeRecurringOperations();
        var viewModel = new RecurringViewModel(operations);
        await viewModel.LoadAsync(Token);
        await viewModel.MarkPaidAsync(viewModel.Occurrences[0], Token);
        Assert.Equal(1, operations.MarkPaidCount);
        await viewModel.SetHistoryAsync(false, Token);
        await viewModel.SkipAsync(viewModel.Occurrences[0], Token);
        Assert.Equal(1, operations.SkipCount);
    }

    [Fact]
    public async Task Future_occurrence_does_not_invoke_mark_paid_and_duplicate_save_is_guarded()
    {
        var operations = new FakeRecurringOperations { FutureOnly = true, DelayCreate = true };
        var viewModel = new RecurringViewModel(operations);
        await viewModel.LoadAsync(Token);
        await viewModel.MarkPaidAsync(viewModel.Occurrences[0], Token);
        Assert.Equal(0, operations.MarkPaidCount);
        var request = new CreateRecurringExpenseRequest(Guid.NewGuid(), Guid.NewGuid(), Schedule());
        var first = viewModel.CreateExpenseAsync(request, Token);
        await operations.CreateStarted.Task;
        Assert.False(await viewModel.CreateExpenseAsync(request, Token));
        operations.ReleaseCreate.SetResult();
        Assert.True(await first);
        Assert.Equal(1, operations.CreateCount);
    }

    [Fact]
    public async Task Overlapping_loads_are_serialized_and_latest_filter_and_result_win()
    {
        var operations = new FakeRecurringOperations { DelayFirstOverview = true, DistinctOverviewResults = true };
        var viewModel = new RecurringViewModel(operations);
        var first = viewModel.LoadAsync(Token);
        await operations.FirstOverviewStarted.Task;
        var second = viewModel.SetHistoryAsync(true, Token);
        Assert.Equal(1, operations.OverviewCallCount);
        Assert.Equal(1, operations.MaxConcurrentOverviewCalls);
        Assert.True(viewModel.IsLoading);
        operations.ReleaseFirstOverview.SetResult();
        await Task.WhenAll(first, second);
        Assert.Equal(2, operations.OverviewCallCount);
        Assert.Equal(1, operations.MaxConcurrentOverviewCalls);
        Assert.Equal("Latest", Assert.Single(viewModel.Schedules).Title);
        Assert.Equal("Skipped", Assert.Single(viewModel.Occurrences).StatusDisplay);
        Assert.True(viewModel.ShowHistory);
        Assert.False(viewModel.IsLoading);
    }

    [Fact]
    public async Task Post_write_refresh_queues_behind_active_load_and_applies_authoritative_state()
    {
        var operations = new FakeRecurringOperations { DelayFirstOverview = true };
        var viewModel = new RecurringViewModel(operations);
        var initialLoad = viewModel.LoadAsync(Token);
        await operations.FirstOverviewStarted.Task;
        var create = viewModel.CreateExpenseAsync(new(Guid.NewGuid(), Guid.NewGuid(), Schedule()), Token);
        await operations.CreateStarted.Task;
        Assert.Equal(1, operations.OverviewCallCount);
        Assert.False(create.IsCompleted);
        operations.ReleaseFirstOverview.SetResult();
        await Task.WhenAll(initialLoad, create);
        Assert.Equal(2, operations.OverviewCallCount);
        Assert.Equal(1, operations.MaxConcurrentOverviewCalls);
        Assert.Contains(viewModel.Schedules, item => item.Title == "Created");
        Assert.False(viewModel.IsLoading);
    }

    private static RecurringScheduleInput Schedule() => new(100, RecurrenceFrequencyUnit.Day, 1, new(2026, 9, 2), null, null, null, null, "Bill", null);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class FakeRecurringOperations : IRecurringOperations
    {
        private readonly Guid scheduleId = Guid.NewGuid();
        private readonly Guid pendingId = Guid.NewGuid();
        public bool FutureOnly { get; set; }
        public bool DelayCreate { get; set; }
        public bool DelayFirstOverview { get; set; }
        public bool DistinctOverviewResults { get; set; }
        public bool CreatedVisible { get; private set; }
        public int CreateCount { get; private set; }
        public int MarkPaidCount { get; private set; }
        public int SkipCount { get; private set; }
        public int OverviewCallCount { get; private set; }
        public int MaxConcurrentOverviewCalls { get; private set; }
        private int activeOverviewCalls;
        public TaskCompletionSource CreateStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseCreate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource FirstOverviewStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirstOverview { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<RecurringOverview> GetOverviewAsync(CancellationToken cancellationToken = default)
        {
            var call = ++OverviewCallCount;
            var concurrent = Interlocked.Increment(ref activeOverviewCalls);
            MaxConcurrentOverviewCalls = Math.Max(MaxConcurrentOverviewCalls, concurrent);
            var created = CreatedVisible;
            var title = DistinctOverviewResults ? (call == 1 ? "Obsolete" : "Latest") : "Bill";
            var overview = BuildOverview(title, created);
            try
            {
                if (DelayFirstOverview && call == 1)
                {
                    FirstOverviewStarted.TrySetResult();
                    await ReleaseFirstOverview.Task;
                }

                return overview;
            }
            finally
            {
                Interlocked.Decrement(ref activeOverviewCalls);
            }
        }

        private RecurringOverview BuildOverview(string title, bool includeCreated)
        {
            var schedule = new RecurringScheduleRecord(scheduleId, TransactionType.Expense, Guid.NewGuid(), "Wallet", null, null, Guid.NewGuid(), "Bills", 100, "PHP", RecurrenceFrequencyUnit.Day, 1, null, null, null, new(2026, 9, 1), null, title, true);
            var pending = new RecurringOccurrenceRecord(pendingId, scheduleId, FutureOnly ? new(2026, 9, 3) : new(2026, 9, 2), RecurringOccurrenceStatus.Pending, null, TransactionType.Expense, 100, "PHP", "Bill", "Wallet", null, "Bills");
            var skipped = new RecurringOccurrenceRecord(Guid.NewGuid(), scheduleId, new(2026, 9, 1), RecurringOccurrenceStatus.Skipped, null, TransactionType.Expense, 100, "PHP", "Bill", "Wallet", null, "Bills");
            var schedules = includeCreated
                ? new[] { schedule, schedule with { Id = Guid.NewGuid(), Description = "Created" } }
                : [schedule];
            return new RecurringOverview(new(2026, 9, 2), schedules, [pending, skipped]);
        }

        public async Task<CreateRecurringTransactionResult> CreateExpenseAsync(CreateRecurringExpenseRequest request, CancellationToken cancellationToken = default)
        {
            CreateCount++; CreatedVisible = true; CreateStarted.TrySetResult(); if (DelayCreate) await ReleaseCreate.Task; return new(Guid.NewGuid());
        }
        public Task<CreateRecurringTransactionResult> CreateIncomeAsync(CreateRecurringIncomeRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new CreateRecurringTransactionResult(Guid.NewGuid()));
        public Task<CreateRecurringTransactionResult> CreateTransferAsync(CreateRecurringTransferRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new CreateRecurringTransactionResult(Guid.NewGuid()));
        public Task<TransactionResult> MarkPaidAsync(Guid occurrenceId, CancellationToken cancellationToken = default) { MarkPaidCount++; throw new InvalidOperationException("Fake result is not needed."); }
        public Task SkipAsync(Guid occurrenceId, CancellationToken cancellationToken = default) { SkipCount++; return Task.CompletedTask; }
    }
}
