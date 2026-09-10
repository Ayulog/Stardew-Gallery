using System.Collections;
using System.Reflection;
using StardewModdingAPI;

namespace StardewGallery.Appearance;

// Framework-owned public records are projected without a hard assembly dependency.
internal static class PublicAppearanceData
{
    private static readonly Dictionary<(Type Type, string Name), MemberInfo?> members = [];
    internal static object? Get(object value, string name)
    {
        var key = (value.GetType(), name);
        if (!members.TryGetValue(key, out MemberInfo? member))
            members[key] = member = (MemberInfo?)key.Item1.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
                ?? key.Item1.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        return member switch { PropertyInfo p => p.GetValue(value), FieldInfo f => f.GetValue(value), _ => null };
    }
    internal static string? String(object value, string name) => Get(value, name) as string;
    internal static bool Bool(object value, string name) => Get(value, name) is true;
    internal static int Int(object value, string name, int fallback) => Get(value, name) is int number ? number : fallback;
    internal static float Float(object value, string name, float fallback) => Get(value, name) is float number ? number : fallback;
}

internal sealed class ScaleUpSprites(IModRegistry registry)
{
    internal const string ModId = "Arborsm.ScaleUpUnofficial";
    private PropertyInfo? index;
    private bool checkedIndex;

    internal DetailedSprite? Get(string? textureName)
    {
        if (textureName is null || !registry.IsLoaded(ModId)) return null;
        if (!checkedIndex)
        {
            checkedIndex = true;
            // This is the public index used by the framework's own Draw hook.
            index = Type.GetType("ScaleUpUnofficial.ScaleUpMod, ScaleUpUnofficial", throwOnError: false)
                ?.GetProperty("ScalesByAsset", BindingFlags.Public | BindingFlags.Static);
        }
        if (index?.GetValue(null) is not IDictionary data || !data.Contains(textureName) || data[textureName] is not { } entry
            || PublicAppearanceData.Get(entry, "Sprite") is not { } sprite) return null;
        return new(PublicAppearanceData.Int(sprite, "SpriteOriginX", 32),
            PublicAppearanceData.Int(sprite, "SpriteOriginY", PublicAppearanceData.Bool(sprite, "IsSmallSprite") ? 78 : 112));
    }
}
