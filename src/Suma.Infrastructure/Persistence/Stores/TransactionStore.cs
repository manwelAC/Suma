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
}
