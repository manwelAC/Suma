using Suma.Application.Common.Exceptions;
using Suma.Application.Tests.TestDoubles;
using Suma.Application.Transactions.CreateExpense;
using Suma.Application.Transactions.CreateIncome;
using Suma.Application.Transactions.CreateRefund;
using Suma.Application.Transactions.CreateTransfer;
using Suma.Domain.Accounts;
using Suma.Domain.Categories;
using Suma.Domain.Transactions;
using Suma.Domain.ValueObjects;
using Xunit;

namespace Suma.Application.Tests.Transactions;

public sealed class TransactionUseCaseTests
{
    [Theory]
    [InlineData(2026, 9, 1)]
    [InlineData(2026, 9, 2)]
    public async Task CreateExpense_allows_past_and_today(int year, int month, int day)
    {
        var data = ValidExpenseData(out var account, out var category);
        var result = await new CreateExpenseUseCase(data, data, data, data, Clock).ExecuteAsync(
            new(account.Id, category.Id, 500, "PHP", new DateOnly(year, month, day)), Token);
        Assert.Equal(new DateOnly(year, month, day), result.TransactionDate);
    }

    [Fact]
    public async Task CreateExpense_future_date_is_rejected_before_add_or_save()
    {
        var data = ValidExpenseData(out var account, out var category);
        var exception = await Assert.ThrowsAsync<ApplicationValidationException>(() =>
            new CreateExpenseUseCase(data, data, data, data, Clock).ExecuteAsync(
                new(account.Id, category.Id, 500, "PHP", new DateOnly(2026, 9, 3)), Token));
        Assert.Equal("Transaction date cannot be in the future.", exception.Message);
        Assert.Equal(0, data.AddedTransactionCount);
        Assert.Equal(0, data.SaveCount);
    }

    [Fact]
    public async Task CreateExpense_valid_request_adds_transaction_and_saves_once()
    {
        var data = ValidExpenseData(out var account, out var category);
        var result = await new CreateExpenseUseCase(data, data, data, data, Clock).ExecuteAsync(
            new(account.Id, category.Id, 500, "PHP", new DateOnly(2026, 9, 1)), Token);
        Assert.Equal(TransactionType.Expense, result.Type);
        Assert.Equal(1, data.AddedTransactionCount);
        Assert.Equal(1, data.SaveCount);
    }

    [Theory]
    [InlineData("missing-account")]
    [InlineData("archived-account")]
    [InlineData("missing-category")]
    [InlineData("archived-category")]
    [InlineData("income-category")]
    [InlineData("currency")]
    public async Task CreateExpense_rejects_invalid_cross_entity_state(string scenario)
    {
        var data = ValidExpenseData(out var account, out var category);
        if (scenario == "missing-account") data.Accounts.Clear();
        if (scenario == "archived-account") account.Archive();
        if (scenario == "missing-category") data.Categories.Clear();
        if (scenario == "archived-category") category.Archive();
        if (scenario == "income-category") { data.Categories.Clear(); category = AddCategory(data, CategoryTransactionKind.Income); }
        var currency = scenario == "currency" ? "USD" : "PHP";
        await Assert.ThrowsAnyAsync<Exception>(() => new CreateExpenseUseCase(data, data, data, data, Clock).ExecuteAsync(new(account.Id, category.Id, 500, currency, new DateOnly(2026, 9, 1)), Token));
        Assert.Equal(0, data.SaveCount);
    }

    [Fact]
    public async Task CreateIncome_valid_request_succeeds()
    {
        var data = ValidIncomeData(out var account, out var category);
        var result = await new CreateIncomeUseCase(data, data, data, data, Clock).ExecuteAsync(new(account.Id, category.Id, 500, "PHP", new DateOnly(2026, 9, 1)), Token);
        Assert.Equal(TransactionType.Income, result.Type);
        Assert.Equal(1, data.SaveCount);
    }

    [Fact]
    public async Task CreateIncome_future_date_is_rejected_before_persistence()
    {
        var data = ValidIncomeData(out var account, out var category);
        await Assert.ThrowsAsync<ApplicationValidationException>(() => new CreateIncomeUseCase(data, data, data, data, Clock).ExecuteAsync(new(account.Id, category.Id, 500, "PHP", new DateOnly(2026, 9, 3)), Token));
        Assert.Equal(0, data.AddedTransactionCount);
        Assert.Equal(0, data.SaveCount);
    }

    [Theory]
    [InlineData("missing-account")]
    [InlineData("archived-account")]
    [InlineData("missing-category")]
    [InlineData("expense-category")]
    [InlineData("currency")]
    public async Task CreateIncome_rejects_invalid_cross_entity_state(string scenario)
    {
        var data = ValidIncomeData(out var account, out var category);
        if (scenario == "missing-account") data.Accounts.Clear();
        if (scenario == "archived-account") account.Archive();
        if (scenario == "missing-category") data.Categories.Clear();
        if (scenario == "expense-category") { data.Categories.Clear(); category = AddCategory(data, CategoryTransactionKind.Expense); }
        await Assert.ThrowsAnyAsync<Exception>(() => new CreateIncomeUseCase(data, data, data, data, Clock).ExecuteAsync(new(account.Id, category.Id, 500, scenario == "currency" ? "USD" : "PHP", new DateOnly(2026, 9, 1)), Token));
    }

    [Fact]
    public async Task CreateTransfer_creates_exactly_one_transaction_and_saves_once()
    {
        var data = new FakeData();
        var source = AddAccount(data, "Source");
        var destination = AddAccount(data, "Destination");
        var result = await new CreateTransferUseCase(data, data, data, Clock).ExecuteAsync(new(source.Id, destination.Id, 500, "PHP", new DateOnly(2026, 9, 1)), Token);
        Assert.Equal(TransactionType.Transfer, result.Type);
        Assert.Equal(1, data.AddedTransactionCount);
        Assert.Equal(1, data.SaveCount);
    }

