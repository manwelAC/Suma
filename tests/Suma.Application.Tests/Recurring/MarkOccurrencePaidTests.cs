using Suma.Application.Recurring.MarkOccurrencePaid;
using Suma.Application.Common.Exceptions;
using Suma.Application.Tests.TestDoubles;
using Suma.Application.Tests.Transactions;
using Suma.Domain.Categories;
using Suma.Domain.Recurring;
using Suma.Domain.Transactions;
using Suma.Domain.ValueObjects;
using Xunit;

namespace Suma.Application.Tests.Recurring;

public sealed class MarkOccurrencePaidTests
{
    [Theory]
    [InlineData(2026, 9, 1)]
    [InlineData(2026, 9, 2)]
    public async Task Occurrence_due_in_past_or_today_can_be_paid(int year, int month, int day)
    {
        var dueDate = new DateOnly(year, month, day);
        var data = ValidData(TransactionType.Expense, out var occurrence, dueDate);
        var result = await UseCase(data).ExecuteAsync(occurrence.Id, Token);
        Assert.Equal(dueDate, result.TransactionDate);
        Assert.Equal(RecurringOccurrenceStatus.Paid, occurrence.Status);
    }

    [Fact]
    public async Task Future_occurrence_is_rejected_without_mutation_or_persistence()
    {
        var data = ValidData(TransactionType.Expense, out var occurrence, new DateOnly(2026, 9, 3));
        await Assert.ThrowsAsync<ApplicationValidationException>(() =>
            UseCase(data).ExecuteAsync(occurrence.Id, Token));
        Assert.Equal(RecurringOccurrenceStatus.Pending, occurrence.Status);
        Assert.Null(occurrence.TransactionId);
        Assert.Equal(0, data.AddedTransactionCount);
        Assert.Equal(0, data.SaveCount);
    }

    [Theory]
    [InlineData(TransactionType.Expense)]
    [InlineData(TransactionType.Income)]
    [InlineData(TransactionType.Transfer)]
    public async Task Pending_occurrence_creates_expected_single_transaction_and_becomes_paid(TransactionType type)
    {
        var data = ValidData(type, out var occurrence);
        var result = await UseCase(data).ExecuteAsync(occurrence.Id, Token);
        Assert.Equal(type, result.Type);
        Assert.Equal(RecurringOccurrenceStatus.Paid, occurrence.Status);
        Assert.Equal(result.Id, occurrence.TransactionId);
        Assert.Equal(1, data.AddedTransactionCount);
        Assert.Equal(1, data.SaveCount);
    }

    [Theory]
    [InlineData("paid")]
    [InlineData("skipped")]
    [InlineData("missing-occurrence")]
    [InlineData("missing-recurring")]
    public async Task Invalid_occurrence_workflow_is_rejected_without_save(string scenario)
    {
        var data = ValidData(TransactionType.Expense, out var occurrence);
        if (scenario == "paid") occurrence.MarkPaid(Guid.NewGuid());
        if (scenario == "skipped") occurrence.Skip();
        if (scenario == "missing-occurrence") data.Occurrences.Clear();
        if (scenario == "missing-recurring") data.RecurringTransactions.Clear();
        await Assert.ThrowsAnyAsync<Exception>(() => UseCase(data).ExecuteAsync(occurrence.Id, Token));
        Assert.Equal(0, data.SaveCount);
        Assert.Equal(0, data.AddedTransactionCount);
    }

    [Theory]
    [InlineData("missing-account")]
    [InlineData("archived-account")]
    [InlineData("missing-category")]
    [InlineData("archived-category")]
    [InlineData("wrong-category")]
    [InlineData("currency")]
    public async Task Recurring_expense_cross_entity_failure_does_not_mutate_or_persist(string scenario)
    {
        var data = ValidData(
            TransactionType.Expense,
            out var occurrence,
            sourceCurrency: scenario == "currency" ? "USD" : "PHP",
            categoryKind: scenario == "wrong-category" ? CategoryTransactionKind.Income : CategoryTransactionKind.Expense);
        var source = data.Accounts.Values.Single(account => account.Name == "Source");
        var category = data.Categories.Values.Single();
        if (scenario == "missing-account") data.Accounts.Remove(source.Id);
        if (scenario == "archived-account") source.Archive();
        if (scenario == "missing-category") data.Categories.Clear();
        if (scenario == "archived-category") category.Archive();
        await AssertFailureWithoutMutationAsync(data, occurrence);
    }

