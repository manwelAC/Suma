using Suma.Application.Abstractions.Persistence;
using Suma.Application.Common.Exceptions;
using Suma.Application.Savings.GetSavingsGoals;

namespace Suma.Application.Savings.GetSavingsGoalDetails;

public sealed record SavingsGoalDetails(SavingsGoalSummary Summary, IReadOnlyList<GoalContributionHistoryRecord> Contributions);

public sealed class GetSavingsGoalDetailsUseCase(ISavingsGoalStore goals, IGoalContributionStore contributions)
{
    public async Task<SavingsGoalDetails> ExecuteAsync(Guid goalId, CancellationToken cancellationToken = default)
    {
        var record = (await goals.GetRecordsAsync(false, cancellationToken)).Concat(await goals.GetRecordsAsync(true, cancellationToken)).SingleOrDefault(item => item.Id == goalId)
            ?? throw new NotFoundException("Savings Goal was not found.");
        return new(GetSavingsGoalsUseCase.ToSummary(record), await contributions.GetForGoalAsync(goalId, cancellationToken));
    }
}
