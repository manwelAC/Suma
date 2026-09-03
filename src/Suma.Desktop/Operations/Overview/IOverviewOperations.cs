using Suma.Application.Overview.GetOverview;

namespace Suma.Desktop.Operations.Overview;

public interface IOverviewOperations
{
    Task<OverviewResult> GetAsync(string? currencyCode, CancellationToken cancellationToken = default);
}
