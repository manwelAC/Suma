using Suma.Application.Abstractions.Persistence;
using Suma.Application.Common.Exceptions;

namespace Suma.Application.Budgets.RestoreBudget;

public sealed class RestoreBudgetUseCase(IBudgetStore budgets, IUnitOfWork unitOfWork)
{
    public async Task ExecuteAsync(Guid budgetId, CancellationToken cancellationToken = default)
    {
        var budget = await budgets.GetByIdAsync(budgetId, cancellationToken)
            ?? throw new NotFoundException("Budget was not found.");
        if (await budgets.HasActiveOverlapAsync(
            budget.PeriodStart,
            budget.PeriodEnd,
            budget.Id,
            cancellationToken))
        {
            throw new ConflictException("Restore failed because its period overlaps another active budget.");
        }

        budget.Restore();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
