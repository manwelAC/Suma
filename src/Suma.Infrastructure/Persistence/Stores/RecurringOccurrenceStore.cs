using Microsoft.EntityFrameworkCore;
using Suma.Application.Abstractions.Persistence;
using Suma.Domain.Recurring;

namespace Suma.Infrastructure.Persistence.Stores;

public sealed class RecurringOccurrenceStore(SumaDbContext context) : IRecurringOccurrenceStore
{
    public Task<RecurringOccurrence?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.RecurringOccurrences.SingleOrDefaultAsync(occurrence => occurrence.Id == id, cancellationToken);
}
