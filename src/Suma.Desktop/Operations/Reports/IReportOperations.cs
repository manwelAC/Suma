using Suma.Application.Budgets.GetBudgetDetails;
using Suma.Application.Reports.GetAccountMovementDetail;
using Suma.Application.Reports.GetFinancialReport;
using Suma.Application.Reports.GetReportOptions;

namespace Suma.Desktop.Operations.Reports;

public interface IReportOperations
{
    Task<ReportOptions> GetOptionsAsync(CancellationToken cancellationToken = default);
    Task<FinancialReportResult> GetFinancialAsync(FinancialReportRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountMovementDetailRow>> GetAccountDetailAsync(AccountMovementDetailRequest request, CancellationToken cancellationToken = default);
    Task<BudgetDetails> GetBudgetAsync(Guid budgetId, CancellationToken cancellationToken = default);
    Task<byte[]> GetCsvAsync(ReportCsvRequest request, CancellationToken cancellationToken = default);
}

public enum ReportCsvType { CashFlow, ExpenseCategories, IncomeCategories, AccountMovement, BudgetPerformance }
public sealed record ReportCsvRequest(ReportCsvType Type, FinancialReportResult? Financial = null, IReadOnlyList<AccountMovementDetailRow>? AccountDetails = null, BudgetDetails? Budget = null);
