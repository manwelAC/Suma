using Suma.Domain.Savings;
using Suma.Domain.ValueObjects;
using Xunit;

namespace Suma.Domain.Tests.Savings;

public sealed class GoalContributionTests
{
    [Theory]
    [InlineData(GoalContributionType.Deposit)]
    [InlineData(GoalContributionType.Withdrawal)]
    public void Create_WithValidValues_PreservesLedgerAttribution(GoalContributionType type)
    {
        var savingsGoalId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var amount = new Money(100_000, "PHP");

        var contribution = new GoalContribution(savingsGoalId, transactionId, type, amount);

        Assert.NotEqual(Guid.Empty, contribution.Id);
        Assert.Equal(savingsGoalId, contribution.SavingsGoalId);
        Assert.Equal(transactionId, contribution.TransactionId);
        Assert.Equal(type, contribution.Type);
        Assert.Same(amount, contribution.Amount);
        Assert.Equal(100_000, contribution.Amount.AmountMinor);
        Assert.Equal("PHP", contribution.Amount.CurrencyCode);
        Assert.True(contribution.Amount.IsPositive);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Create_WithEmptyRequiredId_IsRejected(bool emptyGoal, bool emptyTransaction)
    {
        var goalId = emptyGoal ? Guid.Empty : Guid.NewGuid();
        var transactionId = emptyTransaction ? Guid.Empty : Guid.NewGuid();

        Assert.Throws<ArgumentException>(
            () => new GoalContribution(
                goalId,
                transactionId,
                GoalContributionType.Deposit,
                PositiveAmount()));
    }

    [Fact]
    public void Create_WithUndefinedType_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GoalContribution(
                Guid.NewGuid(),
                Guid.NewGuid(),
                (GoalContributionType)999,
                PositiveAmount()));
    }

    [Fact]
    public void Create_WithNullAmount_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => new GoalContribution(
                Guid.NewGuid(),
                Guid.NewGuid(),
                GoalContributionType.Deposit,
                null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithNonPositiveAmount_IsRejected(long amountMinor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GoalContribution(
                Guid.NewGuid(),
                Guid.NewGuid(),
                GoalContributionType.Deposit,
                new Money(amountMinor, "PHP")));
    }

    private static Money PositiveAmount() => new(100_000, "PHP");
}
