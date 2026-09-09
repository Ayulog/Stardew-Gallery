using System.Globalization;
using System.Text.Json;

namespace StardewGallery;

internal sealed class EventNameStore(string directory, Action<string> warn)
{
    private sealed record Document(int Version, Dictionary<string, string> Names);
    private Dictionary<string, string>? names;
    private bool readOnly;
    private string FilePath => Path.Combine(directory, "event-names.json");
    internal int Revision { get; private set; }
    internal string? Get(EventIdentity identity) { Load(); return names!.GetValueOrDefault(Key(identity)); }
    private static string Key(EventIdentity identity) => identity.AssetName.ToUpperInvariant() + "\u001f" + identity.EventId;
    internal static string Clean(string text)
    {
        text = GallerySearchInput.CleanText(text).Trim();
        int[] elements = StringInfo.ParseCombiningCharacters(text);
        return elements.Length <= 100 ? text : text[..elements[100]];
    }
    internal bool Set(EventIdentity identity, string text)
    {
        Load();
        if (readOnly) return false;
        var next = new Dictionary<string, string>(names!, StringComparer.Ordinal);
        text = Clean(text);
        if (text.Length == 0) next.Remove(Key(identity)); else next[Key(identity)] = text;
        try
        {
            Directory.CreateDirectory(directory);
            string pending = Path.Combine(directory, "event-names-" + Guid.NewGuid().ToString("N") + ".pending");
            File.WriteAllText(pending, JsonSerializer.Serialize(new Document(1, next)));
            if (File.Exists(FilePath))
            {
                string archive = Path.Combine(directory, "archive");
                Directory.CreateDirectory(archive);
                File.Replace(pending, FilePath, Path.Combine(archive, "event-names-" + Guid.NewGuid().ToString("N") + ".json"));
            }
            else File.Move(pending, FilePath);
            names = next; Revision++; return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { warn("Event names could not be saved: " + error.Message); return false; }
    }
    private void Load()
    {
        if (names is not null) return;
        names = new(StringComparer.Ordinal);
        if (!File.Exists(FilePath)) return;
        try
        {
            Document doc = JsonSerializer.Deserialize<Document>(File.ReadAllText(FilePath)) ?? throw new InvalidDataException("Empty names document.");
            if (doc.Version != 1 || doc.Names is null) throw new InvalidDataException("Unsupported names document.");
            names = new(doc.Names, StringComparer.Ordinal);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        { readOnly = true; warn("Event names are read-only: " + error.Message); }
    }
}
