using Suma.Application.Savings.AddGoalContribution;
using Suma.Application.Savings.CreateSavingsGoal;
using Suma.Application.Savings.GetGoalContributionCandidates;
using Suma.Application.Savings.GetSavingsGoalDetails;
using Suma.Application.Savings.GetSavingsGoals;

namespace Suma.Desktop.Operations.Savings;

public interface ISavingsOperations
{
    Task<IReadOnlyList<SavingsGoalSummary>> GetGoalsAsync(bool archived, CancellationToken cancellationToken = default);
    Task<SavingsGoalDetails> GetDetailsAsync(Guid goalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GoalContributionCandidate>> GetCandidatesAsync(Guid goalId, CancellationToken cancellationToken = default);
    Task<CreateSavingsGoalResult> CreateAsync(CreateSavingsGoalRequest request, CancellationToken cancellationToken = default);
    Task<AddGoalContributionResult> AddContributionAsync(AddGoalContributionRequest request, CancellationToken cancellationToken = default);
    Task ArchiveAsync(Guid goalId, CancellationToken cancellationToken = default);
    Task RestoreAsync(Guid goalId, CancellationToken cancellationToken = default);
}
