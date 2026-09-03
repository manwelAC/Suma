using Suma.Application.Overview.GetOverview;
using Suma.Application.Recurring.EnsureRecurringOccurrences;
using Suma.Application.Tests.TestDoubles;
using Suma.Domain.Accounts;
using Suma.Domain.Budgets;
using Suma.Domain.Categories;
using Suma.Domain.Recurring;
using Suma.Domain.Savings;
using Suma.Domain.Transactions;
using Suma.Domain.ValueObjects;
using Xunit;

namespace Suma.Application.Tests.Overview;

public sealed class GetOverviewUseCaseTests
{
    [Fact]
    public async Task Ats_uses_included_balances_across_ledger_shapes_and_keeps_archived_negative_accounts()
    {
        var data = new FakeData();
        var included = Account(data, "Wallet", 1_000, "PHP", true);
        var archived = Account(data, "Archived", -200, "PHP", true); archived.Archive();
        var excluded = Account(data, "Savings", 500, "PHP", false);
        var income = Category(data, CategoryTransactionKind.Income);
        var expense = Category(data, CategoryTransactionKind.Expense);
        Add(data, Transaction.CreateIncome(included.Id, income.Id, new(500, "PHP"), Today));
        var purchase = Transaction.CreateExpense(excluded.Id, expense.Id, new(300, "PHP"), Today);
        Add(data, purchase);
        Add(data, Transaction.CreateRefund(included.Id, expense.Id, purchase.Id, new(100, "PHP"), Today));
        Add(data, Transaction.CreateTransfer(included.Id, excluded.Id, new(250, "PHP"), Today));

        var result = await UseCase(data).ExecuteAsync("PHP", Token);

        Assert.Equal(1_150, result.IncludedAccountBalanceMinor);
        Assert.Equal(1_600, result.AccountTotalMinor);
        Assert.Equal(1_150, result.AvailableToSpendMinor);
        Assert.Contains(result.Accounts, item => item.IsArchived && item.Included && item.BalanceMinor == -200);
    }

    [Fact]
    public async Task Protected_reserve_reuses_exact_category_spend_and_refunds_and_floors_each_allocation()
    {
        var data = new FakeData();
        var included = Account(data, "Wallet", 2_000, "PHP", true);
        var excluded = Account(data, "Other", 1_000, "PHP", false);
        var food = Category(data, CategoryTransactionKind.Expense);
        var travel = Category(data, CategoryTransactionKind.Expense);
        var budget = new Budget("September", new(2026, 9, 1), new(2026, 9, 30), new Money(99_999, "PHP"));
        data.Budgets.Add(budget.Id, budget);
        data.Allocations.Add(new(budget.Id, food.Id, new Money(600, "PHP"), true));
        data.Allocations.Add(new(budget.Id, travel.Id, new Money(200, "PHP"), false));
        var expense = Transaction.CreateExpense(excluded.Id, food.Id, new Money(500, "PHP"), Today);
        Add(data, expense);
        Add(data, Transaction.CreateRefund(excluded.Id, food.Id, expense.Id, new Money(100, "PHP"), new(2026, 10, 5)));
        Add(data, Transaction.CreateExpense(included.Id, travel.Id, new Money(300, "PHP"), Today));

        var result = await UseCase(data).ExecuteAsync("PHP", Token);

        Assert.Equal(200, result.ProtectedBudgetRemainingMinor);
        Assert.Equal(1_700, result.IncludedAccountBalanceMinor);
        Assert.Equal(1_500, result.AvailableToSpendMinor);
        Assert.Equal(99_999, result.CurrentBudget!.ExpectedIncomeMinor);
    }

    [Fact]
    public async Task Currency_savings_and_pending_recurring_are_informational_only()
    {
        var data = new FakeData();
        Account(data, "PHP", 100, "PHP", true);
        Account(data, "USD", 20_000, "USD", true);
        var goal = new SavingsGoal("Goal", new Money(50_000, "PHP")); data.Goals.Add(goal.Id, goal);

        var php = await UseCase(data).ExecuteAsync("PHP", Token);
        var usd = await UseCase(data).ExecuteAsync("USD", Token);

        Assert.Equal(100, php.AvailableToSpendMinor);
        Assert.Equal(20_000, usd.AvailableToSpendMinor);
        Assert.Single(php.Savings);
        Assert.Empty(usd.Savings);
        Assert.Equal(["PHP", "USD"], php.AvailableCurrencies);
    }

