using Suma.Application.Abstractions.Persistence;
using Suma.Application.Common.Exceptions;

namespace Suma.Application.Savings.ArchiveSavingsGoal;

public sealed class ArchiveSavingsGoalUseCase(ISavingsGoalStore goals, IUnitOfWork unitOfWork)
{
    public async Task ExecuteAsync(Guid goalId, CancellationToken cancellationToken = default)
    {
        var goal = await goals.GetByIdAsync(goalId, cancellationToken) ?? throw new NotFoundException("Savings Goal was not found.");
        goal.Archive();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
