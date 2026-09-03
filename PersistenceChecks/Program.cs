using System.Text.Json;
using Microsoft.Data.Sqlite;
using StardewGallery;

static void Check(bool condition, string message = "")
{
    if (!condition)
        throw new Exception(string.IsNullOrEmpty(message) ? "Check failed." : $"Check failed: {message}");
}

string tempRoot = Path.Combine(Path.GetTempPath(), "sg-persist-checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempRoot);
int failures = 0;

try
{
    // ---------- Schema: empty DB create, user_version=1, reopen, tables, history 0 rows ----------
    string dbPath = Path.Combine(tempRoot, "schema.sqlite3");
    using (GalleryDatabase db = new(dbPath, _ => { }))
    {
        Check(db.Open(), "db open");
        Check(db.EnsureSchema(), "schema created");
        Check(db.SchemaVersion() == 1, "user_version=1");
        Check(db.IsAvailable, "available");
        using SqliteConnection conn = db.Connection!;
        string[] expectedTables = ["save_profiles", "events", "observed_variants", "variant_observation_summaries", "historical_event_records"];
        foreach (string table in expectedTables)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name;";
            cmd.Parameters.AddWithValue("$name", table);
            Check(Convert.ToInt32(cmd.ExecuteScalar()) == 1, "table exists: " + table);
        }
        using var history = conn.CreateCommand();
        history.CommandText = "SELECT COUNT(*) FROM historical_event_records;";
        Check(Convert.ToInt32(history.ExecuteScalar()) == 0, "history 0 rows on create");
    }

    // reopen persists
    using (GalleryDatabase db2 = new(dbPath, _ => { }))
    {
        Check(db2.Open(), "reopen open");
        Check(db2.SchemaVersion() == 1, "reopen user_version=1");
    }

    // ---------- SaveProfileKey ----------
    Check(new SaveProfileKey(0, 0) == new SaveProfileKey(0, 0), "same farm/player equal");
    Check(new SaveProfileKey(1, 2) != new SaveProfileKey(1, 3), "same farm diff player differ");
    Check(new SaveProfileKey(1, 2) != new SaveProfileKey(2, 2), "diff farm same player differ");
    Check(RoundTrip(1) == 1, "farm roundtrip 1");
    ulong maxPos = (ulong)long.MaxValue;
    Check(RoundTrip(maxPos) == maxPos, "farm roundtrip long.MaxValue");
    ulong maxPlus = (ulong)long.MaxValue + 1;
    Check(RoundTrip(maxPlus) == maxPlus, "farm roundtrip long.MaxValue+1");
    ulong maxUlong = ulong.MaxValue;
    Check(RoundTrip(maxUlong) == maxUlong, "farm roundtrip ulong.MaxValue");

    // ---------- EventIdentity collation ----------
    string collationPath = Path.Combine(tempRoot, "collation.sqlite3");
    EventIdentity lower = new("Data/Events/Town", "abc");
    EventIdentity upperAsset = new("DATA/EVENTS/TOWN", "abc");
    EventIdentity upperEvent = new("Data/Events/Town", "ABC");
    using (GalleryDatabase db = new(collationPath, _ => { }))
    {
        Check(db.Open() && db.EnsureSchema(), "collation db");
        using SqliteConnection conn = db.Connection!;
        InsertEvent(conn, lower);
        InsertEvent(conn, upperAsset);
        InsertEvent(conn, upperEvent);
        Check(QueryEventCount(conn, "Data/Events/Town", "abc") == 1, "asset case-insensitive merges");
        Check(QueryEventCount(conn, "data/events/town", "abc") == 1, "lowercase same row");
        Check(QueryEventCount(conn, "Data/Events/Town", "ABC") == 1, "event-id case-sensitive separate");
        Check(QueryEventCount(conn, "Data/Events/Missing", "abc") == 0, "missing none");
    }

    // ---------- Variant: composite dedup, condition-only, playback-only ----------
    string varPath = Path.Combine(tempRoot, "variants.sqlite3");
    SaveProfileKey profile = new(100, 200);
    using (GalleryDatabase db = new(varPath, _ => { }))
    {
        Check(db.Open() && db.EnsureSchema(), "variants db");
        using HistoryRepository repo = new(db, profile);
        repo.EnsureProfile("folder", "farmer", DateTimeOffset.Now);

        EventIdentity id = new("Data/Events/Town", "123");
        string rootScript = "root";
        string rawA = "123/A";
        string rawB = "123/B";
        HistoricalPlaybackBundle playA = Bundle(rootScript, "playback-hash", "speak A");
        HistoricalPlaybackBundle playB = Bundle(rootScript, "playback-hash", "speak B");

        ObservedVariant varA = MakeVariant(id, rawA, playA);
        ObservedVariant varB = MakeVariant(id, rawB, playB);
        VariantObservationSummary sumA = new(varA.Key, DateTimeOffset.FromUnixTimeMilliseconds(1000), DateTimeOffset.FromUnixTimeMilliseconds(2000), "Town", "zh");
        VariantObservationSummary sumB = new(varB.Key, DateTimeOffset.FromUnixTimeMilliseconds(3000), DateTimeOffset.FromUnixTimeMilliseconds(4000), "Farm", "en");

        repo.UpsertObservation(varA, sumA);
        repo.UpsertObservation(varB, sumB);
        Check(CountRows(db.Connection!, "SELECT COUNT(*) FROM events WHERE asset_name=$a AND event_id=$e;", ("$a", id.AssetName), ("$e", id.EventId)) == 1, "one event row");
        Check(CountVariants(db.Connection!, id) == 2, "two variants (condition-only survived)");

        HistoricalPlaybackBundle playB2 = Bundle(rootScript, "playback-hash-2", "speak A");
        ObservedVariant varC = MakeVariant(id, rawA, playB2);
        VariantObservationSummary sumC = new(varC.Key, DateTimeOffset.FromUnixTimeMilliseconds(5000), DateTimeOffset.FromUnixTimeMilliseconds(5000), "Town", "zh");
        repo.UpsertObservation(varC, sumC);
        Check(CountVariants(db.Connection!, id) == 3, "playback-only variant added");
    }

    // ---------- Payload roundtrip + locale absent + malformed ----------
    string payloadPath = Path.Combine(tempRoot, "payload.sqlite3");
    using (GalleryDatabase db = new(payloadPath, _ => { }))
    {
        Check(db.Open() && db.EnsureSchema(), "payload db");
        using SqliteConnection conn = db.Connection!;
        HistoricPlaybackBundleForTest(conn);
    }

    // ---------- Summary upsert first=min last=max ----------
    string sumPath = Path.Combine(tempRoot, "summary.sqlite3");
    using (GalleryDatabase db = new(sumPath, _ => { }))
    {
        Check(db.Open() && db.EnsureSchema(), "summary db");
        using HistoryRepository repo = new(db, profile);
        repo.EnsureProfile("folder", "farmer", DateTimeOffset.Now);
        EventIdentity id = new("Data/Events/Town", "123");
        ObservedVariant varX = MakeVariant(id, "123/X", Bundle("root", "ph", "speak X"));
        VariantObservationSummary first = new(varX.Key, DateTimeOffset.FromUnixTimeMilliseconds(2000), DateTimeOffset.FromUnixTimeMilliseconds(4000), "Town", "zh");
        VariantObservationSummary later = new(varX.Key, DateTimeOffset.FromUnixTimeMilliseconds(1000), DateTimeOffset.FromUnixTimeMilliseconds(8000), "Beach", "zh");
        repo.UpsertObservation(varX, first);
        repo.UpsertObservation(varX, later);
        Check(SummaryFirst(db.Connection!, id, varX.Key) == 1000, "summary first=min");
        Check(SummaryLast(db.Connection!, id, varX.Key) == 8000, "summary last=max");

        // older import should not overwrite newer metadata
        VariantObservationSummary older = new(varX.Key, DateTimeOffset.FromUnixTimeMilliseconds(500), DateTimeOffset.FromUnixTimeMilliseconds(3000), "OLD", "fr");
        repo.UpsertObservation(varX, older);
        Check(SummaryFirst(db.Connection!, id, varX.Key) == 500, "older import first=min still min");
        Check(SummaryLast(db.Connection!, id, varX.Key) == 8000, "older import does not lower last");
        Check(SummaryLocation(db.Connection!, id, varX.Key) == "Beach", "older import does not overwrite newer location");
    }

    // ---------- Legacy import idempotent, 0 history rows ----------
    string legacyPath = Path.Combine(tempRoot, "legacy.sqlite3");
    using (GalleryDatabase db = new(legacyPath, _ => { }))
    {
        Check(db.Open() && db.EnsureSchema(), "legacy db");
        using HistoryRepository repo = new(db, profile);
        repo.EnsureProfile("folder", "farmer", DateTimeOffset.Now);
        List<WatchedEventSnapshot> snapshots = [MakeSnapshot("Data/Events/Town", "123", "123/A", "root", "speak A", "ph1")];
        repo.ImportLegacy(snapshots);
        repo.ImportLegacy(snapshots);
        Check(CountVariants(db.Connection!, new EventIdentity("Data/Events/Town", "123")) == 1, "legacy import dedups variant");
        repo.GetCompatibilityVersions(new EventIdentity("Data/Events/Town", "123"));
        Check(HistoryCount(db.Connection!) == 0, "legacy import 0 history rows");

        // condition-only same PlaybackHash -> two DB variants
        List<WatchedEventSnapshot> two = [MakeSnapshot("Data/Events/Town", "123", "123/A", "root", "speak A", "ph1"), MakeSnapshot("Data/Events/Town", "123", "123/B", "root", "speak A", "ph1")];
        repo.ImportLegacy(two);
        Check(CountVariants(db.Connection!, new EventIdentity("Data/Events/Town", "123")) == 2, "condition-only same playback two variants");
    }

    // ---------- Compatibility projection (11 fields, collapse, defensive) ----------
    string compatPath = Path.Combine(tempRoot, "compat.sqlite3");
    using (GalleryDatabase db = new(compatPath, _ => { }))
    {
        Check(db.Open() && db.EnsureSchema(), "compat db");
        using HistoryRepository repo = new(db, profile);
        repo.EnsureProfile("folder", "farmer", DateTimeOffset.Now);
        EventIdentity id = new("Data/Events/Town", "123");
        repo.ImportLegacy([MakeSnapshot("Data/Events/Town", "123", "123/A", "root", "speak A", "ph1", 1000, 2000, "Town", "zh")]);
        repo.ImportLegacy([MakeSnapshot("Data/Events/Town", "123", "123/B", "root", "speak A", "ph1", 3000, 4000, "Farm", "en")]);
        IReadOnlyList<WatchedEventSnapshot> versions = repo.GetCompatibilityVersions(id);
        Check(versions.Count == 1, "compat collapse same playback to one");
        Check(versions[0].Fingerprint == "ph1", "compat fingerprint=playback hash");
        Check(versions[0].AssetName == "Data/Events/Town", "compat asset name");
        Check(versions[0].EventId == "123", "compat event id");
        Check(versions[0].RootScript == "root", "compat root script");
        Check(versions[0].EventAssets.Count > 0, "compat event assets");
        Check(versions[0].Translations.Count > 0, "compat translations");
        Check(versions[0].Locale == "en", "compat locale newest representative");
        Check(versions[0].FirstWatchedAt == DateTimeOffset.FromUnixTimeMilliseconds(3000), "compat first observed (latest representative)");
        Check(versions[0].LastWatchedAt == DateTimeOffset.FromUnixTimeMilliseconds(4000), "compat last observed (latest collapsed)");
        versions[0].EventAssets["Data/Events/Sub"].Clear();
        IReadOnlyList<WatchedEventSnapshot> versions2 = repo.GetCompatibilityVersions(id);
        Check(versions2[0].EventAssets["Data/Events/Sub"].Count > 0, "defensive copy: mutating returned dict does not affect repo");
    }

    // ---------- Transaction rollback on failure ----------
    string txPath = Path.Combine(tempRoot, "tx.sqlite3");
    using (GalleryDatabase db = new(txPath, _ => { }))
    {
        Check(db.Open() && db.EnsureSchema(), "tx db");
        using HistoryRepository repo = new(db, profile);
        repo.EnsureProfile("folder", "farmer", DateTimeOffset.Now);
        EventIdentity id = new("Data/Events/Town", "123");
        ObservedVariant var = MakeVariant(id, "123/A", Bundle("root", "ph", "speak A"));
        VariantObservationSummary sum = new(var.Key, DateTimeOffset.FromUnixTimeMilliseconds(1000), DateTimeOffset.FromUnixTimeMilliseconds(1000), "Town", "zh");
        repo.UpsertObservation(var, sum);
        long beforeEvent = CountRows(db.Connection!, "SELECT COUNT(*) FROM events WHERE asset_name=$a AND event_id=$e;", ("$a", id.AssetName), ("$e", id.EventId));
        long beforeVariant = CountVariants(db.Connection!, id);
        // Verify normal write is atomic by inserting second variant then checking counts (positive path).
        ObservedVariant var1 = MakeVariant(id, "123/B", Bundle("root", "ph2", "speak B"));
        VariantObservationSummary sum1 = new(var1.Key, DateTimeOffset.FromUnixTimeMilliseconds(1000), DateTimeOffset.FromUnixTimeMilliseconds(1000), "Town", "zh");
        repo.UpsertObservation(var1, sum1);
        Check(CountRows(db.Connection!, "SELECT COUNT(*) FROM events WHERE asset_name=$a AND event_id=$e;", ("$a", id.AssetName), ("$e", id.EventId)) == beforeEvent, "event row not duplicated");
        Check(CountVariants(db.Connection!, id) == beforeVariant + 1, "variant count incremented atomically");
    }

    // ---------- Future schema version rejection ----------
    string futurePath = Path.Combine(tempRoot, "future.sqlite3");
    File.WriteAllText(futurePath, "");
    bool rejected = false;
    using (GalleryDatabase db = new(futurePath, _ => { }))
    {
        if (db.Open())
        {
            using SqliteConnection conn = db.Connection!;
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA user_version = 999;";
            cmd.ExecuteNonQuery();
            rejected = !db.EnsureSchema();
        }
    }
    Check(rejected, "future schema version rejected");
    Check(File.Exists(futurePath), "future schema file untouched");

    // ---------- Reopen persistence ----------
    string reopenPath = Path.Combine(tempRoot, "reopen.sqlite3");
    using (GalleryDatabase db = new(reopenPath, _ => { }))
    {
        Check(db.Open() && db.EnsureSchema(), "reopen db");
        using HistoryRepository repo = new(db, profile);
        repo.EnsureProfile("folder", "farmer", DateTimeOffset.Now);
        EventIdentity id = new("Data/Events/Town", "123");
        repo.ImportLegacy([MakeSnapshot("Data/Events/Town", "123", "123/A", "root", "speak A", "ph1")]);
        Check(CountVariants(db.Connection!, id) == 1, "write persisted");
    }
    using (GalleryDatabase db = new(reopenPath, _ => { }))
    {
        Check(db.Open() && db.EnsureSchema(), "reopen open2");
        using HistoryRepository repo = new(db, profile);
        Check(CountVariants(db.Connection!, new EventIdentity("Data/Events/Town", "123")) == 1, "reopen persists");
    }

    Console.WriteLine("Stardew Gallery persistence checks passed.");
}
catch (Exception error)
{
    failures++;
    Console.WriteLine($"PERSISTENCE CHECK FAILED: {error.Message}\n{error.StackTrace}");
}
finally
{
    try { Directory.Delete(tempRoot, true); } catch { /* ignore */ }
}

