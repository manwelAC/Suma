using Suma.Application.Abstractions.Persistence;
using Suma.Domain.Accounts;
using Suma.Domain.Budgets;
using Suma.Domain.Categories;
using Suma.Domain.Recurring;
using Suma.Domain.Savings;
using Suma.Domain.Transactions;

namespace Suma.Application.Tests.TestDoubles;

internal sealed class FakeData : IAccountStore, ICategoryStore, ITransactionStore, IBudgetStore, IBudgetAllocationStore, IRecurringTransactionStore, IRecurringOccurrenceStore, ISavingsGoalStore, IGoalContributionStore, IOverviewStore, IReportStore, IUnitOfWork
{
    public Dictionary<Guid, Account> Accounts { get; } = [];
    public Dictionary<Guid, Category> Categories { get; } = [];
    public Dictionary<Guid, Transaction> Transactions { get; } = [];
    public Dictionary<Guid, Budget> Budgets { get; } = [];
    public Dictionary<Guid, RecurringTransaction> RecurringTransactions { get; } = [];
    public Dictionary<Guid, RecurringOccurrence> Occurrences { get; } = [];
    public Dictionary<Guid, SavingsGoal> Goals { get; } = [];
    public List<BudgetAllocation> Allocations { get; } = [];
    public List<GoalContribution> Contributions { get; } = [];
    public bool HasOverlap { get; set; }
    public long RefundedAmountMinor { get; set; }
    public long AttributedAmountMinor { get; set; }
    public int SaveCount { get; private set; }
    public int AddedTransactionCount { get; private set; }
    public int AddedRecurringTransactionCount { get; private set; }
    public IReadOnlyList<ReportCategoryFact>? ReportCategoryFactsOverride { get; set; }
    public IReadOnlyList<ReportAccountMovementFact>? ReportAccountFactsOverride { get; set; }

