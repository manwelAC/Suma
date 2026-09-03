namespace Suma.Infrastructure.Runtime;

public static class LocalDataPaths
{
    public static string BuildApplicationDirectory(string localApplicationDataRoot) => Path.Combine(localApplicationDataRoot, "Suma");
    public static string BuildDatabasePath(string localApplicationDataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localApplicationDataRoot);
        return Path.Combine(localApplicationDataRoot, "Suma", "suma.db");
    }

    public static string GetRuntimeDatabasePath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var databasePath = BuildDatabasePath(root);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        return databasePath;
    }
}

public sealed record SumaRuntimePaths(string DatabasePath)
{
    public string ApplicationDirectory => Path.GetDirectoryName(DatabasePath)!;
    public string SecurityPath => Path.Combine(ApplicationDirectory, "security.json");
    public string RestoreDirectory => Path.Combine(ApplicationDirectory, "restore");
    public string PendingRestorePath => Path.Combine(RestoreDirectory, "pending.suma-backup");
    public string RollbackPath => Path.Combine(RestoreDirectory, "rollback.suma-backup");
    public string RestoreStatePath => Path.Combine(RestoreDirectory, "restore-state.json");
}
