using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.GameData.Characters;

namespace StardewGallery;

internal sealed class GalleryCatalogCache(IMonitor monitor, Func<bool> debugDiagnostics)
{
    private sealed record IdentityConflict(string Location, string EventId, string SelectedKey, IReadOnlyList<string> CandidateKeys);
    private sealed record CacheSnapshot(ResolvedEventIndex ResolvedEvents, GalleryCatalog Gallery);

    private readonly IEventAssetSourceCatalog eventAssets = new EventAssetCatalog();
    private readonly ResolvedEventReader eventReader = new(
        (key, script) => GameLocation.IsValidLocationEvent(key, script),
        raw => Event.ParseCommands(raw),
        command => ArgUtility.SplitBySpaceQuoteAware(command),
        translationKey => Game1.content.LoadStringReturnNullIfNotFound(translationKey)
    );
    private readonly GalleryCatalogBuilder galleryBuilder = new(
        key => Event.SplitPreconditions(key),
        raw => Event.ParseCommands(raw),
        command => ArgUtility.SplitBySpaceQuoteAware(command),
        positions => ArgUtility.SplitBySpace(positions),
        () => Game1.player.spouse
    );
    private CacheSnapshot? cache;

    internal void Invalidate() => cache = null;

    internal GalleryCatalog Get()
    {
        if (cache is not null)
            return cache.Gallery;

        IReadOnlyList<GalleryCharacter> characters = ScanCharacters();
        ResolvedEventIndex index = ResolvedEventIndex.ReadCurrent(eventAssets, eventReader);
        GalleryCatalogBuildResult build = galleryBuilder.Build(characters, index.CurrentEvents);
        IReadOnlyList<GalleryEvent> events = build.AnalyzedEvents;
        GalleryCatalog catalog = build.Catalog;
        List<IdentityConflict> conflicts = index.Groups
            .Where(group => group.Candidates.Count > 1)
            .Select(group => new IdentityConflict(
                group.Current.LocationName,
                group.Current.EventId,
                group.Current.RawEventKey,
                group.Candidates.Select(candidate => candidate.RawEventKey).ToList()
            ))
            .ToList();

        monitor.Log(
            $"画廊扫描完成：角色候选 {characters.Count}，正式角色 {catalog.Characters.Count}，当前事件 {events.Count}，正式收录 {catalog.Events.Count}，直接归属 {events.Count(entry => entry.Ownership.Kind == OwnershipKind.Direct)}，前置继承 {events.Count(entry => entry.Ownership.Kind == OwnershipKind.Inherited)}，对白推定 {events.Count(entry => entry.Ownership.Kind == OwnershipKind.Inferred)}，排除 {events.Count(entry => entry.Ownership.Kind == OwnershipKind.Excluded)}。",
            LogLevel.Info
        );
        CacheSnapshot snapshot = new(index, catalog);
        cache = snapshot;
        if (debugDiagnostics())
        {
            GalleryDiagnostics.Write("catalog-latest.json", new
            {
                Timestamp = DateTimeOffset.Now,
                Summary = new
                {
                    CharacterCandidates = characters.Count,
                    GalleryCharacters = catalog.Characters.Count,
                    CurrentEvents = events.Count,
                    IncludedEvents = catalog.Events.Count,
                    ExcludedEvents = catalog.ExcludedEvents.Count,
                    IdentityConflicts = conflicts.Count,
                    MissingFragments = events.Count(entry => entry.Fragments.MissingKeys.Count > 0)
                },
                Conflicts = conflicts,
                MissingFragments = events.Where(entry => entry.Fragments.MissingKeys.Count > 0).Select(entry => new
                {
                    entry.LocationName,
                    entry.EventId,
                    entry.Fragments.MissingKeys
                }),
                Catalog = catalog
            }, monitor);
            monitor.Log($"详细扫描诊断已写入 {Path.Combine(GalleryDiagnostics.DirectoryPath, "catalog-latest.json")}。", LogLevel.Info);
        }
        return snapshot.Gallery;
    }

    private IReadOnlyList<GalleryCharacter> ScanCharacters()
    {
        Dictionary<string, NPC> found = new(StringComparer.Ordinal);
        HashSet<string> nonSocial = new(StringComparer.Ordinal);

        Utility.ForEachCharacter(npc =>
        {
            if (npc is Child)
            {
                found.TryAdd(npc.Name + "$$child", npc);
            }
            else if (npc.IsVillager)
            {
                if (!npc.CanSocialize)
                    nonSocial.Add(npc.Name);
                else if (!found.TryAdd(npc.Name, npc) && found[npc.Name] != npc)
                    monitor.Log($"发现重名社交角色 {npc.Name}；与原版社交页一致，仅保留第一个。", LogLevel.Warn);
            }
            return true;
        });

        Event? currentEvent = Game1.currentLocation?.currentEvent;
        if (currentEvent is not null)
        {
            foreach (NPC actor in currentEvent.actors)
            {
                if (actor.IsVillager && actor.CanSocialize)
                    found[actor.Name] = actor;
            }
        }

        foreach (string name in Game1.player.friendshipData.Keys)
        {
            if (nonSocial.Contains(name) || found.ContainsKey(name) || !HasSocialAssets(name))
                continue;

            found[name] = null!;
        }

        List<GalleryCharacter> result = [];
        foreach ((string key, NPC? npc) in found)
        {
            string name = npc is Child ? npc.Name : key;
            bool hasFriendship = Game1.player.friendshipData.TryGetValue(name, out Friendship? friendship);

            if (npc is Child)
            {
                result.Add(new GalleryCharacter(name, npc.displayName, true, friendship?.Points ?? 0));
                continue;
            }

            if (!NPC.TryGetData(name, out CharacterData? data))
                continue;

            string displayName = npc?.displayName ?? NPC.GetDisplayName(name);
            switch (data.SocialTab)
            {
                case SocialTabBehavior.HiddenUntilMet when !hasFriendship:
                case SocialTabBehavior.HiddenAlways:
                    continue;
                case SocialTabBehavior.UnknownUntilMet when !hasFriendship:
                    break;
            }

            result.Add(new GalleryCharacter(name, displayName, hasFriendship || data.SocialTab == SocialTabBehavior.AlwaysShown, friendship?.Points ?? 0));
        }

        return result;
    }

    private static bool HasSocialAssets(string name)
    {
        if (!NPC.TryGetData(name, out _) || !NPC.CanSocializePerData(name, Game1.getLocationFromName("Town")))
            return false;

        string textureName = NPC.getTextureNameForCharacter(name);
        return Game1.content.DoesAssetExist<Texture2D>($"Characters\\{textureName}")
            && Game1.content.DoesAssetExist<Texture2D>($"Portraits\\{textureName}");
    }
}
