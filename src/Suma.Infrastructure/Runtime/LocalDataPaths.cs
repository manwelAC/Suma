namespace Suma.Infrastructure.Runtime;

public static class LocalDataPaths
{
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
