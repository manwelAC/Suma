using Suma.Application.Abstractions.Persistence;
using Suma.Application.Budgets.GetBudgets;
using Suma.Application.Common.Exceptions;

namespace Suma.Application.Budgets.GetBudgetDetails;

public sealed record BudgetAllocationDetail(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    bool CategoryArchived,
    long AmountMinor,
    long SpentMinor,
    long RemainingMinor,
    decimal UtilizationPercent,
    bool ReserveFromAvailable);

public sealed record BudgetDetails(
    BudgetSummary Summary,
    long AllocatedMinor,
    long SpentMinor,
    long RemainingMinor,
    IReadOnlyList<BudgetAllocationDetail> Allocations);

public sealed class GetBudgetDetailsUseCase(
    IBudgetStore budgets,
    IBudgetAllocationStore allocations,
    ITransactionStore transactions)
{
    public async Task<BudgetDetails> ExecuteAsync(Guid budgetId, CancellationToken cancellationToken = default)
    {
        var budget = await budgets.GetByIdAsync(budgetId, cancellationToken)
            ?? throw new NotFoundException("Budget was not found.");
        var allocationRecords = await allocations.GetForBudgetAsync(budgetId, cancellationToken);
        var spending = (await transactions.GetNetExpenseAmountsByCategoryAsync(
                budget.PeriodStart,
                budget.PeriodEnd,
                budget.CurrencyCode,
                allocationRecords.Select(allocation => allocation.CategoryId).ToArray(),
                cancellationToken))
            .ToDictionary(item => item.CategoryId, item => item.AmountMinor);

        var details = allocationRecords.Select(allocation =>
        {
            var spent = spending.GetValueOrDefault(allocation.CategoryId);
            var remaining = checked(allocation.AmountMinor - spent);
            var utilization = checked((decimal)spent * 100m / allocation.AmountMinor);
            return new BudgetAllocationDetail(
                allocation.Id,
                allocation.CategoryId,
                allocation.CategoryName,
                allocation.CategoryArchived,
                allocation.AmountMinor,
                spent,
                remaining,
                utilization,
                allocation.ReserveFromAvailable);
        }).ToArray();

        var allocated = details.Aggregate(0L, (total, item) => checked(total + item.AmountMinor));
        var spentTotal = details.Aggregate(0L, (total, item) => checked(total + item.SpentMinor));
        return new BudgetDetails(
            new BudgetSummary(
                budget.Id,
                budget.Name,
                budget.PeriodStart,
                budget.PeriodEnd,
                budget.ExpectedIncome.AmountMinor,
                budget.CurrencyCode,
                budget.IsArchived),
            allocated,
            spentTotal,
            checked(allocated - spentTotal),
            details);
    }
}
