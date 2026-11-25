using StardewGallery;

/// <summary>
/// Pure in-memory Stage 4 checks covering whole-page book pagination, 4-season/day-night/weather scenic window palettes,
/// <c>Hearts/Stage · Location</c> card title formatting (without raw <c>EventId</c> leakage), <c>StorySearchIndex</c>
/// <c>EncounterOnly</c> filtering and <c>DisplayTitle</c>/pinyin search, and three-ribbon <c>GalleryPageHistory</c> navigation.
/// Zero disk temporary directories or file deletions.
/// </summary>
internal static class Stage4Checks
{
    private static int assertions;

    internal static void Run()
    {
        assertions = 0;
        CheckWholePagePagination();
        CheckScenicWindowPalettes();
        CheckScenicWindowRulesAndHobbyAnimations();
        CheckScenicMeadowStroll();
        CheckCardTitleFormatter();
        CheckStorySearchIndexEncounterAndDisplayTitles();
        CheckThreeRibbonNavigationAndSocialRoot();
        Console.WriteLine($"Stage 4 checks passed ({assertions} assertions).");
    }

    private static void CheckWholePagePagination()
    {
        Check(GalleryPagination.TotalPages(0, 6) == 1, "empty list still shows page 1 / 1");
        Check(GalleryPagination.ClampPage(5, 0, 6) == 0 && GalleryPagination.VisibleCount(0, 0, 6) == 0, "empty list clamps to page 0 with 0 visible");

        Check(GalleryPagination.TotalPages(6, 6) == 1 && GalleryPagination.TotalPages(7, 6) == 2 && GalleryPagination.TotalPages(18, 6) == 3,
            "6-per-page album/index total pages");
        Check(GalleryPagination.TotalPages(8, 8) == 1 && GalleryPagination.TotalPages(9, 8) == 2,
            "8-per-page moments total pages");
        Check(GalleryPagination.TotalPages(18, 18) == 1 && GalleryPagination.TotalPages(19, 18) == 2 && GalleryPagination.TotalPages(37, 18) == 3,
            "18-per-page villager overview total pages");

        Check(GalleryPagination.Turn(0, -1, 19, 18) == (0, false), "turning left on page 0 is a no-op");
        Check(GalleryPagination.Turn(0, 1, 19, 18) == (1, true), "turning right on page 0 advances to page 1");
        Check(GalleryPagination.Turn(1, 1, 19, 18) == (1, false), "turning right on last page is a no-op");
        Check(GalleryPagination.Turn(1, -1, 19, 18) == (0, true), "turning left on page 1 returns to page 0");

        for (int index = 0; index < 15; index++)
        {
            int page = GalleryPagination.PageForIndex(index, 15, 6);
            int first = GalleryPagination.FirstIndex(page, 15, 6);
            int vis = GalleryPagination.VisibleCount(page, 15, 6);
            Check(index >= first && index < first + vis, $"item {index} lands inside its resolved page [{first}..{first + vis - 1}]");
        }
    }

    private static void CheckScenicWindowPalettes()
    {
        string[] seasons = ["spring", "summer", "fall", "winter"];
        int[] times = [900, 1800, 2100];
        string?[] weathers = ["Sun", "Rain", "Storm", "Snow", "Wind", "GreenRain"];

        foreach (string season in seasons)
        {
            foreach (int time in times)
            {
                foreach (string? weather in weathers)
                {
                    ScenicWindowPalette p = ScenicWindowStyle.Resolve(season, time, weather);
                    Check(p.NormalizedSeason == season, $"normalized season {season}");
                    Check(p.TimePhase == (time < 1700 ? ScenicTimePhase.Day : time < 2000 ? ScenicTimePhase.Dusk : ScenicTimePhase.Night),
                        $"time phase for {time}");
                    Check(p.AtmosphereOverlay.Alpha is >= 0f and <= 1f, "valid overlay alpha");
                }
            }
        }

        ScenicWindowPalette springDay = ScenicWindowStyle.Resolve("Spring", 1000, "Sun");
        ScenicWindowPalette fallDay = ScenicWindowStyle.Resolve("Fall", 1000, "Sun");
        ScenicWindowPalette winterNight = ScenicWindowStyle.Resolve("Winter", 2200, null, isSnowing: true);
        ScenicWindowPalette greenRain = ScenicWindowStyle.Resolve("Summer", 1200, "Rain", isRaining: true, isGreenRain: true);
        ScenicWindowPalette unknownSeason = ScenicWindowStyle.Resolve("custom_unknown", 800, null);

        Check(springDay.SceneTint != fallDay.SceneTint, "spring and fall day scenes have distinct seasonal palettes");
        Check(winterNight.TimePhase == ScenicTimePhase.Night && winterNight.Weather == ScenicWeatherKind.Snow && winterNight.AtmosphereOverlay.Alpha > 0.35f,
            "winter snowy night applies deep night overlay and snow weather");
        Check(greenRain.Weather == ScenicWeatherKind.GreenRain && greenRain.AtmosphereOverlay.G > greenRain.AtmosphereOverlay.R,
            "green rain overrides ordinary rain with green atmospheric tint");
        Check(unknownSeason.NormalizedSeason == "spring", "unknown season falls back cleanly to spring");
    }

