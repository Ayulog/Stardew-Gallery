namespace StardewGallery;

/// <summary>
/// Pure, deterministic rules for silent Auto-Memento capture during natural story playback, villager weddings,
/// and single-player gallery replays.
/// </summary>
internal static class AutoMementoRules
{
    internal static EventIdentity? ResolveLiveStoryIdentity(
        GalleryCatalog catalog,
        string? fromAssetName,
        string? currentLocationName,
        string? eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
            return null;
        string id = eventId.Trim();
        if (id is "-1" or "-2")
            return null;

        if (!string.IsNullOrWhiteSpace(fromAssetName) && TryMatchCatalog(catalog, fromAssetName, id, out EventIdentity matched))
            return matched;

        if (!string.IsNullOrWhiteSpace(currentLocationName)
            && TryMatchCatalog(catalog, "Data/Events/" + currentLocationName.Trim(), id, out matched))
        {
            return matched;
        }

        string normalizedAsset = (fromAssetName ?? "").Trim().Replace('\\', '/');
        if ((normalizedAsset.Equals("Data/Events/Trailer_Big", StringComparison.OrdinalIgnoreCase)
                || string.Equals(currentLocationName?.Trim(), "Trailer_Big", StringComparison.OrdinalIgnoreCase))
            && TryMatchCatalog(catalog, "Data/Events/Trailer", id, out matched))
        {
            return matched;
        }

        if (id is "558291" or "558292" && TryMatchCatalog(catalog, "Data/Events/FarmHouse", id, out matched))
            return matched;

        return null;
    }

    private static bool TryMatchCatalog(GalleryCatalog catalog, string assetName, string eventId, out EventIdentity identity)
    {
        if (catalog.Find(new EventIdentity(assetName, eventId)) is { Kind: StoryKind.Heart or StoryKind.Ordinary } entry)
        {
            identity = entry.Resolved.Identity;
            return true;
        }
        identity = default;
        return false;
    }

