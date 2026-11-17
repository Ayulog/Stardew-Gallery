using StardewValley;

namespace StardewGallery;

/// <summary>
/// Reads only the explicitly supported game fields with the same lookups as GameStateQuery.DefaultResolvers (1.6.15).
/// Never invokes registered GSQ handlers, trigger actions or anything that changes state.
/// </summary>
internal static class SafeQueryStateReader
{
    /// <param name="location">Where the check runs: the event location for event preconditions, else the player's location.</param>
    internal static bool? Read(SafeQueryClause clause, GameLocation? location)
    {
        string[] a = clause.Arguments;
        switch (clause.Name)
        {
            case "WEATHER":
                // TryGetLocationArg fails (an error result, so false) when a named location doesn't exist.
                return SafeGameQuery.IsContextLocation(a[0]) && (location ?? Game1.currentLocation) is null ? null
                    : Location(a[0], location) is { } weatherLocation
                        && a.Skip(1).Contains(weatherLocation.GetWeather().Weather, StringComparer.OrdinalIgnoreCase);
            case "LOCATION_SEASON":
                return SafeGameQuery.IsContextLocation(a[0]) && (location ?? Game1.currentLocation) is null ? null
                    : Location(a[0], location) is { } seasonLocation
                        && a.Skip(1).Contains(Game1.GetSeasonKeyForLocation(seasonLocation), StringComparer.OrdinalIgnoreCase);
            case "IS_GREEN_RAIN_DAY":
            {
                WorldDate date = new(Game1.Date);
                date.TotalDays++;
                return Utility.isGreenRainDay(date.DayOfMonth, date.Season);
            }
            case "IS_FESTIVAL_DAY":
            {
                string? contextId = null;
                if (a.Length > 0 && !a[0].Equals("any", StringComparison.OrdinalIgnoreCase))
                {
                    if (SafeGameQuery.IsContextLocation(a[0]))
                    {
                        if ((location ?? Game1.currentLocation) is not { } festivalLocation) return null;
                        contextId = festivalLocation.GetLocationContextId();
                    }
                    else
                        contextId = a[0];
                }
                int offset = a.Length > 1 ? SafeGameQuery.ParseInt(a[1]) : 0;
                int day = (Game1.Date.TotalDays + offset) % 112;
                return Utility.isFestivalDay(day % 28 + 1, (Season)(day / 28), contextId);
            }
            case "WORLD_STATE_ID":
                return a.Any(StardewValley.Network.NetWorldState.checkAnywhereForWorldStateID);
            case "BUILDINGS_CONSTRUCTED":
                return Buildings(a, location);
            case "IS_COMMUNITY_CENTER_COMPLETE":
                return Game1.MasterPlayer.hasCompletedCommunityCenter() && !Game1.MasterPlayer.mailReceived.Contains("JojaMember");
            case "IS_JOJA_MART_COMPLETE":
                return Utility.hasFinishedJojaRoute();
            case "IS_ISLAND_NORTH_BRIDGE_FIXED":
                return Game1.getLocationFromName("IslandNorth") is StardewValley.Locations.IslandNorth islandNorth && islandNorth.bridgeFixed.Value;
            case "FOUND_ALL_LOST_BOOKS":
                return Game1.netWorldState.Value.LostBooksFound >= 21;
            case "IS_MULTIPLAYER":
                return Game1.IsMultiplayer;
            case "IS_HOST":
                return Game1.IsMasterGame;
            case "IS_VISITING_ISLAND":
                return Game1.IsVisitingIslandToday(a[0]);
            case "MUSEUM_DONATIONS":
                return MuseumDonations(a);
            case "MINE_LOWEST_LEVEL_REACHED":
                return Range(StardewValley.Locations.MineShaft.lowestLevelReached, a, 0);
        }
        if (!clause.Name.StartsWith("PLAYER_", StringComparison.Ordinal) || a.Length < 1) return null;
        Farmer[]? players = a[0].ToUpperInvariant() switch
        {
            // Event preconditions pass no player, so Target is the local player like Current.
            "CURRENT" or "TARGET" => [Game1.player], "HOST" => [Game1.MasterPlayer],
            "ANY" or "ALL" => Game1.getAllFarmers().ToArray(),
            _ => long.TryParse(a[0], out long id) && Game1.GetPlayer(id) is { } player ? [player] : null
        };
        if (players is null) return null;
        bool? Matches(Farmer player)
        {
            if (SafeGameQuery.SkillQueries.TryGetValue(clause.Name, out var skill))
                return Range(SkillLevel(player, skill.Skill, skill.Base), a, 1);
            return clause.Name switch
            {
                "PLAYER_HEARTS" => a[1].ToUpperInvariant() switch
                {
                    "ANY" => player.hasAFriendWithHeartLevel(SafeGameQuery.ParseInt(a[2]), false, SafeGameQuery.OptionalMax(a, 3)),
                    "ANYDATEABLE" => player.hasAFriendWithHeartLevel(SafeGameQuery.ParseInt(a[2]), true, SafeGameQuery.OptionalMax(a, 3)),
                    _ => Range(player.getFriendshipHeartLevelForNPC(a[1]), a, 2)
                },
                "PLAYER_FRIENDSHIP_POINTS" => a[1].ToUpperInvariant() switch
                {
                    "ANY" => player.hasAFriendWithFriendshipPoints(SafeGameQuery.ParseInt(a[2]), false, SafeGameQuery.OptionalMax(a, 3)),
                    "ANYDATEABLE" => player.hasAFriendWithFriendshipPoints(SafeGameQuery.ParseInt(a[2]), true, SafeGameQuery.OptionalMax(a, 3)),
                    _ => Range(player.getFriendshipLevelForNPC(a[1]), a, 2)
                },
                "PLAYER_HAS_SEEN_EVENT" => a.Skip(1).Any(player.eventsSeen.Contains),
                "PLAYER_HAS_CONVERSATION_TOPIC" => a.Skip(1).Any(player.activeDialogueEvents.ContainsKey),
                "PLAYER_HAS_RUN_TRIGGER_ACTION" => a.Skip(1).Any(player.triggerActionsRun.Contains),
                "PLAYER_HAS_DIALOGUE_ANSWER" => a.Skip(1).Any(player.DialogueQuestionsAnswered.Contains),
                "PLAYER_VISITED_LOCATION" => a.Skip(1).Any(player.locationsVisited.Contains),
                "PLAYER_HAS_MAIL" => (a.Length < 3 ? "any" : a[2].ToLowerInvariant()) switch
                    { "received" => player.mailReceived.Contains(a[1]), "mailbox" => player.mailbox.Contains(a[1]),
                        "tomorrow" => player.mailForTomorrow.Contains(a[1]), _ => player.hasOrWillReceiveMail(a[1]) },
                "PLAYER_NPC_RELATIONSHIP" => a[1].Equals("Any", StringComparison.OrdinalIgnoreCase)
                    ? player.friendshipData.Values.Any(friendship => Relationship(friendship, a.Skip(2)))
                    : player.friendshipData.TryGetValue(a[1], out Friendship? friendship) && Relationship(friendship, a.Skip(2)),
                "PLAYER_HAS_ITEM" => HasItem(player, a),
                // ArgUtility.TryGetRemainder joins the rest, so unquoted recipe names with spaces work.
                "PLAYER_HAS_CRAFTING_RECIPE" => player.craftingRecipes.ContainsKey(string.Join(' ', a.Skip(1))),
                "PLAYER_HAS_COOKING_RECIPE" => player.cookingRecipes.ContainsKey(string.Join(' ', a.Skip(1))),
                "PLAYER_HAS_ACHIEVEMENT" => player.achievements.Contains(SafeGameQuery.ParseInt(a[1])),
                "PLAYER_HAS_SECRET_NOTE" => player.secretNotesSeen.Contains(SafeGameQuery.ParseInt(a[1])),
                // Any value other than Male means female, as in the game.
                "PLAYER_GENDER" => player.IsMale == a[1].Equals("Male", StringComparison.OrdinalIgnoreCase),
                "PLAYER_CURRENT_MONEY" => Range(player.Money, a, 1),
                "PLAYER_MONEY_EARNED" => Range(player.totalMoneyEarned, a, 1),
                "PLAYER_FARMHOUSE_UPGRADE" => Range(player.HouseUpgradeLevel, a, 1),
                "PLAYER_HAS_MET" => a.Skip(1).Any(player.friendshipData.ContainsKey),
                "PLAYER_HAS_PET" => player.hasPet(),
                "PLAYER_HAS_CHILDREN" => Children(player, a),
                "PLAYER_HAS_TOWN_KEY" => player.HasTownKey,
                "PLAYER_SPECIAL_ORDER_COMPLETE" => a.Skip(1).Any(player.team.completedSpecialOrders.Contains),
                "PLAYER_SPECIAL_ORDER_ACTIVE" => a.Skip(1).Any(player.team.SpecialOrderActive),
                "PLAYER_HAS_HEARD_SONG" => a.Skip(1).Any(player.songsHeard.Contains),
                "PLAYER_KILLED_MONSTERS" => KilledMonsters(player, a),
                _ => null
            };
        }
        bool?[] values = players.Select(Matches).ToArray();
        if (a[0].Equals("All", StringComparison.OrdinalIgnoreCase))
            return values.Contains(false) ? false : values.Contains(null) ? null : true;
        return values.Contains(true) ? true : values.Contains(null) ? null : false;
    }

