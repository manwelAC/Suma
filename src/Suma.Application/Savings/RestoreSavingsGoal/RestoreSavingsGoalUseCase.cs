using Suma.Application.Abstractions.Persistence;
using Suma.Application.Common.Exceptions;

namespace Suma.Application.Savings.RestoreSavingsGoal;

public sealed class RestoreSavingsGoalUseCase(ISavingsGoalStore goals, IUnitOfWork unitOfWork)
{
    public async Task ExecuteAsync(Guid goalId, CancellationToken cancellationToken = default)
    {
        var goal = await goals.GetByIdAsync(goalId, cancellationToken) ?? throw new NotFoundException("Savings Goal was not found.");
        goal.Restore();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
