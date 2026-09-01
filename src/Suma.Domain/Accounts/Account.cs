using Suma.Domain.Common;
using Suma.Domain.ValueObjects;

namespace Suma.Domain.Accounts;

public sealed class Account : Entity
{
    public Account(
        string name,
        AccountType type,
        Money openingBalance,
        string currencyCode,
        bool includeInAvailableToSpend)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(openingBalance);

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Account type is not supported.");
        }

        var normalizedCurrencyCode = Money.NormalizeCurrencyCode(currencyCode);
        if (!string.Equals(openingBalance.CurrencyCode, normalizedCurrencyCode, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Opening balance currency must match the account currency.",
                nameof(openingBalance));
        }

        Name = name.Trim();
        Type = type;
        OpeningBalance = openingBalance;
        CurrencyCode = normalizedCurrencyCode;
        IncludeInAvailableToSpend = includeInAvailableToSpend;
    }

    public string Name { get; }

    public AccountType Type { get; }

    public Money OpeningBalance { get; }

    public string CurrencyCode { get; }

    public bool IncludeInAvailableToSpend { get; private set; }

    public bool IsArchived { get; private set; }

    public void Archive() => IsArchived = true;

    public void Restore() => IsArchived = false;

    public void SetAvailableToSpendInclusion(bool include) => IncludeInAvailableToSpend = include;
}
