using Suma.Application.Savings.AddGoalContribution;
using Suma.Application.Savings.CreateSavingsGoal;
using Suma.Application.Savings.GetSavingsGoals;
using Suma.Application.Savings.GetSavingsGoalDetails;
using Suma.Application.Savings.GetGoalContributionCandidates;
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

    [Fact]
    public async Task Goal_reads_separate_archive_and_derive_unclamped_progress_remaining_and_history()
    {
        var data = ValidContributionData(out var goal, out var transaction);
        var account = data.Accounts.Values.Single(); account.Archive();
        goal.SetDestinationAccount(account.Id);
        data.Contributions.Add(new(goal.Id, transaction.Id, GoalContributionType.Deposit, new Money(1_200, "PHP")));
        data.Contributions.Add(new(goal.Id, transaction.Id, GoalContributionType.Withdrawal, new Money(300, "PHP")));
        var archived = new SavingsGoal("Archived", new Money(500, "PHP")); archived.Archive(); data.Goals.Add(archived.Id, archived);
        var active = await new GetSavingsGoalsUseCase(data).ExecuteAsync(false, Token);
        var archivedRows = await new GetSavingsGoalsUseCase(data).ExecuteAsync(true, Token);
        var summary = Assert.Single(active);
        Assert.Equal(900, summary.ProgressMinor);
        Assert.Equal(9_100, summary.RemainingMinor);
        Assert.Equal(account.Name, summary.DestinationAccountName);
        Assert.Equal(archived.Id, Assert.Single(archivedRows).Id);
        Assert.Equal(2, (await new GetSavingsGoalDetailsUseCase(data, data).ExecuteAsync(goal.Id, Token)).Contributions.Count);
    }

    [Fact]
    public async Task Progress_can_be_negative_or_above_target_without_clamping()
    {
        var data = ValidContributionData(out var goal, out var transaction);
        goal.SetTargetAmount(new Money(1_000, "PHP"));
        data.Contributions.Add(new(goal.Id, transaction.Id, GoalContributionType.Withdrawal, new Money(200, "PHP")));
        var negative = Assert.Single(await new GetSavingsGoalsUseCase(data).ExecuteAsync(false, Token));
        Assert.Equal(-200, negative.ProgressMinor); Assert.Equal(1_200, negative.RemainingMinor);
        data.Contributions.Clear(); data.Contributions.Add(new(goal.Id, transaction.Id, GoalContributionType.Deposit, new Money(1_500, "PHP")));
        var above = Assert.Single(await new GetSavingsGoalsUseCase(data).ExecuteAsync(false, Token));
        Assert.Equal(1_500, above.ProgressMinor); Assert.Equal(-500, above.RemainingMinor);
    }

    [Fact]
    public async Task Candidates_exclude_wrong_currency_and_full_attribution_and_preserve_partial_capacity()
    {
        var data = ValidContributionData(out var goal, out var php);
        var usdAccount = TransactionUseCaseTests.AddAccount(data, "Dollar", "USD");
        var category = data.Categories.Values.Single();
        var usd = Transaction.CreateIncome(usdAccount.Id, category.Id, new Money(99_999, "USD"), php.TransactionDate);
        var full = Transaction.CreateIncome(data.Accounts.Values.First().Id, category.Id, new Money(400, "PHP"), php.TransactionDate);
        data.Transactions.Add(usd.Id, usd); data.Transactions.Add(full.Id, full);
        data.Contributions.Add(new(goal.Id, php.Id, GoalContributionType.Deposit, new Money(300, "PHP")));
        data.Contributions.Add(new(goal.Id, full.Id, GoalContributionType.Deposit, new Money(400, "PHP")));
        var candidates = await new GetGoalContributionCandidatesUseCase(data, data).ExecuteAsync(goal.Id, Token);
        var partial = Assert.Single(candidates);
        Assert.Equal(php.Id, partial.TransactionId);
        Assert.Equal(700, partial.RemainingCapacityMinor);
        Assert.DoesNotContain(candidates, item => item.TransactionId == usd.Id || item.TransactionId == full.Id);
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