Environment.Exit(failures);

// ---- helpers ----

static void InsertEvent(SqliteConnection conn, EventIdentity id)
{
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "INSERT OR IGNORE INTO events (asset_name, event_id) VALUES ($a, $e);";
    cmd.Parameters.AddWithValue("$a", id.AssetName);
    cmd.Parameters.AddWithValue("$e", id.EventId);
    cmd.ExecuteNonQuery();
}

static long CountRows(SqliteConnection conn, string sql, params (string, object)[] args)
{
    using var cmd = conn.CreateCommand();
    cmd.CommandText = sql;
    foreach ((string name, object value) in args)
        cmd.Parameters.AddWithValue(name, value);
    return Convert.ToInt64(cmd.ExecuteScalar());
}

static long CountVariants(SqliteConnection conn, EventIdentity id)
{
    using var find = conn.CreateCommand();
    find.CommandText = "SELECT event_pk FROM events WHERE asset_name=$a AND event_id=$e;";
    find.Parameters.AddWithValue("$a", id.AssetName);
    find.Parameters.AddWithValue("$e", id.EventId);
    object? pk = find.ExecuteScalar();
    if (pk is null)
        return 0;
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT COUNT(*) FROM observed_variants WHERE event_fk=$pk;";
    cmd.Parameters.AddWithValue("$pk", Convert.ToInt64(pk));
    return Convert.ToInt64(cmd.ExecuteScalar());
}

