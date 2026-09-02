using Microsoft.Extensions.DependencyInjection;
using Suma.Application.Savings.AddGoalContribution;
using Suma.Application.Savings.ArchiveSavingsGoal;
using Suma.Application.Savings.CreateSavingsGoal;
using Suma.Application.Savings.GetGoalContributionCandidates;
using Suma.Application.Savings.GetSavingsGoalDetails;
using Suma.Application.Savings.GetSavingsGoals;
using Suma.Application.Savings.RestoreSavingsGoal;

namespace Suma.Desktop.Operations.Savings;

public sealed class SavingsOperations(IServiceScopeFactory scopeFactory) : ISavingsOperations
{
    public async Task<IReadOnlyList<SavingsGoalSummary>> GetGoalsAsync(bool archived, CancellationToken cancellationToken = default) { await using var scope = scopeFactory.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<GetSavingsGoalsUseCase>().ExecuteAsync(archived, cancellationToken); }
    public async Task<SavingsGoalDetails> GetDetailsAsync(Guid goalId, CancellationToken cancellationToken = default) { await using var scope = scopeFactory.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<GetSavingsGoalDetailsUseCase>().ExecuteAsync(goalId, cancellationToken); }
    public async Task<IReadOnlyList<GoalContributionCandidate>> GetCandidatesAsync(Guid goalId, CancellationToken cancellationToken = default) { await using var scope = scopeFactory.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<GetGoalContributionCandidatesUseCase>().ExecuteAsync(goalId, cancellationToken); }
    public async Task<CreateSavingsGoalResult> CreateAsync(CreateSavingsGoalRequest request, CancellationToken cancellationToken = default) { await using var scope = scopeFactory.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<CreateSavingsGoalUseCase>().ExecuteAsync(request, cancellationToken); }
    public async Task<AddGoalContributionResult> AddContributionAsync(AddGoalContributionRequest request, CancellationToken cancellationToken = default) { await using var scope = scopeFactory.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<AddGoalContributionUseCase>().ExecuteAsync(request, cancellationToken); }
    public async Task ArchiveAsync(Guid goalId, CancellationToken cancellationToken = default) { await using var scope = scopeFactory.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<ArchiveSavingsGoalUseCase>().ExecuteAsync(goalId, cancellationToken); }
    public async Task RestoreAsync(Guid goalId, CancellationToken cancellationToken = default) { await using var scope = scopeFactory.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<RestoreSavingsGoalUseCase>().ExecuteAsync(goalId, cancellationToken); }
}
