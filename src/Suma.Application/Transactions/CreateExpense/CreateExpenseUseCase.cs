using Suma.Application.Abstractions.Persistence;
using Suma.Application.Abstractions.Time;
using Suma.Application.Common;
using Suma.Application.Common.Exceptions;
using Suma.Domain.Categories;
using Suma.Domain.Transactions;
using Suma.Domain.ValueObjects;

namespace Suma.Application.Transactions.CreateExpense;

public sealed record CreateExpenseRequest(Guid SourceAccountId, Guid CategoryId, long AmountMinor, string CurrencyCode, DateOnly TransactionDate, string? Description = null, string? Notes = null);

public sealed class CreateExpenseUseCase(IAccountStore accounts, ICategoryStore categories, ITransactionStore transactions, IUnitOfWork unitOfWork, IDateProvider dateProvider)
{
    public async Task<TransactionResult> ExecuteAsync(CreateExpenseRequest request, CancellationToken cancellationToken = default)
    {
        Validation.RequireActualTransactionDate(request.TransactionDate, dateProvider.Today);
        var account = await accounts.GetByIdAsync(request.SourceAccountId, cancellationToken)
            ?? throw new NotFoundException("Source account was not found.");
        var category = await categories.GetByIdAsync(request.CategoryId, cancellationToken)
            ?? throw new NotFoundException("Category was not found.");
        Validation.RequireActive(account, "source");
        Validation.RequireCategory(category, CategoryTransactionKind.Expense);
        var amount = new Money(request.AmountMinor, request.CurrencyCode);
        Validation.RequireCurrency(account.CurrencyCode, amount.CurrencyCode, "Transaction currency must match the source account.");
        var transaction = Transaction.CreateExpense(account.Id, category.Id, amount, request.TransactionDate, request.Description, request.Notes);
        await transactions.AddAsync(transaction, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return TransactionResult.From(transaction);
    }
}
