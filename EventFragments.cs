namespace StardewGallery;

internal sealed record EventFragments(
    IReadOnlyList<string> Scripts,
    IReadOnlyList<string> MissingKeys
);

internal static class EventFragmentCollector
{
    internal static EventFragments Collect(
        string rootScript,
        string rootLocation,
        Func<string, IReadOnlyDictionary<string, string>?> loadLocationEvents,
        Func<string, string[]> parseCommands,
        Func<string, string[]> parseArguments,
        Func<string, string?> loadTranslation)
    {
        List<(string Script, string Location)> pending = [(rootScript, rootLocation)];
        List<string> scripts = [];
        List<string> missing = [];
        HashSet<string> visited = new(StringComparer.Ordinal);

        for (int index = 0; index < pending.Count; index++)
        {
            (string script, string location) = pending[index];
            scripts.Add(script);
            foreach (string command in parseCommands(script))
            {
                string[] args = parseArguments(command);
                if (args.Length > 1 && args[0].Equals("changeLocation", StringComparison.OrdinalIgnoreCase))
                    location = args[1];
                if (!TryGetReference(args, out string key, out bool translation)
                    || !visited.Add(translation ? "T:" + key : $"E:{location}:{key}"))
                    continue;

                string? next = translation ? loadTranslation(key) : loadLocationEvents(location)?.GetValueOrDefault(key);
                if (next is null)
                    missing.Add(key);
                else
                    pending.Add((next, location));
            }
        }

        return new EventFragments(scripts, missing);
    }

    private static bool TryGetReference(string[] args, out string key, out bool translation)
    {
        key = "";
        translation = false;
        if (args.Length > 1 && args[0].Equals("switchEvent", StringComparison.OrdinalIgnoreCase))
        {
            key = args[1];
            return true;
        }
        if (args.Length <= 1 || !args[0].Equals("fork", StringComparison.OrdinalIgnoreCase))
            return false;

        key = args.Length > 2 ? args[2] : args[1];
        translation = args.Length > 3 && bool.TryParse(args[3], out bool value) && value;
        return true;
    }
}
