namespace StardewGallery;

internal static class GallerySchema
{
    internal const int CurrentVersion = 2;

    internal const string CreateCommandText =
        """
        CREATE TABLE save_profiles (
            profile_pk       INTEGER PRIMARY KEY,
            farm_unique_id   INTEGER NOT NULL,
            player_unique_id INTEGER NOT NULL,
            save_folder_name TEXT,
            farmer_name      TEXT,
            created_at       INTEGER NOT NULL,
            last_seen_at     INTEGER NOT NULL,
            UNIQUE(farm_unique_id, player_unique_id)
        );

        CREATE TABLE events (
            event_pk   INTEGER PRIMARY KEY,
            asset_name TEXT NOT NULL COLLATE ORDINAL_NOCASE,
            event_id   TEXT NOT NULL COLLATE BINARY,
            UNIQUE(asset_name, event_id)
        );

        CREATE TABLE observed_variants (
            variant_pk           INTEGER PRIMARY KEY,
            event_fk             INTEGER NOT NULL REFERENCES events(event_pk) ON DELETE CASCADE,
            root_definition_hash TEXT NOT NULL COLLATE BINARY,
            playback_hash        TEXT NOT NULL COLLATE BINARY,
            root_script_hash     TEXT NOT NULL COLLATE BINARY,
            raw_event_key        TEXT NOT NULL,
            root_script          TEXT NOT NULL,
            playback_json        TEXT NOT NULL,
            UNIQUE(event_fk, root_definition_hash, playback_hash)
        );

        CREATE TABLE variant_observation_summaries (
            summary_pk                  INTEGER PRIMARY KEY,
            profile_fk                  INTEGER NOT NULL REFERENCES save_profiles(profile_pk) ON DELETE CASCADE,
            variant_fk                  INTEGER NOT NULL REFERENCES observed_variants(variant_pk) ON DELETE CASCADE,
            first_observed_at           INTEGER NOT NULL,
            last_observed_at            INTEGER NOT NULL,
            last_observed_location_name TEXT,
            last_observed_locale        TEXT,
            UNIQUE(profile_fk, variant_fk)
        );

        CREATE TABLE historical_event_records (
            record_pk     INTEGER PRIMARY KEY,
            profile_fk    INTEGER NOT NULL REFERENCES save_profiles(profile_pk) ON DELETE CASCADE,
            variant_fk    INTEGER NOT NULL REFERENCES observed_variants(variant_pk) ON DELETE CASCADE,
            watched_at    INTEGER NOT NULL,
            location_name TEXT,
            locale        TEXT
        );

        CREATE TABLE historical_execution_contexts (
            context_pk       INTEGER PRIMARY KEY,
            record_fk        INTEGER NOT NULL UNIQUE REFERENCES historical_event_records(record_pk) ON DELETE CASCADE,
            schema_version   INTEGER NOT NULL,
            completion_status TEXT NOT NULL,
            execution_json   TEXT NOT NULL
        );

        CREATE INDEX idx_observed_variants_event
        ON observed_variants(event_fk);

        CREATE INDEX idx_variant_summaries_profile_last
        ON variant_observation_summaries(profile_fk, last_observed_at DESC);

        CREATE INDEX idx_history_profile_watched
        ON historical_event_records(profile_fk, watched_at DESC);

        CREATE INDEX idx_history_variant
        ON historical_event_records(variant_fk);

        PRAGMA user_version = 2;
        """;

    internal const string MigrateVersion1To2CommandText =
        """
        CREATE TABLE historical_execution_contexts (
            context_pk       INTEGER PRIMARY KEY,
            record_fk        INTEGER NOT NULL UNIQUE REFERENCES historical_event_records(record_pk) ON DELETE CASCADE,
            schema_version   INTEGER NOT NULL,
            completion_status TEXT NOT NULL,
            execution_json   TEXT NOT NULL
        );

        PRAGMA user_version = 2;
        """;
}
