using System.Reflection;
using System.Runtime.InteropServices;

namespace StardewGallery;

internal static class SqliteNativeBootstrap
{
    private static bool initialized;
    private static readonly object gate = new();

    internal static bool TryInitialize(string modDirectory, Action<string>? logger = null)
    {
        lock (gate)
        {
            if (initialized)
                return true;
            try
            {
                string rid = RuntimeInformation.RuntimeIdentifier;
                string? nativePath = ResolveForPlatform(modDirectory, rid);
                if (nativePath is null)
                {
                    logger?.Invoke($"未找到当前平台 ({rid}) 的 e_sqlite3 native 运行时（{Path.Combine(modDirectory, "runtimes")}）。");
                    return false;
                }

                Assembly provider = Assembly.Load("SQLitePCLRaw.provider.e_sqlite3");
                NativeLibrary.SetDllImportResolver(provider, (libraryName, assembly, searchPath) =>
                {
                    if (!IsSqliteLibrary(libraryName))
                        return IntPtr.Zero;
                    return NativeLibrary.Load(nativePath, assembly, searchPath);
                });
                initialized = true;
                logger?.Invoke($"SQLite native resolver 已注册：{nativePath}");
                return true;
            }
            catch (Exception error)
            {
                logger?.Invoke($"SQLite native bootstrap 失败：\n{error}");
                return false;
            }
        }
    }

    private static string? ResolveForPlatform(string modDirectory, string rid)
    {
        OSPlatform os;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            os = OSPlatform.Windows;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            os = OSPlatform.OSX;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            os = OSPlatform.Linux;
        else
            return null;

        return SqliteNativeResolver.ResolveNativePath(modDirectory, rid, os, RuntimeInformation.ProcessArchitecture);
    }

    private static bool IsSqliteLibrary(string name)
    {
        string trimmed = name;
        int end = trimmed.IndexOfAny(['.', ':']);
        if (end >= 0)
            trimmed = trimmed[..end];
        return trimmed.Equals("e_sqlite3", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("libe_sqlite3", StringComparison.OrdinalIgnoreCase);
    }
}
