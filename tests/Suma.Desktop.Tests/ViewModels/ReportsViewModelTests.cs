using Suma.Application.Abstractions.Persistence;
using Suma.Application.Budgets.GetBudgetDetails;
using Suma.Application.Budgets.GetBudgets;
using Suma.Application.Reports.GetAccountMovementDetail;
using Suma.Application.Reports.GetFinancialReport;
using Suma.Application.Reports.GetReportOptions;
using Suma.Desktop.Operations.Reports;
using Suma.Desktop.ViewModels;
using Suma.Domain.Transactions;
using Xunit;

namespace Suma.Desktop.Tests.ViewModels;

public sealed class ReportsViewModelTests
{
    [Fact]
    public async Task Initial_state_is_cash_flow_mtd_and_section_switch_does_not_reload()
    {
        var ops = new FakeOperations(); var vm = new ReportsViewModel(ops); await vm.InitializeAsync(Token);
        Assert.Equal(ReportSection.CashFlow, vm.Section); Assert.Equal(ReportDatePreset.MonthToDate, vm.Preset); Assert.Equal("USD", vm.Currency); Assert.Equal(new(2026, 9, 1), vm.StartDate); Assert.Equal(new(2026, 9, 3), vm.EndDate); Assert.True(vm.CanExport); Assert.Equal(1, ops.Loads);
        await vm.SetSectionAsync(ReportSection.Categories, Token); Assert.Equal(1, ops.Loads);
        await vm.SetSectionAsync(ReportSection.Accounts, Token); Assert.Equal(1, ops.Loads); Assert.Equal(1, ops.DetailLoads);
    }

