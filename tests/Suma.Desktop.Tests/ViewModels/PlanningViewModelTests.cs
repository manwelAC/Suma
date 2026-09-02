using Suma.Application.Budgets.AddBudgetAllocation;
using Suma.Application.Budgets.CreateBudget;
using Suma.Application.Budgets.GetBudgetDetails;
using Suma.Application.Budgets.GetBudgets;
using Suma.Application.Common.Exceptions;
using Suma.Desktop.Operations.Budgets;
using Suma.Desktop.ViewModels;
using Xunit;

namespace Suma.Desktop.Tests.ViewModels;

public sealed class PlanningViewModelTests
{
    [Fact]
    public async Task Active_and_archived_filters_load_real_details_and_preserve_filter_state()
    {
        var operations = FakeBudgetOperations.Create();
        var viewModel = new PlanningViewModel(operations);

        await viewModel.LoadAsync(Token);

        Assert.False(viewModel.ShowArchived);
        Assert.Single(viewModel.Budgets);
        Assert.NotNull(viewModel.SelectedBudget);
        Assert.Equal("PHP 8.00", viewModel.AllocatedDisplay);

        await viewModel.SetArchivedViewAsync(true, Token);

        Assert.True(viewModel.ShowArchived);
        Assert.Single(viewModel.Budgets);
        Assert.True(viewModel.SelectedBudget?.IsArchived);
    }

    [Fact]
    public async Task Latest_budget_selection_wins_when_an_earlier_detail_load_finishes_last()
    {
        var operations = FakeBudgetOperations.Create(includeSecondActive: true);
        var viewModel = new PlanningViewModel(operations);
        await viewModel.LoadAsync(Token);
        var first = viewModel.Budgets[0];
        var second = viewModel.Budgets[1];
        operations.DelayedBudgetId = first.Id;

        var firstSelection = viewModel.SelectBudgetAsync(first.Id, Token);
        await operations.DetailStarted.Task;
        await viewModel.SelectBudgetAsync(second.Id, Token);
        operations.ReleaseDetail.SetResult();
        await firstSelection;

        Assert.Equal(second.Id, viewModel.SelectedBudget?.Id);
        Assert.Equal(MoneyText.Format(operations.Details[second.Id].RemainingMinor, "PHP"), viewModel.RemainingDisplay);
        Assert.Equal(second.Id, operations.LastCompletedImmediateDetailId);
        Assert.False(viewModel.IsDetailsLoading);
    }

    [Fact]
    public async Task Successful_create_allocation_archive_and_restore_refresh_the_correct_state()
    {
        var operations = FakeBudgetOperations.Create();
        var viewModel = new PlanningViewModel(operations);
        await viewModel.LoadAsync(Token);

        Assert.True(await viewModel.CreateAsync(new("October", new(2026, 10, 1), new(2026, 10, 31), 2_000, "PHP"), Token));
        var createdId = viewModel.SelectedBudget!.Id;
        Assert.True(await viewModel.AddAllocationAsync(new(Guid.NewGuid(), 500, true), Token));
        Assert.Single(viewModel.Allocations);
        Assert.Equal("PHP 5.00", viewModel.AllocatedDisplay);

        await viewModel.ArchiveAsync(Token);
        Assert.DoesNotContain(viewModel.Budgets, item => item.Id == createdId);
        await viewModel.SetArchivedViewAsync(true, Token);
        await viewModel.SelectBudgetAsync(createdId, Token);
        Assert.Equal(createdId, viewModel.SelectedBudget?.Id);

        await viewModel.RestoreAsync(Token);
        Assert.False(viewModel.ShowArchived);
        Assert.Equal(createdId, viewModel.SelectedBudget?.Id);
        Assert.Contains(viewModel.Budgets, item => item.Id == createdId && !item.IsArchived);
    }

