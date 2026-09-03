using Microsoft.EntityFrameworkCore;
using Suma.Application.Abstractions.Persistence;
using Suma.Domain.Transactions;

namespace Suma.Infrastructure.Persistence.Stores;

public sealed class ReportStore(SumaDbContext context) : IReportStore
{
    public async Task<IReadOnlyList<ReportCategoryFact>> GetCategoryFactsAsync(string currencyCode, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        var direct = await (from transaction in FilteredTransactions(currencyCode, startDate, endDate)
                            where transaction.Type == TransactionType.Income || transaction.Type == TransactionType.Expense
                            join category in context.Categories.AsNoTracking() on transaction.CategoryId equals (Guid?)category.Id
                            group transaction by new { category.Id, category.Name, category.IsArchived } into groupRows
                            select new CategoryAggregate(groupRows.Key.Id, groupRows.Key.Name, groupRows.Key.IsArchived,
                                groupRows.Sum(item => item.Type == TransactionType.Income ? item.Amount.AmountMinor : 0L),
                                groupRows.Sum(item => item.Type == TransactionType.Expense ? item.Amount.AmountMinor : 0L), 0L)).ToListAsync(cancellationToken);
        var refunds = await (from refund in FilteredTransactions(currencyCode, startDate, endDate)
                             where refund.Type == TransactionType.Refund
                             join original in context.Transactions.AsNoTracking() on refund.OriginalTransactionId equals (Guid?)original.Id
                             join category in context.Categories.AsNoTracking() on original.CategoryId equals (Guid?)category.Id
                             group refund by new { category.Id, category.Name, category.IsArchived } into groupRows
                             select new CategoryAggregate(groupRows.Key.Id, groupRows.Key.Name, groupRows.Key.IsArchived, 0L, 0L,
                                 groupRows.Sum(item => item.Amount.AmountMinor))).ToListAsync(cancellationToken);

        return direct.Concat(refunds).GroupBy(item => new { item.Id, item.Name, item.IsArchived })
            .Select(group => new ReportCategoryFact(group.Key.Id, group.Key.Name, group.Key.IsArchived,
                group.Aggregate(0L, (sum, item) => checked(sum + item.IncomeMinor)),
                group.Aggregate(0L, (sum, item) => checked(sum + item.ExpenseMinor)),
                group.Aggregate(0L, (sum, item) => checked(sum + item.RefundMinor)))).ToArray();
    }

    public async Task<IReadOnlyList<ReportAccountMovementFact>> GetAccountMovementFactsAsync(string currencyCode, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        var source = await (from transaction in FilteredTransactions(currencyCode, startDate, endDate)
                            where transaction.SourceAccountId.HasValue
                            join account in context.Accounts.AsNoTracking() on transaction.SourceAccountId equals (Guid?)account.Id
                            group transaction by new { account.Id, account.Name, account.IsArchived } into groupRows
                            select new AccountAggregate(groupRows.Key.Id, groupRows.Key.Name, groupRows.Key.IsArchived, 0L, 0L, 0L,
                                groupRows.Sum(item => item.Type == TransactionType.Expense ? item.Amount.AmountMinor : 0L),
                                groupRows.Sum(item => item.Type == TransactionType.Transfer ? item.Amount.AmountMinor : 0L))).ToListAsync(cancellationToken);
        var destination = await (from transaction in FilteredTransactions(currencyCode, startDate, endDate)
                                 where transaction.DestinationAccountId.HasValue
                                 join account in context.Accounts.AsNoTracking() on transaction.DestinationAccountId equals (Guid?)account.Id
                                 group transaction by new { account.Id, account.Name, account.IsArchived } into groupRows
                                 select new AccountAggregate(groupRows.Key.Id, groupRows.Key.Name, groupRows.Key.IsArchived,
                                     groupRows.Sum(item => item.Type == TransactionType.Income ? item.Amount.AmountMinor : 0L),
                                     groupRows.Sum(item => item.Type == TransactionType.Refund ? item.Amount.AmountMinor : 0L),
                                     groupRows.Sum(item => item.Type == TransactionType.Transfer ? item.Amount.AmountMinor : 0L), 0L, 0L)).ToListAsync(cancellationToken);

        return source.Concat(destination).GroupBy(item => new { item.Id, item.Name, item.IsArchived })
            .Select(group => new ReportAccountMovementFact(group.Key.Id, group.Key.Name, group.Key.IsArchived,
                group.Aggregate(0L, (sum, item) => checked(sum + item.IncomeMinor)),
                group.Aggregate(0L, (sum, item) => checked(sum + item.RefundMinor)),
                group.Aggregate(0L, (sum, item) => checked(sum + item.TransferInMinor)),
                group.Aggregate(0L, (sum, item) => checked(sum + item.ExpenseMinor)),
                group.Aggregate(0L, (sum, item) => checked(sum + item.TransferOutMinor)))).ToArray();
    }

