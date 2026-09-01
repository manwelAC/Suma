using Suma.Application.Abstractions.Persistence;
using Suma.Application.Abstractions.Time;
using Suma.Application.Common;
using Suma.Application.Common.Exceptions;
using Suma.Domain.Transactions;
using Suma.Domain.ValueObjects;

namespace Suma.Application.Transactions.CreateTransfer;

public sealed record CreateTransferRequest(Guid SourceAccountId, Guid DestinationAccountId, long AmountMinor, string CurrencyCode, DateOnly TransactionDate, string? Description = null, string? Notes = null);

public sealed class CreateTransferUseCase(IAccountStore accounts, ITransactionStore transactions, IUnitOfWork unitOfWork, IDateProvider dateProvider)
{
    public async Task<TransactionResult> ExecuteAsync(CreateTransferRequest request, CancellationToken cancellationToken = default)
    {
        Validation.RequireActualTransactionDate(request.TransactionDate, dateProvider.Today);
        if (request.SourceAccountId == request.DestinationAccountId)
        {
            throw new ConflictException("Source and destination accounts must be different.");
        }

        var source = await accounts.GetByIdAsync(request.SourceAccountId, cancellationToken)
            ?? throw new NotFoundException("Source account was not found.");
        var destination = await accounts.GetByIdAsync(request.DestinationAccountId, cancellationToken)
            ?? throw new NotFoundException("Destination account was not found.");
        Validation.RequireActive(source, "source");
        Validation.RequireActive(destination, "destination");
        Validation.RequireCurrency(source.CurrencyCode, destination.CurrencyCode, "Transfer accounts must use the same currency.");
        var amount = new Money(request.AmountMinor, request.CurrencyCode);
        Validation.RequireCurrency(source.CurrencyCode, amount.CurrencyCode, "Transfer currency must match both accounts.");
        var transaction = Transaction.CreateTransfer(source.Id, destination.Id, amount, request.TransactionDate, request.Description, request.Notes);
        await transactions.AddAsync(transaction, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return TransactionResult.From(transaction);
    }
}
