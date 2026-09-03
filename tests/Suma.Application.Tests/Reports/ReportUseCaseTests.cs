using System.Text;
using Suma.Application.Budgets.GetBudgetDetails;
using Suma.Application.Budgets.GetBudgets;
using Suma.Application.Reports.Csv;
using Suma.Application.Reports.GetAccountMovementDetail;
using Suma.Application.Reports.GetFinancialReport;
using Suma.Application.Reports.GetReportOptions;
using Suma.Application.Tests.TestDoubles;
using Suma.Domain.Accounts;
using Suma.Domain.Budgets;
using Suma.Domain.Categories;
using Suma.Domain.Transactions;
using Suma.Domain.Recurring;
using Suma.Domain.Savings;
using Suma.Domain.ValueObjects;
using Xunit;

namespace Suma.Application.Tests.Reports;

public sealed class ReportUseCaseTests
{
    [Fact]
    public async Task Cash_flow_is_inclusive_currency_isolated_and_excludes_transfers()
    {
        var data = Data(out var wallet, out var other, out var income, out var expense);
        Add(data, Transaction.CreateIncome(wallet.Id, income.Id, new(1_000, "PHP"), new(2026, 9, 1)));
        Add(data, Transaction.CreateExpense(wallet.Id, expense.Id, new(600, "PHP"), new(2026, 9, 30)));
        Add(data, Transaction.CreateTransfer(wallet.Id, other.Id, new(200, "PHP"), new(2026, 9, 15)));
        var usd = new Account("USD", AccountType.Cash, Money.Zero("USD"), "USD", true); data.Accounts.Add(usd.Id, usd);
        Add(data, Transaction.CreateIncome(usd.Id, income.Id, new(90_000, "USD"), new(2026, 9, 10)));
        var result = await UseCase(data).ExecuteAsync(new("PHP", new(2026, 9, 1), new(2026, 9, 30)), Token);
        Assert.Equal((1_000, 600, 0, 600, 400), (result.CashFlow.GrossIncomeMinor, result.CashFlow.GrossExpenseMinor, result.CashFlow.RefundMinor, result.CashFlow.NetExpenseMinor, result.CashFlow.NetCashFlowMinor));
        Assert.Equal(200, result.AccountMovements.Single(item => item.AccountId == wallet.Id).NetMovementMinor);
        Assert.Equal(200, result.AccountMovements.Single(item => item.AccountId == other.Id).NetMovementMinor);
        Assert.Equal(["Other", "Wallet"], result.AccountMovements.Select(item => item.AccountName));
    }

    [Fact]
    public async Task Refund_uses_own_date_and_original_expense_category()
    {
        var data = Data(out var wallet, out _, out _, out var food); var otherCategory = new Category("Other", CategoryTransactionKind.Expense); data.Categories.Add(otherCategory.Id, otherCategory);
        var oldExpense = Transaction.CreateExpense(wallet.Id, food.Id, new(1_000, "PHP"), new(2026, 8, 1)); Add(data, oldExpense);
        Add(data, Transaction.CreateRefund(wallet.Id, otherCategory.Id, oldExpense.Id, new(250, "PHP"), new(2026, 9, 10), "partial"));
        var result = await UseCase(data).ExecuteAsync(new("PHP", new(2026, 9, 1), new(2026, 9, 30)), Token);
        Assert.Equal(-250, result.CashFlow.NetExpenseMinor); Assert.Equal(250, result.CashFlow.NetCashFlowMinor);
        Assert.Equal(food.Id, Assert.Single(result.ExpenseCategories).CategoryId);
        Assert.Equal(-250, result.ExpenseCategories[0].NetExpenseMinor);
    }

    [Fact]
    public async Task Detail_is_account_relative_deterministic_and_preserves_archived_names()
    {
        var data = Data(out var wallet, out var other, out _, out _); wallet.Archive(); Add(data, Transaction.CreateTransfer(wallet.Id, other.Id, new(500, "PHP"), new(2026, 9, 2), "move"));
        var rows = await new GetAccountMovementDetailUseCase(data).ExecuteAsync(new("PHP", new(2026, 9, 1), new(2026, 9, 30)), Token);
        Assert.Equal(2, rows.Count); Assert.Contains(rows, item => item.AccountArchived && item.Direction == Application.Abstractions.Persistence.ReportMovementDirection.Outflow && item.Counterparty == other.Name); Assert.Contains(rows, item => item.Direction == Application.Abstractions.Persistence.ReportMovementDirection.Inflow && item.Counterparty == wallet.Name);
    }

