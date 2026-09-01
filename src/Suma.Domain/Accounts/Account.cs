using Suma.Domain.Common;
using Suma.Domain.ValueObjects;

namespace Suma.Domain.Accounts;

public sealed class Account : Entity
{
    private Account()
    {
        Name = null!;
        OpeningBalance = null!;
    }

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
        IncludeInAvailableToSpend = includeInAvailableToSpend;
    }

    public string Name { get; private set; }

    public AccountType Type { get; private set; }

    public Money OpeningBalance { get; private set; }

    public string CurrencyCode => OpeningBalance.CurrencyCode;

    public bool IncludeInAvailableToSpend { get; private set; }

    public bool IsArchived { get; private set; }

    public void Archive() => IsArchived = true;

    public void Restore() => IsArchived = false;

    public void SetAvailableToSpendInclusion(bool include) => IncludeInAvailableToSpend = include;
}
