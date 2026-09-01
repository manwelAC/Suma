using Suma.Application.Budgets.AddBudgetAllocation;
using Suma.Application.Budgets.CreateBudget;
using Suma.Application.Tests.TestDoubles;
using Suma.Application.Tests.Transactions;
using Suma.Domain.Budgets;
using Suma.Domain.Categories;
using Suma.Domain.ValueObjects;
using Xunit;

namespace Suma.Application.Tests.Budgets;

public sealed class BudgetUseCaseTests
{
    [Fact]
    public async Task CreateBudget_allows_valid_non_overlapping_periods()
    {
        var data = new FakeData { HasOverlap = false };
        var result = await new CreateBudgetUseCase(data, data).ExecuteAsync(new("September", new(2026, 9, 1), new(2026, 9, 30), 10_000, "PHP"), Token);
        Assert.Equal("September", result.Name);
        Assert.Equal(1, data.SaveCount);
    }

    [Fact]
    public async Task CreateBudget_rejects_active_overlap()
    {
        var data = new FakeData { HasOverlap = true };
        await Assert.ThrowsAnyAsync<Exception>(() => new CreateBudgetUseCase(data, data).ExecuteAsync(new("September", new(2026, 9, 1), new(2026, 9, 30), 0, "PHP"), Token));
    }

    [Fact]
    public async Task CreateBudget_ignores_archived_overlap_when_store_reports_none()
    {
        var data = new FakeData { HasOverlap = false };
        await new CreateBudgetUseCase(data, data).ExecuteAsync(new("September", new(2026, 9, 1), new(2026, 9, 30), 0, "PHP"), Token);
        Assert.Single(data.Budgets);
    }

    [Fact]
    public async Task AddBudgetAllocation_valid_request_succeeds()
    {
        var data = ValidData(out var budget, out var category);
        var result = await new AddBudgetAllocationUseCase(data, data, data, data).ExecuteAsync(new(budget.Id, category.Id, 500, "PHP", true), Token);
        Assert.True(result.ReserveFromAvailable);
        Assert.Equal(1, data.SaveCount);
    }

    [Theory]
    [InlineData("missing-budget")]
    [InlineData("archived-budget")]
    [InlineData("missing-category")]
    [InlineData("archived-category")]
    [InlineData("income-category")]
    [InlineData("currency")]
    [InlineData("duplicate")]
    public async Task AddBudgetAllocation_rejects_invalid_state(string scenario)
    {
        var data = ValidData(out var budget, out var category);
        if (scenario == "missing-budget") data.Budgets.Clear();
        if (scenario == "archived-budget") budget.Archive();
        if (scenario == "missing-category") data.Categories.Clear();
        if (scenario == "archived-category") category.Archive();
        if (scenario == "income-category") { data.Categories.Clear(); category = TransactionUseCaseTests.AddCategory(data, CategoryTransactionKind.Income); }
        if (scenario == "duplicate") data.Allocations.Add(new BudgetAllocation(budget.Id, category.Id, new Money(100, "PHP"), false));
        await Assert.ThrowsAnyAsync<Exception>(() => new AddBudgetAllocationUseCase(data, data, data, data).ExecuteAsync(new(budget.Id, category.Id, 500, scenario == "currency" ? "USD" : "PHP", true), Token));
    }

    private static FakeData ValidData(out Budget budget, out Category category)
    {
        var data = new FakeData();
        budget = new Budget("September", new(2026, 9, 1), new(2026, 9, 30), Money.Zero("PHP"));
        category = TransactionUseCaseTests.AddCategory(data, CategoryTransactionKind.Expense);
        data.Budgets.Add(budget.Id, budget);
        return data;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
