using Suma.Domain.Recurring;
using Suma.Domain.Transactions;

namespace Suma.Application.Abstractions.Persistence;

public interface IRecurringOccurrenceStore
{
    Task<RecurringOccurrence?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<RecurringOccurrence?> GetByTransactionIdAsync(Guid transactionId, CancellationToken cancellationToken = default);

    Task<IReadOnlySet<(Guid RecurringTransactionId, DateOnly DueDate)>> GetExistingKeysAsync(
        IReadOnlyCollection<Guid> recurringTransactionIds,
        DateOnly from,
        DateOnly through,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecurringOccurrenceRecord>> GetRecordsAsync(CancellationToken cancellationToken = default);

    Task AddRangeAsync(IReadOnlyCollection<RecurringOccurrence> occurrences, CancellationToken cancellationToken = default);
}

public sealed record RecurringOccurrenceRecord(
    Guid Id,
    Guid RecurringTransactionId,
    DateOnly DueDate,
    RecurringOccurrenceStatus Status,
    Guid? TransactionId,
    TransactionType Type,
    long AmountMinor,
    string CurrencyCode,
    string? Description,
    string? SourceAccountName,
    string? DestinationAccountName,
    string? CategoryName);
