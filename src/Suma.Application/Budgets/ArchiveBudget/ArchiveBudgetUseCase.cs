using Suma.Application.Abstractions.Persistence;
using Suma.Application.Common.Exceptions;

namespace Suma.Application.Budgets.ArchiveBudget;

public sealed class ArchiveBudgetUseCase(IBudgetStore budgets, IUnitOfWork unitOfWork)
{
    public async Task ExecuteAsync(Guid budgetId, CancellationToken cancellationToken = default)
    {
        var budget = await budgets.GetByIdAsync(budgetId, cancellationToken)
            ?? throw new NotFoundException("Budget was not found.");
        budget.Archive();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
