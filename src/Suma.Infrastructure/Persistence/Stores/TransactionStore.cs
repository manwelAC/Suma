using Microsoft.EntityFrameworkCore;
using Suma.Application.Abstractions.Persistence;
using Suma.Domain.Transactions;

namespace Suma.Infrastructure.Persistence.Stores;

public sealed class TransactionStore(SumaDbContext context) : ITransactionStore
{
    public Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Transactions.SingleOrDefaultAsync(transaction => transaction.Id == id, cancellationToken);

    public async Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default) =>
        await context.Transactions.AddAsync(transaction, cancellationToken);

    public async Task<IReadOnlyList<Transaction>> GetRecentAsync(int limit, CancellationToken cancellationToken = default) =>
        await context.Transactions.AsNoTracking().OrderByDescending(transaction => transaction.TransactionDate).ThenByDescending(transaction => transaction.Id).Take(limit).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Transaction>> GetForAccountAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        await context.Transactions.AsNoTracking().Where(transaction => transaction.SourceAccountId == accountId || transaction.DestinationAccountId == accountId).OrderBy(transaction => transaction.TransactionDate).ToListAsync(cancellationToken);

    public async Task<long> GetRefundedAmountMinorAsync(Guid originalTransactionId, CancellationToken cancellationToken = default) =>
        await context.Transactions.AsNoTracking().Where(transaction => transaction.Type == TransactionType.Refund && transaction.OriginalTransactionId == originalTransactionId).SumAsync(transaction => (long?)transaction.Amount.AmountMinor, cancellationToken) ?? 0;

    public async Task<IReadOnlyList<TransactionHistoryRecord>> GetHistoryAsync(TransactionType? type, int limit, CancellationToken cancellationToken = default)
    {
        var query =
            from transaction in context.Transactions.AsNoTracking()
            join source in context.Accounts.AsNoTracking() on transaction.SourceAccountId equals (Guid?)source.Id into sources
            from source in sources.DefaultIfEmpty()
            join destination in context.Accounts.AsNoTracking() on transaction.DestinationAccountId equals (Guid?)destination.Id into destinations
            from destination in destinations.DefaultIfEmpty()
            join category in context.Categories.AsNoTracking() on transaction.CategoryId equals (Guid?)category.Id into categories
            from category in categories.DefaultIfEmpty()
            where !type.HasValue || transaction.Type == type.Value
            orderby transaction.TransactionDate descending, transaction.Id descending
            select new TransactionHistoryRecord(
                transaction.Id,
                transaction.Type,
                transaction.SourceAccountId,
                source == null ? null : source.Name,
                transaction.DestinationAccountId,
                destination == null ? null : destination.Name,
                transaction.CategoryId,
                category == null ? null : category.Name,
                transaction.OriginalTransactionId,
                transaction.Amount.AmountMinor,
                transaction.Amount.CurrencyCode,
                transaction.TransactionDate,
                transaction.Description,
                transaction.Notes);

        return await query.Take(limit).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RefundableExpenseRecord>> GetRefundableExpensesAsync(int limit, CancellationToken cancellationToken = default)
    {
        var candidateLimit = limit >= 100 ? 500 : Math.Max(limit, 1) * 5;
        var candidates = await (
            from expense in context.Transactions.AsNoTracking()
            join source in context.Accounts.AsNoTracking() on expense.SourceAccountId equals (Guid?)source.Id
            join category in context.Categories.AsNoTracking() on expense.CategoryId equals (Guid?)category.Id
            where expense.Type == TransactionType.Expense && !category.IsArchived
            orderby expense.TransactionDate descending, expense.Id descending
            select new RefundableExpenseRecord(
                expense.Id,
                expense.SourceAccountId!.Value,
                source.Name,
                expense.CategoryId!.Value,
                category.Name,
                expense.Amount.AmountMinor,
                0,
                expense.Amount.CurrencyCode,
                expense.TransactionDate,
                expense.Description))
            .Take(candidateLimit)
            .ToListAsync(cancellationToken);

        var candidateIds = candidates.Select(candidate => candidate.Id).ToArray();
        var totals = await context.Transactions.AsNoTracking()
            .Where(refund => refund.Type == TransactionType.Refund
                && refund.OriginalTransactionId.HasValue
                && candidateIds.Contains(refund.OriginalTransactionId.Value))
            .GroupBy(refund => refund.OriginalTransactionId!.Value)
            .Select(group => new { OriginalId = group.Key, Amount = group.Sum(refund => refund.Amount.AmountMinor) })
            .ToDictionaryAsync(item => item.OriginalId, item => item.Amount, cancellationToken);

        return candidates
            .Select(candidate => candidate with { RefundedAmountMinor = totals.GetValueOrDefault(candidate.Id) })
            .Where(candidate => candidate.RefundedAmountMinor < candidate.AmountMinor)
            .Take(limit)
            .ToArray();
    }
}
