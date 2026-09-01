using Suma.Domain.Savings;
using Suma.Domain.ValueObjects;
using Xunit;

namespace Suma.Domain.Tests.Savings;

public sealed class SavingsGoalTests
{
    private static readonly DateOnly TargetDate = new(2027, 12, 31);

    [Fact]
    public void Create_WithValidValues_CreatesActiveGoal()
    {
        var targetAmount = new Money(10_000_000, "PHP");
        var destinationAccountId = Guid.NewGuid();

        var goal = new SavingsGoal(
            "  Emergency Fund  ",
            targetAmount,
            TargetDate,
            destinationAccountId);

        Assert.NotEqual(Guid.Empty, goal.Id);
        Assert.Equal("Emergency Fund", goal.Name);
        Assert.Same(targetAmount, goal.TargetAmount);
        Assert.Equal("PHP", goal.CurrencyCode);
        Assert.Equal(TargetDate, goal.TargetDate);
        Assert.Equal(destinationAccountId, goal.DestinationAccountId);
        Assert.False(goal.IsArchived);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyName_IsRejected(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => new SavingsGoal(name!, PositiveAmount()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithNonPositiveTargetAmount_IsRejected(long amountMinor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SavingsGoal("Goal", new Money(amountMinor, "PHP")));
    }

    [Fact]
    public void Create_WithNullTargetAmount_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new SavingsGoal("Goal", null!));
    }

    [Fact]
    public void Create_WithNullDestinationAccount_IsAllowed()
    {
        var goal = new SavingsGoal("Goal", PositiveAmount(), destinationAccountId: null);

        Assert.Null(goal.DestinationAccountId);
    }

    [Fact]
    public void Create_WithEmptyDestinationAccount_IsRejected()
    {
        Assert.Throws<ArgumentException>(
            () => new SavingsGoal("Goal", PositiveAmount(), destinationAccountId: Guid.Empty));
    }

    [Fact]
    public void Rename_WithValidName_TrimsAndUpdatesName()
    {
        var goal = CreateGoal();

        goal.Rename("  Travel Fund  ");

        Assert.Equal("Travel Fund", goal.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_WithEmptyName_IsRejectedWithoutMutation(string? name)
    {
        var goal = CreateGoal();
        var originalName = goal.Name;

        Assert.ThrowsAny<ArgumentException>(() => goal.Rename(name!));
        Assert.Equal(originalName, goal.Name);
    }

    [Fact]
    public void SetTargetAmount_WithSameCurrencyPositiveAmount_UpdatesTarget()
    {
        var goal = CreateGoal();
        var newAmount = new Money(20_000_000, "PHP");

        goal.SetTargetAmount(newAmount);

        Assert.Same(newAmount, goal.TargetAmount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SetTargetAmount_WithNonPositiveAmount_IsRejectedWithoutMutation(long amountMinor)
    {
        var goal = CreateGoal();
        var originalAmount = goal.TargetAmount;

        Assert.Throws<ArgumentOutOfRangeException>(
            () => goal.SetTargetAmount(new Money(amountMinor, "PHP")));
        Assert.Same(originalAmount, goal.TargetAmount);
    }

    [Fact]
    public void SetTargetAmount_WithNull_IsRejectedWithoutMutation()
    {
        var goal = CreateGoal();
        var originalAmount = goal.TargetAmount;

        Assert.Throws<ArgumentNullException>(() => goal.SetTargetAmount(null!));
        Assert.Same(originalAmount, goal.TargetAmount);
    }

    [Fact]
    public void SetTargetAmount_WithDifferentCurrency_IsRejectedWithoutMutation()
    {
        var goal = CreateGoal();
        var originalAmount = goal.TargetAmount;

        Assert.Throws<ArgumentException>(() => goal.SetTargetAmount(new Money(20_000_000, "USD")));
        Assert.Same(originalAmount, goal.TargetAmount);
        Assert.Equal("PHP", goal.CurrencyCode);
    }

    [Fact]
    public void SetTargetDate_SetsAndChangesDate()
    {
        var goal = new SavingsGoal("Goal", PositiveAmount());
        var firstDate = new DateOnly(2027, 1, 1);
        var secondDate = new DateOnly(2028, 1, 1);

        goal.SetTargetDate(firstDate);
        Assert.Equal(firstDate, goal.TargetDate);
        goal.SetTargetDate(secondDate);
        Assert.Equal(secondDate, goal.TargetDate);
    }

    [Fact]
    public void SetTargetDate_WithNull_ClearsDate()
    {
        var goal = CreateGoal();

        goal.SetTargetDate(null);

        Assert.Null(goal.TargetDate);
    }

    [Fact]
    public void SetDestinationAccount_SetsAndClearsAccount()
    {
        var goal = new SavingsGoal("Goal", PositiveAmount());
        var accountId = Guid.NewGuid();

        goal.SetDestinationAccount(accountId);
        Assert.Equal(accountId, goal.DestinationAccountId);
        goal.SetDestinationAccount(null);
        Assert.Null(goal.DestinationAccountId);
    }

    [Fact]
    public void SetDestinationAccount_WithEmptyId_IsRejectedWithoutMutation()
    {
        var accountId = Guid.NewGuid();
        var goal = new SavingsGoal("Goal", PositiveAmount(), destinationAccountId: accountId);

        Assert.Throws<ArgumentException>(() => goal.SetDestinationAccount(Guid.Empty));
        Assert.Equal(accountId, goal.DestinationAccountId);
    }

    [Fact]
    public void Archive_ArchivesGoal()
    {
        var goal = CreateGoal();

        goal.Archive();

        Assert.True(goal.IsArchived);
    }

    [Fact]
    public void Restore_RestoresArchivedGoal()
    {
        var goal = CreateGoal();
        goal.Archive();

        goal.Restore();

        Assert.False(goal.IsArchived);
    }

    private static SavingsGoal CreateGoal() =>
        new("Emergency Fund", PositiveAmount(), TargetDate, Guid.NewGuid());

    private static Money PositiveAmount() => new(10_000_000, "PHP");
}
