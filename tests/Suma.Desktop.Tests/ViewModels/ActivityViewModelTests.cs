using Suma.Application.Common.Exceptions;
using Suma.Application.Accounts.CreateAccount;
using Suma.Application.Accounts.GetAccounts;
using Suma.Application.Accounts.UpdateAccount;
using Suma.Application.Categories;
using Suma.Application.Categories.CreateCategory;
using Suma.Application.Categories.GetCategories;
using Suma.Application.Categories.UpdateCategory;
using Suma.Application.Transactions;
using Suma.Application.Transactions.CreateExpense;
using Suma.Application.Transactions.CreateIncome;
using Suma.Application.Transactions.CreateRefund;
using Suma.Application.Transactions.CreateTransfer;
using Suma.Application.Transactions.GetRefundableExpenses;
using Suma.Application.Transactions.GetTransactions;
using Suma.Desktop.Operations.Accounts;
using Suma.Desktop.Operations.Categories;
using Suma.Desktop.Operations.Transactions;
using Suma.Desktop.ViewModels;
using Suma.Domain.Accounts;
using Suma.Domain.Categories;
using Suma.Domain.Transactions;
using Microsoft.UI.Xaml;
using Xunit;

namespace Suma.Desktop.Tests.ViewModels;

public sealed class ActivityViewModelTests
{
    [Fact]
    public async Task History_maps_filters_and_transaction_signs_without_classifying_refund_as_income()
    {
        var operations = new FakeTransactionOperations();
        operations.History.AddRange(
        [
            Item(TransactionType.Expense, "Lunch", 18000),
            Item(TransactionType.Income, "Salary", 1500000),
            Item(TransactionType.Transfer, "Transfer", 100000),
            Item(TransactionType.Refund, "Returned", 5000)
        ]);
        var viewModel = new ActivityViewModel(operations);

        await viewModel.LoadAsync(Token);

        Assert.StartsWith("- ", viewModel.Items.Single(item => item.Type == TransactionType.Expense).AmountDisplay);
        Assert.StartsWith("+ ", viewModel.Items.Single(item => item.Type == TransactionType.Income).AmountDisplay);
        Assert.False(viewModel.Items.Single(item => item.Type == TransactionType.Transfer).AmountDisplay.StartsWith('+'));
        var refund = viewModel.Items.Single(item => item.Type == TransactionType.Refund);
        Assert.StartsWith("+ ", refund.AmountDisplay);
        Assert.Equal("Refund", refund.TypeDisplay);
        Assert.Contains("Refund", refund.Context);

        await viewModel.SetFilterAsync(TransactionType.Transfer, Token);
        Assert.Equal(TransactionType.Transfer, operations.LastRequest?.Type);
        Assert.Equal(TransactionType.Transfer, viewModel.SelectedType);
    }

    [Fact]
    public async Task Workspace_filters_and_summaries_are_currency_isolated_and_keep_detail_selection_authoritative()
    {
        var operations = new FakeTransactionOperations();
        operations.History.AddRange(
        [
            Item(TransactionType.Income, "Salary", 100000),
            Item(TransactionType.Expense, "Lunch", 25000),
            Item(TransactionType.Refund, "Lunch refund", 5000),
            Item(TransactionType.Income, "Dollar income", 9000) with { CurrencyCode = "USD" }
        ]);
        var viewModel = new ActivityViewModel(operations);

        await viewModel.LoadAsync(Token);

        Assert.Equal("PHP", viewModel.SelectedCurrency);
        Assert.Equal(3, viewModel.Items.Count);
        Assert.Contains("1,000.00", viewModel.IncomeDisplay);
        Assert.Contains("250.00", viewModel.ExpenseDisplay);
        Assert.Contains("800.00", viewModel.NetFlowDisplay);
        Assert.NotNull(viewModel.SelectedItem);

        viewModel.SetSearch("Lunch refund");

        Assert.Single(viewModel.Items);
        Assert.Equal(TransactionType.Refund, viewModel.SelectedItem?.Type);
        Assert.Equal(Visibility.Visible, viewModel.DetailVisibility);
        Assert.Equal(1, operations.GetCount);

        viewModel.SetCurrency("USD");

        Assert.Empty(viewModel.Items);
        Assert.Null(viewModel.SelectedItem);
        Assert.Equal(Visibility.Visible, viewModel.DetailEmptyVisibility);
    }

