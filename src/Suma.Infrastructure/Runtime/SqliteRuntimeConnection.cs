using Microsoft.Data.Sqlite;

namespace Suma.Infrastructure.Runtime;

public static class SqliteRuntimeConnection
{
    public static string Build(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        return new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true,
            Pooling = false
        }.ToString();
    }
}
