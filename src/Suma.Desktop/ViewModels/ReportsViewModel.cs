using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Suma.Application.Budgets.GetBudgetDetails;
using Suma.Application.Reports.Csv;
using Suma.Application.Reports.GetAccountMovementDetail;
using Suma.Application.Reports.GetFinancialReport;
using Suma.Application.Reports.GetReportOptions;
using Suma.Desktop.Operations.Reports;

namespace Suma.Desktop.ViewModels;

public enum ReportSection { CashFlow, Categories, Accounts, Budget }
public enum ReportDatePreset { MonthToDate, LastMonth, Last30Days, YearToDate, Custom }
public enum ReportCategoryMode { Expense, Income }
public sealed record ReportExport(string FileName, byte[] Content);

public sealed class ReportsViewModel(IReportOperations operations) : ObservableObject
{
    private readonly object sync = new(); private Task? activeLoad; private bool reload; private long version; private CancellationToken token; private long detailVersion; private long budgetVersion;
    private DateOnly today; private string currency = string.Empty; private DateOnly startDate; private DateOnly endDate; private bool loading; private bool exporting; private string? error; private FinancialReportResult? report; private (string, DateOnly, DateOnly)? appliedKey; private (string, DateOnly, DateOnly)? detailAppliedKey; private BudgetDetails? budget; private Guid? selectedBudgetId;
    public ObservableCollection<string> Currencies { get; } = []; public ObservableCollection<ReportBudgetOption> Budgets { get; } = []; public ObservableCollection<AccountMovementDetailRow> AccountDetails { get; } = [];
    public ReportSection Section { get; private set; } = ReportSection.CashFlow; public ReportDatePreset Preset { get; private set; } = ReportDatePreset.MonthToDate; public ReportCategoryMode CategoryMode { get; private set; } = ReportCategoryMode.Expense;
    public string Currency { get => currency; private set => SetProperty(ref currency, value); }
    public DateOnly StartDate { get => startDate; private set => SetProperty(ref startDate, value); }
    public DateOnly EndDate { get => endDate; private set => SetProperty(ref endDate, value); }
    public bool IsLoading { get => loading; private set { if (SetProperty(ref loading, value)) Notify(); } }
    public string? ErrorMessage { get => error; private set { if (SetProperty(ref error, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsExporting { get => exporting; private set { if (SetProperty(ref exporting, value)) Notify(); } }
    public FinancialReportResult? Report { get => report; private set { if (SetProperty(ref report, value)) Notify(); } }
    public BudgetDetails? Budget { get => budget; private set { if (SetProperty(ref budget, value)) Notify(); } }
    public bool CanExport => !IsLoading && !IsExporting && (Section == ReportSection.Budget ? Budget is not null && Budget.Summary.Id == selectedBudgetId : Report is not null && appliedKey == Key());

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var options = await operations.GetOptionsAsync(cancellationToken); today = options.Today; Replace(Currencies, options.Currencies); Replace(Budgets, options.Budgets); Currency = options.SelectedCurrency; ApplyPreset(ReportDatePreset.MonthToDate);
        if (!string.IsNullOrEmpty(Currency)) await LoadAsync(cancellationToken); if (options.SelectedBudgetId.HasValue) await SelectBudgetAsync(options.SelectedBudgetId.Value, cancellationToken); Notify();
    }
    public Task SetSelectionAsync(string value, DateOnly start, DateOnly end, CancellationToken cancellationToken = default) { Currency = value; StartDate = start; EndDate = end; InvalidateBase(); Notify(); return LoadAsync(cancellationToken); }
    public void SetDraftSelection(string value, DateOnly start, DateOnly end) { Currency = value; StartDate = start; EndDate = end; Preset = ReportDatePreset.Custom; InvalidateBase(); OnPropertyChanged(nameof(Preset)); Notify(); }
    public Task SetPresetAsync(ReportDatePreset value, CancellationToken cancellationToken = default) { ApplyPreset(value); InvalidateBase(); Notify(); return string.IsNullOrEmpty(Currency) ? Task.CompletedTask : LoadAsync(cancellationToken); }
    public async Task SetSectionAsync(ReportSection value, CancellationToken cancellationToken = default) { Section = value; OnPropertyChanged(nameof(Section)); if (value == ReportSection.Accounts) await LoadAccountDetailAsync(cancellationToken); Notify(); }
    public void SetCategoryMode(ReportCategoryMode value) { CategoryMode = value; OnPropertyChanged(nameof(CategoryMode)); }
    public async Task SelectBudgetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var requestVersion = Interlocked.Increment(ref budgetVersion); selectedBudgetId = id; Budget = null; Notify();
        BudgetDetails? result = null; Exception? failure = null; try { result = await operations.GetBudgetAsync(id, cancellationToken); } catch (Exception ex) { failure = ex; }
        if (requestVersion != Interlocked.Read(ref budgetVersion) || selectedBudgetId != id) return;
        if (failure is null && result?.Summary.Id == id) { Budget = result; ErrorMessage = null; } else { Budget = null; ErrorMessage = "Suma could not load that Budget report."; }
        Notify();
    }
    public Task LoadAsync(CancellationToken cancellationToken = default)
    {
        lock (sync)
        {
            version++; reload = true; token = cancellationToken; InvalidateBase();
            if (StartDate > EndDate) { reload = false; Report = null; ErrorMessage = "Start date must be on or before end date."; IsLoading = false; Notify(); return Task.CompletedTask; }
            Notify(); activeLoad ??= PumpAsync(); return activeLoad;
        }
    }
    private async Task PumpAsync() { await Task.Yield(); IsLoading = true; while (true) { long v; (string, DateOnly, DateOnly) key; CancellationToken ct; lock (sync) { v = version; key = Key(); ct = token; reload = false; } if (key.Item2 > key.Item3) { lock (sync) { IsLoading = false; activeLoad = null; Notify(); return; } } FinancialReportResult? result = null; Exception? failure = null; try { result = await operations.GetFinancialAsync(new(key.Item1, key.Item2, key.Item3), ct); } catch (Exception ex) { failure = ex; } if (v == Interlocked.Read(ref version)) { if (failure is null) { Report = result; appliedKey = key; ErrorMessage = null; if (Section == ReportSection.Accounts) await LoadAccountDetailAsync(ct); } else { Report = null; InvalidateBase(); ErrorMessage = "Suma could not load that report."; } } lock (sync) { if (reload || v != version) continue; IsLoading = false; activeLoad = null; Notify(); return; } } }
    public async Task LoadAccountDetailAsync(CancellationToken cancellationToken = default)
    {
        var key = appliedKey; if (Report is null || key is null || key != Key()) return; var requestVersion = Interlocked.Increment(ref detailVersion); detailAppliedKey = null; AccountDetails.Clear(); Notify();
        IReadOnlyList<AccountMovementDetailRow>? rows = null; Exception? failure = null;
        try { rows = await operations.GetAccountDetailAsync(new(key.Value.Item1, key.Value.Item2, key.Value.Item3), cancellationToken); } catch (Exception ex) { failure = ex; }
        if (requestVersion != Interlocked.Read(ref detailVersion) || appliedKey != key || Key() != key) return;
        if (failure is not null) { detailAppliedKey = null; AccountDetails.Clear(); ErrorMessage = "Suma could not load Account movement detail."; Notify(); return; }
        Replace(AccountDetails, rows!); detailAppliedKey = key; ErrorMessage = null; Notify();
    }
    public async Task RunExportInteractionAsync(Func<ReportExport, CancellationToken, Task> saveAsync, CancellationToken cancellationToken = default)
    {
        if (!CanExport) return; IsExporting = true;
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
        if (!CanExport) return null; IsExporting = true; try { return await CreateExportPayloadAsync(cancellationToken); } catch { ErrorMessage = "Suma could not create that CSV export."; return null; } finally { IsExporting = false; }
    }
    private async Task<ReportExport?> CreateExportPayloadAsync(CancellationToken cancellationToken)
    {
        if (Section == ReportSection.Budget)
        {
            var exportBudget = Budget; var exportBudgetId = selectedBudgetId;
            if (exportBudget is null || exportBudget.Summary.Id != exportBudgetId) return null;
            var budgetBytes = await operations.GetCsvAsync(new(ReportCsvType.BudgetPerformance, Budget: exportBudget), cancellationToken);
            if (Budget != exportBudget || selectedBudgetId != exportBudgetId) return null;
            ErrorMessage = null; return new(ReportCsvSerializer.BudgetFileName(exportBudget), budgetBytes);
        }
        var exportKey = appliedKey; if (exportKey is null || exportKey != Key()) return null;
        if (Section == ReportSection.Accounts && detailAppliedKey != exportKey) await LoadAccountDetailAsync(cancellationToken);
        if (appliedKey != exportKey || exportKey != Key() || (Section == ReportSection.Accounts && detailAppliedKey != exportKey)) return null;
        var type = Section switch { ReportSection.CashFlow => ReportCsvType.CashFlow, ReportSection.Categories when CategoryMode == ReportCategoryMode.Expense => ReportCsvType.ExpenseCategories, ReportSection.Categories => ReportCsvType.IncomeCategories, _ => ReportCsvType.AccountMovement };
        var bytes = await operations.GetCsvAsync(new(type, Report, AccountDetails, Budget), cancellationToken);
        if (appliedKey != exportKey || exportKey != Key()) return null;
        var name = ReportCsvSerializer.GeneralFileName(type switch { ReportCsvType.CashFlow => "cash-flow", ReportCsvType.ExpenseCategories => "expense-by-category", ReportCsvType.IncomeCategories => "income-by-category", _ => "account-movement" }, exportKey.Value.Item1, exportKey.Value.Item2, exportKey.Value.Item3); ErrorMessage = null; return new(name, bytes);
    }
    private void ApplyPreset(ReportDatePreset value) { Preset = value; OnPropertyChanged(nameof(Preset)); (StartDate, EndDate) = value switch { ReportDatePreset.MonthToDate => (new(today.Year, today.Month, 1), today), ReportDatePreset.LastMonth => PreviousMonth(today), ReportDatePreset.Last30Days => (today.AddDays(-29), today), ReportDatePreset.YearToDate => (new(today.Year, 1, 1), today), _ => (StartDate, EndDate) }; }
    private static (DateOnly, DateOnly) PreviousMonth(DateOnly value) { var end = new DateOnly(value.Year, value.Month, 1).AddDays(-1); return (new(end.Year, end.Month, 1), end); }
    private void InvalidateBase() { appliedKey = null; detailAppliedKey = null; Interlocked.Increment(ref detailVersion); AccountDetails.Clear(); }
    private (string, DateOnly, DateOnly) Key() => (Currency, StartDate, EndDate); private static void Replace<T>(ObservableCollection<T> collection, IEnumerable<T> values) { collection.Clear(); foreach (var value in values) collection.Add(value); }
    private void Notify() { OnPropertyChanged(nameof(CanExport)); }
}
