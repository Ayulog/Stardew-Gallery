using StardewModdingAPI;
using StardewValley;

namespace StardewGallery;

internal static class GalleryConditionPresentation
{
    internal static IReadOnlyList<ConditionDisplayItem> Build(GalleryEvent entry, ITranslationHelper i18n, CurrentStateSnapshot? state = null)
    {
        ConditionParser parser = ConditionProduction.CreateParser(Event.SplitPreconditions, ArgUtility.SplitBySpaceQuoteAware);
        ConditionPresentationBuilder presentation = new(
            parser,
            ConditionProduction.CreateEvaluator(null),
            (key, arguments) => i18n.Get(key, arguments),
            new ConditionDisplayResolver(NPC.GetDisplayName, id => ItemRegistry.GetData(id)?.DisplayName,
                (key, value) => Term(key, value, i18n), Game1.getTimeOfDayString));
        GameLocation? location = entry.Resolved.HasLocationContext ? Game1.getLocationFromName(entry.LocationName) : null;
        CurrentStateSnapshot current = RuntimeStateReader.ForLocation(state ?? RuntimeStateReader.Capture(), location)
            with { Details = ConditionStateReader.Capture(parser.ParseRawKey(entry.EventKey), location) };
        return presentation.Build(entry.EventKey, current,
            Location(entry, i18n));
    }

    internal static string Location(GalleryEvent entry, ITranslationHelper i18n)
        => !entry.Resolved.HasLocationContext ? i18n.Get("directory.no-location") : GalleryLocationName.Resolve(entry.LocationName, Game1.getLocationFromName(entry.LocationName)?.DisplayName,
            key => i18n.Get(key).HasValue() ? i18n.Get(key).ToString() : null);

    private static string Term(string group, string value, ITranslationHelper i18n)
    {
        if (group == "festival")
        {
            try
            {
                if (Utility.TryGetPassiveFestivalData(value, out var data) && !string.IsNullOrWhiteSpace(data.DisplayName))
                    return StardewValley.TokenizableStrings.TokenParser.ParseText(data.DisplayName);
            }
            catch { }
        }
        string key = $"{group}.{value.ToLowerInvariant()}";
        return i18n.Get(key).HasValue() ? i18n.Get(key).ToString() : value;
    }
}
