using Microsoft.UI.Xaml;
using Suma.Application.Abstractions.Persistence;
using Suma.Domain.Recurring;
using Suma.Domain.Transactions;

namespace Suma.Desktop.ViewModels;

public sealed record RecurringScheduleRowViewModel(RecurringScheduleRecord Value)
{
    public string Title => string.IsNullOrWhiteSpace(Value.Description) ? $"Recurring {Value.Type}" : Value.Description;
    public string AmountDisplay => MoneyText.Format(Value.AmountMinor, Value.CurrencyCode);
    public string PatternDisplay => $"Every {Value.IntervalCount} {Value.FrequencyUnit.ToString().ToLowerInvariant()}{(Value.IntervalCount == 1 ? string.Empty : "s")}";
    public string ContextDisplay => Value.Type switch
    {
        TransactionType.Expense => $"{Value.CategoryName} • {Value.SourceAccountName}",
        TransactionType.Income => $"{Value.CategoryName} • {Value.DestinationAccountName}",
        TransactionType.Transfer => $"{Value.SourceAccountName} → {Value.DestinationAccountName}",
        _ => string.Empty
    };
    public string TypeDisplay => Value.Type.ToString();
}

public sealed record RecurringOccurrenceRowViewModel(RecurringOccurrenceRecord Value, DateOnly Today)
{
    public Guid Id => Value.Id;
    public string Title => string.IsNullOrWhiteSpace(Value.Description) ? Value.Type.ToString() : Value.Description;
    public string DueDisplay => Value.DueDate.ToString("MMM d, yyyy");
    public string AmountDisplay => MoneyText.Format(Value.AmountMinor, Value.CurrencyCode);
    public string StatusDisplay => Value.Status.ToString();
    public string ContextDisplay => Value.Type switch
    {
        TransactionType.Expense => $"Expense • {Value.CategoryName} • {Value.SourceAccountName}",
        TransactionType.Income => $"Income • {Value.CategoryName} • {Value.DestinationAccountName}",
        TransactionType.Transfer => $"Transfer • {Value.SourceAccountName} → {Value.DestinationAccountName}",
        _ => string.Empty
    };
    public string TransactionDisplay => Value.TransactionId.HasValue ? $"Transaction {Value.TransactionId}" : string.Empty;
    public Visibility TransactionVisibility => Value.TransactionId.HasValue ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PendingVisibility => Value.Status == RecurringOccurrenceStatus.Pending ? Visibility.Visible : Visibility.Collapsed;
    public bool CanMarkPaid => Value.Status == RecurringOccurrenceStatus.Pending && Value.DueDate <= Today;
    public string CategoryDisplay => Value.CategoryName ?? Value.Type.ToString();
    public string DueLabel => $"Due {Value.DueDate:MMM d, yyyy}";

    public string Glyph => Title.ToLowerInvariant() switch
    {
        var t when t.Contains("spot") || t.Contains("music") => "\uEC4F",
        var t when t.Contains("pldt") || t.Contains("fiber") || t.Contains("wifi") || t.Contains("net") => "\uE81D",
        var t when t.Contains("card") || t.Contains("bank") || t.Contains("loan") => "\uE8C7",
        var t when t.Contains("netf") || t.Contains("movie") || t.Contains("tv") => "\uE7B5",
        var t when t.Contains("phone") || t.Contains("mob") || t.Contains("post") => "\uE8EA",
        _ => "\uE823"
    };

    public string IconBackgroundHex => Title.ToLowerInvariant() switch
    {
        var t when t.Contains("spot") || t.Contains("music") => "#1DB954",
        var t when t.Contains("pldt") || t.Contains("fiber") || t.Contains("wifi") => "#FDEEEC",
        var t when t.Contains("card") || t.Contains("bank") => "#1E293B",
        var t when t.Contains("netf") => "#000000",
        var t when t.Contains("phone") || t.Contains("mob") => "#EDF5EE",
        _ => "#EDF5EE"
    };

    public string IconForegroundHex => Title.ToLowerInvariant() switch
    {
        var t when t.Contains("spot") || t.Contains("music") => "#FFFFFF",
        var t when t.Contains("pldt") || t.Contains("fiber") => "#D32F2F",
        var t when t.Contains("card") || t.Contains("bank") => "#FFFFFF",
        var t when t.Contains("netf") => "#E50914",
        var t when t.Contains("phone") || t.Contains("mob") => "#16A34A",
        _ => "#2E7D32"
    };

    public Microsoft.UI.Xaml.Media.Brush IconBackground => HexBrush(IconBackgroundHex);
    public Microsoft.UI.Xaml.Media.Brush IconForeground => HexBrush(IconForegroundHex);

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

public sealed record RecurringAccountOption(Guid Id, string Name, string CurrencyCode)
{
    public string Display => $"{Name} • {CurrencyCode}";
}

public sealed record RecurringCategoryOption(Guid Id, string Name)
{
    public string Display => Name;
}
