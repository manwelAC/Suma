using Microsoft.Extensions.DependencyInjection;
using Suma.Application.Accounts.ArchiveAccount;
using Suma.Application.Accounts.CreateAccount;
using Suma.Application.Accounts.GetAccounts;
using Suma.Application.Accounts.RestoreAccount;
using Suma.Application.Accounts.UpdateAccount;

namespace Suma.Desktop.Operations.Accounts;

public sealed class AccountOperations(IServiceScopeFactory scopeFactory) : IAccountOperations
{
    public async Task<IReadOnlyList<AccountSummary>> GetAsync(
        bool archived,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var useCase = scope.ServiceProvider.GetRequiredService<GetAccountsUseCase>();
        return archived
            ? await useCase.ExecuteArchivedAsync(cancellationToken)
            : await useCase.ExecuteAsync(cancellationToken);
    }

    public async Task<CreateAccountResult> CreateAsync(
        CreateAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CreateAccountUseCase>()
            .ExecuteAsync(request, cancellationToken);
    }

    public async Task<UpdateAccountResult> UpdateAsync(
        UpdateAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<UpdateAccountUseCase>()
            .ExecuteAsync(request, cancellationToken);
    }

    public async Task ArchiveAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ArchiveAccountUseCase>()
            .ExecuteAsync(accountId, cancellationToken);
    }

    public async Task RestoreAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<RestoreAccountUseCase>()
            .ExecuteAsync(accountId, cancellationToken);
    }

    public async Task<IReadOnlyList<Suma.Application.Transactions.GetTransactions.TransactionHistoryResult>> GetRecentTransactionsAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var useCase = scope.ServiceProvider.GetRequiredService<Suma.Application.Transactions.GetTransactions.GetTransactionsUseCase>();
        var results = await useCase.ExecuteAsync(new Suma.Application.Transactions.GetTransactions.GetTransactionsRequest(Limit: 50), cancellationToken);
        return results
            .Where(t => t.SourceAccountId == accountId || t.DestinationAccountId == accountId)
            .Take(4)
            .ToArray();
    }
}
