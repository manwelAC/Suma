using Suma.Domain.Budgets;

namespace Suma.Application.Abstractions.Persistence;

public interface IBudgetAllocationStore
{
    Task<bool> ExistsAsync(Guid budgetId, Guid categoryId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BudgetAllocationRecord>> GetForBudgetAsync(Guid budgetId, CancellationToken cancellationToken = default);

    Task AddAsync(BudgetAllocation allocation, CancellationToken cancellationToken = default);
}

public sealed record BudgetAllocationRecord(
    Guid Id,
    Guid BudgetId,
    Guid CategoryId,
    string CategoryName,
    bool CategoryArchived,
    long AmountMinor,
    string CurrencyCode,
    bool ReserveFromAvailable);
