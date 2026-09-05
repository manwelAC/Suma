namespace Suma.Application.Abstractions.Persistence;

public enum BackupValidationFailure { None, InvalidDatabase, InvalidSchema, UnsupportedVersion }
public sealed record BackupValidationResult(bool IsValid, BackupValidationFailure Failure);

public interface IFinanceBackupStore
{
    Task CreateBackupAsync(string destinationPath, CancellationToken cancellationToken = default);
    Task<BackupValidationResult> ValidateAsync(string candidatePath, CancellationToken cancellationToken = default);
    Task<string> StageAsync(string candidatePath, CancellationToken cancellationToken = default);
    Task MarkPendingAsync(string stagedPath, CancellationToken cancellationToken = default);
    Task DiscardStagedAsync(string stagedPath, CancellationToken cancellationToken = default);
    Task ResetDataAsync(CancellationToken cancellationToken = default);
}