    [Fact]
    public async Task CreateTransfer_future_date_is_rejected_before_persistence()
    {
        var data = new FakeData();
        var source = AddAccount(data, "Source");
        var destination = AddAccount(data, "Destination");
        await Assert.ThrowsAsync<ApplicationValidationException>(() => new CreateTransferUseCase(data, data, data, Clock).ExecuteAsync(new(source.Id, destination.Id, 500, "PHP", new DateOnly(2026, 9, 3)), Token));
        Assert.Equal(0, data.AddedTransactionCount);
        Assert.Equal(0, data.SaveCount);
    }

    [Theory]
    [InlineData("missing-source")]
    [InlineData("missing-destination")]
    [InlineData("archived-source")]
    [InlineData("same-account")]
    [InlineData("account-currencies")]
    [InlineData("request-currency")]
    public async Task CreateTransfer_rejects_invalid_state(string scenario)
    {
        var data = new FakeData();
        var source = AddAccount(data, "Source");
        var destination = AddAccount(data, "Destination", scenario == "account-currencies" ? "USD" : "PHP");
        if (scenario == "missing-source") data.Accounts.Remove(source.Id);
        if (scenario == "missing-destination") data.Accounts.Remove(destination.Id);
        if (scenario == "archived-source") source.Archive();
        if (scenario == "same-account") destination = source;
        await Assert.ThrowsAnyAsync<Exception>(() => new CreateTransferUseCase(data, data, data, Clock).ExecuteAsync(new(source.Id, destination.Id, 500, scenario == "request-currency" ? "USD" : "PHP", new DateOnly(2026, 9, 1)), Token));
        Assert.Equal(0, data.AddedTransactionCount);
    }

    [Fact]
    public async Task CreateRefund_allows_partial_and_exact_remaining_refunds()
    {
        foreach (var requested in new long[] { 200, 600 })
        {
            var data = ValidRefundData(out var original, out var account, out var category);
            data.RefundedAmountMinor = 400;
            var result = await new CreateRefundUseCase(data, data, data, data, Clock).ExecuteAsync(new(original.Id, account.Id, category.Id, requested, "PHP", new DateOnly(2026, 9, 2)), Token);
            Assert.Equal(TransactionType.Refund, result.Type);
        }
    }

    [Fact]
    public async Task CreateRefund_future_date_is_rejected_before_persistence()
    {
        var data = ValidRefundData(out var original, out var account, out var category);
        await Assert.ThrowsAsync<ApplicationValidationException>(() => new CreateRefundUseCase(data, data, data, data, Clock).ExecuteAsync(new(original.Id, account.Id, category.Id, 100, "PHP", new DateOnly(2026, 9, 3)), Token));
        Assert.Equal(0, data.AddedTransactionCount);
        Assert.Equal(0, data.SaveCount);
    }

    [Theory]
    [InlineData("missing-original")]
    [InlineData("not-expense")]
    [InlineData("missing-destination")]
    [InlineData("missing-category")]
    [InlineData("wrong-category")]
    [InlineData("currency")]
    [InlineData("exceeds")]
    public async Task CreateRefund_rejects_invalid_state(string scenario)
    {
        var data = ValidRefundData(out var original, out var account, out var category);
        if (scenario == "missing-original") data.Transactions.Clear();
        if (scenario == "not-expense") { data.Transactions.Clear(); original = Transaction.CreateIncome(account.Id, AddCategory(data, CategoryTransactionKind.Income).Id, new Money(1_000, "PHP"), new DateOnly(2026, 9, 1)); data.Transactions.Add(original.Id, original); }
        if (scenario == "missing-destination") data.Accounts.Clear();
        if (scenario == "missing-category") data.Categories.Clear();
        if (scenario == "wrong-category") { data.Categories.Clear(); category = AddCategory(data, CategoryTransactionKind.Income); }
        if (scenario == "exceeds") data.RefundedAmountMinor = 900;
        await Assert.ThrowsAnyAsync<Exception>(() => new CreateRefundUseCase(data, data, data, data, Clock).ExecuteAsync(new(original.Id, account.Id, category.Id, 200, scenario == "currency" ? "USD" : "PHP", new DateOnly(2026, 9, 2)), Token));
    }

    private static FakeData ValidExpenseData(out Account account, out Category category) { var data = new FakeData(); account = AddAccount(data, "Wallet"); category = AddCategory(data, CategoryTransactionKind.Expense); return data; }
    private static FakeData ValidIncomeData(out Account account, out Category category) { var data = new FakeData(); account = AddAccount(data, "Bank"); category = AddCategory(data, CategoryTransactionKind.Income); return data; }
    private static FakeData ValidRefundData(out Transaction original, out Account account, out Category category) { var data = ValidExpenseData(out account, out category); original = Transaction.CreateExpense(account.Id, category.Id, new Money(1_000, "PHP"), new DateOnly(2026, 9, 1)); data.Transactions.Add(original.Id, original); return data; }
    internal static Account AddAccount(FakeData data, string name, string currency = "PHP") { var account = new Account(name, AccountType.Bank, Money.Zero(currency), currency, true); data.Accounts.Add(account.Id, account); return account; }
    internal static Category AddCategory(FakeData data, CategoryTransactionKind kind) { var category = new Category(kind.ToString(), kind); data.Categories.Add(category.Id, category); return category; }
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static FakeDateProvider Clock => new(new DateOnly(2026, 9, 2));
}