    [Fact]
    public async Task Successful_create_refreshes_and_overlapping_submit_is_rejected()
    {
        var operations = new FakeTransactionOperations { DelayCreate = true };
        var viewModel = new ActivityViewModel(operations);
        var input = new ExpenseEditorInput(Guid.NewGuid(), Guid.NewGuid(), 100, "PHP", new(2026, 9, 2), null, null);

        var first = viewModel.CreateExpenseAsync(input, Token);
        await operations.CreateStarted.Task;
        var second = await viewModel.CreateExpenseAsync(input, Token);
        operations.ReleaseCreate.SetResult();

        Assert.False(second);
        Assert.True(await first);
        Assert.Equal(1, operations.CreateCount);
        Assert.Equal(1, operations.GetCount);
    }

    [Fact]
    public async Task Failed_create_preserves_a_user_facing_error()
    {
        var operations = new FakeTransactionOperations { Failure = new ConflictException("Refund amount exceeds the remaining refundable amount.") };
        var viewModel = new ActivityViewModel(operations);

        var succeeded = await viewModel.CreateRefundAsync(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100, "PHP", new(2026, 9, 2), null, null), Token);

        Assert.False(succeeded);
        Assert.Equal("Refund amount exceeds the remaining refundable amount.", viewModel.ErrorMessage);
        Assert.Equal(0, operations.GetCount);
    }

    [Fact]
    public async Task Filter_changed_during_load_wins_and_stale_results_are_not_applied()
    {
        var operations = new FakeTransactionOperations { DelayFirstGet = true };
        operations.History.AddRange(
        [
            Item(TransactionType.Income, "Salary", 1500000),
            Item(TransactionType.Expense, "Lunch", 18000)
        ]);
        var viewModel = new ActivityViewModel(operations);

        var initialLoad = viewModel.LoadAsync(Token);
        await operations.FirstGetStarted.Task;
        var filteredLoad = viewModel.SetFilterAsync(TransactionType.Expense, Token);
        operations.ReleaseFirstGet.SetResult();
        await Task.WhenAll(initialLoad, filteredLoad);

        Assert.Equal(2, operations.GetCount);
        Assert.Equal(TransactionType.Expense, operations.LastRequest?.Type);
        Assert.Single(viewModel.Items);
        Assert.Equal(TransactionType.Expense, viewModel.Items[0].Type);
        Assert.False(viewModel.IsLoading);
    }

    [Fact]
    public async Task Successful_write_during_load_queues_a_refresh_that_is_not_lost()
    {
        var operations = new FakeTransactionOperations
        {
            DelayFirstGet = true,
            AddCreatedToHistory = true
        };
        var viewModel = new ActivityViewModel(operations);
        var input = new ExpenseEditorInput(Guid.NewGuid(), Guid.NewGuid(), 100, "PHP", new(2026, 9, 2), "Lunch", null);

        var initialLoad = viewModel.LoadAsync(Token);
        await operations.FirstGetStarted.Task;
        var create = viewModel.CreateExpenseAsync(input, Token);
        operations.ReleaseFirstGet.SetResult();
        await Task.WhenAll(initialLoad, create);

        Assert.True(await create);
        Assert.Equal(2, operations.GetCount);
        Assert.Single(viewModel.Items);
        Assert.Equal(TransactionType.Expense, viewModel.Items[0].Type);
        Assert.False(viewModel.IsLoading);
    }

    [Fact]
    public async Task Refundable_expense_preload_failure_is_reported_without_stale_options()
    {
        var operations = new FakeTransactionOperations
        {
            RefundableFailure = new InvalidOperationException("Database unavailable.")
        };
        var viewModel = new ActivityViewModel(operations);

        var succeeded = await viewModel.LoadRefundableExpensesAsync(Token);

        Assert.False(succeeded);
        Assert.Empty(viewModel.RefundableExpenses);
        Assert.Equal("Suma could not load refundable expenses. Try again.", viewModel.ErrorMessage);
    }

    [Fact]
    public async Task Editor_option_preload_failure_is_reported_without_partial_options()
    {
        var viewModel = new TransactionEditorViewModel(
            new FailingAccountOperations(),
            new EmptyCategoryOperations());

        var succeeded = await viewModel.LoadAsync(Token);

        Assert.False(succeeded);
        Assert.Empty(viewModel.Accounts);
        Assert.Empty(viewModel.ExpenseCategories);
        Assert.Empty(viewModel.IncomeCategories);
        Assert.Equal("Suma could not load transaction options. Try again.", viewModel.ErrorMessage);
    }

    [Fact]
    public async Task Successful_delete_removes_transaction_and_refreshes_ledger()
    {
        var operations = new FakeTransactionOperations();
        var item = Item(TransactionType.Expense, "Groceries", 25000);
        operations.History.Add(item);
        var viewModel = new ActivityViewModel(operations);

        await viewModel.LoadAsync(Token);
        Assert.Single(viewModel.Items);

        var succeeded = await viewModel.DeleteTransactionAsync(item.Id, Token);

        Assert.True(succeeded);
        Assert.Empty(viewModel.Items);
        Assert.Equal(2, operations.GetCount);
    }

    [Fact]
    public async Task Failed_delete_sets_user_facing_error()
    {
        var operations = new FakeTransactionOperations
        {
            Failure = new ConflictException("Cannot delete transaction because it has associated refunds.")
        };
        var viewModel = new ActivityViewModel(operations);

        var succeeded = await viewModel.DeleteTransactionAsync(Guid.NewGuid(), Token);

        Assert.False(succeeded);
        Assert.Equal("Cannot delete transaction because it has associated refunds.", viewModel.ErrorMessage);
    }

    [Fact]
    public async Task SearchAsync_filters_transactions_and_manages_loading_state()
    {
        var operations = new FakeTransactionOperations();
        operations.History.Add(Item(TransactionType.Expense, "Groceries at Market", 25000));
        operations.History.Add(Item(TransactionType.Expense, "Electric Utility Bill", 45000));
        var viewModel = new ActivityViewModel(operations);

        await viewModel.LoadAsync(Token);
        Assert.Equal(2, viewModel.Items.Count);

        var searchTask = viewModel.SearchAsync("Groceries", Token);
        Assert.True(viewModel.IsSearching);
        Assert.True(viewModel.IsBusy);

        await searchTask;

        Assert.False(viewModel.IsSearching);
        Assert.False(viewModel.IsBusy);
        Assert.Single(viewModel.Items);
        Assert.Equal("Groceries at Market", viewModel.Items[0].Title);
    }

    [Fact]
    public async Task SearchAsync_with_empty_text_resets_filter()
    {
        var operations = new FakeTransactionOperations();
        operations.History.Add(Item(TransactionType.Expense, "Groceries at Market", 25000));
        operations.History.Add(Item(TransactionType.Expense, "Electric Utility Bill", 45000));
        var viewModel = new ActivityViewModel(operations);

        await viewModel.LoadAsync(Token);
        await viewModel.SearchAsync("Groceries", Token);
        Assert.Single(viewModel.Items);

        await viewModel.SearchAsync(string.Empty, Token);
        Assert.Equal(2, viewModel.Items.Count);
    }

    [Fact]
    public async Task DateRange_presets_and_custom_ranges_filter_transactions_correctly()
    {
        var operations = new FakeTransactionOperations();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var itemToday = Item(TransactionType.Expense, "Today expense", 1000) with { TransactionDate = today };
        var itemLastMonth = Item(TransactionType.Expense, "Last month expense", 2000) with { TransactionDate = today.AddMonths(-1) };
        var itemLastYear = Item(TransactionType.Expense, "Old expense", 5000) with { TransactionDate = new(2024, 1, 15) };
        var itemMay2025 = Item(TransactionType.Expense, "May 2025 expense", 3000) with { TransactionDate = new(2025, 5, 10) };

        operations.History.AddRange([itemToday, itemLastMonth, itemLastYear, itemMay2025]);
        var viewModel = new ActivityViewModel(operations);

        await viewModel.LoadAsync(Token);
        Assert.Equal(4, viewModel.Items.Count);

        viewModel.SetDateRange("This month");
        Assert.Single(viewModel.Items);
        Assert.Equal("Today expense", viewModel.Items[0].Title);

        viewModel.SetDateRange("Last month");
        Assert.Single(viewModel.Items);
        Assert.Equal("Last month expense", viewModel.Items[0].Title);

        viewModel.SetDateRange("May 1 – May 31, 2025");
        Assert.Single(viewModel.Items);
        Assert.Equal("May 2025 expense", viewModel.Items[0].Title);

        viewModel.SetDateRange(new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 31));
        Assert.Single(viewModel.Items);
        Assert.Equal("Old expense", viewModel.Items[0].Title);

        viewModel.SetDateRange("All time");
        Assert.Equal(4, viewModel.Items.Count);
    }

    [Fact]
    public async Task Category_and_account_filters_isolate_items_and_preserve_selection()
    {
        var operations = new FakeTransactionOperations();
        var foodExpense = Item(TransactionType.Expense, "Groceries", 25000) with { CategoryName = "Food", SourceAccountName = "Cash Wallet" };
        var rentExpense = Item(TransactionType.Expense, "Apartment Rent", 100000) with { CategoryName = "Housing", SourceAccountName = "Bank Checking" };
        var salaryIncome = Item(TransactionType.Income, "Monthly Salary", 500000) with { CategoryName = "Salary", DestinationAccountName = "Bank Checking" };

        operations.History.AddRange([foodExpense, rentExpense, salaryIncome]);
        var viewModel = new ActivityViewModel(operations);

        await viewModel.LoadAsync(Token);
        Assert.Equal(3, viewModel.Items.Count);
        Assert.Contains("Food", viewModel.Categories);
        Assert.Contains("Housing", viewModel.Categories);
        Assert.Contains("Cash Wallet", viewModel.Accounts);
        Assert.Contains("Bank Checking", viewModel.Accounts);

        // Filter by Category
        viewModel.SetCategory("Food");
        Assert.Single(viewModel.Items);
        Assert.Equal("Groceries", viewModel.Items[0].Title);

        // Filter by Account (Bank Checking matches both rent source and salary destination)
        viewModel.SetCategory("All categories");
        viewModel.SetAccount("Bank Checking");
        Assert.Equal(2, viewModel.Items.Count);

        // Combined Category + Account
        viewModel.SetCategory("Housing");
        Assert.Single(viewModel.Items);
        Assert.Equal("Apartment Rent", viewModel.Items[0].Title);

        // Preserved across reload
        await viewModel.LoadAsync(Token);
        Assert.Equal("Housing", viewModel.SelectedCategory);
        Assert.Equal("Bank Checking", viewModel.SelectedAccount);
        Assert.Single(viewModel.Items);
    }

    [Fact]
    public async Task ResetFiltersAsync_clears_all_active_filters_and_restores_view()
    {
        var operations = new FakeTransactionOperations();
        operations.History.AddRange([
            Item(TransactionType.Expense, "Groceries", 25000) with { CategoryName = "Food" },
            Item(TransactionType.Income, "Salary", 500000) with { CategoryName = "Salary" }
        ]);
        var viewModel = new ActivityViewModel(operations);

        await viewModel.LoadAsync(Token);
        viewModel.SetCategory("Food");
        viewModel.SetSearch("Groc");
        viewModel.SetDateRange("This month");

        Assert.True(viewModel.HasActiveFilters);

        await viewModel.ResetFiltersAsync(Token);

        Assert.False(viewModel.HasActiveFilters);
        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.Equal("All categories", viewModel.SelectedCategory);
        Assert.Equal("All accounts", viewModel.SelectedAccount);
        Assert.Equal("All time", viewModel.SelectedDateRange);
        Assert.Equal(2, viewModel.Items.Count);
    }

    private static TransactionHistoryResult Item(TransactionType type, string description, long amount) => new(
        Guid.NewGuid(), type, Guid.NewGuid(), "Wallet", Guid.NewGuid(), "Bank", Guid.NewGuid(), "Food", null,
        amount, "PHP", new(2026, 9, 2), description, null);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class FakeTransactionOperations : ITransactionOperations
    {
        public List<TransactionHistoryResult> History { get; } = [];
        public GetTransactionsRequest? LastRequest { get; private set; }
        public Exception? Failure { get; init; }
        public Exception? RefundableFailure { get; init; }
        public bool DelayCreate { get; init; }
        public bool DelayFirstGet { get; init; }
        public bool AddCreatedToHistory { get; init; }
        public TaskCompletionSource CreateStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseCreate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource FirstGetStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirstGet { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int CreateCount { get; private set; }
        public int GetCount { get; private set; }

        public async Task<IReadOnlyList<TransactionHistoryResult>> GetAsync(GetTransactionsRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            GetCount++;
            IReadOnlyList<TransactionHistoryResult> snapshot = History
                .Where(item => !request.Type.HasValue || item.Type == request.Type)
                .ToArray();
            if (DelayFirstGet && GetCount == 1)
            {
                FirstGetStarted.TrySetResult();
                await ReleaseFirstGet.Task.WaitAsync(cancellationToken);
            }

            return snapshot;
        }

        public Task<IReadOnlyList<RefundableExpenseResult>> GetRefundableExpensesAsync(CancellationToken cancellationToken = default) =>
            RefundableFailure is null
                ? Task.FromResult<IReadOnlyList<RefundableExpenseResult>>([])
                : Task.FromException<IReadOnlyList<RefundableExpenseResult>>(RefundableFailure);

        public Task<TransactionResult> CreateExpenseAsync(CreateExpenseRequest request, CancellationToken cancellationToken = default) => CreateAsync(TransactionType.Expense, request.AmountMinor, request.CurrencyCode, request.TransactionDate);

        public Task<TransactionResult> CreateIncomeAsync(CreateIncomeRequest request, CancellationToken cancellationToken = default) => CreateAsync(TransactionType.Income, request.AmountMinor, request.CurrencyCode, request.TransactionDate);

        public Task<TransactionResult> CreateTransferAsync(CreateTransferRequest request, CancellationToken cancellationToken = default) => CreateAsync(TransactionType.Transfer, request.AmountMinor, request.CurrencyCode, request.TransactionDate);

        public Task<TransactionResult> CreateRefundAsync(CreateRefundRequest request, CancellationToken cancellationToken = default) => CreateAsync(TransactionType.Refund, request.AmountMinor, request.CurrencyCode, request.TransactionDate);

        public Task DeleteAsync(Guid transactionId, CancellationToken cancellationToken = default)
        {
            if (Failure is not null) throw Failure;
            History.RemoveAll(item => item.Id == transactionId);
            return Task.CompletedTask;
        }

        private async Task<TransactionResult> CreateAsync(TransactionType type, long amount, string currency, DateOnly date)
        {
            if (Failure is not null) throw Failure;
            CreateCount++;
            CreateStarted.TrySetResult();
            if (DelayCreate) await ReleaseCreate.Task;
            var id = Guid.NewGuid();
            if (AddCreatedToHistory)
            {
                History.Add(new(id, type, Guid.NewGuid(), "Wallet", null, null, Guid.NewGuid(), "Food", null, amount, currency, date, "Lunch", null));
            }

            return new(id, type, null, null, null, null, amount, currency, date, null, null);
        }
    }

    private sealed class FailingAccountOperations : IAccountOperations
    {
        public Task<IReadOnlyList<AccountSummary>> GetAsync(bool archived, CancellationToken cancellationToken = default) =>
            Task.FromException<IReadOnlyList<AccountSummary>>(new InvalidOperationException("Database unavailable."));

        public Task<CreateAccountResult> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UpdateAccountResult> UpdateAsync(UpdateAccountRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task ArchiveAsync(Guid accountId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task RestoreAsync(Guid accountId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<Suma.Application.Transactions.GetTransactions.TransactionHistoryResult>> GetRecentTransactionsAsync(Guid accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Suma.Application.Transactions.GetTransactions.TransactionHistoryResult>>([]);
    }

    private sealed class EmptyCategoryOperations : ICategoryOperations
    {
        public Task<IReadOnlyList<CategoryResult>> GetAsync(GetCategoriesRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CategoryResult>>([]);

        public Task<CategoryResult> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<CategoryResult> UpdateAsync(UpdateCategoryRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task ArchiveAsync(Guid categoryId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task RestoreAsync(Guid categoryId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
