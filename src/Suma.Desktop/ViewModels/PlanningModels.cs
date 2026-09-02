using Microsoft.UI.Xaml;

namespace Suma.Desktop.ViewModels;

public sealed record BudgetEditorInput(
    string Name,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    long ExpectedIncomeMinor,
    string CurrencyCode);

public sealed record BudgetAllocationEditorInput(
    Guid CategoryId,
    long AmountMinor,
    bool ReserveFromAvailable);

public sealed record BudgetRowViewModel(
    Guid Id,
    string Name,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string CurrencyCode,
    long ExpectedIncomeMinor,
    bool IsArchived)
{
    public string PeriodDisplay => $"{PeriodStart:MMM d, yyyy} – {PeriodEnd:MMM d, yyyy}";

    public string ExpectedIncomeDisplay => $"Expected income: {MoneyText.Format(ExpectedIncomeMinor, CurrencyCode)}";
}

public sealed record BudgetAllocationRowViewModel(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    bool CategoryArchived,
    long AmountMinor,
    long SpentMinor,
    long RemainingMinor,
    decimal UtilizationPercent,
    bool ReserveFromAvailable,
    string CurrencyCode)
{
    public string CategoryDisplay => CategoryArchived ? $"{CategoryName} · Archived category" : CategoryName;

    public string AllocatedDisplay => $"{MoneyText.Format(AmountMinor, CurrencyCode)} allocated";

    public string SpentDisplay => $"{MoneyText.Format(SpentMinor, CurrencyCode)} spent";

    public string RemainingDisplay => $"{MoneyText.Format(RemainingMinor, CurrencyCode)} remaining";

    public string UtilizationDisplay => $"{UtilizationPercent:0.#}% utilized";

    public string ReserveDisplay => ReserveFromAvailable
        ? "Reserved for future Available-to-Spend calculations"
        : "Not reserved from Available-to-Spend";
}
