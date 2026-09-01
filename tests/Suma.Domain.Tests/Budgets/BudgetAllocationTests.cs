using Suma.Domain.Budgets;
using Suma.Domain.ValueObjects;
using Xunit;

namespace Suma.Domain.Tests.Budgets;

public sealed class BudgetAllocationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Create_WithValidValues_PreservesState(bool reserveFromAvailable)
    {
        var budgetId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var amount = new Money(400_000, "PHP");

        var allocation = new BudgetAllocation(
            budgetId,
            categoryId,
            amount,
            reserveFromAvailable);

        Assert.NotEqual(Guid.Empty, allocation.Id);
        Assert.Equal(budgetId, allocation.BudgetId);
        Assert.Equal(categoryId, allocation.CategoryId);
        Assert.Same(amount, allocation.Amount);
        Assert.Equal("PHP", allocation.CurrencyCode);
        Assert.Equal(reserveFromAvailable, allocation.ReserveFromAvailable);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Create_WithEmptyRequiredId_IsRejected(bool emptyBudget, bool emptyCategory)
    {
        var budgetId = emptyBudget ? Guid.Empty : Guid.NewGuid();
        var categoryId = emptyCategory ? Guid.Empty : Guid.NewGuid();

        Assert.Throws<ArgumentException>(
            () => new BudgetAllocation(budgetId, categoryId, PositiveAmount(), true));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithNonPositiveAmount_IsRejected(long amountMinor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BudgetAllocation(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new Money(amountMinor, "PHP"),
                true));
    }

    [Fact]
    public void Create_WithNullAmount_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => new BudgetAllocation(Guid.NewGuid(), Guid.NewGuid(), null!, true));
    }

    [Fact]
    public void SetAmount_WithSameCurrencyPositiveAmount_UpdatesAmount()
    {
        var allocation = CreateAllocation();
        var amount = new Money(500_000, "PHP");

        allocation.SetAmount(amount);

        Assert.Same(amount, allocation.Amount);
        Assert.Equal("PHP", allocation.CurrencyCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SetAmount_WithNonPositiveAmount_IsRejectedWithoutChangingAmount(long amountMinor)
    {
        var allocation = CreateAllocation();
        var originalAmount = allocation.Amount;

        Assert.Throws<ArgumentOutOfRangeException>(
            () => allocation.SetAmount(new Money(amountMinor, "PHP")));
        Assert.Same(originalAmount, allocation.Amount);
    }

    [Fact]
    public void SetAmount_WithNull_IsRejectedWithoutChangingAmount()
    {
        var allocation = CreateAllocation();
        var originalAmount = allocation.Amount;

        Assert.Throws<ArgumentNullException>(() => allocation.SetAmount(null!));
        Assert.Same(originalAmount, allocation.Amount);
    }

    [Fact]
    public void SetAmount_WithDifferentCurrency_IsRejectedWithoutChangingAmount()
    {
        var allocation = CreateAllocation();
        var originalAmount = allocation.Amount;

        Assert.Throws<ArgumentException>(() => allocation.SetAmount(new Money(500_000, "USD")));
        Assert.Same(originalAmount, allocation.Amount);
        Assert.Equal("PHP", allocation.CurrencyCode);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void SetReserveFromAvailable_UpdatesFlag(bool initialValue, bool newValue)
    {
        var allocation = new BudgetAllocation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PositiveAmount(),
            initialValue);

        allocation.SetReserveFromAvailable(newValue);

        Assert.Equal(newValue, allocation.ReserveFromAvailable);
    }

    private static BudgetAllocation CreateAllocation() =>
        new(Guid.NewGuid(), Guid.NewGuid(), PositiveAmount(), true);

    private static Money PositiveAmount() => new(400_000, "PHP");
}
