using Suma.Application.Abstractions.Persistence;
using Suma.Application.Abstractions.Time;
using Suma.Application.Common;
using Suma.Application.Common.Exceptions;
using Suma.Domain.Categories;
using Suma.Domain.Transactions;
using Suma.Domain.ValueObjects;

namespace Suma.Application.Transactions.CreateIncome;

public sealed record CreateIncomeRequest(Guid DestinationAccountId, Guid CategoryId, long AmountMinor, string CurrencyCode, DateOnly TransactionDate, string? Description = null, string? Notes = null);

public sealed class CreateIncomeUseCase(IAccountStore accounts, ICategoryStore categories, ITransactionStore transactions, IUnitOfWork unitOfWork, IDateProvider dateProvider)
{
    public async Task<TransactionResult> ExecuteAsync(CreateIncomeRequest request, CancellationToken cancellationToken = default)
    {
        Validation.RequireActualTransactionDate(request.TransactionDate, dateProvider.Today);
        var account = await accounts.GetByIdAsync(request.DestinationAccountId, cancellationToken)
            ?? throw new NotFoundException("Destination account was not found.");
        var category = await categories.GetByIdAsync(request.CategoryId, cancellationToken)
            ?? throw new NotFoundException("Category was not found.");
        Validation.RequireActive(account, "destination");
        Validation.RequireCategory(category, CategoryTransactionKind.Income);
        var amount = new Money(request.AmountMinor, request.CurrencyCode);
        Validation.RequireCurrency(account.CurrencyCode, amount.CurrencyCode, "Transaction currency must match the destination account.");
        var transaction = Transaction.CreateIncome(account.Id, category.Id, amount, request.TransactionDate, request.Description, request.Notes);
        await transactions.AddAsync(transaction, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return TransactionResult.From(transaction);
    }
}
