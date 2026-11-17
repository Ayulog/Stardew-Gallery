using System.Diagnostics;

namespace StardewGallery;

internal enum PhotoFolderOpenOutcome
{
    Opened,
    HandheldFallback,
    Failed
}

internal sealed record PhotoFolderOpenResult(PhotoFolderOpenOutcome Outcome, string DisplayPath, string? ErrorMessage = null);

/// <summary>
/// Pure path-containment and argument-list <see cref="ProcessStartInfo"/> helpers for opening local photo folders
/// without shell interpolation or path traversal.
/// </summary>
internal static class PhotoFolderRules
{
    internal static bool IsContainedOrEqual(string candidatePath, string rootPath, StringComparison? comparison = null)
    {
        if (string.IsNullOrWhiteSpace(candidatePath) || string.IsNullOrWhiteSpace(rootPath))
            return false;

        StringComparison cmp = comparison ?? DefaultPathComparison();
        string fullRoot = TrimTrailingSeparators(Path.GetFullPath(rootPath));
        string fullCandidate = TrimTrailingSeparators(Path.GetFullPath(candidatePath));
        if (fullCandidate.Equals(fullRoot, cmp))
            return true;

        return fullCandidate.StartsWith(fullRoot + Path.DirectorySeparatorChar, cmp);
    }

    internal static string? ResolveExistingTargetDirectory(
        string rootDir,
        string? profileDir,
        string? eventDir,
        Func<string, bool> directoryExists,
        StringComparison? comparison = null)
    {
        if (string.IsNullOrWhiteSpace(rootDir))
            return null;

        string fullRoot = Path.GetFullPath(rootDir);
        foreach (string? candidate in new[] { eventDir, profileDir })
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;
            string fullCandidate = Path.GetFullPath(candidate);
            if (IsContainedOrEqual(fullCandidate, fullRoot, comparison) && directoryExists(fullCandidate))
                return fullCandidate;
        }

        return directoryExists(fullRoot) ? fullRoot : null;
    }

    internal static string ResolvePreferredDisplayPath(
        string rootDir,
        string? profileDir,
        string? eventDir,
        Func<string, bool> directoryExists,
        StringComparison? comparison = null)
    {
        if (ResolveExistingTargetDirectory(rootDir, profileDir, eventDir, directoryExists, comparison) is { } existing)
            return existing;
        if (!string.IsNullOrWhiteSpace(eventDir) && IsContainedOrEqual(eventDir, rootDir, comparison))
            return Path.GetFullPath(eventDir);
        if (!string.IsNullOrWhiteSpace(profileDir) && IsContainedOrEqual(profileDir, rootDir, comparison))
            return Path.GetFullPath(profileDir);
        return string.IsNullOrWhiteSpace(rootDir) ? "event-photos" : Path.GetFullPath(rootDir);
    }

    internal static ProcessStartInfo? BuildStartInfo(string targetDirectory, bool isWindows, bool isMacOS, bool isLinux)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory))
            return null;

        string? executable = isWindows ? "explorer.exe"
            : isMacOS ? "/usr/bin/open"
            : isLinux ? "xdg-open"
            : null;
        if (executable is null)
            return null;

        ProcessStartInfo psi = new()
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add(targetDirectory);
        return psi;
    }

    internal static bool IsLinuxHandheldEnvironment(string? xdgCurrentDesktop, string? steamDeckEnv)
        => ShouldUseHandheldFallback(
            isLinux: true,
            xdgCurrentDesktop: xdgCurrentDesktop,
            steamDeckEnv: steamDeckEnv,
            isSteamBigPicture: false,
            isSteamOnDeck: false);

    /// <summary>
    /// Degrades to an in-page path notice only in Steam Big Picture Mode or handheld Game Mode (<c>gamescope</c>, or
    /// Steam Deck without an active desktop session). When the player switches a Steam Deck to Desktop Mode (e.g.
    /// <c>XDG_CURRENT_DESKTOP=KDE</c> or <c>GNOME</c> with Big Picture inactive), <c>xdg-open</c> runs normally.
    /// </summary>
    internal static bool ShouldUseHandheldFallback(
        bool isLinux,
        string? xdgCurrentDesktop,
        string? steamDeckEnv,
        bool isSteamBigPicture,
        bool isSteamOnDeck)
    {
        if (isSteamBigPicture)
            return true;

        if (!isLinux)
            return false;

        string desktop = (xdgCurrentDesktop ?? "").Trim();
        if (desktop.Equals("gamescope", StringComparison.OrdinalIgnoreCase))
            return true;

        bool deckHardware = string.Equals(steamDeckEnv?.Trim(), "1", StringComparison.Ordinal) || isSteamOnDeck;
        return deckHardware && desktop.Length == 0;
    }

    private static StringComparison DefaultPathComparison()
        => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static string TrimTrailingSeparators(string fullPath)
    {
        string? root = Path.GetPathRoot(fullPath);
        if (root is not null && fullPath.Equals(root, StringComparison.OrdinalIgnoreCase))
            return fullPath;
        return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
