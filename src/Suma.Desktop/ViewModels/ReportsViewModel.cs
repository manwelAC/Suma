using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Suma.Application.Budgets.GetBudgetDetails;
using Suma.Application.Reports.Csv;
using Suma.Application.Reports.GetAccountMovementDetail;
using Suma.Application.Reports.GetFinancialReport;
using Suma.Application.Reports.GetReportOptions;
using Suma.Desktop.Operations.Reports;
using Windows.UI;
using Suma.Domain.Transactions;
using Suma.Application.Abstractions.Persistence;

namespace Suma.Desktop.ViewModels;

public enum ReportSection { CashFlow, Categories, Accounts, Budget }
public enum ReportDatePreset { MonthToDate, LastMonth, Last30Days, YearToDate, Last3Months, LastYear, Custom }
public enum ReportCategoryMode { Expense, Income }
public sealed record ReportExport(string FileName, byte[] Content);

public sealed class ReportsViewModel(IReportOperations operations) : ObservableObject
{
    private static readonly SolidColorBrush GreenBrush = new(Color.FromArgb(255, 38, 78, 54)); // #264E36
    private static readonly SolidColorBrush RedBrush = new(Color.FromArgb(255, 198, 40, 40));   // #C62828

    private static readonly (string Hex, Color Color)[] DonutPalette =
    [
        ("#264E36", Color.FromArgb(255, 38, 78, 54)),
        ("#E76F51", Color.FromArgb(255, 231, 111, 81)),
        ("#2A9D8F", Color.FromArgb(255, 42, 157, 143)),
        ("#E9C46A", Color.FromArgb(255, 233, 196, 106)),
        ("#457B9D", Color.FromArgb(255, 69, 123, 157)),
        ("#F4A261", Color.FromArgb(255, 244, 162, 97)),
        ("#5B8A68", Color.FromArgb(255, 91, 138, 104)),
        ("#C06C84", Color.FromArgb(255, 192, 108, 132)),
        ("#1D3557", Color.FromArgb(255, 29, 53, 87)),
        ("#6C5B7B", Color.FromArgb(255, 108, 91, 123))
    ];

    private readonly object sync = new();
    private Task? activeLoad;
    private bool reload;
    private long version;
    private CancellationToken token;
    private long detailVersion;
    private long budgetVersion;

    private DateOnly today;
    private string currency = string.Empty;
    private DateOnly startDate;
    private DateOnly endDate;
    private bool loading;
    private bool exporting;
    private string? error;
    private FinancialReportResult? report;
    private (string, DateOnly, DateOnly)? appliedKey;
    private (string, DateOnly, DateOnly)? detailAppliedKey;
    private BudgetDetails? budget;
    private Guid? selectedBudgetId;

    private IReadOnlyList<AccountMovementDetailRow>? cachedTimelineDetails;
    private CashFlowGranularity granularity = CashFlowGranularity.Daily;
    private CashFlowBreakdownMode breakdownMode = CashFlowBreakdownMode.ByCategory;
    private PeriodComparisonSummary? comparison;
    private string totalExpenseFormatted = string.Empty;
    private string breakdownTotalIncomeFormatted = string.Empty;
    private string breakdownTotalExpenseFormatted = string.Empty;
    private string breakdownTotalNetFormatted = string.Empty;
    private bool breakdownTotalNetIsPositive = true;
    private bool hasTimelineActivity;
    private bool hasExpenseCategories;

    public ObservableCollection<string> Currencies { get; } = [];
    public ObservableCollection<ReportBudgetOption> Budgets { get; } = [];
    public ObservableCollection<AccountMovementDetailRow> AccountDetails { get; } = [];
    public ObservableCollection<CashFlowTimelinePoint> TimelinePoints { get; } = [];
    public ObservableCollection<ReportCategoryDonutItem> TopExpenseDonutItems { get; } = [];
    public ObservableCollection<CashFlowBreakdownItem> BreakdownItems { get; } = [];
    public ObservableCollection<ReportInsightItem> Insights { get; } = [];

    public ReportSection Section { get; private set; } = ReportSection.CashFlow;
    public ReportDatePreset Preset { get; private set; } = ReportDatePreset.MonthToDate;
    public ReportCategoryMode CategoryMode { get; private set; } = ReportCategoryMode.Expense;

    public CashFlowGranularity Granularity
    {
        get => granularity;
        private set => SetProperty(ref granularity, value);
    }

    public CashFlowBreakdownMode BreakdownMode
    {
        get => breakdownMode;
        private set => SetProperty(ref breakdownMode, value);
    }

    public PeriodComparisonSummary? Comparison
    {
        get => comparison;
        private set => SetProperty(ref comparison, value);
    }

    public string TotalExpenseFormatted
    {
        get => totalExpenseFormatted;
        private set => SetProperty(ref totalExpenseFormatted, value);
    }

