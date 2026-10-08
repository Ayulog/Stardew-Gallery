namespace StardewGallery;

internal static class EventKey
{
    private const string PlaceholderMessage = "You open up the XNB file hoping to find a secret, only to see this sentence. You are now disappointed.";

    internal static bool TryGetId(string key, out string id)
    {
        id = key.Split('/', 2)[0].Trim();
        return id.Length > 0;
    }

    internal static bool IsPlaceholderScript(string script)
        => script.Contains(PlaceholderMessage, StringComparison.Ordinal);
}