    private static void CheckScenicWindowRulesAndHobbyAnimations()
    {
        // 12 seasonal x day/dusk/night native 96x68 pixel-art asset paths
        Check(ScenicWindowRules.AllAssetPaths.Count == 12, "Expected exactly 12 seasonal day/dusk/night scenic assets");
        Check(ScenicWindowRules.ResolveAssetPath("spring", 900) == "assets/Scenic-Spring-Day.png", "Spring 09:00 -> Scenic-Spring-Day.png");
        Check(ScenicWindowRules.ResolveAssetPath("Spring", 1700) == "assets/Scenic-Spring-Dusk.png", "Spring 17:00 -> Scenic-Spring-Dusk.png");
        Check(ScenicWindowRules.ResolveAssetPath("SPRING", 2000) == "assets/Scenic-Spring-Night.png", "Spring 20:00 -> Scenic-Spring-Night.png");
        Check(ScenicWindowRules.ResolveAssetPath("summer", 1200) == "assets/Scenic-Summer-Day.png", "Summer Day");
        Check(ScenicWindowRules.ResolveAssetPath("fall", 1830) == "assets/Scenic-Fall-Dusk.png", "Fall Dusk");
        Check(ScenicWindowRules.ResolveAssetPath("winter", 2230) == "assets/Scenic-Winter-Night.png", "Winter Night");
        Check(ScenicWindowRules.ResolveAssetPath("unknown_season", 1100) == "assets/Scenic-Spring-Day.png", "Unknown season falls back to Spring");

        // Weather priority: GreenRain -> Storm -> Rain -> Snow -> Wind -> Clear
        Check(ScenicWindowRules.ResolveWorldWeather("Sun", isRaining: true, isSnowing: true, isLightning: true, isDebrisWeather: true, isGreenRain: true) == ScenicWeatherKind.GreenRain,
            "GreenRain takes highest priority");
        Check(ScenicWindowRules.ResolveWorldWeather("Rain", isRaining: true, isSnowing: true, isLightning: true, isDebrisWeather: true) == ScenicWeatherKind.Storm,
            "Storm overrides Rain/Snow/Wind");
        Check(ScenicWindowRules.ResolveWorldWeather("Snow", isRaining: true, isSnowing: true, isDebrisWeather: true) == ScenicWeatherKind.Rain,
            "Rain overrides Snow/Wind");
        Check(ScenicWindowRules.ResolveWorldWeather("Wind", isSnowing: true, isDebrisWeather: true) == ScenicWeatherKind.Snow,
            "Snow overrides Wind");
        Check(ScenicWindowRules.ResolveWorldWeather("Wind", isDebrisWeather: true) == ScenicWeatherKind.Wind,
            "Wind resolves to Wind");
        Check(ScenicWindowRules.ResolveWorldWeather("Sun") == ScenicWeatherKind.Clear,
            "Sun resolves to Clear");

        // Weather overcast wash applies only to Rain/Storm/GreenRain (keeps Clear/Wind/Snow pixel art crisp)
        Check(ScenicWindowRules.WeatherWashOverlay(ScenicWeatherKind.Clear).Alpha == 0f, "Clear applies zero wash");
        Check(ScenicWindowRules.WeatherWashOverlay(ScenicWeatherKind.Snow).Alpha == 0f, "Snow applies zero wash");
        Check(ScenicWindowRules.WeatherWashOverlay(ScenicWeatherKind.Wind).Alpha == 0f, "Wind applies zero wash");
        Check(ScenicWindowRules.WeatherWashOverlay(ScenicWeatherKind.Rain).Alpha > 0f, "Rain applies subtle wash");
        Check(ScenicWindowRules.WeatherWashOverlay(ScenicWeatherKind.GreenRain).G > ScenicWindowRules.WeatherWashOverlay(ScenicWeatherKind.GreenRain).R,
            "GreenRain applies green-dominant wash");

        // Strict screen-space integer pixel zoom (prevents fractional-scale sprite squashing)
        Check(ScenicWindowRules.ComputePixelZoom(388, 278) == 4, "1080p @ 100% uses exact 4x vanilla pixel zoom");
        Check(ScenicWindowRules.ComputePixelZoom(291, 208) == 4, "1080p @ 125% (user screenshot size) stays exact 4x vanilla pixel zoom");
        Check(ScenicWindowRules.ComputePixelZoom(277, 198) == 4, "720p keeps exact 4x vanilla pixel zoom");
        Check(ScenicWindowRules.ComputePixelZoom(200, 150) == 3, "Small viewport steps down cleanly to integer 3x");
        Check(ScenicWindowRules.ComputePixelZoom(130, 100) == 2, "Very small viewport steps down cleanly to integer 2x");

        ScenicStageLayout stage1080 = ScenicWindowRules.ComputeStageLayout(236, 116, 388, 278);
        Check(stage1080.PixelZoom == 4 && stage1080.StageWidth == 384 && stage1080.StageHeight == 272,
            "1080p stage renders 96x68 at exact 384x272");
        Check(stage1080.ActorFeetY > 116 + 128 && stage1080.ActorFeetY < 116 + 278,
            "1080p actor feet sit comfortably inside meadow");

        ScenicStageLayout stageScaled = ScenicWindowRules.ComputeStageLayout(180, 90, 291, 208);
        Check(stageScaled.PixelZoom == 4 && stageScaled.ActorFeetY - 128 >= 90 && stageScaled.ActorFeetY <= 90 + 208 - 6,
            "125% scaled viewport keeps full 4x character headroom and meadow footing");

        // Safe Data/AnimationDescriptions hobby parser & posture filter
        Dictionary<string, string> descriptions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["abigail_flute"] = "16 17/18 19 18 19/17 16",
            ["abigail_sleep"] = "28/28/28",
            ["haley_photo"] = "26/27 28/26",
            ["modnpc_sleep"] = "16/17/16",
            ["modnpc_sit_chair"] = "16/17/16",
            ["modnpc_violin"] = "16/17 18/16",
            ["bad_overflow"] = "16/99/16"
        };

