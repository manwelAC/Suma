using Suma.Domain.Recurring;

namespace Suma.Application.Recurring;

public static class RecurringOccurrencePolicy
{
    public const int OverdueDays = 365;
    public const int UpcomingDays = 90;
}

internal static class RecurringScheduleCalculator
{
    public static IEnumerable<DateOnly> GetDueDates(RecurringTransaction recurring, DateOnly from, DateOnly through)
    {
        if (!recurring.IsActive || through < recurring.StartDate || recurring.EndDate < from) yield break;
        var due = FirstDue(recurring);
        while (due < from) due = Next(recurring, due);
        while (due <= through && (!recurring.EndDate.HasValue || due <= recurring.EndDate.Value))
        {
            yield return due;
            due = Next(recurring, due);
        }
    }

    private static DateOnly FirstDue(RecurringTransaction recurring) => recurring.FrequencyUnit switch
    {
        RecurrenceFrequencyUnit.Day => recurring.StartDate,
        RecurrenceFrequencyUnit.Week => recurring.StartDate.AddDays(((int)recurring.DayOfWeek!.Value - (int)recurring.StartDate.DayOfWeek + 7) % 7),
        RecurrenceFrequencyUnit.Month => Monthly(recurring.StartDate.Year, recurring.StartDate.Month, recurring.DayOfMonth!.Value, recurring.StartDate),
        RecurrenceFrequencyUnit.Year => Yearly(recurring.StartDate.Year, recurring.MonthOfYear!.Value, recurring.DayOfMonth!.Value, recurring.StartDate),
        _ => throw new ArgumentOutOfRangeException(nameof(recurring))
    };

    private static DateOnly Next(RecurringTransaction recurring, DateOnly current) => recurring.FrequencyUnit switch
    {
        RecurrenceFrequencyUnit.Day => current.AddDays(recurring.IntervalCount),
        RecurrenceFrequencyUnit.Week => current.AddDays(checked(recurring.IntervalCount * 7)),
        RecurrenceFrequencyUnit.Month => FromMonthIndex(current.Year * 12 + current.Month - 1 + recurring.IntervalCount, recurring.DayOfMonth!.Value),
        RecurrenceFrequencyUnit.Year => Date(recurring.MonthOfYear!.Value, recurring.DayOfMonth!.Value, checked(current.Year + recurring.IntervalCount)),
        _ => throw new ArgumentOutOfRangeException(nameof(recurring))
    };

    private static DateOnly Monthly(int year, int month, int day, DateOnly start)
    {
        var date = Date(month, day, year);
        return date >= start ? date : FromMonthIndex(year * 12 + month, day);
    }

    private static DateOnly Yearly(int year, int month, int day, DateOnly start)
    {
        var date = Date(month, day, year);
        return date >= start ? date : Date(month, day, year + 1);
    }

    private static DateOnly FromMonthIndex(int monthIndex, int day)
    {
        var year = Math.DivRem(monthIndex, 12, out var zeroBasedMonth);
        return Date(zeroBasedMonth + 1, day, year);
    }

    private static DateOnly Date(int month, int day, int year) => new(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));
}
