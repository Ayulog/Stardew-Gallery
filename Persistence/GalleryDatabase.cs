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
                if (!ValidateVersion2Schema(conn, transaction))
                    throw new InvalidOperationException("SQLite v2 schema validation failed after create.");
                transaction.Commit();
                return true;
            }
            if (version == 1)
            {
                using var transaction = conn.BeginTransaction();
                using (var migrate = conn.CreateCommand())
                {
                    migrate.Transaction = transaction;
                    migrate.CommandText = GallerySchema.MigrateVersion1To2CommandText;
                    migrate.ExecuteNonQuery();
                }
                if (!ValidateVersion2Schema(conn, transaction))
                    throw new InvalidOperationException("SQLite v2 schema validation failed after migration.");
                transaction.Commit();
                return true;
            }
            if (version == GallerySchema.CurrentVersion)
            {
                if (ValidateVersion2Schema(conn, null))
                    return true;
                logger?.Invoke("SQLite v2 schema validation failed; this session will not use SQLite.");
                return false;
            }
            logger?.Invoke($"SQLite schema 版本 {version} 超出当前支持的 {GallerySchema.CurrentVersion}，本会话禁用 SQLite。");
            return false;
        }
        catch (Exception error)
        {
            logger?.Invoke($"SQLite schema 初始化失败：{error.Message}");
            return false;
        }
    }

    private static bool ValidateVersion2Schema(SqliteConnection conn, SqliteTransaction? transaction)
    {
        if (!ValidateCoreSchema(conn, transaction))
            return false;

        using (var table = conn.CreateCommand())
        {
            table.Transaction = transaction;
            table.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='historical_execution_contexts';";
            if (Convert.ToInt32(table.ExecuteScalar()) != 1)
                return false;
        }

        Dictionary<string, (string Type, bool NotNull, bool PrimaryKey)> columns = new(StringComparer.Ordinal);
        using (var tableInfo = conn.CreateCommand())
        {
            tableInfo.Transaction = transaction;
            tableInfo.CommandText = "PRAGMA table_info(historical_execution_contexts);";
            using SqliteDataReader reader = tableInfo.ExecuteReader();
            while (reader.Read())
                columns[reader.GetString(1)] = (reader.GetString(2), reader.GetInt32(3) == 1, reader.GetInt32(5) == 1);
        }
        if (!HasColumn(columns, "context_pk", "INTEGER", notNull: false, primaryKey: true)
            || !HasColumn(columns, "record_fk", "INTEGER", notNull: true, primaryKey: false)
            || !HasColumn(columns, "schema_version", "INTEGER", notNull: true, primaryKey: false)
            || !HasColumn(columns, "completion_status", "TEXT", notNull: true, primaryKey: false)
            || !HasColumn(columns, "execution_json", "TEXT", notNull: true, primaryKey: false))
            return false;

        bool uniqueRecord = false;
        using (var indexes = conn.CreateCommand())
        {
            indexes.Transaction = transaction;
            indexes.CommandText = "PRAGMA index_list(historical_execution_contexts);";
            using SqliteDataReader reader = indexes.ExecuteReader();
            List<string> uniqueIndexes = [];
            while (reader.Read())
            {
                if (reader.GetInt32(2) == 1)
                    uniqueIndexes.Add(reader.GetString(1));
            }
            reader.Close();
            foreach (string index in uniqueIndexes)
            {
                using var info = conn.CreateCommand();
                info.Transaction = transaction;
                info.CommandText = $"PRAGMA index_info(\"{index.Replace("\"", "\"\"")}\");";
                using SqliteDataReader indexReader = info.ExecuteReader();
                List<string> names = [];
                while (indexReader.Read())
                    names.Add(indexReader.GetString(2));
                if (names.SequenceEqual(["record_fk"], StringComparer.Ordinal))
                    uniqueRecord = true;
            }
        }
        if (!uniqueRecord)
            return false;

        bool cascadeForeignKey = false;
        using (var foreignKeys = conn.CreateCommand())
        {
            foreignKeys.Transaction = transaction;
            foreignKeys.CommandText = "PRAGMA foreign_key_list(historical_execution_contexts);";
            using SqliteDataReader reader = foreignKeys.ExecuteReader();
            while (reader.Read())
            {
                if (StringComparer.Ordinal.Equals(reader.GetString(2), "historical_event_records")
                    && StringComparer.Ordinal.Equals(reader.GetString(3), "record_fk")
                    && StringComparer.Ordinal.Equals(reader.GetString(4), "record_pk")
                    && StringComparer.OrdinalIgnoreCase.Equals(reader.GetString(6), "CASCADE"))
                    cascadeForeignKey = true;
            }
        }
        if (!cascadeForeignKey)
            return false;

        using var foreignKeyCheck = conn.CreateCommand();
        foreignKeyCheck.Transaction = transaction;
        foreignKeyCheck.CommandText = "PRAGMA foreign_key_check;";
        using SqliteDataReader violations = foreignKeyCheck.ExecuteReader();
        return !violations.Read();
    }

    private static bool HasColumn(
        IReadOnlyDictionary<string, (string Type, bool NotNull, bool PrimaryKey)> columns,
        string name,
        string type,
        bool notNull,
        bool primaryKey)
        => columns.TryGetValue(name, out var column)
            && StringComparer.OrdinalIgnoreCase.Equals(column.Type, type)
            && column.NotNull == notNull
            && column.PrimaryKey == primaryKey;

    private static bool ValidateCoreSchema(SqliteConnection conn, SqliteTransaction? transaction)
        => ValidateColumns(conn, transaction, "save_profiles",
            ("profile_pk", "INTEGER", false, true),
            ("farm_unique_id", "INTEGER", true, false),
            ("player_unique_id", "INTEGER", true, false),
            ("save_folder_name", "TEXT", false, false),
            ("farmer_name", "TEXT", false, false),
            ("created_at", "INTEGER", true, false),
            ("last_seen_at", "INTEGER", true, false))
        && HasUniqueIndex(conn, transaction, "save_profiles", "farm_unique_id", "player_unique_id")
        && ValidateColumns(conn, transaction, "events",
            ("event_pk", "INTEGER", false, true),
            ("asset_name", "TEXT", true, false),
            ("event_id", "TEXT", true, false))
        && HasUniqueIndex(conn, transaction, "events", "asset_name", "event_id")
        && HasEventAssetCollation(conn, transaction)
        && ValidateColumns(conn, transaction, "observed_variants",
            ("variant_pk", "INTEGER", false, true),
            ("event_fk", "INTEGER", true, false),
            ("root_definition_hash", "TEXT", true, false),
            ("playback_hash", "TEXT", true, false),
            ("root_script_hash", "TEXT", true, false),
            ("raw_event_key", "TEXT", true, false),
            ("root_script", "TEXT", true, false),
            ("playback_json", "TEXT", true, false))
        && HasUniqueIndex(conn, transaction, "observed_variants", "event_fk", "root_definition_hash", "playback_hash")
        && HasForeignKey(conn, transaction, "observed_variants", "event_fk", "events", "event_pk", "CASCADE")
        && ValidateColumns(conn, transaction, "variant_observation_summaries",
            ("summary_pk", "INTEGER", false, true),
            ("profile_fk", "INTEGER", true, false),
            ("variant_fk", "INTEGER", true, false),
            ("first_observed_at", "INTEGER", true, false),
            ("last_observed_at", "INTEGER", true, false),
            ("last_observed_location_name", "TEXT", false, false),
            ("last_observed_locale", "TEXT", false, false))
        && HasUniqueIndex(conn, transaction, "variant_observation_summaries", "profile_fk", "variant_fk")
        && HasForeignKey(conn, transaction, "variant_observation_summaries", "profile_fk", "save_profiles", "profile_pk", "CASCADE")
        && HasForeignKey(conn, transaction, "variant_observation_summaries", "variant_fk", "observed_variants", "variant_pk", "CASCADE")
        && ValidateColumns(conn, transaction, "historical_event_records",
            ("record_pk", "INTEGER", false, true),
            ("profile_fk", "INTEGER", true, false),
            ("variant_fk", "INTEGER", true, false),
            ("watched_at", "INTEGER", true, false),
            ("location_name", "TEXT", false, false),
            ("locale", "TEXT", false, false))
        && HasForeignKey(conn, transaction, "historical_event_records", "profile_fk", "save_profiles", "profile_pk", "CASCADE")
        && HasForeignKey(conn, transaction, "historical_event_records", "variant_fk", "observed_variants", "variant_pk", "CASCADE");

    private static bool ValidateColumns(SqliteConnection conn, SqliteTransaction? transaction, string table,
        params (string Name, string Type, bool NotNull, bool PrimaryKey)[] expected)
    {
        Dictionary<string, (string Type, bool NotNull, bool PrimaryKey)> actual = new(StringComparer.Ordinal);
        using var command = conn.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"PRAGMA table_info(\"{table}\");";
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
            actual[reader.GetString(1)] = (reader.GetString(2), reader.GetInt32(3) == 1, reader.GetInt32(5) == 1);
        return expected.All(column => HasColumn(actual, column.Name, column.Type, column.NotNull, column.PrimaryKey));
    }

    private static bool HasUniqueIndex(SqliteConnection conn, SqliteTransaction? transaction, string table,
        params string[] expectedColumns)
    {
        using var indexes = conn.CreateCommand();
        indexes.Transaction = transaction;
        indexes.CommandText = $"PRAGMA index_list(\"{table}\");";
        using SqliteDataReader reader = indexes.ExecuteReader();
        List<string> uniqueIndexes = [];
        while (reader.Read())
        {
            if (reader.GetInt32(2) == 1)
                uniqueIndexes.Add(reader.GetString(1));
        }
        reader.Close();
        foreach (string index in uniqueIndexes)
        {
            using var info = conn.CreateCommand();
            info.Transaction = transaction;
            info.CommandText = $"PRAGMA index_info(\"{index.Replace("\"", "\"\"")}\");";
            using SqliteDataReader indexReader = info.ExecuteReader();
            List<string> names = [];
            while (indexReader.Read())
                names.Add(indexReader.GetString(2));
            if (names.SequenceEqual(expectedColumns, StringComparer.Ordinal))
                return true;
        }
        return false;
    }

    private static bool HasForeignKey(SqliteConnection conn, SqliteTransaction? transaction, string table,
        string from, string targetTable, string targetColumn, string onDelete)
    {
        using var command = conn.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"PRAGMA foreign_key_list(\"{table}\");";
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (StringComparer.Ordinal.Equals(reader.GetString(2), targetTable)
                && StringComparer.Ordinal.Equals(reader.GetString(3), from)
                && StringComparer.Ordinal.Equals(reader.GetString(4), targetColumn)
                && StringComparer.OrdinalIgnoreCase.Equals(reader.GetString(6), onDelete))
                return true;
        }
        return false;
    }

    private static bool HasEventAssetCollation(SqliteConnection conn, SqliteTransaction? transaction)
    {
        using var command = conn.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT sql FROM sqlite_master WHERE type='table' AND name='events';";
        string? sql = command.ExecuteScalar() as string;
        return sql?.Contains("asset_name TEXT NOT NULL COLLATE ORDINAL_NOCASE", StringComparison.OrdinalIgnoreCase) == true;
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
