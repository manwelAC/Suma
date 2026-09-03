using Microsoft.Extensions.DependencyInjection;
using Suma.Application.Budgets.GetBudgetDetails;
using Suma.Application.Reports.Csv;
using Suma.Application.Reports.GetAccountMovementDetail;
using Suma.Application.Reports.GetFinancialReport;
using Suma.Application.Reports.GetReportOptions;

namespace Suma.Desktop.Operations.Reports;

public sealed class ReportOperations(IServiceScopeFactory scopeFactory) : IReportOperations
{
    public async Task<ReportOptions> GetOptionsAsync(CancellationToken cancellationToken = default) { await using var scope = scopeFactory.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<GetReportOptionsUseCase>().ExecuteAsync(cancellationToken); }
    public async Task<FinancialReportResult> GetFinancialAsync(FinancialReportRequest request, CancellationToken cancellationToken = default) { await using var scope = scopeFactory.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<GetFinancialReportUseCase>().ExecuteAsync(request, cancellationToken); }
    public async Task<IReadOnlyList<AccountMovementDetailRow>> GetAccountDetailAsync(AccountMovementDetailRequest request, CancellationToken cancellationToken = default) { await using var scope = scopeFactory.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<GetAccountMovementDetailUseCase>().ExecuteAsync(request, cancellationToken); }
    public async Task<BudgetDetails> GetBudgetAsync(Guid budgetId, CancellationToken cancellationToken = default) { await using var scope = scopeFactory.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<GetBudgetDetailsUseCase>().ExecuteAsync(budgetId, cancellationToken); }
    public async Task<byte[]> GetCsvAsync(ReportCsvRequest request, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope(); var csv = scope.ServiceProvider.GetRequiredService<ReportCsvSerializer>();
        return request.Type switch { ReportCsvType.CashFlow => csv.CashFlow(request.Financial!), ReportCsvType.ExpenseCategories => csv.ExpenseCategories(request.Financial!), ReportCsvType.IncomeCategories => csv.IncomeCategories(request.Financial!), ReportCsvType.AccountMovement => csv.AccountMovement(request.AccountDetails!), ReportCsvType.BudgetPerformance => csv.BudgetPerformance(request.Budget!), _ => throw new ArgumentOutOfRangeException(nameof(request)) };
    }
}
