using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Suma.Domain.Accounts;
using Windows.UI;

namespace Suma.Desktop.ViewModels;

public sealed class AccountRowViewModel : ViewModelBase
{
    private bool isSelected;
    private bool includeInAvailableToSpend;
    private int themeIndex;

    public AccountRowViewModel(
        Guid id,
        string name,
        AccountType type,
        string typeDisplay,
        long balanceMinor,
        string balanceDisplay,
        long openingBalanceMinor,
        string openingBalanceDisplay,
        string currencyCode,
        bool includeInAvailableToSpend,
        bool isArchived,
        int themeIndex = 0,
        string? accountNumber = null)
    {
        Id = id;
        Name = name;
        Type = type;
        TypeDisplay = typeDisplay;
        BalanceMinor = balanceMinor;
        BalanceDisplay = balanceDisplay;
        OpeningBalanceMinor = openingBalanceMinor;
        OpeningBalanceDisplay = openingBalanceDisplay;
        CurrencyCode = currencyCode;
        this.includeInAvailableToSpend = includeInAvailableToSpend;
        IsArchived = isArchived;
        this.themeIndex = themeIndex;
        AccountNumber = string.IsNullOrWhiteSpace(accountNumber) ? null : accountNumber.Trim();
    }

    public Guid Id { get; }

    public string Name { get; }

    public AccountType Type { get; }

    public string TypeDisplay { get; }

    public long BalanceMinor { get; }

    public string BalanceDisplay { get; }

    public long OpeningBalanceMinor { get; }

    public string OpeningBalanceDisplay { get; }

    public string CurrencyCode { get; }

    public string? AccountNumber { get; }

    public bool IsArchived { get; }

