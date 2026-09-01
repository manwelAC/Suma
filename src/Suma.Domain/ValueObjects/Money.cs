namespace Suma.Domain.ValueObjects;

public sealed record Money : IComparable<Money>
{
    public Money(long amountMinor, string currencyCode)
    {
        AmountMinor = amountMinor;
        CurrencyCode = NormalizeCurrencyCode(currencyCode);
    }

    public long AmountMinor { get; }

    public string CurrencyCode { get; }

    public bool IsPositive => AmountMinor > 0;

    public bool IsNegative => AmountMinor < 0;

    public bool IsZero => AmountMinor == 0;

    public static Money Zero(string currencyCode) => new(0, currencyCode);

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(checked(left.AmountMinor + right.AmountMinor), left.CurrencyCode);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(checked(left.AmountMinor - right.AmountMinor), left.CurrencyCode);
    }

    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    public int CompareTo(Money? other)
    {
        ArgumentNullException.ThrowIfNull(other);
        EnsureSameCurrency(this, other);
        return AmountMinor.CompareTo(other.AmountMinor);
    }

    internal static string NormalizeCurrencyCode(string currencyCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);

        var normalized = currencyCode.Trim().ToUpperInvariant();
        if (normalized.Length != 3 || normalized.Any(character => character is < 'A' or > 'Z'))
        {
            throw new ArgumentException(
                "Currency code must contain exactly three letters.",
                nameof(currencyCode));
        }

        return normalized;
    }

    private static void EnsureSameCurrency(Money left, Money right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (!string.Equals(left.CurrencyCode, right.CurrencyCode, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Money values with currencies {left.CurrencyCode} and {right.CurrencyCode} cannot be combined or compared.");
        }
    }
}
