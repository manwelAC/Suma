using Suma.Application.Common.Exceptions;
using Suma.Application.Tests.TestDoubles;
using Suma.Application.Transactions.GetRefundableExpenses;
using Suma.Application.Transactions.GetTransactions;
using Suma.Domain.Categories;
using Suma.Domain.Transactions;
using Suma.Domain.ValueObjects;
using Xunit;

namespace Suma.Application.Tests.Transactions;

public sealed class TransactionQueryUseCaseTests
{
    [Fact]
    public async Task History_is_newest_first_filtered_bounded_and_resolves_archived_names()
    {
        var data = new FakeData();
        var account = TransactionUseCaseTests.AddAccount(data, "Archived wallet");
        var category = TransactionUseCaseTests.AddCategory(data, CategoryTransactionKind.Expense);
        account.Archive();
        category.Archive();
        var older = Transaction.CreateExpense(account.Id, category.Id, new Money(100, "PHP"), new(2026, 8, 31), "Older");
        var newer = Transaction.CreateExpense(account.Id, category.Id, new Money(200, "PHP"), new(2026, 9, 1), "Newer");
        data.Transactions.Add(older.Id, older);
        data.Transactions.Add(newer.Id, newer);

        var item = Assert.Single(await new GetTransactionsUseCase(data).ExecuteAsync(new(TransactionType.Expense, 1), Token));
        Assert.Equal(newer.Id, item.Id);
        Assert.Equal("Archived wallet", item.SourceAccountName);
        Assert.Equal(category.Name, item.CategoryName);
        await Assert.ThrowsAsync<ApplicationValidationException>(() => new GetTransactionsUseCase(data).ExecuteAsync(new(Limit: 501), Token));
    }

    [Fact]
    public async Task Refundable_expenses_expose_remaining_amount_and_context()
    {
        var data = new FakeData();
        var account = TransactionUseCaseTests.AddAccount(data, "Wallet");
        var category = TransactionUseCaseTests.AddCategory(data, CategoryTransactionKind.Expense);
        var expense = Transaction.CreateExpense(account.Id, category.Id, new Money(1_000, "PHP"), new(2026, 9, 1), "Groceries");
        var refund = Transaction.CreateRefund(account.Id, category.Id, expense.Id, new Money(250, "PHP"), new(2026, 9, 2));
        data.Transactions.Add(expense.Id, expense);
        data.Transactions.Add(refund.Id, refund);

        var item = Assert.Single(await new GetRefundableExpensesUseCase(data).ExecuteAsync(100, Token));
        Assert.Equal(750, item.RemainingAmountMinor);
        Assert.Equal("Wallet", item.SourceAccountName);
        Assert.Equal(category.Name, item.CategoryName);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
