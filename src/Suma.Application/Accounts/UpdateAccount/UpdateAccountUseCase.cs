using Suma.Application.Abstractions.Persistence;
using Suma.Application.Common;
using Suma.Application.Common.Exceptions;
using Suma.Domain.Accounts;

namespace Suma.Application.Accounts.UpdateAccount;

public sealed record UpdateAccountRequest(
    Guid AccountId,
    string Name,
    AccountType Type,
    bool IncludeInAvailableToSpend);

public sealed record UpdateAccountResult(
    Guid Id,
    string Name,
    AccountType Type,
    bool IncludeInAvailableToSpend);

public sealed class UpdateAccountUseCase(IAccountStore accounts, IUnitOfWork unitOfWork)
{
    public async Task<UpdateAccountResult> ExecuteAsync(
        UpdateAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ApplicationValidationException("Account name is required.");
        }

        if (!Enum.IsDefined(request.Type))
        {
            throw new ApplicationValidationException("Account type is not supported.");
        }

        var account = await accounts.GetByIdAsync(request.AccountId, cancellationToken)
            ?? throw new NotFoundException("Account was not found.");
        Validation.RequireActive(account, "selected");

        account.Rename(request.Name);
        account.ChangeType(request.Type);
        account.SetAvailableToSpendInclusion(request.IncludeInAvailableToSpend);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new UpdateAccountResult(
            account.Id,
            account.Name,
            account.Type,
            account.IncludeInAvailableToSpend);
    }
}
