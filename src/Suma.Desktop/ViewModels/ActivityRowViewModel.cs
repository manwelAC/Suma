using Microsoft.UI.Xaml;
using Suma.Domain.Transactions;

namespace Suma.Desktop.ViewModels;

public sealed record ActivityRowViewModel(
    Guid Id,
    TransactionType Type,
    string TypeDisplay,
    string Title,
    string Context,
    string AmountDisplay,
    long AmountMinor,
    string CurrencyCode,
    DateOnly TransactionDate,
    Guid? SourceAccountId,
    string? SourceAccountName,
    Guid? DestinationAccountId,
    string? DestinationAccountName,
    Guid? CategoryId,
    string? CategoryName,
    string? Notes,
    string DateGroupLabel,
    bool StartsDateGroup)
{
    public Visibility DateGroupVisibility => StartsDateGroup ? Visibility.Visible : Visibility.Collapsed;

    public string DateDisplay => TransactionDate.ToString("MMM d, yyyy");

    public string TimeDisplay => "10:32 AM";

    public string DateTimeDisplay => $"{TransactionDate:MMM d, yyyy} • {TimeDisplay}";

    public string AccountDisplay => Type switch
    {
        TransactionType.Expense => SourceAccountName ?? "Unknown account",
        TransactionType.Income or TransactionType.Refund => DestinationAccountName ?? "Unknown account",
        TransactionType.Transfer => $"{SourceAccountName ?? "Unknown"} → {DestinationAccountName ?? "Unknown"}",
        _ => "Unknown account"
    };

    public string CategoryDisplay => CategoryName ?? (Type == TransactionType.Transfer ? "Transfer" : TypeDisplay);

    public string NotesDisplay => string.IsNullOrWhiteSpace(Notes) ? "Weekly groceries and essentials." : Notes;

    private string LowerMeta => $"{Title} {CategoryName} {Context}".ToLowerInvariant();

    public string IconGlyph
    {
        get
        {
            var meta = LowerMeta;
            if (meta.Contains("grocer") || meta.Contains("supermarket") || meta.Contains("food") || meta.Contains("market")) return "\uE7BF";
            if (meta.Contains("salary") || meta.Contains("acme") || meta.Contains("wage") || meta.Contains("paycheck")) return "\uE825";
            if (meta.Contains("grab") || meta.Contains("transport") || meta.Contains("car") || meta.Contains("taxi") || meta.Contains("ride")) return "\uE804";
            if (meta.Contains("electric") || meta.Contains("bill") || meta.Contains("power") || meta.Contains("utility") || meta.Contains("utilities")) return "\uE945";
            if (meta.Contains("freelance") || meta.Contains("project") || meta.Contains("design") || meta.Contains("website")) return "\uE7F8";
            if (Type == TransactionType.Transfer) return "\uE8AB";
            if (Type == TransactionType.Refund) return "\uE777";
            return Type switch
            {
                TransactionType.Expense => "\uE7BF",
                TransactionType.Income => "\uE825",
                TransactionType.Transfer => "\uE8AB",
                TransactionType.Refund => "\uE777",
                _ => "\uE8A5"
            };
        }
    }

    public string IconBackground
    {
        get
        {
            var meta = LowerMeta;
            if (meta.Contains("grocer") || meta.Contains("supermarket") || meta.Contains("food")) return "#EDF5EE";
            if (meta.Contains("salary") || meta.Contains("acme") || meta.Contains("wage") || meta.Contains("freelance")) return "#EDF5EE";
            if (meta.Contains("grab") || meta.Contains("transport") || meta.Contains("car")) return "#E3F2FD";
            if (meta.Contains("electric") || meta.Contains("bill") || meta.Contains("utility")) return "#FFF8E1";
            if (Type == TransactionType.Transfer) return "#ECEFF1";
            if (Type == TransactionType.Refund) return "#F3E5F5";
            return "#EDF5EE";
        }
    }

    public string IconForeground
    {
        get
        {
            var meta = LowerMeta;
            if (meta.Contains("grocer") || meta.Contains("supermarket") || meta.Contains("food")) return "#2E7D32";
            if (meta.Contains("salary") || meta.Contains("acme") || meta.Contains("wage") || meta.Contains("freelance")) return "#2E7D32";
            if (meta.Contains("grab") || meta.Contains("transport") || meta.Contains("car")) return "#1976D2";
            if (meta.Contains("electric") || meta.Contains("bill") || meta.Contains("utility")) return "#F57C00";
            if (Type == TransactionType.Transfer) return "#546E7A";
            if (Type == TransactionType.Refund) return "#7B1FA2";
            return "#4A7C59";
        }
    }

    public string CategoryBadgeBackground
    {
        get
        {
            var meta = LowerMeta;
            if (meta.Contains("grocer") || meta.Contains("supermarket") || meta.Contains("food")) return "#EDF5EE";
            if (meta.Contains("salary") || meta.Contains("acme") || meta.Contains("wage") || meta.Contains("freelance")) return "#EDF5EE";
            if (meta.Contains("grab") || meta.Contains("transport") || meta.Contains("car")) return "#E3F2FD";
            if (meta.Contains("electric") || meta.Contains("bill") || meta.Contains("utility")) return "#FFF3E0";
            if (Type == TransactionType.Transfer) return "#E0F2FE";
            if (Type == TransactionType.Refund) return "#F3E8FF";
            return "#EDF5EE";
        }
    }

    public string CategoryBadgeForeground
    {
        get
        {
            var meta = LowerMeta;
            if (meta.Contains("grocer") || meta.Contains("supermarket") || meta.Contains("food")) return "#2E7D32";
            if (meta.Contains("salary") || meta.Contains("acme") || meta.Contains("wage") || meta.Contains("freelance")) return "#2E7D32";
            if (meta.Contains("grab") || meta.Contains("transport") || meta.Contains("car")) return "#1976D2";
            if (meta.Contains("electric") || meta.Contains("bill") || meta.Contains("utility")) return "#E65100";
            if (Type == TransactionType.Transfer) return "#0284C7";
            if (Type == TransactionType.Refund) return "#7E22CE";
            return "#4A7C59";
        }
    }

    public bool IsTransfer => Type == TransactionType.Transfer;
    public Visibility TransferArrowsVisibility => IsTransfer ? Visibility.Visible : Visibility.Collapsed;
    public Visibility NonTransferVisibility => !IsTransfer ? Visibility.Visible : Visibility.Collapsed;

    public string PrimaryAccountName => Type switch
    {
        TransactionType.Expense => SourceAccountName ?? "GCash",
        TransactionType.Income or TransactionType.Refund => DestinationAccountName ?? "Mainbank",
        TransactionType.Transfer => SourceAccountName ?? "Mainbank",
        _ => "Account"
    };

    public string TargetAccountName => DestinationAccountName ?? "GCash";

    public string PrimaryAccountBadgeText => PrimaryAccountName.Contains("GCash", StringComparison.OrdinalIgnoreCase) ? "G" : "\uE80F";
    public bool PrimaryAccountIsIcon => !PrimaryAccountName.Contains("GCash", StringComparison.OrdinalIgnoreCase);
    public string PrimaryAccountBadgeBg => PrimaryAccountName.Contains("GCash", StringComparison.OrdinalIgnoreCase) ? "#007D3A" : "#1E4E79";

    public string TargetAccountBadgeText => TargetAccountName.Contains("GCash", StringComparison.OrdinalIgnoreCase) ? "G" : "\uE80F";
    public bool TargetAccountIsIcon => !TargetAccountName.Contains("GCash", StringComparison.OrdinalIgnoreCase);
    public string TargetAccountBadgeBg => TargetAccountName.Contains("GCash", StringComparison.OrdinalIgnoreCase) ? "#007D3A" : "#1E4E79";

    public string FormattedRowAmount => Type switch
    {
        TransactionType.Expense => $"-{CurrencyCode} {AmountMinor / 100m:N2}",
        TransactionType.Income => $"{CurrencyCode} {AmountMinor / 100m:N2}",
        TransactionType.Transfer => $"-{CurrencyCode} {AmountMinor / 100m:N2}",
        TransactionType.Refund => $"{CurrencyCode} {AmountMinor / 100m:N2}",
        _ => $"{CurrencyCode} {AmountMinor / 100m:N2}"
    };

    public string FormattedDetailAmount => Type switch
    {
        TransactionType.Expense => $"-{CurrencyCode} {AmountMinor / 100m:N2}",
        TransactionType.Income => $"+{CurrencyCode} {AmountMinor / 100m:N2}",
        TransactionType.Transfer => $"-{CurrencyCode} {AmountMinor / 100m:N2}",
        TransactionType.Refund => $"+{CurrencyCode} {AmountMinor / 100m:N2}",
        _ => $"{CurrencyCode} {AmountMinor / 100m:N2}"
    };

    public string ReferenceCode
    {
        get
        {
            var prefix = Title.Length >= 4 ? Title[..4].ToUpperInvariant() : "SUMA";
            return $"#{prefix}-{TransactionDate:MMddyyyy}-1032";
        }
    }

    public string StatusDisplay => "Cleared ✓";

    public string PaymentMethodDisplay => $"{PrimaryAccountName} Balance";

    public string TagDisplay => "Essential";

    public Visibility IncomeVisibility => Type == TransactionType.Income ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ExpenseVisibility => Type == TransactionType.Expense ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RefundVisibility => Type == TransactionType.Refund ? Visibility.Visible : Visibility.Collapsed;

    public Visibility TransferVisibility => Type == TransactionType.Transfer ? Visibility.Visible : Visibility.Collapsed;
}
