using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace StardewGallery;

internal sealed class HistoryRepository(GalleryDatabase database, SaveProfileKey profile, Action<string>? logger = null) : IDisposable
{
    internal bool WriteDisabled { get; set; }

    public void Dispose()
    {
        // The repository does not own the GalleryDatabase; disposal is a no-op.
        // Connection lifecycle is managed by GalleryDatabase via ModEntry.
    }

    internal long? EnsureProfile(string? saveFolderName, string? farmerName, DateTimeOffset now)
    {
        SqliteConnection? conn = database.Connection;
        if (conn is null)
            return null;
        long stored = profile.StoredFarmUniqueId;
        long nowMs = now.ToUnixTimeMilliseconds();
        using var command = conn.CreateCommand();
        command.CommandText =
            """
            INSERT INTO save_profiles (farm_unique_id, player_unique_id, save_folder_name, farmer_name, created_at, last_seen_at)
            VALUES ($farm, $player, $folder, $farmer, $now, $now)
            ON CONFLICT(farm_unique_id, player_unique_id) DO UPDATE SET
                save_folder_name = COALESCE($folder, save_profiles.save_folder_name),
                farmer_name = COALESCE($farmer, save_profiles.farmer_name),
                last_seen_at = $now;
            """;
        command.Parameters.AddWithValue("$farm", stored);
        command.Parameters.AddWithValue("$player", profile.PlayerUniqueId);
        command.Parameters.AddWithValue("$folder", saveFolderName);
        command.Parameters.AddWithValue("$farmer", farmerName);
        command.Parameters.AddWithValue("$now", nowMs);
        command.ExecuteNonQuery();

        using var select = conn.CreateCommand();
        select.CommandText = "SELECT profile_pk FROM save_profiles WHERE farm_unique_id = $farm AND player_unique_id = $player;";
        select.Parameters.AddWithValue("$farm", stored);
        select.Parameters.AddWithValue("$player", profile.PlayerUniqueId);
        return Convert.ToInt64(select.ExecuteScalar());
    }

    internal void UpsertObservation(ObservedVariant variant, VariantObservationSummary summary)
    {
        SqliteConnection? conn = database.Connection;
        if (conn is null || WriteDisabled)
            return;
        using var transaction = conn.BeginTransaction();
        long eventPk = EnsureEvent(conn, transaction, variant.Key.Identity);
        long variantPk = EnsureVariant(conn, transaction, variant, eventPk);
        UpsertSummary(conn, transaction, variantPk, summary);
        transaction.Commit();
    }

    private static long EnsureEvent(SqliteConnection conn, SqliteTransaction tx, EventIdentity identity)
    {
        using var command = conn.CreateCommand();
        command.Transaction = tx;
        command.CommandText =
            """
            INSERT OR IGNORE INTO events (asset_name, event_id) VALUES ($asset, $event);
            """;
        command.Parameters.AddWithValue("$asset", identity.AssetName);
        command.Parameters.AddWithValue("$event", identity.EventId);
        command.ExecuteNonQuery();

        using var select = conn.CreateCommand();
        select.Transaction = tx;
        select.CommandText = "SELECT event_pk FROM events WHERE asset_name = $asset AND event_id = $event;";
        select.Parameters.AddWithValue("$asset", identity.AssetName);
        select.Parameters.AddWithValue("$event", identity.EventId);
        return Convert.ToInt64(select.ExecuteScalar());
    }