    [Fact]
    public async Task Initial_currency_prefers_active_included_then_ordinal_persisted_fallback()
    {
        var onlyUsd = new FakeData(); Account(onlyUsd, "USD", 100, "USD", true);
        Assert.Equal("USD", (await UseCase(onlyUsd).ExecuteAsync(null, Token)).CurrencyCode);

        var excludedPhp = new FakeData(); Account(excludedPhp, "PHP", 100, "PHP", false); Account(excludedPhp, "USD", 100, "USD", true);
        Assert.Equal("USD", (await UseCase(excludedPhp).ExecuteAsync(null, Token)).CurrencyCode);

        var archivedPhp = new FakeData(); var archived = Account(archivedPhp, "PHP", 100, "PHP", true); archived.Archive(); Account(archivedPhp, "USD", 100, "USD", true);
        Assert.Equal("USD", (await UseCase(archivedPhp).ExecuteAsync(null, Token)).CurrencyCode);

        var noIncluded = new FakeData(); Account(noIncluded, "USD", 100, "USD", false); Account(noIncluded, "PHP", 100, "PHP", false);
        var fallback = await UseCase(noIncluded).ExecuteAsync(null, Token);
        Assert.Equal("PHP", fallback.CurrencyCode);
        Assert.Equal(["PHP", "USD"], fallback.AvailableCurrencies);

        var nonexistent = await UseCase(onlyUsd).ExecuteAsync("PHP", Token);
        Assert.Equal("USD", nonexistent.CurrencyCode);
        Assert.Equal(["USD"], nonexistent.AvailableCurrencies);
    }

    [Fact]
    public async Task No_accounts_returns_safe_empty_overview_without_manufactured_currency()
    {
        var result = await UseCase(new FakeData()).ExecuteAsync(null, Token);
        Assert.Equal(string.Empty, result.CurrencyCode);
        Assert.Empty(result.AvailableCurrencies);
        Assert.Empty(result.Accounts);
    }

    [Fact]
    public async Task Protected_overspend_floors_reserve_while_final_ats_remains_signed()
    {
        var data = new FakeData();
        Account(data, "Included", -100, "PHP", true);
        var payer = Account(data, "Excluded", 1_000, "PHP", false);
        var category = Category(data, CategoryTransactionKind.Expense);
        AddBudget(data, "PHP", category.Id, 500, true);
        Add(data, Transaction.CreateExpense(payer.Id, category.Id, new Money(700, "PHP"), Today));

        var result = await UseCase(data).ExecuteAsync("PHP", Token);
        Assert.Equal(700, result.CurrentBudget!.SpentMinor);
        Assert.Equal(0, result.ProtectedBudgetRemainingMinor);
        Assert.Equal(-100, result.AvailableToSpendMinor);
    }

    [Fact]
    public async Task Boundary_transfer_then_excluded_protected_expense_consumes_reserve_once()
    {
        var data = new FakeData();
        var wallet = Account(data, "Wallet", 10_000, "PHP", true);
        var excluded = Account(data, "Excluded", 0, "PHP", false);
        var category = Category(data, CategoryTransactionKind.Expense);
        AddBudget(data, "PHP", category.Id, 5_000, true);
        Add(data, Transaction.CreateTransfer(wallet.Id, excluded.Id, new Money(2_000, "PHP"), Today));
        Add(data, Transaction.CreateExpense(excluded.Id, category.Id, new Money(2_000, "PHP"), Today));

        var result = await UseCase(data).ExecuteAsync("PHP", Token);
        Assert.Equal(8_000, result.IncludedAccountBalanceMinor);
        Assert.Equal(2_000, result.CurrentBudget!.SpentMinor);
        Assert.Equal(3_000, result.ProtectedBudgetRemainingMinor);
        Assert.Equal(5_000, result.AvailableToSpendMinor);
    }

