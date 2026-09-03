using System.Globalization;
using System.Text;
using Suma.Application.Budgets.GetBudgetDetails;
using Suma.Application.Reports.GetAccountMovementDetail;
using Suma.Application.Reports.GetFinancialReport;

namespace Suma.Application.Reports.Csv;

public sealed class ReportCsvSerializer
{
    private static readonly UTF8Encoding Utf8Bom = new(true);

    public byte[] CashFlow(FinancialReportResult report) => Encode(
        "StartDate,EndDate,Currency,GrossIncome,GrossIncomeMinor,GrossExpense,GrossExpenseMinor,Refunds,RefundsMinor,NetExpense,NetExpenseMinor,NetCashFlow,NetCashFlowMinor",
        [Row(Date(report.StartDate), Date(report.EndDate), report.CurrencyCode, Money(report.CashFlow.GrossIncomeMinor), Minor(report.CashFlow.GrossIncomeMinor), Money(report.CashFlow.GrossExpenseMinor), Minor(report.CashFlow.GrossExpenseMinor), Money(report.CashFlow.RefundMinor), Minor(report.CashFlow.RefundMinor), Money(report.CashFlow.NetExpenseMinor), Minor(report.CashFlow.NetExpenseMinor), Money(report.CashFlow.NetCashFlowMinor), Minor(report.CashFlow.NetCashFlowMinor))]);

    public byte[] ExpenseCategories(FinancialReportResult report) => Encode(
        "StartDate,EndDate,Category,CategoryArchived,GrossExpense,GrossExpenseMinor,Refunds,RefundsMinor,NetExpense,NetExpenseMinor,Currency",
        report.ExpenseCategories.Select(item => Row(Date(report.StartDate), Date(report.EndDate), item.CategoryName, Bool(item.CategoryArchived), Money(item.GrossExpenseMinor), Minor(item.GrossExpenseMinor), Money(item.RefundMinor), Minor(item.RefundMinor), Money(item.NetExpenseMinor), Minor(item.NetExpenseMinor), report.CurrencyCode)));

    public byte[] IncomeCategories(FinancialReportResult report) => Encode(
        "StartDate,EndDate,Category,CategoryArchived,Income,IncomeMinor,Currency",
        report.IncomeCategories.Select(item => Row(Date(report.StartDate), Date(report.EndDate), item.CategoryName, Bool(item.CategoryArchived), Money(item.IncomeMinor), Minor(item.IncomeMinor), report.CurrencyCode)));

    public byte[] AccountMovement(IReadOnlyList<AccountMovementDetailRow> rows) => Encode(
        "Date,Account,AccountArchived,Direction,Type,Counterparty,Category,Description,Amount,AmountMinor,Currency",
        rows.Select(item => Row(Date(item.TransactionDate), item.AccountName, Bool(item.AccountArchived), item.Direction.ToString(), item.Type.ToString(), item.Counterparty, item.Category, item.Description, Money(item.AmountMinor), Minor(item.AmountMinor), item.CurrencyCode)));

    public byte[] BudgetPerformance(BudgetDetails budget) => Encode(
        "Budget,PeriodStart,PeriodEnd,Category,CategoryArchived,Allocation,AllocationMinor,Spent,SpentMinor,Remaining,RemainingMinor,UtilizationPercent,Currency,ReserveFromAvailable",
        budget.Allocations.Select(item => Row(budget.Summary.Name, Date(budget.Summary.PeriodStart), Date(budget.Summary.PeriodEnd), item.CategoryName, Bool(item.CategoryArchived), Money(item.AmountMinor), Minor(item.AmountMinor), Money(item.SpentMinor), Minor(item.SpentMinor), Money(item.RemainingMinor), Minor(item.RemainingMinor), item.UtilizationPercent.ToString(CultureInfo.InvariantCulture), budget.Summary.CurrencyCode, Bool(item.ReserveFromAvailable))));

    public static string GeneralFileName(string slug, string currency, DateOnly start, DateOnly end) => $"suma-{slug}-{currency}-{start:yyyyMMdd}-{end:yyyyMMdd}.csv";
    public static string BudgetFileName(BudgetDetails budget) => $"suma-budget-performance-{budget.Summary.CurrencyCode}-{budget.Summary.PeriodStart:yyyyMMdd}-{budget.Summary.PeriodEnd:yyyyMMdd}.csv";

    private static byte[] Encode(string header, IEnumerable<string> rows)
    {
        var content = Utf8Bom.GetBytes(string.Join("\r\n", new[] { header }.Concat(rows)) + "\r\n");
        return [.. Utf8Bom.GetPreamble(), .. content];
    }
    private static string Row(params string?[] fields) => string.Join(',', fields.Select(Escape));
    private static string Escape(string? value)
    {
        value ??= string.Empty;
        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
    private static string Date(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static string Money(long value) => (value / 100m).ToString("0.00", CultureInfo.InvariantCulture);
    private static string Minor(long value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Bool(bool value) => value ? "true" : "false";
}
