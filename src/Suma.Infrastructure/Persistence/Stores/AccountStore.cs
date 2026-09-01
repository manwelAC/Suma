using Microsoft.EntityFrameworkCore;
using Suma.Application.Abstractions.Persistence;
using Suma.Domain.Accounts;

namespace Suma.Infrastructure.Persistence.Stores;

public sealed class AccountStore(SumaDbContext context) : IAccountStore
{
    public Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Accounts.SingleOrDefaultAsync(account => account.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Account>> GetActiveAsync(CancellationToken cancellationToken = default) =>
        await context.Accounts.AsNoTracking().Where(account => !account.IsArchived).OrderBy(account => account.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Account>> GetArchivedAsync(CancellationToken cancellationToken = default) =>
        await context.Accounts.AsNoTracking().Where(account => account.IsArchived).OrderBy(account => account.Name).ToListAsync(cancellationToken);

    public Task AddAsync(Account account, CancellationToken cancellationToken = default) =>
        context.Accounts.AddAsync(account, cancellationToken).AsTask();
}
