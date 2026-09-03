using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Suma.Application.Abstractions.Persistence;
using Suma.Application.Abstractions.Security;
using Suma.Domain.Accounts;
using Suma.Domain.ValueObjects;
using Suma.Infrastructure.Persistence;
using Suma.Infrastructure.Persistence.Stores;
using Suma.Infrastructure.Runtime;
using Suma.Infrastructure.Security;
using Xunit;

namespace Suma.Infrastructure.Tests.Runtime;

public sealed class M18RuntimeTests
{
    [Fact]
    public async Task Security_json_is_separate_atomic_metadata_without_plaintext_pin()
    {
        using var temp = new TempDirectory(); var paths = new SumaRuntimePaths(Path.Combine(temp.Path, "suma.db")); var store = new JsonSecuritySettingsStore(paths); await new Suma.Application.Security.PinSecurityService(store).EnableAsync("1234", "1234", Token); var settings = await store.ReadAsync(Token); var json = await File.ReadAllTextAsync(paths.SecurityPath, Token);
        Assert.True(settings.Enabled); Assert.Equal("PBKDF2-SHA256", settings.Algorithm); Assert.Equal(210000, settings.Iterations);
        Assert.DoesNotContain("1234", json); Assert.DoesNotContain("PIN\"", json, StringComparison.OrdinalIgnoreCase); Assert.Empty(Directory.EnumerateFiles(temp.Path, "*.tmp")); Assert.NotEqual(paths.DatabasePath, paths.SecurityPath);
    }

