using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Suma.Desktop.ViewModels;

public enum CashFlowGranularity
{
    Daily,
    Weekly,
    Monthly
}

public enum CashFlowBreakdownMode
{
    ByCategory,
    ByAccount
}

public sealed record CashFlowTimelinePoint(
    DateOnly Date,
    string Label,
    long IncomeMinor,
    long ExpenseMinor,
    long NetMinor,
    string FormattedIncome,
    string FormattedExpense,
    string FormattedNet,
    string Tooltip);

public sealed record ReportCategoryDonutItem(
    string CategoryName,
    long AmountMinor,
    double Percentage,
    string FormattedAmount,
    string FormattedPercentage,
    string ColorHex,
    SolidColorBrush ColorBrush);

public sealed record CashFlowBreakdownItem(
    string Name,
    string IconGlyph,
    long IncomeMinor,
    long ExpenseMinor,
    long NetMinor,
    string FormattedIncome,
    string FormattedExpense,
    string FormattedNet,
    double NetMagnitudePercent,
    bool IsPositive,
    SolidColorBrush NetIndicatorBrush);

public sealed record ReportInsightItem(
    string Title,
    string Description,
    string Glyph,
    string BadgeBackgroundHex,
    string IconForegroundHex);

public sealed record PeriodComparisonSummary(
    double? GrossIncomeChangePercent,
    string GrossIncomeComparisonText,
    bool GrossIncomeIsPositive,
    double? NetExpenseChangePercent,
    string NetExpenseComparisonText,
    bool NetExpenseIsPositive,
    double? RefundsChangePercent,
    string RefundsComparisonText,
    bool RefundsIsPositive,
    double? NetCashFlowChangePercent,
    string NetCashFlowComparisonText,
    bool NetCashFlowIsPositive);
