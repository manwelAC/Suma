using Suma.Application.Abstractions.Persistence;

namespace Suma.Infrastructure.Persistence;

public sealed class EfUnitOfWork(SumaDbContext context) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await context.SaveChangesAsync(cancellationToken);
}
