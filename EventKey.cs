namespace StardewGallery;

internal static class EventKey
{
    private const string PlaceholderMessage = "You open up the XNB file hoping to find a secret, only to see this sentence. You are now disappointed.";

    /// <summary>
    /// Read an event ID the way GameLocation.checkEventPrecondition does (1.6.15): the first entry of
    /// Event.SplitPreconditions, which splits on '/' outside quotes, drops empty entries, and keeps quotes,
    /// escapes and surrounding spaces. "123 /f ..." therefore has the ID "123 ", and "/123/..." the ID "123".
    /// </summary>
    internal static bool TryGetId(string key, out string id)
    {
        id = FirstEntry(key ?? string.Empty);
        // "-1" never plays; a blank ID can't be referenced by another event's space-separated conditions.
        return !string.IsNullOrWhiteSpace(id) && id != "-1";
    }

    internal static bool IsPlaceholderScript(string script)
        => script.Contains(PlaceholderMessage, StringComparison.Ordinal);

    private static string FirstEntry(string key)
    {
        bool quoted = false;
        int start = 0;
        for (int index = 0; index < key.Length; index++)
        {
            if (key[index] == '"' && (index == 0 || key[index - 1] != '\\'))
                quoted = !quoted;
            else if (key[index] == '/' && !quoted)
            {
                if (index > start)
                    return key[start..index];
                start = index + 1;
            }
        }
        return key[start..];
    }
}