    public string BreakdownTotalIncomeFormatted
    {
        get => breakdownTotalIncomeFormatted;
        private set => SetProperty(ref breakdownTotalIncomeFormatted, value);
    }

    public string BreakdownTotalExpenseFormatted
    {
        get => breakdownTotalExpenseFormatted;
        private set => SetProperty(ref breakdownTotalExpenseFormatted, value);
    }

    public string BreakdownTotalNetFormatted
    {
        get => breakdownTotalNetFormatted;
        private set => SetProperty(ref breakdownTotalNetFormatted, value);
    }

    public bool BreakdownTotalNetIsPositive
    {
        get => breakdownTotalNetIsPositive;
        private set => SetProperty(ref breakdownTotalNetIsPositive, value);
    }

    public bool HasTimelineActivity
    {
        get => hasTimelineActivity;
        private set => SetProperty(ref hasTimelineActivity, value);
    }

    public bool HasExpenseCategories
    {
        get => hasExpenseCategories;
        private set => SetProperty(ref hasExpenseCategories, value);
    }

    public string Currency { get => currency; private set => SetProperty(ref currency, value); }
    public DateOnly StartDate { get => startDate; private set => SetProperty(ref startDate, value); }
    public DateOnly EndDate { get => endDate; private set => SetProperty(ref endDate, value); }
    public bool IsLoading { get => loading; private set { if (SetProperty(ref loading, value)) Notify(); } }
    public string? ErrorMessage { get => error; private set { if (SetProperty(ref error, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsExporting { get => exporting; private set { if (SetProperty(ref exporting, value)) Notify(); } }
    public FinancialReportResult? Report { get => report; private set { if (SetProperty(ref report, value)) { OnReportChanged(); Notify(); } } }
    public BudgetDetails? Budget { get => budget; private set { if (SetProperty(ref budget, value)) Notify(); } }
    public bool CanExport => !IsLoading && !IsExporting && (Section == ReportSection.Budget ? Budget is not null && Budget.Summary.Id == selectedBudgetId : Report is not null && appliedKey == Key());

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var options = await operations.GetOptionsAsync(cancellationToken);
        today = options.Today;
        Replace(Currencies, options.Currencies);
        Replace(Budgets, options.Budgets);
        Currency = options.SelectedCurrency;
        ApplyPreset(ReportDatePreset.MonthToDate);

        if (!string.IsNullOrEmpty(Currency))
            await LoadAsync(cancellationToken);

        if (options.SelectedBudgetId.HasValue)
            await SelectBudgetAsync(options.SelectedBudgetId.Value, cancellationToken);

        Notify();
    }

    public Task SetSelectionAsync(string value, DateOnly start, DateOnly end, CancellationToken cancellationToken = default)
    {
        Currency = value;
        StartDate = start;
        EndDate = end;
        InvalidateBase();
        Notify();
        return LoadAsync(cancellationToken);
    }

    public void SetDraftSelection(string value, DateOnly start, DateOnly end)
    {
        Currency = value;
        StartDate = start;
        EndDate = end;
        Preset = ReportDatePreset.Custom;
        InvalidateBase();
        OnPropertyChanged(nameof(Preset));
        Notify();
    }

    public Task SetPresetAsync(ReportDatePreset value, CancellationToken cancellationToken = default)
    {
        ApplyPreset(value);
        InvalidateBase();
        Notify();
        return string.IsNullOrEmpty(Currency) ? Task.CompletedTask : LoadAsync(cancellationToken);
    }

    public async Task SetSectionAsync(ReportSection value, CancellationToken cancellationToken = default)
    {
        Section = value;
        OnPropertyChanged(nameof(Section));
        if (value == ReportSection.Accounts)
            await LoadAccountDetailAsync(cancellationToken);
        Notify();
    }

    public void SetCategoryMode(ReportCategoryMode value)
    {
        CategoryMode = value;
        OnPropertyChanged(nameof(CategoryMode));
    }

    public void SetGranularity(CashFlowGranularity value)
    {
        Granularity = value;
        RecalculateTimeline(cachedTimelineDetails);
        Notify();
    }

    public void SetBreakdownMode(CashFlowBreakdownMode value)
    {
        BreakdownMode = value;
        RecalculateBreakdown();
        Notify();
    }

    public async Task SelectBudgetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var requestVersion = Interlocked.Increment(ref budgetVersion);
        selectedBudgetId = id;
        Budget = null;
        Notify();

        BudgetDetails? result = null;
        Exception? failure = null;
        try { result = await operations.GetBudgetAsync(id, cancellationToken); }
        catch (Exception ex) { failure = ex; }

        if (requestVersion != Interlocked.Read(ref budgetVersion) || selectedBudgetId != id)
            return;

        if (failure is null && result?.Summary.Id == id)
        {
            Budget = result;
            ErrorMessage = null;
        }
        else
        {
            Budget = null;
            ErrorMessage = "Suma could not load that Budget report.";
        }

        Notify();
    }

    public Task LoadAsync(CancellationToken cancellationToken = default)
    {
        lock (sync)
        {
            version++;
            reload = true;
            token = cancellationToken;
            InvalidateBase();

            if (StartDate > EndDate)
            {
                reload = false;
                Report = null;
                ErrorMessage = "Start date must be on or before end date.";
                IsLoading = false;
                Notify();
                return Task.CompletedTask;
            }

            Notify();
            activeLoad ??= PumpAsync();
            return activeLoad;
        }
    }

    private async Task PumpAsync()
    {
        await Task.Yield();
        IsLoading = true;
        while (true)
        {
            long v;
            (string, DateOnly, DateOnly) key;
            CancellationToken ct;
            lock (sync)
            {
                v = version;
                key = Key();
                ct = token;
                reload = false;
            }

            if (key.Item2 > key.Item3)
            {
                lock (sync)
                {
                    IsLoading = false;
                    activeLoad = null;
                    Notify();
                    return;
                }
            }

            FinancialReportResult? result = null;
            Exception? failure = null;
            try
            {
                result = await operations.GetFinancialAsync(new(key.Item1, key.Item2, key.Item3), ct);
            }
            catch (Exception ex)
            {
                failure = ex;
            }

            if (v == Interlocked.Read(ref version))
            {
                if (failure is null)
                {
                    Report = result;
                    appliedKey = key;
                    ErrorMessage = null;
                    if (Section == ReportSection.Accounts)
                        await LoadAccountDetailAsync(ct);
                }
                else
                {
                    Report = null;
                    InvalidateBase();
                    ErrorMessage = "Suma could not load that report.";
                }
            }

            lock (sync)
            {
                if (reload || v != version) continue;
                IsLoading = false;
                activeLoad = null;
                Notify();
                return;
            }
        }
    }

    /// <summary>
    /// Loads supplementary dynamic details (timeline points from actual transactions, and previous-period comparison deltas).
    /// </summary>
    public async Task LoadDashboardDetailsAsync(CancellationToken cancellationToken = default)
    {
        var key = appliedKey;
        if (Report is null || key is null || key != Key()) return;

        try
        {
            // 1. Fetch account movement details for cash flow timeline points
            var details = await operations.GetAccountDetailAsync(new(key.Value.Item1, key.Value.Item2, key.Value.Item3), cancellationToken);
            cachedTimelineDetails = details;
            RecalculateTimeline(details);

            // 2. Fetch previous period for comparison percentages
            int days = key.Value.Item3.DayNumber - key.Value.Item2.DayNumber + 1;
            var prevStart = key.Value.Item2.AddDays(-days);
            var prevEnd = key.Value.Item2.AddDays(-1);
            var prev = await operations.GetFinancialAsync(new(key.Value.Item1, prevStart, prevEnd), cancellationToken);
            RecalculateComparison(prev);
        }
        catch
        {
            // Handled gracefully; dashboard remains functional with main report numbers
        }
        finally
        {
            Notify();
        }
    }

    public async Task LoadAccountDetailAsync(CancellationToken cancellationToken = default)
    {
        var key = appliedKey;
        if (Report is null || key is null || key != Key()) return;
        var requestVersion = Interlocked.Increment(ref detailVersion);
        detailAppliedKey = null;
        AccountDetails.Clear();
        Notify();

        IReadOnlyList<AccountMovementDetailRow>? rows = null;
        Exception? failure = null;
        try
        {
            rows = await operations.GetAccountDetailAsync(new(key.Value.Item1, key.Value.Item2, key.Value.Item3), cancellationToken);
        }
        catch (Exception ex)
        {
            failure = ex;
        }

        if (requestVersion != Interlocked.Read(ref detailVersion) || appliedKey != key || Key() != key)
            return;

        if (failure is not null)
        {
            detailAppliedKey = null;
            AccountDetails.Clear();
            ErrorMessage = "Suma could not load Account movement detail.";
            Notify();
            return;
        }

        Replace(AccountDetails, rows!);
        cachedTimelineDetails = rows;
        RecalculateTimeline(rows);
        detailAppliedKey = key;
        ErrorMessage = null;
        Notify();
    }

    public async Task RunExportInteractionAsync(Func<ReportExport, CancellationToken, Task> saveAsync, CancellationToken cancellationToken = default)
    {
        if (!CanExport) return;
        IsExporting = true;
        try
        {
            ReportExport? export;
            try { export = await CreateExportPayloadAsync(cancellationToken); }
            catch { ErrorMessage = "Suma could not create that CSV export."; return; }
            if (export is null) return;
            try { await saveAsync(export, cancellationToken); ErrorMessage = null; }
            catch { ErrorMessage = "Suma could not save that CSV export."; }
        }
        finally { IsExporting = false; }
    }

    public async Task<ReportExport?> CreateExportAsync(CancellationToken cancellationToken = default)
    {
        if (!CanExport) return null;
        IsExporting = true;
        try { return await CreateExportPayloadAsync(cancellationToken); }
        catch { ErrorMessage = "Suma could not create that CSV export."; return null; }
        finally { IsExporting = false; }
    }

    private async Task<ReportExport?> CreateExportPayloadAsync(CancellationToken cancellationToken)
    {
        if (Section == ReportSection.Budget)
        {
            var exportBudget = Budget;
            var exportBudgetId = selectedBudgetId;
            if (exportBudget is null || exportBudget.Summary.Id != exportBudgetId) return null;
            var budgetBytes = await operations.GetCsvAsync(new(ReportCsvType.BudgetPerformance, Budget: exportBudget), cancellationToken);
            if (Budget != exportBudget || selectedBudgetId != exportBudgetId) return null;
            ErrorMessage = null;
            return new(ReportCsvSerializer.BudgetFileName(exportBudget), budgetBytes);
        }

        var exportKey = appliedKey;
        if (exportKey is null || exportKey != Key()) return null;
        if (Section == ReportSection.Accounts && detailAppliedKey != exportKey)
            await LoadAccountDetailAsync(cancellationToken);
        if (appliedKey != exportKey || exportKey != Key() || (Section == ReportSection.Accounts && detailAppliedKey != exportKey))
            return null;

        var type = Section switch
        {
            ReportSection.CashFlow => ReportCsvType.CashFlow,
            ReportSection.Categories when CategoryMode == ReportCategoryMode.Expense => ReportCsvType.ExpenseCategories,
            ReportSection.Categories => ReportCsvType.IncomeCategories,
            _ => ReportCsvType.AccountMovement
        };

        var bytes = await operations.GetCsvAsync(new(type, Report, AccountDetails, Budget), cancellationToken);
        if (appliedKey != exportKey || exportKey != Key()) return null;

        var name = ReportCsvSerializer.GeneralFileName(type switch
        {
            ReportCsvType.CashFlow => "cash-flow",
            ReportCsvType.ExpenseCategories => "expense-by-category",
            ReportCsvType.IncomeCategories => "income-by-category",
            _ => "account-movement"
        }, exportKey.Value.Item1, exportKey.Value.Item2, exportKey.Value.Item3);

        ErrorMessage = null;
        return new(name, bytes);
    }

    private void ApplyPreset(ReportDatePreset value)
    {
        Preset = value;
        OnPropertyChanged(nameof(Preset));
        (StartDate, EndDate) = value switch
        {
            ReportDatePreset.MonthToDate => (new(today.Year, today.Month, 1), today),
            ReportDatePreset.LastMonth => PreviousMonth(today),
            ReportDatePreset.Last30Days => (today.AddDays(-29), today),
            ReportDatePreset.Last3Months => (today.AddMonths(-3), today),
            ReportDatePreset.YearToDate => (new(today.Year, 1, 1), today),
            ReportDatePreset.LastYear => (new(today.Year - 1, 1, 1), new(today.Year - 1, 12, 31)),
            _ => (StartDate, EndDate)
        };
    }

    private static (DateOnly, DateOnly) PreviousMonth(DateOnly value)
    {
        var end = new DateOnly(value.Year, value.Month, 1).AddDays(-1);
        return (new(end.Year, end.Month, 1), end);
    }

    private void InvalidateBase()
    {
        appliedKey = null;
        detailAppliedKey = null;
        Interlocked.Increment(ref detailVersion);
        AccountDetails.Clear();
        cachedTimelineDetails = null;
    }

    private void OnReportChanged()
    {
        RecalculateDonut();
        RecalculateBreakdown();
        RecalculateInsights();
        RecalculateTimeline(cachedTimelineDetails);
        if (Comparison is null)
        {
            Comparison = new PeriodComparisonSummary(
                null, "vs previous period", true,
                null, "vs previous period", true,
                null, "vs previous period", true,
                null, "vs previous period", true);
        }
    }

    private void RecalculateDonut()
    {
        TopExpenseDonutItems.Clear();
        if (Report is null || Report.ExpenseCategories.Count == 0)
        {
            HasExpenseCategories = false;
            TotalExpenseFormatted = MoneyText.Format(0, Currency);
            return;
        }

        var validCategories = Report.ExpenseCategories
            .Where(c => c.NetExpenseMinor > 0)
            .OrderByDescending(c => c.NetExpenseMinor)
            .ToList();

        if (validCategories.Count == 0)
        {
            validCategories = Report.ExpenseCategories
                .Where(c => c.GrossExpenseMinor > 0)
                .OrderByDescending(c => c.GrossExpenseMinor)
                .ToList();
        }

        long totalMinor = validCategories.Sum(c => c.NetExpenseMinor > 0 ? c.NetExpenseMinor : c.GrossExpenseMinor);
        TotalExpenseFormatted = MoneyText.Format(totalMinor, Currency);
        HasExpenseCategories = totalMinor > 0 && validCategories.Count > 0;

        if (totalMinor == 0) return;

        for (int i = 0; i < validCategories.Count; i++)
        {
            var cat = validCategories[i];
            long amt = cat.NetExpenseMinor > 0 ? cat.NetExpenseMinor : cat.GrossExpenseMinor;
            double pct = ((double)amt / totalMinor) * 100.0;
            var paletteItem = DonutPalette[i % DonutPalette.Length];
            TopExpenseDonutItems.Add(new ReportCategoryDonutItem(
                cat.CategoryName + (cat.CategoryArchived ? " (Archived)" : ""),
                amt,
                pct,
                MoneyText.Format(amt, Currency),
                $"{pct:0.#}%",
                paletteItem.Hex,
                new SolidColorBrush(paletteItem.Color)
            ));
        }
    }

    private void RecalculateBreakdown()
    {
        BreakdownItems.Clear();
        if (Report is null)
        {
            BreakdownTotalIncomeFormatted = MoneyText.Format(0, Currency);
            BreakdownTotalExpenseFormatted = MoneyText.Format(0, Currency);
            BreakdownTotalNetFormatted = MoneyText.Format(0, Currency);
            BreakdownTotalNetIsPositive = true;
            return;
        }

        if (BreakdownMode == CashFlowBreakdownMode.ByCategory)
        {
            var catMap = new Dictionary<string, (string Name, long Income, long Expense)>(StringComparer.OrdinalIgnoreCase);
            foreach (var inc in Report.IncomeCategories)
            {
                var name = inc.CategoryName + (inc.CategoryArchived ? " (Archived)" : "");
                catMap[name] = (name, inc.IncomeMinor, 0);
            }
            foreach (var exp in Report.ExpenseCategories)
            {
                var name = exp.CategoryName + (exp.CategoryArchived ? " (Archived)" : "");
                if (catMap.TryGetValue(name, out var existing))
                    catMap[name] = (name, existing.Income, exp.NetExpenseMinor);
                else
                    catMap[name] = (name, 0, exp.NetExpenseMinor);
            }

            var list = catMap.Values
                .Select(v => (v.Name, v.Income, v.Expense, Net: v.Income - v.Expense))
                .OrderByDescending(v => Math.Abs(v.Net))
                .ToList();

            long maxMagnitude = list.Count > 0 ? Math.Max(1, list.Max(v => Math.Abs(v.Net))) : 1;

            foreach (var item in list)
            {
                bool isPos = item.Net >= 0;
                double magPct = Math.Min(100.0, ((double)Math.Abs(item.Net) / maxMagnitude) * 100.0);
                BreakdownItems.Add(new CashFlowBreakdownItem(
                    item.Name,
                    item.Net >= 0 ? "\uE8A7" : "\uE8A8",
                    item.Income,
                    item.Expense,
                    item.Net,
                    MoneyText.Format(item.Income, Currency),
                    MoneyText.Format(item.Expense, Currency),
                    (item.Net >= 0 ? "+" : "") + MoneyText.Format(item.Net, Currency),
                    magPct,
                    isPos,
                    isPos ? GreenBrush : RedBrush
                ));
            }

            long totInc = Report.CashFlow.GrossIncomeMinor;
            long totExp = Report.CashFlow.NetExpenseMinor;
            long totNet = Report.CashFlow.NetCashFlowMinor;
            BreakdownTotalIncomeFormatted = MoneyText.Format(totInc, Currency);
            BreakdownTotalExpenseFormatted = MoneyText.Format(totExp, Currency);
            BreakdownTotalNetFormatted = (totNet >= 0 ? "+" : "") + MoneyText.Format(totNet, Currency);
            BreakdownTotalNetIsPositive = totNet >= 0;
        }
        else
        {
            var list = Report.AccountMovements
                .Select(a => (
                    Name: a.AccountName + (a.AccountArchived ? " (Archived)" : ""),
                    Inflow: a.TotalInflowMinor,
                    Outflow: a.TotalOutflowMinor,
                    Net: a.NetMovementMinor
                ))
                .OrderByDescending(a => Math.Abs(a.Net))
                .ToList();

            long maxMagnitude = list.Count > 0 ? Math.Max(1, list.Max(a => Math.Abs(a.Net))) : 1;

            foreach (var item in list)
            {
                bool isPos = item.Net >= 0;
                double magPct = Math.Min(100.0, ((double)Math.Abs(item.Net) / maxMagnitude) * 100.0);
                BreakdownItems.Add(new CashFlowBreakdownItem(
                    item.Name,
                    "\uE8C7",
                    item.Inflow,
                    item.Outflow,
                    item.Net,
                    MoneyText.Format(item.Inflow, Currency),
                    MoneyText.Format(item.Outflow, Currency),
                    (item.Net >= 0 ? "+" : "") + MoneyText.Format(item.Net, Currency),
                    magPct,
                    isPos,
                    isPos ? GreenBrush : RedBrush
                ));
            }

            long totInflow = Report.AccountMovements.Sum(a => a.TotalInflowMinor);
            long totOutflow = Report.AccountMovements.Sum(a => a.TotalOutflowMinor);
            long totNet = Report.AccountMovements.Sum(a => a.NetMovementMinor);
            BreakdownTotalIncomeFormatted = MoneyText.Format(totInflow, Currency);
            BreakdownTotalExpenseFormatted = MoneyText.Format(totOutflow, Currency);
            BreakdownTotalNetFormatted = (totNet >= 0 ? "+" : "") + MoneyText.Format(totNet, Currency);
            BreakdownTotalNetIsPositive = totNet >= 0;
        }
    }

    private void RecalculateInsights()
    {
        Insights.Clear();
        if (Report is null) return;

        var cf = Report.CashFlow;

        // Insight 1: Savings Rate / Net Margin
        if (cf.GrossIncomeMinor > 0)
        {
            double savingsRate = ((double)cf.NetCashFlowMinor / cf.GrossIncomeMinor) * 100.0;
            if (savingsRate >= 20.0)
            {
                Insights.Add(new ReportInsightItem(
                    "Healthy Cash Surplus",
                    $"Retained {savingsRate:0.#}% of gross income ({MoneyText.Format(cf.NetCashFlowMinor, Currency)}) as net cash surplus.",
                    "\uE930",
                    "#EAF5EA",
                    "#2E7D32"
                ));
            }
            else if (savingsRate >= 0)
            {
                Insights.Add(new ReportInsightItem(
                    "Modest Surplus",
                    $"Retained {savingsRate:0.#}% of gross income after expenses ({MoneyText.Format(cf.NetCashFlowMinor, Currency)} net).",
                    "\uE946",
                    "#FFF8E1",
                    "#F57F17"
                ));
            }
            else
            {
                Insights.Add(new ReportInsightItem(
                    "Deficit Spending",
                    $"Expenses exceeded gross income by {MoneyText.Format(Math.Abs(cf.NetCashFlowMinor), Currency)} this period.",
                    "\uE783",
                    "#FDEDEC",
                    "#C62828"
                ));
            }
        }
        else if (cf.NetExpenseMinor > 0)
        {
            Insights.Add(new ReportInsightItem(
                "Expenses Without Income",
                $"Net expenses total {MoneyText.Format(cf.NetExpenseMinor, Currency)} with no income logged for this period.",
                "\uE783",
                "#FDEDEC",
                "#C62828"
            ));
        }
        else
        {
            Insights.Add(new ReportInsightItem(
                "Ready for Analytics",
                "Log your income and expenses to unlock live financial performance insights.",
                "\uE946",
                "#EDF4FC",
                "#1976D2"
            ));
        }

        // Insight 2: Top Expense Category Driver
        if (Report.ExpenseCategories.Count > 0)
        {
            var top = Report.ExpenseCategories[0];
            if (cf.NetExpenseMinor > 0 && top.NetExpenseMinor > 0)
            {
                double pct = ((double)top.NetExpenseMinor / cf.NetExpenseMinor) * 100.0;
                Insights.Add(new ReportInsightItem(
                    "Primary Expense Driver",
                    $"{top.CategoryName} is your largest expense, accounting for {pct:0.#}% ({MoneyText.Format(top.NetExpenseMinor, Currency)}) of spending.",
                    "\uE825",
                    "#FFF3E0",
                    "#E65100"
                ));
            }
            else
            {
                Insights.Add(new ReportInsightItem(
                    "Active Categories",
                    $"{Report.ExpenseCategories.Count} expense categories tracked in this reporting period.",
                    "\uE825",
                    "#EDF4FC",
                    "#1976D2"
                ));
            }
        }
        else
        {
            Insights.Add(new ReportInsightItem(
                "No Expense Categories",
                "No categorized expenses recorded in this period.",
                "\uE825",
                "#F5F5F5",
                "#757575"
            ));
        }

        // Insight 3: Refund or Account Movement Activity
        if (cf.RefundMinor > 0)
        {
            double refundRatio = cf.GrossExpenseMinor > 0 ? ((double)cf.RefundMinor / cf.GrossExpenseMinor) * 100.0 : 0.0;
            Insights.Add(new ReportInsightItem(
                "Refund Recoveries",
                $"Recovered {MoneyText.Format(cf.RefundMinor, Currency)} in refunds ({refundRatio:0.#}% of gross expense).",
                "\uE72C",
                "#E8F5E9",
                "#2E7D32"
            ));
        }
        else if (Report.AccountMovements.Count > 0)
        {
            var topAccount = Report.AccountMovements.OrderByDescending(a => a.TotalInflowMinor + a.TotalOutflowMinor).First();
            Insights.Add(new ReportInsightItem(
                "Most Active Account",
                $"{topAccount.AccountName} logged {MoneyText.Format(topAccount.TotalInflowMinor + topAccount.TotalOutflowMinor, Currency)} in total cash movement.",
                "\uE8C7",
                "#EDE7F6",
                "#512DA8"
            ));
        }
        else
        {
            Insights.Add(new ReportInsightItem(
                "Account Distribution",
                "Transactions will reflect cash movement across your active accounts.",
                "\uE8C7",
                "#F5F5F5",
                "#757575"
            ));
        }

        // Insight 4: High-level period overview
        if (cf.GrossIncomeMinor > 0 || cf.NetExpenseMinor > 0)
        {
            Insights.Add(new ReportInsightItem(
                "Cash Flow Balance",
                $"Gross Income: {MoneyText.Format(cf.GrossIncomeMinor, Currency)} | Net Expense: {MoneyText.Format(cf.NetExpenseMinor, Currency)} | Net: {(cf.NetCashFlowMinor >= 0 ? "+" : "")}{MoneyText.Format(cf.NetCashFlowMinor, Currency)}",
                "\uE9D9",
                "#E0F2F1",
                "#00695C"
            ));
        }
        else
        {
            Insights.Add(new ReportInsightItem(
                "Clean Slate",
                "No financial transactions found for the selected date range.",
                "\uE9D9",
                "#F5F5F5",
                "#757575"
            ));
        }
    }

    private void RecalculateTimeline(IReadOnlyList<AccountMovementDetailRow>? details)
    {
        TimelinePoints.Clear();
        if (StartDate > EndDate)
        {
            HasTimelineActivity = false;
            return;
        }

        var rows = details ?? cachedTimelineDetails ?? [];
        HasTimelineActivity = rows.Count > 0;

        if (Granularity == CashFlowGranularity.Daily)
        {
            int days = EndDate.DayNumber - StartDate.DayNumber + 1;
            if (days > 62)
            {
                PopulateWeeklyTimeline(rows);
                return;
            }

            for (int i = 0; i < days; i++)
            {
                var date = StartDate.AddDays(i);
                var dayRows = rows.Where(r => r.TransactionDate == date).ToList();
                long income = dayRows.Where(r => r.Direction == ReportMovementDirection.Inflow && (r.Type == TransactionType.Income || r.Type == TransactionType.Refund)).Sum(r => r.AmountMinor);
                long expense = dayRows.Where(r => r.Direction == ReportMovementDirection.Outflow && r.Type == TransactionType.Expense).Sum(r => r.AmountMinor);
                long net = income - expense;
                TimelinePoints.Add(new CashFlowTimelinePoint(
                    date,
                    days <= 14 ? date.ToString("MMM d") : date.Day.ToString(),
                    income,
                    expense,
                    net,
                    MoneyText.Format(income, Currency),
                    MoneyText.Format(expense, Currency),
                    (net >= 0 ? "+" : "") + MoneyText.Format(net, Currency),
                    $"{date:MMM d, yyyy}\nIncome: {MoneyText.Format(income, Currency)}\nExpense: {MoneyText.Format(expense, Currency)}\nNet: {(net >= 0 ? "+" : "")}{MoneyText.Format(net, Currency)}"
                ));
            }
        }
        else if (Granularity == CashFlowGranularity.Weekly)
        {
            PopulateWeeklyTimeline(rows);
        }
        else
        {
            PopulateMonthlyTimeline(rows);
        }
    }

    private void PopulateWeeklyTimeline(IReadOnlyList<AccountMovementDetailRow> rows)
    {
        var current = StartDate;
        while (current <= EndDate)
        {
            var weekEnd = current.AddDays(6);
            if (weekEnd > EndDate) weekEnd = EndDate;

            var curStart = current;
            var curEnd = weekEnd;
            var weekRows = rows.Where(r => r.TransactionDate >= curStart && r.TransactionDate <= curEnd).ToList();
            long income = weekRows.Where(r => r.Direction == ReportMovementDirection.Inflow && (r.Type == TransactionType.Income || r.Type == TransactionType.Refund)).Sum(r => r.AmountMinor);
            long expense = weekRows.Where(r => r.Direction == ReportMovementDirection.Outflow && r.Type == TransactionType.Expense).Sum(r => r.AmountMinor);
            long net = income - expense;

            TimelinePoints.Add(new CashFlowTimelinePoint(
                curStart,
                $"{curStart:MMM d}",
                income,
                expense,
                net,
                MoneyText.Format(income, Currency),
                MoneyText.Format(expense, Currency),
                (net >= 0 ? "+" : "") + MoneyText.Format(net, Currency),
                $"{curStart:MMM d} - {curEnd:MMM d}\nIncome: {MoneyText.Format(income, Currency)}\nExpense: {MoneyText.Format(expense, Currency)}\nNet: {(net >= 0 ? "+" : "")}{MoneyText.Format(net, Currency)}"
            ));

            current = weekEnd.AddDays(1);
        }
    }

    private void PopulateMonthlyTimeline(IReadOnlyList<AccountMovementDetailRow> rows)
    {
        var current = new DateOnly(StartDate.Year, StartDate.Month, 1);
        var last = new DateOnly(EndDate.Year, EndDate.Month, 1);
        while (current <= last)
        {
            var nextMonth = current.AddMonths(1);
            var monthEnd = nextMonth.AddDays(-1);
            var rangeStart = current < StartDate ? StartDate : current;
            var rangeEnd = monthEnd > EndDate ? EndDate : monthEnd;

            var curMonth = current;
            var monthRows = rows.Where(r => r.TransactionDate >= rangeStart && r.TransactionDate <= rangeEnd).ToList();
            long income = monthRows.Where(r => r.Direction == ReportMovementDirection.Inflow && (r.Type == TransactionType.Income || r.Type == TransactionType.Refund)).Sum(r => r.AmountMinor);
            long expense = monthRows.Where(r => r.Direction == ReportMovementDirection.Outflow && r.Type == TransactionType.Expense).Sum(r => r.AmountMinor);
            long net = income - expense;

            TimelinePoints.Add(new CashFlowTimelinePoint(
                rangeStart,
                current.ToString("MMM yyyy"),
                income,
                expense,
                net,
                MoneyText.Format(income, Currency),
                MoneyText.Format(expense, Currency),
                (net >= 0 ? "+" : "") + MoneyText.Format(net, Currency),
                $"{current:MMMM yyyy}\nIncome: {MoneyText.Format(income, Currency)}\nExpense: {MoneyText.Format(expense, Currency)}\nNet: {(net >= 0 ? "+" : "")}{MoneyText.Format(net, Currency)}"
            ));

            current = nextMonth;
        }
    }

    private void RecalculateComparison(FinancialReportResult? prev)
    {
        if (Report is null || prev is null)
        {
            Comparison = new PeriodComparisonSummary(
                null, "vs previous period", true,
                null, "vs previous period", true,
                null, "vs previous period", true,
                null, "vs previous period", true);
            return;
        }

        var curCf = Report.CashFlow;
        var prevCf = prev.CashFlow;

        var inc = CalcDelta(curCf.GrossIncomeMinor, prevCf.GrossIncomeMinor, invertSign: false);
        var exp = CalcDelta(curCf.NetExpenseMinor, prevCf.NetExpenseMinor, invertSign: true);
        var refd = CalcDelta(curCf.RefundMinor, prevCf.RefundMinor, invertSign: false);
        var net = CalcDelta(curCf.NetCashFlowMinor, prevCf.NetCashFlowMinor, invertSign: false);

        Comparison = new PeriodComparisonSummary(
            inc.Pct, inc.Text, inc.IsPos,
            exp.Pct, exp.Text, exp.IsPos,
            refd.Pct, refd.Text, refd.IsPos,
            net.Pct, net.Text, net.IsPos
        );
    }

    private static (double? Pct, string Text, bool IsPos) CalcDelta(long current, long previous, bool invertSign)
    {
        if (previous == 0 && current == 0)
            return (0.0, "0.0% vs last period", true);
        if (previous == 0 && current > 0)
            return (100.0, "+100% vs last period", !invertSign);
        if (previous == 0 && current < 0)
            return (-100.0, "-100% vs last period", invertSign);

        double pct = ((double)(current - previous) / Math.Abs(previous)) * 100.0;
        bool isPos = invertSign ? pct <= 0 : pct >= 0;
        string text = pct >= 0 ? $"+{pct:0.#}% vs last period" : $"{pct:0.#}% vs last period";
        return (pct, text, isPos);
    }

    private (string, DateOnly, DateOnly) Key() => (Currency, StartDate, EndDate);

    private static void Replace<T>(ObservableCollection<T> collection, IEnumerable<T> values)
    {
        collection.Clear();
        foreach (var value in values) collection.Add(value);
    }

    private void Notify() { OnPropertyChanged(nameof(CanExport)); }
}
