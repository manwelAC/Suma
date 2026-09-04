using Suma.Domain.Transactions;

namespace Suma.Application.Abstractions.Persistence;

public interface ITransactionStore
{
    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default);

    Task RemoveAsync(Transaction transaction, CancellationToken cancellationToken = default);

    Task<bool> HasRefundsAsync(Guid transactionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Transaction>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Transaction>> GetForAccountAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task<long> GetRefundedAmountMinorAsync(Guid originalTransactionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TransactionHistoryRecord>> GetHistoryAsync(TransactionType? type, int limit, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RefundableExpenseRecord>> GetRefundableExpensesAsync(int limit, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategoryNetExpenseRecord>> GetNetExpenseAmountsByCategoryAsync(
        DateOnly periodStart,
        DateOnly periodEnd,
        string currencyCode,
        IReadOnlyCollection<Guid> categoryIds,
        CancellationToken cancellationToken = default);
}

public sealed record CategoryNetExpenseRecord(Guid CategoryId, long AmountMinor);

public sealed record TransactionHistoryRecord(
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

public sealed record RefundableExpenseRecord(
    Guid Id,
    Guid SourceAccountId,
    string SourceAccountName,
    Guid CategoryId,
    string CategoryName,
    long AmountMinor,
    long RefundedAmountMinor,
    string CurrencyCode,
    DateOnly TransactionDate,
    string? Description);