        string? abiKey = ScenicWindowRules.SelectHobbyAnimationKey("Abigail", descriptions, 64, 256, 16, 32, out ScenicParsedAnimation abiAnim);
        Check(abiKey == "abigail_flute" && abiAnim.LoopFrames.SequenceEqual([18, 19, 18, 19]),
            "Curated vanilla Abigail hobby resolves abigail_flute");

        // Seasonal outfit sheet with only 4 rows (16 frames): abigail_flute uses frames 16..19 -> must safely reject
        string? abiShortSheet = ScenicWindowRules.SelectHobbyAnimationKey("Abigail", descriptions, 64, 128, 16, 32, out _);
        Check(abiShortSheet is null, "Out-of-bounds hobby animation is safely rejected on 4-row outfit sheets");

        // Modded NPC skips sleep/sit and selects upright violin hobby
        string? modKey = ScenicWindowRules.SelectHobbyAnimationKey("ModNpc", descriptions, 64, 256, 16, 32, out ScenicParsedAnimation modAnim);
        Check(modKey == "modnpc_violin" && modAnim.LoopFrames.SequenceEqual([17, 18]),
            "Modded NPC safely ignores sleep/sit and picks upright violin hobby");

        // Non-standard sheet layout (e.g., 2 columns) rejected safely
        Check(ScenicWindowRules.SelectHobbyAnimationKey("Haley", descriptions, 32, 256, 16, 32, out _) is null,
            "Non-4-column sheet safely skips hobby animations");
    }

    private static void CheckScenicMeadowStroll()
    {
        // Non-4x4 or odd-column sheets safely fall back to frame 0 with 0 offset.
        Check(StardewGallery.Appearance.AppearanceGeometry.ScenicStroll("Abigail", 2500, 32, 128, 16, 32) == (0, 0f),
            "narrow sheet (<4 columns) stays safely at frame 0");
        Check(StardewGallery.Appearance.AppearanceGeometry.ScenicStroll("Abigail", 2500, 64, 96, 16, 32) == (0, 0f),
            "short sheet (<4 rows) stays safely at frame 0");
        Check(StardewGallery.Appearance.AppearanceGeometry.ScenicStroll("Abigail", 2500, 128, 128, 16, 32) == (0, 0f),
            "8-column non-standard sheet stays safely at frame 0");

        // Standard 4x4 sheet (e.g. 64x256 with 16x32 frames): exercises walk-right (4..7), front idle (0), walk-left (12..15), never back (8..11).
        HashSet<int> observedFrames = [];
        for (double ms = 0; ms < 14400; ms += 100)
        {
            (int frame, float offset) = StardewGallery.Appearance.AppearanceGeometry.ScenicStroll("", ms, 64, 256, 16, 32, maxOffset: 28f);
            observedFrames.Add(frame);
            Check(Math.Abs(offset) <= 28.01f, $"stroll offset {offset} stays strictly within ±28px");
            Check(frame is 0 or (>= 4 and <= 7) or (>= 12 and <= 15), $"frame {frame} never uses back-facing 8..11 or out-of-range cells");
        }
        Check(observedFrames.Contains(0) && observedFrames.Overlaps([4, 5, 6, 7]) && observedFrames.Overlaps([12, 13, 14, 15]),
            "meadow stroll visits front-facing idle (0), walk-right (4..7) and walk-left (12..15)");
    }

    private static void CheckCardTitleFormatter()
    {
        static string Hearts(int h) => $"{h}心";
        static string T(string key) => key switch
        {
            "event.parallel-branch" => "平行分支（If 线）",
            "query.kind-ordinary" => "普通剧情",
            "query.kind-heart" => "好感剧情",
            "status.married" => "结婚",
            "status.engaged" => "订婚",
            "status.dating" => "恋爱",
            "status.roommate" => "室友",
            _ => key
        };

        GalleryEvent heartEvent = MakeStory("917409", "Beach", "917409/f Haley 1500/t 1000 1600", StoryKind.Heart, [new("Haley", 1500)]);
        string heartTitle = GalleryCardTitleFormatter.FormatStoryCardTitle(heartEvent, null, "海滩", " · ", false, "Haley", Hearts, T);
        Check(heartTitle == "6心 · 海滩" && !heartTitle.Contains("917409"),
            "default heart card title shows '6心 · 海滩' with zero raw EventId");

        string parallelTitle = GalleryCardTitleFormatter.FormatStoryCardTitle(heartEvent, null, "海滩", " · ", true, "Haley", Hearts, T);
        Check(parallelTitle == "6心 · 海滩 · 平行分支（If 线）",
            "parallel If-route appends clear badge without raw EventId");

        string customTitle = GalleryCardTitleFormatter.FormatStoryCardTitle(heartEvent, "  海边手链回忆  ", "海滩", " · ", true, "Haley", Hearts, T);
        Check(customTitle == "海边手链回忆 · 平行分支（If 线）",
            "custom player name takes priority while keeping parallel branch badge");

        GalleryEvent multiOwner = MakeStory("88001", "Mountain", "88001/f Abigail 1000 Maru 2000", StoryKind.Heart,
            [new("Abigail", 1000), new("Maru", 2000)]);
        Check(GalleryCardTitleFormatter.FormatStoryCardTitle(multiOwner, null, "山区", " · ", false, "Maru", Hearts, T) == "8心 · 山区"
            && GalleryCardTitleFormatter.FormatStoryCardTitle(multiOwner, null, "山区", " · ", false, "Abigail", Hearts, T) == "4心 · 山区",
            "multi-owner heart event formats hearts for the currently viewed album owner");

        GalleryEvent marriedLegacy = MakeStory("88002", "FarmHouse", "88002/O Emily", StoryKind.Heart, [new("Emily", null)]);
        Check(GalleryCardTitleFormatter.FormatStoryCardTitle(marriedLegacy, null, "农舍", " · ", false, "Emily", Hearts, T) == "结婚 · 农舍",
            "spouse-gated heart story without numeric points shows marriage stage + location");

        GalleryEvent engagedGsq = MakeStory("88003", "Town", "88003/G PLAYER_NPC_RELATIONSHIP Current Abigail Engaged", StoryKind.Heart, [new("Abigail", null)]);
        Check(GalleryCardTitleFormatter.FormatStoryCardTitle(engagedGsq, null, "鹈鹕镇", " · ", false, "Abigail", Hearts, T) == "订婚 · 鹈鹕镇",
            "GSQ relationship heart story shows engaged stage + location");

        GalleryEvent negatedSpouse = MakeStory("88004", "Town", "88004/!O Emily/e 100", StoryKind.Heart, [new("Emily", null)]);
        Check(GalleryCardTitleFormatter.FormatStoryCardTitle(negatedSpouse, null, "鹈鹕镇", " · ", false, "Emily", Hearts, T) == "好感剧情 · 鹈鹕镇",
            "negated !O precondition is not mislabeled as married");

        GalleryEvent negatedGsqClause = MakeStory("88005", "Town",
            "88005/G !PLAYER_NPC_RELATIONSHIP Current Emily Married", StoryKind.Heart, [new("Emily", null)]);
        Check(GalleryCardTitleFormatter.FormatStoryCardTitle(negatedGsqClause, null, "鹈鹕镇", " · ", false, "Emily", Hearts, T) == "好感剧情 · 鹈鹕镇",
            "GSQ !PLAYER_NPC_RELATIONSHIP clause is never mislabeled as married");

        GalleryEvent multiClauseNegatedGsq = MakeStory("88006", "Town",
            "88006/G SEASON spring, !PLAYER_NPC_RELATIONSHIP Current Emily Married", StoryKind.Heart, [new("Emily", null)]);
        Check(GalleryCardTitleFormatter.FormatStoryCardTitle(multiClauseNegatedGsq, null, "鹈鹕镇", " · ", false, "Emily", Hearts, T) == "好感剧情 · 鹈鹕镇",
            "compound GSQ with negated !PLAYER_NPC_RELATIONSHIP is never mislabeled as married");

        GalleryEvent crossNpcGsq = MakeStory("88007", "Town",
            "88007/G PLAYER_NPC_RELATIONSHIP Current Haley Married", StoryKind.Heart, [new("Emily", null)]);
        Check(GalleryCardTitleFormatter.FormatStoryCardTitle(crossNpcGsq, null, "鹈鹕镇", " · ", false, "Emily", Hearts, T) == "好感剧情 · 鹈鹕镇",
            "cross-character PLAYER_NPC_RELATIONSHIP for another NPC does not mislabel Emily's story");

        GalleryEvent anyRelationshipWithOwner = MakeStory("88008", "Town",
            "88008/G PLAYER_NPC_RELATIONSHIP_ANY Current Married", StoryKind.Heart, [new("Emily", null)]);
        Check(GalleryCardTitleFormatter.FormatStoryCardTitle(anyRelationshipWithOwner, null, "鹈鹕镇", " · ", false, "Emily", Hearts, T) == "好感剧情 · 鹈鹕镇"
            && GalleryCardTitleFormatter.InferRelationshipStageKey("88008/G PLAYER_NPC_RELATIONSHIP_ANY Current Married", null) == "status.married",
            "PLAYER_NPC_RELATIONSHIP_ANY matches only when no specific npcName is targeted");

        GalleryEvent quotedInvalidGsq = MakeStory("88009", "Town",
            "88009/G \"PLAYER_NPC_RELATIONSHIP Current Emily Married\"", StoryKind.Heart, [new("Emily", null)]);
        Check(GalleryCardTitleFormatter.InferRelationshipStageKey(quotedInvalidGsq.EventKey, "Emily") is null,
            "fully quoted invalid GSQ does not infer a relationship stage");

        GalleryEvent quotedNpcRoommate = MakeStory("88010", "QiNutRoom",
            "88010/G PLAYER_NPC_RELATIONSHIP Current \"Mr. Qi\" Roommate", StoryKind.Heart, [new("Mr. Qi", null)]);
        Check(GalleryCardTitleFormatter.FormatStoryCardTitle(quotedNpcRoommate, null, "齐先生的核桃房", " · ", false, "Mr. Qi", Hearts, T) == "室友 · 齐先生的核桃房",
            "quoted NPC name in positive PLAYER_NPC_RELATIONSHIP resolves roommate stage cleanly");

        GalleryEvent ordinary = MakeStory("77001", "Forest", "77001/Season spring", StoryKind.Ordinary, []);
        Check(GalleryCardTitleFormatter.FormatStoryCardTitle(ordinary, null, "煤矿森林", " · ", false, null, Hearts, T) == "普通剧情 · 煤矿森林"
            && !GalleryCardTitleFormatter.FormatStoryCardTitle(ordinary, null, "煤矿森林", " · ", false, null, Hearts, T).Contains("77001"),
            "ordinary story card title shows '普通剧情 · 煤矿森林' without raw EventId");
    }

    private static void CheckStorySearchIndexEncounterAndDisplayTitles()
    {
        GalleryEvent available = MakeStory("50001", "Town", "50001/f Abigail 500", StoryKind.Heart, [new("Abigail", 500)]);
        GalleryEvent ready = MakeStory("50002", "Beach", "50002/f Haley 1000/w rainy", StoryKind.Heart, [new("Haley", 1000)]);
        GalleryEvent seen = MakeStory("50003", "Mountain", "50003/f Sam 500", StoryKind.Heart, [new("Sam", 500)]);
        GalleryEvent ordinary = MakeStory("50004", "Forest", "50004/t 600 1200", StoryKind.Ordinary, []);

        var prereq = new PrerequisiteEvent("99001", [new MarkerAction("Rule_99001", "Current", true, ["DayStarted"], "", false, false, [], null)], [available], []);
        var catalog = new GalleryCatalog([], [available, ready, seen], [ordinary]) { Prerequisites = [prereq] };

        PinyinTable pinyin = PinyinTable.Parse(["xin 心", "hai 海", "tan 滩", "xiao 小", "zhen 镇"]);
        var clues = new Dictionary<EventIdentity, EncounterClue>
        {
            [available.Resolved.Identity] = EncounterClue.AvailableToday,
            [ready.Resolved.Identity] = EncounterClue.ReadyForTiming,
            [seen.Resolved.Identity] = EncounterClue.None,
            [ordinary.Resolved.Identity] = EncounterClue.AvailableToday
        };

        var index = new StorySearchIndex(
            catalog,
            e => e.LocationName switch { "Beach" => "海滩", "Town" => "小镇", "Mountain" => "山区", _ => "森林" },
            n => n,
            names: null,
            completed: new HashSet<string> { "50003" },
            pinyin: pinyin,
            defaultStoryTitle: e => e.Kind == StoryKind.Heart ? $"2心 · {(e.LocationName == "Beach" ? "海滩" : "小镇")}" : "普通剧情 · 森林",
            clueFor: e => clues.GetValueOrDefault(e.Resolved.Identity));

        StorySearchRow row50001 = index.Search("50001").Single();
        Check(row50001.DisplayTitle == "2心 · 小镇" && row50001.EventId == "50001",
            "StorySearchRow displays readable Hearts · Location title while preserving searchable EventId");

        Check(index.Search("2xin").Any(r => r.EventId == "50001") && index.Search("ht").Any(r => r.EventId == "50002"),
            "pinyin search matches generated DisplayTitle and localized location");

        var encounterAll = index.Search("", new QueryFilter { EncounterOnly = true });
        Check(encounterAll.Select(r => r.EventId).SequenceEqual(["50001", "50002", "50004"]),
            "EncounterOnly quick filter matches AvailableToday and ReadyForTiming stories and excludes None & prerequisites");

        var encounterHeartOnly = index.Search("", new QueryFilter { Kind = QueryKind.Heart, EncounterOnly = true });
        Check(encounterHeartOnly.Select(r => r.EventId).SequenceEqual(["50001", "50002"]),
            "EncounterOnly composes cleanly with Kind=Heart filter");
    }

    private static void CheckThreeRibbonNavigationAndSocialRoot()
    {
        var history = new GalleryPageHistory();
        history.Reset();
        Check(history.Current?.Page == GalleryPage.Home && history.Count == 1, "initial reset starts on Villagers Home");

        history.Current!.Scroll = 2;
        history.Current.Focus = 11;
        history.Open(new(GalleryPage.Album) { CharacterName = "Abigail", Scroll = 1, Focus = 2004, OriginTab = GalleryBookTab.Villagers });
        history.Open(new(GalleryPage.Detail) { Event = new("Data/Events/Town", "1"), CharacterName = "Abigail", OriginTab = GalleryBookTab.Villagers });
        Check(history.Count == 3, "drilled down to Home -> Album -> Detail");

        // Clicking active Villagers tab pops drilldown back to Home while keeping Home's page/focus!
        history.SwitchTab(GalleryBookTab.Villagers);
        Check(history.Count == 1 && history.Current?.Page == GalleryPage.Home && history.Current.Scroll == 2 && history.Current.Focus == 11,
            "clicking Villagers ribbon from nested Album/Detail returns to Home preserving page index and focus");

        // Switching to StoryIndex and Moments tabs retains Home at the bottom of the back stack ([Home, Query] / [Home, Moments])
        history.SwitchTab(GalleryBookTab.StoryIndex);
        Check(history.Count == 2 && history.Current?.Page == GalleryPage.Query && history.Current.OriginTab == GalleryBookTab.StoryIndex,
            "switching to StoryIndex ribbon keeps [Home, Query] stack");
        Check(history.Back() && history.Count == 1 && history.Current?.Page == GalleryPage.Home && history.Current.Scroll == 2,
            "Back from StoryIndex returns to Villagers Home (preserving Home scroll) instead of exiting gallery");

        history.SwitchTab(GalleryBookTab.Moments);
        Check(history.Count == 2 && history.Current?.Page == GalleryPage.Moments && history.Current.OriginTab == GalleryBookTab.Moments,
            "switching to Moments ribbon keeps [Home, Moments] stack");
        Check(history.Back() && history.Count == 1 && history.Current?.Page == GalleryPage.Home,
            "Back from Moments returns to Villagers Home instead of exiting gallery");
        Check(!history.Back() && history.Current is null,
            "second Back from Villagers Home exits gallery");

        // SocialPage context shortcut uses ResetTo(Album) so B from Album exits gallery back to SocialPage
        history.ResetTo(new(GalleryPage.Album) { CharacterName = "Sebastian", OriginTab = GalleryBookTab.Villagers });
        Check(history.Count == 1 && history.Current?.Page == GalleryPage.Album && history.Current.CharacterName == "Sebastian",
            "SocialPage shortcut sets Album as direct single-page root");
        history.Open(new(GalleryPage.Detail) { Event = new("Data/Events/Mountain", "27"), CharacterName = "Sebastian" });
        Check(history.Back() && history.Current?.Page == GalleryPage.Album && history.Current.CharacterName == "Sebastian",
            "first Back from Detail returns to Sebastian's Album");
        Check(!history.Back() && history.Current is null,
            "second Back from SocialPage-launched Album signals root exit so externalReturn restores SocialPage");
    }

    private static GalleryEvent MakeStory(string id, string location, string rawKey, StoryKind kind, IReadOnlyList<EventOwner> owners)
        => new(
            new ResolvedEvent(new EventIdentity("Data/Events/" + location, id), location, rawKey,
                "none/0 0/farmer 1 1 2/speak Haley \"Hello\"/end", new([], []), "def", "scr"),
            new EventOwnership(owners.Count > 0 ? OwnershipKind.Direct : OwnershipKind.Excluded, owners))
        {
            Kind = kind,
            RelatedNpcNames = owners.Select(o => o.Name).ToArray()
        };

    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException("Stage4Checks failed: " + message);
    }
}
