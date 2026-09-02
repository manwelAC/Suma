using Suma.Domain.Savings;
using Suma.Domain.Transactions;

namespace Suma.Application.Abstractions.Persistence;

public interface IGoalContributionStore
{
    Task AddAsync(GoalContribution contribution, CancellationToken cancellationToken = default);

    Task<long> GetAttributedAmountMinorAsync(Guid transactionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GoalContributionHistoryRecord>> GetForGoalAsync(Guid savingsGoalId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GoalContributionCandidateFact>> GetCandidateFactsAsync(string currencyCode, CancellationToken cancellationToken = default);
}

public sealed record GoalContributionHistoryRecord(
    Guid Id, Guid TransactionId, GoalContributionType Type, long AmountMinor, string CurrencyCode,
    DateOnly TransactionDate, TransactionType TransactionType, string? Description,
    string? SourceAccountName, string? DestinationAccountName, string? CategoryName);

public sealed record GoalContributionCandidateFact(
    Guid TransactionId, DateOnly TransactionDate, TransactionType TransactionType, string? Description,
    string? SourceAccountName, string? DestinationAccountName, string? CategoryName,
    long TransactionAmountMinor, string CurrencyCode, long AttributedAmountMinor);
