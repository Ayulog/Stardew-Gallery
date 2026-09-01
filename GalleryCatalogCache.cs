using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.GameData.Characters;

namespace StardewGallery;

internal sealed class GalleryCatalogCache(IMonitor monitor, Func<bool> debugDiagnostics)
{
    private sealed record IdentityConflict(string Location, string EventId, string SelectedKey, IReadOnlyList<string> CandidateKeys);
    private sealed record EventScanResult(IReadOnlyList<GalleryEvent> Events, IReadOnlyList<IdentityConflict> Conflicts);

    private GalleryCatalog? cache;

    internal void Invalidate() => cache = null;

    internal GalleryCatalog Get()
    {
        if (cache is not null)
            return cache;

        IReadOnlyList<GalleryCharacter> characters = ScanCharacters();
        EventScanResult scan = ScanEvents();
        IReadOnlyList<GalleryEvent> events = scan.Events;
        IReadOnlyDictionary<string, EventOwnership> ownership = OwnershipResolver.Resolve(
            events.Select(ParseEvidence).ToList(),
            characters.Select(character => character.Name).ToHashSet(StringComparer.Ordinal)
        );
        List<GalleryEvent> assignedEvents = events
            .Select(entry => entry with { Ownership = ownership[entry.Identity] })
            .Where(entry => entry.Ownership.Kind != OwnershipKind.Excluded)
            .ToList();
        HashSet<string> galleryNames = assignedEvents
            .SelectMany(entry => entry.Ownership.Owners)
            .Select(owner => owner.Name)
            .ToHashSet(StringComparer.Ordinal);
        List<GalleryCharacter> galleryCharacters = characters.Where(character => galleryNames.Contains(character.Name)).ToList();

        monitor.Log(
            $"画廊扫描完成：角色候选 {characters.Count}，正式角色 {galleryCharacters.Count}，当前事件 {events.Count}，正式收录 {assignedEvents.Count}，直接归属 {ownership.Values.Count(value => value.Kind == OwnershipKind.Direct)}，前置继承 {ownership.Values.Count(value => value.Kind == OwnershipKind.Inherited)}，对白推定 {ownership.Values.Count(value => value.Kind == OwnershipKind.Inferred)}，排除 {ownership.Values.Count(value => value.Kind == OwnershipKind.Excluded)}。",
            LogLevel.Info
        );
        cache = new GalleryCatalog(
            galleryCharacters,
            assignedEvents,
            events.Select(entry => entry with { Ownership = ownership[entry.Identity] })
                .Where(entry => entry.Ownership.Kind == OwnershipKind.Excluded)
                .ToList()
        );
        if (debugDiagnostics())
        {
            GalleryDiagnostics.Write("catalog-latest.json", new
            {
                Timestamp = DateTimeOffset.Now,
                Summary = new
                {
                    CharacterCandidates = characters.Count,
                    GalleryCharacters = galleryCharacters.Count,
                    CurrentEvents = events.Count,
                    IncludedEvents = assignedEvents.Count,
                    ExcludedEvents = cache.ExcludedEvents.Count,
                    IdentityConflicts = scan.Conflicts.Count,
                    MissingFragments = events.Count(entry => entry.Fragments.MissingKeys.Count > 0)
                },
                Conflicts = scan.Conflicts,
                MissingFragments = events.Where(entry => entry.Fragments.MissingKeys.Count > 0).Select(entry => new
                {
                    entry.LocationName,
                    entry.EventId,
                    entry.Fragments.MissingKeys
                }),
                Catalog = cache
            }, monitor);
            monitor.Log($"详细扫描诊断已写入 {Path.Combine(GalleryDiagnostics.DirectoryPath, "catalog-latest.json")}。", LogLevel.Info);
        }
        return cache;
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

    private EventScanResult ScanEvents()
    {
        Dictionary<string, List<(GalleryEvent Entry, GameLocation Location)>> candidates = new(StringComparer.Ordinal);

        Utility.ForEachLocation(location =>
        {
            if (!location.TryGetLocationEvents(out string assetName, out Dictionary<string, string> events))
                return true;

            foreach ((string key, string script) in events)
            {
                if (!GameLocation.IsValidLocationEvent(key, script) || !EventKey.TryGetId(key, out string id))
                    continue;
                if (EventKey.IsPlaceholderScript(script))
                    continue;

                string locationName = location.NameOrUniqueName;
                string identity = EventKey.GetIdentity(locationName, id);
                EventFragments fragments = EventFragmentCollector.Collect(
                    script,
                    location.Name,
                    name => LoadLocationEvents(name, location, events),
                    raw => Event.ParseCommands(raw),
                    command => ArgUtility.SplitBySpaceQuoteAware(command),
                    translationKey => Game1.content.LoadStringReturnNullIfNotFound(translationKey)
                );

                GalleryEvent entry = new(identity, locationName, assetName, id, key, script, fragments,
                    new EventOwnership(OwnershipKind.Excluded, [], "not-analyzed"));
                if (!candidates.TryGetValue(identity, out List<(GalleryEvent, GameLocation)>? matches))
                    candidates[identity] = matches = [];
                if (!matches.Any(match => match.Item1.EventKey == key && match.Item1.Script == script))
                    matches.Add((entry, location));
            }
            return true;
        }, includeInteriors: true, includeGenerated: false);

        List<GalleryEvent> result = [];
        List<IdentityConflict> conflicts = [];
        foreach (List<(GalleryEvent Entry, GameLocation Location)> matches in candidates.Values)
        {
            int selectedIndex = EventKey.SelectVariantIndex(matches.Count, index =>
            {
                try
                {
                    (GalleryEvent Entry, GameLocation Location) match = matches[index];
                    string value = match.Location.checkEventPrecondition(match.Entry.EventKey, check_seen: false);
                    return !string.IsNullOrEmpty(value) && value != "-1";
                }
                catch
                {
                    return false;
                }
            });
            (GalleryEvent Entry, GameLocation Location) selected = matches[selectedIndex];
            result.Add(selected.Entry);
            if (matches.Count > 1)
                conflicts.Add(new IdentityConflict(selected.Entry.LocationName, selected.Entry.EventId, selected.Entry.EventKey,
                    matches.Select(match => match.Entry.EventKey).ToList()));
        }

        return new EventScanResult(result, conflicts);
    }

    private static IReadOnlyDictionary<string, string>? LoadLocationEvents(
        string locationName,
        GameLocation rootLocation,
        IReadOnlyDictionary<string, string> rootEvents)
    {
        if (locationName.Equals(rootLocation.Name, StringComparison.OrdinalIgnoreCase)
            || locationName.Equals(rootLocation.NameOrUniqueName, StringComparison.OrdinalIgnoreCase))
            return rootEvents;

        GameLocation? location = Game1.getLocationFromName(locationName);
        string assetName = "Data\\Events\\" + (location?.Name ?? locationName);
        return Game1.content.DoesAssetExist<Dictionary<string, string>>(assetName)
            ? Game1.content.Load<Dictionary<string, string>>(assetName)
            : null;
    }

    private static EventEvidence ParseEvidence(GalleryEvent entry)
    {
        Dictionary<string, int> friendship = new(StringComparer.Ordinal);
        List<string> prerequisites = [];
        foreach (string condition in Event.SplitPreconditions(entry.EventKey).Skip(1))
        {
            string[] args = ArgUtility.SplitBySpaceQuoteAware(condition);
            if (args.Length == 0 || args[0].StartsWith('!'))
                continue;

            if (args[0] == "f" || args[0].Equals("Friendship", StringComparison.OrdinalIgnoreCase))
            {
                for (int i = 1; i + 1 < args.Length; i += 2)
                {
                    if (int.TryParse(args[i + 1], out int points))
                        friendship[args[i]] = Math.Max(friendship.GetValueOrDefault(args[i]), points);
                }
            }
            else if (args[0] == "e" || args[0].Equals("SawEvent", StringComparison.OrdinalIgnoreCase))
                prerequisites.AddRange(args.Skip(1));
        }

        string[] rootCommands = Event.ParseCommands(entry.Script);
        HashSet<string> actors = new(StringComparer.Ordinal);
        if (rootCommands.Length > 2)
        {
            string[] positions = ArgUtility.SplitBySpace(rootCommands[2]);
            for (int i = 0; i + 3 < positions.Length; i += 4)
            {
                string actor = positions[i] == "spouse" ? Game1.player.spouse : positions[i];
                if (!string.IsNullOrWhiteSpace(actor) && actor != "farmer" && actor != "otherFarmers")
                    actors.Add(actor);
            }
        }

        Dictionary<string, int> dialogue = new(StringComparer.Ordinal);
        foreach (string command in entry.Fragments.Scripts.SelectMany(script => Event.ParseCommands(script)).Skip(3))
        {
            string[] args = ArgUtility.SplitBySpaceQuoteAware(command);
            if (args.Length > 1 && args[0].Equals("speak", StringComparison.OrdinalIgnoreCase))
            {
                string speaker = args[1].TrimEnd('?');
                dialogue[speaker] = dialogue.GetValueOrDefault(speaker) + 1;
            }
        }

        return new EventEvidence(entry.Identity, entry.EventId, friendship, prerequisites, actors, dialogue);
    }
}
