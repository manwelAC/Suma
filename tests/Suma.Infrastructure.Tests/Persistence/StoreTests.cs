using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Suma.Domain.Accounts;
using Suma.Domain.Budgets;
using Suma.Domain.Categories;
using Suma.Domain.Recurring;
using Suma.Domain.Savings;
using Suma.Domain.Transactions;
using Suma.Domain.ValueObjects;
using Suma.Infrastructure.Persistence;
using Suma.Infrastructure.Persistence.Stores;
using Xunit;

namespace Suma.Infrastructure.Tests.Persistence;

public sealed class StoreTests
{
    [Fact]
    public async Task Transaction_queries_return_account_ledger_recent_rows_and_refund_aggregate()
    {
        await using var database = await Database.CreateAsync();
        var account = NewAccount("Wallet");
        var category = new Category("Food", CategoryTransactionKind.Expense);
        var expense = Transaction.CreateExpense(account.Id, category.Id, new Money(1_000, "PHP"), new(2026, 9, 1));
        var refund = Transaction.CreateRefund(account.Id, category.Id, expense.Id, new Money(250, "PHP"), new(2026, 9, 2));
        await database.AddAsync(account, category, expense, refund);
        await using var context = database.Context();
        var store = new TransactionStore(context);
        Assert.Equal(2, (await store.GetForAccountAsync(account.Id, Token)).Count);
        Assert.Single(await store.GetRecentAsync(1, Token));
        Assert.Equal(250, await store.GetRefundedAmountMinorAsync(expense.Id, Token));
    }

    [Fact]
    public async Task Transaction_history_and_refundable_queries_preserve_archived_names()
    {
        await using var database = await Database.CreateAsync();
        var source = NewAccount("Archived wallet");
        var destination = NewAccount("Savings");
        var category = new Category("Food", CategoryTransactionKind.Expense);
        var activeCategory = new Category("Travel", CategoryTransactionKind.Expense);
        var expense = Transaction.CreateExpense(source.Id, category.Id, new Money(1_000, "PHP"), new(2026, 9, 1), "Groceries");
        var refund = Transaction.CreateRefund(destination.Id, category.Id, expense.Id, new Money(250, "PHP"), new(2026, 9, 2));
        var activeExpense = Transaction.CreateExpense(source.Id, activeCategory.Id, new Money(500, "PHP"), new(2026, 8, 31), "Bus");
        var activeRefund = Transaction.CreateRefund(destination.Id, activeCategory.Id, activeExpense.Id, new Money(100, "PHP"), new(2026, 9, 1));
        var transfer = Transaction.CreateTransfer(source.Id, destination.Id, new Money(100, "PHP"), new(2026, 9, 3));
        source.Archive();
        category.Archive();
        await database.AddAsync(source, destination, category, activeCategory, expense, refund, activeExpense, activeRefund, transfer);

        await using var context = database.Context();
        var store = new TransactionStore(context);
        var history = Assert.Single(await store.GetHistoryAsync(TransactionType.Expense, 1, Token));
        Assert.Equal("Archived wallet", history.SourceAccountName);
        Assert.Equal("Food", history.CategoryName);
        var all = await store.GetHistoryAsync(null, 2, Token);
        Assert.Equal(TransactionType.Transfer, all[0].Type);
        Assert.Equal("Savings", all[0].DestinationAccountName);
        var refundable = Assert.Single(await store.GetRefundableExpensesAsync(10, Token));
        Assert.Equal(activeExpense.Id, refundable.Id);
        Assert.Equal(100, refundable.RefundedAmountMinor);
    }