    private static long EnsureVariant(SqliteConnection conn, SqliteTransaction tx, ObservedVariant variant, long eventPk)
    {
        string playbackJson = JsonSerializer.Serialize(new PlaybackPayload(
            ToMutableAssets(variant.Playback.EventAssets),
            ToMutableStrings(variant.Playback.Translations)));
        using var command = conn.CreateCommand();
        command.Transaction = tx;
        command.CommandText =
            """
            INSERT INTO observed_variants
                (event_fk, root_definition_hash, playback_hash, root_script_hash, raw_event_key, root_script, playback_json)
            VALUES
                ($event, $rootdef, $playback, $rootscript, $rawkey, $root, $playback_json)
            ON CONFLICT(event_fk, root_definition_hash, playback_hash) DO UPDATE SET
                root_script_hash = excluded.root_script_hash,
                raw_event_key = excluded.raw_event_key,
                root_script = excluded.root_script,
                playback_json = excluded.playback_json;
            """;
        command.Parameters.AddWithValue("$event", eventPk);
        command.Parameters.AddWithValue("$rootdef", variant.Key.RootDefinitionHash);
        command.Parameters.AddWithValue("$playback", variant.Key.PlaybackHash);
        command.Parameters.AddWithValue("$rootscript", variant.RootScriptHash);
        command.Parameters.AddWithValue("$rawkey", variant.RawEventKey);
        command.Parameters.AddWithValue("$root", variant.Playback.RootScript);
        command.Parameters.AddWithValue("$playback_json", playbackJson);
        command.ExecuteNonQuery();

        using var select = conn.CreateCommand();
        select.Transaction = tx;
        select.CommandText =
            """
            SELECT variant_pk FROM observed_variants
            WHERE event_fk = $event AND root_definition_hash = $rootdef AND playback_hash = $playback;
            """;
        select.Parameters.AddWithValue("$event", eventPk);
        select.Parameters.AddWithValue("$rootdef", variant.Key.RootDefinitionHash);
        select.Parameters.AddWithValue("$playback", variant.Key.PlaybackHash);
        return Convert.ToInt64(select.ExecuteScalar());
    }

    private void UpsertSummary(SqliteConnection conn, SqliteTransaction tx, long variantPk, VariantObservationSummary summary)
    {
        long? profilePk = EnsureProfile(conn, tx);
        if (profilePk is null)
            return;
        long firstMs = summary.FirstObservedAt.ToUnixTimeMilliseconds();
        long lastMs = summary.LastObservedAt.ToUnixTimeMilliseconds();
        using var command = conn.CreateCommand();
        command.Transaction = tx;
        command.CommandText =
            """
            INSERT INTO variant_observation_summaries
                (profile_fk, variant_fk, first_observed_at, last_observed_at, last_observed_location_name, last_observed_locale)
            VALUES
                ($profile, $variant, $first, $last, $loc, $locale)
            ON CONFLICT(profile_fk, variant_fk) DO UPDATE SET
                first_observed_at = MIN(variant_observation_summaries.first_observed_at, excluded.first_observed_at),
                last_observed_at = MAX(variant_observation_summaries.last_observed_at, excluded.last_observed_at),
                last_observed_location_name =
                    CASE WHEN excluded.last_observed_at >= variant_observation_summaries.last_observed_at
                         THEN excluded.last_observed_location_name
                         ELSE variant_observation_summaries.last_observed_location_name END,
                last_observed_locale =
                    CASE WHEN excluded.last_observed_at >= variant_observation_summaries.last_observed_at
                         THEN excluded.last_observed_locale
                         ELSE variant_observation_summaries.last_observed_locale END;
            """;
        command.Parameters.AddWithValue("$profile", profilePk.Value);
        command.Parameters.AddWithValue("$variant", variantPk);
        command.Parameters.AddWithValue("$first", firstMs);
        command.Parameters.AddWithValue("$last", lastMs);
        command.Parameters.AddWithValue("$loc", summary.LastObservedLocationName);
        command.Parameters.AddWithValue("$locale", summary.LastObservedLocale);
        command.ExecuteNonQuery();
    }

    private long? EnsureProfile(SqliteConnection conn, SqliteTransaction tx)
    {
        using var command = conn.CreateCommand();
        command.Transaction = tx;
        command.CommandText =
            """
            INSERT OR IGNORE INTO save_profiles (farm_unique_id, player_unique_id, created_at, last_seen_at)
            VALUES ($farm, $player, 0, 0);
            """;
        command.Parameters.AddWithValue("$farm", profile.StoredFarmUniqueId);
        command.Parameters.AddWithValue("$player", profile.PlayerUniqueId);
        command.ExecuteNonQuery();

        using var select = conn.CreateCommand();
        select.Transaction = tx;
        select.CommandText = "SELECT profile_pk FROM save_profiles WHERE farm_unique_id = $farm AND player_unique_id = $player;";
        select.Parameters.AddWithValue("$farm", profile.StoredFarmUniqueId);
        select.Parameters.AddWithValue("$player", profile.PlayerUniqueId);
        return Convert.ToInt64(select.ExecuteScalar());
    }

