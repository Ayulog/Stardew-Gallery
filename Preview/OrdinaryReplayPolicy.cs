namespace StardewGallery;

internal sealed record ReplayCompatibility(string? ReasonKey, string? Detail = null)
{
    internal bool Supported => ReasonKey is null;
}

internal sealed class OrdinaryReplayPolicy(Func<string, string[]> parseCommands, Func<string, string[]> splitArguments)
{
    // Reviewed native staging commands. Delegated actions, world progression, minigames and arbitrary handlers stay outside this set.
    private static readonly HashSet<string> StagingCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "speak", "message", "pause", "precisePause", "move", "faceDirection", "warp", "speed", "emote", "animate", "stopAnimation",
        "showFrame", "farmerAnimation", "ignoreMovementAnimation", "halt", "jump", "shake", "viewport", "fade", "globalFade", "globalFadeToClear",
        "playMusic", "stopMusic", "playSound", "stopSound", "textAboveHead", "spriteText", "screenFlash", "ambientLight", "bgColor",
        "beginSimultaneousCommand", "endSimultaneousCommand", "waitForAllStationary", "skippable", "unskippable", "ignoreEventTileOffset",
        "drawOffset", "positionOffset", "resetVariable", "setRunning", "stopAdvancedMoves", "stopJittering", "startJittering",
        "changeSprite", "changePortrait", "changeName", "changeYSourceRectOffset", "extendSourceRect", "end", "fork", "switchEvent", "changeLocation",
        "addConversationTopic", "eventSeen", "questionAnswered"
    };
    private static readonly HashSet<string> SpecialEventIds = new(StringComparer.Ordinal)
    {
        "-2", "1039573", "9333220", "4324303", "4325434", "3912132", "8675611", "3917601", "3917666", "5183338",
        "33", "897405", "1590166", "980559", "-157039427", "-888999", "-666777", "6497428", "-78765", "690006", "191393",
        "2123343", "404798", "26", "611173", "3091462", "3918602", "19", "992553", "900553", "980558", "60367", "739330", "112", "558292", "100162"
    };

    internal static ReplayCompatibility Context(ResolvedEvent entry)
    {
        if (!entry.HasLocationContext || string.IsNullOrWhiteSpace(entry.LocationName)) return new("replay.context-missing");
        if (entry.Fragments.MissingKeys.Count > 0) return new("replay.fragments-missing", string.Join(", ", entry.Fragments.MissingKeys));
        if (string.IsNullOrWhiteSpace(entry.ResolvedScript)) return new("replay.script-unsupported");
        return new(null);
    }

    internal ReplayCompatibility Check(ResolvedEvent entry, Func<string, bool> nativeCommand, Func<string, bool> existingLocation,
        Func<string, bool>? actorAvailable = null)
    {
        ReplayCompatibility context = Context(entry);
        if (!context.Supported) return context;
        if (!existingLocation(entry.LocationName)) return new("replay.context-missing");
        if (SpecialEventIds.Contains(entry.EventId)) return new("replay.script-unsupported", "special-event-id");
        string[] root = parseCommands(entry.ResolvedScript);
        if (root.Length < 4 || (root[1] != "follow" && !Coordinates(splitArguments(root[1]), 0))) return new("replay.script-unsupported", "event-header");
        string[] positions = splitArguments(root[2]);
        if (positions.Length == 0 || positions.Length % 4 != 0) return new("replay.script-unsupported", "actor-header");
        HashSet<string> actors = new(StringComparer.Ordinal);
        for (int i = 0; i < positions.Length; i += 4)
        {
            string name = positions[i].TrimEnd('?');
            if (!Coordinates(positions, i + 1) || !int.TryParse(positions[i + 1], out int actorX) || actorX == -1 || !int.TryParse(positions[i + 3], out _))
                return new("replay.script-unsupported", "live-or-invalid-actor");
            if (name != "farmer" && name != "otherFarmers" && !positions[i].EndsWith('?') && actorAvailable?.Invoke(name) == false)
                return new("replay.actor-missing", name);
            actors.Add(name);
        }
        IEnumerable<string> commands = root.Skip(3).Concat(entry.Fragments.Scripts.Skip(1).SelectMany(parseCommands));
        bool hasEnd = false;
        foreach (string command in commands)
        {
            string[] args = splitArguments(command);
            if (args.Length == 0 || !StagingCommands.Contains(args[0]) || !nativeCommand(args[0]))
                return new("replay.script-unsupported", args.FirstOrDefault() ?? "empty-command");
            hasEnd |= args[0].Equals("end", StringComparison.OrdinalIgnoreCase);
            if (args[0].Equals("end", StringComparison.OrdinalIgnoreCase) && args.Length > 1
                && !(args[1].Equals("position", StringComparison.Ordinal) && args.Length == 4 && Coordinates(args, 2)))
                return new("replay.script-unsupported", "end " + args[1]);
            if (args[0].Equals("changeLocation", StringComparison.OrdinalIgnoreCase)
                && (args.Length < 2 || !existingLocation(args[1]))) return new("replay.context-missing", args.ElementAtOrDefault(1));
            if ((args[0].Equals("speak", StringComparison.OrdinalIgnoreCase) || args[0].Equals("changePortrait", StringComparison.OrdinalIgnoreCase))
                && (args.Length < 2 || !actors.Contains(args[1].TrimEnd('?')))) return new("replay.actor-missing", args.ElementAtOrDefault(1));
            if (args[0].Equals("speak", StringComparison.OrdinalIgnoreCase) && args.Length < 3)
                return new("replay.script-unsupported", "speak");
        }
        return hasEnd ? new(null) : new("replay.script-unsupported", "no-end-command");
    }
    private static bool Coordinates(string[] args, int offset) => args.Length > offset + 1
        && int.TryParse(args[offset], out _) && int.TryParse(args[offset + 1], out _);
}