    [Fact]
    public async Task Budget_queries_detect_only_active_overlap_and_duplicate_allocation()
    {
        await using var database = await Database.CreateAsync();
        var category = new Category("Food", CategoryTransactionKind.Expense);
        var active = new Budget("September", new(2026, 9, 1), new(2026, 9, 30), Money.Zero("PHP"));
        var archived = new Budget("October", new(2026, 10, 1), new(2026, 10, 31), Money.Zero("PHP"));
        archived.Archive();
        var allocation = new BudgetAllocation(active.Id, category.Id, new Money(100, "PHP"), false);
        await database.AddAsync(category, active, archived, allocation);
        await using var context = database.Context();
        Assert.True(await new BudgetStore(context).HasActiveOverlapAsync(new(2026, 9, 30), new(2026, 10, 2), cancellationToken: Token));
        Assert.False(await new BudgetStore(context).HasActiveOverlapAsync(new(2026, 10, 1), new(2026, 10, 31), cancellationToken: Token));
        Assert.True(await new BudgetAllocationStore(context).ExistsAsync(active.Id, category.Id, Token));
    }

    [Fact]
    public async Task Budget_reads_and_net_spending_preserve_history_and_refund_original_expenses()
    {
        await using var database = await Database.CreateAsync();
        var food = new Category("Food", CategoryTransactionKind.Expense);
        var child = new Category("Dining", CategoryTransactionKind.Expense, food.Id);
        var account = NewAccount("PHP Wallet");
        var usdAccount = new Account("USD Wallet", AccountType.Bank, Money.Zero("USD"), "USD", true);
        var active = new Budget("September", new(2026, 9, 1), new(2026, 9, 30), new Money(2_000, "PHP"));
        var archived = new Budget("August", new(2026, 8, 1), new(2026, 8, 31), Money.Zero("PHP"));
        archived.Archive();
        var allocation = new BudgetAllocation(active.Id, food.Id, new Money(500, "PHP"), true);
        var inPeriod = Transaction.CreateExpense(account.Id, food.Id, new Money(600, "PHP"), new(2026, 9, 2));
        var outOfPeriod = Transaction.CreateExpense(account.Id, food.Id, new Money(100, "PHP"), new(2026, 8, 31));
        var childExpense = Transaction.CreateExpense(account.Id, child.Id, new Money(200, "PHP"), new(2026, 9, 3));
        var foreignCurrencyExpense = Transaction.CreateExpense(usdAccount.Id, food.Id, new Money(20_000, "USD"), new(2026, 9, 4));
        var refundAfterPeriod = Transaction.CreateRefund(account.Id, food.Id, inPeriod.Id, new Money(25, "PHP"), new(2026, 10, 3));
        food.Archive();
        await database.AddAsync(account, usdAccount, food, child, active, archived, allocation, inPeriod, outOfPeriod, childExpense, foreignCurrencyExpense, refundAfterPeriod);

        await using var context = database.Context();
        var activeBudgets = await new BudgetStore(context).GetAsync(false, Token);
        var archivedBudgets = await new BudgetStore(context).GetAsync(true, Token);
        var allocations = await new BudgetAllocationStore(context).GetForBudgetAsync(active.Id, Token);
        var spending = await new TransactionStore(context).GetNetExpenseAmountsByCategoryAsync(
            active.PeriodStart,
            active.PeriodEnd,
            active.CurrencyCode,
            [food.Id],
            Token);

        Assert.Equal(active.Id, Assert.Single(activeBudgets).Id);
        Assert.Equal(archived.Id, Assert.Single(archivedBudgets).Id);
        Assert.True(Assert.Single(allocations).CategoryArchived);
        Assert.Equal("Food", allocations[0].CategoryName);
        Assert.Equal(575, Assert.Single(spending).AmountMinor);
    }

