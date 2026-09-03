using Suma.Application.Abstractions.Persistence;
using Suma.Infrastructure.Persistence.Stores;

namespace Suma.Infrastructure.Runtime;

internal interface IRestoreExecutor
{
    BackupValidationResult Validate(string path);
    void Backup(string sourcePath, string destinationPath, bool recreateDestination);
    void CandidateApplied();
}

internal sealed class RestoreExecutor : IRestoreExecutor
{
    public BackupValidationResult Validate(string path) => FinanceBackupStore.Validate(path);
    public void Backup(string sourcePath, string destinationPath, bool recreateDestination) => FinanceBackupStore.Backup(sourcePath, destinationPath, recreateDestination);
    public void CandidateApplied() { }
}
