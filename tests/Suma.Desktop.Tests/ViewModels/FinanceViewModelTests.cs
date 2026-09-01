using Suma.Application.Accounts.CreateAccount;
using Suma.Application.Accounts.GetAccounts;
using Suma.Application.Accounts.UpdateAccount;
using Suma.Application.Categories;
using Suma.Application.Categories.CreateCategory;
using Suma.Application.Categories.GetCategories;
using Suma.Application.Categories.UpdateCategory;
using Suma.Desktop.Operations.Accounts;
using Suma.Desktop.Operations.Categories;
using Suma.Desktop.ViewModels;
using Suma.Domain.Accounts;
using Suma.Domain.Categories;
using Xunit;

namespace Suma.Desktop.Tests.ViewModels;

public sealed class FinanceViewModelTests
{
    [Fact]
    public void Money_text_uses_decimal_and_checked_minor_units()
    {
        Assert.True(MoneyText.TryParseMinor("12.34", out var amountMinor));
        Assert.Equal(1_234, amountMinor);
        Assert.False(MoneyText.TryParseMinor("12.345", out _));
        Assert.False(MoneyText.TryParseMinor("not money", out _));
    }

    [Fact]
    public async Task Accounts_view_model_loads_real_results_and_refreshes_after_create()
    {
        var operations = new FakeAccountOperations();
        var viewModel = new AccountsViewModel(operations);

        await viewModel.LoadAsync(Token);
        Assert.Single(viewModel.Items);
        Assert.Contains("PHP", viewModel.Items[0].BalanceDisplay);

        Assert.True(await viewModel.CreateAsync(
            new AccountEditorInput("Savings", AccountType.Savings, true, 500, "PHP"), Token));
        Assert.Equal(2, viewModel.Items.Count);
        Assert.Equal(1, operations.CreateCount);
    }

    [Fact]
    public async Task Categories_view_model_preserves_kind_and_archive_filters()
    {
        var operations = new FakeCategoryOperations();
        var viewModel = new CategoriesViewModel(operations);

        await viewModel.LoadAsync(Token);
        Assert.Equal(CategoryTransactionKind.Expense, viewModel.SelectedKind);
        Assert.Single(viewModel.Items);

        await viewModel.SetKindAsync(CategoryTransactionKind.Income, Token);
        Assert.Equal(CategoryTransactionKind.Income, operations.LastRequest?.Kind);

        await viewModel.SetArchivedViewAsync(true, Token);
        Assert.True(operations.LastRequest?.Archived);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class FakeAccountOperations : IAccountOperations
    {
        private readonly List<AccountSummary> items =
        [
            new(Guid.NewGuid(), "Wallet", AccountType.Cash, 1_250, "PHP", true)
        ];

        public int CreateCount { get; private set; }

        public Task<IReadOnlyList<AccountSummary>> GetAsync(bool archived, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AccountSummary>>(archived ? [] : items.ToArray());

        public Task<CreateAccountResult> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken = default)
        {
            var id = Guid.NewGuid();
            items.Add(new AccountSummary(id, request.Name, request.Type, request.OpeningBalanceMinor, request.CurrencyCode, request.IncludeInAvailableToSpend));
            CreateCount++;
            return Task.FromResult(new CreateAccountResult(id, request.Name, request.Type, request.OpeningBalanceMinor, request.CurrencyCode, request.IncludeInAvailableToSpend));
        }

        public Task<UpdateAccountResult> UpdateAsync(UpdateAccountRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new UpdateAccountResult(request.AccountId, request.Name, request.Type, request.IncludeInAvailableToSpend));

        public Task ArchiveAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RestoreAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeCategoryOperations : ICategoryOperations
    {
        public GetCategoriesRequest? LastRequest { get; private set; }

        public Task<IReadOnlyList<CategoryResult>> GetAsync(GetCategoriesRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            IReadOnlyList<CategoryResult> results = request.Archived
                ? []
                : [new CategoryResult(Guid.NewGuid(), request.Kind == CategoryTransactionKind.Expense ? "Food" : "Salary", request.Kind, null, null, false, false)];
            return Task.FromResult(results);
        }

        public Task<CategoryResult> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CategoryResult(Guid.NewGuid(), request.Name, request.Kind, request.ParentCategoryId, null, false, false));

        public Task<CategoryResult> UpdateAsync(UpdateCategoryRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CategoryResult(request.CategoryId, request.Name, CategoryTransactionKind.Expense, request.ParentCategoryId, null, false, false));

        public Task ArchiveAsync(Guid categoryId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RestoreAsync(Guid categoryId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