    [Fact]
    public async Task Options_use_m16_currency_and_current_budget_policy()
    {
        var data = Data(out _, out _, out _, out _); var usd = new Account("USD", AccountType.Cash, Money.Zero("USD"), "USD", true); data.Accounts.Add(usd.Id, usd); foreach (var account in data.Accounts.Values.Where(item => item.CurrencyCode == "PHP")) account.SetAvailableToSpendInclusion(false);
        var current = new Budget("Current", new(2026, 9, 1), new(2026, 9, 30), Money.Zero("USD")); data.Budgets.Add(current.Id, current);
        var options = await new GetReportOptionsUseCase(data, data, new FakeDateProvider(new(2026, 9, 10))).ExecuteAsync(Token);
        Assert.Equal("USD", options.SelectedCurrency); Assert.Equal(current.Id, options.SelectedBudgetId);
    }

    [Fact]
    public void Csv_is_bom_crlf_exact_escaped_deterministic_and_empty_rules_hold()
    {
        var report = new FinancialReportResult("PHP", new(2026, 9, 1), new(2026, 9, 30), new(0, 0, 0, 0, 0), [new(Guid.NewGuid(), "Food, \"Home\"\n食", true, 0, 100, -100)], [], []);
        var csv = new ReportCsvSerializer(); var first = csv.ExpenseCategories(report); var second = csv.ExpenseCategories(report);
        Assert.Equal([0xEF, 0xBB, 0xBF], first.Take(3)); Assert.Equal(first, second); var text = Encoding.UTF8.GetString(first);
        Assert.Contains("\r\n", text); Assert.Contains("\"Food, \"\"Home\"\"\n食\"", text); Assert.Contains("-1.00,-100", text); Assert.DoesNotContain(report.ExpenseCategories[0].CategoryId.ToString(), text);
        var emptyCash = Encoding.UTF8.GetString(csv.CashFlow(report with { ExpenseCategories = [] })); Assert.Equal(3, emptyCash.Split("\r\n").Length);
        var emptyIncome = Encoding.UTF8.GetString(csv.IncomeCategories(report)); Assert.Equal(2, emptyIncome.Split("\r\n").Length);
        var detail = new AccountMovementDetailRow(Guid.NewGuid(), report.StartDate, Guid.NewGuid(), "Wallet", false, Application.Abstractions.Persistence.ReportMovementDirection.Inflow, TransactionType.Income, null, "Salary", null, 100, "PHP");
        Assert.StartsWith("\uFEFFDate,Account,AccountArchived,Direction,Type,Counterparty,Category,Description,Amount,AmountMinor,Currency\r\n", Encoding.UTF8.GetString(csv.AccountMovement([detail])));
        var budget = new BudgetDetails(new BudgetSummary(Guid.NewGuid(), "Budget", report.StartDate, report.EndDate, 0, "PHP", false), 0, 0, 0, []);
        Assert.StartsWith("\uFEFFBudget,PeriodStart,PeriodEnd,Category,CategoryArchived,Allocation,AllocationMinor,Spent,SpentMinor,Remaining,RemainingMinor,UtilizationPercent,Currency,ReserveFromAvailable\r\n", Encoding.UTF8.GetString(csv.BudgetPerformance(budget)));
        Assert.Equal("suma-cash-flow-PHP-20260901-20260930.csv", ReportCsvSerializer.GeneralFileName("cash-flow", "PHP", report.StartDate, report.EndDate));
    }

    [Fact]
    public async Task Invalid_range_is_rejected()
    {
        var data = Data(out _, out _, out _, out _); await Assert.ThrowsAnyAsync<Exception>(() => UseCase(data).ExecuteAsync(new("PHP", new(2026, 9, 2), new(2026, 9, 1)), Token));
    }

