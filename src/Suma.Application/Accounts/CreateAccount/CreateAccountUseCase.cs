using Suma.Application.Abstractions.Persistence;
using Suma.Application.Common.Exceptions;
using Suma.Domain.Accounts;
using Suma.Domain.ValueObjects;

namespace Suma.Application.Accounts.CreateAccount;

public sealed record CreateAccountRequest(
    string Name,
    AccountType Type,
    long OpeningBalanceMinor,
    string CurrencyCode,
    bool IncludeInAvailableToSpend);

public sealed record CreateAccountResult(
    Guid Id,
    string Name,
    AccountType Type,
    long OpeningBalanceMinor,
    string CurrencyCode,
    bool IncludeInAvailableToSpend);

public sealed class CreateAccountUseCase(IAccountStore accounts, IUnitOfWork unitOfWork)
{
    public async Task<CreateAccountResult> ExecuteAsync(
        CreateAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        Account account;
        try
        {
            var openingBalance = new Money(request.OpeningBalanceMinor, request.CurrencyCode);
            account = new Account(
                request.Name,
                request.Type,
                openingBalance,
                request.CurrencyCode,
                request.IncludeInAvailableToSpend);
        }
        catch (ArgumentException exception)
        {
            throw new ApplicationValidationException(exception.Message);
        }

        await accounts.AddAsync(account, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CreateAccountResult(
            account.Id,
            account.Name,
            account.Type,
            account.OpeningBalance.AmountMinor,
            account.CurrencyCode,
            account.IncludeInAvailableToSpend);
    }
}
