using StardewGallery;

internal static class QueryUpgradeChecks
{
    internal static void Run()
    {
        var parser = new ConditionParser(key => key.Split('/'), text => text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        GalleryEvent Story(string id, string npc, string location, string key) => new(new(new("Data/Events/" + location, id), location, key,
            "none/0 0/farmer 1 1 2/speak Sam hello/end", new([], []), "d", "s"), new(OwnershipKind.Direct, [new(npc, 500)]))
            { Kind = StoryKind.Heart, RelatedNpcNames = [npc] };
        var sam = Story("1", "Sam", "Town", "1/e 2111294/f Sam 500/Season summer");
        var mountain = Story("2", "Abigail", "Mountain", "2/f Abigail 1000");
        var catalog = new GalleryCatalog([], [sam, mountain], []);
        PrerequisiteRule[] rules =
        [
            new("invite", ["DayEnding"], "PLAYER_HEARTS Current Emily 8", false, true, ["AddMail Current EmilyCamping", "MarkEventSeen Current 2111294"]),
            new("cancel", ["DayStarted"], "", false, false, ["MarkEventSeen 1 marker false", "MarkEventSeen All marker", "MarkEventSeen Any invalid"]),
            new("existing", ["Manual"], "", false, false, ["MarkEventSeen Current 1"])
        ];
        var prereqs = PrerequisiteCatalog.Build(rules, catalog, text => text.Split(' '), parser, text => text.Split('/'));
        Check(prereqs.Count == 2 && prereqs.Single(p => p.EventId == "2111294").Dependents.Single() == sam, "references join to real marker rules");
        var marker = prereqs.Single(p => p.EventId == "marker");
        Check(marker.Sources.Count == 2 && !marker.Sources[0].SetsMarker && marker.Sources[0].Player == "Host"
            && marker.Sources[1].SetsMarker, "cancel and set retain order and target; invalid selectors excluded");
        Check(prereqs.All(p => p.EventId != "1"), "existing story is not duplicated as marker-only entry");
        var repeated = PrerequisiteCatalog.Build([new("same", ["DayEnding"], "", false, false, []),
            new("SAME", ["DayEnding"], "", false, false, ["MarkEventSeen Current actual"])], catalog, text => text.Split(' '), parser, text => text.Split('/'));
        Check(repeated.Single().EventId == "actual", "empty rule does not consume a duplicate ID");
        var keyGate = Story("55134261", "Lance", "FarmHouse", "55134261/i Golden_Key") with { Kind = StoryKind.Internal, ClassificationReason = "internal-offscreen-marker" };
        var keyStory = Story("55134259", "Lance", "Cavern", "55134259/e 55134261");
        var stepCatalog = new GalleryCatalog([], [keyStory], [keyGate, keyGate with { Resolved = keyGate.Resolved with { Identity = new("Data/Events/Town", "unreferenced") } }]);
        var steps = PrerequisiteCatalog.Build([], stepCatalog, text => text.Split(' '), parser, text => text.Split('/'));
        Check(steps.Single().EventId == "55134261" && steps[0].Dependents.Single() == keyStory, "referenced item gate records its own seen ID; unrelated internal flow stays hidden");
        var unseenGate = Story("900", "Abigail", "Town", "900/n letter") with { Kind = StoryKind.Internal, ClassificationReason = "internal-offscreen-marker" };
        var unseenStory = Story("901", "Abigail", "Town", "901/k 900/f Abigail 1000");
        var marked = PrerequisiteCatalog.Build([new("marks", ["DayStarted"], "", false, false, ["MarkEventSeen Current 900"])],
            new GalleryCatalog([], [unseenStory], [unseenGate]), text => text.Split(' '), parser, text => text.Split('/'));
        Check(PrerequisiteCatalog.Build([], new GalleryCatalog([], [unseenStory], [unseenGate]), text => text.Split(' '), parser, text => text.Split('/')).Count == 0
            && marked.Single().Dependents.Count == 0, "a story requiring an unseen event doesn't make it a prerequisite or a dependent of it");
        catalog = catalog with { Prerequisites = prereqs };
        Check(StoryDependencyLookup.Find(catalog, "2111294").Prerequisite is not null && !StoryDependencyLookup.Find(catalog, "2111294").Missing, "missing prerequisite now resolves");
        var index = new StorySearchIndex(catalog, e => e.LocationName == "Mountain" ? "山区" : "小镇", n => n == "Sam" ? "山姆" : "阿比盖尔",
            identity => identity.EventId == "1" ? "雨天约会" : null, completed: new HashSet<string> { "1", "2111294" });
        Check(index.Search("山", new QueryFilter { Kind = QueryKind.Heart, Npc = "Sam" }).Single().Event == sam, "exact NPC avoids location substring collisions");
        Check(index.Search("山", new QueryFilter { Location = "Data/Events/Mountain" }).Single().Event == mountain, "exact location avoids NPC substring collisions");
        Check(index.Search("雨天约会").Single().EventId == "1" && index.Search("1").First().EventId == "1", "alias and original ID searchable, exact ID first");
        Check(index.Search("", new QueryFilter { Kind = QueryKind.Prerequisite, Completion = QueryCompletion.Complete }).Single().EventId == "2111294", "marker completion uses seen state");
        Check(index.Search("2111294").Single().Prerequisite is not null && index.Search("").Count == 4, "all events includes stories and prerequisites");
        foreach (string query in new[] { "PLAYER_HEARTS Current Emily 8", "PLAYER_FRIENDSHIP_POINTS Current Emily 2502", "PLAYER_NPC_RELATIONSHIP Current Emily Dating Engaged Married",
            "PLAYER_HAS_SEEN_EVENT Current 2123243", "WEATHER Woods Sun", "PLAYER_HAS_MAIL Current letter Received", "!PLAYER_HAS_CONVERSATION_TOPIC Current ElliottGone1" })
        {
            Check(SafeGameQuery.TryParse(query, out var clauses), "known prerequisite query accepted");
            var context = new ConditionEvaluationContext(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null)
                { Details = new() { QueryFacts = clauses.ToDictionary(SafeGameQuery.FactKey, _ => (bool?)true) } };
            Check(SafeGameQuery.Evaluate(clauses, context) == !query.StartsWith('!'), "query fact negation applied once");
            Check(SafeGameQuery.Evaluate(clauses, context with { Details = new() }) is null, "missing facts remain unknown");
        }
        Check(!SafeGameQuery.TryParse("CustomAuthor.UnsafeQuery foo", out _), "unknown third-party query is not invoked");
        Check(Enum.GetValues<QueryKind>().Length == 4 && new QueryFilter().Kind == QueryKind.All, "one all-events option includes every query category");
        Check(GalleryNameText.IsMissing("(no translation:Name.JunimoJade)")
            && GalleryNameText.IsMissing("{{i18n:missing}}"), "translation diagnostics are not player-facing names");
        Check(GalleryLocationName.Resolve("Custom_TestRoom", "(no translation:TestRoom.Name)", _ => "测试房间") == "测试房间", "missing display name uses translation fallback");
        Check(GalleryLocationName.Resolve("Custom_TestRoom", "(no translation:TestRoom.Name)", _ => null) == "Test Room", "missing locale still has a readable fallback");
        var clones = sam with { RelatedNpcNames = ["Sam", "Sam·", "SpriteOnly"] };
        var otherMap = clones with { Resolved = clones.Resolved with { Identity = new("Data/Events/CopyTown", "1") } };
        var grouped = new StorySearchIndex(new([], [clones, otherMap], []), _ => "小镇", _ => "山姆",
            characterKey: (_, id) => id.StartsWith("Sam") ? "Sam" : null, locationKey: _ => "Data/Events/Town");
        Check(grouped.Rows.All(row => row.Npcs.SequenceEqual(["Sam"]) && row.Characters == "山姆"), "actor projection removes sprite-only choices and duplicate aliases");
        Check(grouped.Search("", new QueryFilter { Location = "Data/Events/Town" }).Count == 2
            && grouped.Rows.Select(row => row.Identity).Distinct().Count() == 2, "one location filter retains independent underlying event identities");
        var sameName = new StorySearchIndex(new([], [clones, otherMap], []), _ => "同名地点", _ => "Sam");
        string groupKey = sameName.Rows[0].LocationKey;
        Check(sameName.Rows.Select(row => row.LocationKey).Distinct().Count() == 1
            && sameName.Search("", new QueryFilter { Location = groupKey }).Count == 2, "identical location labels form one selectable group across assets");
        Check(sameName.Search("", location: otherMap.AssetName).Single().Event == otherMap, "explicit asset lookup stays exact after display grouping");
        var farmhouse = new Dictionary<string, string> { ["558291/y 3/H"] = "grandpa scene", ["558292/e 558291"] = "re-evaluation" };
        Check(NativeGlobalEventCopies.IsCopy("Data/Events/Cellar8", "558291/y 3/H", "grandpa scene", farmhouse), "native global grandpa copy is not a cellar story");
        Check(!NativeGlobalEventCopies.IsCopy("Data\\Events\\FarmHouse", "558291/y 3/H", "grandpa scene", farmhouse), "original farmhouse global event retained");
        Check(!NativeGlobalEventCopies.IsCopy("Data/Events/Town", "558291/y 3/H", "modded scene", farmhouse)
            && !NativeGlobalEventCopies.IsCopy("Data/Events/Town", "558291/other condition", "grandpa scene", farmhouse)
            && !NativeGlobalEventCopies.IsCopy("Data/Events/Town", "another/y 3/H", "grandpa scene", farmhouse), "different script, condition, and ID are not discarded");
        CheckTrailerMerge();
        CheckCanonicalCharacterFilter();
        Console.WriteLine("Query upgrade checks passed.");
    }

    /// <summary>GameLocation.TryGetLocationEvents (1.6.15) copies Trailer keys that Trailer_Big lacks into Trailer_Big.</summary>
    private static void CheckTrailerMerge()
    {
        var trailer = new Dictionary<string, string> { ["35/f Penny 500"] = "trailer 35", ["36/f Penny 1000"] = "trailer 36", ["own/f Pam 500"] = "trailer own" };
        var trailerBig = new Dictionary<string, string> { ["own/f Pam 500"] = "big own", ["big/f Pam 750"] = "big only" };
        IReadOnlySet<string> copies = NativeGlobalEventCopies.TrailerCopyKeys(trailer, trailerBig);
        Check(copies.SetEquals(["35/f Penny 500", "36/f Penny 1000"]), "Trailer keys that Trailer_Big defines itself are not merged copies");
        Check(NativeGlobalEventCopies.TrailerCopyKeys(trailer, null).Count == 3 && NativeGlobalEventCopies.TrailerCopyKeys(null, trailerBig).Count == 0,
            "without its own asset Trailer_Big copies every Trailer key");
        string key = "35/f Penny 500";
        Check(NativeGlobalEventCopies.PlaceTrailerEvent("Trailer", key, copies, false) == TrailerEventPlacement.Listed
            && NativeGlobalEventCopies.PlaceTrailerEvent("Trailer_Big", key, copies, false) == TrailerEventPlacement.Hidden,
            "before Pam's house upgrade a merged event is listed once, in Trailer");
        Check(NativeGlobalEventCopies.PlaceTrailerEvent("Trailer", key, copies, true) == TrailerEventPlacement.Hidden
            && NativeGlobalEventCopies.PlaceTrailerEvent("Trailer_Big", key, copies, true) == TrailerEventPlacement.ListedAsTrailer,
            "after the upgrade it plays from Trailer_Big under the Trailer identity");
        Check(new[] { false, true }.All(upgraded => new[] { "Trailer", "Trailer_Big", "Town" }.All(location =>
                NativeGlobalEventCopies.PlaceTrailerEvent(location, "own/f Pam 500", copies, upgraded) == TrailerEventPlacement.Listed
                && NativeGlobalEventCopies.PlaceTrailerEvent(location, "big/f Pam 750", copies, upgraded) == TrailerEventPlacement.Listed)),
            "events that are not merged copies stay where they are defined");

        const string script = "none|0 0|farmer 1 1 2|message hello|end";
        ResolvedEvent Event(string asset, string id) => new(new(asset, id), asset.Replace('\\', '/').Split('/').Last(), id + "/x", script, new([script], []), "definition", "script");
        string[] Split(string value) => value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        GalleryCatalogBuilder builder = new(value => value.Split('/'), value => value.Split('|'), Split, Split, () => null);
        GalleryCatalog catalog = builder.Build([], [Event("Data/Events/Trailer", "35"), Event("Data\\Events\\Trailer", "own"),
            Event("Data/Events/Trailer_Big", "own"), Event("Data/Events/Town", "35")]).Catalog;
        var former = NativeGlobalEventCopies.FormerTrailerCopies(catalog).ToArray();
        Check(former.Length == 1 && former[0].Current == new EventIdentity("Data/Events/Trailer", "35")
            && former[0].Former == new EventIdentity("Data/Events/Trailer_Big", "35"),
            "only a merged trailer event inherits its former Trailer_Big entry; a Trailer_Big event of its own keeps its data");
    }

    private static void CheckCanonicalCharacterFilter()
    {
        string[] Split(string value) => value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        GalleryCatalogBuilder builder = new(key => key.Split('/'), script => script.Split('|'), Split, Split, () => null);
        string[] keys = ["legacy/f Marlon 500", "hearts/G PLAYER_HEARTS Current Marlon 2",
            "points/G PLAYER_FRIENDSHIP_POINTS Current Marlon 500", "multiple/f Marlon 500 Sam 2000",
            "merged/f Marlon 250 MarlonFay 750", "hidden/f SpriteOnly 3000"];
        const string script = "none|0 0|farmer 1 1 2|message hello|end";
        ResolvedEvent[] sources = keys.Select(key => new ResolvedEvent(new("Data/Events/Town", key.Split('/')[0]), "Town", key,
            script, new([script], []), "definition", "script")).ToArray();
        GalleryCatalog catalog = builder.Build([], sources).Catalog;
        StorySearchIndex index = new(catalog, entry => entry.LocationName, name => name,
            characterKey: (entry, name) => entry?.AssetName == "Data/Events/Town"
                ? name switch { "Marlon" => "MarlonFay", "SpriteOnly" => null, _ => name } : name);
        Check(index.Search("", new QueryFilter { Npc = "MarlonFay" }).Count == 5,
            "canonical character filter includes every Marlon relationship");
        StorySearchRow legacy = index.Search("legacy").Single();
        Check(ReferenceEquals(legacy.Event!.Resolved, sources[0]) && legacy.Identity == new EventIdentity("Data/Events/Town", "legacy")
            && legacy.Event.EventKey == "legacy/f Marlon 500" && legacy.Event.Script == script,
            "canonical character projection leaves event identity, raw key and script intact");
    }

    private static void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); }
}
