using Suma.Application.Budgets.AddBudgetAllocation;
using Suma.Application.Budgets.CreateBudget;
using Suma.Application.Budgets.GetBudgetDetails;
using Suma.Application.Budgets.GetBudgets;

namespace Suma.Desktop.Operations.Budgets;

public interface IBudgetOperations
{
    Task<IReadOnlyList<BudgetSummary>> GetAsync(bool archived, CancellationToken cancellationToken = default);

    Task<BudgetDetails> GetDetailsAsync(Guid budgetId, CancellationToken cancellationToken = default);

    Task<CreateBudgetResult> CreateAsync(CreateBudgetRequest request, CancellationToken cancellationToken = default);

    Task<AddBudgetAllocationResult> AddAllocationAsync(AddBudgetAllocationRequest request, CancellationToken cancellationToken = default);

    Task ArchiveAsync(Guid budgetId, CancellationToken cancellationToken = default);

    Task RestoreAsync(Guid budgetId, CancellationToken cancellationToken = default);
}