    [Fact]
    public async Task Online_backup_is_valid_preserves_schema_and_committed_data_and_excludes_security()
    {
        using var temp = new TempDirectory(); var active = Path.Combine(temp.Path, "suma.db"); var backup = Path.Combine(temp.Path, "export.suma-backup"); await CreateDatabaseAsync(active, "Wallet"); var paths = new SumaRuntimePaths(active); await File.WriteAllTextAsync(paths.SecurityPath, "device-security", Token);
        var store = new FinanceBackupStore(paths); await store.CreateBackupAsync(backup, Token); Assert.True((await store.ValidateAsync(backup, Token)).IsValid);
        await using var context = Context(backup); Assert.Equal("Wallet", (await context.Accounts.SingleAsync(Token)).Name); Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(backup)!, "security.json.backup"))); Assert.Equal("device-security", await File.ReadAllTextAsync(paths.SecurityPath, Token));
        using var connection = new SqliteConnection($"Data Source={backup};Mode=ReadOnly;Pooling=False"); connection.Open(); using var command = connection.CreateCommand(); command.CommandText = "PRAGMA integrity_check;"; Assert.Equal("ok", command.ExecuteScalar());
    }

    [Fact]
    public async Task Validation_rejects_random_unrelated_missing_and_wrong_migration_databases_without_touching_active()
    {
        using var temp = new TempDirectory(); var active = Path.Combine(temp.Path, "active.db"); await CreateDatabaseAsync(active, "Original"); var store = new FinanceBackupStore(new(active));
        var random = Path.Combine(temp.Path, "random.suma-backup"); await File.WriteAllTextAsync(random, "not sqlite", Token); Assert.False((await store.ValidateAsync(random, Token)).IsValid);
        var unrelated = Path.Combine(temp.Path, "unrelated.db"); using (var connection = new SqliteConnection($"Data Source={unrelated};Pooling=False")) { connection.Open(); using var command = connection.CreateCommand(); command.CommandText = "CREATE TABLE Other(Id INTEGER);"; command.ExecuteNonQuery(); }
        Assert.Equal(BackupValidationFailure.InvalidSchema, (await store.ValidateAsync(unrelated, Token)).Failure);
        var wrong = Path.Combine(temp.Path, "wrong.db"); await CreateDatabaseAsync(wrong, "Wrong"); using (var connection = new SqliteConnection($"Data Source={wrong};Pooling=False")) { connection.Open(); using var command = connection.CreateCommand(); command.CommandText = "INSERT INTO __EFMigrationsHistory(MigrationId, ProductVersion) VALUES ('99999999999999_Future', '99');"; command.ExecuteNonQuery(); }
        Assert.Equal(BackupValidationFailure.UnsupportedVersion, (await store.ValidateAsync(wrong, Token)).Failure);
        await using var context = Context(active); Assert.Equal("Original", (await context.Accounts.SingleAsync(Token)).Name);
    }

    [Fact]
    public async Task Pending_restore_replaces_finance_at_startup_preserves_pin_and_cleans_artifacts()
    {
        using var temp = new TempDirectory(); var active = Path.Combine(temp.Path, "suma.db"); var candidate = Path.Combine(temp.Path, "candidate.suma-backup"); await CreateDatabaseAsync(active, "Original"); await CreateDatabaseAsync(candidate, "Restored"); var paths = new SumaRuntimePaths(active); await File.WriteAllTextAsync(paths.SecurityPath, "same-pin-metadata", Token);
        var store = new FinanceBackupStore(paths); var staged = await store.StageAsync(candidate, Token); await store.MarkPendingAsync(staged, Token); var result = await new PendingRestoreApplier(paths, NullLogger<PendingRestoreApplier>.Instance).ApplyAsync(Token);
        Assert.True(result.Succeeded); await using var context = Context(active); Assert.Equal("Restored", (await context.Accounts.SingleAsync(Token)).Name); Assert.Equal("same-pin-metadata", await File.ReadAllTextAsync(paths.SecurityPath, Token)); Assert.False(File.Exists(paths.PendingRestorePath)); Assert.False(File.Exists(paths.RollbackPath));
    }

    [Fact]
    public async Task Invalid_pending_restore_never_changes_active_database()
    {
        using var temp = new TempDirectory(); var active = Path.Combine(temp.Path, "suma.db"); await CreateDatabaseAsync(active, "Original"); var paths = new SumaRuntimePaths(active); Directory.CreateDirectory(paths.RestoreDirectory); await File.WriteAllTextAsync(paths.PendingRestorePath, "corrupt", Token);
        var result = await new PendingRestoreApplier(paths, NullLogger<PendingRestoreApplier>.Instance).ApplyAsync(Token); Assert.False(result.Succeeded); Assert.False(result.PreviousDataRecovered); Assert.False(result.RecoveryRequired); await using var context = Context(active); Assert.Equal("Original", (await context.Accounts.SingleAsync(Token)).Name); Assert.False(File.Exists(paths.PendingRestorePath));
    }

    [Fact]
    public async Task Failure_after_replacement_restores_and_validates_original_before_cleanup()
    {
        using var temp = new TempDirectory(); var paths = await PendingScenarioAsync(temp.Path); var executor = new FaultingExecutor { FailAfterCandidate = true };
        var result = await new PendingRestoreApplier(paths, NullLogger<PendingRestoreApplier>.Instance, executor, new(paths)).ApplyAsync(Token);
        Assert.True(result.PreviousDataRecovered); Assert.False(result.RecoveryRequired); Assert.Equal("Original", await AccountNameAsync(paths.DatabasePath)); Assert.False(File.Exists(paths.RollbackPath)); Assert.False(File.Exists(paths.PendingRestorePath)); Assert.False(File.Exists(paths.RestoreStatePath));
    }

    [Fact]
    public async Task Catastrophic_rollback_retains_authoritative_artifact_and_next_start_recovers_first()
    {
        using var temp = new TempDirectory(); var paths = await PendingScenarioAsync(temp.Path); var executor = new FaultingExecutor { FailAfterCandidate = true, FailRollbackApplication = true };
        var result = await new PendingRestoreApplier(paths, NullLogger<PendingRestoreApplier>.Instance, executor, new(paths)).ApplyAsync(Token);
        Assert.True(result.RecoveryRequired); Assert.True(result.RollbackRetained); Assert.True(FinanceBackupStore.Validate(paths.RollbackPath).IsValid); Assert.True(File.Exists(paths.PendingRestorePath)); Assert.True(File.Exists(paths.RestoreStatePath));
        var recovered = await new PendingRestoreApplier(paths, NullLogger<PendingRestoreApplier>.Instance).ApplyAsync(Token);
        Assert.True(recovered.PreviousDataRecovered); Assert.Equal("Original", await AccountNameAsync(paths.DatabasePath)); Assert.False(File.Exists(paths.RollbackPath)); Assert.False(File.Exists(paths.PendingRestorePath));
    }

    [Fact]
    public async Task Interrupted_restore_recovers_existing_rollback_without_reapplying_pending_candidate()
    {
        using var temp = new TempDirectory(); var paths = await PendingScenarioAsync(temp.Path); FinanceBackupStore.Backup(paths.DatabasePath, paths.RollbackPath, false); new RestoreStateStore(paths).Write(RestorePhase.RollbackAuthoritative); FinanceBackupStore.Backup(paths.PendingRestorePath, paths.DatabasePath, false); var rollbackBytes = await File.ReadAllBytesAsync(paths.RollbackPath, Token);
        var result = await new PendingRestoreApplier(paths, NullLogger<PendingRestoreApplier>.Instance).ApplyAsync(Token);
        Assert.True(result.PreviousDataRecovered); Assert.Equal("Original", await AccountNameAsync(paths.DatabasePath)); Assert.False(File.Exists(paths.PendingRestorePath)); Assert.False(File.Exists(paths.RollbackPath)); Assert.NotEmpty(rollbackBytes);
    }

    [Fact]
    public async Task Applied_state_crash_keeps_valid_restored_database_and_finishes_cleanup()
    {
        using var temp = new TempDirectory(); var paths = await PendingScenarioAsync(temp.Path); FinanceBackupStore.Backup(paths.DatabasePath, paths.RollbackPath, false); FinanceBackupStore.Backup(paths.PendingRestorePath, paths.DatabasePath, false); new RestoreStateStore(paths).Write(RestorePhase.CandidateApplied);
        var result = await new PendingRestoreApplier(paths, NullLogger<PendingRestoreApplier>.Instance).ApplyAsync(Token);
        Assert.True(result.Succeeded); Assert.False(result.PreviousDataRecovered); Assert.Equal("Restored", await AccountNameAsync(paths.DatabasePath)); Assert.False(File.Exists(paths.RollbackPath)); Assert.False(File.Exists(paths.RestoreStatePath));
    }

    [Fact]
    public async Task Forged_mapped_schema_and_foreign_key_violations_are_rejected_read_only()
    {
        using var temp = new TempDirectory(); var active = Path.Combine(temp.Path, "active.db"); var forged = Path.Combine(temp.Path, "forged.db"); var brokenForeignKey = Path.Combine(temp.Path, "foreign-key.db"); await CreateDatabaseAsync(active, "Original"); await CreateDatabaseAsync(forged, "Forged"); await CreateDatabaseAsync(brokenForeignKey, "Broken");
        Execute(forged, "ALTER TABLE accounts DROP COLUMN name;");
        Execute(brokenForeignKey, "PRAGMA foreign_keys=OFF; INSERT INTO budget_allocations(id,budget_id,category_id,reserve_from_available,amount_minor,currency_code) VALUES ('11111111-1111-1111-1111-111111111111','22222222-2222-2222-2222-222222222222','33333333-3333-3333-3333-333333333333',0,100,'USD');");
        var store = new FinanceBackupStore(new(active)); Assert.Equal(BackupValidationFailure.InvalidSchema, (await store.ValidateAsync(forged, Token)).Failure); Assert.Equal(BackupValidationFailure.InvalidSchema, (await store.ValidateAsync(brokenForeignKey, Token)).Failure); Assert.Equal("Original", await AccountNameAsync(active));
    }

    [Fact]
    public async Task Unsupported_state_version_recovers_valid_rollback_first_without_reapplying_candidate_or_overwriting_rollback()
    {
        using var temp = new TempDirectory(); var paths = await PendingScenarioAsync(temp.Path);
        FinanceBackupStore.Backup(paths.DatabasePath, paths.RollbackPath, false);
        Execute(paths.DatabasePath, "UPDATE accounts SET name = 'Mutated';");
        await File.WriteAllTextAsync(paths.RestoreStatePath, "{\"Version\":2,\"Phase\":0}", Token);
        var executor = new TrackingExecutor();
        var result = await new PendingRestoreApplier(paths, NullLogger<PendingRestoreApplier>.Instance, executor, new(paths)).ApplyAsync(Token);

        Assert.True(result.PreviousDataRecovered);
        Assert.False(result.RecoveryRequired);
        Assert.Equal("Original", await AccountNameAsync(paths.DatabasePath));
        Assert.DoesNotContain(executor.BackupCalls, call => call.Source == paths.PendingRestorePath);
        Assert.DoesNotContain(executor.BackupCalls, call => call.Destination == paths.RollbackPath);
        var recoveryCall = Assert.Single(executor.BackupCalls);
        Assert.Equal(paths.RollbackPath, recoveryCall.Source);
        Assert.Equal(paths.DatabasePath, recoveryCall.Destination);
        Assert.Equal(0, executor.CandidateAppliedCalls);
        Assert.False(File.Exists(paths.RollbackPath));
        Assert.False(File.Exists(paths.PendingRestorePath));
        Assert.False(File.Exists(paths.RestoreStatePath));
    }

    [Fact]
    public async Task Undefined_phase_is_not_treated_as_normal_startup_and_recovers_valid_rollback_first()
    {
        using var temp = new TempDirectory(); var paths = await PendingScenarioAsync(temp.Path);
        FinanceBackupStore.Backup(paths.DatabasePath, paths.RollbackPath, false);
        Execute(paths.DatabasePath, "UPDATE accounts SET name = 'Mutated';");
        await File.WriteAllTextAsync(paths.RestoreStatePath, "{\"Version\":1,\"Phase\":999}", Token);
        var executor = new TrackingExecutor();
        var result = await new PendingRestoreApplier(paths, NullLogger<PendingRestoreApplier>.Instance, executor, new(paths)).ApplyAsync(Token);

        Assert.NotEqual(PendingRestoreResult.None, result);
        Assert.True(result.PreviousDataRecovered);
        Assert.False(result.RecoveryRequired);
        Assert.Equal("Original", await AccountNameAsync(paths.DatabasePath));
        Assert.DoesNotContain(executor.BackupCalls, call => call.Source == paths.PendingRestorePath);
        Assert.DoesNotContain(executor.BackupCalls, call => call.Destination == paths.RollbackPath);
        var recoveryCall = Assert.Single(executor.BackupCalls);
        Assert.Equal(paths.RollbackPath, recoveryCall.Source);
        Assert.Equal(paths.DatabasePath, recoveryCall.Destination);
        Assert.Equal(0, executor.CandidateAppliedCalls);
        Assert.False(File.Exists(paths.RollbackPath));
        Assert.False(File.Exists(paths.PendingRestorePath));
        Assert.False(File.Exists(paths.RestoreStatePath));
    }

    [Fact]
    public async Task Invalid_or_unsupported_restore_state_without_rollback_fails_safe_with_truthful_message()
    {
        using var temp = new TempDirectory(); var paths = await PendingScenarioAsync(temp.Path);
        Assert.False(File.Exists(paths.RollbackPath));
        await File.WriteAllTextAsync(paths.RestoreStatePath, "{\"Version\":2,\"Phase\":0}", Token);
        var result = await new PendingRestoreApplier(paths, NullLogger<PendingRestoreApplier>.Instance).ApplyAsync(Token);

        Assert.True(result.RecoveryRequired);
        Assert.False(result.RollbackRetained);
        Assert.False(result.Succeeded);
        Assert.False(result.PreviousDataRecovered);
        Assert.NotNull(result.UserMessage);
        Assert.DoesNotContain("rollback", result.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Suma could not safely open your finance data after an interrupted restore. Do not modify or remove Suma data.", result.UserMessage);
        Assert.True(File.Exists(paths.PendingRestorePath));
        Assert.True(File.Exists(paths.RestoreStatePath));
        Assert.Equal("Original", await AccountNameAsync(paths.DatabasePath));
    }

    [Theory]
    [InlineData("{\"Version\":2,\"Phase\":0}")]
    [InlineData("{\"Version\":0,\"Phase\":0}")]
    [InlineData("{\"Version\":1,\"Phase\":999}")]
    [InlineData("{\"Version\":1,\"Phase\":-1}")]
    [InlineData("{malformed-json")]
    public async Task Restore_state_store_rejects_unsupported_versions_and_undefined_phases(string json)
    {
        using var temp = new TempDirectory();
        var paths = new SumaRuntimePaths(Path.Combine(temp.Path, "suma.db"));
        Directory.CreateDirectory(paths.RestoreDirectory);
        await File.WriteAllTextAsync(paths.RestoreStatePath, json, Token);
        var store = new RestoreStateStore(paths);
        Assert.ThrowsAny<Exception>(() => store.Read());
    }

    private static async Task<SumaRuntimePaths> PendingScenarioAsync(string directory) { var active = Path.Combine(directory, "suma.db"); var candidate = Path.Combine(directory, "candidate.suma-backup"); await CreateDatabaseAsync(active, "Original"); await CreateDatabaseAsync(candidate, "Restored"); var paths = new SumaRuntimePaths(active); Directory.CreateDirectory(paths.RestoreDirectory); File.Copy(candidate, paths.PendingRestorePath); return paths; }
    private static async Task<string> AccountNameAsync(string path) { await using var context = Context(path); return (await context.Accounts.SingleAsync(Token)).Name; }
    private static void Execute(string path, string sql) { using var connection = new SqliteConnection($"Data Source={path};Pooling=False"); connection.Open(); using var command = connection.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery(); }

    private static async Task CreateDatabaseAsync(string path, string accountName) { await using var context = Context(path); await context.Database.MigrateAsync(Token); context.Accounts.Add(new Account(accountName, AccountType.Bank, Money.Zero("USD"), "USD", true)); await context.SaveChangesAsync(Token); }
    private static SumaDbContext Context(string path) => new(new DbContextOptionsBuilder<SumaDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options);
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private sealed class TempDirectory : IDisposable { public TempDirectory() { Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Suma-M18-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path); } public string Path { get; } public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); } }
    private sealed class FaultingExecutor : IRestoreExecutor
    {
        public bool FailAfterCandidate { get; set; }
        public bool FailRollbackApplication { get; set; }
        public BackupValidationResult Validate(string path) => FinanceBackupStore.Validate(path);
        public void Backup(string sourcePath, string destinationPath, bool recreateDestination) { if (FailRollbackApplication && Path.GetFileName(sourcePath) == "rollback.suma-backup") throw new IOException("Injected rollback failure."); FinanceBackupStore.Backup(sourcePath, destinationPath, recreateDestination); }
        public void CandidateApplied() { if (FailAfterCandidate) { FailAfterCandidate = false; throw new IOException("Injected post-replacement failure."); } }
    }
    private sealed class TrackingExecutor : IRestoreExecutor
    {
        public List<(string Source, string Destination)> BackupCalls { get; } = [];
        public int CandidateAppliedCalls { get; private set; }
        public BackupValidationResult Validate(string path) => FinanceBackupStore.Validate(path);
        public void Backup(string sourcePath, string destinationPath, bool recreateDestination)
        {
            BackupCalls.Add((sourcePath, destinationPath));
            FinanceBackupStore.Backup(sourcePath, destinationPath, recreateDestination);
        }
        public void CandidateApplied() => CandidateAppliedCalls++;
    }
}
