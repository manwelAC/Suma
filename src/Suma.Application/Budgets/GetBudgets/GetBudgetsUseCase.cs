using Suma.Application.Abstractions.Persistence;

namespace Suma.Application.Budgets.GetBudgets;

public sealed record BudgetSummary(
    Guid Id,
    string Name,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    long ExpectedIncomeMinor,
    string CurrencyCode,
    bool IsArchived);

public sealed class GetBudgetsUseCase(IBudgetStore budgets)
{
    public async Task<IReadOnlyList<BudgetSummary>> ExecuteAsync(
        bool archived,
        CancellationToken cancellationToken = default) =>
        (await budgets.GetAsync(archived, cancellationToken))
            .Select(budget => new BudgetSummary(
                budget.Id,
                budget.Name,
                budget.PeriodStart,
                budget.PeriodEnd,
                budget.ExpectedIncome.AmountMinor,
                budget.CurrencyCode,
                budget.IsArchived))
            .ToArray();
}
