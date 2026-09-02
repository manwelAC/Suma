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
    DateOnly TransactionDate,
    string DateGroupLabel,
    bool StartsDateGroup)
{
    public Visibility DateGroupVisibility => StartsDateGroup ? Visibility.Visible : Visibility.Collapsed;
}
