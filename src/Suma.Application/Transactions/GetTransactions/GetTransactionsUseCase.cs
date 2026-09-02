using Suma.Application.Abstractions.Persistence;
using Suma.Application.Common.Exceptions;
using Suma.Domain.Transactions;

namespace Suma.Application.Transactions.GetTransactions;

public sealed record GetTransactionsRequest(TransactionType? Type = null, int Limit = 200);

public sealed record TransactionHistoryResult(
    Guid Id,
    TransactionType Type,
    Guid? SourceAccountId,
    string? SourceAccountName,
    Guid? DestinationAccountId,
    string? DestinationAccountName,
    Guid? CategoryId,
    string? CategoryName,
    Guid? OriginalTransactionId,
    long AmountMinor,
    string CurrencyCode,
    DateOnly TransactionDate,
    string? Description,
    string? Notes);

public sealed class GetTransactionsUseCase(ITransactionStore transactions)
{
    public async Task<IReadOnlyList<TransactionHistoryResult>> ExecuteAsync(GetTransactionsRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Limit is <= 0 or > 500)
        {
            throw new ApplicationValidationException("Transaction limit must be between 1 and 500.");
        }

        var items = await transactions.GetHistoryAsync(request.Type, request.Limit, cancellationToken);
        return items.Select(item => new TransactionHistoryResult(
            item.Id,
            item.Type,
            item.SourceAccountId,
            item.SourceAccountName,
            item.DestinationAccountId,
            item.DestinationAccountName,
            item.CategoryId,
            item.CategoryName,
            item.OriginalTransactionId,
            item.AmountMinor,
            item.CurrencyCode,
            item.TransactionDate,
            item.Description,
            item.Notes)).ToArray();
    }
}
