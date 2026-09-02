using Suma.Domain.Recurring;
using Suma.Domain.Transactions;

namespace Suma.Application.Abstractions.Persistence;

public interface IRecurringTransactionStore
{
    Task<RecurringTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecurringTransaction>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecurringScheduleRecord>> GetSchedulesAsync(CancellationToken cancellationToken = default);

    Task AddAsync(RecurringTransaction recurringTransaction, CancellationToken cancellationToken = default);
}

public sealed record RecurringScheduleRecord(
    Guid Id,
    TransactionType Type,
    Guid? SourceAccountId,
    string? SourceAccountName,
    Guid? DestinationAccountId,
    string? DestinationAccountName,
    Guid? CategoryId,
    string? CategoryName,
    long AmountMinor,
    string CurrencyCode,
    RecurrenceFrequencyUnit FrequencyUnit,
    int IntervalCount,
    DayOfWeek? DayOfWeek,
    int? DayOfMonth,
    int? MonthOfYear,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Description,
    bool IsActive);
