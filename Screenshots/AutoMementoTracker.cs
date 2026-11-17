using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace StardewGallery;

/// <summary>
/// Per-screen runtime observer that schedules at most one silent auto-memento capture per natural event,
/// local villager wedding, or single-player gallery replay when no cover photo exists yet.
/// </summary>
internal sealed class AutoMementoTracker(
    IMonitor monitor,
    GalleryCatalogCache catalog,
    ReplayCoordinator replay,
    ReplayService replayService,
    Func<bool> autoCaptureEnabled)
{
    private readonly AutoMementoGate gate = new();
    private Event? resolvedInstance;
    private EventIdentity? resolvedIdentity;
    private WeddingDateRecord? resolvedWeddingDate;
    private int lastGameTick = -1;

    internal void Reset()
    {
        gate.Reset();
        resolvedInstance = null;
        resolvedIdentity = null;
        resolvedWeddingDate = null;
        lastGameTick = -1;
    }

    internal void Update(GalleryPhotos photos)
    {
        if (!Context.IsWorldReady || Game1.player is null)
        {
            Reset();
            return;
        }

        if (Game1.ticks == lastGameTick)
            return;
        lastGameTick = Game1.ticks;

        Event? activeEvent = replay.IsActive ? replay.PlayingEvent : Game1.CurrentEvent;
        if (activeEvent is null)
        {
            gate.Reset();
            resolvedInstance = null;
            resolvedIdentity = null;
            resolvedWeddingDate = null;
            return;
        }

        if (!autoCaptureEnabled() || gate.HasAttempted(activeEvent))
            return;

        if (!ReferenceEquals(resolvedInstance, activeEvent))
        {
            resolvedInstance = activeEvent;
            resolvedIdentity = null;
            resolvedWeddingDate = null;
            ResolveTarget(activeEvent);
        }

        if (resolvedIdentity is not { } targetIdentity)
            return;

        int existingCount = photos.Ready ? photos.Read(targetIdentity).Photos.Count : 0;
        bool transitionClear = AutoMementoRules.IsTransitionClear(
            Game1.locationRequest is not null,
            Game1.isWarping,
            Game1.fadeToBlackAlpha,
            Game1.globalFade,
            Game1.fadeToBlack,
            Game1.nonWarpFade);

        (int? mapW, int? mapH) = CurrentMapPixelBounds();
        bool stageVisible = AutoMementoRules.IsStageViewportVisible(
            Game1.viewport.X,
            Game1.viewport.Y,
            Game1.viewport.Width,
            Game1.viewport.Height,
            mapW,
            mapH);

        bool dialogueUp = Game1.activeClickableMenu is DialogueBox dialogue && !dialogue.transitioning;

        if (gate.ShouldRequestCapture(
            activeEvent,
            autoCaptureEnabled: true,
            storeReady: photos.Ready,
            capturePending: photos.Pending,
            existingPhotoCount: existingCount,
            transitionClear: transitionClear,
            stageVisible: stageVisible,
            dialogueUp: dialogueUp))
        {
            photos.RequestSilent(targetIdentity, activeEvent, resolvedWeddingDate);
        }
    }

    private void ResolveTarget(Event activeEvent)
    {
        try
        {
            if (replay.IsActive)
            {
                resolvedIdentity = replayService.ActiveIdentity;
                return;
            }

            if (activeEvent.isFestival)
                return;

            if (activeEvent.isWedding)
            {
                bool hasFarmerSpouse = Game1.player.team?.GetSpouse(Game1.player.UniqueMultiplayerID) is not null;
                if (AutoMementoRules.IsLocalVillagerWedding(
                    activeEvent.isWedding,
                    activeEvent.isFestival,
                    activeEvent.farmer?.UniqueMultiplayerID,
                    Game1.player.UniqueMultiplayerID,
                    Game1.player.hasRoommate(),
                    hasFarmerSpouse,
                    Game1.player.spouse,
                    out string spouseName))
                {
                    WeddingDateRecord date = new(Math.Max(1, Game1.year), Game1.currentSeason ?? "spring", Math.Max(1, Game1.dayOfMonth));
                    if (date.IsValid)
                    {
                        resolvedIdentity = WeddingMemento.IdentityFor(spouseName);
                        resolvedWeddingDate = date.Normalized();
                    }
                }
                return;
            }

            resolvedIdentity = catalog.ResolveCapturableStoryIdentity(
                activeEvent.fromAssetName,
                Game1.currentLocation?.Name,
                activeEvent.id);
        }
        catch (Exception error)
        {
            monitor.LogOnce($"Auto-memento event inspection skipped: {error.Message}", LogLevel.Warn);
        }
    }

    private static (int? Width, int? Height) CurrentMapPixelBounds()
    {
        if (Game1.currentLocation?.Map?.Layers is { Count: > 0 } layers && layers[0] is { } layer
            && layer.LayerWidth > 0 && layer.LayerHeight > 0)
        {
            return (layer.LayerWidth * Game1.tileSize, layer.LayerHeight * Game1.tileSize);
        }
        return (null, null);
    }
}
