using Microsoft.Data.Sqlite;

namespace StardewGallery;

internal sealed class GalleryDatabase(string databasePath, Action<string>? logger = null) : IDisposable
{
    private SqliteConnection? connection;
    private bool disposed;

    internal string DatabasePath { get; } = databasePath;

    internal bool IsAvailable => connection is not null;

    internal SqliteConnection? Connection => connection;

    internal bool Open()
    {
        try
        {
            string? directory = Path.GetDirectoryName(DatabasePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            connection = new SqliteConnection($"Data Source={DatabasePath}");
            connection.Open();
            connection.CreateCollation("ORDINAL_NOCASE",
                (left, right) => StringComparer.OrdinalIgnoreCase.Compare(left, right));
            using (var pragma = connection.CreateCommand())
            {
                pragma.CommandText = "PRAGMA foreign_keys = ON;";
                pragma.ExecuteNonQuery();
            }
            using (var busy = connection.CreateCommand())
            {
                busy.CommandText = "PRAGMA busy_timeout = 5000;";
                busy.ExecuteNonQuery();
            }
            return true;
        }
        catch (Exception error)
        {
            logger?.Invoke($"SQLite 打开失败：\n{error}");
            Dispose();
            return false;
        }
    }

    internal int SchemaVersion()
    {
        SqliteConnection? conn = connection;
        if (conn is null)
            return -1;
        try
        {
            using var command = conn.CreateCommand();
            command.CommandText = "PRAGMA user_version;";
            object? result = command.ExecuteScalar();
            return result is null ? -1 : Convert.ToInt32(result);
        }
        catch (Exception error)
        {
            logger?.Invoke($"SQLite schema 版本读取失败：{error.Message}");
            return -1;
        }
    }

    internal bool EnsureSchema()
    {
        SqliteConnection? conn = connection;
        if (conn is null)
            return false;
        int version = SchemaVersion();
        try
        {
            if (version == 0)
            {
                using var transaction = conn.BeginTransaction();
                using (var create = conn.CreateCommand())
                {
                    create.Transaction = transaction;
                    create.CommandText = GallerySchema.CreateCommandText;
                    create.ExecuteNonQuery();
                }
                transaction.Commit();
                return true;
            }
            if (version == GallerySchema.CurrentVersion)
                return true;
            logger?.Invoke($"SQLite schema 版本 {version} 超出当前支持的 {GallerySchema.CurrentVersion}，本会话禁用 SQLite。");
            return false;
        }
        catch (Exception error)
        {
            logger?.Invoke($"SQLite schema 初始化失败：{error.Message}");
            return false;
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        connection?.Dispose();
        connection = null;
    }
}
