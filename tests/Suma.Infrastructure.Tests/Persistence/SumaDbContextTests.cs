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
using Xunit;

namespace Suma.Infrastructure.Tests.Persistence;

public sealed class SumaDbContextTests
{
    [Fact]
    public void Model_contains_the_nine_expected_tables()
    {
        using var database = new TestDatabase();
        using var context = database.CreateContext();

        var tables = context.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Where(table => table is not null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(
            [
                "accounts",
                "budget_allocations",
                "budgets",
                "categories",
                "goal_contributions",
                "recurring_occurrences",
                "recurring_transactions",
                "savings_goals",
                "transactions"
            ],
            tables);
    }

    [Fact]
    public async Task Initial_migration_creates_a_fresh_database()
    {
        await using var database = new TestDatabase();
        await using var context = database.CreateContext();

        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var migrations = await context.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);
        Assert.Contains(migrations, migration => migration.EndsWith("_InitialFinancialSchema", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Account_round_trips_through_a_new_context()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        var account = new Account("Wallet", AccountType.Cash, new Money(12_345, "php"), "PHP", true);
        account.Archive();

        await SaveAsync(database, account);

        await using var readContext = database.CreateContext();
        var loaded = await readContext.Accounts.SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(account.Id, loaded.Id);
        Assert.Equal("Wallet", loaded.Name);
        Assert.Equal(AccountType.Cash, loaded.Type);
        Assert.Equal(12_345, loaded.OpeningBalance.AmountMinor);
        Assert.Equal("PHP", loaded.OpeningBalance.CurrencyCode);
        Assert.Equal("PHP", loaded.CurrencyCode);
        Assert.True(loaded.IncludeInAvailableToSpend);
        Assert.True(loaded.IsArchived);
    }

    [Fact]
    public async Task Parent_and_child_categories_round_trip()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        var parent = new Category("Housing", CategoryTransactionKind.Expense);
        var child = new Category("Rent", CategoryTransactionKind.Expense, parent.Id, "home", 2, true);

        await using (var writeContext = database.CreateContext())
        {
            writeContext.Categories.AddRange(parent, child);
            await writeContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var readContext = database.CreateContext();
        var loaded = await readContext.Categories.SingleAsync(
            category => category.Id == child.Id,
            TestContext.Current.CancellationToken);

        Assert.Equal(parent.Id, loaded.ParentCategoryId);
        Assert.Equal("Rent", loaded.Name);
        Assert.Equal(CategoryTransactionKind.Expense, loaded.TransactionKind);
        Assert.Equal("home", loaded.IconKey);
        Assert.Equal(2, loaded.SortOrder);
        Assert.True(loaded.IsSystem);
    }

    [Fact]
    public async Task Expense_income_transfer_and_refund_round_trip()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        var source = NewAccount("Source");
        var destination = NewAccount("Destination");
        var expenseCategory = new Category("Food", CategoryTransactionKind.Expense);
        var incomeCategory = new Category("Salary", CategoryTransactionKind.Income);
        var date = new DateOnly(2026, 8, 20);
        var expense = Transaction.CreateExpense(source.Id, expenseCategory.Id, new Money(500, "PHP"), date, "Meal", "Note");
        var income = Transaction.CreateIncome(destination.Id, incomeCategory.Id, new Money(2_000, "PHP"), date);
        var transfer = Transaction.CreateTransfer(source.Id, destination.Id, new Money(700, "PHP"), date);
        var refund = Transaction.CreateRefund(destination.Id, expenseCategory.Id, expense.Id, new Money(200, "PHP"), date);

        await using (var writeContext = database.CreateContext())
        {
            writeContext.AddRange(source, destination, expenseCategory, incomeCategory);
            writeContext.Transactions.AddRange(expense, income, transfer, refund);
            await writeContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var readContext = database.CreateContext();
        var loaded = await readContext.Transactions
            .OrderBy(transaction => transaction.Type)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(4, loaded.Count);
        AssertTransaction(loaded.Single(transaction => transaction.Type == TransactionType.Expense), expense);
        AssertTransaction(loaded.Single(transaction => transaction.Type == TransactionType.Income), income);
        AssertTransaction(loaded.Single(transaction => transaction.Type == TransactionType.Transfer), transfer);
        AssertTransaction(loaded.Single(transaction => transaction.Type == TransactionType.Refund), refund);
    }

    [Fact]
    public async Task Budget_and_allocation_round_trip()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        var category = new Category("Food", CategoryTransactionKind.Expense);
        var budget = new Budget("September", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), new Money(50_000, "PHP"));
        var allocation = new BudgetAllocation(budget.Id, category.Id, new Money(8_000, "PHP"), true);

        await using (var writeContext = database.CreateContext())
        {
            writeContext.AddRange(category, budget, allocation);
            await writeContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var readContext = database.CreateContext();
        var loadedBudget = await readContext.Budgets.SingleAsync(TestContext.Current.CancellationToken);
        var loadedAllocation = await readContext.BudgetAllocations.SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(budget.Id, loadedBudget.Id);
        Assert.Equal(50_000, loadedBudget.ExpectedIncome.AmountMinor);
        Assert.Equal("PHP", loadedBudget.CurrencyCode);
        Assert.Equal(allocation.Id, loadedAllocation.Id);
        Assert.Equal(8_000, loadedAllocation.Amount.AmountMinor);
        Assert.True(loadedAllocation.ReserveFromAvailable);
    }

    [Fact]
    public async Task Duplicate_budget_allocation_is_rejected()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        var category = new Category("Food", CategoryTransactionKind.Expense);
        var budget = new Budget("September", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), Money.Zero("PHP"));

