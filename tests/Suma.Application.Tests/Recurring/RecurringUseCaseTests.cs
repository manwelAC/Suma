using Suma.Application.Recurring.CreateRecurringTransaction;
using Suma.Application.Recurring.EnsureRecurringOccurrences;
using Suma.Application.Recurring.GetRecurringOverview;
using Suma.Application.Recurring.SkipOccurrence;
using Suma.Application.Tests.TestDoubles;
using Suma.Application.Tests.Transactions;
using Suma.Domain.Categories;
using Suma.Domain.Recurring;
using Suma.Domain.Transactions;
using Xunit;

namespace Suma.Application.Tests.Recurring;

public sealed class RecurringUseCaseTests
{
    [Theory]
    [InlineData(TransactionType.Expense)]
    [InlineData(TransactionType.Income)]
    [InlineData(TransactionType.Transfer)]
    public async Task Create_persists_supported_schedule_with_account_currency(TransactionType type)
    {
        var data = Data(out var source, out var destination, out var expense, out var income);
        var useCase = new CreateRecurringTransactionUseCase(data, data, data, data);
        var schedule = Schedule();
        var result = type switch
        {
            TransactionType.Expense => await useCase.ExecuteExpenseAsync(new(source.Id, expense.Id, schedule), Token),
            TransactionType.Income => await useCase.ExecuteIncomeAsync(new(destination.Id, income.Id, schedule), Token),
            TransactionType.Transfer => await useCase.ExecuteTransferAsync(new(source.Id, destination.Id, schedule), Token),
            _ => throw new InvalidOperationException()
        };
        var recurring = data.RecurringTransactions[result.Id];
        Assert.Equal(type, recurring.Type);
        Assert.Equal("PHP", recurring.Amount.CurrencyCode);
        Assert.Equal(1, data.AddedRecurringTransactionCount);
        Assert.Equal(1, data.SaveCount);
        Assert.Empty(data.Transactions);
    }

    [Fact]
    public async Task Invalid_transfer_does_not_save()
    {
        var data = Data(out var source, out _, out _, out _);
        var useCase = new CreateRecurringTransactionUseCase(data, data, data, data);
        await Assert.ThrowsAnyAsync<Exception>(() => useCase.ExecuteTransferAsync(new(source.Id, source.Id, Schedule()), Token));
        Assert.Equal(0, data.SaveCount);
        Assert.Empty(data.RecurringTransactions);
    }

    [Fact]
    public async Task Ensure_is_idempotent_bounded_and_clamps_month_end()
    {
        var data = Data(out var source, out _, out var expense, out _);
        var recurring = RecurringTransaction.CreateExpense(source.Id, expense.Id, new(100, "PHP"), RecurrenceFrequencyUnit.Month, 1, new(2026, 1, 31), dayOfMonth: 31);
        data.RecurringTransactions.Add(recurring.Id, recurring);
        var clock = new FakeDateProvider(new(2026, 2, 15));
        var useCase = new EnsureRecurringOccurrencesUseCase(data, data, data, clock);
        var first = await useCase.ExecuteAsync(Token);
        var second = await useCase.ExecuteAsync(Token);
        Assert.True(first > 0);
        Assert.Equal(0, second);
        Assert.Contains(data.Occurrences.Values, item => item.DueDate == new DateOnly(2026, 2, 28));
        Assert.All(data.Occurrences.Values, item => Assert.InRange(item.DueDate, clock.Today.AddDays(-365), clock.Today.AddDays(90)));
    }

    [Fact]
    public async Task Yearly_February_29_clamps_then_returns_on_leap_year()
    {
        var data = Data(out var source, out _, out var expense, out _);
        var recurring = RecurringTransaction.CreateExpense(source.Id, expense.Id, new(100, "PHP"), RecurrenceFrequencyUnit.Year, 1, new(2024, 2, 29), dayOfMonth: 29, monthOfYear: 2);
        data.RecurringTransactions.Add(recurring.Id, recurring);
        _ = await new EnsureRecurringOccurrencesUseCase(data, data, data, new FakeDateProvider(new(2027, 2, 20))).ExecuteAsync(Token);
        _ = await new EnsureRecurringOccurrencesUseCase(data, data, data, new FakeDateProvider(new(2028, 2, 20))).ExecuteAsync(Token);
        Assert.Contains(data.Occurrences.Values, item => item.DueDate == new DateOnly(2027, 2, 28));
        Assert.Contains(data.Occurrences.Values, item => item.DueDate == new DateOnly(2028, 2, 29));
    }

    [Fact]
    public async Task Overview_resolves_states_and_skip_persists_without_transaction()
    {
        var data = Data(out var source, out _, out var expense, out _);
        var recurring = RecurringTransaction.CreateExpense(source.Id, expense.Id, new(100, "PHP"), RecurrenceFrequencyUnit.Day, 10, new(2026, 9, 1), description: "Internet");
        var occurrence = new RecurringOccurrence(recurring.Id, new(2026, 9, 1));
        data.RecurringTransactions.Add(recurring.Id, recurring);
        data.Occurrences.Add(occurrence.Id, occurrence);
        var clock = new FakeDateProvider(new(2026, 9, 2));
        var ensure = new EnsureRecurringOccurrencesUseCase(data, data, data, clock);
        var overview = await new GetRecurringOverviewUseCase(ensure, data, data, clock).ExecuteAsync(Token);
        Assert.Contains(overview.Schedules, item => item.Description == "Internet" && item.SourceAccountName == source.Name);
        Assert.Contains(overview.Occurrences, item => item.Status == RecurringOccurrenceStatus.Pending);
        await new SkipOccurrenceUseCase(data, data).ExecuteAsync(occurrence.Id, Token);
        Assert.Equal(RecurringOccurrenceStatus.Skipped, occurrence.Status);
        Assert.Empty(data.Transactions);
    }

    private static FakeData Data(out Suma.Domain.Accounts.Account source, out Suma.Domain.Accounts.Account destination, out Category expense, out Category income)
    {
        var data = new FakeData();
        source = TransactionUseCaseTests.AddAccount(data, "Wallet", "PHP");
        destination = TransactionUseCaseTests.AddAccount(data, "Savings", "PHP");
        expense = TransactionUseCaseTests.AddCategory(data, CategoryTransactionKind.Expense);
        income = TransactionUseCaseTests.AddCategory(data, CategoryTransactionKind.Income);
        return data;
    }

    private static RecurringScheduleInput Schedule() => new(100, RecurrenceFrequencyUnit.Month, 1, new(2026, 9, 1), null, null, 1, null, "Schedule", null);
    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