    /// <summary>
    /// Lightweight single-event resolver used during natural gameplay when the full <see cref="GalleryCatalog"/> has not
    /// been built yet, avoiding full <c>Data/Events/*</c> or content-pack disk scans during cutscene startup.
    /// </summary>
    internal static EventIdentity? ResolveSingleAssetStoryIdentity(
        string? fromAssetName,
        string? currentLocationName,
        string? eventId,
        bool pamHouseUpgraded,
        Func<string, IReadOnlyDictionary<string, string>?> loadAssetEvents,
        Func<string, string, bool> isValidLocationEvent,
        Func<ResolvedEvent, bool> isInternalFlow,
        Func<string, string[]> parseCommands,
        Func<string, string[]> parseArguments,
        Func<string, string?> loadTranslation)
    {
        if (string.IsNullOrWhiteSpace(eventId))
            return null;
        string trimmedId = eventId.Trim();
        if (trimmedId is "-1" or "-2")
            return null;

        string? primaryAsset = !string.IsNullOrWhiteSpace(fromAssetName)
            ? fromAssetName.Trim()
            : !string.IsNullOrWhiteSpace(currentLocationName)
                ? "Data/Events/" + currentLocationName.Trim()
                : null;
        if (primaryAsset is null)
            return null;

        string normalizedPrimary = primaryAsset.Replace('\\', '/');
        string rootLocation = GuessLocationName(normalizedPrimary, currentLocationName);

        // Grandpa evaluation global copies (558291 / 558292) belong to Data/Events/FarmHouse.
        if (trimmedId is "558291" or "558292"
            && !normalizedPrimary.Equals("Data/Events/FarmHouse", StringComparison.OrdinalIgnoreCase)
            && loadAssetEvents("Data/Events/FarmHouse") is { } farmhouseEvents)
        {
            IReadOnlyDictionary<string, string>? localEvents = loadAssetEvents(primaryAsset);
            if (TryFindMatchingEntry(localEvents, trimmedId, isValidLocationEvent, out string? localKey, out string? localScript))
            {
                string identityAsset = NativeGlobalEventCopies.IsCopy(primaryAsset, localKey, localScript, farmhouseEvents)
                    ? "Data/Events/FarmHouse"
                    : primaryAsset;
                return ClassifySingleEvent(identityAsset, identityAsset == "Data/Events/FarmHouse" ? "FarmHouse" : rootLocation,
                    localKey, localScript, loadAssetEvents, isInternalFlow, parseCommands, parseArguments, loadTranslation);
            }
            if (TryFindMatchingEntry(farmhouseEvents, trimmedId, isValidLocationEvent, out string? fhKey, out string? fhScript))
            {
                return ClassifySingleEvent("Data/Events/FarmHouse", "FarmHouse",
                    fhKey, fhScript, loadAssetEvents, isInternalFlow, parseCommands, parseArguments, loadTranslation);
            }
            return null;
        }

        // Pam house upgrade merges Trailer events into Trailer_Big while keeping their Data/Events/Trailer identity.
        if (normalizedPrimary.Equals("Data/Events/Trailer_Big", StringComparison.OrdinalIgnoreCase)
            || normalizedPrimary.Equals("Data/Events/Trailer", StringComparison.OrdinalIgnoreCase))
        {
            IReadOnlyDictionary<string, string>? trailerEvents = loadAssetEvents(NativeGlobalEventCopies.TrailerAsset);
            IReadOnlyDictionary<string, string>? trailerBigEvents = loadAssetEvents(NativeGlobalEventCopies.TrailerBigAsset);
            bool fromBig = normalizedPrimary.EndsWith("Trailer_Big", StringComparison.OrdinalIgnoreCase);

            if (fromBig && TryFindMatchingEntry(trailerBigEvents, trimmedId, isValidLocationEvent, out string? bigKey, out string? bigScript))
            {
                return ClassifySingleEvent(NativeGlobalEventCopies.TrailerBigAsset, "Trailer_Big",
                    bigKey, bigScript, loadAssetEvents, isInternalFlow, parseCommands, parseArguments, loadTranslation);
            }

            if (TryFindMatchingEntry(trailerEvents, trimmedId, isValidLocationEvent, out string? trKey, out string? trScript))
            {
                string fragmentRoot = fromBig || pamHouseUpgraded ? "Trailer_Big" : "Trailer";
                return ClassifySingleEvent(NativeGlobalEventCopies.TrailerAsset, fragmentRoot,
                    trKey, trScript, loadAssetEvents, isInternalFlow, parseCommands, parseArguments, loadTranslation);
            }

            if (!fromBig && TryFindMatchingEntry(trailerBigEvents, trimmedId, isValidLocationEvent, out bigKey, out bigScript))
            {
                return ClassifySingleEvent(NativeGlobalEventCopies.TrailerBigAsset, "Trailer_Big",
                    bigKey, bigScript, loadAssetEvents, isInternalFlow, parseCommands, parseArguments, loadTranslation);
            }

            return null;
        }

        IReadOnlyDictionary<string, string>? primaryEvents = loadAssetEvents(primaryAsset);
        if (TryFindMatchingEntry(primaryEvents, trimmedId, isValidLocationEvent, out string? rawKey, out string? rawScript))
        {
            return ClassifySingleEvent(primaryAsset, rootLocation,
                rawKey, rawScript, loadAssetEvents, isInternalFlow, parseCommands, parseArguments, loadTranslation);
        }

        if (!string.IsNullOrWhiteSpace(currentLocationName))
        {
            string fallbackAsset = "Data/Events/" + currentLocationName.Trim();
            if (!fallbackAsset.Equals(normalizedPrimary, StringComparison.OrdinalIgnoreCase)
                && TryFindMatchingEntry(loadAssetEvents(fallbackAsset), trimmedId, isValidLocationEvent, out rawKey, out rawScript))
            {
                return ClassifySingleEvent(fallbackAsset, currentLocationName.Trim(),
                    rawKey, rawScript, loadAssetEvents, isInternalFlow, parseCommands, parseArguments, loadTranslation);
            }
        }

        return null;
    }

    private static bool TryFindMatchingEntry(
        IReadOnlyDictionary<string, string>? events,
        string trimmedEventId,
        Func<string, string, bool> isValidLocationEvent,
        out string matchedKey,
        out string matchedScript)
    {
        if (events is not null)
        {
            foreach ((string key, string script) in events)
            {
                if (!EventKey.TryGetId(key, out string parsedId) || parsedId.Trim() != trimmedEventId)
                    continue;
                if (!isValidLocationEvent(key, script) || EventKey.IsPlaceholderScript(script))
                    continue;
                matchedKey = key;
                matchedScript = script;
                return true;
            }
        }
        matchedKey = "";
        matchedScript = "";
        return false;
    }

    private static EventIdentity? ClassifySingleEvent(
        string assetName,
        string locationName,
        string rawKey,
        string script,
        Func<string, IReadOnlyDictionary<string, string>?> loadAssetEvents,
        Func<ResolvedEvent, bool> isInternalFlow,
        Func<string, string[]> parseCommands,
        Func<string, string[]> parseArguments,
        Func<string, string?> loadTranslation)
    {
        if (!EventKey.TryGetId(rawKey, out string exactId))
            return null;

        EventFragments fragments = EventFragmentCollector.Collect(
            script,
            locationName,
            loc => loadAssetEvents("Data/Events/" + loc),
            parseCommands,
            parseArguments,
            loadTranslation);
        ResolvedEvent resolved = new(
            Identity: new EventIdentity(assetName, exactId),
            LocationName: locationName,
            RawEventKey: rawKey,
            ResolvedScript: script,
            Fragments: fragments,
            RootDefinitionHash: EventHashes.RootDefinition(rawKey, script),
            RootScriptHash: EventHashes.RootScript(script));

        return isInternalFlow(resolved) ? null : resolved.Identity;
    }

