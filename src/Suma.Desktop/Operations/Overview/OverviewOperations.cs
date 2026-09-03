using Microsoft.Extensions.DependencyInjection;
using Suma.Application.Overview.GetOverview;

namespace Suma.Desktop.Operations.Overview;

public sealed class OverviewOperations(IServiceScopeFactory scopeFactory) : IOverviewOperations
{
    public async Task<OverviewResult> GetAsync(string? currencyCode, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<GetOverviewUseCase>().ExecuteAsync(currencyCode, cancellationToken);
    }
}
