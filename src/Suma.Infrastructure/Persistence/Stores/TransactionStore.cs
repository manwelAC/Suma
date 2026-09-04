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

    public Task RemoveAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        context.Transactions.Remove(transaction);
        return Task.CompletedTask;
    }

    public async Task<bool> HasRefundsAsync(Guid transactionId, CancellationToken cancellationToken = default) =>
        await context.Transactions.AsNoTracking().AnyAsync(transaction => transaction.Type == TransactionType.Refund && transaction.OriginalTransactionId == transactionId, cancellationToken);

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

    public async Task<IReadOnlyList<CategoryNetExpenseRecord>> GetNetExpenseAmountsByCategoryAsync(
        DateOnly periodStart,
        DateOnly periodEnd,
        string currencyCode,
        IReadOnlyCollection<Guid> categoryIds,
        CancellationToken cancellationToken = default)
    {
        if (categoryIds.Count == 0)
        {
            return [];
        }

        var ids = categoryIds.Distinct().ToArray();
        var expenses = context.Transactions.AsNoTracking().Where(transaction =>
            transaction.Type == TransactionType.Expense
            && transaction.TransactionDate >= periodStart
            && transaction.TransactionDate <= periodEnd
            && transaction.Amount.CurrencyCode == currencyCode
            && transaction.CategoryId.HasValue
            && ids.Contains(transaction.CategoryId.Value));

        var expenseTotals = await expenses
            .GroupBy(expense => expense.CategoryId!.Value)
            .Select(group => new { CategoryId = group.Key, Amount = group.Sum(expense => expense.Amount.AmountMinor) })
            .ToDictionaryAsync(item => item.CategoryId, item => item.Amount, cancellationToken);

        var refundTotals = await (
            from refund in context.Transactions.AsNoTracking()
            join expense in expenses on refund.OriginalTransactionId equals (Guid?)expense.Id
            where refund.Type == TransactionType.Refund
            group refund by expense.CategoryId!.Value into grouped
            select new { CategoryId = grouped.Key, Amount = grouped.Sum(refund => refund.Amount.AmountMinor) })
            .ToDictionaryAsync(item => item.CategoryId, item => item.Amount, cancellationToken);

        return expenseTotals
            .Select(item => new CategoryNetExpenseRecord(
                item.Key,
                checked(item.Value - refundTotals.GetValueOrDefault(item.Key))))
            .ToArray();
    }
}
