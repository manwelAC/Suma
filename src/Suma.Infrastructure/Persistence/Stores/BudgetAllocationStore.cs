using Microsoft.EntityFrameworkCore;
using Suma.Application.Abstractions.Persistence;
using Suma.Domain.Budgets;

namespace Suma.Infrastructure.Persistence.Stores;

public sealed class BudgetAllocationStore(SumaDbContext context) : IBudgetAllocationStore
{
    public Task<bool> ExistsAsync(Guid budgetId, Guid categoryId, CancellationToken cancellationToken = default) =>
        context.BudgetAllocations.AsNoTracking().AnyAsync(allocation => allocation.BudgetId == budgetId && allocation.CategoryId == categoryId, cancellationToken);

    public async Task AddAsync(BudgetAllocation allocation, CancellationToken cancellationToken = default) =>
        await context.BudgetAllocations.AddAsync(allocation, cancellationToken);
}
