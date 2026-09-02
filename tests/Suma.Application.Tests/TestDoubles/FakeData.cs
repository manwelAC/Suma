using Suma.Application.Abstractions.Persistence;
using Suma.Domain.Accounts;
using Suma.Domain.Budgets;
using Suma.Domain.Categories;
using Suma.Domain.Recurring;
using Suma.Domain.Savings;
using Suma.Domain.Transactions;

namespace Suma.Application.Tests.TestDoubles;

internal sealed class FakeData : IAccountStore, ICategoryStore, ITransactionStore, IBudgetStore, IBudgetAllocationStore, IRecurringTransactionStore, IRecurringOccurrenceStore, ISavingsGoalStore, IGoalContributionStore, IUnitOfWork
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
    Task<long> IGoalContributionStore.GetAttributedAmountMinorAsync(Guid transactionId, CancellationToken cancellationToken) => Task.FromResult(AttributedAmountMinor);

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
