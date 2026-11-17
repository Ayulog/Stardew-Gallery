using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewGallery.Appearance;

namespace StardewGallery;

internal sealed class GalleryApplication(IModHelper helper, IMonitor monitor, GalleryCatalogCache catalog,
    GalleryPhotos photos, ReplayService replay, Func<bool> isUnlocked, Action toggleUnlock, ICharacterAppearance appearance,
    Func<ResolvedEvent, EventSourceDisplay> sourceDetails, EventNameStore names, FavoriteEventStore? favorites = null) : IGalleryNavigation
{
    private readonly GalleryPageHistory history = new();
    private GalleryViewContext? view;
    private GalleryTextures? textures;
    private Action? externalReturn;
    private static PinyinTable? pinyin;
    private static bool pinyinFailed;

    internal static bool OwnsMenu(IClickableMenu? menu) => menu is GalleryMenu or GalleryCharacterMenu or GalleryQueryMenu or GalleryMomentsMenu or GalleryEventDetailMenu or GalleryPrerequisiteMenu or GalleryPhotoMenu or GalleryToolMenu;

    internal void Open()
    {
        try
        {
            externalReturn = null;
            PrepareSession();
            history.Reset();
            ShowCurrent();
            Game1.playSound("bigSelect");
        }
        catch (Exception error) { monitor.Log($"Gallery open failed: {error}", LogLevel.Error); }
    }

    internal void OpenFromSocialPage(string? villagerName, Action restoreSocialPage)
    {
        try
        {
            PrepareSession();
            externalReturn = restoreSocialPage;
            string? canonical = villagerName is { Length: > 0 } ? view!.Characters.Key(villagerName, null) ?? villagerName : null;
            GalleryCharacter? target = canonical is null
                ? null
                : view!.Catalog.Characters.FirstOrDefault(character => character.Name == canonical);
            if (target is not null && (target.IsMet || isUnlocked()))
                history.ResetTo(new(GalleryPage.Album) { CharacterName = target.Name, OriginTab = GalleryBookTab.Villagers });
            else
                history.Reset();
            ShowCurrent();
            Game1.playSound("bigSelect");
        }
        catch (Exception error) { monitor.Log($"Gallery open from SocialPage failed: {error}", LogLevel.Error); }
    }

    private void PrepareSession()
    {
        GalleryCatalog snapshot = catalog.Get();
        if (textures is null)
        {
            textures = new(Load("assets/GalleryHome.png"), Load(GalleryUiAssets.EventAlbum), Load(GalleryUiAssets.EventDetail),
                Load("assets/CharacterScene-day-v2.png"), Load(EventThumbnailAsset.For()), Load(GalleryUiAssets.ReplayGlyph),
                Load(GalleryUiAssets.EventSlotFrame), Load(GalleryUiAssets.ScrollbarTrack), Load(GalleryUiAssets.ConditionStatusIcons));
            if (!GalleryUiSkin.IsLoaded)
                GalleryUiSkin.Load(Load, warning => monitor.Log(warning, LogLevel.Warn));
        }
        AdoptFormerEntries(snapshot);
        view = new(snapshot, helper.Translation, textures, photos, this, isUnlocked, replay.Access)
            { Names = names, Favorites = favorites, Appearance = appearance, EventSource = sourceDetails, Pinyin = LoadPinyin() };
    }
    private Texture2D Load(string path) => helper.ModContent.Load<Texture2D>(path);

    /// <summary>The pinyin table, read once and only for Chinese. Without it, searches still match the text itself.</summary>
    private PinyinTable? LoadPinyin()
    {
        if (LocalizedContentManager.CurrentLanguageCode != LocalizedContentManager.LanguageCode.zh || pinyinFailed)
            return null;
        try
        {
            return pinyin ??= PinyinTable.Parse(File.ReadLines(Path.Combine(helper.DirectoryPath, "assets", "pinyin.txt")));
        }
        catch (Exception error)
        {
            // A missing or damaged table must not keep the gallery from opening.
            pinyinFailed = true;
            monitor.Log($"Pinyin search is off because assets/pinyin.txt could not be read: {error.Message}", LogLevel.Warn);
            return null;
        }
    }

    /// <summary>Carry photos and names from entries that 2.10.0 merged into another identity; best effort, never blocks opening.</summary>
    private void AdoptFormerEntries(GalleryCatalog snapshot)
    {
        foreach ((EventIdentity current, EventIdentity former) in NativeGlobalEventCopies.FormerTrailerCopies(snapshot))
        {
            try
            {
                photos.Adopt(current, former);
                names.Adopt(current, former);
                favorites?.Adopt(current, former);
            }
            catch (Exception error)
            {
                monitor.LogOnce($"Photos or name of {former} could not be moved to {current}; they stay where they are: {error.Message}", LogLevel.Warn);
            }
        }
    }
    internal void Reset() { externalReturn = null; view = null; history.Reset(); }

    public void SwitchTab(GalleryBookTab tab)
    {
        if (view is null) return;
        ReleaseInput();
        history.SwitchTab(tab);
        ShowCurrent();
    }

    public void OpenMoments() => Navigate(new(GalleryPage.Moments) { OriginTab = GalleryBookTab.Moments });

    public void OpenCharacter(string name)
    {
        if (view?.Catalog.Characters.FirstOrDefault(character => character.Name == name) is not { } character
            || !character.IsMet && !isUnlocked()) return;
        Navigate(new(GalleryPage.Album) { CharacterName = name, OriginTab = GalleryBookTab.Villagers });
    }
    public void OpenQuery(string query = "") => Navigate(new(GalleryPage.Query) { SearchText = query, OriginTab = GalleryBookTab.StoryIndex });
    public void OpenPrerequisite(string id)
    {
        if (view?.Catalog.FindPrerequisite(id) is null) return;
        Navigate(new(GalleryPage.Prerequisite) { PrerequisiteId = id });
    }
    public void OpenFilters()
    {
        if (history.Current is not { Page: GalleryPage.Query } query) return;
        Navigate(new(GalleryPage.Filters) { Filter = query.Filter });
    }
    public void ApplyFilters(QueryFilter filter)
    {
        if (history.ApplyFilters(filter)) ShowCurrent();
    }
    public void Rename(EventIdentity identity)
    {
        if (view?.Catalog.Find(identity) is not { Kind: not StoryKind.Internal } && view?.Catalog.FindPrerequisite(identity.EventId)?.Identity != identity) return;
        Navigate(new(GalleryPage.Rename) { Event = identity });
    }
    public void OpenEvent(EventIdentity identity)
    {
        if (view?.Catalog.Find(identity) is not { Kind: not StoryKind.Internal }) return;
        GalleryBookTab tab = history.Current?.Page switch
        {
            GalleryPage.Query => GalleryBookTab.StoryIndex,
            GalleryPage.Moments => GalleryBookTab.Moments,
            GalleryPage.Detail => history.Current.OriginTab,
            _ => GalleryBookTab.Villagers
        };
        Navigate(new(GalleryPage.Detail) { Event = identity, CharacterName = history.Current?.CharacterName, OriginTab = tab });
    }
    public void OpenPhotos(EventIdentity identity)
    {
        if (!WeddingMemento.IsWeddingIdentity(identity) && view?.Catalog.Find(identity) is not { Kind: not StoryKind.Internal }) return;
        Navigate(new(GalleryPage.Photos) { Event = identity });
    }
    public void Replay(EventIdentity identity, ReplayForkOption? forkOverride = null)
    {
        if (WeddingMemento.IsWeddingIdentity(identity)) return;
        if (view?.Catalog.Find(identity) is not { } entry) return;
        ReleaseInput(); replay.Request(entry, ShowCurrent, forkOverride);
    }
    public void ToggleUnlock() => toggleUnlock();
    public void Back()
    {
        ReleaseInput();
        if (history.Back())
            ShowCurrent();
        else if (externalReturn is { } restore)
        {
            externalReturn = null;
            restore();
        }
        else
            Close();
    }
    public void Close() { externalReturn = null; ReleaseInput(); Game1.activeClickableMenu = null; }
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
        if (page.Page == GalleryPage.Album && !view.Catalog.Characters.Any(character => character.Name == page.CharacterName)
            || page.Page == GalleryPage.Prerequisite && view.Catalog.FindPrerequisite(page.PrerequisiteId!) is null) { Back(); return; }
        // An already-open name editor may finish or cancel after its event leaves the current catalog.
        // The catalog still governs every content page and replay entry point.
        if (page.Page != GalleryPage.Rename
            && !(page.Page == GalleryPage.Photos && page.Event is { } photoId && WeddingMemento.IsWeddingIdentity(photoId))
            && page.Event is { } identity && view.Catalog.Find(identity) is not { Kind: not StoryKind.Internal }) { Back(); return; }
        Game1.activeClickableMenu = page.Page switch
        {
            GalleryPage.Home => new GalleryMenu(view, page),
            GalleryPage.Album => new GalleryCharacterMenu(view, page),
            GalleryPage.Query => new GalleryQueryMenu(view, page),
            GalleryPage.Moments => new GalleryMomentsMenu(view, page),
            GalleryPage.Detail => new GalleryEventDetailMenu(view, page, GalleryConditionPresentation.Build(view.Catalog.Find(page.Event!.Value)!, helper.Translation)),
            GalleryPage.Photos => new GalleryPhotoMenu(view, page),
            GalleryPage.Filters => new GalleryFilterMenu(view, page),
            GalleryPage.Prerequisite => new GalleryPrerequisiteMenu(view, page),
            GalleryPage.Rename => new GalleryRenameMenu(view, page),
            _ => null
        };
    }
}
