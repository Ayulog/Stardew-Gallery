using System.Diagnostics;
using System.Globalization;
using StardewGallery;

internal static class GallerySearchChecks
{
    internal static void Run()
    {
        CultureInfo savedCulture = CultureInfo.CurrentCulture;
        try
        {
            GalleryCatalog catalog = new(
                [new("Abigail", "\u963f\u6bd4\u76d6\u5c14", false, 0), new("Leah", "Leah", true, 500),
                 new("Zed", "\u5f20\u4e09", true, 0), new("I", "I", true, 0),
                 new("TwinA", "Same", true, 0), new("TwinB", "Same", false, 0)],
                [Event("Mod.Event.ABC", "Abigail", "Leah"), Event("123", "Zed"),
                 Event("wrong-owner-case", "abigail"), Event("no-owner")],
                [Event("excluded-only", "Abigail")]);
            string[] queries = ["", "  ", "\u963f\u6bd4", "abIGAiL", "mod.event", "ABC", "23", "not-found",
                "  Leah  ", "i", "I", "\u0131", "\u0130", "same", "wrong-owner-case", "excluded-only", "no-owner"];
            GallerySearchFilter filter = new();
            foreach (string culture in new[] { "en-US", "zh-CN", "tr-TR" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                foreach (bool chineseSort in new[] { false, true })
                {
                    List<GalleryCharacter> expected = [];
                    foreach (string query in queries)
                    {
                        LegacyUpdate(catalog, query, chineseSort, ref expected);
                        filter.Update(catalog, query, culture, chineseSort);
                        Check(filter.Results.SequenceEqual(expected), $"2.1.0 result parity: {culture}/{chineseSort}/{query}");
                    }
                }
            }

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            filter.Update(catalog, "  mod.event  ", "en", false);
            Check(filter.Results.Select(character => character.Name).ToHashSet().SetEquals(["Abigail", "Leah"]),
                "shared event matches both owners, including an unmet character");
            List<GalleryCharacter> previous = filter.Results;
            for (int frame = 0; frame < 300; frame++)
                Check(!filter.Update(catalog, "  mod.event  ", "en", false)
                    && ReferenceEquals(filter.Results, previous), "idle frames reuse results, including padded queries");

            Check(!filter.Update(catalog, "ABC", "en", false), "same character sequence keeps click components");
            Check(!ReferenceEquals(filter.Results, previous), "changed text is evaluated immediately");
            previous = filter.Results;
            GalleryCatalog sameValues = catalog with { };
            Check(sameValues == catalog && !ReferenceEquals(sameValues, catalog), "equal-valued snapshot fixture");
            filter.Update(sameValues, "ABC", "en", false);
            Check(!ReferenceEquals(previous, filter.Results), "new catalog instance invalidates even if values compare equal");
            GalleryCatalog newOwners = catalog with { Events = [Event("Mod.Event.ABC", "Zed")] };
            filter.Update(newOwners, "ABC", "en", false);
            Check(filter.Results is [{ Name: "Zed" }], "new catalog uses current event owners");
            GalleryCatalog newCharacters = newOwners with { Characters = [new("Zed", "Renamed", false, 250)] };
            filter.Update(newCharacters, "ABC", "en", false);
            Check(ReferenceEquals(filter.Results.Single(), newCharacters.Characters[0]), "new character snapshot replaces old data");

            previous = filter.Results;
            filter.Update(newCharacters, "ABC", "fr", false);
            Check(!ReferenceEquals(previous, filter.Results), "locale invalidates when sort language remains English");
            previous = filter.Results;
            filter.Update(newCharacters, "ABC", "fr", true);
            Check(!ReferenceEquals(previous, filter.Results), "sort comparison invalidates");

            GalleryCatalog cultureCatalog = new([new("Internal", "I", true, 0)], [], []);
            filter.Update(cultureCatalog, "\u0131", "en", false);
            Check(filter.Results.Count == 0, "English display-name comparison");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            filter.Update(cultureCatalog, "\u0131", "en", false);
            Check(filter.Results.Count == 1, "culture change invalidates display-name matching independently of locale");

            Check(filter.Update(catalog, "not-found", "en", false) && filter.Results.Count == 0, "no-results transition");
            Check(filter.Update(catalog, "", "en", false) && filter.Results.Count == catalog.Characters.Count, "clear restores all characters");
            filter.Update(new([], [], []), "", "en", false);
            Check(filter.Results.Count == 0, "empty catalog does not reuse previous matches");
            Console.WriteLine("Gallery search checks passed.");
        }
        finally
        {
            CultureInfo.CurrentCulture = savedCulture;
        }
    }

    internal static void Benchmark()
    {
        CultureInfo savedCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            const int frames = 300;
            GalleryCharacter[] characters = Enumerable.Range(0, 120)
                .Select(index => new GalleryCharacter($"NPC{index:D4}", $"Character {index:D4}", index % 2 == 0, 0)).ToArray();
            GalleryCatalog catalog = new(characters,
                Enumerable.Range(0, 1800).Select(index => Event((1000000 + index).ToString(CultureInfo.InvariantCulture),
                    characters[index % characters.Length].Name)).ToArray(), []);
            string[] typing = ["", "1", "10", "100", "1001", "10017", "100179", "1001799", "NPC0119", ""];
            Console.WriteLine($"Search benchmark: {characters.Length} NPCs, {catalog.Events.Count} events, {frames} calls; synthetic, not game FPS.");
            foreach (string scenario in new[] { "empty", "npc", "event-id", "missing", "typing" })
            {
                string Query(int frame) => scenario switch
                {
                    "npc" => "NPC0119",
                    "event-id" => "1001799",
                    "missing" => "no-match",
                    "typing" => typing[frame / 30],
                    _ => ""
                };
                List<GalleryCharacter> legacy = [];
                GallerySearchFilter warmup = new();
                for (int index = 0; index < 20; index++)
                {
                    LegacyUpdate(catalog, Query(index), false, ref legacy);
                    warmup.Update(catalog, Query(index), "en", false);
                }
                legacy = [];
                GallerySearchFilter current = new();
                int recomputations = 0;
                var before = Measure(frames, frame => LegacyUpdate(catalog, Query(frame), false, ref legacy));
                var after = Measure(frames, frame =>
                {
                    List<GalleryCharacter> previous = current.Results;
                    current.Update(catalog, Query(frame), "en", false);
                    if (!ReferenceEquals(previous, current.Results))
                        recomputations++;
                });
                Check(legacy.SequenceEqual(current.Results), "benchmark final result parity: " + scenario);
                Check(recomputations == (scenario == "typing" ? typing.Length : 1), "benchmark refresh count: " + scenario);
                Console.WriteLine(FormattableString.Invariant(
                    $"{scenario}: legacy={before.Milliseconds:F2}ms/{before.Bytes}B/{frames} refreshes; current={after.Milliseconds:F2}ms/{after.Bytes}B/{recomputations} refreshes"));
                string idleQuery = Query(frames - 1);
                var idle = Measure(frames, _ => current.Update(catalog, idleQuery, "en", false));
                Check(idle.Bytes == 0, "idle refresh must not allocate LINQ closures: " + scenario);
                Console.WriteLine(FormattableString.Invariant($"  warmed idle: {idle.Milliseconds:F2}ms/{idle.Bytes}B/{frames} calls"));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = savedCulture;
        }
    }

    private static (double Milliseconds, long Bytes) Measure(int count, Action<int> action)
    {
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        long start = Stopwatch.GetTimestamp();
        for (int index = 0; index < count; index++)
            action(index);
        return ((Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency, GC.GetAllocatedBytesForCurrentThread() - bytes);
    }

    // Frozen 2.1.0 RefreshFilter work, excluding only UI scrollbar/component updates.
    private static bool LegacyUpdate(GalleryCatalog catalog, string text, bool chineseSort, ref List<GalleryCharacter> results)
    {
        string previous = string.Join('\u001f', results.Select(character => character.Name));
        string query = text.Trim();
        results = catalog.Characters
            .Where(character => query.Length == 0
                || character.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || character.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || catalog.Events.Any(entry => entry.EventId.Contains(query, StringComparison.OrdinalIgnoreCase)
                    && entry.Ownership.Owners.Any(owner => owner.Name == character.Name)))
            .OrderBy(character => character.DisplayName, Comparer<string>.Create((left, right) =>
                CultureInfo.GetCultureInfo(chineseSort ? "zh-CN" : "en-US").CompareInfo.Compare(left, right,
                    CompareOptions.IgnoreCase | CompareOptions.IgnoreWidth)))
            .ToList();
        return previous != string.Join('\u001f', results.Select(character => character.Name));
    }

    private static GalleryEvent Event(string id, params string[] owners) => new(
        new(new("Data/Events/Town", id), "Town", id, "", new([], []), "", ""),
        new(OwnershipKind.Direct, owners.Select(owner => new EventOwner(owner, null)).ToArray()));

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Gallery search: " + message);
    }
}