    [Fact]
    public async Task Duplicate_submit_is_guarded_and_application_errors_and_negative_remaining_are_user_visible()
    {
        var operations = FakeBudgetOperations.Create();
        operations.DelayCreate = true;
        var viewModel = new PlanningViewModel(operations);
        await viewModel.LoadAsync(Token);
        Assert.Contains('-', viewModel.RemainingDisplay);

        var input = new BudgetEditorInput("October", new(2026, 10, 1), new(2026, 10, 31), 0, "PHP");
        var first = viewModel.CreateAsync(input, Token);
        await operations.CreateStarted.Task;
        Assert.False(await viewModel.CreateAsync(input, Token));
        operations.ReleaseCreate.SetResult();
        Assert.True(await first);
        Assert.Equal(1, operations.CreateCount);

        operations.WriteFailure = new ConflictException("This budget period overlaps another active budget.");
        Assert.False(await viewModel.CreateAsync(input with { Name = "Conflict" }, Token));
        Assert.Equal("This budget period overlaps another active budget.", viewModel.ErrorMessage);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class FakeBudgetOperations : IBudgetOperations
    {
        public List<BudgetSummary> Summaries { get; } = [];
        public Dictionary<Guid, BudgetDetails> Details { get; } = [];
        public Guid? DelayedBudgetId { get; set; }
        public Guid? LastCompletedImmediateDetailId { get; private set; }
        public bool DelayCreate { get; set; }
        public Exception? WriteFailure { get; set; }
        public int CreateCount { get; private set; }
        public TaskCompletionSource DetailStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseDetail { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CreateStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseCreate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public static FakeBudgetOperations Create(bool includeSecondActive = false)
        {
            var operations = new FakeBudgetOperations();
            operations.Add("September", new(2026, 9, 1), new(2026, 9, 30), archived: false, allocated: 800, spent: 925);
            if (includeSecondActive) operations.Add("October", new(2026, 10, 1), new(2026, 10, 31), archived: false, allocated: 300, spent: 100);
            operations.Add("August", new(2026, 8, 1), new(2026, 8, 31), archived: true, allocated: 200, spent: 50);
            return operations;
        }

        public Task<IReadOnlyList<BudgetSummary>> GetAsync(bool archived, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BudgetSummary>>(Summaries.Where(item => item.IsArchived == archived).OrderByDescending(item => item.PeriodStart).ToArray());

        public async Task<BudgetDetails> GetDetailsAsync(Guid budgetId, CancellationToken cancellationToken = default)
        {
            if (DelayedBudgetId == budgetId)
            {
                DetailStarted.TrySetResult();
                await ReleaseDetail.Task.WaitAsync(cancellationToken);
            }
            else
            {
                LastCompletedImmediateDetailId = budgetId;
            }

            return Details[budgetId];
        }

        public async Task<CreateBudgetResult> CreateAsync(CreateBudgetRequest request, CancellationToken cancellationToken = default)
        {
            if (WriteFailure is not null) throw WriteFailure;
            CreateCount++;
            CreateStarted.TrySetResult();
            if (DelayCreate) await ReleaseCreate.Task.WaitAsync(cancellationToken);
            var summary = Add(request.Name, request.PeriodStart, request.PeriodEnd, archived: false, allocated: 0, spent: 0, expectedIncome: request.ExpectedIncomeMinor);
            return new(summary.Id, summary.Name, summary.PeriodStart, summary.PeriodEnd, summary.ExpectedIncomeMinor, summary.CurrencyCode);
        }

        public Task<AddBudgetAllocationResult> AddAllocationAsync(AddBudgetAllocationRequest request, CancellationToken cancellationToken = default)
        {
            if (WriteFailure is not null) throw WriteFailure;
            var existing = Details[request.BudgetId];
            var allocationId = Guid.NewGuid();
            var allocation = new BudgetAllocationDetail(allocationId, request.CategoryId, "Food", false, request.AmountMinor, 0, request.AmountMinor, 0, request.ReserveFromAvailable);
            var allocations = existing.Allocations.Append(allocation).ToArray();
            Details[request.BudgetId] = existing with
            {
                AllocatedMinor = existing.AllocatedMinor + request.AmountMinor,
                RemainingMinor = existing.RemainingMinor + request.AmountMinor,
                Allocations = allocations
            };
            return Task.FromResult(new AddBudgetAllocationResult(allocationId, request.BudgetId, request.CategoryId, request.AmountMinor, request.CurrencyCode, request.ReserveFromAvailable));
        }

        public Task ArchiveAsync(Guid budgetId, CancellationToken cancellationToken = default)
        {
            var index = Summaries.FindIndex(item => item.Id == budgetId);
            Summaries[index] = Summaries[index] with { IsArchived = true };
            Details[budgetId] = Details[budgetId] with { Summary = Summaries[index] };
            return Task.CompletedTask;
        }

        public Task RestoreAsync(Guid budgetId, CancellationToken cancellationToken = default)
        {
            if (WriteFailure is not null) throw WriteFailure;
            var index = Summaries.FindIndex(item => item.Id == budgetId);
            Summaries[index] = Summaries[index] with { IsArchived = false };
            Details[budgetId] = Details[budgetId] with { Summary = Summaries[index] };
            return Task.CompletedTask;
        }

        private BudgetSummary Add(string name, DateOnly start, DateOnly end, bool archived, long allocated, long spent, long expectedIncome = 2_000)
        {
            var summary = new BudgetSummary(Guid.NewGuid(), name, start, end, expectedIncome, "PHP", archived);
            Summaries.Add(summary);
            Details.Add(summary.Id, new BudgetDetails(summary, allocated, spent, allocated - spent, []));
            return summary;
        }
    }
}
