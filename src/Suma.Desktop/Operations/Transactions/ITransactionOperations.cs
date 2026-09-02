using Suma.Application.Transactions;
using Suma.Application.Transactions.CreateExpense;
using Suma.Application.Transactions.CreateIncome;
using Suma.Application.Transactions.CreateRefund;
using Suma.Application.Transactions.CreateTransfer;
using Suma.Application.Transactions.GetRefundableExpenses;
using Suma.Application.Transactions.GetTransactions;

namespace Suma.Desktop.Operations.Transactions;

public interface ITransactionOperations
{
    Task<IReadOnlyList<TransactionHistoryResult>> GetAsync(GetTransactionsRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RefundableExpenseResult>> GetRefundableExpensesAsync(CancellationToken cancellationToken = default);

    Task<TransactionResult> CreateExpenseAsync(CreateExpenseRequest request, CancellationToken cancellationToken = default);

    Task<TransactionResult> CreateIncomeAsync(CreateIncomeRequest request, CancellationToken cancellationToken = default);

    Task<TransactionResult> CreateTransferAsync(CreateTransferRequest request, CancellationToken cancellationToken = default);

    Task<TransactionResult> CreateRefundAsync(CreateRefundRequest request, CancellationToken cancellationToken = default);
}
