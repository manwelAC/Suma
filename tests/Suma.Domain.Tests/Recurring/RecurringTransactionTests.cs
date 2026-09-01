using Suma.Domain.Recurring;
using Suma.Domain.Transactions;
using Suma.Domain.ValueObjects;
using Xunit;

namespace Suma.Domain.Tests.Recurring;

public sealed class RecurringTransactionTests
{
    private static readonly DateOnly StartDate = new(2026, 9, 1);
    private static readonly DateOnly EndDate = new(2027, 9, 1);

    [Fact]
    public void CreateExpense_WithValidValues_CreatesActiveExpensePlan()
    {
        var sourceAccountId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var amount = PositiveAmount();

        var recurring = RecurringTransaction.CreateExpense(
            sourceAccountId,
            categoryId,
            amount,
            RecurrenceFrequencyUnit.Day,
            1,
            StartDate,
            EndDate,
            description: "  Netflix  ",
            notes: "  Family plan  ");

        Assert.NotEqual(Guid.Empty, recurring.Id);
        Assert.Equal(TransactionType.Expense, recurring.Type);
        Assert.Equal(sourceAccountId, recurring.SourceAccountId);
        Assert.Null(recurring.DestinationAccountId);
        Assert.Equal(categoryId, recurring.CategoryId);
        Assert.Same(amount, recurring.Amount);
        Assert.Equal(RecurrenceFrequencyUnit.Day, recurring.FrequencyUnit);
        Assert.Equal(1, recurring.IntervalCount);
        Assert.Equal(StartDate, recurring.StartDate);
        Assert.Equal(EndDate, recurring.EndDate);
        Assert.Null(recurring.DayOfWeek);
        Assert.Null(recurring.DayOfMonth);
        Assert.Null(recurring.MonthOfYear);
        Assert.Equal("Netflix", recurring.Description);
        Assert.Equal("Family plan", recurring.Notes);
        Assert.True(recurring.IsActive);
    }

    [Fact]
    public void CreateIncome_WithValidValues_CreatesIncomePlan()
    {
        var destinationAccountId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var recurring = RecurringTransaction.CreateIncome(
            destinationAccountId,
            categoryId,
            PositiveAmount(),
            RecurrenceFrequencyUnit.Week,
            2,
            StartDate,
            dayOfWeek: DayOfWeek.Monday);

        Assert.Equal(TransactionType.Income, recurring.Type);
        Assert.Null(recurring.SourceAccountId);
        Assert.Equal(destinationAccountId, recurring.DestinationAccountId);
        Assert.Equal(categoryId, recurring.CategoryId);
        Assert.Equal(DayOfWeek.Monday, recurring.DayOfWeek);
    }

    [Fact]
    public void CreateTransfer_WithValidValues_CreatesTransferPlan()
    {
        var sourceAccountId = Guid.NewGuid();
        var destinationAccountId = Guid.NewGuid();

        var recurring = RecurringTransaction.CreateTransfer(
            sourceAccountId,
            destinationAccountId,
            PositiveAmount(),
            RecurrenceFrequencyUnit.Year,
            1,
            StartDate,
            dayOfMonth: 31,
            monthOfYear: 12);

        Assert.Equal(TransactionType.Transfer, recurring.Type);
        Assert.Equal(sourceAccountId, recurring.SourceAccountId);
        Assert.Equal(destinationAccountId, recurring.DestinationAccountId);
        Assert.Null(recurring.CategoryId);
        Assert.Equal(31, recurring.DayOfMonth);
        Assert.Equal(12, recurring.MonthOfYear);
    }

    [Theory]
    [InlineData("expense-source")]
    [InlineData("expense-category")]
    [InlineData("income-destination")]
    [InlineData("income-category")]
    [InlineData("transfer-source")]
    [InlineData("transfer-destination")]
    public void Create_WithEmptyRequiredIdentifier_IsRejected(string invalidIdentifier)
    {
        Assert.Throws<ArgumentException>(() => CreateWithInvalidIdentifier(invalidIdentifier));
    }

    [Fact]
    public void CreateTransfer_WithSameSourceAndDestination_IsRejected()
    {
        var accountId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(
            () => RecurringTransaction.CreateTransfer(
                accountId,
                accountId,
                PositiveAmount(),
                RecurrenceFrequencyUnit.Day,
                1,
                StartDate));
    }

    [Theory]
    [InlineData(TransactionType.Expense)]
    [InlineData(TransactionType.Income)]
    [InlineData(TransactionType.Transfer)]
    public void Create_WithNullAmount_IsRejected(TransactionType type)
    {
        Assert.Throws<ArgumentNullException>(() => CreateByType(type, null!));
    }

