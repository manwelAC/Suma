using Suma.Application.Abstractions.Persistence;
using Suma.Application.Common.Exceptions;

namespace Suma.Application.Accounts.RestoreAccount;

public sealed class RestoreAccountUseCase(IAccountStore accounts, IUnitOfWork unitOfWork)
{
    public async Task ExecuteAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await accounts.GetByIdAsync(accountId, cancellationToken)
            ?? throw new NotFoundException("Account was not found.");

        account.Restore();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