    [Fact]
    public async Task Goal_query_sums_existing_attribution_and_unit_of_work_saves()
    {
        await using var database = await Database.CreateAsync();
        var account = NewAccount("Savings");
        var category = new Category("Salary", CategoryTransactionKind.Income);
        var transaction = Transaction.CreateIncome(account.Id, category.Id, new Money(1_000, "PHP"), new(2026, 9, 1));
        var goal = new SavingsGoal("Goal", new Money(10_000, "PHP"));
        var first = new GoalContribution(goal.Id, transaction.Id, GoalContributionType.Deposit, new Money(300, "PHP"));
        var second = new GoalContribution(goal.Id, transaction.Id, GoalContributionType.Withdrawal, new Money(200, "PHP"));
        await database.AddAsync(account, category, transaction, goal, first, second);
        await using var context = database.Context();
        Assert.Equal(500, await new GoalContributionStore(context).GetAttributedAmountMinorAsync(transaction.Id, Token));
        context.Accounts.Add(NewAccount("Second"));
        await new EfUnitOfWork(context).SaveChangesAsync(Token);
        Assert.Equal(2, await context.Accounts.CountAsync(Token));
    }

    [Fact]
    public async Task Recurring_reads_preserve_historical_names_states_keys_and_paid_transaction()
    {
        await using var database = await Database.CreateAsync();
        var account = NewAccount("Archived wallet");
        var category = new Category("Bills", CategoryTransactionKind.Expense);
        var recurring = RecurringTransaction.CreateExpense(account.Id, category.Id, new Money(500, "PHP"), RecurrenceFrequencyUnit.Month, 1, new(2026, 9, 5), dayOfMonth: 5, description: "Internet");
        var pending = new RecurringOccurrence(recurring.Id, new(2026, 9, 5));
        var skipped = new RecurringOccurrence(recurring.Id, new(2026, 10, 5)); skipped.Skip();
        var paid = new RecurringOccurrence(recurring.Id, new(2026, 8, 5));
        var transaction = Transaction.CreateExpense(account.Id, category.Id, new Money(500, "PHP"), paid.DueDate, "Internet");
        paid.MarkPaid(transaction.Id);
        account.Archive(); category.Archive();
        await database.AddAsync(account, category, recurring, pending, skipped, paid, transaction);
        await using var context = database.Context();
        var schedules = await new RecurringTransactionStore(context).GetSchedulesAsync(Token);
        var store = new RecurringOccurrenceStore(context);
        var records = await store.GetRecordsAsync(Token);
        var keys = await store.GetExistingKeysAsync([recurring.Id], new(2026, 8, 1), new(2026, 10, 31), Token);
        Assert.Equal("Archived wallet", Assert.Single(schedules).SourceAccountName);
        Assert.Equal("Bills", schedules[0].CategoryName);
        Assert.Contains(records, item => item.Status == RecurringOccurrenceStatus.Pending);
        Assert.Contains(records, item => item.Status == RecurringOccurrenceStatus.Skipped);
        Assert.Contains(records, item => item.Status == RecurringOccurrenceStatus.Paid && item.TransactionId == transaction.Id);
        Assert.Equal(3, keys.Count);
    }

    [Fact]
    public async Task Mutable_occurrence_lookup_is_tracked()
    {
        await using var database = await Database.CreateAsync();
        var account = NewAccount("Wallet");
        var category = new Category("Food", CategoryTransactionKind.Expense);
        var recurring = RecurringTransaction.CreateExpense(account.Id, category.Id, new Money(100, "PHP"), RecurrenceFrequencyUnit.Day, 1, new(2026, 9, 1));
        var occurrence = new RecurringOccurrence(recurring.Id, new(2026, 9, 2));
        await database.AddAsync(account, category, recurring, occurrence);
        await using var context = database.Context();
        var loaded = await new RecurringOccurrenceStore(context).GetByIdAsync(occurrence.Id, Token);
        Assert.NotNull(loaded);
        Assert.Equal(EntityState.Unchanged, context.Entry(loaded).State);
    }

