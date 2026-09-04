using Suma.Application.Common.Exceptions;
using Suma.Application.Tests.TestDoubles;
using Suma.Application.Transactions.DeleteTransaction;
using Suma.Domain.Accounts;
using Suma.Domain.Categories;
using Suma.Domain.Recurring;
using Suma.Domain.Savings;
using Suma.Domain.Transactions;
using Suma.Domain.ValueObjects;
using Xunit;

namespace Suma.Application.Tests.Transactions;

public sealed class DeleteTransactionUseCaseTests
{
    [Fact]
    public async Task DeleteTransaction_non_existent_throws_not_found()
    {
        var data = new FakeData();
        var useCase = new DeleteTransactionUseCase(data, data, data, data);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(Guid.NewGuid(), Token));
        Assert.Equal("Transaction was not found.", exception.Message);
    }

    [Fact]
    public async Task DeleteTransaction_with_active_refunds_throws_conflict()
    {
        var data = new FakeData();
        var account = AddAccount(data, "Wallet");
        var category = AddCategory(data, CategoryTransactionKind.Expense);
        var expense = Transaction.CreateExpense(account.Id, category.Id, new Money(1_000, "PHP"), new DateOnly(2026, 9, 1));
        var refund = Transaction.CreateRefund(account.Id, category.Id, expense.Id, new Money(300, "PHP"), new DateOnly(2026, 9, 2));
        data.Transactions.Add(expense.Id, expense);
        data.Transactions.Add(refund.Id, refund);

        var useCase = new DeleteTransactionUseCase(data, data, data, data);
        var exception = await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(expense.Id, Token));
        Assert.Contains("associated refunds", exception.Message);
        Assert.True(data.Transactions.ContainsKey(expense.Id));
    }

    [Fact]
    public async Task DeleteTransaction_linked_to_savings_goal_contributions_throws_conflict()
    {
        var data = new FakeData();
        var account = AddAccount(data, "Wallet");
        var category = AddCategory(data, CategoryTransactionKind.Income);
        var income = Transaction.CreateIncome(account.Id, category.Id, new Money(5_000, "PHP"), new DateOnly(2026, 9, 1));
        data.Transactions.Add(income.Id, income);
        data.AttributedAmountMinor = 1_000;

        var useCase = new DeleteTransactionUseCase(data, data, data, data);
        var exception = await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(income.Id, Token));
        Assert.Contains("savings goal", exception.Message);
        Assert.True(data.Transactions.ContainsKey(income.Id));
    }

    [Fact]
    public async Task DeleteTransaction_linked_to_recurring_occurrence_resets_occurrence_to_pending()
    {
        var data = new FakeData();
        var account = AddAccount(data, "Wallet");
        var category = AddCategory(data, CategoryTransactionKind.Expense);
        var expense = Transaction.CreateExpense(account.Id, category.Id, new Money(1_000, "PHP"), new DateOnly(2026, 9, 1));
        data.Transactions.Add(expense.Id, expense);

        var recurringId = Guid.NewGuid();
        var occurrence = new RecurringOccurrence(recurringId, new DateOnly(2026, 9, 1));
        occurrence.MarkPaid(expense.Id);
        data.Occurrences.Add(occurrence.Id, occurrence);

        var useCase = new DeleteTransactionUseCase(data, data, data, data);
        await useCase.ExecuteAsync(expense.Id, Token);

        Assert.False(data.Transactions.ContainsKey(expense.Id));
        Assert.Equal(RecurringOccurrenceStatus.Pending, occurrence.Status);
        Assert.Null(occurrence.TransactionId);
        Assert.Equal(1, data.SaveCount);
    }

    [Fact]
    public async Task DeleteTransaction_regular_expense_removes_from_store_and_saves()
    {
        var data = new FakeData();
        var account = AddAccount(data, "Wallet");
        var category = AddCategory(data, CategoryTransactionKind.Expense);
        var expense = Transaction.CreateExpense(account.Id, category.Id, new Money(1_000, "PHP"), new DateOnly(2026, 9, 1));
        data.Transactions.Add(expense.Id, expense);

        var useCase = new DeleteTransactionUseCase(data, data, data, data);
        await useCase.ExecuteAsync(expense.Id, Token);

        Assert.False(data.Transactions.ContainsKey(expense.Id));
        Assert.Equal(1, data.SaveCount);
    }

    [Fact]
    public async Task DeleteTransaction_refund_can_be_deleted()
    {
        var data = new FakeData();
        var account = AddAccount(data, "Wallet");
        var category = AddCategory(data, CategoryTransactionKind.Expense);
        var expense = Transaction.CreateExpense(account.Id, category.Id, new Money(1_000, "PHP"), new DateOnly(2026, 9, 1));
        var refund = Transaction.CreateRefund(account.Id, category.Id, expense.Id, new Money(300, "PHP"), new DateOnly(2026, 9, 2));
        data.Transactions.Add(expense.Id, expense);
        data.Transactions.Add(refund.Id, refund);

        var useCase = new DeleteTransactionUseCase(data, data, data, data);
        await useCase.ExecuteAsync(refund.Id, Token);

        Assert.False(data.Transactions.ContainsKey(refund.Id));
        Assert.True(data.Transactions.ContainsKey(expense.Id));
        Assert.Equal(1, data.SaveCount);
    }

    private static Account AddAccount(FakeData data, string name, string currency = "PHP")
    {
        var account = new Account(name, AccountType.Bank, Money.Zero(currency), currency, true);
        data.Accounts.Add(account.Id, account);
        return account;
    }

    private static Category AddCategory(FakeData data, CategoryTransactionKind kind)
    {
        var category = new Category(kind.ToString(), kind);
        data.Categories.Add(category.Id, category);
        return category;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
