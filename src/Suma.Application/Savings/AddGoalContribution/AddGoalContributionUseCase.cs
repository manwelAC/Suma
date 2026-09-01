using Suma.Application.Abstractions.Persistence;
using Suma.Application.Common;
using Suma.Application.Common.Exceptions;
using Suma.Domain.Savings;
using Suma.Domain.ValueObjects;

namespace Suma.Application.Savings.AddGoalContribution;

public sealed record AddGoalContributionRequest(Guid SavingsGoalId, Guid TransactionId, GoalContributionType Type, long AmountMinor, string CurrencyCode);
public sealed record AddGoalContributionResult(Guid Id, Guid SavingsGoalId, Guid TransactionId, GoalContributionType Type, long AmountMinor, string CurrencyCode);

public sealed class AddGoalContributionUseCase(ISavingsGoalStore goals, ITransactionStore transactions, IGoalContributionStore contributions, IUnitOfWork unitOfWork)
{
    public async Task<AddGoalContributionResult> ExecuteAsync(AddGoalContributionRequest request, CancellationToken cancellationToken = default)
    {
        var goal = await goals.GetByIdAsync(request.SavingsGoalId, cancellationToken)
            ?? throw new NotFoundException("Savings Goal was not found.");
        if (goal.IsArchived)
        {
            throw new ConflictException("The Savings Goal is archived.");
        }

        var transaction = await transactions.GetByIdAsync(request.TransactionId, cancellationToken)
            ?? throw new NotFoundException("Transaction was not found.");
        var amount = new Money(request.AmountMinor, request.CurrencyCode);
        Validation.RequireCurrency(goal.CurrencyCode, amount.CurrencyCode, "Contribution currency must match the Savings Goal.");
        Validation.RequireCurrency(transaction.Amount.CurrencyCode, amount.CurrencyCode, "Contribution currency must match the Transaction.");
        var attributed = await contributions.GetAttributedAmountMinorAsync(transaction.Id, cancellationToken);
        if (attributed > transaction.Amount.AmountMinor - amount.AmountMinor)
        {
            throw new ConflictException("Contribution attribution exceeds the Transaction amount.");
        }

        var contribution = new GoalContribution(goal.Id, transaction.Id, request.Type, amount);
        await contributions.AddAsync(contribution, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new AddGoalContributionResult(contribution.Id, contribution.SavingsGoalId, contribution.TransactionId, contribution.Type, contribution.Amount.AmountMinor, contribution.Amount.CurrencyCode);
    }
}
