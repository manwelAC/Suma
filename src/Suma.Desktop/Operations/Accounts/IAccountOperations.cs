using Suma.Application.Accounts.CreateAccount;
using Suma.Application.Accounts.GetAccounts;
using Suma.Application.Accounts.UpdateAccount;

namespace Suma.Desktop.Operations.Accounts;

public interface IAccountOperations
{
    Task<IReadOnlyList<AccountSummary>> GetAsync(bool archived, CancellationToken cancellationToken = default);

    Task<CreateAccountResult> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken = default);

    Task<UpdateAccountResult> UpdateAsync(UpdateAccountRequest request, CancellationToken cancellationToken = default);

    Task ArchiveAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task RestoreAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Suma.Application.Transactions.GetTransactions.TransactionHistoryResult>> GetRecentTransactionsAsync(Guid accountId, CancellationToken cancellationToken = default);
}
