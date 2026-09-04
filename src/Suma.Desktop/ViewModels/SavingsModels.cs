using Suma.Application.Abstractions.Persistence;
using Suma.Application.Savings.GetGoalContributionCandidates;
using Suma.Application.Savings.GetSavingsGoals;

namespace Suma.Desktop.ViewModels;

public sealed record SavingsGoalRowViewModel(SavingsGoalSummary Value)
{
    public Guid Id => Value.Id;
    public string Name => Value.Name;
    public string TargetDisplay => MoneyText.Format(Value.TargetAmountMinor, Value.CurrencyCode);
    public string ProgressDisplay => MoneyText.Format(Value.ProgressMinor, Value.CurrencyCode);
    public string RemainingDisplay => MoneyText.Format(Value.RemainingMinor, Value.CurrencyCode);
    public string TargetDateDisplay => Value.TargetDate?.ToString("MMM d, yyyy") ?? "No target date";
    public string DestinationDisplay => Value.DestinationAccountName ?? "No destination account";
    public string StatusDisplay => Value.IsArchived ? "Archived" : Value.ProgressMinor >= Value.TargetAmountMinor ? "Target reached" : "In progress";
    public double ProgressPercent => Value.TargetAmountMinor > 0 ? (double)Math.Clamp(checked((decimal)Value.ProgressMinor * 100m / Value.TargetAmountMinor), 0m, 100m) : 0;
    public string ProgressPercentDisplay => $"{ProgressPercent:0}%";
    public string TargetRatioDisplay => $"{ProgressDisplay} of {TargetDisplay}";
    public string TargetDateLabel => Value.TargetDate.HasValue ? $"Target date: {Value.TargetDate.Value:MMM d, yyyy}" : "No target date";
}

public sealed record GoalContributionRowViewModel(GoalContributionHistoryRecord Value)
{
    public string AmountDisplay => MoneyText.Format(Value.AmountMinor, Value.CurrencyCode);
    public string Title => $"{Value.Type} • {Value.TransactionType}";
    public string DateDisplay => Value.TransactionDate.ToString("MMM d, yyyy");
    public string Description => Value.Description ?? Value.CategoryName ?? "Transaction";
    public string Context => Value.TransactionType switch
    {
        Domain.Transactions.TransactionType.Transfer => $"{Value.SourceAccountName} → {Value.DestinationAccountName}",
        Domain.Transactions.TransactionType.Income => Value.DestinationAccountName ?? string.Empty,
        _ => Value.SourceAccountName ?? string.Empty
    };
}

public sealed record GoalCandidateRowViewModel(GoalContributionCandidate Value)
{
    public string Display => $"{Value.TransactionDate:MMM d} • {Value.TransactionType} • {Value.Description ?? Value.CategoryName ?? "Transaction"} • {MoneyText.Format(Value.RemainingCapacityMinor, Value.CurrencyCode)} available";
}

public sealed record SavingsAccountOption(Guid? Id, string Name, string? CurrencyCode)
{
    public string Display => Id.HasValue ? $"{Name} • {CurrencyCode}" : Name;
}