    [Fact]
    public async Task Active_account_read_query_is_no_tracking()
    {
        await using var database = await Database.CreateAsync();
        var active = NewAccount("Active");
        var archived = NewAccount("Archived");
        archived.Archive();
        await database.AddAsync(active, archived);
        await using var context = database.Context();
        var results = await new AccountStore(context).GetActiveAsync(Token);
        Assert.Single(results);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Account_store_persists_create_update_archive_and_restore()
    {
        await using var database = await Database.CreateAsync();
        var account = NewAccount("Wallet");
        await using (var context = database.Context())
        {
            await new AccountStore(context).AddAsync(account, Token);
            await new EfUnitOfWork(context).SaveChangesAsync(Token);
        }

        await using (var context = database.Context())
        {
            var tracked = await new AccountStore(context).GetByIdAsync(account.Id, Token);
            Assert.NotNull(tracked);
            tracked.Rename("Daily wallet");
            tracked.ChangeType(AccountType.EWallet);
            tracked.SetAvailableToSpendInclusion(false);
            tracked.Archive();
            await new EfUnitOfWork(context).SaveChangesAsync(Token);
        }

        await using (var context = database.Context())
        {
            var store = new AccountStore(context);
            Assert.Empty(await store.GetActiveAsync(Token));
            var archived = Assert.Single(await store.GetArchivedAsync(Token));
            Assert.Equal("Daily wallet", archived.Name);
            Assert.Equal(AccountType.EWallet, archived.Type);
            Assert.False(archived.IncludeInAvailableToSpend);
        }

        await using (var context = database.Context())
        {
            var store = new AccountStore(context);
            var tracked = await store.GetByIdAsync(account.Id, Token);
            Assert.NotNull(tracked);
            tracked.Restore();
            await new EfUnitOfWork(context).SaveChangesAsync(Token);
        }

        await using (var context = database.Context())
        {
            Assert.Single(await new AccountStore(context).GetActiveAsync(Token));
        }
    }

    [Fact]
    public async Task Category_store_persists_listing_updates_archive_restore_and_child_lookup()
    {
        await using var database = await Database.CreateAsync();
        var parent = new Category("Living", CategoryTransactionKind.Expense);
        var child = new Category("Food", CategoryTransactionKind.Expense, parent.Id);
        await using (var context = database.Context())
        {
            var store = new CategoryStore(context);
            await store.AddAsync(parent, Token);
            await store.AddAsync(child, Token);
            await new EfUnitOfWork(context).SaveChangesAsync(Token);
        }

        await using (var context = database.Context())
        {
            var store = new CategoryStore(context);
            Assert.Equal(2, (await store.GetAllAsync(Token)).Count);
            Assert.True(await store.HasActiveChildrenAsync(parent.Id, Token));
            var tracked = await store.GetByIdAsync(child.Id, Token);
            Assert.NotNull(tracked);
            tracked.Rename("Groceries");
            tracked.Archive();
            await new EfUnitOfWork(context).SaveChangesAsync(Token);
        }

        await using (var context = database.Context())
        {
            var store = new CategoryStore(context);
            Assert.False(await store.HasActiveChildrenAsync(parent.Id, Token));
            var archived = Assert.Single(await store.GetAllAsync(Token), category => category.IsArchived);
            Assert.Equal("Groceries", archived.Name);
            var tracked = await store.GetByIdAsync(child.Id, Token);
            Assert.NotNull(tracked);
            tracked.Restore();
            await new EfUnitOfWork(context).SaveChangesAsync(Token);
        }

        await using (var context = database.Context())
        {
            Assert.True(await new CategoryStore(context).HasActiveChildrenAsync(parent.Id, Token));
        }
    }

    private static Account NewAccount(string name) => new(name, AccountType.Bank, Money.Zero("PHP"), "PHP", true);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class Database : IAsyncDisposable
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        private Database() => connection.Open();
        public static async Task<Database> CreateAsync()
        {
            var database = new Database();
            await using var context = database.Context();
            await context.Database.MigrateAsync(Token);
            return database;
        }
        public SumaDbContext Context() => new(new DbContextOptionsBuilder<SumaDbContext>().UseSqlite(connection).Options);
        public async Task AddAsync(params object[] entities)
        {
            await using var context = Context();
            context.AddRange(entities);
            await context.SaveChangesAsync(Token);
        }
        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }
}
