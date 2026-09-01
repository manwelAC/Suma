using Microsoft.EntityFrameworkCore;
using Suma.Application.Abstractions.Persistence;
using Suma.Domain.Savings;

namespace Suma.Infrastructure.Persistence.Stores;

public sealed class GoalContributionStore(SumaDbContext context) : IGoalContributionStore
{
    public async Task AddAsync(GoalContribution contribution, CancellationToken cancellationToken = default) =>
        await context.GoalContributions.AddAsync(contribution, cancellationToken);

    public async Task<long> GetAttributedAmountMinorAsync(Guid transactionId, CancellationToken cancellationToken = default) =>
        await context.GoalContributions.AsNoTracking().Where(contribution => contribution.TransactionId == transactionId).SumAsync(contribution => (long?)contribution.Amount.AmountMinor, cancellationToken) ?? 0;
}
