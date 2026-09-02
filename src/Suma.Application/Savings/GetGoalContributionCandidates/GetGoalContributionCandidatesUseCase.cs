using Suma.Application.Abstractions.Persistence;
using Suma.Application.Common.Exceptions;

namespace Suma.Application.Savings.GetGoalContributionCandidates;

public sealed record GoalContributionCandidate(
    Guid TransactionId, DateOnly TransactionDate, Domain.Transactions.TransactionType TransactionType,
    string? Description, string? SourceAccountName, string? DestinationAccountName, string? CategoryName,
    long TransactionAmountMinor, string CurrencyCode, long AttributedAmountMinor, long RemainingCapacityMinor);

public sealed class GetGoalContributionCandidatesUseCase(ISavingsGoalStore goals, IGoalContributionStore contributions)
{
    public async Task<IReadOnlyList<GoalContributionCandidate>> ExecuteAsync(Guid goalId, CancellationToken cancellationToken = default)
    {
        var goal = await goals.GetByIdAsync(goalId, cancellationToken) ?? throw new NotFoundException("Savings Goal was not found.");
        if (goal.IsArchived) throw new ConflictException("The Savings Goal is archived.");
        return (await contributions.GetCandidateFactsAsync(goal.CurrencyCode, cancellationToken))
            .Select(item => new GoalContributionCandidate(item.TransactionId, item.TransactionDate, item.TransactionType,
                item.Description, item.SourceAccountName, item.DestinationAccountName, item.CategoryName,
                item.TransactionAmountMinor, item.CurrencyCode, item.AttributedAmountMinor,
                checked(item.TransactionAmountMinor - item.AttributedAmountMinor)))
            .Where(item => item.RemainingCapacityMinor > 0)
            .ToArray();
    }
}
