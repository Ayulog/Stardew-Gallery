using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace StardewGallery;

internal sealed class ReplayService(IModHelper helper, IMonitor monitor, ReplayCoordinator coordinator,
    Func<bool> protectionReady, Func<bool> ordinaryEnabled, Func<bool> unlockAll, Func<bool> showWarning)
{
    private bool warningShown;
    private bool confirmationPending;
    private EventIdentity? activeIdentity;

    internal EventIdentity? ActiveIdentity
    {
        get
        {
            if (!coordinator.IsActive) activeIdentity = null;
            return activeIdentity;
        }
    }

    internal ReplayAccess Access(GalleryEvent entry)
        => ReplayAccessPolicy.Evaluate(entry.Kind, Game1.player?.eventsSeen.Contains(entry.EventId) == true,
            unlockAll(), ordinaryEnabled(), protectionReady(), Context.IsMultiplayer,
            coordinator.IsActive || confirmationPending || Game1.eventUp || Game1.CurrentEvent is not null);

    internal void Request(GalleryEvent entry, Action completed)
    {
        bool finished = false;
        void Complete()
        {
            if (finished) return;
            finished = true;
            activeIdentity = null;
            completed();
        }

        void Report(string message) => Game1.addHUDMessage(new HUDMessage(message, HUDMessage.error_type));

        void Start()
        {
            // Settings and world state can change while the confirmation is open.
            ReplayAccess access = Access(entry);
            if (!access.Allowed)
            {
                Report(helper.Translation.Get(access.ReasonKey ?? "replay.failed"));
                Complete();
                return;
            }
            try
            {
                if (coordinator.TryStart(entry, Complete, out string error))
                    activeIdentity = entry.Resolved.Identity;
                else
                {
                    activeIdentity = null;
                    if (!coordinator.IsActive)
                    {
                        Report(error);
                        Complete();
                    }
                }
            }
            catch (Exception error)
            {
                activeIdentity = null;
                monitor.Log($"Replay request failed: {entry.Identity}.\n{error}", LogLevel.Error);
                Report(helper.Translation.Get("replay.failed"));
                if (!coordinator.IsActive) Complete();
            }
        }

        ReplayAccess initial = Access(entry);
        if (!initial.Allowed)
        {
            Report(helper.Translation.Get(initial.ReasonKey ?? "replay.failed"));
            return;
        }
        if (showWarning() && !warningShown)
        {
            confirmationPending = true;
            Game1.activeClickableMenu = new GalleryReplayConfirmationDialog(helper.Translation.Get("replay.warning"), _ =>
            {
                confirmationPending = false;
                warningShown = true;
                Game1.activeClickableMenu = null;
                Start();
            }, _ =>
            {
                confirmationPending = false;
                Game1.activeClickableMenu = null;
                Complete();
            });
            return;
        }
        Start();
    }

    internal void ResetWarnings()
    {
        warningShown = false;
        confirmationPending = false;
        activeIdentity = null;
    }
}
