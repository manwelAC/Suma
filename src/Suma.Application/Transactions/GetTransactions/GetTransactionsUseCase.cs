using Suma.Application.Abstractions.Persistence;
using Suma.Application.Common.Exceptions;

namespace Suma.Application.Transactions.GetTransactions;

public sealed class GetTransactionsUseCase(ITransactionStore transactions)
{
    public async Task<IReadOnlyList<TransactionResult>> ExecuteAsync(int limit, CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            throw new ApplicationValidationException("Transaction limit must be greater than zero.");
        }

        var items = await transactions.GetRecentAsync(limit, cancellationToken);
        return items.Select(TransactionResult.From).ToArray();
    }
}
