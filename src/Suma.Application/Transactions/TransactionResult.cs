using Suma.Domain.Transactions;

namespace Suma.Application.Transactions;

public sealed record TransactionResult(
    Guid Id,
    TransactionType Type,
    Guid? SourceAccountId,
    Guid? DestinationAccountId,
    Guid? CategoryId,
    Guid? OriginalTransactionId,
    long AmountMinor,
    string CurrencyCode,
    DateOnly TransactionDate,
    string? Description,
    string? Notes)
{
    public static TransactionResult From(Transaction transaction) => new(
        transaction.Id,
        transaction.Type,
        transaction.SourceAccountId,
        transaction.DestinationAccountId,
        transaction.CategoryId,
        transaction.OriginalTransactionId,
        transaction.Amount.AmountMinor,
        transaction.Amount.CurrencyCode,
        transaction.TransactionDate,
        transaction.Description,
        transaction.Notes);
}
