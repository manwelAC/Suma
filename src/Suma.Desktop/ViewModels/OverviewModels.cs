using System;
using Suma.Application.Overview.GetOverview;
using Suma.Domain.Transactions;

namespace Suma.Desktop.ViewModels;

public sealed record OverviewAccountRow(OverviewAccountSummary Value, string CurrencyCode)
{
    public string Name => Value.Name;
    public string BalanceDisplay => MoneyText.Format(Value.BalanceMinor, CurrencyCode);
    public string StatusDisplay => $"{(Value.Included ? "Included" : "Excluded")}{(Value.IsArchived ? " • Archived" : string.Empty)}";
    public bool IsIncluded => Value.Included;
    public bool IsArchived => Value.IsArchived;
    public string Initial => string.IsNullOrWhiteSpace(Value.Name) ? "A" : Value.Name.Substring(0, 1).ToUpperInvariant();
    public string IconGlyph => Value.Included ? "\uE8C7" : "\uE80F";
}

public sealed record OverviewSavingsRow(OverviewSavingsSummary Value, string CurrencyCode)
{
    public string Name => Value.Name;
    public string ProgressDisplay => $"{MoneyText.Format(Value.ProgressMinor, CurrencyCode)} of {MoneyText.Format(Value.TargetMinor, CurrencyCode)}";
    public double ProgressPercentage => Value.TargetMinor > 0
        ? Math.Min(100.0, Math.Max(0.0, (double)Value.ProgressMinor / Value.TargetMinor * 100.0))
        : 0.0;
}

public sealed record OverviewUpcomingDisplay(OverviewUpcomingRow Value, string CurrencyCode)
{
    public string Title => Value.Description ?? Value.Type.ToString();
    public string DueDateDisplay => $"{Value.DueDate:MMM d}";
    public string AmountDisplay => MoneyText.Format(Value.AmountMinor, CurrencyCode);
    public string Detail => $"{Value.DueDate:MMM d} • {Value.Type}";
}

public sealed record OverviewActivityDisplay(OverviewActivityRow Value, string CurrencyCode)
{
    public string Title => Value.Description ?? Value.Type.ToString();
    public string DateDisplay => $"{Value.TransactionDate:MMM d}";
    public string SubtitleDisplay => $"{Value.TransactionDate:MMM d} • {Value.Type}";
    public string AmountDisplay => MoneyText.Format(Value.AmountMinor, CurrencyCode);
    public string TypeDisplay => Value.Type.ToString();
    public string IconGlyph => Value.Type switch
    {
        TransactionType.Expense => "\uE7BF",
        TransactionType.Income => "\uE8C7",
        _ => "\uE8AB"
    };
}
