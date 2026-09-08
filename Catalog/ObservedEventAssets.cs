namespace StardewGallery;

internal sealed class ObservedEventAssets
{
    private readonly HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
    internal IReadOnlyList<string> Names => names.OrderBy(name => name, StringComparer.Ordinal).ToArray();
    internal bool Observe(string name)
    {
        string? normalized = Normalize(name);
        return normalized is not null && names.Add(normalized);
    }
    internal void Clear() => names.Clear();
    internal static string? Normalize(string name)
    {
        string normalized = name.Replace('\\', '/');
        return normalized.StartsWith("Data/Events/", StringComparison.OrdinalIgnoreCase)
            && normalized.Split('/').All(part => part.Length > 0 && part != "." && part != ".." && !part.Contains(':'))
            ? normalized : null;
    }
}
