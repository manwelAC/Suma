using Suma.Application.Overview.GetOverview;
using Suma.Desktop.Operations.Overview;
using Suma.Desktop.ViewModels;
using Xunit;

namespace Suma.Desktop.Tests.ViewModels;

public sealed class OverviewViewModelTests
{
    [Fact]
    public async Task First_load_projects_result_and_resets_loading()
    {
        var operations = new FakeOverviewOperations();
        var viewModel = new OverviewViewModel(operations);

        await viewModel.LoadAsync(Token);

        Assert.Equal("PHP", viewModel.SelectedCurrency);
        Assert.Equal("PHP 1.23", viewModel.AvailableToSpendDisplay);
        Assert.False(viewModel.IsLoading);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task Overlapping_loads_are_serialized_all_callers_complete_and_latest_currency_wins()
    {
        var operations = new FakeOverviewOperations { DelayFirst = true };
        var viewModel = new OverviewViewModel(operations);
        var first = viewModel.LoadAsync(Token);
        await operations.FirstStarted.Task;
        var second = viewModel.SelectCurrencyAsync("USD", Token);

        Assert.Equal(1, operations.MaxConcurrentOverviewLoads);
        operations.ReleaseFirst.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(2, operations.LoadCount);
        Assert.Equal(1, operations.MaxConcurrentOverviewLoads);
        Assert.Equal("USD", viewModel.SelectedCurrency);
        Assert.Equal("USD 4.56", viewModel.AvailableToSpendDisplay);
        Assert.False(viewModel.IsLoading);
    }

    [Fact]
    public async Task Stale_failure_cannot_overwrite_newer_success()
    {
        var operations = new FakeOverviewOperations { DelayFirst = true, FailFirst = true };
        var viewModel = new OverviewViewModel(operations);
        var first = viewModel.LoadAsync(Token);
        await operations.FirstStarted.Task;
        var second = viewModel.SelectCurrencyAsync("USD", Token);
        operations.ReleaseFirst.SetResult();

        await Task.WhenAll(first, second);

        Assert.Null(viewModel.ErrorMessage);
        Assert.Equal("USD", viewModel.SelectedCurrency);
    }

    [Fact]
    public async Task Current_failure_is_presented_and_loading_drains()
    {
        var operations = new FakeOverviewOperations { FailFirst = true };
        var viewModel = new OverviewViewModel(operations);

        await viewModel.LoadAsync(Token);

        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
        Assert.False(viewModel.IsLoading);
    }

    [Fact]
    public async Task Failed_currency_change_clears_old_snapshot_and_can_be_retried()
    {
        var operations = new FakeOverviewOperations();
        var viewModel = new OverviewViewModel(operations);
        await viewModel.LoadAsync(Token);
        Assert.Equal("PHP 1.23", viewModel.AvailableToSpendDisplay);
        operations.FailCurrency = "USD";

        await viewModel.SelectCurrencyAsync("USD", Token);

        Assert.Equal("USD", viewModel.SelectedCurrency);
        Assert.Equal("Unavailable", viewModel.AvailableToSpendDisplay);
        Assert.Empty(viewModel.Accounts);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
        Assert.False(viewModel.IsLoading);

        operations.FailCurrency = null;
        await viewModel.LoadAsync(Token);
        Assert.Equal("USD 4.56", viewModel.AvailableToSpendDisplay);
        Assert.Null(viewModel.ErrorMessage);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class FakeOverviewOperations : IOverviewOperations
    {
        private int active;
        public bool DelayFirst { get; set; }
        public bool FailFirst { get; set; }
        public string? FailCurrency { get; set; }
        public int LoadCount { get; private set; }
        public int MaxConcurrentOverviewLoads { get; private set; }
        public TaskCompletionSource FirstStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirst { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<OverviewResult> GetAsync(string? currencyCode, CancellationToken cancellationToken = default)
        {
            var call = ++LoadCount;
            MaxConcurrentOverviewLoads = Math.Max(MaxConcurrentOverviewLoads, Interlocked.Increment(ref active));
            try
            {
                if (call == 1 && DelayFirst)
                {
                    FirstStarted.TrySetResult();
                    await ReleaseFirst.Task;
                }
                if (call == 1 && FailFirst) throw new InvalidOperationException();
                if (currencyCode == FailCurrency) throw new InvalidOperationException();
                var authoritativeCurrency = string.IsNullOrEmpty(currencyCode) ? "PHP" : currencyCode;
                var amount = authoritativeCurrency == "USD" ? 456 : 123;
                return new(authoritativeCurrency, ["PHP", "USD"], amount, amount, 0, amount, [], null, [], [], []);
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
        }
    }
}
