using Microsoft.Xna.Framework.Content;
using StardewValley;

namespace StardewGallery;

/// <summary>
/// Safe read-only wrapper around <see cref="LocalizedContentManager.LoadStringReturnNullIfNotFound(string, bool)"/>.
/// In Stardew Valley 1.6.15, <c>LoadStringReturnNullIfNotFound</c> calls <c>parseStringPath</c> and <c>Load&lt;Dictionary&lt;string, string&gt;&gt;</c>
/// without internal try/catch, throwing <see cref="ContentLoadException"/> whenever the string has no ':' or names a missing/non-dictionary asset.
/// </summary>
internal static class SafeTranslationLoader
{
    internal static string? TryLoad(LocalizedContentManager? content, string? rawKey, bool localeFallback = true)
    {
        if (content is null || !ParallelBranchAnalyzer.HasTranslationKeySyntax(rawKey))
            return null;

        int colon = rawKey!.IndexOf(':');
        string assetName = rawKey[..colon];
        if (string.IsNullOrWhiteSpace(assetName))
            return null;

        try
        {
            if (!content.DoesAssetExist<Dictionary<string, string>>(assetName))
                return null;
            return content.LoadStringReturnNullIfNotFound(rawKey, localeFallback);
        }
        catch (Exception ex) when (ex is ContentLoadException or InvalidCastException or ArgumentException or FormatException)
        {
            return null;
        }
    }
}
