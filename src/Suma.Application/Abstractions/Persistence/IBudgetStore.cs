using Suma.Domain.Budgets;

namespace Suma.Application.Abstractions.Persistence;

public interface IBudgetStore
{
    Task<Budget?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Budget>> GetAsync(bool archived, CancellationToken cancellationToken = default);

    Task<bool> HasActiveOverlapAsync(DateOnly periodStart, DateOnly periodEnd, Guid? excludingBudgetId = null, CancellationToken cancellationToken = default);

    Task AddAsync(Budget budget, CancellationToken cancellationToken = default);
}
