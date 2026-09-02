using Suma.Application.Budgets.AddBudgetAllocation;
using Suma.Application.Budgets.ArchiveBudget;
using Suma.Application.Budgets.CreateBudget;
using Suma.Application.Budgets.GetBudgetDetails;
using Suma.Application.Budgets.GetBudgets;
using Suma.Application.Budgets.RestoreBudget;
using Suma.Application.Common.Exceptions;
using Suma.Application.Tests.TestDoubles;
using Suma.Application.Tests.Transactions;
using Suma.Domain.Budgets;
using Suma.Domain.Categories;
using Suma.Domain.Transactions;
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

    [Fact]
    public async Task Budget_queries_separate_archive_state_and_resolve_archived_allocation_names()
    {
        var data = ValidData(out var active, out var category);
        data.Allocations.Add(new(active.Id, category.Id, new Money(500, "PHP"), true));
        category.Archive();
        var archived = new Budget("August", new(2026, 8, 1), new(2026, 8, 31), new Money(2_000, "PHP"));
        archived.Archive();
        data.Budgets.Add(archived.Id, archived);

        var activeResults = await new GetBudgetsUseCase(data).ExecuteAsync(false, Token);
        var archivedResults = await new GetBudgetsUseCase(data).ExecuteAsync(true, Token);
        var details = await new GetBudgetDetailsUseCase(data, data, data).ExecuteAsync(active.Id, Token);

        Assert.Equal(active.Id, Assert.Single(activeResults).Id);
        Assert.Equal(archived.Id, Assert.Single(archivedResults).Id);
        Assert.True(Assert.Single(details.Allocations).CategoryArchived);
        Assert.Equal(category.Name, details.Allocations[0].CategoryName);
    }

    [Fact]
    public async Task Budget_details_use_matching_currency_exact_categories_and_all_refunds_and_allow_overspending()
    {
        var data = ValidData(out var budget, out var food);
        var transport = TransactionUseCaseTests.AddCategory(data, CategoryTransactionKind.Expense);
        var child = TransactionUseCaseTests.AddCategory(data, CategoryTransactionKind.Expense);
        data.Allocations.Add(new(budget.Id, food.Id, new Money(500, "PHP"), true));
        data.Allocations.Add(new(budget.Id, transport.Id, new Money(300, "PHP"), false));

        var foodExpense = Transaction.CreateExpense(Guid.NewGuid(), food.Id, new Money(600, "PHP"), new(2026, 9, 2));
        var transportExpense = Transaction.CreateExpense(Guid.NewGuid(), transport.Id, new Money(350, "PHP"), new(2026, 9, 3));
        var outOfPeriod = Transaction.CreateExpense(Guid.NewGuid(), food.Id, new Money(100, "PHP"), new(2026, 8, 31));
        var childExpense = Transaction.CreateExpense(Guid.NewGuid(), child.Id, new Money(200, "PHP"), new(2026, 9, 4));
        var foreignCurrencyExpense = Transaction.CreateExpense(Guid.NewGuid(), food.Id, new Money(20_000, "USD"), new(2026, 9, 5));
        var refundAfterPeriod = Transaction.CreateRefund(Guid.NewGuid(), food.Id, foodExpense.Id, new Money(25, "PHP"), new(2026, 10, 3));
        foreach (var transaction in new[] { foodExpense, transportExpense, outOfPeriod, childExpense, foreignCurrencyExpense, refundAfterPeriod })
        {
            data.Transactions.Add(transaction.Id, transaction);
        }

        var details = await new GetBudgetDetailsUseCase(data, data, data).ExecuteAsync(budget.Id, Token);
        var foodDetail = Assert.Single(details.Allocations, item => item.CategoryId == food.Id);

        Assert.Equal(575, foodDetail.SpentMinor);
        Assert.Equal(-75, foodDetail.RemainingMinor);
        Assert.Equal(115m, foodDetail.UtilizationPercent);
        Assert.Equal(800, details.AllocatedMinor);
        Assert.Equal(925, details.SpentMinor);
        Assert.Equal(-125, details.RemainingMinor);
    }

    [Fact]
    public async Task Archive_and_restore_persist_and_restore_rechecks_overlap()
    {
        var data = ValidData(out var budget, out _);
        await new ArchiveBudgetUseCase(data, data).ExecuteAsync(budget.Id, Token);
        Assert.True(budget.IsArchived);
        Assert.Equal(1, data.SaveCount);

        data.HasOverlap = false;
        await new RestoreBudgetUseCase(data, data).ExecuteAsync(budget.Id, Token);
        Assert.False(budget.IsArchived);
        Assert.Equal(2, data.SaveCount);

        budget.Archive();
        data.HasOverlap = true;
        await Assert.ThrowsAsync<ConflictException>(() => new RestoreBudgetUseCase(data, data).ExecuteAsync(budget.Id, Token));
        Assert.True(budget.IsArchived);
        Assert.Equal(2, data.SaveCount);
    }

    [Fact]
    public async Task Archive_and_restore_report_missing_budget_without_saving()
    {
        var data = new FakeData();
        await Assert.ThrowsAsync<NotFoundException>(() => new ArchiveBudgetUseCase(data, data).ExecuteAsync(Guid.NewGuid(), Token));
        await Assert.ThrowsAsync<NotFoundException>(() => new RestoreBudgetUseCase(data, data).ExecuteAsync(Guid.NewGuid(), Token));
        Assert.Equal(0, data.SaveCount);
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
