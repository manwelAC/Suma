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

    public string BudgetAmountDisplay => MoneyText.Format(AmountMinor, CurrencyCode);

    public string SpentAmountDisplay => MoneyText.Format(SpentMinor, CurrencyCode);

    public string RemainingAmountDisplay => MoneyText.Format(RemainingMinor, CurrencyCode);

    public string PercentDisplay => $"{UtilizationPercent:0.#}%";

    public double ProgressValue => (double)Math.Clamp(UtilizationPercent, 0m, 100m);

    public string Glyph => CategoryName.ToLowerInvariant() switch
    {
        var n when n.Contains("hous") || n.Contains("rent") || n.Contains("home") => "\uE80F",
        var n when n.Contains("groc") || n.Contains("food") || n.Contains("market") => "\uE7BF",
        var n when n.Contains("trans") || n.Contains("gas") || n.Contains("car") => "\uE806",
        var n when n.Contains("din") || n.Contains("rest") || n.Contains("eat") => "\uE7AD",
        var n when n.Contains("heal") || n.Contains("med") || n.Contains("care") => "\uEB51",
        var n when n.Contains("util") || n.Contains("bill") || n.Contains("elect") => "\uE81D",
        var n when n.Contains("ent") || n.Contains("play") || n.Contains("fun") => "\uEC4F",
        _ => "\uE712"
    };

    public string IconBackgroundHex => CategoryName.ToLowerInvariant() switch
    {
        var n when n.Contains("hous") || n.Contains("rent") || n.Contains("home") => "#EDF5EE",
        var n when n.Contains("groc") || n.Contains("food") || n.Contains("market") => "#E3F2FD",
        var n when n.Contains("trans") || n.Contains("gas") || n.Contains("car") => "#FEF3C7",
        var n when n.Contains("din") || n.Contains("rest") || n.Contains("eat") => "#F3E8FF",
        var n when n.Contains("heal") || n.Contains("med") || n.Contains("care") => "#FFE4E6",
        _ => "#F1F5F9"
    };

    public string IconForegroundHex => CategoryName.ToLowerInvariant() switch
    {
        var n when n.Contains("hous") || n.Contains("rent") || n.Contains("home") => "#2E7D32",
        var n when n.Contains("groc") || n.Contains("food") || n.Contains("market") => "#1976D2",
        var n when n.Contains("trans") || n.Contains("gas") || n.Contains("car") => "#D97706",
        var n when n.Contains("din") || n.Contains("rest") || n.Contains("eat") => "#7C3AED",
        var n when n.Contains("heal") || n.Contains("med") || n.Contains("care") => "#E11D48",
        _ => "#475569"
    };

    public Microsoft.UI.Xaml.Media.Brush IconBackground => HexBrush(IconBackgroundHex);
    public Microsoft.UI.Xaml.Media.Brush IconForeground => HexBrush(IconForegroundHex);
    public Microsoft.UI.Xaml.Media.Brush ProgressBrush => IconForeground;

    private static Microsoft.UI.Xaml.Media.Brush HexBrush(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
        {
            byte r = Convert.ToByte(hex[..2], 16);
            byte g = Convert.ToByte(hex[2..4], 16);
            byte b = Convert.ToByte(hex[4..6], 16);
            return new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, r, g, b));
        }
        return new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 46, 125, 50));
    }
}
