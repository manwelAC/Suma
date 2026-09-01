using Suma.Application.Abstractions.Persistence;
using Suma.Application.Common;
using Suma.Application.Common.Exceptions;
using Suma.Domain.Savings;
using Suma.Domain.ValueObjects;

namespace Suma.Application.Savings.CreateSavingsGoal;

public sealed record CreateSavingsGoalRequest(string Name, long TargetAmountMinor, string CurrencyCode, DateOnly? TargetDate = null, Guid? DestinationAccountId = null);
public sealed record CreateSavingsGoalResult(Guid Id, string Name, long TargetAmountMinor, string CurrencyCode, DateOnly? TargetDate, Guid? DestinationAccountId);

public sealed class CreateSavingsGoalUseCase(IAccountStore accounts, ISavingsGoalStore goals, IUnitOfWork unitOfWork)
{
    public async Task<CreateSavingsGoalResult> ExecuteAsync(CreateSavingsGoalRequest request, CancellationToken cancellationToken = default)
    {
        var amount = new Money(request.TargetAmountMinor, request.CurrencyCode);
        if (request.DestinationAccountId.HasValue)
        {
            var account = await accounts.GetByIdAsync(request.DestinationAccountId.Value, cancellationToken)
                ?? throw new NotFoundException("Destination account was not found.");
            Validation.RequireActive(account, "destination");
            Validation.RequireCurrency(account.CurrencyCode, amount.CurrencyCode, "Goal currency must match the destination account.");
        }

        var goal = new SavingsGoal(request.Name, amount, request.TargetDate, request.DestinationAccountId);
        await goals.AddAsync(goal, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CreateSavingsGoalResult(goal.Id, goal.Name, goal.TargetAmount.AmountMinor, goal.CurrencyCode, goal.TargetDate, goal.DestinationAccountId);
    }
}
