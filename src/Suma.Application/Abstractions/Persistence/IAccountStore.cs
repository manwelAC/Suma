using Suma.Domain.Accounts;

namespace Suma.Application.Abstractions.Persistence;

public interface IAccountStore
{
    Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Account>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Account>> GetArchivedAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Account account, CancellationToken cancellationToken = default);
}
