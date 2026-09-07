using System.Globalization;

namespace StardewGallery;

internal sealed class GallerySearchFilter
{
    private GalleryCatalog? lastCatalog;
    private string? lastText;
    private string? lastLocale;
    private CompareInfo? lastSearchComparison;
    private bool lastChineseSort;

    internal List<GalleryCharacter> Results { get; private set; } = [];

    // Catalogs are snapshots: a new instance must invalidate results even with the same query.
    internal bool Update(GalleryCatalog catalog, string text, string locale, bool chineseSort)
    {
        CompareInfo searchComparison = CultureInfo.CurrentCulture.CompareInfo;
        if (ReferenceEquals(lastCatalog, catalog)
            && lastText == text
            && lastLocale == locale
            && Equals(lastSearchComparison, searchComparison)
            && lastChineseSort == chineseSort)
            return false;

        List<GalleryCharacter> next = Filter(catalog, text, chineseSort);
        bool changed = !Results.Select(character => character.Name)
            .SequenceEqual(next.Select(character => character.Name), StringComparer.Ordinal);

        Results = next;
        lastCatalog = catalog;
        lastText = text;
        lastLocale = locale;
        lastSearchComparison = searchComparison;
        lastChineseSort = chineseSort;
        return changed;
    }

    // Keep LINQ closures outside Update so cache hits allocate nothing.
    private static List<GalleryCharacter> Filter(GalleryCatalog catalog, string text, bool chineseSort)
    {
        string query = text.Trim();
        CompareInfo sortComparison = CultureInfo.GetCultureInfo(chineseSort ? "zh-CN" : "en-US").CompareInfo;
        return catalog.Characters
            .Where(character => query.Length == 0
                || character.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || character.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || catalog.Events.Any(entry => entry.EventId.Contains(query, StringComparison.OrdinalIgnoreCase)
                    && entry.Ownership.Owners.Any(owner => owner.Name == character.Name)))
            .OrderBy(character => character.DisplayName, Comparer<string>.Create((left, right) =>
                sortComparison.Compare(left, right, CompareOptions.IgnoreCase | CompareOptions.IgnoreWidth)))
            .ToList();
    }
}
