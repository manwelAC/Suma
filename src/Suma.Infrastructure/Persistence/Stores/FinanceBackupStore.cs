using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Suma.Application.Abstractions.Persistence;
using Suma.Infrastructure.Runtime;

namespace Suma.Infrastructure.Persistence.Stores;

public sealed class FinanceBackupStore(SumaRuntimePaths paths) : IFinanceBackupStore
{
    internal static readonly string[] SupportedMigrations =
    [
        "20260901061524_InitialFinancialSchema",
        "20260904073000_AddAccountNumberToAccounts"
    ];
    private static readonly string[] RequiredTables = ["accounts", "budget_allocations", "budgets", "categories", "goal_contributions", "recurring_occurrences", "recurring_transactions", "savings_goals", "transactions", "__EFMigrationsHistory"];

    public Task CreateBackupAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try { Backup(paths.DatabasePath, destinationPath, true); }
        catch { if (File.Exists(destinationPath)) File.Delete(destinationPath); throw; }
        return Task.CompletedTask;
    }

    public Task<BackupValidationResult> ValidateAsync(string candidatePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(Validate(candidatePath));
    }

    public async Task<string> StageAsync(string candidatePath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(paths.RestoreDirectory); var staged = Path.Combine(paths.RestoreDirectory, $"staged-{Guid.NewGuid():N}.suma-backup");
        try
        {
            await using (var source = new FileStream(candidatePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            await using (var target = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.WriteThrough))
            { await source.CopyToAsync(target, cancellationToken); await target.FlushAsync(cancellationToken); target.Flush(true); }
        }
        catch { if (File.Exists(staged)) File.Delete(staged); throw; }
        var validation = Validate(staged); if (!validation.IsValid) { File.Delete(staged); throw new InvalidDataException("Staged backup validation failed."); }
        return staged;
    }

    public Task MarkPendingAsync(string stagedPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); var validation = Validate(stagedPath); if (!validation.IsValid) throw new InvalidDataException("Staged backup is no longer valid.");
        Directory.CreateDirectory(paths.RestoreDirectory); File.Move(stagedPath, paths.PendingRestorePath, true); return Task.CompletedTask;
    }

    public Task DiscardStagedAsync(string stagedPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); var fullPath = Path.GetFullPath(stagedPath); var root = Path.GetFullPath(paths.RestoreDirectory) + Path.DirectorySeparatorChar;
        if (fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(fullPath).StartsWith("staged-", StringComparison.Ordinal) && File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }

    public async Task ResetDataAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var connectionString = ConnectionString(paths.DatabasePath, SqliteOpenMode.ReadWrite);
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            PRAGMA foreign_keys = OFF;
            DELETE FROM goal_contributions;
            DELETE FROM savings_goals;
            DELETE FROM recurring_occurrences;
            DELETE FROM recurring_transactions;
            DELETE FROM budget_allocations;
            DELETE FROM budgets;
            DELETE FROM transactions;
            DELETE FROM accounts;
            DELETE FROM categories;
            PRAGMA foreign_keys = ON;
            VACUUM;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal static void Backup(string sourcePath, string destinationPath, bool recreateDestination)
    {
        if (Path.GetFullPath(sourcePath).Equals(Path.GetFullPath(destinationPath), StringComparison.OrdinalIgnoreCase)) throw new IOException("Backup destination must differ from the active database.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destinationPath))!); if (recreateDestination && File.Exists(destinationPath)) File.Delete(destinationPath);
        using var source = new SqliteConnection(ConnectionString(sourcePath, SqliteOpenMode.ReadOnly)); using var destination = new SqliteConnection(ConnectionString(destinationPath, SqliteOpenMode.ReadWriteCreate)); source.Open(); destination.Open(); source.BackupDatabase(destination);
    }

    internal static BackupValidationResult Validate(string path)
    {
        if (!File.Exists(path)) return new(false, BackupValidationFailure.InvalidDatabase);
        try
        {
            using var connection = new SqliteConnection(ConnectionString(path, SqliteOpenMode.ReadOnly)); connection.Open();
            using (var command = connection.CreateCommand()) { command.CommandText = "PRAGMA integrity_check;"; if (!string.Equals(command.ExecuteScalar()?.ToString(), "ok", StringComparison.OrdinalIgnoreCase)) return new(false, BackupValidationFailure.InvalidDatabase); }
            using (var command = connection.CreateCommand()) { command.CommandText = "PRAGMA foreign_key_check;"; using var reader = command.ExecuteReader(); if (reader.Read()) return new(false, BackupValidationFailure.InvalidSchema); }
            var tables = new HashSet<string>(StringComparer.Ordinal);
            using (var command = connection.CreateCommand()) { command.CommandText = "SELECT name FROM sqlite_master WHERE type='table';"; using var reader = command.ExecuteReader(); while (reader.Read()) tables.Add(reader.GetString(0)); }
            if (RequiredTables.Any(table => !tables.Contains(table))) return new(false, BackupValidationFailure.InvalidSchema);
            if (!HasCurrentMappedSchema(connection)) return new(false, BackupValidationFailure.InvalidSchema);
            var migrations = new List<string>(); using (var command = connection.CreateCommand()) { command.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;"; using var reader = command.ExecuteReader(); while (reader.Read()) migrations.Add(reader.GetString(0)); }
            return migrations.SequenceEqual(SupportedMigrations, StringComparer.Ordinal) ? new(true, BackupValidationFailure.None) : new(false, BackupValidationFailure.UnsupportedVersion);
        }
        catch (Exception exception) when (exception is SqliteException or InvalidOperationException) { return new(false, BackupValidationFailure.InvalidDatabase); }
    }
    private static bool HasCurrentMappedSchema(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<SumaDbContext>().UseSqlite(connection).Options; using var context = new SumaDbContext(options);
        var mappings = context.Model.GetEntityTypes().Where(entity => entity.GetTableName() is not null).GroupBy(entity => new { Table = entity.GetTableName()!, Schema = entity.GetSchema() });
        foreach (var mapping in mappings)
        {
            var storeObject = StoreObjectIdentifier.Table(mapping.Key.Table, mapping.Key.Schema); var expected = mapping.SelectMany(entity => entity.GetProperties()).Select(property => property.GetColumnName(storeObject)).Where(name => name is not null).Cast<string>().ToHashSet(StringComparer.Ordinal);
            var actual = new HashSet<string>(StringComparer.Ordinal); using (var command = connection.CreateCommand()) { command.CommandText = $"PRAGMA table_info({Quote(mapping.Key.Table)});"; using var reader = command.ExecuteReader(); while (reader.Read()) actual.Add(reader.GetString(1)); }
            if (!expected.IsSubsetOf(actual)) return false;
            using var probe = connection.CreateCommand(); probe.CommandText = $"SELECT {string.Join(", ", expected.Select(Quote))} FROM {Quote(mapping.Key.Table)} LIMIT 1;"; using var ignored = probe.ExecuteReader();
        }
        return true;
    }
    private static string Quote(string identifier) => '"' + identifier.Replace("\"", "\"\"") + '"';
    private static string ConnectionString(string path, SqliteOpenMode mode) => new SqliteConnectionStringBuilder { DataSource = Path.GetFullPath(path), Mode = mode, Pooling = false, ForeignKeys = true }.ToString();
}
