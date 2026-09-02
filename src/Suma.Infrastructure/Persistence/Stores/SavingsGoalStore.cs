using Microsoft.EntityFrameworkCore;
using Suma.Application.Abstractions.Persistence;
using Suma.Domain.Savings;

namespace Suma.Infrastructure.Persistence.Stores;

public sealed class SavingsGoalStore(SumaDbContext context) : ISavingsGoalStore
{
    public Task<SavingsGoal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.SavingsGoals.SingleOrDefaultAsync(goal => goal.Id == id, cancellationToken);

    public async Task AddAsync(SavingsGoal goal, CancellationToken cancellationToken = default) =>
        await context.SavingsGoals.AddAsync(goal, cancellationToken);

    public async Task<IReadOnlyList<SavingsGoalFactRecord>> GetRecordsAsync(bool archived, CancellationToken cancellationToken = default)
    {
        var query =
            from goal in context.SavingsGoals.AsNoTracking()
            join account in context.Accounts.AsNoTracking() on goal.DestinationAccountId equals (Guid?)account.Id into accounts
            from account in accounts.DefaultIfEmpty()
            where goal.IsArchived == archived
            orderby goal.Name, goal.Id
            select new SavingsGoalFactRecord(goal.Id, goal.Name, goal.TargetAmount.AmountMinor, goal.TargetAmount.CurrencyCode,
                goal.TargetDate, goal.DestinationAccountId, account == null ? null : account.Name, goal.IsArchived,
                context.GoalContributions.Where(item => item.SavingsGoalId == goal.Id && item.Type == GoalContributionType.Deposit).Sum(item => (long?)item.Amount.AmountMinor) ?? 0,
                context.GoalContributions.Where(item => item.SavingsGoalId == goal.Id && item.Type == GoalContributionType.Withdrawal).Sum(item => (long?)item.Amount.AmountMinor) ?? 0);
        return await query.ToListAsync(cancellationToken);
    }
}
