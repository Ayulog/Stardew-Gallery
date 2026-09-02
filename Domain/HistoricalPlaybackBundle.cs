using System.Text.Json.Serialization;

namespace StardewGallery;

internal sealed record WatchedEventSnapshot(
    string LocationName,
    string AssetName,
    string EventId,
    string EventKey,
    string RootScript,
    Dictionary<string, Dictionary<string, string>> EventAssets,
    Dictionary<string, string> Translations,
    string Locale,
    string Fingerprint,
    DateTimeOffset FirstWatchedAt,
    DateTimeOffset LastWatchedAt
)
{
    [JsonIgnore]
    internal EventIdentity Identity => new(AssetName, EventId);

    [JsonIgnore]
    internal HistoricalPlaybackBundle Playback => HistoricalPlaybackBundle.From(this);
}

internal sealed record HistoricalPlaybackBundle(
    string RootScript,
    IReadOnlyDictionary<string, Dictionary<string, string>> EventAssets,
    IReadOnlyDictionary<string, string> Translations,
    string Locale,
    string PlaybackHash
)
{
    internal static HistoricalPlaybackBundle From(WatchedEventSnapshot snapshot) => new(
        snapshot.RootScript,
        snapshot.EventAssets,
        snapshot.Translations,
        snapshot.Locale,
        snapshot.Fingerprint);
}
