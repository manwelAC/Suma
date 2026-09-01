using Suma.Domain.Savings;

namespace Suma.Application.Abstractions.Persistence;

public interface ISavingsGoalStore
{
    Task<SavingsGoal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(SavingsGoal goal, CancellationToken cancellationToken = default);
}
