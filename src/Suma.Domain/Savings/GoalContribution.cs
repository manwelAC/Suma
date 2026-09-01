using Suma.Domain.Common;
using Suma.Domain.ValueObjects;

namespace Suma.Domain.Savings;

public sealed class GoalContribution : Entity
{
    public GoalContribution(
        Guid savingsGoalId,
        Guid transactionId,
        GoalContributionType type,
        Money amount)
    {
        EnsureNotEmpty(savingsGoalId, nameof(savingsGoalId));
        EnsureNotEmpty(transactionId, nameof(transactionId));

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(
                nameof(type),
                type,
                "Goal contribution type is not supported.");
        }

        ArgumentNullException.ThrowIfNull(amount);
        if (!amount.IsPositive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                amount.AmountMinor,
                "Goal contribution amount must be greater than zero.");
        }

        SavingsGoalId = savingsGoalId;
        TransactionId = transactionId;
        Type = type;
        Amount = amount;
    }

    public Guid SavingsGoalId { get; }

    public Guid TransactionId { get; }

    public GoalContributionType Type { get; }

    public Money Amount { get; }

    private static void EnsureNotEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Identifier cannot be empty.", parameterName);
        }
    }
}