    [Theory]
    [InlineData(TransactionType.Expense, 0)]
    [InlineData(TransactionType.Expense, -1)]
    [InlineData(TransactionType.Income, 0)]
    [InlineData(TransactionType.Income, -1)]
    [InlineData(TransactionType.Transfer, 0)]
    [InlineData(TransactionType.Transfer, -1)]
    public void Create_WithNonPositiveAmount_IsRejected(TransactionType type, long amountMinor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateByType(type, new Money(amountMinor, "PHP")));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithNonPositiveIntervalCount_IsRejected(int intervalCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RecurringTransaction.CreateExpense(
                Guid.NewGuid(),
                Guid.NewGuid(),
                PositiveAmount(),
                RecurrenceFrequencyUnit.Day,
                intervalCount,
                StartDate));
    }

    [Fact]
    public void Create_WithUndefinedFrequencyUnit_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RecurringTransaction.CreateExpense(
                Guid.NewGuid(),
                Guid.NewGuid(),
                PositiveAmount(),
                (RecurrenceFrequencyUnit)999,
                1,
                StartDate));
    }

    [Theory]
    [InlineData("day-of-week")]
    [InlineData("day-of-month")]
    [InlineData("month-of-year")]
    public void DayFrequency_WithSelector_IsRejected(string selector)
    {
        Assert.Throws<ArgumentException>(() => CreateDailyWithSelector(selector));
    }

    [Fact]
    public void WeekFrequency_WithoutDayOfWeek_IsRejected()
    {
        Assert.Throws<ArgumentException>(
            () => CreateExpense(RecurrenceFrequencyUnit.Week));
    }

    [Theory]
    [InlineData("day-of-month")]
    [InlineData("month-of-year")]
    public void WeekFrequency_WithUnsupportedSelector_IsRejected(string selector)
    {
        int? dayOfMonth = selector == "day-of-month" ? 1 : null;
        int? monthOfYear = selector == "month-of-year" ? 1 : null;

        Assert.Throws<ArgumentException>(
            () => CreateExpense(
                RecurrenceFrequencyUnit.Week,
                dayOfWeek: DayOfWeek.Monday,
                dayOfMonth: dayOfMonth,
                monthOfYear: monthOfYear));
    }

    [Fact]
    public void MonthFrequency_WithoutDayOfMonth_IsRejected()
    {
        Assert.Throws<ArgumentException>(
            () => CreateExpense(RecurrenceFrequencyUnit.Month));
    }

    [Fact]
    public void MonthFrequency_WithDayOfMonth_IsAllowed()
    {
        var recurring = CreateExpense(RecurrenceFrequencyUnit.Month, dayOfMonth: 31);

        Assert.Equal(31, recurring.DayOfMonth);
        Assert.Null(recurring.DayOfWeek);
        Assert.Null(recurring.MonthOfYear);
    }

    [Theory]
    [InlineData("day-of-week")]
    [InlineData("month-of-year")]
    public void MonthFrequency_WithUnsupportedSelector_IsRejected(string selector)
    {
        DayOfWeek? dayOfWeek = selector == "day-of-week" ? DayOfWeek.Monday : null;
        int? monthOfYear = selector == "month-of-year" ? 1 : null;

        Assert.Throws<ArgumentException>(
            () => CreateExpense(
                RecurrenceFrequencyUnit.Month,
                dayOfWeek: dayOfWeek,
                dayOfMonth: 15,
                monthOfYear: monthOfYear));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void YearFrequency_WithoutRequiredSelector_IsRejected(
        bool missingDayOfMonth,
        bool missingMonthOfYear)
    {
        Assert.Throws<ArgumentException>(
            () => CreateExpense(
                RecurrenceFrequencyUnit.Year,
                dayOfMonth: missingDayOfMonth ? null : 1,
                monthOfYear: missingMonthOfYear ? null : 1));
    }

    [Fact]
    public void YearFrequency_WithDayOfWeek_IsRejected()
    {
        Assert.Throws<ArgumentException>(
            () => CreateExpense(
                RecurrenceFrequencyUnit.Year,
                dayOfWeek: DayOfWeek.Monday,
                dayOfMonth: 1,
                monthOfYear: 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    public void Create_WithDayOfMonthOutsideRange_IsRejected(int dayOfMonth)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateExpense(RecurrenceFrequencyUnit.Month, dayOfMonth: dayOfMonth));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Create_WithMonthOfYearOutsideRange_IsRejected(int monthOfYear)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateExpense(
                RecurrenceFrequencyUnit.Year,
                dayOfMonth: 1,
                monthOfYear: monthOfYear));
    }

    [Fact]
    public void Create_WithNullEndDate_IsAllowed()
    {
        var recurring = CreateExpense(RecurrenceFrequencyUnit.Day);

        Assert.Null(recurring.EndDate);
    }

    [Fact]
    public void Create_WithStartDateEqualToEndDate_IsAllowed()
    {
        var recurring = RecurringTransaction.CreateExpense(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PositiveAmount(),
            RecurrenceFrequencyUnit.Day,
            1,
            StartDate,
            StartDate);

        Assert.Equal(StartDate, recurring.StartDate);
        Assert.Equal(StartDate, recurring.EndDate);
    }

    [Fact]
    public void Create_WithStartDateAfterEndDate_IsRejected()
    {
        Assert.Throws<ArgumentException>(
            () => RecurringTransaction.CreateExpense(
                Guid.NewGuid(),
                Guid.NewGuid(),
                PositiveAmount(),
                RecurrenceFrequencyUnit.Day,
                1,
                EndDate,
                StartDate));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  Netflix  ", "Netflix")]
    public void Create_NormalizesOptionalText(string? supplied, string? expected)
    {
        var recurring = RecurringTransaction.CreateExpense(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PositiveAmount(),
            RecurrenceFrequencyUnit.Day,
            1,
            StartDate,
            description: supplied,
            notes: supplied);

        Assert.Equal(expected, recurring.Description);
        Assert.Equal(expected, recurring.Notes);
    }

    [Fact]
    public void SetAmount_WithSameCurrencyPositiveAmount_UpdatesAmount()
    {
        var recurring = CreateExpense(RecurrenceFrequencyUnit.Day);
        var newAmount = new Money(75_000, "PHP");

        recurring.SetAmount(newAmount);

        Assert.Same(newAmount, recurring.Amount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SetAmount_WithNonPositiveAmount_IsRejectedWithoutMutation(long amountMinor)
    {
        var recurring = CreateExpense(RecurrenceFrequencyUnit.Day);
        var originalAmount = recurring.Amount;

        Assert.Throws<ArgumentOutOfRangeException>(
            () => recurring.SetAmount(new Money(amountMinor, "PHP")));
        Assert.Same(originalAmount, recurring.Amount);
    }

    [Fact]
    public void SetAmount_WithNull_IsRejectedWithoutMutation()
    {
        var recurring = CreateExpense(RecurrenceFrequencyUnit.Day);
        var originalAmount = recurring.Amount;

        Assert.Throws<ArgumentNullException>(() => recurring.SetAmount(null!));
        Assert.Same(originalAmount, recurring.Amount);
    }

    [Fact]
    public void SetAmount_WithDifferentCurrency_IsRejectedWithoutMutation()
    {
        var recurring = CreateExpense(RecurrenceFrequencyUnit.Day);
        var originalAmount = recurring.Amount;

        Assert.Throws<ArgumentException>(() => recurring.SetAmount(new Money(75_000, "USD")));
        Assert.Same(originalAmount, recurring.Amount);
    }

    [Fact]
    public void UpdateSchedule_WithValidSchedule_UpdatesAllScheduleState()
    {
        var recurring = CreateExpense(RecurrenceFrequencyUnit.Day);
        var newStart = new DateOnly(2027, 1, 1);
        var newEnd = new DateOnly(2028, 1, 1);

        recurring.UpdateSchedule(
            RecurrenceFrequencyUnit.Year,
            2,
            newStart,
            newEnd,
            dayOfMonth: 31,
            monthOfYear: 12);

        Assert.Equal(RecurrenceFrequencyUnit.Year, recurring.FrequencyUnit);
        Assert.Equal(2, recurring.IntervalCount);
        Assert.Equal(newStart, recurring.StartDate);
        Assert.Equal(newEnd, recurring.EndDate);
        Assert.Null(recurring.DayOfWeek);
        Assert.Equal(31, recurring.DayOfMonth);
        Assert.Equal(12, recurring.MonthOfYear);
    }

    [Fact]
    public void UpdateSchedule_WithInvalidSchedule_IsRejectedWithoutMutation()
    {
        var recurring = RecurringTransaction.CreateExpense(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PositiveAmount(),
            RecurrenceFrequencyUnit.Week,
            2,
            StartDate,
            EndDate,
            dayOfWeek: DayOfWeek.Friday);

        Assert.Throws<ArgumentException>(
            () => recurring.UpdateSchedule(
                RecurrenceFrequencyUnit.Year,
                1,
                EndDate,
                StartDate,
                dayOfMonth: 1,
                monthOfYear: 1));
        Assert.Equal(RecurrenceFrequencyUnit.Week, recurring.FrequencyUnit);
        Assert.Equal(2, recurring.IntervalCount);
        Assert.Equal(StartDate, recurring.StartDate);
        Assert.Equal(EndDate, recurring.EndDate);
        Assert.Equal(DayOfWeek.Friday, recurring.DayOfWeek);
        Assert.Null(recurring.DayOfMonth);
        Assert.Null(recurring.MonthOfYear);
    }

    [Fact]
    public void UpdateDetails_NormalizesDescriptionAndNotes()
    {
        var recurring = CreateExpense(RecurrenceFrequencyUnit.Day);

        recurring.UpdateDetails("  Netflix  ", "   ");

        Assert.Equal("Netflix", recurring.Description);
        Assert.Null(recurring.Notes);
    }

    [Fact]
    public void UpdateDetails_WithNullValues_ClearsDescriptionAndNotes()
    {
        var recurring = RecurringTransaction.CreateExpense(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PositiveAmount(),
            RecurrenceFrequencyUnit.Day,
            1,
            StartDate,
            description: "Netflix",
            notes: "Family plan");

        recurring.UpdateDetails(null, null);

        Assert.Null(recurring.Description);
        Assert.Null(recurring.Notes);
    }

    [Fact]
    public void DeactivateAndReactivate_UpdatesActiveState()
    {
        var recurring = CreateExpense(RecurrenceFrequencyUnit.Day);

        Assert.True(recurring.IsActive);
        recurring.Deactivate();
        Assert.False(recurring.IsActive);
        recurring.Reactivate();
        Assert.True(recurring.IsActive);
    }

    private static RecurringTransaction CreateExpense(
        RecurrenceFrequencyUnit frequencyUnit,
        DayOfWeek? dayOfWeek = null,
        int? dayOfMonth = null,
        int? monthOfYear = null) =>
        RecurringTransaction.CreateExpense(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PositiveAmount(),
            frequencyUnit,
            1,
            StartDate,
            dayOfWeek: dayOfWeek,
            dayOfMonth: dayOfMonth,
            monthOfYear: monthOfYear);

    private static void CreateByType(TransactionType type, Money amount)
    {
        switch (type)
        {
            case TransactionType.Expense:
                RecurringTransaction.CreateExpense(
                    Guid.NewGuid(), Guid.NewGuid(), amount, RecurrenceFrequencyUnit.Day, 1, StartDate);
                break;
            case TransactionType.Income:
                RecurringTransaction.CreateIncome(
                    Guid.NewGuid(), Guid.NewGuid(), amount, RecurrenceFrequencyUnit.Day, 1, StartDate);
                break;
            case TransactionType.Transfer:
                RecurringTransaction.CreateTransfer(
                    Guid.NewGuid(), Guid.NewGuid(), amount, RecurrenceFrequencyUnit.Day, 1, StartDate);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    private static void CreateWithInvalidIdentifier(string invalidIdentifier)
    {
        var firstId = invalidIdentifier.EndsWith("source", StringComparison.Ordinal) ||
            invalidIdentifier.EndsWith("destination", StringComparison.Ordinal)
                ? Guid.Empty
                : Guid.NewGuid();
        var secondId = invalidIdentifier.EndsWith("category", StringComparison.Ordinal) ||
            invalidIdentifier == "transfer-destination"
                ? Guid.Empty
                : Guid.NewGuid();

        if (invalidIdentifier.StartsWith("expense", StringComparison.Ordinal))
        {
            RecurringTransaction.CreateExpense(
                firstId, secondId, PositiveAmount(), RecurrenceFrequencyUnit.Day, 1, StartDate);
        }
        else if (invalidIdentifier.StartsWith("income", StringComparison.Ordinal))
        {
            RecurringTransaction.CreateIncome(
                firstId, secondId, PositiveAmount(), RecurrenceFrequencyUnit.Day, 1, StartDate);
        }
        else
        {
            RecurringTransaction.CreateTransfer(
                firstId, secondId, PositiveAmount(), RecurrenceFrequencyUnit.Day, 1, StartDate);
        }
    }

    private static void CreateDailyWithSelector(string selector)
    {
        RecurringTransaction.CreateExpense(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PositiveAmount(),
            RecurrenceFrequencyUnit.Day,
            1,
            StartDate,
            dayOfWeek: selector == "day-of-week" ? DayOfWeek.Monday : null,
            dayOfMonth: selector == "day-of-month" ? 1 : null,
            monthOfYear: selector == "month-of-year" ? 1 : null);
    }

    private static Money PositiveAmount() => new(50_000, "PHP");
}
