using Suma.Domain.Savings;

namespace Suma.Application.Abstractions.Persistence;

public interface IGoalContributionStore
{
    Task AddAsync(GoalContribution contribution, CancellationToken cancellationToken = default);

    Task<long> GetAttributedAmountMinorAsync(Guid transactionId, CancellationToken cancellationToken = default);
}
