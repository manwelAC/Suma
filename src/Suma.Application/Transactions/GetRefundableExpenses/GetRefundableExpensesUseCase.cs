using Suma.Application.Abstractions.Persistence;
using Suma.Application.Common.Exceptions;

namespace Suma.Application.Transactions.GetRefundableExpenses;

public sealed record RefundableExpenseResult(
    Guid Id,
    Guid SourceAccountId,
    string SourceAccountName,
    Guid CategoryId,
    string CategoryName,
    long OriginalAmountMinor,
    long RemainingAmountMinor,
    string CurrencyCode,
    DateOnly TransactionDate,
    string? Description);

public sealed class GetRefundableExpensesUseCase(ITransactionStore transactions)
{
    public async Task<IReadOnlyList<RefundableExpenseResult>> ExecuteAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        if (limit is <= 0 or > 500)
        {
            throw new ApplicationValidationException("Refundable expense limit must be between 1 and 500.");
        }

        var items = await transactions.GetRefundableExpensesAsync(limit, cancellationToken);
        return items.Select(item => new RefundableExpenseResult(
            item.Id,
            item.SourceAccountId,
            item.SourceAccountName,
            item.CategoryId,
            item.CategoryName,
            item.AmountMinor,
            checked(item.AmountMinor - item.RefundedAmountMinor),
            item.CurrencyCode,
            item.TransactionDate,
            item.Description)).ToArray();
    }
}
