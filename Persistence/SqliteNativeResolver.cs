using System.Runtime.InteropServices;

namespace StardewGallery;

internal static class SqliteNativeResolver
{
    internal const string RuntimesDir = "runtimes";
    internal const string NativeDir = "native";

    internal static string? ResolveNativePath(string modDirectory, string? rid, OSPlatform os, Architecture architecture)
    {
        string? exact = TryRidDirectory(modDirectory, rid);
        if (exact is not null)
            return exact;
        return ResolveFallbackPath(modDirectory, os, architecture);
    }

    internal static string? ResolveFallbackPath(string modDirectory, OSPlatform os, Architecture architecture)
    {
        string? rid = FallbackRid(os, architecture);
        return rid is null ? null : TryRidDirectory(modDirectory, rid);
    }

    internal static string? TryRidDirectory(string modDirectory, string? rid)
    {
        if (string.IsNullOrWhiteSpace(rid))
            return null;
        string? fileName = NativeFileName(rid);
        if (fileName is null)
            return null;
        string path = Path.Combine(modDirectory, RuntimesDir, rid, NativeDir, fileName);
        return File.Exists(path) ? path : null;
    }

    internal static string? FallbackRid(OSPlatform os, Architecture architecture)
    {
        string arch = architecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            _ => ""
        };
        if (arch.Length == 0)
            return null;
        if (os == OSPlatform.Windows)
            return "win-" + arch;
        if (os == OSPlatform.OSX)
            return "osx-" + arch;
        if (os == OSPlatform.Linux)
            return "linux-" + arch;
        return null;
    }

    internal static string? NativeFileName(string rid)
    {
        if (rid.StartsWith("win", StringComparison.Ordinal))
            return "e_sqlite3.dll";
        if (rid.StartsWith("linux", StringComparison.Ordinal))
            return "libe_sqlite3.so";
        if (rid.StartsWith("osx", StringComparison.Ordinal) || rid.StartsWith("mac", StringComparison.Ordinal))
            return "libe_sqlite3.dylib";
        return null;
    }
}
