using Microsoft.UI.Xaml;
using Suma.Domain.Accounts;

namespace Suma.Desktop.ViewModels;

public sealed record AccountRowViewModel(
    Guid Id,
    string Name,
    AccountType Type,
    string TypeDisplay,
    string BalanceDisplay,
    string CurrencyCode,
    bool IncludeInAvailableToSpend,
    bool IsArchived)
{
    public string AvailableToSpendDisplay => IncludeInAvailableToSpend
        ? "Included in future Available-to-Spend"
        : "Excluded from future Available-to-Spend";

    public Visibility ActiveActionsVisibility => IsArchived ? Visibility.Collapsed : Visibility.Visible;

    public Visibility RestoreActionVisibility => IsArchived ? Visibility.Visible : Visibility.Collapsed;
}
