using Microsoft.EntityFrameworkCore;
using Suma.Application.Abstractions.Persistence;
using Suma.Domain.Savings;

namespace Suma.Infrastructure.Persistence.Stores;

public sealed class SavingsGoalStore(SumaDbContext context) : ISavingsGoalStore
{
    public Task<SavingsGoal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.SavingsGoals.SingleOrDefaultAsync(goal => goal.Id == id, cancellationToken);

    public async Task AddAsync(SavingsGoal goal, CancellationToken cancellationToken = default) =>
        await context.SavingsGoals.AddAsync(goal, cancellationToken);
}
