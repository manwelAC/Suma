using Suma.Application.Abstractions.Persistence;
using Suma.Application.Abstractions.Time;
using Suma.Domain.Recurring;

namespace Suma.Application.Recurring.EnsureRecurringOccurrences;

public sealed class EnsureRecurringOccurrencesUseCase(IRecurringTransactionStore recurringTransactions, IRecurringOccurrenceStore occurrences, IUnitOfWork unitOfWork, IDateProvider dateProvider)
{
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var schedules = await recurringTransactions.GetActiveAsync(cancellationToken);
        if (schedules.Count == 0) return 0;
        var from = dateProvider.Today.AddDays(-RecurringOccurrencePolicy.OverdueDays);
        var through = dateProvider.Today.AddDays(RecurringOccurrencePolicy.UpcomingDays);
        var keys = await occurrences.GetExistingKeysAsync(schedules.Select(item => item.Id).ToArray(), from, through, cancellationToken);
        var additions = schedules.SelectMany(schedule => RecurringScheduleCalculator.GetDueDates(schedule, from, through)
            .Where(dueDate => !keys.Contains((schedule.Id, dueDate)))
            .Select(dueDate => new RecurringOccurrence(schedule.Id, dueDate))).ToArray();
        if (additions.Length == 0) return 0;
        await occurrences.AddRangeAsync(additions, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return additions.Length;
    }
}
