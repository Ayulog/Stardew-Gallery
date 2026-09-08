using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace StardewGallery;

internal sealed class GalleryPhotos(IModHelper helper, IMonitor monitor, Action<string, bool> notify) : IDisposable
{
    private EventPhotoStore? store;
    private CaptureRequest? request;
    private readonly Dictionary<string, CachedTexture> thumbnails = [];
    private readonly Dictionary<string, CachedTexture> previews = [];
    private long cacheClock;
    private sealed record CaptureRequest(EventPhotoStore Store, EventIdentity Identity, Event Event);
    private sealed class CachedTexture(Texture2D? texture, long used)
    {
        internal Texture2D? Texture { get; } = texture;
        internal long Used { get; set; } = used;
    }

    internal bool Pending => request is not null;
    internal bool Ready => store is not null;

    internal void Load(SaveProfileKey profile)
    {
        Dispose();
        store = new EventPhotoStore(Path.Combine(helper.DirectoryPath, "event-photos"), profile, text => monitor.Log(text, LogLevel.Warn));
    }

    internal EventPhotoSet Read(EventIdentity identity) => store?.Read(identity) ?? new([], null, ReadOnly: true);

    internal void Request(EventIdentity identity, Event playing)
    {
        if (store is not null && request is null)
            request = new(store, identity, playing);
    }

    internal void Update(Event? playing)
    {
        if (request is not null && (!ReferenceEquals(request.Event, playing) || !ReferenceEquals(request.Store, store)))
            request = null;
    }

    internal void Capture(SpriteBatch batch, Event? playing)
    {
        CaptureRequest? capture = request;
        if (capture is null)
            return;
        request = null;
        if (!ReferenceEquals(capture.Event, playing) || !ReferenceEquals(capture.Store, store)
            || Game1.locationRequest is not null || Game1.fadeToBlackAlpha > 0 || Game1.globalFade)
        {
            notify("photo.unavailable", true);
            return;
        }
        try
        {
            RenderTargetBinding[] targets = batch.GraphicsDevice.GetRenderTargets();
            if (targets.Length != 1 || targets[0].RenderTarget is not RenderTarget2D target)
                throw new InvalidOperationException("No current world render target.");
            if (target.Width > 8192 || target.Height > 8192 || (long)target.Width * target.Height > 16_777_216)
                throw new InvalidOperationException("World render target exceeds the capture limit.");
            // Read only the completed world pass. HUD, dialogue and gallery controls are drawn later.
            Color[] pixels = new Color[target.Width * target.Height];
            target.GetData(pixels);
            using Texture2D original = new(batch.GraphicsDevice, target.Width, target.Height);
            original.SetData(pixels);
            using Texture2D thumbnail = Reduced(batch.GraphicsDevice, pixels, target.Width, target.Height, 512, 288);
            capture.Store.Add(capture.Identity, Encode(original), Encode(thumbnail));
            ClearTextures();
            notify("photo.captured", false);
            Game1.playSound("cameraNoise");
        }
        catch (Exception error)
        {
            monitor.Log($"Event screenshot failed: {error}", LogLevel.Error);
            notify("photo.failed", true);
        }
    }

    internal Texture2D? Cover(EventIdentity identity)
    {
        string? id = Read(identity).CoverId;
        return id is null ? null : Image(identity, id, preview: false);
    }

    internal Texture2D? Image(EventIdentity identity, string id, bool preview)
    {
        if (store is null)
            return null;
        string path = store.ImagePath(identity, id, thumbnail: !preview);
        Dictionary<string, CachedTexture> cache = preview ? previews : thumbnails;
        if (cache.TryGetValue(path, out CachedTexture? hit))
        {
            hit.Used = ++cacheClock;
            return hit.Texture;
        }
        Texture2D? texture = null;
        try
        {
            if (!File.Exists(store.ImagePath(identity, id)))
                throw new FileNotFoundException("Original photo is missing.");
            using FileStream file = File.OpenRead(path);
            EventPhotoStore.ValidatePng(file);
            file.Position = 0;
            using Texture2D source = Texture2D.FromStream(Game1.graphics.GraphicsDevice, file);
            Color[] pixels = new Color[source.Width * source.Height];
            source.GetData(pixels);
            texture = Reduced(source.GraphicsDevice, pixels, source.Width, source.Height, preview ? 1280 : 512, preview ? 720 : 288);
        }
        catch (Exception error)
        {
            monitor.Log($"Photo unavailable; using placeholder: {Path.GetFileName(path)}: {error.Message}", LogLevel.Warn);
        }
        cache[path] = new(texture, ++cacheClock);
        int limit = preview ? 2 : 24;
        if (cache.Count > limit)
        {
            var oldest = cache.MinBy(pair => pair.Value.Used);
            oldest.Value.Texture?.Dispose();
            cache.Remove(oldest.Key);
        }
        return texture;
    }

    internal void SetCover(EventIdentity identity, string? id)
    {
        try { (store ?? throw new InvalidOperationException("No save loaded.")).SetCover(identity, id); }
        finally { ClearTextures(); }
    }

    internal void Archive(EventIdentity identity, string id)
    {
        try { (store ?? throw new InvalidOperationException("No save loaded.")).Archive(identity, id); }
        finally { ClearTextures(); }
    }

    internal void ReportFailure(Exception error)
    {
        monitor.Log($"Photo operation failed: {error}", LogLevel.Error);
        notify("photo.failed", true);
    }

    internal static void DrawCover(SpriteBatch batch, Texture2D texture, Rectangle destination, Color tint)
    {
        var crop = PhotoImageLayout.Crop(texture.Width, texture.Height, destination.Width, destination.Height);
        batch.Draw(texture, destination, new Rectangle(crop.X, crop.Y, crop.Width, crop.Height), tint);
    }

    internal static void DrawContained(SpriteBatch batch, Texture2D texture, Rectangle destination)
    {
        float scale = Math.Min(destination.Width / (float)texture.Width, destination.Height / (float)texture.Height);
        int width = (int)Math.Round(texture.Width * scale), height = (int)Math.Round(texture.Height * scale);
        batch.Draw(texture, new Rectangle(destination.Center.X - width / 2, destination.Center.Y - height / 2, width, height), Color.White);
    }

    internal static Texture2D Reduced(GraphicsDevice device, Color[] pixels, int width, int height, int maxWidth, int maxHeight)
    {
        var size = PhotoImageLayout.Fit(width, height, maxWidth, maxHeight);
        Color[] reduced = new Color[size.Width * size.Height];
        for (int y = 0; y < size.Height; y++)
            for (int x = 0; x < size.Width; x++)
                reduced[y * size.Width + x] = pixels[(int)((long)y * height / size.Height) * width + (int)((long)x * width / size.Width)];
        Texture2D texture = new(device, size.Width, size.Height);
        texture.SetData(reduced);
        return texture;
    }

    private static byte[] Encode(Texture2D texture)
    {
        using MemoryStream file = new();
        texture.SaveAsPng(file, texture.Width, texture.Height);
        return file.ToArray();
    }

    internal void ReleasePreviews() => Clear(previews);
    internal void ClearTextures() { Clear(thumbnails); Clear(previews); }
    private static void Clear(Dictionary<string, CachedTexture> cache)
    {
        foreach (CachedTexture item in cache.Values)
            item.Texture?.Dispose();
        cache.Clear();
    }
    public void Dispose() { request = null; store = null; ClearTextures(); }
}
