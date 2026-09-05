using StardewValley;

namespace StardewGallery;

internal enum EventLaunchFailureKind
{
    InvalidPlayback,
    LocationMissing,
    ConstructionFailed,
    SchedulingFailed
}

internal sealed record EventLaunchFailure(
    EventLaunchFailureKind Kind,
    string Detail
);

internal sealed record EventLaunchResult(
    bool Accepted,
    Event? Event,
    EventLaunchFailure? Failure
);

internal sealed class EventLauncher
{
    internal EventLaunchResult TryLaunch(EventPlayback playback, Action<GameLocation>? prepareEnvironment = null)
    {
        if (string.IsNullOrWhiteSpace(playback.AssetName) || string.IsNullOrWhiteSpace(playback.EventId)
            || string.IsNullOrWhiteSpace(playback.RootScript) || string.IsNullOrWhiteSpace(playback.LocationName))
            return new EventLaunchResult(false, null,
                new EventLaunchFailure(EventLaunchFailureKind.InvalidPlayback, "事件播放规格无效（AssetName/EventId/RootScript/LocationName 为空）。"));

        GameLocation? location = Game1.getLocationFromName(playback.LocationName);
        if (location is null)
            return new EventLaunchResult(false, null,
                new EventLaunchFailure(EventLaunchFailureKind.LocationMissing, $"目标地点缺失：{playback.LocationName}"));

        Event replayEvent;
        try
        {
            replayEvent = new Event(playback.RootScript, playback.AssetName, playback.EventId, Game1.player);
        }
        catch (Exception error)
        {
            return new EventLaunchResult(false, null,
                new EventLaunchFailure(EventLaunchFailureKind.ConstructionFailed, $"事件构造失败：{error.Message}"));
        }

        if (location.Name != Game1.currentLocation.Name)
        {
            try
            {
                LocationRequest request = Game1.getLocationRequest(location.Name);
                request.OnLoad += () =>
                {
                    prepareEnvironment?.Invoke(Game1.currentLocation);
                    Game1.currentLocation.currentEvent = replayEvent;
                };
                int x = 8;
                int y = 8;
                Utility.getDefaultWarpLocation(request.Name, ref x, ref y);
                Game1.warpFarmer(request, x, y, Game1.player.FacingDirection);
                return new EventLaunchResult(true, replayEvent, null);
            }
            catch (Exception error)
            {
                return new EventLaunchResult(false, replayEvent,
                    new EventLaunchFailure(EventLaunchFailureKind.SchedulingFailed, $"跨地图调度失败：{error.Message}"));
            }
        }

        try
        {
            Game1.globalFadeToBlack(() =>
            {
                Game1.forceSnapOnNextViewportUpdate = true;
                prepareEnvironment?.Invoke(Game1.currentLocation);
                Game1.currentLocation.startEvent(replayEvent);
                Game1.globalFadeToClear();
            });
            return new EventLaunchResult(true, replayEvent, null);
        }
        catch (Exception error)
        {
            return new EventLaunchResult(false, replayEvent,
                new EventLaunchFailure(EventLaunchFailureKind.SchedulingFailed, $"同地点调度失败：{error.Message}"));
        }
    }
}
