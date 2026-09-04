using Microsoft.EntityFrameworkCore;
using Suma.Application.Abstractions.Persistence;
using Suma.Domain.Recurring;

namespace Suma.Infrastructure.Persistence.Stores;

public sealed class RecurringOccurrenceStore(SumaDbContext context) : IRecurringOccurrenceStore
{
    public Task<RecurringOccurrence?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.RecurringOccurrences.SingleOrDefaultAsync(occurrence => occurrence.Id == id, cancellationToken);

    public Task<RecurringOccurrence?> GetByTransactionIdAsync(Guid transactionId, CancellationToken cancellationToken = default) =>
        context.RecurringOccurrences.SingleOrDefaultAsync(occurrence => occurrence.TransactionId == transactionId, cancellationToken);

    public async Task<IReadOnlySet<(Guid RecurringTransactionId, DateOnly DueDate)>> GetExistingKeysAsync(
        IReadOnlyCollection<Guid> recurringTransactionIds,
        DateOnly from,
        DateOnly through,
        CancellationToken cancellationToken = default)
    {
        var ids = recurringTransactionIds.Distinct().ToArray();
        var keys = await context.RecurringOccurrences.AsNoTracking()
            .Where(item => ids.Contains(item.RecurringTransactionId) && item.DueDate >= from && item.DueDate <= through)
            .Select(item => new { item.RecurringTransactionId, item.DueDate })
            .ToListAsync(cancellationToken);
        return keys.Select(item => (item.RecurringTransactionId, item.DueDate)).ToHashSet();
    }

    public async Task<IReadOnlyList<RecurringOccurrenceRecord>> GetRecordsAsync(CancellationToken cancellationToken = default)
    {
        var query =
            from occurrence in context.RecurringOccurrences.AsNoTracking()
            join recurring in context.RecurringTransactions.AsNoTracking() on occurrence.RecurringTransactionId equals recurring.Id
            join source in context.Accounts.AsNoTracking() on recurring.SourceAccountId equals (Guid?)source.Id into sources
            from source in sources.DefaultIfEmpty()
            join destination in context.Accounts.AsNoTracking() on recurring.DestinationAccountId equals (Guid?)destination.Id into destinations
            from destination in destinations.DefaultIfEmpty()
            join category in context.Categories.AsNoTracking() on recurring.CategoryId equals (Guid?)category.Id into categories
            from category in categories.DefaultIfEmpty()
            orderby occurrence.DueDate descending, occurrence.Id
            select new RecurringOccurrenceRecord(
                occurrence.Id, occurrence.RecurringTransactionId, occurrence.DueDate, occurrence.Status,
                occurrence.TransactionId, recurring.Type, recurring.Amount.AmountMinor, recurring.Amount.CurrencyCode,
                recurring.Description, source == null ? null : source.Name,
                destination == null ? null : destination.Name, category == null ? null : category.Name);
        return await query.ToListAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IReadOnlyCollection<RecurringOccurrence> occurrences, CancellationToken cancellationToken = default) =>
        await context.RecurringOccurrences.AddRangeAsync(occurrences, cancellationToken);
}
