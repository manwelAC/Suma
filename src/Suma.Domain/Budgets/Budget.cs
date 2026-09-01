using Suma.Domain.Common;
using Suma.Domain.ValueObjects;

namespace Suma.Domain.Budgets;

public sealed class Budget : Entity
{
    private Budget()
    {
        Name = null!;
        ExpectedIncome = null!;
    }

    public Budget(
        string name,
        DateOnly periodStart,
        DateOnly periodEnd,
        Money expectedIncome)
    {
        Name = NormalizeName(name);
        EnsureValidPeriod(periodStart, periodEnd);
        EnsureNonNegativeExpectedIncome(expectedIncome);

        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        ExpectedIncome = expectedIncome;
    }

    public string Name { get; private set; }

    public DateOnly PeriodStart { get; private set; }

    public DateOnly PeriodEnd { get; private set; }

    public Money ExpectedIncome { get; private set; }

    public string CurrencyCode => ExpectedIncome.CurrencyCode;

    public bool IsArchived { get; private set; }

    public void Rename(string name) => Name = NormalizeName(name);

    public void UpdatePeriod(DateOnly periodStart, DateOnly periodEnd)
    {
        EnsureValidPeriod(periodStart, periodEnd);

        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
    }

    public void SetExpectedIncome(Money expectedIncome)
    {
        EnsureNonNegativeExpectedIncome(expectedIncome);

        if (!string.Equals(CurrencyCode, expectedIncome.CurrencyCode, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Expected income currency must match the Budget currency.",
                nameof(expectedIncome));
        }

        ExpectedIncome = expectedIncome;
    }

    public void Archive() => IsArchived = true;

    public void Restore() => IsArchived = false;

    private static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return name.Trim();
    }

    private static void EnsureValidPeriod(DateOnly periodStart, DateOnly periodEnd)
    {
        if (periodStart > periodEnd)
        {
            throw new ArgumentException(
                "Budget period start must be on or before the period end.",
                nameof(periodStart));
        }
    }

    private static void EnsureNonNegativeExpectedIncome(Money expectedIncome)
    {
        ArgumentNullException.ThrowIfNull(expectedIncome);

        if (expectedIncome.IsNegative)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedIncome),
                expectedIncome.AmountMinor,
                "Expected income cannot be negative.");
        }
    }
}
