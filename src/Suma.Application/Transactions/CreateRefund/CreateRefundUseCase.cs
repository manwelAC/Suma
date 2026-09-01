using Suma.Application.Abstractions.Persistence;
using Suma.Application.Abstractions.Time;
using Suma.Application.Common;
using Suma.Application.Common.Exceptions;
using Suma.Domain.Categories;
using Suma.Domain.Transactions;
using Suma.Domain.ValueObjects;

namespace Suma.Application.Transactions.CreateRefund;

public sealed record CreateRefundRequest(Guid OriginalTransactionId, Guid DestinationAccountId, Guid CategoryId, long AmountMinor, string CurrencyCode, DateOnly TransactionDate, string? Description = null, string? Notes = null);

public sealed class CreateRefundUseCase(IAccountStore accounts, ICategoryStore categories, ITransactionStore transactions, IUnitOfWork unitOfWork, IDateProvider dateProvider)
{
    public async Task<TransactionResult> ExecuteAsync(CreateRefundRequest request, CancellationToken cancellationToken = default)
    {
        Validation.RequireActualTransactionDate(request.TransactionDate, dateProvider.Today);
        var original = await transactions.GetByIdAsync(request.OriginalTransactionId, cancellationToken)
            ?? throw new NotFoundException("Original transaction was not found.");
        if (original.Type != TransactionType.Expense)
        {
            throw new ConflictException("Only an Expense transaction can be refunded.");
        }

        var account = await accounts.GetByIdAsync(request.DestinationAccountId, cancellationToken)
            ?? throw new NotFoundException("Destination account was not found.");
        var category = await categories.GetByIdAsync(request.CategoryId, cancellationToken)
            ?? throw new NotFoundException("Category was not found.");
        Validation.RequireActive(account, "destination");
        Validation.RequireCategory(category, CategoryTransactionKind.Expense);
        var amount = new Money(request.AmountMinor, request.CurrencyCode);
        Validation.RequireCurrency(original.Amount.CurrencyCode, account.CurrencyCode, "Destination account currency must match the original Expense.");
        Validation.RequireCurrency(original.Amount.CurrencyCode, amount.CurrencyCode, "Refund currency must match the original Expense.");
        var refunded = await transactions.GetRefundedAmountMinorAsync(original.Id, cancellationToken);
        if (refunded > original.Amount.AmountMinor - amount.AmountMinor)
        {
            throw new ConflictException("Refund amount exceeds the remaining refundable amount.");
        }

        var refund = Transaction.CreateRefund(account.Id, category.Id, original.Id, amount, request.TransactionDate, request.Description, request.Notes);
        await transactions.AddAsync(refund, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return TransactionResult.From(refund);
    }
}
