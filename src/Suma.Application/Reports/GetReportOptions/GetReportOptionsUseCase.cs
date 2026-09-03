using Suma.Application.Abstractions.Persistence;
using Suma.Application.Abstractions.Time;

namespace Suma.Application.Reports.GetReportOptions;

public sealed record ReportBudgetOption(Guid Id, string Name, DateOnly PeriodStart, DateOnly PeriodEnd, string CurrencyCode, bool IsArchived);
public sealed record ReportOptions(DateOnly Today, IReadOnlyList<string> Currencies, string SelectedCurrency, IReadOnlyList<ReportBudgetOption> Budgets, Guid? SelectedBudgetId);

public sealed class GetReportOptionsUseCase(IOverviewStore overview, IBudgetStore budgets, IDateProvider dateProvider)
{
    public async Task<ReportOptions> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var currencyFacts = await overview.GetAccountCurrencyFactsAsync(cancellationToken);
        var currencies = currencyFacts.Select(item => item.CurrencyCode).ToArray();
        var selectedCurrency = currencyFacts.FirstOrDefault(item => item.HasActiveIncludedAccount)?.CurrencyCode ?? currencies.FirstOrDefault() ?? string.Empty;
        var all = (await budgets.GetAsync(false, cancellationToken)).Concat(await budgets.GetAsync(true, cancellationToken))
            .Select(item => new ReportBudgetOption(item.Id, item.Name, item.PeriodStart, item.PeriodEnd, item.CurrencyCode, item.IsArchived)).ToArray();
        var selected = all.Where(item => !item.IsArchived && item.PeriodStart <= dateProvider.Today && item.PeriodEnd >= dateProvider.Today)
            .OrderByDescending(item => item.PeriodStart).ThenByDescending(item => item.PeriodEnd).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Id).FirstOrDefault()
            ?? all.OrderByDescending(item => item.PeriodStart).ThenByDescending(item => item.PeriodEnd).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Id).FirstOrDefault();
        return new(dateProvider.Today, currencies, selectedCurrency, all.OrderByDescending(item => item.PeriodStart).ThenByDescending(item => item.PeriodEnd).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Id).ToArray(), selected?.Id);
    }
}
