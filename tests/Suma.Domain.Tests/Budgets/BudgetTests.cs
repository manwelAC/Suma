using Suma.Domain.Budgets;
using Suma.Domain.ValueObjects;
using Xunit;

namespace Suma.Domain.Tests.Budgets;

public sealed class BudgetTests
{
    private static readonly DateOnly PeriodStart = new(2026, 9, 1);
    private static readonly DateOnly PeriodEnd = new(2026, 9, 30);

    [Fact]
    public void Create_WithValidValues_CreatesActiveBudget()
    {
        var expectedIncome = new Money(1_800_000, "PHP");

        var budget = new Budget("  September 2026  ", PeriodStart, PeriodEnd, expectedIncome);

        Assert.NotEqual(Guid.Empty, budget.Id);
        Assert.Equal("September 2026", budget.Name);
        Assert.Equal(PeriodStart, budget.PeriodStart);
        Assert.Equal(PeriodEnd, budget.PeriodEnd);
        Assert.Same(expectedIncome, budget.ExpectedIncome);
        Assert.Equal("PHP", budget.CurrencyCode);
        Assert.False(budget.IsArchived);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyName_IsRejected(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => new Budget(name!, PeriodStart, PeriodEnd, Money.Zero("PHP")));
    }

    [Theory]
    [InlineData(1_800_000)]
    [InlineData(0)]
    public void Create_WithNonNegativeExpectedIncome_IsAllowed(long amountMinor)
    {
        var expectedIncome = new Money(amountMinor, "PHP");

        var budget = new Budget("September", PeriodStart, PeriodEnd, expectedIncome);

        Assert.Same(expectedIncome, budget.ExpectedIncome);
    }

    [Fact]
    public void Create_WithNegativeExpectedIncome_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Budget("September", PeriodStart, PeriodEnd, new Money(-1, "PHP")));
    }

    [Fact]
    public void Create_WithNullExpectedIncome_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => new Budget("September", PeriodStart, PeriodEnd, null!));
    }

    [Fact]
    public void Create_WithOneDayPeriod_IsAllowed()
    {
        var date = new DateOnly(2026, 9, 15);

        var budget = new Budget("One Day", date, date, Money.Zero("PHP"));

        Assert.Equal(date, budget.PeriodStart);
        Assert.Equal(date, budget.PeriodEnd);
    }

    [Fact]
    public void Create_WithReversedPeriod_IsRejected()
    {
        Assert.Throws<ArgumentException>(
            () => new Budget("Invalid", PeriodEnd, PeriodStart, Money.Zero("PHP")));
    }

    [Fact]
    public void Rename_WithValidName_TrimsAndUpdatesName()
    {
        var budget = CreateBudget();

        budget.Rename("  September Budget  ");

        Assert.Equal("September Budget", budget.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_WithEmptyName_IsRejectedWithoutChangingName(string? name)
    {
        var budget = CreateBudget();
        var originalName = budget.Name;

        Assert.ThrowsAny<ArgumentException>(() => budget.Rename(name!));
        Assert.Equal(originalName, budget.Name);
    }

    [Fact]
    public void UpdatePeriod_WithValidPeriod_UpdatesBothDates()
    {
        var budget = CreateBudget();
        var newStart = new DateOnly(2026, 10, 1);
        var newEnd = new DateOnly(2026, 10, 31);

        budget.UpdatePeriod(newStart, newEnd);

        Assert.Equal(newStart, budget.PeriodStart);
        Assert.Equal(newEnd, budget.PeriodEnd);
    }

    [Fact]
    public void UpdatePeriod_WithReversedPeriod_IsRejectedWithoutChangingPeriod()
    {
        var budget = CreateBudget();
        var originalStart = budget.PeriodStart;
        var originalEnd = budget.PeriodEnd;

        Assert.Throws<ArgumentException>(() => budget.UpdatePeriod(PeriodEnd, PeriodStart));
        Assert.Equal(originalStart, budget.PeriodStart);
        Assert.Equal(originalEnd, budget.PeriodEnd);
    }

    [Theory]
    [InlineData(2_000_000)]
    [InlineData(0)]
    public void SetExpectedIncome_WithSameCurrencyNonNegativeAmount_UpdatesIncome(long amountMinor)
    {
        var budget = CreateBudget();
        var expectedIncome = new Money(amountMinor, "PHP");

        budget.SetExpectedIncome(expectedIncome);

        Assert.Same(expectedIncome, budget.ExpectedIncome);
        Assert.Equal("PHP", budget.CurrencyCode);
    }

    [Fact]
    public void SetExpectedIncome_WithNegativeAmount_IsRejectedWithoutChangingIncome()
    {
        var budget = CreateBudget();
        var originalIncome = budget.ExpectedIncome;

        Assert.Throws<ArgumentOutOfRangeException>(
            () => budget.SetExpectedIncome(new Money(-1, "PHP")));
        Assert.Same(originalIncome, budget.ExpectedIncome);
    }

    [Fact]
    public void SetExpectedIncome_WithNull_IsRejectedWithoutChangingIncome()
    {
        var budget = CreateBudget();
        var originalIncome = budget.ExpectedIncome;

        Assert.Throws<ArgumentNullException>(() => budget.SetExpectedIncome(null!));
        Assert.Same(originalIncome, budget.ExpectedIncome);
    }

    [Fact]
    public void SetExpectedIncome_WithDifferentCurrency_IsRejectedWithoutChangingIncome()
    {
        var budget = CreateBudget();
        var originalIncome = budget.ExpectedIncome;

        Assert.Throws<ArgumentException>(
            () => budget.SetExpectedIncome(new Money(2_000_000, "USD")));
        Assert.Same(originalIncome, budget.ExpectedIncome);
        Assert.Equal("PHP", budget.CurrencyCode);
    }

    [Fact]
    public void Archive_ArchivesBudget()
    {
        var budget = CreateBudget();

        budget.Archive();

        Assert.True(budget.IsArchived);
    }

    [Fact]
    public void Restore_RestoresArchivedBudget()
    {
        var budget = CreateBudget();
        budget.Archive();

        budget.Restore();

        Assert.False(budget.IsArchived);
    }

    private static Budget CreateBudget() =>
        new("September", PeriodStart, PeriodEnd, new Money(1_800_000, "PHP"));
}