    public bool IncludeInAvailableToSpend
    {
        get => includeInAvailableToSpend;
        set
        {
            if (SetProperty(ref includeInAvailableToSpend, value))
            {
                OnPropertyChanged(nameof(AvailableToSpendDisplay));
                OnPropertyChanged(nameof(AtsBadgeText));
                OnPropertyChanged(nameof(AtsBadgeBackground));
                OnPropertyChanged(nameof(AtsBadgeBorder));
            }
        }
    }

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (SetProperty(ref isSelected, value))
            {
                OnPropertyChanged(nameof(CheckmarkVisibility));
                OnPropertyChanged(nameof(MenuVisibility));
                OnPropertyChanged(nameof(CardBorderBrush));
                OnPropertyChanged(nameof(CardBorderThickness));
            }
        }
    }

    public int ThemeIndex
    {
        get => themeIndex;
        set
        {
            if (SetProperty(ref themeIndex, value))
            {
                OnPropertyChanged(nameof(GradientBrush));
                OnPropertyChanged(nameof(GradientStartColor));
                OnPropertyChanged(nameof(GradientEndColor));
            }
        }
    }

    public string MaskedNumber
    {
        get
        {
            if (Type == AccountType.Cash && string.IsNullOrWhiteSpace(AccountNumber))
            {
                return "WALLET";
            }

            if (!string.IsNullOrWhiteSpace(AccountNumber))
            {
                var clean = AccountNumber.Trim();
                if (clean.Length > 4)
                {
                    return $"•••• {clean[^4..]}";
                }
                return $"•••• {clean}";
            }

            return $"•••• {Math.Abs(Id.GetHashCode() % 9000) + 1000}";
        }
    }

    public bool HasAccountNumber => !string.IsNullOrWhiteSpace(AccountNumber);

    public Visibility AccountNumberRowVisibility => HasAccountNumber ? Visibility.Visible : Visibility.Collapsed;

    public string AccountNumberDisplay => !string.IsNullOrWhiteSpace(AccountNumber) ? AccountNumber.Trim() : MaskedNumber;

    public string AccountIdentifierLabel => Type switch
    {
        AccountType.EWallet => "Mobile number",
        _ => "Account number"
    };

    public bool IsWallet => Type == AccountType.Cash;

    public Visibility WalletBadgeVisibility => IsWallet ? Visibility.Visible : Visibility.Collapsed;

    public Visibility CardNumberVisibility => IsWallet ? Visibility.Collapsed : Visibility.Visible;

    public string TypeSubtitle => Type switch
    {
        AccountType.Savings => "Savings Account",
        AccountType.Bank => "Bank Account",
        AccountType.Cash => "Cash",
        AccountType.EWallet => "E-Wallet",
        _ => TypeDisplay
    };

    public string AccountDetailSubtitle => $"{TypeSubtitle} • {MaskedNumber}";

    public string AvailableToSpendDisplay => IncludeInAvailableToSpend
        ? "Included in future Available-to-Spend"
        : "Excluded from future Available-to-Spend";

    public string AtsBadgeText => IncludeInAvailableToSpend ? "Included in ATS" : "Excluded from ATS";

    public Brush AtsBadgeBackground => new SolidColorBrush(
        IncludeInAvailableToSpend ? Color.FromArgb(40, 255, 255, 255) : Color.FromArgb(40, 0, 0, 0));

    public Brush AtsBadgeBorder => new SolidColorBrush(
        IncludeInAvailableToSpend ? Color.FromArgb(60, 255, 255, 255) : Color.FromArgb(40, 255, 255, 255));

    public Visibility CheckmarkVisibility => IsSelected ? Visibility.Visible : Visibility.Collapsed;

    public Visibility MenuVisibility => IsSelected ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ActiveActionsVisibility => IsArchived ? Visibility.Collapsed : Visibility.Visible;

    public Visibility RestoreActionVisibility => IsArchived ? Visibility.Visible : Visibility.Collapsed;

    public Thickness CardBorderThickness => IsSelected ? new Thickness(2) : new Thickness(1);

    public Brush CardBorderBrush => new SolidColorBrush(
        IsSelected ? Color.FromArgb(240, 255, 255, 255) : Color.FromArgb(45, 255, 255, 255));

    public Color GradientStartColor
    {
        get
        {
            if (IsArchived) return Color.FromArgb(255, 160, 165, 171);
            return (ThemeIndex % 6) switch
            {
                0 => Color.FromArgb(255, 77, 110, 84),   // Sage Green
                1 => Color.FromArgb(255, 30, 63, 43),    // Deep Forest Green
                2 => Color.FromArgb(255, 42, 46, 51),    // Charcoal Slate
                3 => Color.FromArgb(255, 184, 146, 84),  // Warm Bronze / Gold
                4 => Color.FromArgb(255, 72, 96, 115),   // Slate Blue
                5 => Color.FromArgb(255, 117, 110, 138), // Soft Lavender
                _ => Color.FromArgb(255, 77, 110, 84)
            };
        }
    }

    public Color GradientEndColor
    {
        get
        {
            if (IsArchived) return Color.FromArgb(255, 117, 123, 130);
            return (ThemeIndex % 6) switch
            {
                0 => Color.FromArgb(255, 52, 78, 59),    // Sage Green Dark
                1 => Color.FromArgb(255, 18, 38, 26),    // Deep Forest Green Dark
                2 => Color.FromArgb(255, 28, 31, 34),    // Charcoal Slate Dark
                3 => Color.FromArgb(255, 140, 107, 52),  // Warm Bronze Dark
                4 => Color.FromArgb(255, 48, 67, 82),    // Slate Blue Dark
                5 => Color.FromArgb(255, 80, 74, 97),    // Soft Lavender Dark
                _ => Color.FromArgb(255, 52, 78, 59)
            };
        }
    }

    public LinearGradientBrush GradientBrush
    {
        get
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1)
            };
            brush.GradientStops.Add(new GradientStop { Color = GradientStartColor, Offset = 0.0 });
            brush.GradientStops.Add(new GradientStop { Color = GradientEndColor, Offset = 1.0 });
            return brush;
        }
    }
}
