using Suma.Application.Abstractions.Persistence;
using Suma.Application.Common;
using Suma.Application.Common.Exceptions;
using Suma.Domain.Budgets;
using Suma.Domain.Categories;
using Suma.Domain.ValueObjects;

namespace Suma.Application.Budgets.AddBudgetAllocation;

public sealed record AddBudgetAllocationRequest(Guid BudgetId, Guid CategoryId, long AmountMinor, string CurrencyCode, bool ReserveFromAvailable);
public sealed record AddBudgetAllocationResult(Guid Id, Guid BudgetId, Guid CategoryId, long AmountMinor, string CurrencyCode, bool ReserveFromAvailable);

public sealed class AddBudgetAllocationUseCase(IBudgetStore budgets, ICategoryStore categories, IBudgetAllocationStore allocations, IUnitOfWork unitOfWork)
{
    public async Task<AddBudgetAllocationResult> ExecuteAsync(AddBudgetAllocationRequest request, CancellationToken cancellationToken = default)
    {
        var budget = await budgets.GetByIdAsync(request.BudgetId, cancellationToken)
            ?? throw new NotFoundException("Budget was not found.");
        if (budget.IsArchived)
        {
            throw new ConflictException("The Budget is archived.");
        }

        var category = await categories.GetByIdAsync(request.CategoryId, cancellationToken)
            ?? throw new NotFoundException("Category was not found.");
        Validation.RequireCategory(category, CategoryTransactionKind.Expense);
        var amount = new Money(request.AmountMinor, request.CurrencyCode);
        Validation.RequireCurrency(budget.CurrencyCode, amount.CurrencyCode, "Allocation currency must match the Budget.");
        if (await allocations.ExistsAsync(budget.Id, category.Id, cancellationToken))
        {
            throw new ConflictException("An allocation already exists for this Budget and Category.");
        }

        var allocation = new BudgetAllocation(budget.Id, category.Id, amount, request.ReserveFromAvailable);
        await allocations.AddAsync(allocation, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new AddBudgetAllocationResult(allocation.Id, allocation.BudgetId, allocation.CategoryId, allocation.Amount.AmountMinor, allocation.CurrencyCode, allocation.ReserveFromAvailable);
    }
}
