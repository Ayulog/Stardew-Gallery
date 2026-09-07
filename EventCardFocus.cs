namespace StardewGallery;

internal enum EventCardAction
{
    Details = 0,
    Replay = 1
}

internal enum EventCardDirection
{
    Up,
    Right,
    Down,
    Left
}

internal readonly record struct EventCardFocus(int EventIndex, EventCardAction Action)
{
    internal const int Columns = 2;
    internal const int DefaultVisibleRows = 3;

    internal int GetComponentId(int componentBase)
    {
        if (EventIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(EventIndex));
        if (Action is not (EventCardAction.Details or EventCardAction.Replay))
            throw new ArgumentOutOfRangeException(nameof(Action));
        if (componentBase < 0)
            throw new ArgumentOutOfRangeException(nameof(componentBase));
        return checked(componentBase + 2 * EventIndex + (int)Action);
    }

    internal static bool TryFromComponentId(int componentId, int componentBase, int eventCount, out EventCardFocus focus)
    {
        focus = default;
        if (componentBase < 0 || componentId < componentBase || eventCount <= 0)
            return false;
        int offset = componentId - componentBase;
        int index = offset / 2;
        if (index >= eventCount)
            return false;
        focus = new EventCardFocus(index, (EventCardAction)(offset % 2));
        return true;
    }

    internal (EventCardFocus? Focus, int ScrollRow) Normalize(
        int eventCount, Func<int, bool> isUnlocked, int scrollRow, int visibleRows = DefaultVisibleRows)
    {
        if (eventCount < 0)
            throw new ArgumentOutOfRangeException(nameof(eventCount));
        if (visibleRows <= 0)
            throw new ArgumentOutOfRangeException(nameof(visibleRows));
        ArgumentNullException.ThrowIfNull(isUnlocked);
        if (Action is not (EventCardAction.Details or EventCardAction.Replay))
            throw new ArgumentOutOfRangeException(nameof(Action));
        if (eventCount == 0)
            return (null, 0);

        int index = Math.Clamp(EventIndex, 0, eventCount - 1);
        EventCardAction action = Action == EventCardAction.Replay && !isUnlocked(index)
            ? EventCardAction.Details : Action;
        int row = index / Columns;
        int totalRows = (eventCount - 1) / Columns + 1;
        int maxScroll = Math.Max(0, totalRows - visibleRows);
        int firstVisibleRow = Math.Clamp(scrollRow, 0, maxScroll);
        firstVisibleRow = Math.Clamp(firstVisibleRow, Math.Max(0, row - visibleRows + 1), Math.Min(row, maxScroll));
        return (new EventCardFocus(index, action), firstVisibleRow);
    }

    internal (EventCardFocus? Focus, int ScrollRow) Navigate(
        EventCardDirection direction, int eventCount, Func<int, bool> isUnlocked,
        int scrollRow, int visibleRows = DefaultVisibleRows)
    {
        if (direction is not (EventCardDirection.Up or EventCardDirection.Right or EventCardDirection.Down or EventCardDirection.Left))
            throw new ArgumentOutOfRangeException(nameof(direction));
        var normalized = Normalize(eventCount, isUnlocked, scrollRow, visibleRows);
        if (normalized.Focus is not EventCardFocus current)
            return normalized;

        int index = current.EventIndex;
        EventCardAction action = current.Action;
        switch (direction)
        {
            case EventCardDirection.Up:
                if (action == EventCardAction.Replay)
                    action = EventCardAction.Details;
                else if (index >= Columns)
                    index -= Columns;
                break;
            case EventCardDirection.Down:
                if (action == EventCardAction.Details && isUnlocked(index))
                    action = EventCardAction.Replay;
                else if (index < eventCount - Columns)
                    index += Columns;
                break;
            case EventCardDirection.Left:
                if (index % Columns != 0)
                    index--;
                break;
            case EventCardDirection.Right:
                if (index % Columns == 0 && index < eventCount - 1)
                    index++;
                break;
        }

        return new EventCardFocus(index, action).Normalize(eventCount, isUnlocked, normalized.ScrollRow, visibleRows);
    }

    internal EventCardFocus? ClampToViewport(int eventCount, Func<int, bool> isUnlocked, int scrollRow)
    {
        if (eventCount <= 0)
            return null;
        int maxScroll = Math.Max(0, (eventCount - 1) / Columns + 1 - DefaultVisibleRows);
        int firstRow = Math.Clamp(scrollRow, 0, maxScroll);
        int row = Math.Clamp(Math.Max(0, EventIndex) / Columns, firstRow, Math.Min(firstRow + DefaultVisibleRows - 1, (eventCount - 1) / Columns));
        int index = Math.Min(row * Columns + Math.Max(0, EventIndex) % Columns, eventCount - 1);
        return new EventCardFocus(index, Action == EventCardAction.Replay && isUnlocked(index) ? EventCardAction.Replay : EventCardAction.Details);
    }
}