    [Fact]
    public async Task Protected_expense_and_refund_to_included_account_restore_balance_and_reserve_without_duplication()
    {
        var data = new FakeData();
        var wallet = Account(data, "Wallet", 10_000, "PHP", true);
        var category = Category(data, CategoryTransactionKind.Expense);
        AddBudget(data, "PHP", category.Id, 5_000, true);
        var expense = Transaction.CreateExpense(wallet.Id, category.Id, new Money(2_000, "PHP"), Today); Add(data, expense);
        Add(data, Transaction.CreateRefund(wallet.Id, category.Id, expense.Id, new Money(500, "PHP"), new(2026, 10, 2)));

        var result = await UseCase(data).ExecuteAsync("PHP", Token);
        Assert.Equal(8_500, result.IncludedAccountBalanceMinor);
        Assert.Equal(1_500, result.CurrentBudget!.SpentMinor);
        Assert.Equal(3_500, result.ProtectedBudgetRemainingMinor);
        Assert.Equal(5_000, result.AvailableToSpendMinor);
    }

    [Fact]
    public async Task Pending_recurring_is_upcoming_only_and_foreign_budget_never_affects_selected_currency()
    {
        var data = new FakeData();
        var php = Account(data, "PHP", 1_000, "PHP", true);
        var usd = Account(data, "USD", 20_000, "USD", true);
        var phpExpense = Category(data, CategoryTransactionKind.Expense);
        var usdExpense = Category(data, CategoryTransactionKind.Expense);
        AddBudget(data, "PHP", phpExpense.Id, 300, true);
        AddBudget(data, "USD", usdExpense.Id, 5_000, true);
        Add(data, Transaction.CreateExpense(usd.Id, usdExpense.Id, new Money(2_000, "USD"), Today));
        var recurring = RecurringTransaction.CreateExpense(php.Id, phpExpense.Id, new Money(900, "PHP"), RecurrenceFrequencyUnit.Month, 1, Today, dayOfMonth: Today.Day, description: "Pending bill");
        data.RecurringTransactions.Add(recurring.Id, recurring);
        var occurrence = new RecurringOccurrence(recurring.Id, Today); data.Occurrences.Add(occurrence.Id, occurrence);

        var phpResult = await UseCase(data).ExecuteAsync("PHP", Token);
        var usdResult = await UseCase(data).ExecuteAsync("USD", Token);
        Assert.Equal(300, phpResult.ProtectedBudgetRemainingMinor);
        Assert.Equal(700, phpResult.AvailableToSpendMinor);
        Assert.Equal(3_000, usdResult.ProtectedBudgetRemainingMinor);
        Assert.Equal(15_000, usdResult.AvailableToSpendMinor);
        Assert.Contains(phpResult.Upcoming, item => item.Id == occurrence.Id && item.AmountMinor == 900);
    }

    private static readonly DateOnly Today = new(2026, 9, 10);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static GetOverviewUseCase UseCase(FakeData data)
    {
        var clock = new FakeDateProvider(Today);
        return new(data, data, data, data, data, new EnsureRecurringOccurrencesUseCase(data, data, data, clock), clock);
    }

    private static Account Account(FakeData data, string name, long opening, string currency, bool included)
    {
        var account = new Account(name, AccountType.Cash, new Money(opening, currency), currency, included);
        data.Accounts.Add(account.Id, account);
        return account;
    }

    private static Category Category(FakeData data, CategoryTransactionKind kind)
    {
        var category = new Category(Guid.NewGuid().ToString(), kind); data.Categories.Add(category.Id, category); return category;
    }

    private static void Add(FakeData data, Transaction transaction) => data.Transactions.Add(transaction.Id, transaction);

    private static Budget AddBudget(FakeData data, string currency, Guid categoryId, long allocation, bool reserve)
    {
        var budget = new Budget($"{currency} Budget", new(2026, 9, 1), new(2026, 9, 30), Money.Zero(currency));
        data.Budgets.Add(budget.Id, budget);
        data.Allocations.Add(new(budget.Id, categoryId, new Money(allocation, currency), reserve));
        return budget;
    }
}
