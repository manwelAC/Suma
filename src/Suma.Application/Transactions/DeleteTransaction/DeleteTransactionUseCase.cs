using Suma.Application.Abstractions.Persistence;
using Suma.Application.Common.Exceptions;

namespace Suma.Application.Transactions.DeleteTransaction;

public sealed class DeleteTransactionUseCase(
    ITransactionStore transactions,
    IRecurringOccurrenceStore occurrences,
    IGoalContributionStore goalContributions,
    IUnitOfWork unitOfWork)
{
    public async Task ExecuteAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        var transaction = await transactions.GetByIdAsync(transactionId, cancellationToken)
            ?? throw new NotFoundException("Transaction was not found.");

        if (await transactions.HasRefundsAsync(transaction.Id, cancellationToken))
        {
            throw new ConflictException("Cannot delete transaction because it has associated refunds. Please delete the refunds first.");
        }

        var attributedGoalMinor = await goalContributions.GetAttributedAmountMinorAsync(transaction.Id, cancellationToken);
        if (attributedGoalMinor > 0)
        {
            throw new ConflictException("Cannot delete transaction because it is linked to one or more savings goal contributions.");
        }

        var occurrence = await occurrences.GetByTransactionIdAsync(transaction.Id, cancellationToken);
        if (occurrence is not null)
        {
            occurrence.ResetToPending();
        }

        await transactions.RemoveAsync(transaction, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
