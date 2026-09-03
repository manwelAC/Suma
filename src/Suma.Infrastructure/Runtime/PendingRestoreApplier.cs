using Microsoft.Extensions.Logging;

namespace Suma.Infrastructure.Runtime;

public sealed class PendingRestoreApplier : IPendingRestoreApplier
{
    private const string RecoveredMessage = "Suma could not restore that backup. Your previous data was recovered.";
    private const string RecoveryRequiredWithRollbackMessage = "Suma could not recover your previous data automatically. Do not modify or remove Suma data; the local rollback file was preserved.";
    private const string RecoveryRequiredWithoutRollbackMessage = "Suma could not safely open your finance data after an interrupted restore. Do not modify or remove Suma data.";
    private readonly SumaRuntimePaths paths; private readonly ILogger<PendingRestoreApplier> logger; private readonly IRestoreExecutor executor; private readonly RestoreStateStore stateStore;

    public PendingRestoreApplier(SumaRuntimePaths paths, ILogger<PendingRestoreApplier> logger) : this(paths, logger, new RestoreExecutor(), new RestoreStateStore(paths)) { }
    internal PendingRestoreApplier(SumaRuntimePaths paths, ILogger<PendingRestoreApplier> logger, IRestoreExecutor executor, RestoreStateStore stateStore) { this.paths = paths; this.logger = logger; this.executor = executor; this.stateStore = stateStore; }

    public Task<PendingRestoreResult> ApplyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); Directory.CreateDirectory(paths.RestoreDirectory);
        RestoreTransactionState? state;
        try { state = stateStore.Read(); }
        catch (Exception exception) { logger.LogCritical(exception, "Suma restore state could not be read."); return Task.FromResult(RecoverRollback()); }

        if (state?.Phase == RestorePhase.CandidateApplied)
        {
            if (executor.Validate(paths.DatabasePath).IsValid) { CleanupSuccessfulRestore(); return Task.FromResult(new PendingRestoreResult(true, true, false, false, false, null)); }
            return Task.FromResult(RecoverRollback());
        }
        if (state?.Phase == RestorePhase.RollbackAuthoritative || (state is null && File.Exists(paths.RollbackPath))) return Task.FromResult(RecoverRollback());
        if (state?.Phase == RestorePhase.NoPreviousDatabaseApplying)
        {
            if (executor.Validate(paths.DatabasePath).IsValid) { CleanupSuccessfulRestore(); return Task.FromResult(new PendingRestoreResult(true, true, false, false, false, null)); }
            CleanupFailedWithoutPrevious(); return Task.FromResult(new PendingRestoreResult(true, false, false, false, false, "Suma could not restore that backup. No previous finance database existed."));
        }
        if (!File.Exists(paths.PendingRestorePath)) return Task.FromResult(PendingRestoreResult.None);
        return Task.FromResult(ApplyPending());
    }

    private PendingRestoreResult ApplyPending()
    {
        if (!executor.Validate(paths.PendingRestorePath).IsValid) { File.Delete(paths.PendingRestorePath); CleanupStaged(); return new(true, false, false, false, false, "That file is not a valid Suma backup."); }
        var hadPrevious = File.Exists(paths.DatabasePath);
        if (hadPrevious && !executor.Validate(paths.DatabasePath).IsValid) return RecoveryRequired(File.Exists(paths.RollbackPath));
        try
        {
            if (hadPrevious)
            {
                if (File.Exists(paths.RollbackPath)) return RecoverRollback();
                executor.Backup(paths.DatabasePath, paths.RollbackPath, false);
                if (!executor.Validate(paths.RollbackPath).IsValid) throw new InvalidDataException("Rollback snapshot validation failed.");
                stateStore.Write(RestorePhase.RollbackAuthoritative);
            }
            else stateStore.Write(RestorePhase.NoPreviousDatabaseApplying);

            executor.Backup(paths.PendingRestorePath, paths.DatabasePath, false); executor.CandidateApplied();
            if (!executor.Validate(paths.DatabasePath).IsValid) throw new InvalidDataException("Restored database validation failed.");
            stateStore.Write(RestorePhase.CandidateApplied); CleanupSuccessfulRestore();
            return new(true, true, false, false, false, null);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Pending Suma restore failed after restore processing began.");
            if (hadPrevious && File.Exists(paths.RollbackPath)) return RecoverRollback();
            CleanupFailedWithoutPrevious(); return new(true, false, false, false, false, "Suma could not restore that backup. No previous finance database existed.");
        }
    }

    private PendingRestoreResult RecoverRollback()
    {
        if (!File.Exists(paths.RollbackPath) || !executor.Validate(paths.RollbackPath).IsValid) return RecoveryRequired(File.Exists(paths.RollbackPath));
        try
        {
            executor.Backup(paths.RollbackPath, paths.DatabasePath, false);
            if (!executor.Validate(paths.DatabasePath).IsValid) throw new InvalidDataException("Recovered database validation failed.");
            if (File.Exists(paths.PendingRestorePath)) File.Delete(paths.PendingRestorePath); CleanupStaged(); stateStore.Delete(); File.Delete(paths.RollbackPath);
            return new(true, false, true, false, false, RecoveredMessage);
        }
        catch (Exception exception) { logger.LogCritical(exception, "Suma restore rollback failed. The rollback artifact remains authoritative at {RollbackPath}.", paths.RollbackPath); return RecoveryRequired(true); }
    }

    private PendingRestoreResult RecoveryRequired(bool rollbackRetained) => new(true, false, false, rollbackRetained, true, rollbackRetained ? RecoveryRequiredWithRollbackMessage : RecoveryRequiredWithoutRollbackMessage);
    private void CleanupSuccessfulRestore() { if (File.Exists(paths.PendingRestorePath)) File.Delete(paths.PendingRestorePath); CleanupStaged(); if (File.Exists(paths.RollbackPath)) File.Delete(paths.RollbackPath); stateStore.Delete(); }
    private void CleanupFailedWithoutPrevious() { if (File.Exists(paths.DatabasePath)) File.Delete(paths.DatabasePath); if (File.Exists(paths.PendingRestorePath)) File.Delete(paths.PendingRestorePath); CleanupStaged(); stateStore.Delete(); }
    private void CleanupStaged() { foreach (var path in Directory.EnumerateFiles(paths.RestoreDirectory, "staged-*.suma-backup")) File.Delete(path); }
}
