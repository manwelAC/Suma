using Suma.Application.Overview.GetOverview;

namespace Suma.Desktop.ViewModels;

public sealed record OverviewAccountRow(OverviewAccountSummary Value, string CurrencyCode)
{
    public string Name => Value.Name;
    public string BalanceDisplay => MoneyText.Format(Value.BalanceMinor, CurrencyCode);
    public string StatusDisplay => $"{(Value.Included ? "Included" : "Excluded")}{(Value.IsArchived ? " • Archived" : string.Empty)}";
}

public sealed record OverviewSavingsRow(OverviewSavingsSummary Value, string CurrencyCode)
{
    public string Name => Value.Name;
    public string ProgressDisplay => $"{MoneyText.Format(Value.ProgressMinor, CurrencyCode)} of {MoneyText.Format(Value.TargetMinor, CurrencyCode)}";
}

public sealed record OverviewUpcomingDisplay(OverviewUpcomingRow Value, string CurrencyCode)
{
    public string Title => Value.Description ?? Value.Type.ToString();
    public string Detail => $"{Value.DueDate:MMM d} • {Value.Type} • {MoneyText.Format(Value.AmountMinor, CurrencyCode)}";
}

public sealed record OverviewActivityDisplay(OverviewActivityRow Value, string CurrencyCode)
{
    public string Title => Value.Description ?? Value.Type.ToString();
    public string Detail => $"{Value.TransactionDate:MMM d} • {Value.Type} • {MoneyText.Format(Value.AmountMinor, CurrencyCode)}";
}