    [Fact]
    public async Task Empty_same_day_future_and_planning_only_data_report_zero_actual_activity()
    {
        var data = Data(out var wallet, out _, out _, out var expense);
        var recurring = RecurringTransaction.CreateExpense(wallet.Id, expense.Id, new Money(500, "PHP"), RecurrenceFrequencyUnit.Month, 1, new(2027, 1, 1), dayOfMonth: 1); data.RecurringTransactions.Add(recurring.Id, recurring);
        var pending = new RecurringOccurrence(recurring.Id, new(2027, 1, 1)); data.Occurrences.Add(pending.Id, pending); var skipped = new RecurringOccurrence(recurring.Id, new(2027, 2, 1)); skipped.Skip(); data.Occurrences.Add(skipped.Id, skipped);
        var goal = new SavingsGoal("Goal", new Money(10_000, "PHP")); data.Goals.Add(goal.Id, goal); var budget = new Budget("Future", new(2027, 1, 1), new(2027, 1, 31), new Money(99_999, "PHP")); data.Budgets.Add(budget.Id, budget);
        var result = await UseCase(data).ExecuteAsync(new("PHP", new(2027, 1, 1), new(2027, 1, 1)), Token);
        Assert.Equal(new CashFlowSummary(0, 0, 0, 0, 0), result.CashFlow); Assert.Empty(result.ExpenseCategories); Assert.Empty(result.AccountMovements);
    }

    [Fact]
    public async Task Refund_outside_range_does_not_change_general_report()
    {
        var data = Data(out var wallet, out _, out _, out var category); var expense = Transaction.CreateExpense(wallet.Id, category.Id, new Money(500, "PHP"), new(2026, 9, 1)); Add(data, expense); Add(data, Transaction.CreateRefund(wallet.Id, category.Id, expense.Id, new Money(200, "PHP"), new(2026, 10, 1)));
        var result = await UseCase(data).ExecuteAsync(new("PHP", new(2026, 9, 1), new(2026, 9, 30)), Token); Assert.Equal((500, 0, 500), (result.CashFlow.GrossExpenseMinor, result.CashFlow.RefundMinor, result.CashFlow.NetExpenseMinor));
    }

    [Fact]
    public async Task Checked_aggregation_rejects_component_and_account_overflow()
    {
        var data = Data(out var wallet, out _, out _, out var category);
        data.ReportCategoryFactsOverride = [new(category.Id, category.Name, false, long.MaxValue, 0, 0), new(Guid.NewGuid(), "Second", false, 1, 0, 0)];
        await Assert.ThrowsAsync<OverflowException>(() => UseCase(data).ExecuteAsync(new("PHP", new(2026, 9, 1), new(2026, 9, 1)), Token));
        data.ReportCategoryFactsOverride = [];
        data.ReportAccountFactsOverride = [new(wallet.Id, wallet.Name, false, long.MaxValue, 1, 0, 0, 0)];
        await Assert.ThrowsAsync<OverflowException>(() => UseCase(data).ExecuteAsync(new("PHP", new(2026, 9, 1), new(2026, 9, 1)), Token));
    }

    [Fact]
    public async Task Checked_net_formulas_reject_overflow()
    {
        var data = Data(out _, out _, out _, out var category); data.ReportAccountFactsOverride = [];
        data.ReportCategoryFactsOverride = [new(category.Id, category.Name, false, 0, long.MaxValue, -1)];
        await Assert.ThrowsAsync<OverflowException>(() => UseCase(data).ExecuteAsync(new("PHP", new(2026, 9, 1), new(2026, 9, 1)), Token));
        data.ReportCategoryFactsOverride = [new(category.Id, category.Name, false, long.MaxValue, 0, 1)];
        await Assert.ThrowsAsync<OverflowException>(() => UseCase(data).ExecuteAsync(new("PHP", new(2026, 9, 1), new(2026, 9, 1)), Token));
    }

    private static GetFinancialReportUseCase UseCase(FakeData data) => new(data, data);
    private static FakeData Data(out Account wallet, out Account other, out Category income, out Category expense) { var data = new FakeData(); wallet = new("Wallet", AccountType.Cash, Money.Zero("PHP"), "PHP", true); other = new("Other", AccountType.Bank, Money.Zero("PHP"), "PHP", true); income = new("Salary", CategoryTransactionKind.Income); expense = new("Food", CategoryTransactionKind.Expense); data.Accounts.Add(wallet.Id, wallet); data.Accounts.Add(other.Id, other); data.Categories.Add(income.Id, income); data.Categories.Add(expense.Id, expense); return data; }
    private static void Add(FakeData data, Transaction item) => data.Transactions.Add(item.Id, item);
    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
