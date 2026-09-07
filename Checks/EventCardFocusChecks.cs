using System.Runtime.CompilerServices;
using StardewGallery;

internal static class EventCardFocusChecks
{
    // Run with the existing executable without changing its top-level entry point.
    [ModuleInitializer]
    internal static void Run()
    {
        static bool Unlocked(int _) => true;
        static bool Locked(int _) => false;

        Check(new EventCardFocus(0, EventCardAction.Replay).ClampToViewport(12, Unlocked, 3)
            == new EventCardFocus(6, EventCardAction.Replay), "Footer memory must follow an explicitly scrolled viewport.");
        Check(new EventCardFocus(1, EventCardAction.Replay).ClampToViewport(12, Locked, 3)
            == new EventCardFocus(7, EventCardAction.Details), "Scrolled footer preserves column with locked fallback.");
        Check(new EventCardFocus(6, EventCardAction.Details).ClampToViewport(12, Unlocked, 2)
            == new EventCardFocus(6, EventCardAction.Details), "Footer round trip must retain visible Details identity.");

        for (int index = 0; index < 6; index++)
        {
            Expect(new(index, EventCardAction.Details), EventCardDirection.Down, 10, Unlocked,
                new(index, EventCardAction.Replay), 0);
            Expect(new(index, EventCardAction.Details), EventCardDirection.Down, 10, Locked,
                new(index + 2, EventCardAction.Details), index >= 4 ? 1 : 0);
            Expect(new(index, EventCardAction.Replay), EventCardDirection.Up, 10, Unlocked,
                new(index, EventCardAction.Details), 0);
            Expect(new(index, EventCardAction.Details), EventCardDirection.Up, 10, Unlocked,
                new(Math.Max(index % 2, index - 2), EventCardAction.Details), 0);
        }

        Expect(new(4, EventCardAction.Replay), EventCardDirection.Down, 10, Unlocked,
            new(6, EventCardAction.Replay), 1);
        Expect(new(4, EventCardAction.Replay), EventCardDirection.Down, 10, i => i != 6,
            new(6, EventCardAction.Details), 1);
        Expect(new(6, EventCardAction.Details), EventCardDirection.Up, 10, Unlocked,
            new(4, EventCardAction.Details), 2, scrollRow: 3);
        Expect(new(8, EventCardAction.Replay), EventCardDirection.Down, 10, Unlocked,
            new(8, EventCardAction.Replay), 2, scrollRow: 2);
        Expect(new(8, EventCardAction.Details), EventCardDirection.Down, 10, Locked,
            new(8, EventCardAction.Details), 2, scrollRow: 2);

        foreach (EventCardAction action in Enum.GetValues<EventCardAction>())
        {
            for (int index = 0; index < 9; index++)
            {
                int scroll = Math.Max(0, index / 2 - 2);
                int left = index % 2 == 1 ? index - 1 : index;
                int right = index % 2 == 0 && index + 1 < 9 ? index + 1 : index;
                Expect(new(index, action), EventCardDirection.Left, 9, Unlocked, new(left, action), scroll, scroll);
                Expect(new(index, action), EventCardDirection.Right, 9, Unlocked, new(right, action), scroll, scroll);
            }
        }
        Expect(new(2, EventCardAction.Replay), EventCardDirection.Right, 9, i => i != 3,
            new(3, EventCardAction.Details), 0);
        Expect(new(3, EventCardAction.Replay), EventCardDirection.Left, 9, i => i != 2,
            new(2, EventCardAction.Details), 0);
        Expect(new(7, EventCardAction.Replay), EventCardDirection.Down, 9, Unlocked,
            new(7, EventCardAction.Replay), 1, scrollRow: 1);

        // Exhaust all unlock patterns for a grid larger than the six-card viewport.
        for (int mask = 0; mask < 1 << 9; mask++)
        {
            bool IsUnlocked(int index) => (mask & (1 << index)) != 0;
            HashSet<EventCardFocus> visited = [new(0, EventCardAction.Details)];
            Queue<(EventCardFocus Focus, int ScrollRow)> pending = new();
            pending.Enqueue((new(0, EventCardAction.Details), 0));
            while (pending.TryDequeue(out var state))
            {
                foreach (EventCardDirection direction in Enum.GetValues<EventCardDirection>())
                {
                    var next = state.Focus.Navigate(direction, 9, IsUnlocked, state.ScrollRow);
                    Check(next.Focus.HasValue, "Nonempty grid lost focus.");
                    EventCardFocus focus = next.Focus!.Value;
                    Check(focus.EventIndex >= 0 && focus.EventIndex < 9, "Focus escaped grid.");
                    Check(focus.Action == EventCardAction.Details || IsUnlocked(focus.EventIndex), "Locked Replay received focus.");
                    Check(next.ScrollRow >= 0 && next.ScrollRow <= 2
                        && focus.EventIndex / 2 >= next.ScrollRow && focus.EventIndex / 2 < next.ScrollRow + 3,
                        "Focus is outside the normalized viewport.");
                    if (visited.Add(focus))
                        pending.Enqueue((focus, next.ScrollRow));
                }
            }
            for (int index = 0; index < 9; index++)
            {
                Check(visited.Contains(new(index, EventCardAction.Details)), $"Details {index} unreachable for mask {mask}.");
                if (IsUnlocked(index))
                {
                    Check(visited.Contains(new(index, EventCardAction.Replay)), $"Replay {index} unreachable for mask {mask}.");
                    var up = new EventCardFocus(index, EventCardAction.Replay).Navigate(EventCardDirection.Up, 9, IsUnlocked, 0);
                    Check(up.Focus == new EventCardFocus(index, EventCardAction.Details), "Replay Up must reach same-card Details.");
                }
            }
        }

        EventCardFocus stale = new(99, EventCardAction.Replay);
        Check(stale.Normalize(0, _ => throw new Exception("Empty grid queried unlock."), 99) == (null, 0), "Empty normalization failed.");
        Check(stale.Navigate(EventCardDirection.Down, 0, Unlocked, -1) == (null, 0), "Empty navigation failed.");
        Check(stale.Normalize(7, Locked, 99) == (new EventCardFocus(6, EventCardAction.Details), 1), "Shrinking grid normalization failed.");
        Check(new EventCardFocus(-1, EventCardAction.Details).Normalize(7, Unlocked, -99)
            == (new EventCardFocus(0, EventCardAction.Details), 0), "Negative state normalization failed.");
        Check(new EventCardFocus(4, EventCardAction.Details).Normalize(10, Unlocked, 1).ScrollRow == 1, "Visible focus moved scroll unnecessarily.");
        Check(new EventCardFocus(8, EventCardAction.Details).Normalize(10, Unlocked, 0, 1).ScrollRow == 4, "Single-row viewport failed.");
        Check(stale.Normalize(1, Locked, 99) == (new EventCardFocus(0, EventCardAction.Details), 0), "Single-card normalization failed.");

        const int componentBase = 2000;
        HashSet<int> ids = [];
        for (int index = 0; index < 5000; index++)
        {
            foreach (EventCardAction action in Enum.GetValues<EventCardAction>())
            {
                EventCardFocus focus = new(index, action);
                int id = focus.GetComponentId(componentBase);
                Check(id == componentBase + 2 * index + (int)action && ids.Add(id), "Component IDs collided.");
                Check(EventCardFocus.TryFromComponentId(id, componentBase, 5000, out var decoded) && decoded == focus,
                    "Component ID round-trip failed.");
            }
        }
        Check(!EventCardFocus.TryFromComponentId(componentBase - 1, componentBase, 5000, out _), "Accepted non-card ID.");
        Check(!EventCardFocus.TryFromComponentId(componentBase + 10000, componentBase, 5000, out _), "Accepted past-end ID.");
        Check(!EventCardFocus.TryFromComponentId(componentBase, componentBase, 0, out _), "Accepted ID for empty grid.");
        Throws<OverflowException>(() => stale.GetComponentId(int.MaxValue));
        Throws<ArgumentOutOfRangeException>(() => stale.Normalize(-1, Unlocked, 0));
        Throws<ArgumentOutOfRangeException>(() => stale.Normalize(1, Unlocked, 0, 0));
        Throws<ArgumentNullException>(() => stale.Normalize(1, null!, 0));
        Throws<ArgumentOutOfRangeException>(() => stale.Navigate((EventCardDirection)99, 1, Unlocked, 0));

        Console.WriteLine("Event card focus checks passed (including all 512 nine-card unlock patterns).");
    }

    private static void Expect(EventCardFocus start, EventCardDirection direction, int count,
        Func<int, bool> unlocked, EventCardFocus expected, int expectedScroll, int scrollRow = 0)
    {
        var actual = start.Navigate(direction, count, unlocked, scrollRow);
        Check(actual == (expected, expectedScroll), $"{start} {direction}: expected ({expected}, {expectedScroll}), got {actual}.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void Throws<T>(System.Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
