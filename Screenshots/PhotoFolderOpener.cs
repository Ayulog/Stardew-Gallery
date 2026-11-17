using System.Diagnostics;
using System.Runtime.CompilerServices;
using Steamworks;

namespace StardewGallery;

/// <summary>
/// Opens the local <c>event-photos</c> folder on desktop systems, and gracefully degrades to a readable path notice
/// on Steam Deck Game Mode, Steam Big Picture Mode, or headless/sandboxed environments.
/// </summary>
internal static class PhotoFolderOpener
{
    internal static PhotoFolderOpenResult TryOpen(string rootDir, string? profileDir, string? eventDir)
    {
        string displayPath = PhotoFolderRules.ResolvePreferredDisplayPath(rootDir, profileDir, eventDir, Directory.Exists);
        if (IsHandheldOrOverlayMode())
            return new(PhotoFolderOpenOutcome.HandheldFallback, displayPath);

        try
        {
            if (string.IsNullOrWhiteSpace(rootDir))
                return new(PhotoFolderOpenOutcome.Failed, displayPath, "Missing photo root directory.");

            string fullRoot = Path.GetFullPath(rootDir);
            Directory.CreateDirectory(fullRoot);

            string? targetDir = PhotoFolderRules.ResolveExistingTargetDirectory(fullRoot, profileDir, eventDir, Directory.Exists);
            if (targetDir is null)
                return new(PhotoFolderOpenOutcome.Failed, displayPath, "Target directory is unavailable.");

            ProcessStartInfo? startInfo = PhotoFolderRules.BuildStartInfo(
                targetDir,
                OperatingSystem.IsWindows(),
                OperatingSystem.IsMacOS(),
                OperatingSystem.IsLinux());
            if (startInfo is null)
                return new(PhotoFolderOpenOutcome.Failed, targetDir, "Unsupported desktop platform.");

            using Process? process = Process.Start(startInfo);
            if (process is null)
                return new(PhotoFolderOpenOutcome.Failed, targetDir, "File manager process did not start.");

            return new(PhotoFolderOpenOutcome.Opened, targetDir);
        }
        catch (Exception error)
        {
            return new(PhotoFolderOpenOutcome.Failed, displayPath, error.Message);
        }
    }

    internal static bool IsHandheldOrOverlayMode()
    {
        bool isBigPicture = false;
        bool isOnDeck = false;
        try
        {
            (isBigPicture, isOnDeck) = QuerySteamMode();
        }
        catch
        {
            // Non-Steam builds (such as GOG) or uninitialized Steamworks safely continue on desktop.
        }

        return PhotoFolderRules.ShouldUseHandheldFallback(
            isLinux: OperatingSystem.IsLinux(),
            xdgCurrentDesktop: Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP"),
            steamDeckEnv: Environment.GetEnvironmentVariable("SteamDeck"),
            isSteamBigPicture: isBigPicture,
            isSteamOnDeck: isOnDeck);
    }

    // Isolated so loading PhotoFolderOpener never forces JIT resolution of Steamworks.NET on non-Steam copies.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (bool IsBigPicture, bool IsOnDeck) QuerySteamMode()
        => SteamOverlayDetector.Query();
}

internal static class SteamOverlayDetector
{
    internal static (bool IsBigPicture, bool IsOnDeck) Query()
        => (SteamUtils.IsSteamInBigPictureMode(), SteamUtils.IsSteamRunningOnSteamDeck());
}