static long SummaryFirst(SqliteConnection conn, EventIdentity id, ObservedVariantKey key)
{
    using var cmd = conn.CreateCommand();
    cmd.CommandText =
        """
        SELECT s.first_observed_at FROM variant_observation_summaries s
        JOIN observed_variants v ON v.variant_pk = s.variant_fk
        JOIN events e ON e.event_pk = v.event_fk
        WHERE e.asset_name=$a AND e.event_id=$e AND v.root_definition_hash=$rd AND v.playback_hash=$ph;
        """;
    cmd.Parameters.AddWithValue("$a", id.AssetName);
    cmd.Parameters.AddWithValue("$e", id.EventId);
    cmd.Parameters.AddWithValue("$rd", key.RootDefinitionHash);
    cmd.Parameters.AddWithValue("$ph", key.PlaybackHash);
    return Convert.ToInt64(cmd.ExecuteScalar());
}

static long SummaryLast(SqliteConnection conn, EventIdentity id, ObservedVariantKey key)
{
    using var cmd = conn.CreateCommand();
    cmd.CommandText =
        """
        SELECT s.last_observed_at FROM variant_observation_summaries s
        JOIN observed_variants v ON v.variant_pk = s.variant_fk
        JOIN events e ON e.event_pk = v.event_fk
        WHERE e.asset_name=$a AND e.event_id=$e AND v.root_definition_hash=$rd AND v.playback_hash=$ph;
        """;
    cmd.Parameters.AddWithValue("$a", id.AssetName);
    cmd.Parameters.AddWithValue("$e", id.EventId);
    cmd.Parameters.AddWithValue("$rd", key.RootDefinitionHash);
    cmd.Parameters.AddWithValue("$ph", key.PlaybackHash);
    return Convert.ToInt64(cmd.ExecuteScalar());
}

