using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace StardewGallery.Appearance;

internal sealed record PortraitureImage(Texture2D Texture, PortraitFrame Frame);

internal sealed class PortraiturePortraits
{
    private sealed record Access(bool Supported, MethodInfo? Tick);
    private readonly Dictionary<Type, Access> accessors = [];

    /// <summary>Whether a texture is one of Portraiture's own types, which only Portraiture can draw correctly.</summary>
    internal bool IsPortraiture(Texture2D source) => Accessor(source.GetType()).Supported;

    internal PortraitureImage? Read(Texture2D source)
    {
        Access access = Accessor(source.GetType());
        if (!access.Supported) return null;

        // Portraiture guards Tick by its own update counter, including shared portraits.
        access.Tick?.Invoke(source, null);
        if (PublicAppearanceData.Get(source, "STexture") is not Texture2D texture || texture.IsDisposed
            || ReferenceEquals(source, texture) || texture.GetType() != typeof(Texture2D))
            throw new InvalidOperationException("Portraiture did not provide a usable backing texture.");

        Rectangle? forced = PublicAppearanceData.Get(source, "ForcedSourceRectangle") as Rectangle?;
        float scale = PublicAppearanceData.Float(source, "Scale", float.NaN);
        var frame = PortraitureRegion.Create(scale, Math.Min(64, source.Width), Math.Min(64, source.Height),
            forced is Rectangle r ? (r.X, r.Y, r.Width, r.Height) : null);
        if (frame is null || !frame.TryRegion(texture.Width, texture.Height, out _))
            throw new InvalidOperationException("Portraiture portrait frame is outside its backing texture.");

        // Drawing the real image keeps gallery placement independent of Above Box mode.
        return new(texture, frame);
    }

    private Access Accessor(Type type)
    {
        if (accessors.TryGetValue(type, out var access)) return access;
        bool scaled = false, animated = false;
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            if (current.Assembly.GetName().Name != "Portraiture") continue;
            scaled |= current.FullName == "Portraiture.ScaledTexture2D";
            animated |= current.FullName == "Portraiture.AnimatedTexture2D";
        }
        return accessors[type] = new(scaled, animated
            ? type.GetMethod("Tick", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes) : null);
    }
}