    internal NaturalOccurrenceWriteResult AddNaturalOccurrence(
        ObservedVariant variant,
        VariantObservationSummary summary,
        HistoricalEventRecord record,
        HistoricalExecutionContext? context)
    {
        SqliteConnection? conn = database.Connection;
        if (conn is null || WriteDisabled)
            return new NaturalOccurrenceWriteResult(-1, ExecutionContextWriteStatus.Failed);

        if (record.Variant != variant.Key || summary.Variant != variant.Key)
            throw new ArgumentException("Natural occurrence variant, summary, and record keys must match.");

        string? executionJson = null;
        ExecutionContextWriteStatus contextStatus = context is null
            ? ExecutionContextWriteStatus.Missing
            : ExecutionContextWriteStatus.Rejected;
        if (context is not null
            && StringComparer.Ordinal.Equals(context.PlaybackHash, variant.Key.PlaybackHash)
            && HistoricalExecutionContextCodec.TryEncode(context, out string encoded))
        {
            executionJson = encoded;
            contextStatus = ExecutionContextWriteStatus.Stored;
        }

        using var transaction = conn.BeginTransaction();
        long eventPk = EnsureEvent(conn, transaction, variant.Key.Identity);
        long variantPk = EnsureVariant(conn, transaction, variant, eventPk);
        UpsertSummary(conn, transaction, variantPk, summary);
        long? profilePk = EnsureProfile(conn, transaction);
        if (profilePk is null)
            throw new InvalidOperationException("Natural occurrence profile could not be resolved.");

        long recordId;
        using var command = conn.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO historical_event_records (profile_fk, variant_fk, watched_at, location_name, locale)
            VALUES ($profile, $variant, $watched, $loc, $locale);
            """;
        command.Parameters.AddWithValue("$profile", profilePk.Value);
        command.Parameters.AddWithValue("$variant", variantPk);
        command.Parameters.AddWithValue("$watched", record.WatchedAt.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("$loc", record.LocationName);
        command.Parameters.AddWithValue("$locale", record.Locale);
        command.ExecuteNonQuery();
        using (var identity = conn.CreateCommand())
        {
            identity.Transaction = transaction;
            identity.CommandText = "SELECT last_insert_rowid();";
            recordId = Convert.ToInt64(identity.ExecuteScalar());
        }

        if (executionJson is not null)
        {
            Execute(transaction, "SAVEPOINT execution_context;");
            try
            {
                using var execution = conn.CreateCommand();
                execution.Transaction = transaction;
                execution.CommandText =
                    """
                    INSERT INTO historical_execution_contexts
                        (record_fk, schema_version, completion_status, execution_json)
                    VALUES ($record, $schema, $completion, $json);
                    """;
                execution.Parameters.AddWithValue("$record", recordId);
                execution.Parameters.AddWithValue("$schema", context!.SchemaVersion);
                execution.Parameters.AddWithValue("$completion", context.Completion.ToString());
                execution.Parameters.AddWithValue("$json", executionJson);
                execution.ExecuteNonQuery();
                Execute(transaction, "RELEASE execution_context;");
            }
            catch (Exception error)
            {
                Execute(transaction, "ROLLBACK TO execution_context;");
                Execute(transaction, "RELEASE execution_context;");
                contextStatus = ExecutionContextWriteStatus.Failed;
                logger?.Invoke($"execution context persistence failed for record {recordId}: {error.Message}");
            }
        }

        transaction.Commit();
        return new NaturalOccurrenceWriteResult(recordId, contextStatus);
    }

    private static void Execute(SqliteTransaction transaction, string sql)
    {
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    internal void ImportLegacy(IEnumerable<WatchedEventSnapshot> snapshots)
    {
        foreach (WatchedEventSnapshot snapshot in snapshots)
        {
            LegacyHistoryProjection projection = LegacyHistoryAdapter.From(snapshot);
            UpsertObservation(projection.Variant, projection.Observation);
        }
    }

    internal IReadOnlyList<WatchedEventSnapshot> LoadAllSnapshotsForProfile()
    {
        SqliteConnection? conn = database.Connection;
        if (conn is null)
            return [];
        long? profilePk = ResolveProfilePk(conn);
        if (profilePk is null)
            return [];

        List<WatchedEventSnapshot> rows = [];
        using var command = conn.CreateCommand();
        command.CommandText =
            """
            SELECT e.asset_name, e.event_id,
                   v.root_definition_hash, v.playback_hash, v.root_script_hash, v.raw_event_key, v.root_script,
                   v.playback_json,
                   s.first_observed_at, s.last_observed_at, s.last_observed_location_name, s.last_observed_locale
            FROM observed_variants v
            JOIN events e ON e.event_pk = v.event_fk
            JOIN variant_observation_summaries s ON s.variant_fk = v.variant_pk
            WHERE s.profile_fk = $profile;
            """;
        command.Parameters.AddWithValue("$profile", profilePk.Value);
        using (SqliteDataReader reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                string asset = reader.GetString(0);
                string eventId = reader.GetString(1);
                if (TryMaterializeSnapshot(reader, 2, asset, eventId, out WatchedEventSnapshot? snapshot) && snapshot is not null)
                    rows.Add(snapshot);
            }
        }
        return rows;
    }

    internal IReadOnlyList<PersistedHistoricalOccurrence> LoadHistoricalOccurrencesForProfile()
    {
        SqliteConnection? conn = database.Connection;
        if (conn is null)
            return [];
        long? profilePk = ResolveProfilePk(conn);
        if (profilePk is null)
            return [];

        List<PersistedHistoricalOccurrence> rows = [];
        using var command = conn.CreateCommand();
        command.CommandText =
            """
            SELECT h.record_pk, e.asset_name, e.event_id,
                   v.root_definition_hash, v.playback_hash,
                   h.watched_at, h.location_name, h.locale,
                   c.schema_version, c.completion_status, c.execution_json
            FROM historical_event_records h
            JOIN observed_variants v ON v.variant_pk = h.variant_fk
            JOIN events e ON e.event_pk = v.event_fk
            LEFT JOIN historical_execution_contexts c ON c.record_fk = h.record_pk
            WHERE h.profile_fk = $profile
            ORDER BY h.watched_at DESC, h.record_pk DESC;
            """;
        command.Parameters.AddWithValue("$profile", profilePk.Value);
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            EventIdentity identity = new(reader.GetString(1), reader.GetString(2));
            ObservedVariantKey variant = new(identity, reader.GetString(3), reader.GetString(4));
            HistoricalEventRecord record = new(
                variant,
                DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(5)),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7));

            HistoricalExecutionContextLoad execution;
            try
            {
                execution = reader.IsDBNull(8)
                    ? HistoricalExecutionContextCodec.Decode(null, variant.PlaybackHash)
                    : HistoricalExecutionContextCodec.Decode(reader.GetString(10), variant.PlaybackHash);
                if (execution.Context is HistoricalExecutionContext context
                    && (reader.GetInt32(8) != context.SchemaVersion
                        || !StringComparer.Ordinal.Equals(reader.GetString(9), context.Completion.ToString())))
                    execution = InvalidExecutionContext();
            }
            catch (Exception error)
            {
                logger?.Invoke($"execution context columns are invalid for record {reader.GetInt64(0)}: {error.Message}");
                execution = InvalidExecutionContext();
            }
            rows.Add(new PersistedHistoricalOccurrence(reader.GetInt64(0), record, execution));
        }
        return rows;
    }

    private static HistoricalExecutionContextLoad InvalidExecutionContext()
        => new(HistoricalExecutionContextState.Invalid, null, ExecutionContextInvalidReason.InvalidModel);

    internal IReadOnlyList<WatchedEventSnapshot> GetCompatibilityVersions(EventIdentity identity)
    {
        SqliteConnection? conn = database.Connection;
        if (conn is null)
            return [];
        long eventPk = ResolveEventPk(conn, identity);
        if (eventPk < 0)
            return [];
        long? profilePk = ResolveProfilePk(conn);
        if (profilePk is null)
            return [];

        List<WatchedEventSnapshot> rows = [];
        using var command = conn.CreateCommand();
        command.CommandText =
            """
            SELECT v.root_definition_hash, v.playback_hash, v.root_script_hash, v.raw_event_key, v.root_script,
                   v.playback_json,
                   s.first_observed_at, s.last_observed_at, s.last_observed_location_name, s.last_observed_locale
            FROM observed_variants v
            JOIN variant_observation_summaries s ON s.variant_fk = v.variant_pk
            WHERE v.event_fk = $event AND s.profile_fk = $profile;
            """;
        command.Parameters.AddWithValue("$event", eventPk);
        command.Parameters.AddWithValue("$profile", profilePk.Value);
        using (SqliteDataReader reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                if (TryMaterializeSnapshot(reader, 0, identity.AssetName, identity.EventId, out WatchedEventSnapshot? snapshot) && snapshot is not null)
                    rows.Add(snapshot);
            }
        }

        List<WatchedEventSnapshot> collapsed = rows
            .GroupBy(snapshot => snapshot.Fingerprint, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(snapshot => snapshot.LastWatchedAt).First())
            .OrderByDescending(snapshot => snapshot.LastWatchedAt)
            .ToList();
        return collapsed;
    }

    private bool TryMaterializeSnapshot(SqliteDataReader reader, int offset, string assetName, string eventId,
        out WatchedEventSnapshot? snapshot)
    {
        snapshot = null;
        string playbackJson = reader.GetString(offset + 5);
        PlaybackPayload? payload = null;
        try
        {
            payload = JsonSerializer.Deserialize<PlaybackPayload>(playbackJson);
        }
        catch (Exception error)
        {
            logger?.Invoke($"playback_json 反序列化失败：{error.Message}");
            return false;
        }
        if (payload is null)
            return false;

        snapshot = new WatchedEventSnapshot(
            reader.IsDBNull(offset + 8) ? "" : reader.GetString(offset + 8),
            assetName,
            eventId,
            reader.GetString(offset + 3),
            reader.GetString(offset + 4),
            DeepCopyAssets(payload.EventAssets),
            DeepCopyStrings(payload.Translations),
            reader.IsDBNull(offset + 9) ? "" : reader.GetString(offset + 9),
            reader.GetString(offset + 1),
            DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(offset + 6)),
            DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(offset + 7)));
        return true;
    }

    private long ResolveEventPk(SqliteConnection conn, EventIdentity identity)
    {
        using var select = conn.CreateCommand();
        select.CommandText = "SELECT event_pk FROM events WHERE asset_name = $asset AND event_id = $event;";
        select.Parameters.AddWithValue("$asset", identity.AssetName);
        select.Parameters.AddWithValue("$event", identity.EventId);
        object? result = select.ExecuteScalar();
        return result is null ? -1 : Convert.ToInt64(result);
    }

    private long? ResolveProfilePk(SqliteConnection conn)
    {
        using var select = conn.CreateCommand();
        select.CommandText = "SELECT profile_pk FROM save_profiles WHERE farm_unique_id = $farm AND player_unique_id = $player;";
        select.Parameters.AddWithValue("$farm", profile.StoredFarmUniqueId);
        select.Parameters.AddWithValue("$player", profile.PlayerUniqueId);
        object? result = select.ExecuteScalar();
        return result is null ? null : Convert.ToInt64(result);
    }

    private static Dictionary<string, Dictionary<string, string>> ToMutableAssets(
        IReadOnlyDictionary<string, Dictionary<string, string>> assets)
    {
        Dictionary<string, Dictionary<string, string>> result = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string asset, Dictionary<string, string> entries) in assets)
        {
            Dictionary<string, string> copy = new(StringComparer.Ordinal);
            foreach ((string key, string value) in entries)
                copy[key] = value;
            result[asset] = copy;
        }
        return result;
    }

    private static Dictionary<string, string> ToMutableStrings(IReadOnlyDictionary<string, string> source)
    {
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        foreach ((string key, string value) in source)
            result[key] = value;
        return result;
    }

    private static Dictionary<string, Dictionary<string, string>> DeepCopyAssets(
        Dictionary<string, Dictionary<string, string>> assets)
    {
        Dictionary<string, Dictionary<string, string>> result = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string asset, Dictionary<string, string> entries) in assets)
        {
            Dictionary<string, string> copy = new(StringComparer.Ordinal);
            foreach ((string key, string value) in entries)
                copy[key] = value;
            result[asset] = copy;
        }
        return result;
    }

    private static Dictionary<string, string> DeepCopyStrings(Dictionary<string, string> source)
    {
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        foreach ((string key, string value) in source)
            result[key] = value;
        return result;
    }
}
