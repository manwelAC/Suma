using Suma.Application.Abstractions.Persistence;
using Suma.Application.Common.Exceptions;
using Suma.Domain.ValueObjects;

namespace Suma.Application.Reports.GetFinancialReport;

public sealed record FinancialReportRequest(string CurrencyCode, DateOnly StartDate, DateOnly EndDate);
public sealed record CashFlowSummary(long GrossIncomeMinor, long GrossExpenseMinor, long RefundMinor, long NetExpenseMinor, long NetCashFlowMinor);
public sealed record ExpenseCategorySummary(Guid CategoryId, string CategoryName, bool CategoryArchived, long GrossExpenseMinor, long RefundMinor, long NetExpenseMinor);
public sealed record IncomeCategorySummary(Guid CategoryId, string CategoryName, bool CategoryArchived, long IncomeMinor);
public sealed record AccountMovementSummary(Guid AccountId, string AccountName, bool AccountArchived, long IncomeInMinor, long RefundInMinor, long TransferInMinor, long ExpenseOutMinor, long TransferOutMinor, long TotalInflowMinor, long TotalOutflowMinor, long NetMovementMinor);
public sealed record FinancialReportResult(string CurrencyCode, DateOnly StartDate, DateOnly EndDate, CashFlowSummary CashFlow, IReadOnlyList<ExpenseCategorySummary> ExpenseCategories, IReadOnlyList<IncomeCategorySummary> IncomeCategories, IReadOnlyList<AccountMovementSummary> AccountMovements);

public sealed class GetFinancialReportUseCase(IReportStore reports, IOverviewStore overview)
{
    public async Task<FinancialReportResult> ExecuteAsync(FinancialReportRequest request, CancellationToken cancellationToken = default)
    {
        if (request.StartDate > request.EndDate) throw new ApplicationValidationException("Report start date must be on or before end date.");
        var currency = new Money(0, request.CurrencyCode).CurrencyCode;
        var currencies = await overview.GetAccountCurrencyFactsAsync(cancellationToken);
        if (!currencies.Any(item => item.CurrencyCode == currency)) throw new ApplicationValidationException("Report currency must belong to a persisted Account.");

        var categoryFacts = await reports.GetCategoryFactsAsync(currency, request.StartDate, request.EndDate, cancellationToken);
        var accountFacts = await reports.GetAccountMovementFactsAsync(currency, request.StartDate, request.EndDate, cancellationToken);
        var grossIncome = categoryFacts.Aggregate(0L, (sum, item) => checked(sum + item.IncomeMinor));
        var grossExpense = categoryFacts.Aggregate(0L, (sum, item) => checked(sum + item.GrossExpenseMinor));
        var refunds = categoryFacts.Aggregate(0L, (sum, item) => checked(sum + item.RefundMinor));
        var netExpense = checked(grossExpense - refunds);
        var netCashFlow = checked(checked(grossIncome - grossExpense) + refunds);

        var expenseCategories = categoryFacts.Where(item => item.GrossExpenseMinor != 0 || item.RefundMinor != 0)
            .Select(item => new ExpenseCategorySummary(item.CategoryId, item.CategoryName, item.CategoryArchived, item.GrossExpenseMinor, item.RefundMinor, checked(item.GrossExpenseMinor - item.RefundMinor)))
            .OrderByDescending(item => item.NetExpenseMinor).ThenBy(item => item.CategoryName, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.CategoryId).ToArray();
        var incomeCategories = categoryFacts.Where(item => item.IncomeMinor != 0)
            .Select(item => new IncomeCategorySummary(item.CategoryId, item.CategoryName, item.CategoryArchived, item.IncomeMinor))
            .OrderByDescending(item => item.IncomeMinor).ThenBy(item => item.CategoryName, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.CategoryId).ToArray();
        var movements = accountFacts.Select(item =>
        {
            var inflow = checked(checked(item.IncomeInMinor + item.RefundInMinor) + item.TransferInMinor);
            var outflow = checked(item.ExpenseOutMinor + item.TransferOutMinor);
            return new AccountMovementSummary(item.AccountId, item.AccountName, item.AccountArchived, item.IncomeInMinor, item.RefundInMinor, item.TransferInMinor, item.ExpenseOutMinor, item.TransferOutMinor, inflow, outflow, checked(inflow - outflow));
        }).OrderBy(item => item.AccountName, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.AccountId).ToArray();

        return new(currency, request.StartDate, request.EndDate, new(grossIncome, grossExpense, refunds, netExpense, netCashFlow), expenseCategories, incomeCategories, movements);
    }
}
