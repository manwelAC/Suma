using Suma.Domain.Recurring;
using Suma.Domain.Transactions;

namespace Suma.Application.Abstractions.Persistence;

public interface IOverviewStore
{
    Task<IReadOnlyList<OverviewCurrencyFact>> GetAccountCurrencyFactsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OverviewAccountBalanceFact>> GetAccountBalanceFactsAsync(
        string currencyCode,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OverviewRecurringFact>> GetUpcomingRecurringAsync(
        string currencyCode,
        DateOnly today,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OverviewActivityFact>> GetRecentActivityAsync(
        string currencyCode,
        int limit,
        CancellationToken cancellationToken = default);
}

public sealed record OverviewCurrencyFact(string CurrencyCode, bool HasActiveIncludedAccount);

public sealed record OverviewAccountBalanceFact(
    Guid AccountId,
    string Name,
    bool IsArchived,
    bool IncludeInAvailableToSpend,
    long OpeningBalanceMinor,
    long IncomeMinor,
    long RefundMinor,
    long TransferInMinor,
    long ExpenseMinor,
    long TransferOutMinor,
    string CurrencyCode);

public sealed record OverviewRecurringFact(
    Guid OccurrenceId,
    DateOnly DueDate,
    TransactionType Type,
    long AmountMinor,
    string CurrencyCode,
    string? Description);

public sealed record OverviewActivityFact(
    Guid TransactionId,
    DateOnly TransactionDate,
    TransactionType Type,
    long AmountMinor,
    string CurrencyCode,
    string? Description);