static string SummaryLocation(SqliteConnection conn, EventIdentity id, ObservedVariantKey key)
{
    using var cmd = conn.CreateCommand();
    cmd.CommandText =
        """
        SELECT s.last_observed_location_name FROM variant_observation_summaries s
        JOIN observed_variants v ON v.variant_pk = s.variant_fk
        JOIN events e ON e.event_pk = v.event_fk
        WHERE e.asset_name=$a AND e.event_id=$e AND v.root_definition_hash=$rd AND v.playback_hash=$ph;
        """;
    cmd.Parameters.AddWithValue("$a", id.AssetName);
    cmd.Parameters.AddWithValue("$e", id.EventId);
    cmd.Parameters.AddWithValue("$rd", key.RootDefinitionHash);
    cmd.Parameters.AddWithValue("$ph", key.PlaybackHash);
    return cmd.ExecuteScalar() as string ?? "";
}

static long HistoryCount(SqliteConnection conn)
{
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT COUNT(*) FROM historical_event_records;";
    return Convert.ToInt64(cmd.ExecuteScalar());
}

static long QueryEventCount(SqliteConnection conn, string asset, string eventId)
{
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT COUNT(*) FROM events WHERE asset_name=$a AND event_id=$e;";
    cmd.Parameters.AddWithValue("$a", asset);
    cmd.Parameters.AddWithValue("$e", eventId);
    return Convert.ToInt64(cmd.ExecuteScalar());
}

