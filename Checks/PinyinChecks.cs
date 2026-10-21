using StardewGallery;

internal static class PinyinChecks
{
    internal static void Run()
    {
        PinyinTable table = PinyinTable.Parse(File.ReadLines(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../assets/pinyin.txt"))));
        bool Finds(string text, string query) => PinyinTable.Spelling(query) is { } spelling && table.Matches(text, spelling);

        Check(Finds("阿比盖尔", "abge") && Finds("阿比盖尔", "abigaier") && Finds("阿比盖尔", "abig"), "initials, full and partial pinyin");
        Check(Finds("阿比盖尔", "ABGE") && Finds("阿比盖尔", "a bi gai'er") && Finds("阿比盖尔", "gai’er"), "case, spaces and apostrophes");
        Check(Finds("阿比盖尔", "gai") && Finds("阿比盖尔", "bga") && Finds("阿比盖尔", "abgaier"), "a match can start inside the text and mix forms");
        Check(!Finds("阿比盖尔", "age") && !Finds("阿比盖尔", "abgl") && !Finds("阿比盖尔", "abgex"), "characters must be consecutive and all letters used");
        Check(Finds("银行", "yh") && Finds("行走", "xz") && Finds("长大", "zd") && Finds("长大", "cd"), "polyphones match each common reading");
        // 女 is read only nü; 绿 is also read lu, so it can't show the u spelling on its own.
        Check(Finds("女巫", "nvwu") && Finds("女巫", "nuwu") && Finds("女巫", "n\u00FCwu") && Finds("绿雨", "lvyu"), "ü is v, u or ü");
        Check(Finds("阿比盖尔", "阿bge") && Finds("Leah 的家", "LEAH") && Finds("2心事件", "2xsj") && !Finds("2 心事件", "2xsj"),
            "other characters, spaces included, match themselves");
        Check(Finds("塞巴斯蒂安", "sbsda") && !Finds("塞巴斯蒂安", "zbsda") && !Finds("塞巴斯蒂安", "sbsta"), "readings come from the table");
        Check(PinyinTable.Spelling("123") is null && PinyinTable.Spelling("阿比") is null && PinyinTable.Spelling("   ") is null,
            "queries without Latin letters stay with the text match");
        Check(PinyinTable.Spelling(new string('a', 63)) is not null && PinyinTable.Spelling(new string('a', 64)) is null, "long queries are skipped");
        Check(!Finds("", "a") && Finds(new string('阿', 70), new string('a', 63)), "empty and long texts");
        Check(Throws(() => PinyinTable.Parse(["a"])) && Throws(() => PinyinTable.Parse(["a abc"])) && PinyinTable.Parse(["# note", "", "a 阿"]).Matches("阿", "a"),
            "malformed lines are rejected");

        GalleryCatalog catalog = new([new("Abigail", "阿比盖尔", true, 0), new("Zed", "张三", true, 0), new("Leah", "Leah", true, 0)], [], []);
        GallerySearchFilter filter = new();
        filter.Update(catalog, "zs", "zh", true);
        Check(filter.Results.Count == 0, "home search without a table matches text only");
        Check(filter.Update(catalog, "zs", "zh", true, table) && filter.Results.Single().Name == "Zed", "home search finds a display name by pinyin");
        Check(!filter.Update(catalog, "zs", "zh", true, table), "the table is part of the cached query");
        filter.Update(catalog, "a", "zh", true, table);
        Check(filter.Results.Select(character => character.Name).OrderBy(name => name, StringComparer.Ordinal).SequenceEqual(["Abigail", "Leah"]), "internal names and pinyin both match");

        GalleryEvent Entry(string asset, string id) => new(new(new(asset, id), "Beach", id, "scene", new(["scene"], []), "d", "s"), new(OwnershipKind.Excluded, []))
            { Kind = StoryKind.Heart, RelatedNpcNames = ["Abigail"] };
        GalleryCatalog events = new([], [Entry("Data/Events/Beach", "101"), Entry("Data/Events/Town", "102")], []);
        StorySearchIndex Index(PinyinTable? pinyin) => new(events, entry => entry.AssetName.EndsWith("Beach") ? "海边" : "小镇", _ => "阿比盖尔",
            identity => identity.EventId == "101" ? "夏日约会" : "雨中漫步", pinyin: pinyin);
        Check(Index(null).Search("hb").Count == 0, "query page without a table matches text only");
        StorySearchIndex index = Index(table);
        Check(index.Search("hb").Single().EventId == "101" && index.Search("xiaozhen").Single().EventId == "102", "locations by pinyin");
        Check(index.Search("xryh").Single().EventId == "101" && index.Search("yuzhong").Single().EventId == "102", "titles by pinyin");
        Check(index.Search("abge").Count == 2 && index.Search("101").Single().EventId == "101", "characters by pinyin; IDs unchanged");
        Console.WriteLine("Pinyin search checks passed.");
    }

    private static bool Throws(Action action)
    {
        try { action(); return false; }
        catch (FormatException) { return true; }
    }

    private static void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); }
}
