using Suma.Domain.Transactions;

namespace Suma.Application.Abstractions.Persistence;

public interface ITransactionStore
{
    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Transaction>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Transaction>> GetForAccountAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task<long> GetRefundedAmountMinorAsync(Guid originalTransactionId, CancellationToken cancellationToken = default);
}
