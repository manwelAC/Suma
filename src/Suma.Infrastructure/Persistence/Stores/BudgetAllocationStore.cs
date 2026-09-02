using Microsoft.EntityFrameworkCore;
using Suma.Application.Abstractions.Persistence;
using Suma.Domain.Budgets;

namespace Suma.Infrastructure.Persistence.Stores;

public sealed class BudgetAllocationStore(SumaDbContext context) : IBudgetAllocationStore
{
    public Task<bool> ExistsAsync(Guid budgetId, Guid categoryId, CancellationToken cancellationToken = default) =>
        context.BudgetAllocations.AsNoTracking().AnyAsync(allocation => allocation.BudgetId == budgetId && allocation.CategoryId == categoryId, cancellationToken);

    public async Task<IReadOnlyList<BudgetAllocationRecord>> GetForBudgetAsync(Guid budgetId, CancellationToken cancellationToken = default) =>
        await (
            from allocation in context.BudgetAllocations.AsNoTracking()
            join category in context.Categories.AsNoTracking() on allocation.CategoryId equals category.Id
            where allocation.BudgetId == budgetId
            orderby category.Name
            select new BudgetAllocationRecord(
                allocation.Id,
                allocation.BudgetId,
                allocation.CategoryId,
                category.Name,
                category.IsArchived,
                allocation.Amount.AmountMinor,
                allocation.Amount.CurrencyCode,
                allocation.ReserveFromAvailable))
            .ToListAsync(cancellationToken);

    public async Task AddAsync(BudgetAllocation allocation, CancellationToken cancellationToken = default) =>
        await context.BudgetAllocations.AddAsync(allocation, cancellationToken);
}
