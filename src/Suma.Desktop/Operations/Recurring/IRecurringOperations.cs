using Suma.Application.Recurring.CreateRecurringTransaction;
using Suma.Application.Recurring.GetRecurringOverview;
using Suma.Application.Transactions;

namespace Suma.Desktop.Operations.Recurring;

public interface IRecurringOperations
{
    Task<RecurringOverview> GetOverviewAsync(CancellationToken cancellationToken = default);
    Task<CreateRecurringTransactionResult> CreateExpenseAsync(CreateRecurringExpenseRequest request, CancellationToken cancellationToken = default);
    Task<CreateRecurringTransactionResult> CreateIncomeAsync(CreateRecurringIncomeRequest request, CancellationToken cancellationToken = default);
    Task<CreateRecurringTransactionResult> CreateTransferAsync(CreateRecurringTransferRequest request, CancellationToken cancellationToken = default);
    Task<TransactionResult> MarkPaidAsync(Guid occurrenceId, CancellationToken cancellationToken = default);
    Task SkipAsync(Guid occurrenceId, CancellationToken cancellationToken = default);
}
