using Suma.Domain.ValueObjects;
using Xunit;

namespace Suma.Domain.Tests.ValueObjects;

public sealed class MoneyTests
{
    [Fact]
    public void Create_StoresMinorUnitsAndCurrency()
    {
        var money = new Money(14_950, "PHP");

        Assert.Equal(14_950, money.AmountMinor);
        Assert.Equal("PHP", money.CurrencyCode);
    }

    [Fact]
    public void EqualValues_AreEqual()
    {
        var first = new Money(100, "PHP");
        var second = new Money(100, "PHP");

        Assert.Equal(first, second);
    }

    [Fact]
    public void Addition_AddsMinorUnits()
    {
        var result = new Money(100, "PHP") + new Money(250, "PHP");

        Assert.Equal(new Money(350, "PHP"), result);
    }

    [Fact]
    public void Subtraction_SubtractsMinorUnits()
    {
        var result = new Money(250, "PHP") - new Money(400, "PHP");

        Assert.Equal(new Money(-150, "PHP"), result);
    }

    [Fact]
    public void NegativeAmount_IsAllowedAndReportedAsNegative()
    {
        var money = new Money(-1, "PHP");

        Assert.True(money.IsNegative);
        Assert.False(money.IsPositive);
        Assert.False(money.IsZero);
    }

    [Fact]
    public void PositiveAmount_IsReportedAsPositive()
    {
        var money = new Money(1, "PHP");

        Assert.True(money.IsPositive);
        Assert.False(money.IsNegative);
        Assert.False(money.IsZero);
    }

    [Fact]
    public void Zero_CreatesZeroForCurrency()
    {
        var money = Money.Zero("PHP");

        Assert.Equal(0, money.AmountMinor);
        Assert.True(money.IsZero);
        Assert.False(money.IsPositive);
        Assert.False(money.IsNegative);
    }

    [Fact]
    public void CurrencyCode_IsTrimmedAndNormalizedToUppercase()
    {
        var money = new Money(100, " php ");

        Assert.Equal("PHP", money.CurrencyCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("PH")]
    [InlineData("PESO")]
    [InlineData("P1P")]
    public void InvalidCurrencyCode_IsRejected(string? currencyCode)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Money(100, currencyCode!));
    }

    [Fact]
    public void Addition_WithDifferentCurrencies_IsRejected()
    {
        var php = new Money(100, "PHP");
        var usd = new Money(100, "USD");

        Assert.Throws<InvalidOperationException>(() => php + usd);
    }

    [Fact]
    public void Subtraction_WithDifferentCurrencies_IsRejected()
    {
        var php = new Money(100, "PHP");
        var usd = new Money(100, "USD");

        Assert.Throws<InvalidOperationException>(() => php - usd);
    }

    [Fact]
    public void Comparison_OrdersValuesWithTheSameCurrency()
    {
        var smaller = new Money(100, "PHP");
        var larger = new Money(200, "PHP");

        Assert.True(smaller < larger);
        Assert.True(larger > smaller);
        Assert.True(smaller <= new Money(100, "PHP"));
        Assert.True(larger >= smaller);
    }

    [Fact]
    public void Comparison_WithDifferentCurrencies_IsRejected()
    {
        var php = new Money(100, "PHP");
        var usd = new Money(200, "USD");

        Assert.Throws<InvalidOperationException>(() => php.CompareTo(usd));
    }

    [Fact]
    public void EqualValues_WithNormalizedCurrencies_AreEqual()
    {
        var lowercase = new Money(100, "php");
        var uppercase = new Money(100, "PHP");

        Assert.Equal(lowercase, uppercase);
    }

    [Fact]
    public void Addition_WhenAmountOverflows_ThrowsOverflowException()
    {
        var maximum = new Money(long.MaxValue, "PHP");
        var one = new Money(1, "PHP");

        Assert.Throws<OverflowException>(() => maximum + one);
    }

    [Fact]
    public void Subtraction_WhenAmountOverflows_ThrowsOverflowException()
    {
        var minimum = new Money(long.MinValue, "PHP");
        var one = new Money(1, "PHP");

        Assert.Throws<OverflowException>(() => minimum - one);
    }

}