    /// <summary>GameStateQuery.Helpers.GetLocation, with Here meaning the checked location (see ConditionParityChecks).</summary>
    private static GameLocation? Location(string name, GameLocation? location)
        => SafeGameQuery.IsContextLocation(name) ? location ?? Game1.currentLocation : Game1.getLocationFromName(name);

    private static bool? Buildings(string[] a, GameLocation? location)
    {
        bool allLocations = a[0].Equals("All", StringComparison.OrdinalIgnoreCase);
        string type = a.Length > 1 ? a[1] : "All";
        bool allTypes = type.Equals("All", StringComparison.OrdinalIgnoreCase);
        bool underConstruction = a.Length > 4 && bool.Parse(a[4]);
        int count;
        if (allLocations)
            count = allTypes ? Game1.GetNumberBuildingsConstructed(underConstruction) : Game1.GetNumberBuildingsConstructed(type, underConstruction);
        else
        {
            if (Location(a[0], location) is not { } target) return SafeGameQuery.IsContextLocation(a[0]) ? null : false;
            count = allTypes ? target.getNumberBuildingsConstructed(underConstruction) : target.getNumberBuildingsConstructed(type, underConstruction);
        }
        int min = a.Length > 2 ? SafeGameQuery.ParseInt(a[2]) : 1;
        return count >= min && count <= SafeGameQuery.OptionalMax(a, 3);
    }

