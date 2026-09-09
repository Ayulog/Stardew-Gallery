namespace StardewGallery;

internal sealed record EventSourceRecord(string Asset, string EventId, string? OwnerUniqueID, string OwnerName, string? Version = null);

internal sealed class EventSourceCatalog
{
    private readonly Dictionary<EventIdentity, string> sources = [];
    private readonly Dictionary<string, string> labels = new(StringComparer.Ordinal);
    internal EventSourceCatalog(IEnumerable<EventSourceRecord> records, Func<string, string?> installedVersion)
    {
        foreach (var group in records.GroupBy(record => new EventIdentity(record.Asset, record.EventId)))
        {
            var matches = group.Where(record => record.OwnerUniqueID is null || installedVersion(record.OwnerUniqueID) == record.Version).ToArray();
            if (matches.Length != 1) continue;
            var record = matches[0];
            string source = record.OwnerUniqueID ?? "base";
            sources[group.Key] = source; labels[source] = record.OwnerName;
        }
    }
    internal string Get(EventIdentity identity) => sources.GetValueOrDefault(identity) ?? "unknown";
    internal string Name(string source) => labels.GetValueOrDefault(source) ?? source;
}
