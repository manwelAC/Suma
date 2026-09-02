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
}

public sealed record RecurringAccountOption(Guid Id, string Name, string CurrencyCode)
{
    public string Display => $"{Name} • {CurrencyCode}";
}

public sealed record RecurringCategoryOption(Guid Id, string Name)
{
    public string Display => Name;
}
