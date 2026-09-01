using Suma.Domain.Common;
using Suma.Domain.ValueObjects;

namespace Suma.Domain.Savings;

public sealed class SavingsGoal : Entity
{
    private SavingsGoal()
    {
        Name = null!;
        TargetAmount = null!;
    }

    public SavingsGoal(
        string name,
        Money targetAmount,
        DateOnly? targetDate = null,
        Guid? destinationAccountId = null)
    {
        Name = NormalizeName(name);
        EnsurePositiveAmount(targetAmount);
        EnsureOptionalIdentifier(destinationAccountId, nameof(destinationAccountId));

        TargetAmount = targetAmount;
        TargetDate = targetDate;
        DestinationAccountId = destinationAccountId;
    }

    public string Name { get; private set; }

    public Money TargetAmount { get; private set; }

    public string CurrencyCode => TargetAmount.CurrencyCode;

    public DateOnly? TargetDate { get; private set; }

    public Guid? DestinationAccountId { get; private set; }

    public bool IsArchived { get; private set; }

    public void Rename(string name) => Name = NormalizeName(name);

    public void SetTargetAmount(Money amount)
    {
        EnsurePositiveAmount(amount);

        if (!string.Equals(CurrencyCode, amount.CurrencyCode, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Target amount currency must match the Savings Goal currency.",
                nameof(amount));
        }

        TargetAmount = amount;
    }

    public void SetTargetDate(DateOnly? targetDate) => TargetDate = targetDate;

    public void SetDestinationAccount(Guid? accountId)
    {
        EnsureOptionalIdentifier(accountId, nameof(accountId));
        DestinationAccountId = accountId;
    }

    public void Archive() => IsArchived = true;

    public void Restore() => IsArchived = false;

    private static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return name.Trim();
    }

    private static void EnsurePositiveAmount(Money amount)
    {
        ArgumentNullException.ThrowIfNull(amount);

        if (!amount.IsPositive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                amount.AmountMinor,
                "Savings Goal target amount must be greater than zero.");
        }
    }

    private static void EnsureOptionalIdentifier(Guid? value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Identifier cannot be empty.", parameterName);
        }
    }
}
