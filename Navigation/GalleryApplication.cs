using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace StardewGallery;

internal sealed class GalleryApplication(IModHelper helper, IMonitor monitor, GalleryCatalogCache catalog,
    GalleryPhotos photos, ReplayService replay, Func<bool> isUnlocked, Action toggleUnlock) : IGalleryNavigation
{
    private readonly GalleryPageHistory history = new();
    private GalleryViewContext? view;
    private GalleryTextures? textures;

    internal static bool OwnsMenu(IClickableMenu? menu) => menu is GalleryMenu or GalleryCharacterMenu or GalleryQueryMenu or GalleryEventDetailMenu or GalleryPhotoMenu;

    internal void Open()
    {
        try
        {
            GalleryCatalog snapshot = catalog.Get();
            textures ??= new(Load("assets/GalleryHome.png"), Load(GalleryUiAssets.EventAlbum), Load(GalleryUiAssets.EventDetail),
                Load("assets/CharacterScene-day-v2.png"), Load(EventThumbnailAsset.For()), Load(GalleryUiAssets.ReplayGlyph),
                Load(GalleryUiAssets.EventSlotFrame), Load(GalleryUiAssets.ScrollbarTrack), Load(GalleryUiAssets.ConditionStatusIcons));
            view = new(snapshot, helper.Translation, textures, photos, this, isUnlocked, replay.Access);
            history.Reset(); ShowCurrent(); Game1.playSound("bigSelect");
        }
        catch (Exception error) { monitor.Log($"Gallery open failed: {error}", LogLevel.Error); }
    }
    private Texture2D Load(string path) => helper.ModContent.Load<Texture2D>(path);
    internal void Reset() { view = null; history.Reset(); }

    public void OpenCharacter(string name)
    {
        if (view?.Catalog.Characters.FirstOrDefault(character => character.Name == name) is not { } character
            || !character.IsMet && !isUnlocked()) return;
        Navigate(new(GalleryPage.Album) { CharacterName = name });
    }
    public void OpenQuery(string query = "") => Navigate(new(GalleryPage.Query) { SearchText = query });
    public void OpenEvent(EventIdentity identity)
    {
        if (view?.Catalog.Find(identity) is not { Kind: not StoryKind.Internal }) return;
        Navigate(new(GalleryPage.Detail) { Event = identity, CharacterName = history.Current?.CharacterName });
    }
    public void OpenPhotos(EventIdentity identity)
    {
        if (view?.Catalog.Find(identity) is not { Kind: not StoryKind.Internal }) return;
        Navigate(new(GalleryPage.Photos) { Event = identity });
    }
    public void Replay(EventIdentity identity)
    {
        if (view?.Catalog.Find(identity) is not { } entry) return;
        ReleaseInput(); replay.Request(entry, ShowCurrent);
    }
    public void ToggleUnlock() => toggleUnlock();
    public void Back()
    {
        ReleaseInput();
        if (history.Back()) ShowCurrent(); else Close();
    }
    public void Close() { ReleaseInput(); Game1.activeClickableMenu = null; }
    private static void ReleaseInput() => (Game1.activeClickableMenu as IGallerySearchMenu)?.DeselectSearch();
    private void Navigate(GalleryPageState page)
    {
        if (view is null) return;
        ReleaseInput();
        try { history.Open(page, ShowCurrent); }
        catch (Exception error)
        {
            monitor.Log($"Gallery page could not open: {page.Page}.\n{error}", LogLevel.Error);
            Game1.addHUDMessage(new HUDMessage(helper.Translation.Get("nav.unavailable"), HUDMessage.error_type));
        }
    }
    private void ShowCurrent()
    {
        if (view is null || history.Current is not { } page || !Context.IsWorldReady) return;
        if (page.Event is { } identity && view.Catalog.Find(identity) is not { Kind: not StoryKind.Internal }) { Back(); return; }
        Game1.activeClickableMenu = page.Page switch
        {
            GalleryPage.Home => new GalleryMenu(view, page),
            GalleryPage.Album => new GalleryCharacterMenu(view, page),
            GalleryPage.Query => new GalleryQueryMenu(view, page),
            GalleryPage.Detail => new GalleryEventDetailMenu(view, page, GalleryConditionPresentation.Build(view.Catalog.Find(page.Event!.Value)!, helper.Translation)),
            GalleryPage.Photos => new GalleryPhotoMenu(view, page),
            _ => null
        };
    }
}