        await using var context = database.CreateContext();
        context.AddRange(
            category,
            budget,
            new BudgetAllocation(budget.Id, category.Id, new Money(100, "PHP"), false),
            new BudgetAllocation(budget.Id, category.Id, new Money(200, "PHP"), true));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Recurring_transaction_and_paid_occurrence_round_trip()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        var account = NewAccount("Wallet");
        var category = new Category("Rent", CategoryTransactionKind.Expense);
        var recurring = RecurringTransaction.CreateExpense(
            account.Id,
            category.Id,
            new Money(15_000, "PHP"),
            RecurrenceFrequencyUnit.Month,
            2,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31),
            dayOfMonth: 31,
            description: "Rent");
        recurring.Deactivate();
        var transaction = Transaction.CreateExpense(account.Id, category.Id, new Money(15_000, "PHP"), new DateOnly(2026, 3, 31));
        var occurrence = new RecurringOccurrence(recurring.Id, new DateOnly(2026, 3, 31));
        occurrence.MarkPaid(transaction.Id);

        await using (var writeContext = database.CreateContext())
        {
            writeContext.AddRange(account, category, recurring, transaction, occurrence);
            await writeContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var readContext = database.CreateContext();
        var loadedRecurring = await readContext.RecurringTransactions.SingleAsync(TestContext.Current.CancellationToken);
        var loadedOccurrence = await readContext.RecurringOccurrences.SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(recurring.Id, loadedRecurring.Id);
        Assert.Equal(RecurrenceFrequencyUnit.Month, loadedRecurring.FrequencyUnit);
        Assert.Equal(2, loadedRecurring.IntervalCount);
        Assert.Equal(31, loadedRecurring.DayOfMonth);
        Assert.Equal(new DateOnly(2026, 12, 31), loadedRecurring.EndDate);
        Assert.False(loadedRecurring.IsActive);
        Assert.Equal(RecurringOccurrenceStatus.Paid, loadedOccurrence.Status);
        Assert.Equal(transaction.Id, loadedOccurrence.TransactionId);
    }

    [Fact]
    public async Task Duplicate_recurring_occurrence_is_rejected()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        var account = NewAccount("Wallet");
        var category = new Category("Rent", CategoryTransactionKind.Expense);
        var recurring = RecurringTransaction.CreateExpense(
            account.Id,
            category.Id,
            new Money(100, "PHP"),
            RecurrenceFrequencyUnit.Day,
            1,
            new DateOnly(2026, 1, 1));
        var dueDate = new DateOnly(2026, 1, 2);

        await using var context = database.CreateContext();
        context.AddRange(
            account,
            category,
            recurring,
            new RecurringOccurrence(recurring.Id, dueDate),
            new RecurringOccurrence(recurring.Id, dueDate));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Savings_goal_and_contribution_round_trip()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        var account = NewAccount("Savings");
        var category = new Category("Salary", CategoryTransactionKind.Income);
        var transaction = Transaction.CreateIncome(account.Id, category.Id, new Money(5_000, "PHP"), new DateOnly(2026, 9, 1));
        var goal = new SavingsGoal("Emergency Fund", new Money(100_000, "PHP"), new DateOnly(2027, 9, 1), account.Id);
        goal.Archive();
        var contribution = new GoalContribution(goal.Id, transaction.Id, GoalContributionType.Deposit, new Money(5_000, "PHP"));

        await using (var writeContext = database.CreateContext())
        {
            writeContext.AddRange(account, category, transaction, goal, contribution);
            await writeContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var readContext = database.CreateContext();
        var loadedGoal = await readContext.SavingsGoals.SingleAsync(TestContext.Current.CancellationToken);
        var loadedContribution = await readContext.GoalContributions.SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(goal.Id, loadedGoal.Id);
        Assert.Equal(100_000, loadedGoal.TargetAmount.AmountMinor);
        Assert.Equal("PHP", loadedGoal.CurrencyCode);
        Assert.Equal(account.Id, loadedGoal.DestinationAccountId);
        Assert.True(loadedGoal.IsArchived);
        Assert.Equal(contribution.Id, loadedContribution.Id);
        Assert.Equal(transaction.Id, loadedContribution.TransactionId);
        Assert.Equal(GoalContributionType.Deposit, loadedContribution.Type);
        Assert.Equal(5_000, loadedContribution.Amount.AmountMinor);
    }

    [Fact]
    public async Task Invalid_foreign_key_is_rejected()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        await using var context = database.CreateContext();
        context.BudgetAllocations.Add(new BudgetAllocation(Guid.NewGuid(), Guid.NewGuid(), new Money(100, "PHP"), false));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Deleting_referenced_account_does_not_cascade_financial_history()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        var account = NewAccount("Wallet");
        var category = new Category("Food", CategoryTransactionKind.Expense);
        var transaction = Transaction.CreateExpense(account.Id, category.Id, new Money(100, "PHP"), new DateOnly(2026, 9, 1));

        await using (var writeContext = database.CreateContext())
        {
            writeContext.AddRange(account, category, transaction);
            await writeContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var deleteContext = database.CreateContext();
        deleteContext.Accounts.Remove(await deleteContext.Accounts.SingleAsync(TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<DbUpdateException>(
            () => deleteContext.SaveChangesAsync(TestContext.Current.CancellationToken));

        await using var verifyContext = database.CreateContext();
        Assert.Equal(1, await verifyContext.Accounts.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await verifyContext.Transactions.CountAsync(TestContext.Current.CancellationToken));
    }

    private static Account NewAccount(string name) =>
        new(name, AccountType.Bank, Money.Zero("PHP"), "PHP", true);

    private static async Task SaveAsync(TestDatabase database, object entity)
    {
        await using var context = database.CreateContext();
        context.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static void AssertTransaction(Transaction actual, Transaction expected)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Type, actual.Type);
        Assert.Equal(expected.SourceAccountId, actual.SourceAccountId);
        Assert.Equal(expected.DestinationAccountId, actual.DestinationAccountId);
        Assert.Equal(expected.CategoryId, actual.CategoryId);
        Assert.Equal(expected.OriginalTransactionId, actual.OriginalTransactionId);
        Assert.Equal(expected.Amount, actual.Amount);
        Assert.Equal(expected.TransactionDate, actual.TransactionDate);
        Assert.Equal(expected.Description, actual.Description);
        Assert.Equal(expected.Notes, actual.Notes);
    }

    private sealed class TestDatabase : IAsyncDisposable, IDisposable
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");

        public TestDatabase()
        {
            connection.Open();
        }

        public static async Task<TestDatabase> CreateMigratedAsync()
        {
            var database = new TestDatabase();
            await using var context = database.CreateContext();
            await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
            return database;
        }

        public SumaDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<SumaDbContext>()
                .UseSqlite(connection)
                .Options;

            return new SumaDbContext(options);
        }

        public void Dispose() => connection.Dispose();

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }
}
