using Suma.Application.Abstractions.Persistence;
using Suma.Domain.Accounts;

namespace Suma.Application.Accounts.GetAccounts;

public sealed record AccountSummary(Guid Id, string Name, AccountType Type, long BalanceMinor, string CurrencyCode, bool IncludeInAvailableToSpend);

public sealed class GetAccountsUseCase(IAccountStore accounts, ITransactionStore transactions)
{
    public async Task<IReadOnlyList<AccountSummary>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return await BuildSummariesAsync(await accounts.GetActiveAsync(cancellationToken), cancellationToken);
    }

    public async Task<IReadOnlyList<AccountSummary>> ExecuteArchivedAsync(CancellationToken cancellationToken = default)
    {
        return await BuildSummariesAsync(await accounts.GetArchivedAsync(cancellationToken), cancellationToken);
    }

    private async Task<IReadOnlyList<AccountSummary>> BuildSummariesAsync(
        IReadOnlyList<Account> selectedAccounts,
        CancellationToken cancellationToken)
    {
        var results = new List<AccountSummary>(selectedAccounts.Count);
        foreach (var account in selectedAccounts)
        {
            var ledger = await transactions.GetForAccountAsync(account.Id, cancellationToken);
            var balance = AccountBalanceCalculator.Calculate(account, ledger);
            results.Add(new AccountSummary(account.Id, account.Name, account.Type, balance, account.CurrencyCode, account.IncludeInAvailableToSpend));
        }

        return results;
    }
}
