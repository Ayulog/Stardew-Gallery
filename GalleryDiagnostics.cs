using System.Text.Json;
using StardewModdingAPI;

namespace StardewGallery;

internal static class GalleryDiagnostics
{
    internal static string DirectoryPath => Path.Combine(Constants.DataPath, "StardewGallery", "diagnostics");

    internal static void Write(string fileName, object value, IMonitor monitor)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(Path.Combine(DirectoryPath, fileName), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception error)
        {
            monitor.Log($"写入调试 JSON 失败：{error.Message}", LogLevel.Warn);
        }
    }
}
