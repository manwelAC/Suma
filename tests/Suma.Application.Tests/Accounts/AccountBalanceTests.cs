using Suma.Application.Accounts.GetAccountBalance;
using Suma.Application.Accounts.GetAccounts;
using Suma.Application.Common.Exceptions;
using Suma.Application.Tests.TestDoubles;
using Suma.Application.Tests.Transactions;
using Suma.Domain.Categories;
using Suma.Domain.Transactions;
using Suma.Domain.ValueObjects;
using Xunit;

namespace Suma.Application.Tests.Accounts;

public sealed class AccountBalanceTests
{
    [Fact]
    public async Task Balance_uses_opening_income_refund_expense_and_transfer_effects()
    {
        var data = new FakeData();
        var account = TransactionUseCaseTests.AddAccount(data, "Main");
        var other = TransactionUseCaseTests.AddAccount(data, "Other");
        var expense = TransactionUseCaseTests.AddCategory(data, CategoryTransactionKind.Expense);
        var income = TransactionUseCaseTests.AddCategory(data, CategoryTransactionKind.Income);
        var original = Transaction.CreateExpense(account.Id, expense.Id, new Money(300, "PHP"), new(2026, 9, 1));
        Add(data, original);
        Add(data, Transaction.CreateIncome(account.Id, income.Id, new Money(1_000, "PHP"), new(2026, 9, 1)));
        Add(data, Transaction.CreateRefund(account.Id, expense.Id, original.Id, new Money(100, "PHP"), new(2026, 9, 2)));
        Add(data, Transaction.CreateTransfer(account.Id, other.Id, new Money(200, "PHP"), new(2026, 9, 2)));
        Add(data, Transaction.CreateTransfer(other.Id, account.Id, new Money(50, "PHP"), new(2026, 9, 2)));
        var result = await new GetAccountBalanceUseCase(data, data).ExecuteAsync(account.Id, Token);
        Assert.Equal(650, result.BalanceMinor);
    }

    [Fact]
    public async Task GetAccounts_returns_only_active_UI_neutral_summaries()
    {
        var data = new FakeData();
        TransactionUseCaseTests.AddAccount(data, "Active");
        var archived = TransactionUseCaseTests.AddAccount(data, "Archived");
        archived.Archive();
        var results = await new GetAccountsUseCase(data, data).ExecuteAsync(Token);
        Assert.Single(results);
        Assert.Equal("Active", results[0].Name);
    }

    [Fact]
    public async Task GetAccountBalance_rejects_mixed_currency_ledger()
    {
        var data = MixedCurrencyData(out var account);
        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new GetAccountBalanceUseCase(data, data).ExecuteAsync(account.Id, Token));
        Assert.Equal("Ledger transaction currency does not match the account.", exception.Message);
    }

    [Fact]
    public async Task GetAccounts_rejects_mixed_currency_ledger_with_identical_semantics()
    {
        var data = MixedCurrencyData(out _);
        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new GetAccountsUseCase(data, data).ExecuteAsync(Token));
        Assert.Equal("Ledger transaction currency does not match the account.", exception.Message);
    }

    private static FakeData MixedCurrencyData(out Suma.Domain.Accounts.Account account)
    {
        var data = new FakeData();
        account = TransactionUseCaseTests.AddAccount(data, "Main", "PHP");
        var category = TransactionUseCaseTests.AddCategory(data, CategoryTransactionKind.Expense);
        Add(data, Transaction.CreateExpense(account.Id, category.Id, new Money(100, "USD"), new(2026, 9, 1)));
        return data;
    }

    private static void Add(FakeData data, Transaction transaction) => data.Transactions.Add(transaction.Id, transaction);
    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
