using StardewModdingAPI;

namespace StardewGallery;

/// <summary>Reads source evidence when opening a detail view. Does not run during drawing.</summary>
internal sealed class EventSourceDetails(EventDefinitionSources definitions, ITranslationHelper i18n)
{
    internal EventSourceDisplay Read(ResolvedEvent resolved)
        => EventOriginPresentation.Build(resolved, definitions.Read(resolved.AssetName, resolved.EventId), (key, args) => i18n.Get(key, args));
}