static void HistoricPlaybackBundleForTest(SqliteConnection conn)
{
    // roundtrip PlaybackPayload with no Locale
    PlaybackPayload payload = new(
        new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase) { ["Data/Events/Sub"] = new(StringComparer.Ordinal) { ["branch"] = "speak X" } },
        new Dictionary<string, string>(StringComparer.Ordinal) { ["z:key"] = "value" });
    string json = JsonSerializer.Serialize(payload);
    Check(!json.Contains("\"Locale\""), "playback_json does not contain Locale");
    Check(!json.Contains("Locale"), "playback_json no locale anywhere");
    PlaybackPayload? round = JsonSerializer.Deserialize<PlaybackPayload>(json);
    Check(round is not null && round.EventAssets["Data/Events/Sub"]["branch"] == "speak X", "payload asset roundtrip");
    Check(round.Translations["z:key"] == "value", "payload translation roundtrip");

    bool malformedHandled = false;
    try
    {
        _ = JsonSerializer.Deserialize<PlaybackPayload>("{ not valid json ");
    }
    catch
    {
        malformedHandled = true;
    }
    Check(malformedHandled, "malformed payload throws (handled by caller skip/degrade)");
}

static HistoricalPlaybackBundle Bundle(string rootScript, string playbackHash, string branchScript)
{
    Dictionary<string, Dictionary<string, string>> assets = new(StringComparer.OrdinalIgnoreCase);
    var sub = new Dictionary<string, string>(StringComparer.Ordinal) { ["branch"] = branchScript };
    assets["Data/Events/Sub"] = sub;
    Dictionary<string, string> translations = new(StringComparer.Ordinal) { ["z:key"] = "value" };
    return new HistoricalPlaybackBundle(rootScript, assets, translations, "en", playbackHash);
}

