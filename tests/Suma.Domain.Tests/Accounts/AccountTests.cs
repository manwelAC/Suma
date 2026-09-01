using Suma.Domain.Accounts;
using Suma.Domain.ValueObjects;
using Xunit;

namespace Suma.Domain.Tests.Accounts;

public sealed class AccountTests
{
    [Fact]
    public void Create_WithValidValues_CreatesActiveAccount()
    {
        var openingBalance = new Money(14_950, "PHP");

        var account = new Account("Everyday Cash", AccountType.Cash, openingBalance, "php", true);

        Assert.NotEqual(Guid.Empty, account.Id);
        Assert.Equal("Everyday Cash", account.Name);
        Assert.Equal(AccountType.Cash, account.Type);
        Assert.Equal(openingBalance, account.OpeningBalance);
        Assert.Equal("PHP", account.CurrencyCode);
        Assert.True(account.IncludeInAvailableToSpend);
        Assert.False(account.IsArchived);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyName_IsRejected(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => new Account(name!, AccountType.Cash, Money.Zero("PHP"), "PHP", true));
    }

    [Fact]
    public void Create_WithMismatchedOpeningBalanceCurrency_IsRejected()
    {
        Assert.Throws<ArgumentException>(
            () => new Account("Cash", AccountType.Cash, Money.Zero("USD"), "PHP", true));
    }

    [Theory]
    [InlineData(-100)]
    [InlineData(0)]
    [InlineData(100)]
    public void Create_AllowsAnyOpeningBalanceSign(long amountMinor)
    {
        var account = new Account(
            "Cash",
            AccountType.Cash,
            new Money(amountMinor, "PHP"),
            "PHP",
            true);

        Assert.Equal(amountMinor, account.OpeningBalance.AmountMinor);
    }

    [Fact]
    public void Archive_ArchivesAccount()
    {
        var account = CreateAccount();

        account.Archive();

        Assert.True(account.IsArchived);
    }

    [Fact]
    public void Restore_RestoresArchivedAccount()
    {
        var account = CreateAccount();
        account.Archive();

        account.Restore();

        Assert.False(account.IsArchived);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetAvailableToSpendInclusion_UpdatesInclusion(bool include)
    {
        var account = CreateAccount();

        account.SetAvailableToSpendInclusion(include);

        Assert.Equal(include, account.IncludeInAvailableToSpend);
    }

    private static Account CreateAccount() =>
        new("Cash", AccountType.Cash, Money.Zero("PHP"), "PHP", true);
}
