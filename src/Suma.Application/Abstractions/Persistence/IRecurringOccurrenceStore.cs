using Suma.Domain.Recurring;

namespace Suma.Application.Abstractions.Persistence;

public interface IRecurringOccurrenceStore
{
    Task<RecurringOccurrence?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
