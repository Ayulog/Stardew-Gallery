using StardewModdingAPI;
using StardewValley;

namespace StardewGallery;

internal static class GalleryConditionPresentation
{
    internal static IReadOnlyList<ConditionDisplayItem> Build(GalleryEvent entry, ITranslationHelper i18n, CurrentStateSnapshot? state = null)
    {
        ConditionPresentationBuilder presentation = new(
            ConditionProduction.CreateParser(Event.SplitPreconditions, ArgUtility.SplitBySpaceQuoteAware),
            ConditionProduction.CreateEvaluator(null),
            (key, arguments) => i18n.Get(key, arguments),
            new ConditionDisplayResolver(NPC.GetDisplayName, id => ItemRegistry.GetData(id)?.DisplayName,
                (key, value) => i18n.Get($"{key}.{value.ToLowerInvariant()}").HasValue()
                    ? i18n.Get($"{key}.{value.ToLowerInvariant()}").ToString() : value, Game1.getTimeOfDayString));
        GameLocation? location = Game1.getLocationFromName(entry.LocationName);
        return presentation.Build(entry.EventKey, RuntimeStateReader.ForLocation(state ?? RuntimeStateReader.Capture(), location),
            Location(entry, i18n));
    }

    internal static string Location(GalleryEvent entry, ITranslationHelper i18n)
        => GalleryLocationName.Resolve(entry.LocationName, Game1.getLocationFromName(entry.LocationName)?.DisplayName,
            key => i18n.Get(key).HasValue() ? i18n.Get(key).ToString() : null);
}