    private static bool HasItem(Farmer player, string[] a)
    {
        string id = a[1];
        int min = a.Length > 2 ? SafeGameQuery.ParseInt(a[2]) : 1, max = SafeGameQuery.OptionalMax(a, 3);
        // Golden walnuts and Qi gems aren't inventory items; the game reads their counters.
        if (id is "73" or "(O)73") return Game1.netWorldState.Value.GoldenWalnuts is int walnuts && walnuts >= min && walnuts <= max;
        if (id is "858" or "(O)858") return player.QiGems >= min && player.QiGems <= max;
        if (max == int.MaxValue) return player.Items.ContainsId(id, min);
        int count = player.Items.CountId(id);
        return count >= min && count <= max;
    }

    private static int SkillLevel(Farmer player, string skill, bool baseLevel) => skill switch
    {
        "farming" => baseLevel ? player.farmingLevel.Value : player.FarmingLevel,
        "mining" => baseLevel ? player.miningLevel.Value : player.MiningLevel,
        "foraging" => baseLevel ? player.foragingLevel.Value : player.ForagingLevel,
        "fishing" => baseLevel ? player.fishingLevel.Value : player.FishingLevel,
        "combat" => baseLevel ? player.combatLevel.Value : player.CombatLevel,
        _ => baseLevel ? player.luckLevel.Value : player.LuckLevel
    };

    private static bool Range(long value, string[] a, int index) => value >= SafeGameQuery.ParseInt(a[index])
        && value <= SafeGameQuery.OptionalMax(a, index + 1);

    private static bool MuseumDonations(string[] a)
    {
        int min = SafeGameQuery.ParseInt(a[0]);
        bool hasMax = a.Length >= 2 && SafeGameQuery.IsInt(a[1]);
        int max = hasMax ? SafeGameQuery.ParseInt(a[1]) : int.MaxValue;
        int typeStart = hasMax ? 2 : 1;
        var pieces = Game1.netWorldState.Value.MuseumPieces;
        int count = typeStart >= a.Length
            ? pieces.Length
            : pieces.Values.Count(itemId => ItemRegistry.GetDataOrErrorItem(itemId).ObjectType is { } objectType
                && a.Skip(typeStart).Contains(objectType, StringComparer.Ordinal));
        return count >= min && count <= max;
    }

    private static bool Children(Farmer player, string[] a)
    {
        int min = a.Length > 1 ? SafeGameQuery.ParseInt(a[1]) : 1;
        int max = SafeGameQuery.OptionalMax(a, 2);
        int count = player.getChildrenCount();
        return count >= min && count <= max;
    }

    private static bool KilledMonsters(Farmer player, string[] a)
    {
        int index = 1;
        int killed = 0;
        while (index < a.Length && !SafeGameQuery.IsInt(a[index]))
        {
            killed += player.stats.getMonstersKilled(a[index]);
            index++;
        }
        int min = index < a.Length ? SafeGameQuery.ParseInt(a[index]) : 1;
        int max = SafeGameQuery.OptionalMax(a, index + 1);
        return killed >= min && killed <= max;
    }

    private static bool Relationship(Friendship friendship, IEnumerable<string> types)
        => types.Any(type => type.ToLowerInvariant() switch
        {
            "roommate" => friendship.Status == FriendshipStatus.Married && friendship.RoommateMarriage,
            "married" => friendship.Status == FriendshipStatus.Married && !friendship.RoommateMarriage,
            "divorced" => friendship.Status == FriendshipStatus.Divorced && !friendship.RoommateMarriage,
            _ => friendship.Status.ToString().Equals(type, StringComparison.OrdinalIgnoreCase)
        });
}