    [Theory]
    [InlineData("missing-account")]
    [InlineData("archived-account")]
    [InlineData("missing-category")]
    [InlineData("archived-category")]
    [InlineData("wrong-category")]
    [InlineData("currency")]
    public async Task Recurring_income_cross_entity_failure_does_not_mutate_or_persist(string scenario)
    {
        var data = ValidData(
            TransactionType.Income,
            out var occurrence,
            destinationCurrency: scenario == "currency" ? "USD" : "PHP",
            categoryKind: scenario == "wrong-category" ? CategoryTransactionKind.Expense : CategoryTransactionKind.Income);
        var destination = data.Accounts.Values.Single(account => account.Name == "Destination");
        var category = data.Categories.Values.Single();
        if (scenario == "missing-account") data.Accounts.Remove(destination.Id);
        if (scenario == "archived-account") destination.Archive();
        if (scenario == "missing-category") data.Categories.Clear();
        if (scenario == "archived-category") category.Archive();
        await AssertFailureWithoutMutationAsync(data, occurrence);
    }

    [Theory]
    [InlineData("missing-source")]
    [InlineData("missing-destination")]
    [InlineData("archived-source")]
    [InlineData("archived-destination")]
    [InlineData("account-currencies")]
    [InlineData("amount-currency")]
    public async Task Recurring_transfer_cross_entity_failure_does_not_mutate_or_persist(string scenario)
    {
        var data = ValidData(
            TransactionType.Transfer,
            out var occurrence,
            destinationCurrency: scenario == "account-currencies" ? "USD" : "PHP",
            amountCurrency: scenario == "amount-currency" ? "USD" : "PHP");
        var source = data.Accounts.Values.Single(account => account.Name == "Source");
        var destination = data.Accounts.Values.Single(account => account.Name == "Destination");
        if (scenario == "missing-source") data.Accounts.Remove(source.Id);
        if (scenario == "missing-destination") data.Accounts.Remove(destination.Id);
        if (scenario == "archived-source") source.Archive();
        if (scenario == "archived-destination") destination.Archive();
        await AssertFailureWithoutMutationAsync(data, occurrence);
    }

    private static FakeData ValidData(
        TransactionType type,
        out RecurringOccurrence occurrence,
        DateOnly? dueDate = null,
        string sourceCurrency = "PHP",
        string destinationCurrency = "PHP",
        string amountCurrency = "PHP",
        CategoryTransactionKind? categoryKind = null)
    {
        var data = new FakeData();
        var source = TransactionUseCaseTests.AddAccount(data, "Source", sourceCurrency);
        var destination = TransactionUseCaseTests.AddAccount(data, "Destination", destinationCurrency);
        var kind = categoryKind ?? (type == TransactionType.Income ? CategoryTransactionKind.Income : CategoryTransactionKind.Expense);
        var category = TransactionUseCaseTests.AddCategory(data, kind);
        var recurring = type switch
        {
            TransactionType.Expense => RecurringTransaction.CreateExpense(source.Id, category.Id, new Money(100, amountCurrency), RecurrenceFrequencyUnit.Day, 1, new(2026, 9, 1)),
            TransactionType.Income => RecurringTransaction.CreateIncome(destination.Id, category.Id, new Money(100, amountCurrency), RecurrenceFrequencyUnit.Day, 1, new(2026, 9, 1)),
            _ => RecurringTransaction.CreateTransfer(source.Id, destination.Id, new Money(100, amountCurrency), RecurrenceFrequencyUnit.Day, 1, new(2026, 9, 1))
        };
        occurrence = new RecurringOccurrence(recurring.Id, dueDate ?? new DateOnly(2026, 9, 2));
        data.RecurringTransactions.Add(recurring.Id, recurring);
        data.Occurrences.Add(occurrence.Id, occurrence);
        return data;
    }

    private static async Task AssertFailureWithoutMutationAsync(FakeData data, RecurringOccurrence occurrence)
    {
        await Assert.ThrowsAnyAsync<Exception>(() => UseCase(data).ExecuteAsync(occurrence.Id, Token));
        Assert.Equal(RecurringOccurrenceStatus.Pending, occurrence.Status);
        Assert.Null(occurrence.TransactionId);
        Assert.Equal(0, data.AddedTransactionCount);
        Assert.Equal(0, data.SaveCount);
    }

    private static MarkOccurrencePaidUseCase UseCase(FakeData data) =>
        new(data, data, data, data, data, data, Clock);

    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static FakeDateProvider Clock => new(new DateOnly(2026, 9, 2));
}
