using Microsoft.EntityFrameworkCore;
using Suma.Application.Abstractions.Persistence;
using Suma.Domain.Budgets;

namespace Suma.Infrastructure.Persistence.Stores;

public sealed class BudgetStore(SumaDbContext context) : IBudgetStore
{
    public Task<Budget?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Budgets.SingleOrDefaultAsync(budget => budget.Id == id, cancellationToken);

    public Task<bool> HasActiveOverlapAsync(DateOnly periodStart, DateOnly periodEnd, Guid? excludingBudgetId = null, CancellationToken cancellationToken = default) =>
        context.Budgets.AsNoTracking().AnyAsync(
            budget => !budget.IsArchived
                && (!excludingBudgetId.HasValue || budget.Id != excludingBudgetId.Value)
                && budget.PeriodStart <= periodEnd
                && budget.PeriodEnd >= periodStart,
            cancellationToken);

    public async Task AddAsync(Budget budget, CancellationToken cancellationToken = default) =>
        await context.Budgets.AddAsync(budget, cancellationToken);
}
