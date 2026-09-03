using Microsoft.EntityFrameworkCore;
using Suma.Application.Abstractions.Persistence;
using Suma.Domain.Recurring;
using Suma.Domain.Transactions;

namespace Suma.Infrastructure.Persistence.Stores;

public sealed class OverviewStore(SumaDbContext context) : IOverviewStore
{
    public async Task<IReadOnlyList<OverviewCurrencyFact>> GetAccountCurrencyFactsAsync(CancellationToken cancellationToken = default)
    {
        var currencies = await context.Accounts.AsNoTracking()
            .Select(item => item.OpeningBalance.CurrencyCode)
            .Distinct()
            .OrderBy(item => item)
            .ToListAsync(cancellationToken);
        var activeIncludedCurrencies = await context.Accounts.AsNoTracking()
            .Where(item => !item.IsArchived && item.IncludeInAvailableToSpend)
            .Select(item => item.OpeningBalance.CurrencyCode)
            .Distinct()
            .ToListAsync(cancellationToken);
        return currencies.Select(item => new OverviewCurrencyFact(
            item,
            activeIncludedCurrencies.Contains(item, StringComparer.Ordinal))).ToArray();
    }

    public async Task<IReadOnlyList<OverviewAccountBalanceFact>> GetAccountBalanceFactsAsync(string currencyCode, CancellationToken cancellationToken = default)
    {
        var query = context.Accounts.AsNoTracking().Where(account => account.OpeningBalance.CurrencyCode == currencyCode)
            .OrderBy(account => account.Name).Select(account => new OverviewAccountBalanceFact(
                account.Id, account.Name, account.IsArchived, account.IncludeInAvailableToSpend,
                account.OpeningBalance.AmountMinor,
                context.Transactions.Where(item => item.Type == TransactionType.Income && item.DestinationAccountId == account.Id).Sum(item => (long?)item.Amount.AmountMinor) ?? 0,
                context.Transactions.Where(item => item.Type == TransactionType.Refund && item.DestinationAccountId == account.Id).Sum(item => (long?)item.Amount.AmountMinor) ?? 0,
                context.Transactions.Where(item => item.Type == TransactionType.Transfer && item.DestinationAccountId == account.Id).Sum(item => (long?)item.Amount.AmountMinor) ?? 0,
                context.Transactions.Where(item => item.Type == TransactionType.Expense && item.SourceAccountId == account.Id).Sum(item => (long?)item.Amount.AmountMinor) ?? 0,
                context.Transactions.Where(item => item.Type == TransactionType.Transfer && item.SourceAccountId == account.Id).Sum(item => (long?)item.Amount.AmountMinor) ?? 0,
                account.OpeningBalance.CurrencyCode));
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OverviewRecurringFact>> GetUpcomingRecurringAsync(string currencyCode, DateOnly today, int limit, CancellationToken cancellationToken = default) =>
        await (from occurrence in context.RecurringOccurrences.AsNoTracking()
               join recurring in context.RecurringTransactions.AsNoTracking() on occurrence.RecurringTransactionId equals recurring.Id
               where occurrence.Status == RecurringOccurrenceStatus.Pending && occurrence.DueDate >= today
                   && recurring.Amount.CurrencyCode == currencyCode
               orderby occurrence.DueDate, occurrence.Id
               select new OverviewRecurringFact(occurrence.Id, occurrence.DueDate, recurring.Type,
                   recurring.Amount.AmountMinor, recurring.Amount.CurrencyCode, recurring.Description))
            .Take(limit).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<OverviewActivityFact>> GetRecentActivityAsync(string currencyCode, int limit, CancellationToken cancellationToken = default) =>
        await context.Transactions.AsNoTracking().Where(item => item.Amount.CurrencyCode == currencyCode)
            .OrderByDescending(item => item.TransactionDate).ThenByDescending(item => item.Id)
            .Select(item => new OverviewActivityFact(item.Id, item.TransactionDate, item.Type,
                item.Amount.AmountMinor, item.Amount.CurrencyCode, item.Description))
            .Take(limit).ToListAsync(cancellationToken);
}
