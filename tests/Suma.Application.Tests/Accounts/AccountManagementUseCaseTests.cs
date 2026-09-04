using Suma.Application.Accounts.ArchiveAccount;
using Suma.Application.Accounts.CreateAccount;
using Suma.Application.Accounts.GetAccounts;
using Suma.Application.Accounts.RestoreAccount;
using Suma.Application.Accounts.UpdateAccount;
using Suma.Application.Common.Exceptions;
using Suma.Application.Tests.TestDoubles;
using Suma.Domain.Accounts;
using Suma.Domain.ValueObjects;
using Xunit;

namespace Suma.Application.Tests.Accounts;

public sealed class AccountManagementUseCaseTests
{
    [Fact]
    public async Task Create_valid_account_adds_and_saves()
    {
        var data = new FakeData();

        var result = await new CreateAccountUseCase(data, data).ExecuteAsync(
            new("Everyday", AccountType.Bank, 12_345, "php", true), Token);

        Assert.Equal("PHP", result.CurrencyCode);
        Assert.Equal(12_345, result.OpeningBalanceMinor);
        Assert.Single(data.Accounts);
        Assert.Equal(1, data.SaveCount);
    }

    [Theory]
    [InlineData("", "PHP", AccountType.Bank)]
    [InlineData("Everyday", "PH", AccountType.Bank)]
    [InlineData("Everyday", "PHP", (AccountType)999)]
    public async Task Create_invalid_input_rejects_without_save(string name, string currency, AccountType type)
    {
        var data = new FakeData();

        await Assert.ThrowsAsync<ApplicationValidationException>(() =>
            new CreateAccountUseCase(data, data).ExecuteAsync(new(name, type, 0, currency, true), Token));

        Assert.Empty(data.Accounts);
        Assert.Equal(0, data.SaveCount);
    }

    [Fact]
    public async Task Update_changes_only_safe_mutable_fields()
    {
        var data = DataWithAccount(out var account);
        var originalOpeningBalance = account.OpeningBalance;

        var result = await new UpdateAccountUseCase(data, data).ExecuteAsync(
            new(account.Id, "  Daily wallet ", AccountType.EWallet, false), Token);

        Assert.Equal("Daily wallet", result.Name);
        Assert.Equal(AccountType.EWallet, account.Type);
        Assert.False(account.IncludeInAvailableToSpend);
        Assert.Equal(originalOpeningBalance.AmountMinor, account.OpeningBalance.AmountMinor);
        Assert.Equal(1, data.SaveCount);
    }

    [Fact]
    public async Task Update_updates_account_number_and_opening_balance()
    {
        var data = DataWithAccount(out var account);

        var result = await new UpdateAccountUseCase(data, data).ExecuteAsync(
            new(account.Id, "Daily wallet", AccountType.EWallet, true, "09171234567", 99_000), Token);

        Assert.Equal("09171234567", result.AccountNumber);
        Assert.Equal("09171234567", account.AccountNumber);
        Assert.Equal(99_000, result.OpeningBalanceMinor);
        Assert.Equal(99_000, account.OpeningBalance.AmountMinor);
        Assert.Equal(1, data.SaveCount);
    }

    [Fact]
    public async Task Update_missing_account_returns_not_found_without_save()
    {
        var data = new FakeData();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateAccountUseCase(data, data).ExecuteAsync(
                new(Guid.NewGuid(), "Missing", AccountType.Other, false), Token));

        Assert.Equal(0, data.SaveCount);
    }

    [Fact]
    public async Task Invalid_update_does_not_mutate_or_save()
    {
        var data = DataWithAccount(out var account);

        await Assert.ThrowsAsync<ApplicationValidationException>(() =>
            new UpdateAccountUseCase(data, data).ExecuteAsync(
                new(account.Id, " ", AccountType.Savings, false), Token));

        Assert.Equal("Cash", account.Name);
        Assert.Equal(AccountType.Cash, account.Type);
        Assert.Equal(0, data.SaveCount);
    }

    [Fact]
    public async Task Archive_and_restore_persist_and_archived_list_is_derived()
    {
        var data = DataWithAccount(out var account);
        await new ArchiveAccountUseCase(data, data).ExecuteAsync(account.Id, Token);

        var archived = await new GetAccountsUseCase(data, data).ExecuteArchivedAsync(Token);
        Assert.Single(archived);
        Assert.True(account.IsArchived);

        await new RestoreAccountUseCase(data, data).ExecuteAsync(account.Id, Token);
        Assert.False(account.IsArchived);
        Assert.Equal(2, data.SaveCount);
    }

    [Fact]
    public async Task Archive_and_restore_missing_account_return_not_found_without_save()
    {
        var data = new FakeData();
        var id = Guid.NewGuid();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new ArchiveAccountUseCase(data, data).ExecuteAsync(id, Token));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new RestoreAccountUseCase(data, data).ExecuteAsync(id, Token));

        Assert.Equal(0, data.SaveCount);
    }

    private static FakeData DataWithAccount(out Account account)
    {
        var data = new FakeData();
        account = new Account("Cash", AccountType.Cash, new Money(500, "PHP"), "PHP", true);
        data.Accounts.Add(account.Id, account);
        return data;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
