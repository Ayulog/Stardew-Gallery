using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace StardewGallery;

internal sealed class ReplayCoordinator(IMonitor monitor, IModHelper helper, HistoricalReplayAssets historicalAssets,
    Func<bool> autoAdvanceDialogue, Func<bool> debugDiagnostics)
{
    private const int StartTimeoutTicks = 900;
    private readonly EventLauncher eventLauncher = new();
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
    private PreviewInjectionScope? previewScope;

    internal bool IsActive => snapshot is not null || previewScope is not null;
    internal int SpeedMultiplier => speedMultiplier;
    internal int EffectiveSpeedMultiplier => !IsActive || restoring || !observed || Game1.CurrentEvent is null
        || ReplayLifecycleRules.BlocksReplaySpeed(
            ReplayLifecycleRules.IsTransitionBlocking(Game1.fadeToBlackAlpha, Game1.globalFade, Game1.nonWarpFade, Game1.locationRequest is not null),
            Game1.activeClickableMenu is StardewValley.Menus.DialogueBox)
        || Game1.activeClickableMenu is not null && Game1.activeClickableMenu is not StardewValley.Menus.DialogueBox
        || Game1.activeClickableMenu is StardewValley.Menus.DialogueBox dialogue && ReplaySpeedPatches.IsChoice(dialogue)
        ? 1 : speedMultiplier;

    internal bool TryStart(GalleryEvent entry, Action reopenMenu, out string error)
    {
        error = string.Empty;
        if (IsActive)
        {
            error = helper.Translation.Get("replay.already-running");
            return false;
        }

        EventPlayback playback = EventPlayback.ForCurrent(entry.Resolved);

        try
        {
            Trace($"回放请求：地点={playback.LocationName}，事件={playback.EventId}。");
            backupPath = ReplayBackup.Create();
            Trace($"回放备份完成：{backupPath}");
            snapshot = ReplaySnapshot.Capture();
            reopen = reopenMenu;
            eventId = playback.EventId;
            targetLocationName = playback.LocationName;
            speedMultiplier = 1;
            WriteDiagnostics("requested");
            Game1.activeClickableMenu = null;
            EventLaunchResult launch = eventLauncher.TryLaunch(playback);
            if (launch.Accepted)
            {
                Trace($"事件回放已接受：地点={playback.LocationName}，事件={playback.EventId}。");
                WriteDiagnostics("accepted");
                return true;
            }
            error = MapLaunchFailure(launch.Failure, playback.LocationName);
        }
        catch (Exception ex)
        {
            monitor.Log($"回放启动失败：地点={playback.LocationName}，事件={playback.EventId}。\n{ex}", LogLevel.Error);
            error = helper.Translation.Get("replay.failed");
        }

        Restore(error);
        return false;
    }

    internal bool TryStartPreview(GalleryEvent entry, PreviewState state, Action reopenMenu, out string error)
    {
        error = string.Empty;
        if (IsActive)
        {
            error = helper.Translation.Get("replay.already-running");
            return false;
        }
        if (state is null)
        {
            error = helper.Translation.Get("preview.not-available");
            return false;
        }

        EventPlayback playback = EventPlayback.ForCurrent(entry.Resolved);
        try
        {
            Trace($"预览请求：地点={playback.LocationName}，事件={playback.EventId}。");
            backupPath = ReplayBackup.Create();
            Trace($"预览备份完成：{backupPath}");
            snapshot = ReplaySnapshot.Capture();
            previewScope = PreviewInjectionScope.Apply(new RuntimePreviewStateAccessor(), state);
            reopen = reopenMenu;
            eventId = playback.EventId;
            targetLocationName = playback.LocationName;
            speedMultiplier = 1;
            WriteDiagnostics("preview-requested");
            Game1.activeClickableMenu = null;
            EventLaunchResult launch = eventLauncher.TryLaunch(playback);
            if (launch.Accepted)
            {
                Trace($"事件预览已接受：地点={playback.LocationName}，事件={playback.EventId}。");
                WriteDiagnostics("preview-accepted");
                return true;
            }
            error = MapLaunchFailure(launch.Failure, playback.LocationName);
        }
        catch (Exception ex)
        {
            monitor.Log($"预览启动失败：地点={playback.LocationName}，事件={playback.EventId}。\n{ex}", LogLevel.Error);
            error = helper.Translation.Get("preview.failed");
        }

        Restore(error);
        return false;
    }

    private string MapLaunchFailure(EventLaunchFailure? failure, string locationName)
    {
        return failure?.Kind switch
        {
            EventLaunchFailureKind.InvalidPlayback => helper.Translation.Get("replay.not-found"),
            EventLaunchFailureKind.LocationMissing => helper.Translation.Get("replay.location-missing", new { location = locationName }),
            EventLaunchFailureKind.ConstructionFailed => helper.Translation.Get("replay.failed"),
            EventLaunchFailureKind.SchedulingFailed => helper.Translation.Get("replay.not-started"),
            _ => helper.Translation.Get("replay.not-started")
        };
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
        string? completedBackup = backupPath;
        if (completedBackup is not null && !ReplayBackup.Delete(completedBackup))
            monitor.Log($"回放成功后清理临时备份失败（保留为 stale）：{completedBackup}", LogLevel.Warn);
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
        previewScope?.Dispose();
        previewScope = null;
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
    private const string RootName = "backups";
    private const string ArchiveRootName = "backups-archive";

    internal static string DataRoot()
        => Path.Combine(Constants.DataPath, "StardewGallery");

    internal static string RootFor(string save)
        => Path.Combine(DataRoot(), RootName, save);

    internal static string ArchiveRootFor(string save)
        => Path.Combine(DataRoot(), ArchiveRootName, save);

    internal static string Create()
    {
        string source = Constants.CurrentSavePath ?? throw new InvalidOperationException("当前存档目录不存在。");
        string save = Constants.SaveFolderName ?? throw new InvalidOperationException("当前存档名称不存在。");
        string root = RootFor(save);
        Directory.CreateDirectory(root);
        Prune(save);
        string destination = Path.Combine(root, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
        Copy(source, destination, overwrite: false);
        return destination;
    }

    internal static void Restore(string backup)
    {
        string destination = Constants.CurrentSavePath ?? throw new InvalidOperationException("当前存档目录不存在。");
        Copy(backup, destination, overwrite: true);
    }

    internal static bool Delete(string backupPath)
    {
        if (string.IsNullOrWhiteSpace(backupPath) || !IsUnderDataRoot(backupPath))
            return false;
        try
        {
            if (Directory.Exists(backupPath))
                Directory.Delete(backupPath, recursive: true);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    internal static void Prune(string save)
    {
        string root = RootFor(save);
        string archive = ArchiveRootFor(save);
        try
        {
            IEnumerable<string> activeDirs = Directory.Exists(root) ? Directory.EnumerateDirectories(root) : [];
            IEnumerable<string> archiveDirs = Directory.Exists(archive) ? Directory.EnumerateDirectories(archive) : [];
            List<string> names = activeDirs
                .Concat(archiveDirs)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .ToList();
            IReadOnlyList<string> retain = ReplayBackupRetention.Retain(names);
            foreach (string name in names.Where(name => !retain.Contains(name)))
            {
                string activePath = Path.Combine(root, name);
                string archivePath = Path.Combine(archive, name);
                if (Directory.Exists(activePath))
                    Directory.Delete(activePath, recursive: true);
                else if (Directory.Exists(archivePath))
                    Directory.Delete(archivePath, recursive: true);
            }
            // migrate retained archive dirs back into active root; drop empty archive/save dir
            foreach (string name in retain)
            {
                string activePath = Path.Combine(root, name);
                string archivePath = Path.Combine(archive, name);
                if (!Directory.Exists(activePath) && Directory.Exists(archivePath))
                    Directory.Move(archivePath, activePath);
            }
            if (Directory.Exists(archive) && !Directory.EnumerateDirectories(archive).Any())
                Directory.Delete(archive, recursive: false);
        }
        catch (Exception)
        {
            // cleanup failure must not affect gameplay/replay
        }
    }

    private static bool IsUnderDataRoot(string path)
    {
        string root = Path.GetFullPath(DataRoot()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        string full = Path.GetFullPath(path);
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
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
