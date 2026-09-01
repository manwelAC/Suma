using Suma.Application.Savings.AddGoalContribution;
using Suma.Application.Savings.CreateSavingsGoal;
using Suma.Application.Tests.TestDoubles;
using Suma.Application.Tests.Transactions;
using Suma.Domain.Categories;
using Suma.Domain.Savings;
using Suma.Domain.Transactions;
using Suma.Domain.ValueObjects;
using Xunit;

namespace Suma.Application.Tests.Savings;

public sealed class SavingsUseCaseTests
{
    [Fact]
    public async Task CreateSavingsGoal_valid_destination_succeeds()
    {
        var data = new FakeData();
        var account = TransactionUseCaseTests.AddAccount(data, "Savings");
        var result = await new CreateSavingsGoalUseCase(data, data, data).ExecuteAsync(new("Goal", 1_000, "PHP", DestinationAccountId: account.Id), Token);
        Assert.Equal(account.Id, result.DestinationAccountId);
        Assert.Equal(1, data.SaveCount);
    }

    [Fact]
    public async Task CreateSavingsGoal_allows_no_destination()
    {
        var data = new FakeData();
        var result = await new CreateSavingsGoalUseCase(data, data, data).ExecuteAsync(new("Goal", 1_000, "PHP"), Token);
        Assert.Null(result.DestinationAccountId);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("archived")]
    [InlineData("currency")]
    public async Task CreateSavingsGoal_rejects_invalid_destination(string scenario)
    {
        var data = new FakeData();
        var account = TransactionUseCaseTests.AddAccount(data, "Savings");
        if (scenario == "missing") data.Accounts.Clear();
        if (scenario == "archived") account.Archive();
        await Assert.ThrowsAnyAsync<Exception>(() => new CreateSavingsGoalUseCase(data, data, data).ExecuteAsync(new("Goal", 1_000, scenario == "currency" ? "USD" : "PHP", DestinationAccountId: account.Id), Token));
    }

    [Theory]
    [InlineData(GoalContributionType.Deposit)]
    [InlineData(GoalContributionType.Withdrawal)]
    public async Task AddGoalContribution_valid_direction_succeeds(GoalContributionType type)
    {
        var data = ValidContributionData(out var goal, out var transaction);
        var result = await new AddGoalContributionUseCase(data, data, data, data).ExecuteAsync(new(goal.Id, transaction.Id, type, 500, "PHP"), Token);
        Assert.Equal(type, result.Type);
        Assert.Equal(1, data.SaveCount);
    }

    [Theory]
    [InlineData("missing-goal")]
    [InlineData("archived-goal")]
    [InlineData("missing-transaction")]
    [InlineData("currency")]
    [InlineData("exceeds")]
    [InlineData("existing-exceeds")]
    public async Task AddGoalContribution_rejects_invalid_state(string scenario)
    {
        var data = ValidContributionData(out var goal, out var transaction);
        if (scenario == "missing-goal") data.Goals.Clear();
        if (scenario == "archived-goal") goal.Archive();
        if (scenario == "missing-transaction") data.Transactions.Clear();
        if (scenario == "existing-exceeds") data.AttributedAmountMinor = 800;
        var amount = scenario == "exceeds" ? 1_100 : 500;
        await Assert.ThrowsAnyAsync<Exception>(() => new AddGoalContributionUseCase(data, data, data, data).ExecuteAsync(new(goal.Id, transaction.Id, GoalContributionType.Deposit, amount, scenario == "currency" ? "USD" : "PHP"), Token));
    }

    private static FakeData ValidContributionData(out SavingsGoal goal, out Transaction transaction)
    {
        var data = new FakeData();
        var account = TransactionUseCaseTests.AddAccount(data, "Savings");
        var category = TransactionUseCaseTests.AddCategory(data, CategoryTransactionKind.Income);
        transaction = Transaction.CreateIncome(account.Id, category.Id, new Money(1_000, "PHP"), new(2026, 9, 1));
        goal = new SavingsGoal("Goal", new Money(10_000, "PHP"));
        data.Transactions.Add(transaction.Id, transaction);
        data.Goals.Add(goal.Id, goal);
        return data;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
