using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace StardewGallery;

internal sealed class ReplayCoordinator(IMonitor monitor, IModHelper helper, HistoricalReplayAssets historicalAssets,
    Func<bool> autoAdvanceDialogue, Func<bool> debugDiagnostics)
{
    private const int StartTimeoutTicks = 900;
    private ReplaySnapshot? snapshot;
    private Action? reopen;
    private string? backupPath;
    private string? eventId;
    private int ticks;
    private int quietTicks;
    private bool observed;
    private bool restoring;
    private int restoreStableTicks;
    private string? targetLocationName;
    private bool restorePlayerApplied;
    private int speedMultiplier = 1;
    private int dialogueAutoTicks;
    private Event? activeReplayEvent;

    internal bool IsActive => snapshot is not null;
    internal int SpeedMultiplier => speedMultiplier;
    internal int EffectiveSpeedMultiplier => !IsActive || restoring || !observed || Game1.CurrentEvent is null
        || ReplayLifecycleRules.BlocksReplaySpeed(
            ReplayLifecycleRules.IsTransitionBlocking(Game1.fadeToBlackAlpha, Game1.globalFade, Game1.nonWarpFade, Game1.locationRequest is not null),
            Game1.activeClickableMenu is StardewValley.Menus.DialogueBox)
        || Game1.activeClickableMenu is not null && Game1.activeClickableMenu is not StardewValley.Menus.DialogueBox
        || Game1.activeClickableMenu is StardewValley.Menus.DialogueBox dialogue && ReplaySpeedPatches.IsChoice(dialogue)
        ? 1 : speedMultiplier;

    internal bool TryStart(GalleryEvent entry, WatchedEventSnapshot? watchedVersion, Action reopenMenu, out string error)
    {
        error = string.Empty;
        if (IsActive)
        {
            error = helper.Translation.Get("replay.already-running");
            return false;
        }

        GameLocation? location = Game1.getLocationFromName(entry.LocationName);
        if (location is null)
        {
            error = helper.Translation.Get("replay.location-missing", new { location = entry.LocationName });
            return false;
        }

        try
        {
            Trace($"回放请求：地点={entry.LocationName}，事件={entry.EventId}，版本={(watchedVersion is null ? "当前" : watchedVersion.Fingerprint[..12])}。");
            backupPath = ReplayBackup.Create();
            Trace($"回放备份完成：{backupPath}");
            snapshot = ReplaySnapshot.Capture();
            reopen = reopenMenu;
            eventId = entry.EventId;
            targetLocationName = entry.LocationName;
            speedMultiplier = 1;
            WriteDiagnostics("requested");
            Game1.activeClickableMenu = null;
            bool started;
            bool validEvent;
            if (watchedVersion is null)
                started = Game1.PlayEvent(entry.EventId, location, out validEvent, checkPreconditions: false, checkSeen: false);
            else
            {
                historicalAssets.Activate(watchedVersion);
                started = StartHistoricalEvent(watchedVersion, location);
                validEvent = started;
            }
            if (started && validEvent)
            {
                Trace($"事件回放已接受：地点={entry.LocationName}，事件={entry.EventId}。");
                WriteDiagnostics("accepted");
                return true;
            }
            error = helper.Translation.Get(validEvent ? "replay.not-started" : "replay.not-found");
        }
        catch (Exception ex)
        {
            monitor.Log($"回放启动失败：地点={entry.LocationName}，事件={entry.EventId}。\n{ex}", LogLevel.Error);
            error = helper.Translation.Get("replay.failed");
        }

        Restore(error);
        return false;
    }

    private static bool StartHistoricalEvent(WatchedEventSnapshot snapshot, GameLocation location)
    {
        Event replayEvent = new(snapshot.RootScript, snapshot.AssetName, snapshot.EventId, Game1.player);
        if (location.Name != Game1.currentLocation.Name)
        {
            LocationRequest request = Game1.getLocationRequest(location.Name);
            request.OnLoad += () => Game1.currentLocation.currentEvent = replayEvent;
            int x = 8;
            int y = 8;
            Utility.getDefaultWarpLocation(request.Name, ref x, ref y);
            Game1.warpFarmer(request, x, y, Game1.player.FacingDirection);
        }
        else
        {
            Game1.globalFadeToBlack(() =>
            {
                Game1.forceSnapOnNextViewportUpdate = true;
                Game1.currentLocation.startEvent(replayEvent);
                Game1.globalFadeToClear();
            });
        }
        return true;
    }

    internal void Update()
    {
        if (snapshot is null)
            return;

        if (restoring)
        {
            bool transitionPending = Game1.locationRequest is not null;
            bool fading = ReplayLifecycleRules.IsTransitionBlocking(Game1.fadeToBlackAlpha, Game1.globalFade, Game1.nonWarpFade, false);
            if (!restorePlayerApplied)
            {
                if (ReplayLifecycleRules.CanApplyRestore(transitionPending, fading))
                {
                    snapshot.RestorePlayer();
                    restorePlayerApplied = true;
                    bool alreadyThere = Game1.currentLocation?.NameOrUniqueName.Equals(snapshot.LocationName, StringComparison.OrdinalIgnoreCase) == true;
                    if (!alreadyThere)
                    {
                        Game1.warpFarmer(snapshot.LocationName, (int)snapshot.Tile.X, (int)snapshot.Tile.Y, false);
                    }
                }
                if (++ticks >= StartTimeoutTicks)
                    FailSafe(new TimeoutException("等待事件换图结束超时。"));
                return;
            }

            bool locationMatches = Game1.currentLocation?.NameOrUniqueName.Equals(snapshot.LocationName, StringComparison.OrdinalIgnoreCase) == true;
            restoreStableTicks = locationMatches && !transitionPending && !fading ? restoreStableTicks + 1 : 0;
            if (ReplayLifecycleRules.CanFinishRestore(locationMatches, transitionPending, fading, restoreStableTicks))
            {
                snapshot.RestorePositionAndPresentation();
                FinishRestore();
            }
            else if (++ticks >= StartTimeoutTicks)
                FailSafe(new TimeoutException("恢复原地点超时。"));
            return;
        }

        ticks++;
        bool playing = Game1.CurrentEvent is not null || Game1.eventUp;
        if (playing)
        {
            if (ReplayLifecycleRules.IsSecondaryEvent(observed, activeReplayEvent, Game1.CurrentEvent))
            {
                Event secondary = Game1.CurrentEvent!;
                Trace($"阻止回放结束后自动触发的独立事件：回放={eventId}，后续={secondary.id}。");
                secondary.markEventSeen = false;
                secondary.exitEvent();
                Restore(null);
                return;
            }
            if (!observed)
            {
                activeReplayEvent = Game1.CurrentEvent;
                Trace($"已观察到事件实际开始：地点={Game1.currentLocation?.NameOrUniqueName}，事件={eventId}。");
                if (Game1.currentLocation is GameLocation current)
                    Trace($"回放地图诊断：实际地点={current.NameOrUniqueName}，地图={current.mapPath.Value}，尺寸={current.Map.Layers[0].LayerWidth}x{current.Map.Layers[0].LayerHeight}，玩家地块={Game1.player.Tile}，房屋等级={Game1.player.HouseUpgradeLevel}。");
                WriteDiagnostics("started");
            }
            observed = true;
            quietTicks = 0;
            UpdateAutoAdvance();
            if (ticks is 120 or 300 or 600)
                LogPlaybackProgress();
            return;
        }

        if (observed)
            quietTicks++;
        if (ReplayLifecycleRules.ShouldRestore(observed, quietTicks, ticks, StartTimeoutTicks))
            Restore(observed ? null : helper.Translation.Get("replay.start-timeout").ToString());
    }

    private void Restore(string? message)
    {
        if (snapshot is null || restoring)
            return;
        try
        {
            Trace($"开始恢复回放前状态：事件={eventId}，实际开始={observed}。");
            restoring = true;
            ticks = 0;
            restoreStableTicks = 0;
            restorePlayerApplied = false;
            if (!string.IsNullOrWhiteSpace(message))
                Game1.addHUDMessage(new HUDMessage(message, HUDMessage.error_type));
        }
        catch (Exception ex)
        {
            FailSafe(ex);
        }
    }

    private void FinishRestore()
    {
        Trace($"回放状态恢复完成：事件={eventId}。");
        Action? open = reopen;
        Clear();
        open?.Invoke();
    }

    private void FailSafe(Exception error)
    {
        monitor.Log($"内存恢复失败，将使用回放前备份并返回标题：事件={eventId}，备份={backupPath}。\n{error}", LogLevel.Error);
        try
        {
            if (backupPath is not null)
                ReplayBackup.Restore(backupPath);
        }
        catch (Exception backupError)
        {
            monitor.Log($"备份覆盖失败：{backupError}", LogLevel.Error);
        }
        string? save = Constants.SaveFolderName;
        Game1.activeClickableMenu = null;
        Clear();
        if (save is not null)
            SaveGame.Load(save);
        else
            Game1.exitToTitle = true;
    }

    private void Clear()
    {
        historicalAssets.Clear();
        snapshot = null;
        reopen = null;
        backupPath = null;
        eventId = null;
        ticks = 0;
        quietTicks = 0;
        observed = false;
        restoring = false;
        restoreStableTicks = 0;
        targetLocationName = null;
        restorePlayerApplied = false;
        speedMultiplier = 1;
        dialogueAutoTicks = 0;
        activeReplayEvent = null;
    }

    internal void CycleSpeed()
    {
        if (!IsActive || restoring)
            return;
        speedMultiplier = ReplayLifecycleRules.NextSpeed(speedMultiplier);
        dialogueAutoTicks = 0;
        Game1.playSound("smallSelect");
        Trace($"回放速度切换为 {speedMultiplier}x：事件={eventId}，当前有效={EffectiveSpeedMultiplier}x，遮罩={Game1.fadeToBlackAlpha:0.00}，换图={Game1.locationRequest is not null}，菜单={Game1.activeClickableMenu?.GetType().Name ?? "无"}。");
    }

    private void UpdateAutoAdvance()
    {
        if (!autoAdvanceDialogue() || EffectiveSpeedMultiplier <= 1
            || Game1.activeClickableMenu is not StardewValley.Menus.DialogueBox dialogue
            || ReplaySpeedPatches.IsChoice(dialogue) || dialogue.transitioning
            || dialogue.characterIndexInDialogue < dialogue.getCurrentString().Length - 1 || dialogue.safetyTimer > 0)
        {
            dialogueAutoTicks = 0;
            return;
        }
        if (++dialogueAutoTicks < (speedMultiplier == 4 ? 12 : 20))
            return;
        dialogueAutoTicks = 0;
        dialogue.receiveLeftClick(Game1.getMouseX(true), Game1.getMouseY(true));
    }

    private void LogPlaybackProgress()
    {
        Event? current = Game1.CurrentEvent;
        int commandIndex = current?.CurrentCommand ?? -1;
        string command = current is not null && commandIndex >= 0 && commandIndex < current.eventCommands.Length
            ? current.eventCommands[commandIndex]
            : "<none>";
        Trace($"回放进度诊断：目标地点={targetLocationName}，实际地点={Game1.currentLocation?.NameOrUniqueName}，事件={eventId}，帧={ticks}，命令={commandIndex}:{command}，玩家地块={Game1.player.Tile}，镜头=({Game1.viewport.X},{Game1.viewport.Y})，淡出={Game1.fadeToBlack || Game1.globalFade}。");
        WriteDiagnostics("progress", commandIndex, command);
    }

    private void Trace(string message)
    {
        if (debugDiagnostics())
            monitor.Log(message, LogLevel.Debug);
    }

    private void WriteDiagnostics(string stage, int commandIndex = -1, string? command = null)
    {
        if (!debugDiagnostics())
            return;
        GalleryDiagnostics.Write("replay-latest.json", new
        {
            Timestamp = DateTimeOffset.Now,
            Stage = stage,
            EventId = eventId,
            TargetLocation = targetLocationName,
            ActualLocation = Game1.currentLocation?.NameOrUniqueName,
            Map = Game1.currentLocation?.mapPath.Value,
            PlayerTile = Game1.player is null ? null : new { Game1.player.Tile.X, Game1.player.Tile.Y },
            Viewport = new { Game1.viewport.X, Game1.viewport.Y },
            FadeAlpha = Game1.fadeToBlackAlpha,
            LocationPending = Game1.locationRequest is not null,
            SelectedSpeed = speedMultiplier,
            EffectiveSpeed = EffectiveSpeedMultiplier,
            Menu = Game1.activeClickableMenu?.GetType().Name,
            CommandIndex = commandIndex,
            Command = command
        }, monitor);
    }
}

internal static class ReplayBackup
{
    internal static string Create()
    {
        string source = Constants.CurrentSavePath ?? throw new InvalidOperationException("当前存档目录不存在。");
        string save = Constants.SaveFolderName ?? throw new InvalidOperationException("当前存档名称不存在。");
        string data = Path.Combine(Constants.DataPath, "StardewGallery");
        string root = Path.Combine(data, "backups", save);
        Directory.CreateDirectory(root);
        string destination = Path.Combine(root, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
        Copy(source, destination, overwrite: false);
        foreach (DirectoryInfo old in new DirectoryInfo(root).EnumerateDirectories().OrderByDescending(p => p.Name).Skip(5))
        {
            string archive = Path.Combine(data, "backups-archive", save);
            Directory.CreateDirectory(archive);
            old.MoveTo(Path.Combine(archive, old.Name));
        }
        return destination;
    }

    internal static void Restore(string backup)
    {
        string destination = Constants.CurrentSavePath ?? throw new InvalidOperationException("当前存档目录不存在。");
        Copy(backup, destination, overwrite: true);
    }

    private static void Copy(string source, string destination, bool overwrite)
    {
        Directory.CreateDirectory(destination);
        foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)), overwrite);
    }
}