    private static string GuessLocationName(string normalizedAssetName, string? currentLocationName)
    {
        const string prefix = "Data/Events/";
        if (normalizedAssetName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && normalizedAssetName.Length > prefix.Length)
        {
            return normalizedAssetName[prefix.Length..];
        }
        return string.IsNullOrWhiteSpace(currentLocationName) ? "" : currentLocationName.Trim();
    }

    internal static bool IsLocalVillagerWedding(
        bool isWedding,
        bool isFestival,
        long? eventFarmerId,
        long? localPlayerId,
        bool hasRoommate,
        bool hasFarmerSpouse,
        string? spouseName,
        out string validSpouseName)
    {
        validSpouseName = "";
        if (!isWedding || isFestival || hasRoommate || hasFarmerSpouse)
            return false;
        if (eventFarmerId is null || localPlayerId is null || eventFarmerId.Value != localPlayerId.Value)
            return false;
        if (string.IsNullOrWhiteSpace(spouseName))
            return false;
        validSpouseName = spouseName.Trim();
        return true;
    }

    /// <summary>
    /// Checks 1.6.15 fade and warp flags without inspecting <c>Game1.fadeIn</c>, because <c>ScreenFade.UpdateGlobalFade</c>
    /// leaves <c>fadeIn == true</c> after <c>GlobalFadeToClear</c> finishes.
    /// </summary>
    internal static bool IsTransitionClear(
        bool hasLocationRequest,
        bool isWarping,
        float fadeToBlackAlpha,
        bool globalFade,
        bool fadeToBlack,
        bool nonWarpFade)
        => !hasLocationRequest
            && !isWarping
            && fadeToBlackAlpha <= 0f
            && !globalFade
            && !fadeToBlack
            && !nonWarpFade;

    /// <summary>
    /// Accepts small indoor maps whose centered viewport has modest negative margins while rejecting hidden offscreen
    /// staging coordinates such as <c>viewport -100 -100</c> or <c>viewport -1000 -1000</c>.
    /// </summary>
    internal static bool IsStageViewportVisible(
        int viewportX,
        int viewportY,
        int viewportWidth,
        int viewportHeight,
        int? mapPixelWidth,
        int? mapPixelHeight)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0)
            return false;

        if (mapPixelWidth is > 0 && mapPixelHeight is > 0)
        {
            return viewportX < mapPixelWidth.Value
                && (long)viewportX + viewportWidth > 0
                && viewportY < mapPixelHeight.Value
                && (long)viewportY + viewportHeight > 0;
        }

        return viewportX > -2000 && viewportY > -2000;
    }
}

/// <summary>
/// Stateful per-screen gate ensuring at most one silent auto-memento capture attempt per <c>Event</c> instance,
/// triggered only after transitions clear and the visible stage settles.
/// </summary>
internal sealed class AutoMementoGate
{
    internal const int MinPostFadeTicks = 8;
    internal const int FallbackSettleTicks = 45;

    private object? currentEvent;
    private object? attemptedEvent;
    private int settledTicks;

    internal int SettledTicks => settledTicks;
    internal bool HasAttempted(object? eventInstance) => eventInstance is not null && ReferenceEquals(attemptedEvent, eventInstance);

    internal void Reset()
    {
        currentEvent = null;
        attemptedEvent = null;
        settledTicks = 0;
    }

    internal bool ShouldRequestCapture(
        object? activeEvent,
        bool autoCaptureEnabled,
        bool storeReady,
        bool capturePending,
        int existingPhotoCount,
        bool transitionClear,
        bool stageVisible,
        bool dialogueUp)
    {
        if (activeEvent is null)
        {
            Reset();
            return false;
        }

        if (!ReferenceEquals(currentEvent, activeEvent))
        {
            currentEvent = activeEvent;
            settledTicks = 0;
        }

        if (!autoCaptureEnabled || !storeReady || capturePending || ReferenceEquals(attemptedEvent, activeEvent))
        {
            if (!transitionClear || !stageVisible)
                settledTicks = 0;
            return false;
        }

        if (existingPhotoCount > 0)
        {
            attemptedEvent = activeEvent;
            return false;
        }

        if (!transitionClear || !stageVisible)
        {
            settledTicks = 0;
            return false;
        }

        settledTicks++;
        if (settledTicks >= MinPostFadeTicks && (dialogueUp || settledTicks >= FallbackSettleTicks))
        {
            attemptedEvent = activeEvent;
            return true;
        }

        return false;
    }
}
