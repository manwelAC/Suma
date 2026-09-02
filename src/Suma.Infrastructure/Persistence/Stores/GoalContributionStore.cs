using Microsoft.EntityFrameworkCore;
using Suma.Application.Abstractions.Persistence;
using Suma.Domain.Savings;

namespace Suma.Infrastructure.Persistence.Stores;

public sealed class GoalContributionStore(SumaDbContext context) : IGoalContributionStore
{
    public async Task AddAsync(GoalContribution contribution, CancellationToken cancellationToken = default) =>
        await context.GoalContributions.AddAsync(contribution, cancellationToken);

    public async Task<long> GetAttributedAmountMinorAsync(Guid transactionId, CancellationToken cancellationToken = default) =>
        await context.GoalContributions.AsNoTracking().Where(contribution => contribution.TransactionId == transactionId).SumAsync(contribution => (long?)contribution.Amount.AmountMinor, cancellationToken) ?? 0;

    public async Task<IReadOnlyList<GoalContributionHistoryRecord>> GetForGoalAsync(Guid savingsGoalId, CancellationToken cancellationToken = default)
    {
        var query =
            from contribution in context.GoalContributions.AsNoTracking()
            join transaction in context.Transactions.AsNoTracking() on contribution.TransactionId equals transaction.Id
            join source in context.Accounts.AsNoTracking() on transaction.SourceAccountId equals (Guid?)source.Id into sources
            from source in sources.DefaultIfEmpty()
            join destination in context.Accounts.AsNoTracking() on transaction.DestinationAccountId equals (Guid?)destination.Id into destinations
            from destination in destinations.DefaultIfEmpty()
            join category in context.Categories.AsNoTracking() on transaction.CategoryId equals (Guid?)category.Id into categories
            from category in categories.DefaultIfEmpty()
            where contribution.SavingsGoalId == savingsGoalId
            orderby transaction.TransactionDate descending, contribution.Id descending
            select new GoalContributionHistoryRecord(contribution.Id, contribution.TransactionId, contribution.Type,
                contribution.Amount.AmountMinor, contribution.Amount.CurrencyCode, transaction.TransactionDate,
                transaction.Type, transaction.Description, source == null ? null : source.Name,
                destination == null ? null : destination.Name, category == null ? null : category.Name);
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GoalContributionCandidateFact>> GetCandidateFactsAsync(string currencyCode, CancellationToken cancellationToken = default)
    {
        var query =
            from transaction in context.Transactions.AsNoTracking()
            join source in context.Accounts.AsNoTracking() on transaction.SourceAccountId equals (Guid?)source.Id into sources
            from source in sources.DefaultIfEmpty()
            join destination in context.Accounts.AsNoTracking() on transaction.DestinationAccountId equals (Guid?)destination.Id into destinations
            from destination in destinations.DefaultIfEmpty()
            join category in context.Categories.AsNoTracking() on transaction.CategoryId equals (Guid?)category.Id into categories
            from category in categories.DefaultIfEmpty()
            where transaction.Amount.CurrencyCode == currencyCode
            orderby transaction.TransactionDate descending, transaction.Id descending
            select new GoalContributionCandidateFact(transaction.Id, transaction.TransactionDate, transaction.Type,
                transaction.Description, source == null ? null : source.Name,
                destination == null ? null : destination.Name, category == null ? null : category.Name,
                transaction.Amount.AmountMinor, transaction.Amount.CurrencyCode,
                context.GoalContributions.Where(item => item.TransactionId == transaction.Id).Sum(item => (long?)item.Amount.AmountMinor) ?? 0);
        return await query.ToListAsync(cancellationToken);
    }
}
