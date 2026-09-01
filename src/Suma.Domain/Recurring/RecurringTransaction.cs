using Suma.Domain.Common;
using Suma.Domain.Transactions;
using Suma.Domain.ValueObjects;

namespace Suma.Domain.Recurring;

public sealed class RecurringTransaction : Entity
{
    private RecurringTransaction(
        TransactionType type,
        Guid? sourceAccountId,
        Guid? destinationAccountId,
        Guid? categoryId,
        Money amount,
        RecurrenceFrequencyUnit frequencyUnit,
        int intervalCount,
        DateOnly startDate,
        DateOnly? endDate,
        DayOfWeek? dayOfWeek,
        int? dayOfMonth,
        int? monthOfYear,
        string? description,
        string? notes)
    {
        Type = type;
        SourceAccountId = sourceAccountId;
        DestinationAccountId = destinationAccountId;
        CategoryId = categoryId;
        Amount = amount;
        FrequencyUnit = frequencyUnit;
        IntervalCount = intervalCount;
        StartDate = startDate;
        EndDate = endDate;
        DayOfWeek = dayOfWeek;
        DayOfMonth = dayOfMonth;
        MonthOfYear = monthOfYear;
        Description = NormalizeOptionalText(description);
        Notes = NormalizeOptionalText(notes);
        IsActive = true;
    }

    public TransactionType Type { get; }

    public Guid? SourceAccountId { get; }

    public Guid? DestinationAccountId { get; }

    public Guid? CategoryId { get; }

    public Money Amount { get; private set; }

    public RecurrenceFrequencyUnit FrequencyUnit { get; private set; }

    public int IntervalCount { get; private set; }

    public DayOfWeek? DayOfWeek { get; private set; }

    public int? DayOfMonth { get; private set; }

