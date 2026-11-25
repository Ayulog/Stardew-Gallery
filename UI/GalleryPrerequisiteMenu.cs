using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace StardewGallery;

/// <summary>
/// Full <c>1672x941</c> leather-bound detail spread (<c>GalleryEventDetail-v3.png</c>) for prerequisite markers:
/// left wax-sealed envelope window + 6 filled plaques + bottom-left chapter ribbons; right title plaque, metadata
/// header with Rename button &amp; envelope window, scrollable rule/condition checklist with single-line jump bars,
/// and wide Back + disabled Replay footer buttons.
/// </summary>
internal sealed class GalleryPrerequisiteMenu : IClickableMenu
{
    private const int RowPadding = 10, StatusSize = 28, StatusGap = 12, ScrollStep = 96;
    private const int BackComponentId = 1000, RenameComponentId = 1005, JumpComponentBase = 2000;
    private static readonly RasterizerState ClipRasterizer = new() { ScissorTestEnable = true };

    private sealed record PrereqRow(
        string Text,
        ConditionStatusIcon? Status = null,
        string? Note = null,
        Action? Activate = null,
        bool IsSectionHeader = false);

    private readonly GalleryViewContext context;
    private readonly GalleryPageState state;
    private readonly ITranslationHelper i18n;
    private readonly PrerequisiteEvent entry;
    private readonly List<PrereqRow> rows = [];
    private readonly List<Rectangle> rowRects = [];
    private readonly bool obtained;
    private readonly string primarySourceSummary;
    private readonly string triggerSummary;

    private float menuScale = 1f;
    private int drawOffsetX, drawOffsetY, viewportWidth, viewportHeight;
    private int scroll, contentHeight;
    private bool draggingScrollbar, keyboardFocus;
    private int scrollbarGrabOffset;
    private Point lastMouse;
    private Rectangle contentBounds, scrollTrack, scrollThumb, backBounds, replayBounds;

    private static Rectangle RenameBounds => new(755, 246, 224, 42);

    internal GalleryPrerequisiteMenu(GalleryViewContext context, GalleryPageState state)
        : base(0, 0, GalleryDrawing.MenuWidth, GalleryDrawing.MenuHeight, true)
    {
        this.context = context;
        this.state = state;
        i18n = context.I18n;
        entry = context.Catalog.FindPrerequisite(state.PrerequisiteId!)
            ?? throw new ArgumentException("Missing prerequisite.", nameof(state));
        obtained = Game1.player?.eventsSeen.Contains(entry.EventId) == true;
        primarySourceSummary = BuildPrimarySourceSummary();
        triggerSummary = BuildTriggerSummary();

        BuildRows();
        scroll = state.Scroll;
        int initialFocus = state.Focus;
        RecalculateLayout();
        if (initialFocus >= 0)
            FocusComponent(initialFocus);
        else
            FocusComponent(FirstJumpComponentId() ?? BackComponentId);
    }

    private string BuildPrimarySourceSummary()
    {
        foreach (MarkerAction source in entry.Sources)
        {
            EventOriginMatch origin = context.Catalog.RuleOrigins.GetValueOrDefault(source.RuleId) ?? EventOriginMatch.Unknown;
            if (origin.Provider is { } owner)
            {
                string name = owner.IsGameBase ? i18n.Get("source.game") : owner.Name;
                return i18n.Get("source.summary", new { provider = name });
            }
            if (origin.Ambiguous)
                return i18n.Get("source.summary", new { provider = i18n.Get("source.origin-ambiguous") });
        }
        if (entry.InternalSources.Count > 0)
            return context.EventSource(entry.InternalSources[0].Resolved).Summary;
        return i18n.Get("source.summary", new { provider = i18n.Get("source.unknown") });
    }

