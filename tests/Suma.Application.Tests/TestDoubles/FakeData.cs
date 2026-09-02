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
    Task<Budget?> IBudgetStore.GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Budgets.GetValueOrDefault(id));
    Task<bool> IBudgetStore.HasActiveOverlapAsync(DateOnly periodStart, DateOnly periodEnd, Guid? excludingBudgetId, CancellationToken cancellationToken) => Task.FromResult(HasOverlap);
    Task<bool> IBudgetAllocationStore.ExistsAsync(Guid budgetId, Guid categoryId, CancellationToken cancellationToken) => Task.FromResult(Allocations.Any(allocation => allocation.BudgetId == budgetId && allocation.CategoryId == categoryId));
    Task<RecurringTransaction?> IRecurringTransactionStore.GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(RecurringTransactions.GetValueOrDefault(id));
    Task<RecurringOccurrence?> IRecurringOccurrenceStore.GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Occurrences.GetValueOrDefault(id));
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
    Task ISavingsGoalStore.AddAsync(SavingsGoal goal, CancellationToken cancellationToken) { Goals.Add(goal.Id, goal); return Task.CompletedTask; }
    Task IGoalContributionStore.AddAsync(GoalContribution contribution, CancellationToken cancellationToken) { Contributions.Add(contribution); return Task.CompletedTask; }
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) { SaveCount++; return Task.CompletedTask; }
}
