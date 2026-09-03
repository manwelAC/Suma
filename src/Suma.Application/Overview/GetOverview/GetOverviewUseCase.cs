using Suma.Application.Abstractions.Persistence;
using Suma.Application.Abstractions.Time;
using Suma.Application.Recurring.EnsureRecurringOccurrences;
using Suma.Domain.ValueObjects;

namespace Suma.Application.Overview.GetOverview;

public sealed record OverviewAccountSummary(Guid Id, string Name, long BalanceMinor, bool IsArchived, bool Included);
public sealed record OverviewBudgetSummary(Guid Id, string Name, DateOnly PeriodStart, DateOnly PeriodEnd, long ExpectedIncomeMinor, long AllocatedMinor, long SpentMinor, long RemainingMinor, long ProtectedRemainingMinor);
public sealed record OverviewSavingsSummary(Guid Id, string Name, long ProgressMinor, long TargetMinor, long RemainingMinor);
public sealed record OverviewUpcomingRow(Guid Id, DateOnly DueDate, Domain.Transactions.TransactionType Type, long AmountMinor, string? Description);
public sealed record OverviewActivityRow(Guid Id, DateOnly TransactionDate, Domain.Transactions.TransactionType Type, long AmountMinor, string? Description);
public sealed record OverviewResult(
    string CurrencyCode,
    IReadOnlyList<string> AvailableCurrencies,
    long AvailableToSpendMinor,
    long IncludedAccountBalanceMinor,
    long ProtectedBudgetRemainingMinor,
    long AccountTotalMinor,
    IReadOnlyList<OverviewAccountSummary> Accounts,
    OverviewBudgetSummary? CurrentBudget,
    IReadOnlyList<OverviewSavingsSummary> Savings,
    IReadOnlyList<OverviewUpcomingRow> Upcoming,
    IReadOnlyList<OverviewActivityRow> RecentActivity);

public sealed class GetOverviewUseCase(
    IOverviewStore overview,
    IBudgetStore budgets,
    IBudgetAllocationStore allocations,
    ITransactionStore transactions,
    ISavingsGoalStore savings,
    EnsureRecurringOccurrencesUseCase ensureOccurrences,
    IDateProvider dateProvider)
{
    public async Task<OverviewResult> ExecuteAsync(string? currencyCode, CancellationToken cancellationToken = default)
    {
        var currencyFacts = await overview.GetAccountCurrencyFactsAsync(cancellationToken);
        var currencies = currencyFacts.Select(item => item.CurrencyCode).ToArray();
        if (currencies.Length == 0)
        {
            return new(string.Empty, [], 0, 0, 0, 0, [], null, [], [], []);
        }

        var requestedCurrency = string.IsNullOrWhiteSpace(currencyCode)
            ? null
            : new Money(0, currencyCode).CurrencyCode;
        var currency = requestedCurrency is not null && currencies.Contains(requestedCurrency, StringComparer.Ordinal)
            ? requestedCurrency
            : currencyFacts.FirstOrDefault(item => item.HasActiveIncludedAccount)?.CurrencyCode ?? currencies[0];
        var accountFacts = await overview.GetAccountBalanceFactsAsync(currency, cancellationToken);
        var accounts = accountFacts.Select(ToAccount).ToArray();
        var included = accounts.Where(item => item.Included).Aggregate(0L, (total, item) => checked(total + item.BalanceMinor));
        var accountTotal = accounts.Aggregate(0L, (total, item) => checked(total + item.BalanceMinor));

        var currentBudget = (await budgets.GetAsync(false, cancellationToken)).SingleOrDefault(item =>
            item.CurrencyCode == currency && item.PeriodStart <= dateProvider.Today && item.PeriodEnd >= dateProvider.Today);
        OverviewBudgetSummary? budgetSummary = null;
        var protectedRemaining = 0L;
        if (currentBudget is not null)
        {
            var allocationFacts = await allocations.GetForBudgetAsync(currentBudget.Id, cancellationToken);
            var spending = (await transactions.GetNetExpenseAmountsByCategoryAsync(
                currentBudget.PeriodStart, currentBudget.PeriodEnd, currency,
                allocationFacts.Select(item => item.CategoryId).ToArray(), cancellationToken))
                .ToDictionary(item => item.CategoryId, item => item.AmountMinor);
            var allocated = 0L;
            var spent = 0L;
            foreach (var allocation in allocationFacts)
            {
                var allocationSpent = spending.GetValueOrDefault(allocation.CategoryId);
                allocated = checked(allocated + allocation.AmountMinor);
                spent = checked(spent + allocationSpent);
                if (allocation.ReserveFromAvailable)
                {
                    protectedRemaining = checked(protectedRemaining + Math.Max(0L, checked(allocation.AmountMinor - allocationSpent)));
                }
            }

            budgetSummary = new(currentBudget.Id, currentBudget.Name, currentBudget.PeriodStart, currentBudget.PeriodEnd,
                currentBudget.ExpectedIncome.AmountMinor, allocated, spent, checked(allocated - spent), protectedRemaining);
        }

        _ = await ensureOccurrences.ExecuteAsync(cancellationToken);
        var savingsRows = (await savings.GetRecordsAsync(false, cancellationToken))
            .Where(item => item.CurrencyCode == currency)
            .Select(item =>
            {
                var progress = checked(item.DepositMinor - item.WithdrawalMinor);
                return new OverviewSavingsSummary(item.Id, item.Name, progress, item.TargetAmountMinor, checked(item.TargetAmountMinor - progress));
            }).ToArray();
        var upcoming = (await overview.GetUpcomingRecurringAsync(currency, dateProvider.Today, 5, cancellationToken))
            .Select(item => new OverviewUpcomingRow(item.OccurrenceId, item.DueDate, item.Type, item.AmountMinor, item.Description)).ToArray();
        var activity = (await overview.GetRecentActivityAsync(currency, 5, cancellationToken))
            .Select(item => new OverviewActivityRow(item.TransactionId, item.TransactionDate, item.Type, item.AmountMinor, item.Description)).ToArray();

        return new(currency, currencies, checked(included - protectedRemaining), included,
            protectedRemaining, accountTotal, accounts, budgetSummary, savingsRows, upcoming, activity);
    }

    private static OverviewAccountSummary ToAccount(OverviewAccountBalanceFact item)
    {
        var balance = item.OpeningBalanceMinor;
        balance = checked(balance + item.IncomeMinor);
        balance = checked(balance + item.RefundMinor);
        balance = checked(balance + item.TransferInMinor);
        balance = checked(balance - item.ExpenseMinor);
        balance = checked(balance - item.TransferOutMinor);
        return new(item.AccountId, item.Name, balance, item.IsArchived, item.IncludeInAvailableToSpend);
    }
}
