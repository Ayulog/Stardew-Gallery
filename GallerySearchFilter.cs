using System.Globalization;

namespace StardewGallery;

internal sealed class GallerySearchFilter
{
    private GalleryCatalog? lastCatalog;
    private string? lastText;
    private string? lastLocale;
    private CompareInfo? lastSearchComparison;
    private bool lastChineseSort;
    private PinyinTable? lastPinyin;

    internal List<GalleryCharacter> Results { get; private set; } = [];

    // Catalogs are snapshots: a new instance must invalidate results even with the same query.
    // With a pinyin table, Latin letters also find Chinese display names by their pinyin.
    internal bool Update(GalleryCatalog catalog, string text, string locale, bool chineseSort, PinyinTable? pinyin = null)
    {
        CompareInfo searchComparison = CultureInfo.CurrentCulture.CompareInfo;
        if (ReferenceEquals(lastCatalog, catalog)
            && lastText == text
            && lastLocale == locale
            && Equals(lastSearchComparison, searchComparison)
            && lastChineseSort == chineseSort
            && ReferenceEquals(lastPinyin, pinyin))
            return false;

        List<GalleryCharacter> next = Filter(catalog, text, chineseSort, pinyin);
        bool changed = !Results.Select(character => character.Name)
            .SequenceEqual(next.Select(character => character.Name), StringComparer.Ordinal);

        Results = next;
        lastCatalog = catalog;
        lastText = text;
        lastLocale = locale;
        lastSearchComparison = searchComparison;
        lastChineseSort = chineseSort;
        lastPinyin = pinyin;
        return changed;
    }

    // Keep LINQ closures outside Update so cache hits allocate nothing.
    private static List<GalleryCharacter> Filter(GalleryCatalog catalog, string text, bool chineseSort, PinyinTable? pinyin)
    {
        string query = text.Trim();
        string? spelling = pinyin is null ? null : PinyinTable.Spelling(query);
        CompareInfo sortComparison = CultureInfo.GetCultureInfo(chineseSort ? "zh-CN" : "en-US").CompareInfo;
        return catalog.Characters
            .Where(character => query.Length == 0
                || character.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || character.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || spelling is not null && pinyin!.Matches(character.DisplayName, spelling))
            .OrderBy(character => character.DisplayName, Comparer<string>.Create((left, right) =>
                sortComparison.Compare(left, right, CompareOptions.IgnoreCase | CompareOptions.IgnoreWidth)))
            .ToList();
    }
}
