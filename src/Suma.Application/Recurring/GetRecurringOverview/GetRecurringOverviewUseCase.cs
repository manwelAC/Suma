using Suma.Application.Abstractions.Persistence;
using Suma.Application.Abstractions.Time;
using Suma.Application.Recurring.EnsureRecurringOccurrences;

namespace Suma.Application.Recurring.GetRecurringOverview;

public sealed record RecurringOverview(DateOnly Today, IReadOnlyList<RecurringScheduleRecord> Schedules, IReadOnlyList<RecurringOccurrenceRecord> Occurrences);

public sealed class GetRecurringOverviewUseCase(EnsureRecurringOccurrencesUseCase ensureOccurrences, IRecurringTransactionStore recurringTransactions, IRecurringOccurrenceStore occurrences, IDateProvider dateProvider)
{
    public async Task<RecurringOverview> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _ = await ensureOccurrences.ExecuteAsync(cancellationToken);
        return new(dateProvider.Today, await recurringTransactions.GetSchedulesAsync(cancellationToken), await occurrences.GetRecordsAsync(cancellationToken));
    }
}