    public int? MonthOfYear { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    public string? Description { get; private set; }

    public string? Notes { get; private set; }

    public bool IsActive { get; private set; }

    public static RecurringTransaction CreateExpense(
        Guid sourceAccountId,
        Guid categoryId,
        Money amount,
        RecurrenceFrequencyUnit frequencyUnit,
        int intervalCount,
        DateOnly startDate,
        DateOnly? endDate = null,
        DayOfWeek? dayOfWeek = null,
        int? dayOfMonth = null,
        int? monthOfYear = null,
        string? description = null,
        string? notes = null)
    {
        EnsureNotEmpty(sourceAccountId, nameof(sourceAccountId));
        EnsureNotEmpty(categoryId, nameof(categoryId));

        return Create(
            TransactionType.Expense,
            sourceAccountId,
            null,
            categoryId,
            amount,
            frequencyUnit,
            intervalCount,
            startDate,
            endDate,
            dayOfWeek,
            dayOfMonth,
            monthOfYear,
            description,
            notes);
    }

    public static RecurringTransaction CreateIncome(
        Guid destinationAccountId,
        Guid categoryId,
        Money amount,
        RecurrenceFrequencyUnit frequencyUnit,
        int intervalCount,
        DateOnly startDate,
        DateOnly? endDate = null,
        DayOfWeek? dayOfWeek = null,
        int? dayOfMonth = null,
        int? monthOfYear = null,
        string? description = null,
        string? notes = null)
    {
        EnsureNotEmpty(destinationAccountId, nameof(destinationAccountId));
        EnsureNotEmpty(categoryId, nameof(categoryId));

        return Create(
            TransactionType.Income,
            null,
            destinationAccountId,
            categoryId,
            amount,
            frequencyUnit,
            intervalCount,
            startDate,
            endDate,
            dayOfWeek,
            dayOfMonth,
            monthOfYear,
            description,
            notes);
    }

    public static RecurringTransaction CreateTransfer(
        Guid sourceAccountId,
        Guid destinationAccountId,
        Money amount,
        RecurrenceFrequencyUnit frequencyUnit,
        int intervalCount,
        DateOnly startDate,
        DateOnly? endDate = null,
        DayOfWeek? dayOfWeek = null,
        int? dayOfMonth = null,
        int? monthOfYear = null,
        string? description = null,
        string? notes = null)
    {
        EnsureNotEmpty(sourceAccountId, nameof(sourceAccountId));
        EnsureNotEmpty(destinationAccountId, nameof(destinationAccountId));

        if (sourceAccountId == destinationAccountId)
        {
            throw new ArgumentException(
                "Source and destination accounts must be different.",
                nameof(destinationAccountId));
        }

        return Create(
            TransactionType.Transfer,
            sourceAccountId,
            destinationAccountId,
            null,
            amount,
            frequencyUnit,
            intervalCount,
            startDate,
            endDate,
            dayOfWeek,
            dayOfMonth,
            monthOfYear,
            description,
            notes);
    }

    public void SetAmount(Money amount)
    {
        EnsurePositiveAmount(amount);

        if (!string.Equals(Amount.CurrencyCode, amount.CurrencyCode, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Amount currency must match the recurring transaction currency.",
                nameof(amount));
        }

        Amount = amount;
    }

    public void UpdateSchedule(
        RecurrenceFrequencyUnit frequencyUnit,
        int intervalCount,
        DateOnly startDate,
        DateOnly? endDate = null,
        DayOfWeek? dayOfWeek = null,
        int? dayOfMonth = null,
        int? monthOfYear = null)
    {
        ValidateSchedule(
            frequencyUnit,
            intervalCount,
            startDate,
            endDate,
            dayOfWeek,
            dayOfMonth,
            monthOfYear);

        FrequencyUnit = frequencyUnit;
        IntervalCount = intervalCount;
        StartDate = startDate;
        EndDate = endDate;
        DayOfWeek = dayOfWeek;
        DayOfMonth = dayOfMonth;
        MonthOfYear = monthOfYear;
    }

    public void UpdateDetails(string? description, string? notes)
    {
        Description = NormalizeOptionalText(description);
        Notes = NormalizeOptionalText(notes);
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    private static RecurringTransaction Create(
        TransactionType type,
        Guid? sourceAccountId,
        Guid? destinationAccountId,
        Guid? categoryId,
        Money amount,
        RecurrenceFrequencyUnit frequencyUnit,
        int intervalCount,
        DateOnly startDate,
        DateOnly? endDate,
        DayOfWeek? dayOfWeek,
        int? dayOfMonth,
        int? monthOfYear,
        string? description,
        string? notes)
    {
        EnsurePositiveAmount(amount);
        ValidateSchedule(
            frequencyUnit,
            intervalCount,
            startDate,
            endDate,
            dayOfWeek,
            dayOfMonth,
            monthOfYear);

        return new RecurringTransaction(
            type,
            sourceAccountId,
            destinationAccountId,
            categoryId,
            amount,
            frequencyUnit,
            intervalCount,
            startDate,
            endDate,
            dayOfWeek,
            dayOfMonth,
            monthOfYear,
            description,
            notes);
    }

    private static void ValidateSchedule(
        RecurrenceFrequencyUnit frequencyUnit,
        int intervalCount,
        DateOnly startDate,
        DateOnly? endDate,
        DayOfWeek? dayOfWeek,
        int? dayOfMonth,
        int? monthOfYear)
    {
        if (!Enum.IsDefined(frequencyUnit))
        {
            throw new ArgumentOutOfRangeException(
                nameof(frequencyUnit),
                frequencyUnit,
                "Recurrence frequency unit is not supported.");
        }

        if (intervalCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(intervalCount),
                intervalCount,
                "Interval count must be greater than zero.");
        }

        if (endDate.HasValue && startDate > endDate.Value)
        {
            throw new ArgumentException(
                "Recurring transaction start date must be on or before the end date.",
                nameof(startDate));
        }

        ValidateFrequencyShape(frequencyUnit, dayOfWeek, dayOfMonth, monthOfYear);
    }

    private static void ValidateFrequencyShape(
        RecurrenceFrequencyUnit frequencyUnit,
        DayOfWeek? dayOfWeek,
        int? dayOfMonth,
        int? monthOfYear)
    {
        switch (frequencyUnit)
        {
            case RecurrenceFrequencyUnit.Day:
                EnsureNull(dayOfWeek, nameof(dayOfWeek), frequencyUnit);
                EnsureNull(dayOfMonth, nameof(dayOfMonth), frequencyUnit);
                EnsureNull(monthOfYear, nameof(monthOfYear), frequencyUnit);
                break;
            case RecurrenceFrequencyUnit.Week:
                if (!dayOfWeek.HasValue)
                {
                    throw new ArgumentException("Weekly recurrence requires a day of week.", nameof(dayOfWeek));
                }

                if (!Enum.IsDefined(dayOfWeek.Value))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(dayOfWeek),
                        dayOfWeek,
                        "Day of week is not supported.");
                }

                EnsureNull(dayOfMonth, nameof(dayOfMonth), frequencyUnit);
                EnsureNull(monthOfYear, nameof(monthOfYear), frequencyUnit);
                break;
            case RecurrenceFrequencyUnit.Month:
                EnsureNull(dayOfWeek, nameof(dayOfWeek), frequencyUnit);
                EnsureDayOfMonth(dayOfMonth);
                EnsureNull(monthOfYear, nameof(monthOfYear), frequencyUnit);
                break;
            case RecurrenceFrequencyUnit.Year:
                EnsureNull(dayOfWeek, nameof(dayOfWeek), frequencyUnit);
                EnsureDayOfMonth(dayOfMonth);
                EnsureMonthOfYear(monthOfYear);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(frequencyUnit),
                    frequencyUnit,
                    "Recurrence frequency unit is not supported.");
        }
    }

    private static void EnsureNotEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Identifier cannot be empty.", parameterName);
        }
    }

    private static void EnsurePositiveAmount(Money amount)
    {
        ArgumentNullException.ThrowIfNull(amount);

        if (!amount.IsPositive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                amount.AmountMinor,
                "Recurring transaction amount must be greater than zero.");
        }
    }

    private static void EnsureNull<T>(T? value, string parameterName, RecurrenceFrequencyUnit frequencyUnit)
        where T : struct
    {
        if (value.HasValue)
        {
            throw new ArgumentException(
                $"{parameterName} is not valid for {frequencyUnit} recurrence.",
                parameterName);
        }
    }

    private static void EnsureDayOfMonth(int? dayOfMonth)
    {
        if (dayOfMonth is < 1 or > 31)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dayOfMonth),
                dayOfMonth,
                "Day of month must be between 1 and 31.");
        }

        if (!dayOfMonth.HasValue)
        {
            throw new ArgumentException("A day of month is required.", nameof(dayOfMonth));
        }
    }

    private static void EnsureMonthOfYear(int? monthOfYear)
    {
        if (monthOfYear is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(
                nameof(monthOfYear),
                monthOfYear,
                "Month of year must be between 1 and 12.");
        }

        if (!monthOfYear.HasValue)
        {
            throw new ArgumentException("A month of year is required.", nameof(monthOfYear));
        }
    }

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
