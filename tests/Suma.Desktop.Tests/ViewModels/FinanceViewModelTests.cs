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
            new(Guid.NewGuid(), "Wallet", AccountType.Cash, 1_250, "PHP", true, 1_250)
        ];

        public int CreateCount { get; private set; }

        public Task<IReadOnlyList<AccountSummary>> GetAsync(bool archived, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AccountSummary>>(archived ? [] : items.ToArray());

        public Task<CreateAccountResult> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken = default)
        {
            var id = Guid.NewGuid();
            items.Add(new AccountSummary(id, request.Name, request.Type, request.OpeningBalanceMinor, request.CurrencyCode, request.IncludeInAvailableToSpend, request.OpeningBalanceMinor, request.AccountNumber));
            CreateCount++;
            return Task.FromResult(new CreateAccountResult(id, request.Name, request.Type, request.OpeningBalanceMinor, request.CurrencyCode, request.IncludeInAvailableToSpend, request.AccountNumber));
        }

        public Task<UpdateAccountResult> UpdateAsync(UpdateAccountRequest request, CancellationToken cancellationToken = default)
        {
            var index = items.FindIndex(item => item.Id == request.AccountId);
            long openingMinor = 0;
            if (index >= 0)
            {
                var existing = items[index];
                openingMinor = request.OpeningBalanceMinor ?? existing.OpeningBalanceMinor;
                var balanceMinor = request.OpeningBalanceMinor.HasValue ? request.OpeningBalanceMinor.Value : existing.BalanceMinor;
                items[index] = existing with
                {
                    Name = request.Name,
                    Type = request.Type,
                    IncludeInAvailableToSpend = request.IncludeInAvailableToSpend,
                    AccountNumber = request.AccountNumber,
                    OpeningBalanceMinor = openingMinor,
                    BalanceMinor = balanceMinor
                };
            }
            return Task.FromResult(new UpdateAccountResult(request.AccountId, request.Name, request.Type, request.IncludeInAvailableToSpend, request.AccountNumber, openingMinor));
        }

        public Task ArchiveAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RestoreAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<Suma.Application.Transactions.GetTransactions.TransactionHistoryResult>> GetRecentTransactionsAsync(Guid accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Suma.Application.Transactions.GetTransactions.TransactionHistoryResult>>([]);
    }

    [Fact]
    public async Task Accounts_view_model_create_and_update_with_account_number_and_opening_balance()
    {
        var operations = new FakeAccountOperations();
        var viewModel = new AccountsViewModel(operations);

        await viewModel.LoadAsync(Token);

        // Create an e-wallet with mobile number
        var created = await viewModel.CreateAsync(
            new AccountEditorInput("GCash", AccountType.EWallet, true, 100_000, "PHP", "09171234567"), Token);
        Assert.True(created);

        var gcash = viewModel.ActiveAccounts.First(a => a.Name == "GCash");
        Assert.Equal("•••• 4567", gcash.MaskedNumber);
        Assert.Equal("09171234567", gcash.AccountNumber);
        Assert.True(gcash.HasAccountNumber);

        // Update opening balance and account number
        viewModel.SelectAccount(gcash);
        var updated = await viewModel.UpdateAsync(
            gcash.Id,
            new AccountEditorInput("GCash", AccountType.EWallet, true, 250_000, "PHP", "09189998888"), Token);
        Assert.True(updated);

        var refreshed = viewModel.ActiveAccounts.First(a => a.Name == "GCash");
        Assert.Equal("•••• 8888", refreshed.MaskedNumber);
        Assert.Equal("09189998888", refreshed.AccountNumber);
        Assert.Contains("2,500.00", refreshed.BalanceDisplay);
    }

    [Fact]
    public async Task Accounts_view_model_computes_included_and_excluded_balances_and_active_count()
    {
        var operations = new FakeAccountOperations();
        var viewModel = new AccountsViewModel(operations);

        await viewModel.LoadAsync(Token);
        // Default fake has 1 Cash Wallet with 1,250 minor (12.50 PHP), included in ATS
        Assert.Single(viewModel.ActiveAccounts);
        Assert.Contains("12.50", viewModel.IncludedBalanceDisplay);
        Assert.Contains("0.00", viewModel.ExcludedBalanceDisplay);
        Assert.Equal(1, viewModel.ActiveAccountsCount);

        // Add a bank account excluded from ATS
        await viewModel.CreateAsync(
            new AccountEditorInput("Vault", AccountType.Bank, false, 50_000, "PHP"), Token);

        Assert.Equal(2, viewModel.ActiveAccounts.Count);
        Assert.Contains("12.50", viewModel.IncludedBalanceDisplay);
        Assert.Contains("500.00", viewModel.ExcludedBalanceDisplay);
        Assert.Equal(2, viewModel.ActiveAccountsCount);
    }

    [Fact]
    public async Task Accounts_view_model_card_styling_and_selection()
    {
        var operations = new FakeAccountOperations();
        var viewModel = new AccountsViewModel(operations);

        await viewModel.LoadAsync(Token);
        var walletCard = viewModel.ActiveAccounts[0];

        // Cash wallet card styling assertions
        Assert.True(walletCard.IsWallet);
        Assert.Equal("WALLET", walletCard.MaskedNumber);
        Assert.Equal("Included in ATS", walletCard.AtsBadgeText);

        // Selection
        Assert.Equal(walletCard, viewModel.SelectedAccount);
        Assert.True(walletCard.IsSelected);
        Assert.Equal("Wallet", viewModel.SelectedAccountName);

        // Theme switching
        viewModel.SetCardTheme(2);
        Assert.Equal(2, viewModel.SelectedThemeIndex);
        Assert.Equal(2, walletCard.ThemeIndex);
    }

    [Fact]
    public async Task Accounts_view_model_toggle_ats_updates_metrics()
    {
        var operations = new FakeAccountOperations();
        var viewModel = new AccountsViewModel(operations);

        await viewModel.LoadAsync(Token);
        var wallet = viewModel.ActiveAccounts[0];
        Assert.True(wallet.IncludeInAvailableToSpend);

        // Toggle to excluded
        await viewModel.ToggleAtsAsync(wallet, Token);

        Assert.False(viewModel.ActiveAccounts[0].IncludeInAvailableToSpend);
        Assert.Contains("0.00", viewModel.IncludedBalanceDisplay);
        Assert.Contains("12.50", viewModel.ExcludedBalanceDisplay);
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
