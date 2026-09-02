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
