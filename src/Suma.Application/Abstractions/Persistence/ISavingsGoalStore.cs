using Suma.Domain.Savings;

namespace Suma.Application.Abstractions.Persistence;

public interface ISavingsGoalStore
{
    Task<SavingsGoal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(SavingsGoal goal, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SavingsGoalFactRecord>> GetRecordsAsync(bool archived, CancellationToken cancellationToken = default);
}

public sealed record SavingsGoalFactRecord(
    Guid Id, string Name, long TargetAmountMinor, string CurrencyCode, DateOnly? TargetDate,
    Guid? DestinationAccountId, string? DestinationAccountName, bool IsArchived,
    long DepositMinor, long WithdrawalMinor);
