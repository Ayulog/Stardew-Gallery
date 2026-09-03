namespace StardewGallery;

internal sealed record PlaybackPayload(
    Dictionary<string, Dictionary<string, string>> EventAssets,
    Dictionary<string, string> Translations
);
