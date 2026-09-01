using Suma.Application.Abstractions.Persistence;
using Suma.Application.Common.Exceptions;

namespace Suma.Application.Accounts.GetAccountBalance;

public sealed record AccountBalanceResult(Guid AccountId, long BalanceMinor, string CurrencyCode);

public sealed class GetAccountBalanceUseCase(IAccountStore accounts, ITransactionStore transactions)
{
    public async Task<AccountBalanceResult> ExecuteAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await accounts.GetByIdAsync(accountId, cancellationToken)
            ?? throw new NotFoundException("Account was not found.");
        var ledger = await transactions.GetForAccountAsync(accountId, cancellationToken);
        var balance = AccountBalanceCalculator.Calculate(account, ledger);

        return new AccountBalanceResult(account.Id, balance, account.CurrencyCode);
    }
}
