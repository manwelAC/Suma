using Microsoft.Extensions.DependencyInjection;
using Suma.Application.Transactions;
using Suma.Application.Transactions.CreateExpense;
using Suma.Application.Transactions.CreateIncome;
using Suma.Application.Transactions.CreateRefund;
using Suma.Application.Transactions.CreateTransfer;
using Suma.Application.Transactions.DeleteTransaction;
using Suma.Application.Transactions.GetRefundableExpenses;
using Suma.Application.Transactions.GetTransactions;

namespace Suma.Desktop.Operations.Transactions;

public sealed class TransactionOperations(IServiceScopeFactory scopeFactory) : ITransactionOperations
{
    public async Task<IReadOnlyList<TransactionHistoryResult>> GetAsync(GetTransactionsRequest request, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<GetTransactionsUseCase>().ExecuteAsync(request, cancellationToken);
    }

    public async Task<IReadOnlyList<RefundableExpenseResult>> GetRefundableExpensesAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<GetRefundableExpensesUseCase>().ExecuteAsync(cancellationToken: cancellationToken);
    }

    public async Task<TransactionResult> CreateExpenseAsync(CreateExpenseRequest request, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CreateExpenseUseCase>().ExecuteAsync(request, cancellationToken);
    }

    public async Task<TransactionResult> CreateIncomeAsync(CreateIncomeRequest request, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CreateIncomeUseCase>().ExecuteAsync(request, cancellationToken);
    }

    public async Task<TransactionResult> CreateTransferAsync(CreateTransferRequest request, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CreateTransferUseCase>().ExecuteAsync(request, cancellationToken);
    }

    public async Task<TransactionResult> CreateRefundAsync(CreateRefundRequest request, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CreateRefundUseCase>().ExecuteAsync(request, cancellationToken);
    }

    public async Task DeleteAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DeleteTransactionUseCase>().ExecuteAsync(transactionId, cancellationToken);
    }
}
