using StardewValley;

namespace StardewGallery;

// Reads only the explicitly supported game fields. Never invokes registered GSQ handlers.
internal static class SafeQueryStateReader
{
    internal static bool? Read(SafeQueryClause clause, GameLocation? target)
    {
        string[] a = clause.Arguments;
        if (clause.Name == "WEATHER")
        {
            GameLocation? location = a[0].Equals("Here", StringComparison.OrdinalIgnoreCase) ? Game1.currentLocation
                : a[0].Equals("Target", StringComparison.OrdinalIgnoreCase) ? target ?? Game1.currentLocation : Game1.getLocationFromName(a[0]);
            return location is null ? null : a.Skip(1).Contains(location.GetWeather().Weather, StringComparer.OrdinalIgnoreCase);
        }
        if (!clause.Name.StartsWith("PLAYER_", StringComparison.Ordinal) || a.Length < 2) return null;
        Farmer[]? players = a[0].ToUpperInvariant() switch
        {
            "CURRENT" or "TARGET" => [Game1.player], "HOST" => [Game1.MasterPlayer],
            "ANY" or "ALL" => Game1.getAllFarmers().ToArray(),
            _ => long.TryParse(a[0], out long id) && Game1.GetPlayer(id) is { } player ? [player] : null
        };
        if (players is null) return null;
        bool? Matches(Farmer player) => clause.Name switch
        {
            "PLAYER_HEARTS" => Range(player.getFriendshipHeartLevelForNPC(a[1]), a),
            "PLAYER_FRIENDSHIP_POINTS" => Range(player.getFriendshipLevelForNPC(a[1]), a),
            "PLAYER_HAS_SEEN_EVENT" => a.Skip(1).Any(player.eventsSeen.Contains),
            "PLAYER_HAS_CONVERSATION_TOPIC" => a.Skip(1).Any(player.activeDialogueEvents.ContainsKey),
            "PLAYER_HAS_RUN_TRIGGER_ACTION" => a.Skip(1).Any(player.triggerActionsRun.Contains),
            "PLAYER_HAS_MAIL" => (a.Length < 3 ? "any" : a[2].ToLowerInvariant()) switch
                { "received" => player.mailReceived.Contains(a[1]), "mailbox" => player.mailbox.Contains(a[1]),
                    "tomorrow" => player.mailForTomorrow.Contains(a[1]), _ => player.hasOrWillReceiveMail(a[1]) },
            "PLAYER_NPC_RELATIONSHIP" => a[1].Equals("Any", StringComparison.OrdinalIgnoreCase)
                ? player.friendshipData.Values.Any(friendship => Relationship(friendship, a.Skip(2)))
                : player.friendshipData.TryGetValue(a[1], out Friendship? friendship) && Relationship(friendship, a.Skip(2)),
            _ => null
        };
        bool?[] values = players.Select(Matches).ToArray();
        if (a[0].Equals("All", StringComparison.OrdinalIgnoreCase))
            return values.Contains(false) ? false : values.Contains(null) ? null : true;
        return values.Contains(true) ? true : values.Contains(null) ? null : false;
    }
    private static bool Range(int value, string[] a) => value >= SafeGameQuery.ParseInt(a[2])
        && (a.Length < 4 || value <= SafeGameQuery.ParseInt(a[3]));
    private static bool Relationship(Friendship friendship, IEnumerable<string> types)
        => types.Any(type => type.ToLowerInvariant() switch
        {
            "roommate" => friendship.Status == FriendshipStatus.Married && friendship.RoommateMarriage,
            "married" => friendship.Status == FriendshipStatus.Married && !friendship.RoommateMarriage,
            "divorced" => friendship.Status == FriendshipStatus.Divorced && !friendship.RoommateMarriage,
            _ => friendship.Status.ToString().Equals(type, StringComparison.OrdinalIgnoreCase)
        });
}
