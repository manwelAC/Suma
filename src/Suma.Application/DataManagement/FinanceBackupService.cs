using Suma.Application.Abstractions.Persistence;
using Suma.Application.Common.Exceptions;

namespace Suma.Application.DataManagement;

public sealed class FinanceBackupService(IFinanceBackupStore store)
{
    public Task CreateAsync(string destinationPath, CancellationToken cancellationToken = default) => store.CreateBackupAsync(destinationPath, cancellationToken);
    public async Task<string> ValidateAndStageAsync(string candidatePath, CancellationToken cancellationToken = default)
    {
        var validation = await store.ValidateAsync(candidatePath, cancellationToken);
        if (!validation.IsValid) throw new ApplicationValidationException(validation.Failure == BackupValidationFailure.UnsupportedVersion ? "This backup was created by an unsupported Suma database version." : "That file is not a valid Suma backup.");
        return await store.StageAsync(candidatePath, cancellationToken);
    }
    public Task MarkPendingAsync(string stagedPath, CancellationToken cancellationToken = default) => store.MarkPendingAsync(stagedPath, cancellationToken);
    public Task DiscardStagedAsync(string stagedPath, CancellationToken cancellationToken = default) => store.DiscardStagedAsync(stagedPath, cancellationToken);
}
