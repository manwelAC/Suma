using Suma.Application.Abstractions.Persistence;

namespace Suma.Application.Savings.GetSavingsGoals;

public sealed record SavingsGoalSummary(
    Guid Id, string Name, long TargetAmountMinor, string CurrencyCode, long ProgressMinor,
    long RemainingMinor, DateOnly? TargetDate, Guid? DestinationAccountId,
    string? DestinationAccountName, bool IsArchived);

public sealed class GetSavingsGoalsUseCase(ISavingsGoalStore goals)
{
    public async Task<IReadOnlyList<SavingsGoalSummary>> ExecuteAsync(bool archived, CancellationToken cancellationToken = default) =>
        (await goals.GetRecordsAsync(archived, cancellationToken)).Select(ToSummary).ToArray();

    internal static SavingsGoalSummary ToSummary(SavingsGoalFactRecord item)
    {
        var progress = checked(item.DepositMinor - item.WithdrawalMinor);
        return new(item.Id, item.Name, item.TargetAmountMinor, item.CurrencyCode, progress,
            checked(item.TargetAmountMinor - progress), item.TargetDate, item.DestinationAccountId,
            item.DestinationAccountName, item.IsArchived);
    }
}