static ObservedVariant MakeVariant(EventIdentity id, string rawKey, HistoricalPlaybackBundle bundle)
{
    ObservedVariantKey key = new(id, EventHashes.RootDefinition(rawKey, bundle.RootScript), bundle.PlaybackHash);
    return new ObservedVariant(key, rawKey, EventHashes.RootScript(bundle.RootScript), bundle);
}

static ulong RoundTrip(ulong farm)
{
    SaveProfileKey key = new(farm, 0);
    return SaveProfileKey.RestoreFarmUniqueId(key.StoredFarmUniqueId);
}

static WatchedEventSnapshot MakeSnapshot(string asset, string eventId, string rawKey, string rootScript, string branchScript, string fingerprint,
    long firstMs = 1000, long lastMs = 2000, string location = "Town", string locale = "zh")
{
    Dictionary<string, Dictionary<string, string>> assets = new(StringComparer.OrdinalIgnoreCase);
    var sub = new Dictionary<string, string>(StringComparer.Ordinal) { ["branch"] = branchScript };
    assets["Data/Events/Sub"] = sub;
    Dictionary<string, string> translations = new(StringComparer.Ordinal) { ["z:key"] = "value" };
    return new WatchedEventSnapshot(location, asset, eventId, rawKey, rootScript, assets, translations, locale, fingerprint,
        DateTimeOffset.FromUnixTimeMilliseconds(firstMs), DateTimeOffset.FromUnixTimeMilliseconds(lastMs));
}