    Task<Account?> IAccountStore.GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Accounts.GetValueOrDefault(id));
    Task<IReadOnlyList<Account>> IAccountStore.GetActiveAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Account>>(Accounts.Values.Where(account => !account.IsArchived).ToArray());
    Task<IReadOnlyList<Account>> IAccountStore.GetArchivedAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Account>>(Accounts.Values.Where(account => account.IsArchived).ToArray());
    Task<Category?> ICategoryStore.GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Categories.GetValueOrDefault(id));
    Task<IReadOnlyList<Category>> ICategoryStore.GetAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Category>>(Categories.Values.ToArray());
    Task<bool> ICategoryStore.HasActiveChildrenAsync(Guid parentCategoryId, CancellationToken cancellationToken) => Task.FromResult(Categories.Values.Any(category => category.ParentCategoryId == parentCategoryId && !category.IsArchived));
    Task<Transaction?> ITransactionStore.GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Transactions.GetValueOrDefault(id));
    Task<IReadOnlyList<Transaction>> ITransactionStore.GetRecentAsync(int limit, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Transaction>>(Transactions.Values.Take(limit).ToArray());
    Task<IReadOnlyList<Transaction>> ITransactionStore.GetForAccountAsync(Guid accountId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Transaction>>(Transactions.Values.Where(transaction => transaction.SourceAccountId == accountId || transaction.DestinationAccountId == accountId).ToArray());
    Task<long> ITransactionStore.GetRefundedAmountMinorAsync(Guid originalTransactionId, CancellationToken cancellationToken) => Task.FromResult(RefundedAmountMinor);
    Task<IReadOnlyList<TransactionHistoryRecord>> ITransactionStore.GetHistoryAsync(TransactionType? type, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TransactionHistoryRecord>>(Transactions.Values
            .Where(item => !type.HasValue || item.Type == type.Value)
            .OrderByDescending(item => item.TransactionDate)
            .ThenByDescending(item => item.Id)
            .Take(limit)
            .Select(item => new TransactionHistoryRecord(
                item.Id, item.Type, item.SourceAccountId,
                item.SourceAccountId.HasValue ? Accounts.GetValueOrDefault(item.SourceAccountId.Value)?.Name : null,
                item.DestinationAccountId,
                item.DestinationAccountId.HasValue ? Accounts.GetValueOrDefault(item.DestinationAccountId.Value)?.Name : null,
                item.CategoryId,
                item.CategoryId.HasValue ? Categories.GetValueOrDefault(item.CategoryId.Value)?.Name : null,
                item.OriginalTransactionId, item.Amount.AmountMinor, item.Amount.CurrencyCode,
                item.TransactionDate, item.Description, item.Notes))
            .ToArray());
    Task<IReadOnlyList<RefundableExpenseRecord>> ITransactionStore.GetRefundableExpensesAsync(int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RefundableExpenseRecord>>(Transactions.Values
            .Where(item => item.Type == TransactionType.Expense)
            .Select(item => new
            {
                Item = item,
                Refunded = Transactions.Values.Where(refund => refund.Type == TransactionType.Refund && refund.OriginalTransactionId == item.Id).Sum(refund => refund.Amount.AmountMinor)
            })
            .Where(item => !Categories[item.Item.CategoryId!.Value].IsArchived)
            .Where(item => item.Refunded < item.Item.Amount.AmountMinor)
            .OrderByDescending(item => item.Item.TransactionDate)
            .Take(limit)
            .Select(item => new RefundableExpenseRecord(
                item.Item.Id, item.Item.SourceAccountId!.Value, Accounts[item.Item.SourceAccountId.Value].Name,
                item.Item.CategoryId!.Value, Categories[item.Item.CategoryId.Value].Name,
                item.Item.Amount.AmountMinor, item.Refunded, item.Item.Amount.CurrencyCode,
                item.Item.TransactionDate, item.Item.Description))
            .ToArray());
    Task<IReadOnlyList<CategoryNetExpenseRecord>> ITransactionStore.GetNetExpenseAmountsByCategoryAsync(DateOnly periodStart, DateOnly periodEnd, string currencyCode, IReadOnlyCollection<Guid> categoryIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CategoryNetExpenseRecord>>(Transactions.Values
            .Where(expense => expense.Type == TransactionType.Expense
                && expense.TransactionDate >= periodStart
                && expense.TransactionDate <= periodEnd
                && string.Equals(expense.Amount.CurrencyCode, currencyCode, StringComparison.Ordinal)
                && expense.CategoryId.HasValue
                && categoryIds.Contains(expense.CategoryId.Value))
            .GroupBy(expense => expense.CategoryId!.Value)
            .Select(group => new CategoryNetExpenseRecord(
                group.Key,
                group.Sum(expense => expense.Amount.AmountMinor)
                    - Transactions.Values.Where(refund => refund.Type == TransactionType.Refund
                        && refund.OriginalTransactionId.HasValue
                        && group.Any(expense => expense.Id == refund.OriginalTransactionId.Value))
                        .Sum(refund => refund.Amount.AmountMinor)))
            .ToArray());
    Task<Budget?> IBudgetStore.GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Budgets.GetValueOrDefault(id));
    Task<IReadOnlyList<Budget>> IBudgetStore.GetAsync(bool archived, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Budget>>(Budgets.Values.Where(budget => budget.IsArchived == archived).OrderByDescending(budget => budget.PeriodStart).ToArray());
    Task<bool> IBudgetStore.HasActiveOverlapAsync(DateOnly periodStart, DateOnly periodEnd, Guid? excludingBudgetId, CancellationToken cancellationToken) => Task.FromResult(HasOverlap);
    Task<bool> IBudgetAllocationStore.ExistsAsync(Guid budgetId, Guid categoryId, CancellationToken cancellationToken) => Task.FromResult(Allocations.Any(allocation => allocation.BudgetId == budgetId && allocation.CategoryId == categoryId));
    Task<IReadOnlyList<BudgetAllocationRecord>> IBudgetAllocationStore.GetForBudgetAsync(Guid budgetId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BudgetAllocationRecord>>(Allocations
            .Where(allocation => allocation.BudgetId == budgetId)
            .Select(allocation => new BudgetAllocationRecord(
                allocation.Id,
                allocation.BudgetId,
                allocation.CategoryId,
                Categories[allocation.CategoryId].Name,
                Categories[allocation.CategoryId].IsArchived,
                allocation.Amount.AmountMinor,
                allocation.CurrencyCode,
                allocation.ReserveFromAvailable))
            .ToArray());
    Task<RecurringTransaction?> IRecurringTransactionStore.GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(RecurringTransactions.GetValueOrDefault(id));
    Task<IReadOnlyList<RecurringTransaction>> IRecurringTransactionStore.GetActiveAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<RecurringTransaction>>(RecurringTransactions.Values.Where(item => item.IsActive).ToArray());
    Task<IReadOnlyList<RecurringScheduleRecord>> IRecurringTransactionStore.GetSchedulesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<RecurringScheduleRecord>>(RecurringTransactions.Values.Select(item => new RecurringScheduleRecord(item.Id, item.Type, item.SourceAccountId, item.SourceAccountId.HasValue ? Accounts.GetValueOrDefault(item.SourceAccountId.Value)?.Name : null, item.DestinationAccountId, item.DestinationAccountId.HasValue ? Accounts.GetValueOrDefault(item.DestinationAccountId.Value)?.Name : null, item.CategoryId, item.CategoryId.HasValue ? Categories.GetValueOrDefault(item.CategoryId.Value)?.Name : null, item.Amount.AmountMinor, item.Amount.CurrencyCode, item.FrequencyUnit, item.IntervalCount, item.DayOfWeek, item.DayOfMonth, item.MonthOfYear, item.StartDate, item.EndDate, item.Description, item.IsActive)).ToArray());
    Task<RecurringOccurrence?> IRecurringOccurrenceStore.GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Occurrences.GetValueOrDefault(id));
    Task<IReadOnlySet<(Guid RecurringTransactionId, DateOnly DueDate)>> IRecurringOccurrenceStore.GetExistingKeysAsync(IReadOnlyCollection<Guid> recurringTransactionIds, DateOnly from, DateOnly through, CancellationToken cancellationToken) => Task.FromResult<IReadOnlySet<(Guid, DateOnly)>>(Occurrences.Values.Where(item => recurringTransactionIds.Contains(item.RecurringTransactionId) && item.DueDate >= from && item.DueDate <= through).Select(item => (item.RecurringTransactionId, item.DueDate)).ToHashSet());
    Task<IReadOnlyList<RecurringOccurrenceRecord>> IRecurringOccurrenceStore.GetRecordsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<RecurringOccurrenceRecord>>(Occurrences.Values.Select(item => { var recurring = RecurringTransactions[item.RecurringTransactionId]; return new RecurringOccurrenceRecord(item.Id, item.RecurringTransactionId, item.DueDate, item.Status, item.TransactionId, recurring.Type, recurring.Amount.AmountMinor, recurring.Amount.CurrencyCode, recurring.Description, recurring.SourceAccountId.HasValue ? Accounts.GetValueOrDefault(recurring.SourceAccountId.Value)?.Name : null, recurring.DestinationAccountId.HasValue ? Accounts.GetValueOrDefault(recurring.DestinationAccountId.Value)?.Name : null, recurring.CategoryId.HasValue ? Categories.GetValueOrDefault(recurring.CategoryId.Value)?.Name : null); }).OrderByDescending(item => item.DueDate).ToArray());
    Task<SavingsGoal?> ISavingsGoalStore.GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Goals.GetValueOrDefault(id));
    Task<IReadOnlyList<SavingsGoalFactRecord>> ISavingsGoalStore.GetRecordsAsync(bool archived, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SavingsGoalFactRecord>>(Goals.Values.Where(goal => goal.IsArchived == archived).Select(goal => new SavingsGoalFactRecord(
            goal.Id, goal.Name, goal.TargetAmount.AmountMinor, goal.CurrencyCode, goal.TargetDate, goal.DestinationAccountId,
            goal.DestinationAccountId.HasValue ? Accounts.GetValueOrDefault(goal.DestinationAccountId.Value)?.Name : null, goal.IsArchived,
            Contributions.Where(item => item.SavingsGoalId == goal.Id && item.Type == GoalContributionType.Deposit).Sum(item => item.Amount.AmountMinor),
            Contributions.Where(item => item.SavingsGoalId == goal.Id && item.Type == GoalContributionType.Withdrawal).Sum(item => item.Amount.AmountMinor))).ToArray());
    Task<long> IGoalContributionStore.GetAttributedAmountMinorAsync(Guid transactionId, CancellationToken cancellationToken) => Task.FromResult(AttributedAmountMinor);
    Task<IReadOnlyList<GoalContributionHistoryRecord>> IGoalContributionStore.GetForGoalAsync(Guid savingsGoalId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<GoalContributionHistoryRecord>>(Contributions.Where(item => item.SavingsGoalId == savingsGoalId).Select(item =>
        {
            var transaction = Transactions[item.TransactionId];
            return new GoalContributionHistoryRecord(item.Id, item.TransactionId, item.Type, item.Amount.AmountMinor, item.Amount.CurrencyCode,
                transaction.TransactionDate, transaction.Type, transaction.Description,
                transaction.SourceAccountId.HasValue ? Accounts.GetValueOrDefault(transaction.SourceAccountId.Value)?.Name : null,
                transaction.DestinationAccountId.HasValue ? Accounts.GetValueOrDefault(transaction.DestinationAccountId.Value)?.Name : null,
                transaction.CategoryId.HasValue ? Categories.GetValueOrDefault(transaction.CategoryId.Value)?.Name : null);
        }).ToArray());
    Task<IReadOnlyList<GoalContributionCandidateFact>> IGoalContributionStore.GetCandidateFactsAsync(string currencyCode, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<GoalContributionCandidateFact>>(Transactions.Values.Where(item => item.Amount.CurrencyCode == currencyCode).Select(item => new GoalContributionCandidateFact(
            item.Id, item.TransactionDate, item.Type, item.Description,
            item.SourceAccountId.HasValue ? Accounts.GetValueOrDefault(item.SourceAccountId.Value)?.Name : null,
            item.DestinationAccountId.HasValue ? Accounts.GetValueOrDefault(item.DestinationAccountId.Value)?.Name : null,
            item.CategoryId.HasValue ? Categories.GetValueOrDefault(item.CategoryId.Value)?.Name : null,
            item.Amount.AmountMinor, item.Amount.CurrencyCode,
            Contributions.Where(contribution => contribution.TransactionId == item.Id).Sum(contribution => contribution.Amount.AmountMinor))).ToArray());

    Task<IReadOnlyList<OverviewCurrencyFact>> IOverviewStore.GetAccountCurrencyFactsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OverviewCurrencyFact>>(Accounts.Values
            .GroupBy(item => item.CurrencyCode)
            .Select(group => new OverviewCurrencyFact(group.Key, group.Any(item => !item.IsArchived && item.IncludeInAvailableToSpend)))
            .OrderBy(item => item.CurrencyCode)
            .ToArray());

    Task<IReadOnlyList<OverviewAccountBalanceFact>> IOverviewStore.GetAccountBalanceFactsAsync(string currencyCode, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OverviewAccountBalanceFact>>(Accounts.Values.Where(account => account.CurrencyCode == currencyCode).Select(account =>
            new OverviewAccountBalanceFact(account.Id, account.Name, account.IsArchived, account.IncludeInAvailableToSpend, account.OpeningBalance.AmountMinor,
                Transactions.Values.Where(item => item.Type == TransactionType.Income && item.DestinationAccountId == account.Id).Sum(item => item.Amount.AmountMinor),
                Transactions.Values.Where(item => item.Type == TransactionType.Refund && item.DestinationAccountId == account.Id).Sum(item => item.Amount.AmountMinor),
                Transactions.Values.Where(item => item.Type == TransactionType.Transfer && item.DestinationAccountId == account.Id).Sum(item => item.Amount.AmountMinor),
                Transactions.Values.Where(item => item.Type == TransactionType.Expense && item.SourceAccountId == account.Id).Sum(item => item.Amount.AmountMinor),
                Transactions.Values.Where(item => item.Type == TransactionType.Transfer && item.SourceAccountId == account.Id).Sum(item => item.Amount.AmountMinor),
                account.CurrencyCode)).ToArray());

    Task<IReadOnlyList<OverviewRecurringFact>> IOverviewStore.GetUpcomingRecurringAsync(string currencyCode, DateOnly today, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OverviewRecurringFact>>(Occurrences.Values.Where(item => item.Status == RecurringOccurrenceStatus.Pending && item.DueDate >= today)
            .Select(item => (Occurrence: item, Recurring: RecurringTransactions[item.RecurringTransactionId]))
            .Where(item => item.Recurring.Amount.CurrencyCode == currencyCode).OrderBy(item => item.Occurrence.DueDate).Take(limit)
            .Select(item => new OverviewRecurringFact(item.Occurrence.Id, item.Occurrence.DueDate, item.Recurring.Type, item.Recurring.Amount.AmountMinor, item.Recurring.Amount.CurrencyCode, item.Recurring.Description)).ToArray());

    Task<IReadOnlyList<OverviewActivityFact>> IOverviewStore.GetRecentActivityAsync(string currencyCode, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OverviewActivityFact>>(Transactions.Values.Where(item => item.Amount.CurrencyCode == currencyCode)
            .OrderByDescending(item => item.TransactionDate).Take(limit)
            .Select(item => new OverviewActivityFact(item.Id, item.TransactionDate, item.Type, item.Amount.AmountMinor, item.Amount.CurrencyCode, item.Description)).ToArray());

    Task<IReadOnlyList<ReportCategoryFact>> IReportStore.GetCategoryFactsAsync(string currencyCode, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
    {
        if (ReportCategoryFactsOverride is not null) return Task.FromResult(ReportCategoryFactsOverride);
        var rows = Transactions.Values.Where(item => item.Amount.CurrencyCode == currencyCode && item.TransactionDate >= startDate && item.TransactionDate <= endDate && item.Type != TransactionType.Transfer)
            .Select(item => (Item: item, CategoryId: item.Type == TransactionType.Refund ? Transactions[item.OriginalTransactionId!.Value].CategoryId!.Value : item.CategoryId!.Value));
        return Task.FromResult<IReadOnlyList<ReportCategoryFact>>(rows.GroupBy(item => item.CategoryId).Select(group => new ReportCategoryFact(group.Key, Categories[group.Key].Name, Categories[group.Key].IsArchived,
            group.Where(item => item.Item.Type == TransactionType.Income).Sum(item => item.Item.Amount.AmountMinor), group.Where(item => item.Item.Type == TransactionType.Expense).Sum(item => item.Item.Amount.AmountMinor), group.Where(item => item.Item.Type == TransactionType.Refund).Sum(item => item.Item.Amount.AmountMinor))).ToArray());
    }

    Task<IReadOnlyList<ReportAccountMovementFact>> IReportStore.GetAccountMovementFactsAsync(string currencyCode, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
    {
        if (ReportAccountFactsOverride is not null) return Task.FromResult(ReportAccountFactsOverride);
        var rows = Transactions.Values.Where(item => item.Amount.CurrencyCode == currencyCode && item.TransactionDate >= startDate && item.TransactionDate <= endDate);
        return Task.FromResult<IReadOnlyList<ReportAccountMovementFact>>(Accounts.Values.Where(account => account.CurrencyCode == currencyCode).Select(account => new ReportAccountMovementFact(account.Id, account.Name, account.IsArchived,
            rows.Where(item => item.Type == TransactionType.Income && item.DestinationAccountId == account.Id).Sum(item => item.Amount.AmountMinor), rows.Where(item => item.Type == TransactionType.Refund && item.DestinationAccountId == account.Id).Sum(item => item.Amount.AmountMinor), rows.Where(item => item.Type == TransactionType.Transfer && item.DestinationAccountId == account.Id).Sum(item => item.Amount.AmountMinor), rows.Where(item => item.Type == TransactionType.Expense && item.SourceAccountId == account.Id).Sum(item => item.Amount.AmountMinor), rows.Where(item => item.Type == TransactionType.Transfer && item.SourceAccountId == account.Id).Sum(item => item.Amount.AmountMinor)))
            .Where(item => item.IncomeInMinor != 0 || item.RefundInMinor != 0 || item.TransferInMinor != 0 || item.ExpenseOutMinor != 0 || item.TransferOutMinor != 0).ToArray());
    }

    Task<IReadOnlyList<ReportAccountMovementDetailFact>> IReportStore.GetAccountMovementDetailsAsync(string currencyCode, DateOnly startDate, DateOnly endDate, Guid? accountId, CancellationToken cancellationToken)
    {
        var result = new List<ReportAccountMovementDetailFact>();
        foreach (var item in Transactions.Values.Where(item => item.Amount.CurrencyCode == currencyCode && item.TransactionDate >= startDate && item.TransactionDate <= endDate))
        {
            var category = item.Type == TransactionType.Refund ? Categories[Transactions[item.OriginalTransactionId!.Value].CategoryId!.Value].Name : item.CategoryId.HasValue ? Categories[item.CategoryId.Value].Name : null;
            if (item.SourceAccountId.HasValue && (!accountId.HasValue || accountId == item.SourceAccountId)) { var account = Accounts[item.SourceAccountId.Value]; result.Add(new(item.Id, item.TransactionDate, account.Id, account.Name, account.IsArchived, ReportMovementDirection.Outflow, item.Type, item.DestinationAccountId.HasValue ? Accounts[item.DestinationAccountId.Value].Name : null, category, item.Description, item.Amount.AmountMinor, currencyCode)); }
            if (item.DestinationAccountId.HasValue && (!accountId.HasValue || accountId == item.DestinationAccountId)) { var account = Accounts[item.DestinationAccountId.Value]; result.Add(new(item.Id, item.TransactionDate, account.Id, account.Name, account.IsArchived, ReportMovementDirection.Inflow, item.Type, item.SourceAccountId.HasValue ? Accounts[item.SourceAccountId.Value].Name : null, category, item.Description, item.Amount.AmountMinor, currencyCode)); }
        }
        return Task.FromResult<IReadOnlyList<ReportAccountMovementDetailFact>>(result);
    }

    Task ITransactionStore.AddAsync(Transaction transaction, CancellationToken cancellationToken)
    {
        Transactions.Add(transaction.Id, transaction);
        AddedTransactionCount++;
        return Task.CompletedTask;
    }

    Task IAccountStore.AddAsync(Account account, CancellationToken cancellationToken) { Accounts.Add(account.Id, account); return Task.CompletedTask; }
    Task ICategoryStore.AddAsync(Category category, CancellationToken cancellationToken) { Categories.Add(category.Id, category); return Task.CompletedTask; }

    Task IBudgetStore.AddAsync(Budget budget, CancellationToken cancellationToken) { Budgets.Add(budget.Id, budget); return Task.CompletedTask; }
    Task IBudgetAllocationStore.AddAsync(BudgetAllocation allocation, CancellationToken cancellationToken) { Allocations.Add(allocation); return Task.CompletedTask; }
    Task IRecurringTransactionStore.AddAsync(RecurringTransaction recurringTransaction, CancellationToken cancellationToken) { RecurringTransactions.Add(recurringTransaction.Id, recurringTransaction); AddedRecurringTransactionCount++; return Task.CompletedTask; }
    Task IRecurringOccurrenceStore.AddRangeAsync(IReadOnlyCollection<RecurringOccurrence> occurrences, CancellationToken cancellationToken) { foreach (var occurrence in occurrences) Occurrences.Add(occurrence.Id, occurrence); return Task.CompletedTask; }
    Task ISavingsGoalStore.AddAsync(SavingsGoal goal, CancellationToken cancellationToken) { Goals.Add(goal.Id, goal); return Task.CompletedTask; }
    Task IGoalContributionStore.AddAsync(GoalContribution contribution, CancellationToken cancellationToken) { Contributions.Add(contribution); return Task.CompletedTask; }
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) { SaveCount++; return Task.CompletedTask; }
}
