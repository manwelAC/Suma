using Suma.Domain.Budgets;

namespace Suma.Application.Abstractions.Persistence;

public interface IBudgetAllocationStore
{
    Task<bool> ExistsAsync(Guid budgetId, Guid categoryId, CancellationToken cancellationToken = default);

    Task AddAsync(BudgetAllocation allocation, CancellationToken cancellationToken = default);
}
