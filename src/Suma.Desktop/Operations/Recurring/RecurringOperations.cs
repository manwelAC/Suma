using Microsoft.Extensions.DependencyInjection;
using Suma.Application.Recurring.CreateRecurringTransaction;
using Suma.Application.Recurring.GetRecurringOverview;
using Suma.Application.Recurring.MarkOccurrencePaid;
using Suma.Application.Recurring.SkipOccurrence;
using Suma.Application.Transactions;

namespace Suma.Desktop.Operations.Recurring;

public sealed class RecurringOperations(IServiceScopeFactory scopeFactory) : IRecurringOperations
{
    public async Task<RecurringOverview> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<GetRecurringOverviewUseCase>().ExecuteAsync(cancellationToken);
    }

    public async Task<CreateRecurringTransactionResult> CreateExpenseAsync(CreateRecurringExpenseRequest request, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CreateRecurringTransactionUseCase>().ExecuteExpenseAsync(request, cancellationToken);
    }

    public async Task<CreateRecurringTransactionResult> CreateIncomeAsync(CreateRecurringIncomeRequest request, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CreateRecurringTransactionUseCase>().ExecuteIncomeAsync(request, cancellationToken);
    }

    public async Task<CreateRecurringTransactionResult> CreateTransferAsync(CreateRecurringTransferRequest request, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CreateRecurringTransactionUseCase>().ExecuteTransferAsync(request, cancellationToken);
    }

    public async Task<TransactionResult> MarkPaidAsync(Guid occurrenceId, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MarkOccurrencePaidUseCase>().ExecuteAsync(occurrenceId, cancellationToken);
    }

    public async Task SkipAsync(Guid occurrenceId, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SkipOccurrenceUseCase>().ExecuteAsync(occurrenceId, cancellationToken);
    }
}
