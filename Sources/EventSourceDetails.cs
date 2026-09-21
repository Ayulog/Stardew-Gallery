using StardewModdingAPI;
using StardewValley;

namespace StardewGallery;

/// <summary>Reads source evidence when opening a detail view. Does not run during drawing.</summary>
internal sealed class EventSourceDetails(EventDefinitionSources definitions, SmapiEventSourceObserver observer, bool enabledAtStartup,
    Func<bool> configured, ITranslationHelper i18n, IMonitor monitor)
{
    private bool reportedFailure;

    internal EventSourceDisplay Read(ResolvedEvent resolved)
    {
        EventSourceDisplayState state = configured() != enabledAtStartup ? EventSourceDisplayState.RestartRequired
            : !enabledAtStartup ? EventSourceDisplayState.Disabled
            : !observer.Enabled ? EventSourceDisplayState.Unavailable : EventSourceDisplayState.Ready;
        EventSourceInfo? info = null;
        if (state == EventSourceDisplayState.Ready)
        {
            info = Unknown(resolved);
            try
            {
                GameLocation? location = Game1.getLocationFromName(resolved.LocationName);
                if (location?.TryGetLocationEvents(out string assetName, out Dictionary<string, string> events) == true)
                    info = EventSourceDetailLookup.Read(resolved, assetName, events, observer.Lookup);
            }
            catch (Exception error)
            {
                if (!reportedFailure)
                {
                    reportedFailure = true;
                    monitor.Log("Event detail source lookup unavailable: " + error.Message, LogLevel.Warn);
                }
            }
        }
        EventOriginMatch origin = definitions.Read(resolved.AssetName, resolved.EventId);
        return EventOriginPresentation.Build(resolved, origin, info, state, (key, args) => i18n.Get(key, args));
    }

    internal static EventSourceInfo Unknown(ResolvedEvent resolved)
        => EventSourceDetailLookup.Unknown(resolved);
}