    [Fact]
    public async Task Presets_resolve_exact_dates()
    {
        var vm = new ReportsViewModel(new FakeOperations()); await vm.InitializeAsync(Token);
        await vm.SetPresetAsync(ReportDatePreset.LastMonth, Token); Assert.Equal((new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)), (vm.StartDate, vm.EndDate));
        await vm.SetPresetAsync(ReportDatePreset.Last30Days, Token); Assert.Equal((new DateOnly(2026, 8, 5), new DateOnly(2026, 9, 3)), (vm.StartDate, vm.EndDate));
        await vm.SetPresetAsync(ReportDatePreset.YearToDate, Token); Assert.Equal(new(2026, 1, 1), vm.StartDate);
    }

    [Fact]
    public async Task Overlapping_loads_are_serialized_latest_wins_and_export_is_snapshot_safe()
    {
        var ops = new FakeOperations(); var vm = new ReportsViewModel(ops); await vm.InitializeAsync(Token); ops.DelayNext = true;
        var first = vm.SetSelectionAsync("USD", new(2026, 8, 1), new(2026, 8, 31), Token); await ops.Started.Task; var second = vm.SetSelectionAsync("USD", new(2026, 7, 1), new(2026, 7, 31), Token);
        Assert.False(vm.CanExport); Assert.Equal(1, ops.MaxConcurrent); ops.Release.SetResult(); await Task.WhenAll(first, second);
        Assert.Equal(1, ops.MaxConcurrent); Assert.Equal(new(2026, 7, 1), vm.Report!.StartDate); Assert.True(vm.CanExport); Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task Current_failure_clears_snapshot_disables_export_and_retry_works()
    {
        var ops = new FakeOperations(); var vm = new ReportsViewModel(ops); await vm.InitializeAsync(Token); ops.Fail = true;
        await vm.SetSelectionAsync("USD", new(2026, 8, 1), new(2026, 8, 31), Token); Assert.Null(vm.Report); Assert.False(vm.CanExport); Assert.NotNull(vm.ErrorMessage); Assert.False(vm.IsLoading);
        ops.Fail = false; await vm.LoadAsync(Token); Assert.NotNull(vm.Report); Assert.True(vm.CanExport);
    }

    [Fact]
    public async Task Account_detail_is_invalidated_and_exported_only_for_exact_new_base_key()
    {
        var ops = new FakeOperations { DetailFactory = request => [Detail(request.StartDate.Month == 9 ? "September" : "August", request.StartDate)] };
        var vm = new ReportsViewModel(ops); await vm.InitializeAsync(Token); await vm.SetSectionAsync(ReportSection.Accounts, Token);
        Assert.Equal("September", Assert.Single(vm.AccountDetails).Description);

        await vm.SetSelectionAsync("USD", new(2026, 8, 1), new(2026, 8, 31), Token);

        Assert.Equal("August", Assert.Single(vm.AccountDetails).Description);
        var export = await vm.CreateExportAsync(Token);
        Assert.NotNull(export); Assert.Contains("20260801-20260831", export.FileName);
        Assert.Equal("August", Assert.Single(ops.LastCsvRequest!.AccountDetails!).Description);
        Assert.DoesNotContain(ops.LastCsvRequest.AccountDetails!, item => item.Description == "September");
    }

    [Fact]
    public async Task Delayed_stale_account_detail_cannot_overwrite_newer_detail()
    {
        var ops = new FakeOperations { DetailFactory = request => [Detail(request.StartDate.Month == 9 ? "September" : "August", request.StartDate)], DelaySeptemberDetail = true };
        var vm = new ReportsViewModel(ops); await vm.InitializeAsync(Token);
        var september = vm.SetSectionAsync(ReportSection.Accounts, Token); await ops.DetailStarted.Task;
        var august = vm.SetSelectionAsync("USD", new(2026, 8, 1), new(2026, 8, 31), Token); await august;
        Assert.Equal("August", Assert.Single(vm.AccountDetails).Description);
        ops.DetailRelease.SetResult(); await september;
        Assert.Equal("August", Assert.Single(vm.AccountDetails).Description); Assert.True(vm.CanExport);
        await vm.CreateExportAsync(Token); Assert.Equal("August", Assert.Single(ops.LastCsvRequest!.AccountDetails!).Description);
    }

    [Fact]
    public async Task Invalid_range_is_explicit_does_not_query_and_can_be_corrected()
    {
        var ops = new FakeOperations(); var vm = new ReportsViewModel(ops); await vm.InitializeAsync(Token); var priorLoads = ops.Loads;
        await vm.SetSelectionAsync("USD", new(2026, 9, 30), new(2026, 9, 1), Token);
        Assert.Equal(priorLoads, ops.Loads); Assert.Null(vm.Report); Assert.False(vm.CanExport); Assert.False(vm.IsLoading); Assert.Equal("Start date must be on or before end date.", vm.ErrorMessage);
        await vm.SetSelectionAsync("USD", new(2026, 9, 1), new(2026, 9, 30), Token);
        Assert.Equal(priorLoads + 1, ops.Loads); Assert.True(vm.CanExport); Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task Full_export_interaction_stays_busy_catches_save_failure_and_allows_retry()
    {
        var ops = new FakeOperations(); var vm = new ReportsViewModel(ops); await vm.InitializeAsync(Token); var saveStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var saves = 0;
        var first = vm.RunExportInteractionAsync(async (_, _) => { saves++; saveStarted.SetResult(); await release.Task; }, Token); await saveStarted.Task;
        Assert.True(vm.IsExporting); Assert.False(vm.CanExport); await vm.RunExportInteractionAsync((_, _) => { saves++; return Task.CompletedTask; }, Token); Assert.Equal(1, saves);
        release.SetResult(); await first; Assert.False(vm.IsExporting); Assert.True(vm.CanExport);
        await vm.RunExportInteractionAsync((_, _) => throw new IOException("disk"), Token); Assert.Equal("Suma could not save that CSV export.", vm.ErrorMessage); Assert.False(vm.IsExporting);
        await vm.RunExportInteractionAsync((_, _) => Task.CompletedTask, Token); Assert.Null(vm.ErrorMessage); Assert.True(vm.CanExport);
    }

    [Fact]
    public async Task Stale_or_failed_budget_selection_cannot_make_wrong_budget_exportable()
    {
        var firstId = Guid.NewGuid(); var secondId = Guid.NewGuid(); var failedId = Guid.NewGuid(); var ops = new FakeOperations { DelayedBudgetId = firstId };
        var vm = new ReportsViewModel(ops); await vm.InitializeAsync(Token); await vm.SetSectionAsync(ReportSection.Budget, Token);
        var first = vm.SelectBudgetAsync(firstId, Token); await ops.BudgetStarted.Task; await vm.SelectBudgetAsync(secondId, Token); ops.BudgetRelease.SetResult(); await first;
        Assert.Equal(secondId, vm.Budget!.Summary.Id); Assert.True(vm.CanExport);
        ops.FailedBudgetId = failedId; await vm.SelectBudgetAsync(failedId, Token);
        Assert.Null(vm.Budget); Assert.False(vm.CanExport); Assert.NotNull(vm.ErrorMessage);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private sealed class FakeOperations : IReportOperations
    {
        private int active; public int Loads { get; private set; }
        public int DetailLoads { get; private set; }
        public int MaxConcurrent { get; private set; }
        public bool DelayNext { get; set; }
        public bool Fail { get; set; }
        public bool DelaySeptemberDetail { get; set; }
        public Func<AccountMovementDetailRequest, IReadOnlyList<AccountMovementDetailRow>> DetailFactory { get; set; } = _ => [];
        public ReportCsvRequest? LastCsvRequest { get; private set; }
        public TaskCompletionSource DetailStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource DetailRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Guid? DelayedBudgetId { get; set; }
        public Guid? FailedBudgetId { get; set; }
        public TaskCompletionSource BudgetStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource BudgetRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously); public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<ReportOptions> GetOptionsAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ReportOptions(new(2026, 9, 3), ["USD"], "USD", [], null));
        public async Task<FinancialReportResult> GetFinancialAsync(FinancialReportRequest request, CancellationToken cancellationToken = default) { Loads++; MaxConcurrent = Math.Max(MaxConcurrent, Interlocked.Increment(ref active)); try { if (DelayNext) { DelayNext = false; Started.TrySetResult(); await Release.Task; } if (Fail) throw new InvalidOperationException(); return new(request.CurrencyCode, request.StartDate, request.EndDate, new(0, 0, 0, 0, 0), [], [], []); } finally { Interlocked.Decrement(ref active); } }
        public async Task<IReadOnlyList<AccountMovementDetailRow>> GetAccountDetailAsync(AccountMovementDetailRequest request, CancellationToken cancellationToken = default) { DetailLoads++; if (DelaySeptemberDetail && request.StartDate.Month == 9) { DelaySeptemberDetail = false; DetailStarted.SetResult(); await DetailRelease.Task; } return DetailFactory(request); }
        public async Task<BudgetDetails> GetBudgetAsync(Guid budgetId, CancellationToken cancellationToken = default) { if (DelayedBudgetId == budgetId) { DelayedBudgetId = null; BudgetStarted.SetResult(); await BudgetRelease.Task; } if (FailedBudgetId == budgetId) throw new InvalidOperationException(); return new(new BudgetSummary(budgetId, "Budget", new(2026, 9, 1), new(2026, 9, 30), 0, "USD", false), 0, 0, 0, []); }
        public Task<byte[]> GetCsvAsync(ReportCsvRequest request, CancellationToken cancellationToken = default) { LastCsvRequest = request; return Task.FromResult(Array.Empty<byte>()); }
    }
    private static AccountMovementDetailRow Detail(string description, DateOnly date) => new(Guid.NewGuid(), date, Guid.NewGuid(), "Checking", false, ReportMovementDirection.Inflow, TransactionType.Income, null, "Salary", description, 100, "USD");
}
