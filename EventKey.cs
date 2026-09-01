namespace StardewGallery;

internal static class EventKey
{
    private const string PlaceholderMessage = "You open up the XNB file hoping to find a secret, only to see this sentence. You are now disappointed.";

    internal static bool TryGetId(string key, out string id)
    {
        id = key.Split('/', 2)[0].Trim();
        return id.Length > 0;
    }

    internal static string GetIdentity(string locationName, string eventId)
        => $"{locationName}\u001f{eventId}";

    internal static int SelectVariantIndex(int count, Func<int, bool> matches)
    {
        for (int index = 0; index < count; index++)
        {
            if (matches(index))
                return index;
        }
        return 0;
    }

    internal static bool IsPlaceholderScript(string script)
        => script.Contains(PlaceholderMessage, StringComparison.Ordinal);

    internal static string GetScriptFingerprint(string script)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(script)))[..12];

    internal static string GetSnapshotFingerprint(string rootScript, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> eventAssets,
        IReadOnlyDictionary<string, string> translations)
    {
        System.Text.StringBuilder text = new(rootScript);
        foreach ((string asset, IReadOnlyDictionary<string, string> entries) in eventAssets.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            text.Append('\0').Append(asset);
            foreach ((string key, string script) in entries.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                text.Append('\0').Append(key).Append('\0').Append(script);
        }
        foreach ((string key, string value) in translations.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            text.Append('\0').Append(key).Append('\0').Append(value);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text.ToString())));
    }
}
