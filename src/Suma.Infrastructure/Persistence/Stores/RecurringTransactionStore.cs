using Microsoft.EntityFrameworkCore;
using Suma.Application.Abstractions.Persistence;
using Suma.Domain.Recurring;

namespace Suma.Infrastructure.Persistence.Stores;

public sealed class RecurringTransactionStore(SumaDbContext context) : IRecurringTransactionStore
{
    public Task<RecurringTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.RecurringTransactions.SingleOrDefaultAsync(recurring => recurring.Id == id, cancellationToken);

    public async Task<IReadOnlyList<RecurringTransaction>> GetActiveAsync(CancellationToken cancellationToken = default) =>
        await context.RecurringTransactions.AsNoTracking().Where(recurring => recurring.IsActive).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RecurringScheduleRecord>> GetSchedulesAsync(CancellationToken cancellationToken = default)
    {
        var query =
            from recurring in context.RecurringTransactions.AsNoTracking()
            join source in context.Accounts.AsNoTracking() on recurring.SourceAccountId equals (Guid?)source.Id into sources
            from source in sources.DefaultIfEmpty()
            join destination in context.Accounts.AsNoTracking() on recurring.DestinationAccountId equals (Guid?)destination.Id into destinations
            from destination in destinations.DefaultIfEmpty()
            join category in context.Categories.AsNoTracking() on recurring.CategoryId equals (Guid?)category.Id into categories
            from category in categories.DefaultIfEmpty()
            orderby recurring.StartDate, recurring.Id
            select new RecurringScheduleRecord(
                recurring.Id, recurring.Type, recurring.SourceAccountId, source == null ? null : source.Name,
                recurring.DestinationAccountId, destination == null ? null : destination.Name,
                recurring.CategoryId, category == null ? null : category.Name,
                recurring.Amount.AmountMinor, recurring.Amount.CurrencyCode, recurring.FrequencyUnit,
                recurring.IntervalCount, recurring.DayOfWeek, recurring.DayOfMonth, recurring.MonthOfYear,
                recurring.StartDate, recurring.EndDate, recurring.Description, recurring.IsActive);
        return await query.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(RecurringTransaction recurringTransaction, CancellationToken cancellationToken = default) =>
        await context.RecurringTransactions.AddAsync(recurringTransaction, cancellationToken);
}