    public async Task<IReadOnlyList<ReportAccountMovementDetailFact>> GetAccountMovementDetailsAsync(string currencyCode, DateOnly startDate, DateOnly endDate, Guid? accountId, CancellationToken cancellationToken = default)
    {
        var query = GetRows(currencyCode, startDate, endDate);
        if (accountId.HasValue) query = query.Where(item => item.SourceAccountId == accountId || item.DestinationAccountId == accountId);
        var rows = await query.ToListAsync(cancellationToken);
        return ExpandAccounts(rows).Where(item => !accountId.HasValue || item.AccountId == accountId.Value)
            .Select(item => new ReportAccountMovementDetailFact(item.TransactionId, item.TransactionDate, item.AccountId, item.AccountName, item.AccountArchived, item.Direction, item.Type, item.Counterparty, item.CategoryName, item.Description, item.AmountMinor, item.CurrencyCode)).ToArray();
    }

    private IQueryable<LedgerRow> GetRows(string currencyCode, DateOnly startDate, DateOnly endDate) =>
        from transaction in FilteredTransactions(currencyCode, startDate, endDate)
        join source in context.Accounts.AsNoTracking() on transaction.SourceAccountId equals (Guid?)source.Id into sources
        from source in sources.DefaultIfEmpty()
        join destination in context.Accounts.AsNoTracking() on transaction.DestinationAccountId equals (Guid?)destination.Id into destinations
        from destination in destinations.DefaultIfEmpty()
        join original in context.Transactions.AsNoTracking() on transaction.OriginalTransactionId equals (Guid?)original.Id into originals
        from original in originals.DefaultIfEmpty()
        let reportCategoryId = transaction.Type == TransactionType.Refund ? original.CategoryId : transaction.CategoryId
        join category in context.Categories.AsNoTracking() on reportCategoryId equals (Guid?)category.Id into categories
        from category in categories.DefaultIfEmpty()
        select new LedgerRow(transaction.Id, transaction.TransactionDate, transaction.Type, transaction.SourceAccountId,
            source == null ? null : source.Name, source != null && source.IsArchived, transaction.DestinationAccountId,
            destination == null ? null : destination.Name, destination != null && destination.IsArchived,
            reportCategoryId, category == null ? null : category.Name, category != null && category.IsArchived,
            transaction.Description, transaction.Amount.AmountMinor, transaction.Amount.CurrencyCode);

    private static IEnumerable<AccountRow> ExpandAccounts(IEnumerable<LedgerRow> rows)
    {
        foreach (var row in rows)
        {
            if (row.SourceAccountId.HasValue)
                yield return new(row.TransactionId, row.TransactionDate, row.SourceAccountId.Value, row.SourceAccountName!, row.SourceArchived, ReportMovementDirection.Outflow, row.Type, row.DestinationAccountName, row.CategoryName, row.Description, row.AmountMinor, row.CurrencyCode);
            if (row.DestinationAccountId.HasValue)
                yield return new(row.TransactionId, row.TransactionDate, row.DestinationAccountId.Value, row.DestinationAccountName!, row.DestinationArchived, ReportMovementDirection.Inflow, row.Type, row.SourceAccountName, row.CategoryName, row.Description, row.AmountMinor, row.CurrencyCode);
        }
    }

    private IQueryable<Transaction> FilteredTransactions(string currencyCode, DateOnly startDate, DateOnly endDate) => context.Transactions.AsNoTracking().Where(transaction => transaction.Amount.CurrencyCode == currencyCode && transaction.TransactionDate >= startDate && transaction.TransactionDate <= endDate);
    private sealed record CategoryAggregate(Guid Id, string Name, bool IsArchived, long IncomeMinor, long ExpenseMinor, long RefundMinor);
    private sealed record AccountAggregate(Guid Id, string Name, bool IsArchived, long IncomeMinor, long RefundMinor, long TransferInMinor, long ExpenseMinor, long TransferOutMinor);
    private sealed record LedgerRow(Guid TransactionId, DateOnly TransactionDate, TransactionType Type, Guid? SourceAccountId, string? SourceAccountName, bool SourceArchived, Guid? DestinationAccountId, string? DestinationAccountName, bool DestinationArchived, Guid? CategoryId, string? CategoryName, bool CategoryArchived, string? Description, long AmountMinor, string CurrencyCode);
    private sealed record AccountRow(Guid TransactionId, DateOnly TransactionDate, Guid AccountId, string AccountName, bool AccountArchived, ReportMovementDirection Direction, TransactionType Type, string? Counterparty, string? CategoryName, string? Description, long AmountMinor, string CurrencyCode);
}
