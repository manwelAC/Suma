using Suma.Domain.Common;
using Suma.Domain.ValueObjects;

namespace Suma.Domain.Budgets;

public sealed class BudgetAllocation : Entity
{
    public BudgetAllocation(
        Guid budgetId,
        Guid categoryId,
        Money amount,
        bool reserveFromAvailable)
    {
        EnsureNotEmpty(budgetId, nameof(budgetId));
        EnsureNotEmpty(categoryId, nameof(categoryId));
        EnsurePositiveAmount(amount);

        BudgetId = budgetId;
        CategoryId = categoryId;
        Amount = amount;
        ReserveFromAvailable = reserveFromAvailable;
    }

    public Guid BudgetId { get; }

    public Guid CategoryId { get; }

    public Money Amount { get; private set; }

    public bool ReserveFromAvailable { get; private set; }

    public string CurrencyCode => Amount.CurrencyCode;

    public void SetAmount(Money amount)
    {
        EnsurePositiveAmount(amount);

        if (!string.Equals(CurrencyCode, amount.CurrencyCode, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Allocation amount currency must match the existing currency.",
                nameof(amount));
        }

        Amount = amount;
    }

    public void SetReserveFromAvailable(bool reserve) => ReserveFromAvailable = reserve;

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
                "Allocation amount must be greater than zero.");
        }
    }
}
