using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewGallery.Appearance;

namespace StardewGallery;

internal sealed record GalleryTextures(Texture2D Home, Texture2D Album, Texture2D Detail, Texture2D Scene,
    Texture2D Thumbnail, Texture2D Replay, Texture2D SlotFrame, Texture2D Scrollbar, Texture2D ConditionIcons);

internal sealed record GalleryViewContext(GalleryCatalog Catalog, ITranslationHelper I18n, GalleryTextures Textures,
    GalleryPhotos Photos, IGalleryNavigation Navigation, Func<bool> IsUnlocked, Func<GalleryEvent, ReplayAccess> ReplayAccess)
{
    internal ICharacterAppearance Appearance { get; init; } = null!;
    internal EventNameStore? Names { get; init; }
    internal FavoriteEventStore? Favorites { get; init; }
    internal bool IsFavorite(EventIdentity identity) => Favorites?.IsFavorite(identity) == true;
    /// <summary>Set for Chinese: searches typed in Latin letters also match Chinese names by pinyin.</summary>
    internal PinyinTable? Pinyin { get; init; }
    internal Func<ResolvedEvent, EventSourceDisplay> EventSource { get; init; } = resolved =>
        EventOriginPresentation.Build(resolved, EventOriginMatch.Unknown, (key, args) => I18n.Get(key, args));
    internal GalleryLocationNames Locations { get; } = new(I18n);
    internal GalleryCharacterNames Characters { get; } = new(Catalog, I18n);
    internal string Name(EventIdentity identity) => Names?.Get(identity) ?? identity.EventId;
    internal bool IsParallelBranch(GalleryEvent entry) => Catalog.IsParallelBranchUnlocked(entry, Game1.player?.eventsSeen);
    internal bool IsStoryUnlocked(GalleryEvent entry) => ReplayAccessPolicy.IsUnlockedForViewing(
        entry.Kind,
        Game1.player?.eventsSeen.Contains(entry.EventId) == true,
        IsUnlocked(),
        IsParallelBranch(entry));

    internal string CardTitle(GalleryEvent entry, string? preferredOwnerName = null)
        => GalleryCardTitleFormatter.FormatStoryCardTitle(
            entry,
            Names?.Get(entry.Resolved.Identity),
            Locations.Get(entry.LocationName),
            GalleryDrawing.TextSeparator,
            IsParallelBranch(entry),
            preferredOwnerName,
            hearts => I18n.Get("event.hearts", new { hearts }),
            key => I18n.Get(key));

    internal string CardPrimaryLead(GalleryEvent entry, string? preferredOwnerName = null)
        => GalleryCardTitleFormatter.FormatPrimaryLead(
            entry,
            preferredOwnerName,
            hearts => I18n.Get("event.hearts", new { hearts }),
            key => I18n.Get(key));

    internal string? StoryQuote(GalleryEvent entry) => StoryQuoteExtractor.GetVisibleQuote(
        entry,
        seen: Game1.player?.eventsSeen.Contains(entry.EventId) == true,
        unlockAll: IsUnlocked(),
        parallelBranchUnlocked: IsParallelBranch(entry),
        parseCommands: raw => Event.ParseCommands(raw),
        splitArguments: cmd => ArgUtility.SplitBySpaceQuoteAware(cmd),
        loadTranslation: key => SafeTranslationLoader.TryLoad(Game1.content, key),
        farmerName: Game1.player?.Name,
        farmerIsMale: Game1.player?.IsMale ?? true,
        spouseName: Game1.player?.spouse);

    internal string MusicTitle(GalleryEvent entry) => GalleryMusicNames.FormatForDetail(entry.Resolved, I18n);

    private Dictionary<EventIdentity, EncounterClue>? eventClues;
    private Dictionary<string, EncounterClue>? characterClues;
    private Dictionary<string, StoryNodeProgress>? characterProgress;
    private CurrentStateSnapshot? capturedState;
    private Dictionary<string, (GameLocation? Location, CurrentStateSnapshot State)>? locationStates;
    private ConditionParser? clueParser;
    private ConditionEvaluator? clueEvaluator;

    internal EncounterClue EventClue(GalleryEvent entry)
    {
        if (entry.Kind == StoryKind.Internal)
            return EncounterClue.None;
        eventClues ??= [];
        if (eventClues.TryGetValue(entry.Resolved.Identity, out EncounterClue cached))
            return cached;

        EncounterClue computed = ComputeEventClue(entry);
        eventClues[entry.Resolved.Identity] = computed;
        return computed;
    }

    internal EncounterClue CharacterClue(string characterName)
    {
        characterClues ??= new(StringComparer.Ordinal);
        if (characterClues.TryGetValue(characterName, out EncounterClue cached))
            return cached;

        EncounterClue best = EncounterClue.None;
        foreach (GalleryEvent ev in Catalog.HeartEventsFor(characterName))
        {
            EncounterClue clue = EventClue(ev);
            if (clue == EncounterClue.AvailableToday)
            {
                best = EncounterClue.AvailableToday;
                break;
            }
            if (clue == EncounterClue.ReadyForTiming)
                best = EncounterClue.ReadyForTiming;
        }
        characterClues[characterName] = best;
        return best;
    }

    internal StoryNodeProgress CharacterProgress(string characterName)
    {
        characterProgress ??= new(StringComparer.Ordinal);
        if (!characterProgress.TryGetValue(characterName, out StoryNodeProgress progress))
        {
            progress = CharacterStoryProgress.Measure(
                Catalog.HeartEventsFor(characterName),
                Catalog,
                Game1.player?.eventsSeen);
            characterProgress[characterName] = progress;
        }
        return progress;
    }

    private EncounterClue ComputeEventClue(GalleryEvent entry)
    {
        try
        {
            if (Game1.player is null)
                return EncounterClue.None;
            capturedState ??= RuntimeStateReader.Capture();
            if (capturedState.EventsSeen?.Contains(entry.EventId) == true || Catalog.IsParallelBranchUnlocked(entry, capturedState.EventsSeen))
                return EncounterClue.None;

            clueParser ??= ConditionProduction.CreateParser(Event.SplitPreconditions, ArgUtility.SplitBySpaceQuoteAware);
            clueEvaluator ??= ConditionProduction.CreateEvaluator(null);
            locationStates ??= new(StringComparer.OrdinalIgnoreCase);

            if (!locationStates.TryGetValue(entry.LocationName, out var locPair))
            {
                GameLocation? loc = Game1.getLocationFromName(entry.LocationName);
                locPair = (loc, RuntimeStateReader.ForLocation(capturedState, loc));
                locationStates[entry.LocationName] = locPair;
            }

            ConditionSet parsed = clueParser.ParseRawKey(entry.EventKey);
            CurrentStateSnapshot state = locPair.State with { Details = ConditionStateReader.Capture(parsed, locPair.Location) };
            return EncounterClueClassifier.Classify(
                entry,
                parsed,
                state,
                clueEvaluator,
                parallelBranchUnlocked: false,
                locationAvailable: locPair.Location is not null);
        }
        catch
        {
            return EncounterClue.None;
        }
    }

    private (int NamesRevision, StorySearchIndex Index, float RowScale)? queryIndex;

    /// <summary>
    /// The query page is rebuilt on every visit; keep its index and row text scale for this view
    /// (one catalog and locale), and rebuild them after a custom name changes.
    /// </summary>
    internal (StorySearchIndex Index, float RowScale) QueryIndex(Func<(StorySearchIndex Index, float RowScale)> build)
    {
        int revision = Names?.Revision ?? 0;
        if (queryIndex is { } cached && cached.NamesRevision == revision)
            return (cached.Index, cached.RowScale);
        (StorySearchIndex index, float rowScale) = build();
        queryIndex = (revision, index, rowScale);
        return (index, rowScale);
    }
}