    private string BuildTriggerSummary()
    {
        string[] triggers = entry.Sources
            .SelectMany(source => source.Triggers.Select(t => Term("trigger", t)))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.CurrentCulture)
            .ToArray();
        if (triggers.Length > 0)
            return string.Join(", ", triggers);
        if (entry.InternalSources.Count > 0)
            return string.Join(", ", entry.InternalSources.Select(s => context.Locations.Get(s.LocationName)).Distinct(StringComparer.CurrentCulture));
        return i18n.Get("nav.read-only");
    }

    private void BuildRows()
    {
        rows.Clear();
        rows.Add(new(i18n.Get(obtained ? "prerequisite.complete" : "prerequisite.incomplete"),
            Status: obtained ? ConditionStatusIcon.Check : ConditionStatusIcon.Cross));

        foreach (MarkerAction source in entry.Sources)
        {
            rows.Add(new(i18n.Get(source.SetsMarker ? "prerequisite.set" : "prerequisite.remove", new { rule = source.RuleId }),
                IsSectionHeader: true));
            foreach (string line in EventOriginPresentation.BuildRule(
                context.Catalog.RuleOrigins.GetValueOrDefault(source.RuleId) ?? EventOriginMatch.Unknown,
                (key, args) => i18n.Get(key, args)))
            {
                rows.Add(new(line));
            }
            rows.Add(new(i18n.Get("prerequisite.trigger", new
            {
                triggers = string.Join(", ", source.Triggers.Select(trigger => Term("trigger", trigger))),
                player = Term("player", source.Player)
            })));
            if (source.HostOnly)
                rows.Add(new(i18n.Get("prerequisite.host"),
                    Status: Game1.IsMasterGame ? ConditionStatusIcon.Check : ConditionStatusIcon.Unknown));
            bool alreadyRun = Game1.player?.triggerActionsRun.Contains(source.RuleId) == true;
            rows.Add(new(i18n.Get("prerequisite.not-run"),
                Status: alreadyRun ? ConditionStatusIcon.Cross : source.HostOnly && !Game1.IsMasterGame ? ConditionStatusIcon.Unknown : ConditionStatusIcon.Check));
            if (source.SkipCondition is not null)
            {
                rows.Add(new(i18n.Get("prerequisite.skip"), IsSectionHeader: true));
                AddQueryConditions(source.SkipCondition, source.HostOnly && !Game1.IsMasterGame);
            }
            rows.Add(new(i18n.Get("event-detail.requirements"), IsSectionHeader: true));
            AddQueryConditions(source.Condition, source.HostOnly && !Game1.IsMasterGame);
            if (source.MailIds.Count > 0)
                rows.Add(new(i18n.Get("prerequisite.mail", new { ids = string.Join(", ", source.MailIds) })));
        }

        foreach (GalleryEvent flow in entry.InternalSources)
        {
            rows.Add(new(context.Locations.Get(flow.LocationName), IsSectionHeader: true));
            foreach (ConditionDisplayItem condition in GalleryConditionPresentation.Build(flow, i18n))
                AddConditionRow(condition);
        }

        foreach (GalleryEvent story in entry.Dependents)
        {
            GalleryEvent captured = story;
            string storyTitle = context.CardTitle(captured);
            string location = context.Locations.Get(captured.LocationName);
            string label = i18n.Get("prerequisite.next", new { name = storyTitle, location }) + "  >";
            rows.Add(new(label, Activate: () =>
            {
                SaveState();
                context.Navigation.OpenEvent(captured.Resolved.Identity);
            }));
        }
    }

    private void AddQueryConditions(string raw, bool forceUnknown)
    {
        IReadOnlyList<ConditionDisplayItem> list = GalleryConditionPresentation.Query(raw, i18n);
        if (list.Count == 0)
        {
            rows.Add(new(i18n.Get("condition.none"), Status: forceUnknown ? ConditionStatusIcon.Unknown : ConditionStatusIcon.Check));
            return;
        }
        foreach (ConditionDisplayItem condition in list)
            AddConditionRow(condition, forceUnknown);
    }

    private void AddConditionRow(ConditionDisplayItem condition, bool forceUnknown = false)
    {
        ConditionStatusIcon status = forceUnknown ? ConditionStatusIcon.Unknown : ConditionRowPresentation.Status(condition.Evaluation);
        string? reasonKey = status == ConditionStatusIcon.Unknown ? ConditionRowPresentation.UnknownReasonKey(condition) : null;
        string? note = reasonKey is not null ? i18n.Get(reasonKey).ToString() : null;
        string text = ConditionRowPresentation.Text(condition, (k, a) => i18n.Get(k, a));
        rows.Add(new(text, Status: status, Note: note));

        foreach (string id in GalleryEventNavigation.References(condition.Expression).Distinct(StringComparer.Ordinal))
        {
            string targetId = id;
            StoryDependencyResult dependency = StoryDependencyLookup.Find(context.Catalog, targetId);
            if (dependency.Prerequisite is { } prereq && prereq.EventId != entry.EventId)
            {
                string pTitle = PrerequisitePresentation.Title(prereq, context);
                string pDisplay = pTitle == prereq.EventId ? "ID " + targetId : $"{pTitle} (ID {targetId})";
                rows.Add(new($"{i18n.Get("query.kind-prerequisite")}: {pDisplay}  >",
                    Activate: () =>
                    {
                        SaveState();
                        context.Navigation.OpenPrerequisite(targetId);
                    }));
            }
            foreach (GalleryEvent story in dependency.Stories)
            {
                GalleryEvent captured = story;
                string storyLabel = $"{context.CardTitle(captured)} (ID {targetId}) · {context.Locations.Get(captured.LocationName)}";
                rows.Add(new($"{i18n.Get("nav.other-event")}: {storyLabel}  >",
                    Activate: () =>
                    {
                        SaveState();
                        context.Navigation.OpenEvent(captured.Resolved.Identity);
                    }));
            }
        }
    }

    private string Term(string group, string value)
    {
        var text = i18n.Get(group + "." + value.ToLowerInvariant());
        return text.HasValue() ? text.ToString() : value;
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        base.gameWindowSizeChanged(oldBounds, newBounds);
        RecalculateLayout();
    }

    public override void update(GameTime time)
    {
        base.update(time);
        SaveState();
    }

    public override void performHoverAction(int x, int y)
    {
        if (lastMouse != new Point(x, y))
        {
            if (lastMouse != default) keyboardFocus = false;
            lastMouse = new Point(x, y);
        }
        base.performHoverAction(x, y);
    }

    public override void receiveScrollWheelAction(int direction)
    {
        ScrollBy(direction < 0 ? ScrollStep : -ScrollStep);
        Game1.playSound("shiny4");
    }

    public override void receiveRightClick(int x, int y, bool playSound = true) => Return();

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        (x, y) = ToLogical(x, y);
        if (upperRightCloseButton?.bounds.Contains(x, y) == true)
        {
            SaveState();
            context.Navigation.Close();
            return;
        }
        if (GalleryBookChrome.TryClickRibbonTab(x, y, state.OriginTab, context.Navigation, () => SaveState(), albumPage: true))
            return;
        if (MaxScroll > 0 && scrollThumb.Contains(x, y))
        {
            draggingScrollbar = true;
            scrollbarGrabOffset = y - scrollThumb.Y;
            return;
        }
        if (MaxScroll > 0 && scrollTrack.Contains(x, y))
        {
            ScrollBy(y < scrollThumb.Y ? -contentBounds.Height : contentBounds.Height);
            return;
        }
        if (RenameBounds.Contains(x, y))
        {
            FocusComponent(RenameComponentId);
            SaveState(RenameComponentId);
            Game1.playSound("smallSelect");
            context.Navigation.Rename(entry.Identity);
            return;
        }
        if (backBounds.Contains(x, y))
        {
            Return();
            return;
        }
        if (contentBounds.Contains(x, y))
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Activate is not null && RowBounds(i).Contains(x, y))
                {
                    FocusComponent(JumpComponentBase + i);
                    Game1.playSound("smallSelect");
                    rows[i].Activate!.Invoke();
                    return;
                }
            }
        }
    }

    public override void leftClickHeld(int x, int y)
    {
        base.leftClickHeld(x, y);
        if (!draggingScrollbar || MaxScroll <= 0 || scrollTrack.Height <= scrollThumb.Height)
            return;
        (_, int ly) = ToLogical(x, y);
        float ratio = Math.Clamp((ly - scrollbarGrabOffset - scrollTrack.Y) / (float)(scrollTrack.Height - scrollThumb.Height), 0f, 1f);
        ScrollBy((int)Math.Round(ratio * MaxScroll) - scroll);
    }

    public override void releaseLeftClick(int x, int y)
    {
        base.releaseLeftClick(x, y);
        draggingScrollbar = false;
    }

    public override void receiveKeyPress(Keys key)
    {
        if (key is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Enter or Keys.PageUp or Keys.PageDown)
            keyboardFocus = true;
        if (key == Keys.Escape || Game1.options.menuButton.Any(binding => binding.key == key))
        {
            Return();
            return;
        }
        if (key is Keys.PageUp or Keys.PageDown)
        {
            ScrollBy(key == Keys.PageUp ? -contentBounds.Height / 2 : contentBounds.Height / 2);
            return;
        }
        if (key == Keys.Enter)
        {
            ActivateFocused(currentlySnappedComponent?.myID ?? BackComponentId);
            return;
        }
        if (key is Keys.Up or Keys.Right or Keys.Down or Keys.Left)
        {
            applyMovementKey(key switch { Keys.Up => 0, Keys.Right => 1, Keys.Down => 2, _ => 3 });
            return;
        }
        base.receiveKeyPress(key);
    }

    public override void receiveGamePadButton(Buttons button)
    {
        if (button == Buttons.B)
        {
            Return();
            return;
        }
        if (button is Buttons.LeftShoulder or Buttons.RightShoulder)
        {
            ScrollBy(button == Buttons.LeftShoulder ? -contentBounds.Height / 2 : contentBounds.Height / 2);
            return;
        }
        base.receiveGamePadButton(button);
    }

    public override void applyMovementKey(int direction)
    {
        if (direction is < 0 or > 3)
            return;
        int id = currentlySnappedComponent?.myID ?? BackComponentId;
        int[] jumpIds = Enumerable.Range(0, rows.Count).Where(i => rows[i].Activate is not null).Select(i => JumpComponentBase + i).ToArray();

        if (id >= GalleryBookChrome.RibbonTabBaseId && id < GalleryBookChrome.RibbonTabBaseId + GallerySpreadLayout.RibbonTabCount)
        {
            int tab = id - GalleryBookChrome.RibbonTabBaseId;
            if (direction == 3 && tab > 0) FocusComponent(GalleryBookChrome.RibbonTabBaseId + tab - 1);
            else if (direction == 1 && tab + 1 < GallerySpreadLayout.RibbonTabCount) FocusComponent(GalleryBookChrome.RibbonTabBaseId + tab + 1);
            else if (direction == 1) FocusComponent(BackComponentId);
            else if (direction == 0) FocusComponent(RenameComponentId);
            return;
        }

        if (id == RenameComponentId)
        {
            if (direction == 2)
                FocusComponent(jumpIds.Length > 0 ? jumpIds[0] : BackComponentId);
            else if (direction == 3)
                FocusComponent(GalleryBookChrome.RibbonTabBaseId + 2);
            return;
        }

        if (id >= JumpComponentBase)
        {
            int pos = Array.IndexOf(jumpIds, id);
            if (direction == 0)
            {
                if (pos > 0) FocusComponent(jumpIds[pos - 1]);
                else { ScrollBy(-ScrollStep); FocusComponent(RenameComponentId); }
            }
            else if (direction == 2)
            {
                if (pos >= 0 && pos + 1 < jumpIds.Length) FocusComponent(jumpIds[pos + 1]);
                else FocusComponent(BackComponentId);
            }
            else if (direction == 3)
                FocusComponent(GalleryBookChrome.RibbonTabBaseId + 2);
            return;
        }

        if (id == BackComponentId)
        {
            if (direction == 3)
                FocusComponent(GalleryBookChrome.RibbonTabBaseId + 2);
            else if (direction == 0)
            {
                if (jumpIds.Length > 0) FocusComponent(jumpIds[^1]);
                else if (MaxScroll > 0) ScrollBy(-ScrollStep);
                else FocusComponent(RenameComponentId);
            }
        }
    }

    private void ActivateFocused(int id)
    {
        if (id >= GalleryBookChrome.RibbonTabBaseId && id < GalleryBookChrome.RibbonTabBaseId + GallerySpreadLayout.RibbonTabCount)
        {
            SaveState();
            Game1.playSound("shwip");
            context.Navigation.SwitchTab((GalleryBookTab)(id - GalleryBookChrome.RibbonTabBaseId));
            return;
        }
        if (id == RenameComponentId)
        {
            SaveState(RenameComponentId);
            Game1.playSound("smallSelect");
            context.Navigation.Rename(entry.Identity);
            return;
        }
        if (id == BackComponentId)
        {
            Return();
            return;
        }
        if (id >= JumpComponentBase && id - JumpComponentBase < rows.Count && rows[id - JumpComponentBase].Activate is { } act)
        {
            Game1.playSound("smallSelect");
            act();
        }
    }

    public override void populateClickableComponentList() => BuildClickableComponents();

    public override void snapToDefaultClickableComponent()
        => FocusComponent(FirstJumpComponentId() ?? BackComponentId);

    public override void draw(SpriteBatch b)
    {
        if (GalleryLayout.Changed(viewportWidth, viewportHeight, Game1.uiViewport.Width, Game1.uiViewport.Height))
            RecalculateLayout();

        b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * .45f);
        (int mouseX, int mouseY) = ToLogical(Game1.getMouseX(true), Game1.getMouseY(true));
        bool showFocus = Game1.options.snappyMenus && Game1.options.gamepadControls || keyboardFocus;
        int focusedId = currentlySnappedComponent?.myID ?? BackComponentId;

        GalleryDrawing.BeginScaled(b, menuScale, drawOffsetX, drawOffsetY);

        string statusLabel = i18n.Get(obtained ? "prerequisite.complete" : "prerequisite.incomplete");
        Rectangle leftWindow = GalleryDrawing.Inset(GalleryBookChrome.ToRect(GallerySpreadLayout.PortraitBounds), -4, -4);
        Rectangle rightWindow = GalleryBookChrome.ToRect(GallerySpreadLayout.DetailThumbnailBounds);
        GalleryBookChrome.DrawLockedStoryEnvelope(b, leftWindow, "ID " + entry.EventId, statusLabel, parallel: obtained);
        GalleryBookChrome.DrawLockedStoryEnvelope(b, rightWindow, "ID " + entry.EventId, statusLabel, parallel: obtained);

        b.Draw(context.Textures.Detail, new Rectangle(0, 0, width, height), Color.White);
        GalleryUiSkin.DrawPill(b, new Point(rightWindow.X + 8, rightWindow.Y + 8), 26, statusLabel,
            obtained ? UiPill.Amber : UiPill.Wood, maxWidth: 200);

        GalleryBookChrome.DrawRibbonTabs(b, state.OriginTab, focusedId, showFocus, new Point(mouseX, mouseY), i18n, out string? hoverTooltip, albumPage: true);
        GalleryBookChrome.DrawPageTitle(b, i18n.Get("query.kind-prerequisite"), menuScale, drawOffsetX, drawOffsetY);

        DrawLeftPlaques(b);
        DrawMetadataHeader(b, mouseX, mouseY, showFocus, focusedId);

        if (MaxScroll > 0)
        {
            GalleryDrawing.DrawScrollbarTrack(b, context.Textures.Scrollbar, scrollTrack);
            GalleryDrawing.DrawScrollbar(b, scrollThumb);
        }

        BeginContentClip(b);
        DrawRows(b, mouseX, mouseY, showFocus, focusedId);
        EndContentClip(b);

        bool backHot = backBounds.Contains(mouseX, mouseY) || showFocus && focusedId == BackComponentId;
        GalleryDrawing.DrawButton(b, backBounds, i18n.Get("nav.back"), backHot);
        if (showFocus && focusedId == BackComponentId)
            GalleryUiSkin.DrawFocusCorners(b, GalleryDrawing.Inset(backBounds, -4, -4), 12);

        GalleryUiSkin.DrawWoodButton(b, replayBounds, i18n.Get("event.replay"), UiButtonState.Disabled, leadingIcon: UiHudIcon.FastForward);

        upperRightCloseButton?.draw(b);
        GalleryDrawing.EndScaled(b);

        if (hoverTooltip is null)
        {
            if (replayBounds.Contains(mouseX, mouseY))
                hoverTooltip = i18n.Get("replay.internal");
            else if (GalleryBookChrome.ToRect(GallerySpreadLayout.LeftRowBounds(1)).Contains(mouseX, mouseY)
                || GalleryBookChrome.ToRect(GallerySpreadLayout.DetailEventIdBounds).Contains(mouseX, mouseY))
                hoverTooltip = PrerequisitePresentation.Title(entry, context) + "\n" + i18n.Get("event-detail.event-id", new { id = entry.EventId });
        }
        if (hoverTooltip is not null)
            GalleryDrawing.DrawHoverTextSafe(b, GalleryDrawing.ParseTextSafe(hoverTooltip, Game1.smallFont, Math.Max(180, Math.Min(560, viewportWidth - 80))), Game1.smallFont);
        drawMouse(b);
    }

    private void DrawLeftPlaques(SpriteBatch b)
    {
        string[] lines =
        [
            i18n.Get("query.kind-prerequisite"),
            i18n.Get("event-detail.event-id", new { id = entry.EventId }),
            i18n.Get(obtained ? "prerequisite.complete" : "prerequisite.incomplete"),
            primarySourceSummary,
            triggerSummary,
            i18n.Get("replay.internal")
        ];
        float rowScale = Math.Min(0.9f, 34f / Math.Max(1f, Game1.smallFont.LineSpacing));
        for (int i = 0; i < lines.Length; i++)
        {
            Rectangle area = GalleryDrawing.Inset(GalleryBookChrome.ToRect(GallerySpreadLayout.LeftRowBounds(i)), 34, 4);
            string safeLine = GalleryDrawing.SanitizeForFont(Game1.smallFont, lines[i]);
            string fitted = GalleryTextFit.Ellipsize(safeLine, area.Width / rowScale, s => GalleryDrawing.MeasureStringSafe(Game1.smallFont, s).X);
            Vector2 size = GalleryDrawing.MeasureStringSafe(Game1.smallFont, fitted) * rowScale;
            GalleryDrawing.DrawStringSafe(b, Game1.smallFont, fitted, new Vector2(area.Center.X - size.X / 2f, area.Center.Y - size.Y / 2f),
                GallerySpreadDrawing.Ink, 0f, Vector2.Zero, rowScale, SpriteEffects.None, 0f);
        }
    }

    private void DrawMetadataHeader(SpriteBatch b, int mouseX, int mouseY, bool showFocus, int focusedId)
    {
        string titleLine = PrerequisitePresentation.Title(entry, context);
        if (titleLine != entry.EventId)
            titleLine += GalleryDrawing.TextSeparator + "ID " + entry.EventId;
        else
            titleLine = "ID " + entry.EventId;

        string statusAndTrigger = i18n.Get(obtained ? "prerequisite.complete" : "prerequisite.incomplete")
            + GalleryDrawing.TextSeparator + triggerSummary;

        float lineScale = Math.Min(0.94f, 26f / Math.Max(1f, Game1.smallFont.LineSpacing));
        GalleryDrawing.DrawEllipsized(b, titleLine, GalleryBookChrome.ToRect(GallerySpreadLayout.DetailEventIdBounds), lineScale);
        GalleryDrawing.DrawEllipsized(b, statusAndTrigger, GalleryBookChrome.ToRect(GallerySpreadLayout.DetailLocationBounds), lineScale);
        GalleryDrawing.DrawEllipsized(b, primarySourceSummary, GalleryBookChrome.ToRect(GallerySpreadLayout.DetailSourceBounds), lineScale);

        bool renameFocused = showFocus && focusedId == RenameComponentId;
        bool renameHot = RenameBounds.Contains(mouseX, mouseY) || renameFocused;
        GalleryDrawing.DrawButton(b, RenameBounds, i18n.Get("name.edit"), renameHot);
        if (renameFocused)
            GalleryUiSkin.DrawFocusCorners(b, GalleryDrawing.Inset(RenameBounds, -4, -4), 10);

        Rectangle headingBounds = GalleryBookChrome.ToRect(GallerySpreadLayout.ConditionHeadingBounds);
        GalleryDrawing.DrawLeftFitted(b, i18n.Get("event-detail.requirements"), headingBounds, GallerySpreadDrawing.Ink);
    }

    private void DrawRows(SpriteBatch b, int mouseX, int mouseY, bool showFocus, int focusedId)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            Rectangle r = RowBounds(i);
            if (!r.Intersects(contentBounds))
                continue;
            PrereqRow row = rows[i];
            if (row.Activate is not null)
            {
                bool hovered = contentBounds.Contains(mouseX, mouseY) && r.Contains(mouseX, mouseY);
                bool focused = showFocus && focusedId == JumpComponentBase + i;
                Rectangle bar = new(r.X + 4, r.Y + 4, r.Width - 8, r.Height - 8);
                b.Draw(Game1.staminaRect, bar, hovered || focused ? new Color(246, 218, 146) * 0.88f : new Color(236, 220, 186) * 0.68f);
                GalleryBookChrome.DrawBorder(b, bar, new Color(188, 152, 102) * 0.8f, 1);
                if (focused)
                    GalleryUiSkin.DrawFocusCorners(b, GalleryDrawing.Inset(bar, -2, -2), 8);
                float jumpScale = Math.Min(0.9f, (bar.Height - 6f) / Math.Max(1f, Game1.smallFont.LineSpacing));
                GalleryDrawing.DrawEllipsized(b, row.Text, new Rectangle(bar.X + 12, bar.Y, bar.Width - 24, bar.Height), jumpScale);
                continue;
            }

            if (i > 0)
                b.Draw(Game1.staminaRect, new Rectangle(r.X, r.Y, r.Width, 1), GallerySpreadDrawing.LightInk);

            int textWidth = r.Width - RowPadding * 2 - (row.Status is not null ? StatusSize + StatusGap : 0);
            string wrapped = GalleryDrawing.ParseTextSafe(row.Text, Game1.smallFont, Math.Max(80, textWidth));
            Color ink = row.IsSectionHeader ? GallerySpreadDrawing.Ink : Game1.textColor;
            GalleryDrawing.DrawStringSafe(b, Game1.smallFont, wrapped, new Vector2(r.X + RowPadding, r.Y + RowPadding), ink);

            if (row.Note is { Length: > 0 } note)
            {
                int bodyH = wrapped.Split('\n').Length * Game1.smallFont.LineSpacing;
                string wrappedNote = GalleryDrawing.ParseTextSafe(note, Game1.smallFont, Math.Max(80, textWidth));
                GalleryDrawing.DrawStringSafe(b, Game1.smallFont, wrappedNote, new Vector2(r.X + RowPadding, r.Y + RowPadding + bodyH + 6), new Color(125, 90, 35));
            }

            if (row.Status is { } status)
            {
                Rectangle src = GalleryBookChrome.ToRect(status switch
                {
                    ConditionStatusIcon.Check => GallerySpreadLayout.ConditionCheckSource,
                    ConditionStatusIcon.Cross => GallerySpreadLayout.ConditionCrossSource,
                    _ => GallerySpreadLayout.ConditionQuestionSource
                });
                b.Draw(context.Textures.ConditionIcons,
                    new Rectangle(r.Right - RowPadding - StatusSize, r.Y + r.Height / 2 - StatusSize / 2, StatusSize, StatusSize),
                    src,
                    Color.White);
            }
        }
    }

    private void BeginContentClip(SpriteBatch b)
    {
        b.End();
        b.GraphicsDevice.ScissorRectangle = Rectangle.Intersect(ToScreen(contentBounds), b.GraphicsDevice.Viewport.Bounds);
        Matrix transform = Matrix.CreateScale(menuScale) * Matrix.CreateTranslation(drawOffsetX, drawOffsetY, 0f);
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, ClipRasterizer, null, transform);
    }

    private void EndContentClip(SpriteBatch b)
    {
        b.End();
        b.GraphicsDevice.ScissorRectangle = new Rectangle(0, 0, b.GraphicsDevice.PresentationParameters.BackBufferWidth, b.GraphicsDevice.PresentationParameters.BackBufferHeight);
        Matrix transform = Matrix.CreateScale(menuScale) * Matrix.CreateTranslation(drawOffsetX, drawOffsetY, 0f);
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);
    }

    private void RecalculateLayout()
    {
        width = GalleryDrawing.MenuWidth;
        height = GalleryDrawing.MenuHeight;
        xPositionOnScreen = yPositionOnScreen = 0;
        viewportWidth = Game1.uiViewport.Width;
        viewportHeight = Game1.uiViewport.Height;
        menuScale = (float)GalleryLayout.ScaleToFit(viewportWidth, viewportHeight, width, height, 24);
        drawOffsetX = (int)Math.Round((viewportWidth - width * menuScale) / 2f);
        drawOffsetY = (int)Math.Round((viewportHeight - height * menuScale) / 2f);

        contentBounds = GalleryBookChrome.ToRect(GallerySpreadLayout.ConditionViewportBounds);
        scrollTrack = GalleryBookChrome.ToRect(GallerySpreadLayout.DetailScrollTrackBounds);
        backBounds = GalleryBookChrome.ToRect(GallerySpreadLayout.DetailBackBounds);
        replayBounds = GalleryBookChrome.ToRect(GallerySpreadLayout.DetailReplayBounds);

        initializeUpperRightCloseButton();
        GalleryBookChrome.PositionCloseButton(upperRightCloseButton, menuScale, drawOffsetX, drawOffsetY, viewportWidth, viewportHeight);

        rowRects.Clear();
        int y = contentBounds.Y;
        foreach (PrereqRow row in rows)
        {
            int h;
            if (row.Activate is not null)
            {
                h = 46;
            }
            else
            {
                int textW = contentBounds.Width - RowPadding * 2 - (row.Status is not null ? StatusSize + StatusGap : 0);
                int linesH = GalleryDrawing.ParseTextSafe(row.Text, Game1.smallFont, Math.Max(80, textW)).Split('\n').Length * Game1.smallFont.LineSpacing;
                if (row.Note is { Length: > 0 } note)
                    linesH += 6 + GalleryDrawing.ParseTextSafe(note, Game1.smallFont, Math.Max(80, textW)).Split('\n').Length * Game1.smallFont.LineSpacing;
                h = Math.Max(StatusSize, linesH) + RowPadding * 2;
            }
            rowRects.Add(new Rectangle(contentBounds.X, y, contentBounds.Width, h));
            y += h;
        }
        contentHeight = Math.Max(0, y - contentBounds.Y);
        scroll = Math.Clamp(scroll, 0, MaxScroll);
        UpdateScrollbar();
        BuildClickableComponents();
    }

    private int MaxScroll => Math.Max(0, contentHeight - contentBounds.Height);

    private Rectangle RowBounds(int index)
    {
        Rectangle r = rowRects[index];
        r.Y -= scroll;
        return r;
    }

    private void ScrollBy(int delta)
    {
        int next = Math.Clamp(scroll + delta, 0, MaxScroll);
        if (next == scroll)
            return;
        scroll = next;
        UpdateScrollbar();
        BuildClickableComponents();
    }

    private void UpdateScrollbar()
    {
        int thumbH = Math.Max(42, contentBounds.Height * contentBounds.Height / Math.Max(contentBounds.Height, Math.Max(1, contentHeight)));
        int thumbY = scrollTrack.Y + (MaxScroll == 0 ? 0 : (scrollTrack.Height - thumbH) * scroll / MaxScroll);
        scrollThumb = new Rectangle(scrollTrack.X, thumbY, scrollTrack.Width, thumbH);
    }

    private int? FirstJumpComponentId()
    {
        for (int i = 0; i < rows.Count; i++)
            if (rows[i].Activate is not null)
                return JumpComponentBase + i;
        return null;
    }

    private void BuildClickableComponents()
    {
        int previous = currentlySnappedComponent?.myID ?? BackComponentId;
        allClickableComponents =
        [
            new ClickableComponent(ToScreen(backBounds), "back") { myID = BackComponentId },
            new ClickableComponent(ToScreen(RenameBounds), "rename") { myID = RenameComponentId }
        ];
        GalleryBookChrome.AddRibbonTabComponents(allClickableComponents, ToScreen, BackComponentId, albumPage: true, upNeighborId: RenameComponentId);

        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Activate is null)
                continue;
            Rectangle vis = Rectangle.Intersect(RowBounds(i), contentBounds);
            Rectangle target = vis.Height > 0 ? vis : new Rectangle(contentBounds.X + 8, contentBounds.Y + 8, contentBounds.Width - 16, 36);
            allClickableComponents.Add(new ClickableComponent(ToScreen(target), $"jump-{i}") { myID = JumpComponentBase + i });
        }

        currentlySnappedComponent = allClickableComponents.FirstOrDefault(c => c.myID == previous)
            ?? allClickableComponents.FirstOrDefault(c => c.myID == BackComponentId);
        SaveState();
    }

    private void FocusComponent(int id)
    {
        if (id >= JumpComponentBase && id - JumpComponentBase < rowRects.Count)
        {
            Rectangle r = RowBounds(id - JumpComponentBase);
            if (r.Top < contentBounds.Top)
                ScrollBy(r.Top - contentBounds.Top);
            else if (r.Bottom > contentBounds.Bottom)
                ScrollBy(r.Bottom - contentBounds.Bottom);
        }
        currentlySnappedComponent = allClickableComponents.FirstOrDefault(c => c.myID == id)
            ?? allClickableComponents.FirstOrDefault(c => c.myID == BackComponentId);
        SaveState();
        if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            snapCursorToCurrentSnappedComponent();
    }

    private void SaveState(int? focusOverride = null)
    {
        state.Scroll = scroll;
        state.Focus = focusOverride ?? currentlySnappedComponent?.myID ?? BackComponentId;
    }

    private void Return()
    {
        Game1.playSound("bigDeSelect");
        SaveState();
        context.Navigation.Back();
    }

    private Rectangle ToScreen(Rectangle bounds) => GalleryDrawing.ScaleRectangle(bounds, menuScale, drawOffsetX, drawOffsetY);
    private (int X, int Y) ToLogical(int x, int y) => ((int)Math.Round((x - drawOffsetX) / menuScale), (int)Math.Round((y - drawOffsetY) / menuScale));
}
