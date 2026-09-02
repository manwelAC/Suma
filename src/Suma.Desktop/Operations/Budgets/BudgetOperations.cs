using Microsoft.Extensions.DependencyInjection;
using Suma.Application.Budgets.AddBudgetAllocation;
using Suma.Application.Budgets.ArchiveBudget;
using Suma.Application.Budgets.CreateBudget;
using Suma.Application.Budgets.GetBudgetDetails;
using Suma.Application.Budgets.GetBudgets;
using Suma.Application.Budgets.RestoreBudget;

namespace Suma.Desktop.Operations.Budgets;

public sealed class BudgetOperations(IServiceScopeFactory scopeFactory) : IBudgetOperations
{
    public async Task<IReadOnlyList<BudgetSummary>> GetAsync(bool archived, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<GetBudgetsUseCase>()
            .ExecuteAsync(archived, cancellationToken);
    }

    public async Task<BudgetDetails> GetDetailsAsync(Guid budgetId, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<GetBudgetDetailsUseCase>()
            .ExecuteAsync(budgetId, cancellationToken);
    }

    public async Task<CreateBudgetResult> CreateAsync(CreateBudgetRequest request, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CreateBudgetUseCase>()
            .ExecuteAsync(request, cancellationToken);
    }

    public async Task<AddBudgetAllocationResult> AddAllocationAsync(AddBudgetAllocationRequest request, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AddBudgetAllocationUseCase>()
            .ExecuteAsync(request, cancellationToken);
    }

    public async Task ArchiveAsync(Guid budgetId, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ArchiveBudgetUseCase>()
            .ExecuteAsync(budgetId, cancellationToken);
    }

    public async Task RestoreAsync(Guid budgetId, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<RestoreBudgetUseCase>()
            .ExecuteAsync(budgetId, cancellationToken);
    }
}
