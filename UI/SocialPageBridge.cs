using System.Reflection;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Menus;

namespace StardewGallery;

/// <summary>
/// Read-only inspection and seamless return bridge for vanilla <see cref="GameMenu"/>'s <see cref="SocialPage"/>.
/// Never injects custom buttons or components into <see cref="SocialPage"/>.
/// </summary>
internal static class SocialPageBridge
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly FieldInfo? SlotPositionField = typeof(SocialPage).GetField("slotPosition", Flags);
    private static readonly FieldInfo? ScrollBarField = typeof(SocialPage).GetField("scrollBar", Flags);
    private static readonly FieldInfo? SpritesField = typeof(SocialPage).GetField("sprites", Flags);
    private static readonly MethodInfo? SetScrollBarMethod = typeof(SocialPage).GetMethod("setScrollBarToCurrentIndex", Flags);

    internal readonly record struct EntryDescriptor(string? InternalName, bool IsPlayer, bool IsChild, bool IsMet = true, string? DisplayName = null);

    internal readonly record struct SlotDescriptor(int Index, int MyId, Rectangle Bounds);

    internal readonly record struct VisibleSlotBadge(string InternalName, string DisplayName, Rectangle BadgeBounds, EncounterClue Clue);

    /// <summary>
    /// Pure resolution of the villager NPC <c>InternalName</c> pointed at by either controller focus or mouse hover
    /// within the currently visible <see cref="SocialPage"/> window (<c>slotPosition .. slotPosition + slotsOnPage - 1</c>).
    /// </summary>
    internal static string? ResolvePointedVillager(
        IReadOnlyList<EntryDescriptor> entries,
        IReadOnlyList<SlotDescriptor> slots,
        int slotPosition,
        int slotsOnPage,
        bool useControllerSnap,
        int? snappedComponentId,
        int mouseX,
        int mouseY)
    {
        if (entries.Count == 0 || slots.Count == 0 || slotsOnPage <= 0)
            return null;

        int start = Math.Max(0, slotPosition);
        int endExclusive = Math.Min(slots.Count, start + slotsOnPage);

        if (useControllerSnap && snappedComponentId is int snappedId)
        {
            for (int i = start; i < endExclusive; i++)
            {
                SlotDescriptor slot = slots[i];
                if (slot.MyId == snappedId)
                    return VillagerNameAt(entries, slot.Index);
            }
            // When controller snap is on a top tab or non-character control, do not fall back to a stale mouse coordinate.
            return null;
        }

        for (int i = start; i < endExclusive; i++)
        {
            SlotDescriptor slot = slots[i];
            if (slot.Bounds.Contains(mouseX, mouseY))
                return VillagerNameAt(entries, slot.Index);
        }

        return null;
    }

    private static string? VillagerNameAt(IReadOnlyList<EntryDescriptor> entries, int index)
    {
        if (index < 0 || index >= entries.Count)
            return null;
        EntryDescriptor entry = entries[index];
        if (entry.IsPlayer || entry.IsChild || string.IsNullOrWhiteSpace(entry.InternalName))
            return null;
        return entry.InternalName;
    }

    internal static bool TryInspectActiveMenu(out GameMenu gameMenu, out SocialPage socialPage, out string? pointedVillagerName)
    {
        gameMenu = null!;
        socialPage = null!;
        pointedVillagerName = null;

        if (Game1.activeClickableMenu is not GameMenu gm
            || gm.GetChildMenu() is not null
            || gm.GetCurrentPage() is not SocialPage sp
            || sp.GetChildMenu() is not null)
        {
            return false;
        }

        gameMenu = gm;
        socialPage = sp;
        pointedVillagerName = InspectPointedVillager(sp, Game1.getMouseX(true), Game1.getMouseY(true));
        return true;
    }

    internal static string? InspectPointedVillager(SocialPage socialPage, int mouseX, int mouseY)
    {
        List<SocialPage.SocialEntry>? rawEntries = socialPage.SocialEntries;
        List<ClickableTextureComponent>? rawSlots = socialPage.characterSlots;
        if (rawEntries is null || rawSlots is null || rawEntries.Count == 0 || rawSlots.Count == 0)
            return null;

        var entries = new EntryDescriptor[rawEntries.Count];
        for (int i = 0; i < rawEntries.Count; i++)
        {
            SocialPage.SocialEntry e = rawEntries[i];
            entries[i] = e is null ? default : new EntryDescriptor(e.InternalName, e.IsPlayer, e.IsChild, e.IsMet, e.DisplayName);
        }

        var slots = new SlotDescriptor[rawSlots.Count];
        for (int i = 0; i < rawSlots.Count; i++)
        {
            ClickableTextureComponent? c = rawSlots[i];
            slots[i] = c is null ? new SlotDescriptor(i, -1, Rectangle.Empty) : new SlotDescriptor(i, c.myID, c.bounds);
        }

        int slotPos = ReadSlotPosition(socialPage);
        bool useSnap = Game1.options?.snappyMenus == true && Game1.options.gamepadControls && socialPage.currentlySnappedComponent is not null;
        int? snappedId = socialPage.currentlySnappedComponent?.myID;

        return ResolvePointedVillager(entries, slots, slotPos, SocialPage.slotsOnPage, useSnap, snappedId, mouseX, mouseY);
    }

    internal static int ReadSlotPosition(SocialPage socialPage)
        => SlotPositionField?.GetValue(socialPage) is int value ? value : 0;

    internal static Action CreateRestoreCallback(GameMenu gameMenu, SocialPage socialPage)
    {
        int savedSlotPosition = ReadSlotPosition(socialPage);
        ClickableComponent? savedComponent = socialPage.currentlySnappedComponent;
        int? savedSnappedId = savedComponent?.myID;

        return () =>
        {
            gameMenu.currentTab = GameMenu.socialTab;
            Game1.activeClickableMenu = gameMenu;

            int slotCount = socialPage.characterSlots?.Count ?? 0;
            int maxSlot = Math.Max(0, slotCount - SocialPage.slotsOnPage);
            int restoredSlot = Math.Clamp(savedSlotPosition, 0, maxSlot);
            SlotPositionField?.SetValue(socialPage, restoredSlot);

            try
            {
                if (ScrollBarField?.GetValue(socialPage) is not null)
                    SetScrollBarMethod?.Invoke(socialPage, null);
            }
            catch
            {
                // Headless test fixtures without scrollbar textures still restore slotPosition and focus cleanly.
            }

            try
            {
                if (SpritesField?.GetValue(socialPage) is System.Collections.ICollection sprites && sprites.Count == slotCount && slotCount > 0)
                    socialPage.updateSlots();
            }
            catch
            {
                // Ignore headless layout updates when sprite components are uninitialized.
            }

            ClickableComponent? target = null;
            if (savedSnappedId is int id)
            {
                target = socialPage.characterSlots?.FirstOrDefault(c => c is not null && c.myID == id)
                    ?? socialPage.allClickableComponents?.FirstOrDefault(c => c is not null && c.myID == id);
            }
            socialPage.currentlySnappedComponent = target ?? savedComponent;

            if (Game1.options?.snappyMenus == true && Game1.options.gamepadControls && socialPage.currentlySnappedComponent is not null)
                socialPage.snapCursorToCurrentSnappedComponent();
        };
    }

    internal static Rectangle ComputeBadgeBounds(Rectangle slotBounds)
        => slotBounds.Width <= 0 || slotBounds.Height <= 0
            ? Rectangle.Empty
            : new Rectangle(slotBounds.X + 8, slotBounds.Y + 8, 32, 32);

    internal static IReadOnlyList<VisibleSlotBadge> ResolveVisibleBadges(
        IReadOnlyList<EntryDescriptor> entries,
        IReadOnlyList<SlotDescriptor> slots,
        int slotPosition,
        int slotsOnPage,
        IReadOnlyDictionary<string, EncounterClue> cluesByVillager)
    {
        if (entries.Count == 0 || slots.Count == 0 || slotsOnPage <= 0 || cluesByVillager.Count == 0)
            return [];

        int start = Math.Max(0, slotPosition);
        int endExclusive = Math.Min(slots.Count, start + slotsOnPage);
        List<VisibleSlotBadge> badges = [];

        for (int i = start; i < endExclusive; i++)
        {
            SlotDescriptor slot = slots[i];
            if (slot.Index < 0 || slot.Index >= entries.Count)
                continue;
            EntryDescriptor entry = entries[slot.Index];
            if (entry.IsPlayer || entry.IsChild || !entry.IsMet || string.IsNullOrWhiteSpace(entry.InternalName))
                continue;
            if (!cluesByVillager.TryGetValue(entry.InternalName, out EncounterClue clue) || clue == EncounterClue.None)
                continue;
            Rectangle badgeRect = ComputeBadgeBounds(slot.Bounds);
            if (badgeRect == Rectangle.Empty)
                continue;
            string displayName = string.IsNullOrWhiteSpace(entry.DisplayName) ? entry.InternalName : entry.DisplayName;
            badges.Add(new VisibleSlotBadge(entry.InternalName, displayName, badgeRect, clue));
        }

        return badges;
    }

    internal static IReadOnlyList<VisibleSlotBadge> CollectVisibleBadges(
        SocialPage socialPage,
        IReadOnlyDictionary<string, EncounterClue> cluesByVillager)
    {
        List<SocialPage.SocialEntry>? rawEntries = socialPage.SocialEntries;
        List<ClickableTextureComponent>? rawSlots = socialPage.characterSlots;
        if (rawEntries is null || rawSlots is null || rawEntries.Count == 0 || rawSlots.Count == 0 || cluesByVillager.Count == 0)
            return [];

        var entries = new EntryDescriptor[rawEntries.Count];
        for (int i = 0; i < rawEntries.Count; i++)
        {
            SocialPage.SocialEntry e = rawEntries[i];
            entries[i] = e is null ? default : new EntryDescriptor(e.InternalName, e.IsPlayer, e.IsChild, e.IsMet, e.DisplayName);
        }

        var slots = new SlotDescriptor[rawSlots.Count];
        for (int i = 0; i < rawSlots.Count; i++)
        {
            ClickableTextureComponent? c = rawSlots[i];
            slots[i] = c is null ? new SlotDescriptor(i, -1, Rectangle.Empty) : new SlotDescriptor(i, c.myID, c.bounds);
        }

        return ResolveVisibleBadges(entries, slots, ReadSlotPosition(socialPage), SocialPage.slotsOnPage, cluesByVillager);
    }

    internal static bool TryHitBadge(
        SocialPage socialPage,
        IReadOnlyDictionary<string, EncounterClue> cluesByVillager,
        int mouseX,
        int mouseY,
        out string? villagerName)
    {
        foreach (VisibleSlotBadge badge in CollectVisibleBadges(socialPage, cluesByVillager))
        {
            if (badge.BadgeBounds.Contains(mouseX, mouseY))
            {
                villagerName = badge.InternalName;
                return true;
            }
        }
        villagerName = null;
        return false;
    }

    internal static string? FormatMorningEncounterHint(
        IReadOnlyList<string> displayNames,
        string nameSeparator,
        Func<string, object, string> translate)
    {
        if (displayNames is null || displayNames.Count == 0)
            return null;
        if (displayNames.Count <= 3)
        {
            string joined = string.Join(nameSeparator, displayNames);
            return translate("clue.morning-hint", new { names = joined });
        }
        string firstTwo = displayNames[0] + nameSeparator + displayNames[1];
        return translate("clue.morning-hint-many", new { names = firstTwo, count = displayNames.Count });
    }
}
