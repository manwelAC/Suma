using Suma.Application.Abstractions.Persistence;
using Suma.Application.Common.Exceptions;
using Suma.Domain.Budgets;
using Suma.Domain.ValueObjects;

namespace Suma.Application.Budgets.CreateBudget;

public sealed record CreateBudgetRequest(string Name, DateOnly PeriodStart, DateOnly PeriodEnd, long ExpectedIncomeMinor, string CurrencyCode);
public sealed record CreateBudgetResult(Guid Id, string Name, DateOnly PeriodStart, DateOnly PeriodEnd, long ExpectedIncomeMinor, string CurrencyCode);

public sealed class CreateBudgetUseCase(IBudgetStore budgets, IUnitOfWork unitOfWork)
{
    public async Task<CreateBudgetResult> ExecuteAsync(CreateBudgetRequest request, CancellationToken cancellationToken = default)
    {
        if (await budgets.HasActiveOverlapAsync(request.PeriodStart, request.PeriodEnd, cancellationToken: cancellationToken))
        {
            throw new ConflictException("The Budget period overlaps an active Budget.");
        }

        var budget = new Budget(request.Name, request.PeriodStart, request.PeriodEnd, new Money(request.ExpectedIncomeMinor, request.CurrencyCode));
        await budgets.AddAsync(budget, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CreateBudgetResult(budget.Id, budget.Name, budget.PeriodStart, budget.PeriodEnd, budget.ExpectedIncome.AmountMinor, budget.CurrencyCode);
    }
}
