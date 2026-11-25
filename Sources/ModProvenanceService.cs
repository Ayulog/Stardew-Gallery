using System.Text.RegularExpressions;

namespace StardewGallery;

internal sealed record ModProvenanceManifest(
    string UniqueId,
    string Name,
    string? Author = null,
    string? ContentPackFor = null,
    IReadOnlyList<string>? Dependencies = null,
    string? ParentDirName = null);

internal sealed record ShelfVolume(string Key, string DisplayName, bool IsVanilla = false)
{
    internal static ShelfVolume Vanilla { get; } = new(StoryQuerySource.GameKey, "Vanilla", IsVanilla: true);
}

/// <summary>
/// Pure-C# 3-tier mod provenance and same-origin sub-pack grouping service.
/// Never reads game runtime state, never stores absolute local paths, and never alters gallery unlock permissions.
/// </summary>
internal sealed class ModProvenanceService
{
    private static readonly Regex LeadingTagsPattern = new(
        @"^(\s*\[(CP|FTM|NPC|CC|AT|JA|MFM|SAAT|BL|TMXL|STF)\]\s*)+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TrailingSuffixPattern = new(
        @"(?:(?:\s*-\s*|\s+|^)(?:C#|SMAPI\s*组件)|(?:\s*-\s*|\s*)(?:\(SMAPI\s*component\)|\(Content\s*Patcher\)|\(CP\)|\(1\.6[^)]*\)))\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AuxiliaryTagPattern = new(
        @"^\s*\[(FTM|JA|AT|SAAT|MFM|STF|TMXL|CC|BL)\]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly Dictionary<string, ModProvenanceManifest> manifestsById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ShelfVolume> volumeByModId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> canonicalModIdByModId = new(StringComparer.OrdinalIgnoreCase);

    internal ModProvenanceService(IEnumerable<ModProvenanceManifest> manifests)
    {
        List<ModProvenanceManifest> list = manifests
            .Where(m => !string.IsNullOrWhiteSpace(m.UniqueId))
            .GroupBy(m => m.UniqueId.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First() with { UniqueId = g.Key })
            .ToList();

        foreach (ModProvenanceManifest m in list)
            manifestsById[m.UniqueId] = m;

        BuildVolumeComponents(list);
    }

    internal static string CleanModName(string? rawName, string? fallbackId = null)
    {
        string raw = (rawName ?? string.Empty).Trim();
        string cleaned = raw;
        if (cleaned.Length > 0)
        {
            cleaned = LeadingTagsPattern.Replace(cleaned, string.Empty).Trim();
            while (cleaned.Length > 0)
            {
                string next = TrailingSuffixPattern.Replace(cleaned, string.Empty).Trim();
                if (next == cleaned)
                    break;
                cleaned = next;
            }
        }
        if (cleaned.Length > 0)
            return cleaned;
        if (raw.Length > 0)
            return raw;
        return fallbackId?.Trim() ?? string.Empty;
    }

    internal ShelfVolume VolumeForActor(EventSourceActor actor)
    {
        if (actor.IsGameBase)
            return ShelfVolume.Vanilla;
        if (volumeByModId.TryGetValue(actor.UniqueId, out ShelfVolume? volume))
            return volume;
        string display = CleanModName(actor.Name, actor.UniqueId);
        return new ShelfVolume(StoryQuerySource.ModKey(actor.UniqueId), display);
    }

    internal ShelfVolume? VolumeForModId(string? uniqueId)
        => string.IsNullOrWhiteSpace(uniqueId) ? null : volumeByModId.GetValueOrDefault(uniqueId.Trim());

    internal ShelfVolume? ResolveOriginVolume(EventOriginMatch? origin)
    {
        if (origin is null)
            return null;
        if (origin.Provider is { IsGameBase: true })
            return ShelfVolume.Vanilla;

        if (origin.Status == EventOriginMatchStatus.Identified && origin.Provider is { } provider)
            return VolumeForActor(provider);

        if (origin.Status == EventOriginMatchStatus.Incomplete && origin.Provider is { } incompleteProvider)
            return VolumeForActor(incompleteProvider);

        if (origin.Status == EventOriginMatchStatus.Ambiguous && origin.Candidates.Count > 0)
        {
            EventDefinitionCandidate[] declarations = origin.Candidates.Where(c => !c.IsModification).ToArray();
            if (declarations.Length > 0)
            {
                ShelfVolume[] distinct = declarations.Select(c => VolumeForActor(c.Actor))
                    .DistinctBy(v => v.Key, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (distinct.Length == 1)
                    return distinct[0];
            }
        }

        return null;
    }

    internal ShelfVolume? ResolveCharacterVolume(
        string characterName,
        EventOriginMatch? characterMatch,
        IEnumerable<ShelfVolume?>? heartEventVolumes = null)
    {
        ShelfVolume? direct = ResolveOriginVolume(characterMatch);
        if (direct is not null)
            return direct;

        if (heartEventVolumes is not null)
        {
            ShelfVolume[] nonVanilla = heartEventVolumes
                .Where(v => v is { IsVanilla: false })
                .Cast<ShelfVolume>()
                .DistinctBy(v => v.Key, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (nonVanilla.Length == 1)
                return nonVanilla[0];
        }

        return null;
    }

    internal ShelfVolume? ResolveEventVolume(
        GalleryEvent entry,
        EventOriginMatch? eventOrigin,
        Func<string, EventOriginMatch?> characterOriginLookup,
        Func<string, string?>? canonicalNpc = null)
    {
        ShelfVolume? direct = ResolveOriginVolume(eventOrigin);
        if (direct is not null)
            return direct;

        if (eventOrigin?.Status == EventOriginMatchStatus.Ambiguous)
            return null;

        // Tier 2: Inherit from primary/related NPC's Data/Characters declarer when event origin is unknown (e.g. pure C# events).
        IEnumerable<string> ownerCandidates = entry.Ownership.Owners.Select(o => o.Name);
        ShelfVolume? inherited = ResolveFromNpcList(ownerCandidates, characterOriginLookup, canonicalNpc);
        if (inherited is not null)
            return inherited;

        return ResolveFromNpcList(entry.RelatedNpcNames, characterOriginLookup, canonicalNpc);
    }

    internal ShelfVolume? ResolveRuleVolume(EventOriginMatch? ruleOrigin)
        => ResolveOriginVolume(ruleOrigin);

    private ShelfVolume? ResolveFromNpcList(
        IEnumerable<string> npcNames,
        Func<string, EventOriginMatch?> characterOriginLookup,
        Func<string, string?>? canonicalNpc)
    {
        HashSet<ShelfVolume> nonVanilla = new(ShelfVolumeKeyComparer.Instance);
        foreach (string rawNpc in npcNames)
        {
            if (string.IsNullOrWhiteSpace(rawNpc))
                continue;
            string normalized = canonicalNpc?.Invoke(rawNpc) ?? rawNpc;
            EventOriginMatch? charOrigin = characterOriginLookup(normalized);
            if (charOrigin is null or { Status: EventOriginMatchStatus.Unknown } && !string.Equals(normalized, rawNpc, StringComparison.Ordinal))
                charOrigin = characterOriginLookup(rawNpc);
            ShelfVolume? charVolume = ResolveOriginVolume(charOrigin);
            if (charVolume is { IsVanilla: false })
                nonVanilla.Add(charVolume);
        }
        return nonVanilla.Count == 1 ? nonVanilla.First() : null;
    }

    private void BuildVolumeComponents(List<ModProvenanceManifest> list)
    {
        if (list.Count == 0)
            return;

        List<ModProvenanceManifest> ordered = list
            .OrderBy(m => m.UniqueId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Dictionary<string, string> cleanNames = ordered.ToDictionary(
            m => m.UniqueId,
            m => CleanModName(m.Name, null),
            StringComparer.OrdinalIgnoreCase);

        int n = ordered.Count;
        int[] parent = new int[n];
        HashSet<string>[] componentAuthors = new HashSet<string>[n];
        for (int i = 0; i < n; i++)
        {
            parent[i] = i;
            componentAuthors[i] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(ordered[i].Author))
                componentAuthors[i].Add(ordered[i].Author!.Trim());
        }

        int Find(int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }
            return x;
        }

        bool CanUnion(int rootA, int rootB)
        {
            if (rootA == rootB)
                return false;
            if (componentAuthors[rootA].Count == 0 || componentAuthors[rootB].Count == 0)
                return true;
            HashSet<string> merged = new(componentAuthors[rootA], StringComparer.OrdinalIgnoreCase);
            merged.UnionWith(componentAuthors[rootB]);
            return merged.Count <= 1;
        }

        void UnionIfCompatible(int i, int j)
        {
            int rootA = Find(i);
            int rootB = Find(j);
            if (!CanUnion(rootA, rootB))
                return;
            parent[rootB] = rootA;
            componentAuthors[rootA].UnionWith(componentAuthors[rootB]);
        }

        List<(int Priority, int I, int J)> candidateEdges = [];
        for (int i = 0; i < n; i++)
        {
            ModProvenanceManifest a = ordered[i];
            for (int j = i + 1; j < n; j++)
            {
                ModProvenanceManifest b = ordered[j];
                if (!ShouldMerge(a, b, cleanNames[a.UniqueId], cleanNames[b.UniqueId]))
                    continue;
                bool bothSameNonEmptyAuthor = !string.IsNullOrWhiteSpace(a.Author)
                    && !string.IsNullOrWhiteSpace(b.Author)
                    && string.Equals(a.Author.Trim(), b.Author.Trim(), StringComparison.OrdinalIgnoreCase);
                bool hasDirectDep = DirectDependsOn(a, b.UniqueId) || DirectDependsOn(b, a.UniqueId);
                int priority = (bothSameNonEmptyAuthor ? 2 : 0) + (hasDirectDep ? 1 : 0);
                candidateEdges.Add((priority, i, j));
            }
        }

        foreach (var edge in candidateEdges
            .OrderByDescending(e => e.Priority)
            .ThenBy(e => e.I)
            .ThenBy(e => e.J))
        {
            UnionIfCompatible(edge.I, edge.J);
        }

        Dictionary<int, List<ModProvenanceManifest>> components = new();
        for (int i = 0; i < n; i++)
        {
            int root = Find(i);
            if (!components.TryGetValue(root, out List<ModProvenanceManifest>? members))
                components[root] = members = [];
            members.Add(ordered[i]);
        }

        foreach (List<ModProvenanceManifest> component in components.Values)
        {
            ModProvenanceManifest canonical = PickCanonicalMainPack(component);
            string displayName = CleanModName(canonical.Name, canonical.UniqueId);
            ShelfVolume volume = new(StoryQuerySource.ModKey(canonical.UniqueId), displayName);
            foreach (ModProvenanceManifest member in component)
            {
                volumeByModId[member.UniqueId] = volume;
                canonicalModIdByModId[member.UniqueId] = canonical.UniqueId;
            }
        }
    }

    private static bool ShouldMerge(ModProvenanceManifest a, ModProvenanceManifest b, string cleanA, string cleanB)
    {
        if (AuthorsConflict(a, b))
            return false;

        // Rule (a): identical non-empty cleaned mod name (when authors do not conflict).
        if (cleanA.Length > 0 && string.Equals(cleanA, cleanB, StringComparison.OrdinalIgnoreCase))
            return true;

        // Rule (b): identical non-empty Author AND (direct dependency between them OR same immediate Mods/<Parent>/ folder).
        if (string.IsNullOrWhiteSpace(a.Author) || string.IsNullOrWhiteSpace(b.Author))
            return false;
        if (!string.Equals(a.Author.Trim(), b.Author.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;

        if (DirectDependsOn(a, b.UniqueId) || DirectDependsOn(b, a.UniqueId))
            return true;

        if (!string.IsNullOrWhiteSpace(a.ParentDirName)
            && !string.IsNullOrWhiteSpace(b.ParentDirName)
            && string.Equals(a.ParentDirName.Trim(), b.ParentDirName.Trim(), StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static bool AuthorsConflict(ModProvenanceManifest a, ModProvenanceManifest b)
        => !string.IsNullOrWhiteSpace(a.Author)
            && !string.IsNullOrWhiteSpace(b.Author)
            && !string.Equals(a.Author.Trim(), b.Author.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool DirectDependsOn(ModProvenanceManifest mod, string targetId)
    {
        if (!string.IsNullOrWhiteSpace(mod.ContentPackFor)
            && string.Equals(mod.ContentPackFor.Trim(), targetId, StringComparison.OrdinalIgnoreCase))
            return true;
        if (mod.Dependencies is null)
            return false;
        foreach (string dep in mod.Dependencies)
        {
            if (!string.IsNullOrWhiteSpace(dep) && string.Equals(dep.Trim(), targetId, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static ModProvenanceManifest PickCanonicalMainPack(List<ModProvenanceManifest> component)
    {
        if (component.Count == 1)
            return component[0];

        HashSet<string> memberIds = component.Select(m => m.UniqueId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        bool IsContentPatcherPack(ModProvenanceManifest m)
            => string.Equals(m.ContentPackFor?.Trim(), "Pathoschild.ContentPatcher", StringComparison.OrdinalIgnoreCase);

        bool IsRootInComponent(ModProvenanceManifest m)
        {
            if (!string.IsNullOrWhiteSpace(m.ContentPackFor) && memberIds.Contains(m.ContentPackFor.Trim())
                && !string.Equals(m.ContentPackFor.Trim(), m.UniqueId, StringComparison.OrdinalIgnoreCase))
                return false;
            if (m.Dependencies is not null)
            {
                foreach (string dep in m.Dependencies)
                {
                    if (!string.IsNullOrWhiteSpace(dep)
                        && !string.Equals(dep.Trim(), m.UniqueId, StringComparison.OrdinalIgnoreCase)
                        && memberIds.Contains(dep.Trim()))
                        return false;
                }
            }
            return true;
        }

        bool HasAuxiliaryTag(ModProvenanceManifest m)
            => !string.IsNullOrWhiteSpace(m.Name) && AuxiliaryTagPattern.IsMatch(m.Name);

        return component
            .OrderByDescending(IsContentPatcherPack)
            .ThenByDescending(IsRootInComponent)
            .ThenBy(HasAuxiliaryTag)
            .ThenBy(m => CleanModName(m.Name, m.UniqueId).Length)
            .ThenBy(m => (m.Name ?? string.Empty).Trim().Length)
            .ThenBy(m => m.UniqueId, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    private sealed class ShelfVolumeKeyComparer : IEqualityComparer<ShelfVolume>
    {
        internal static ShelfVolumeKeyComparer Instance { get; } = new();
        public bool Equals(ShelfVolume? x, ShelfVolume? y)
            => ReferenceEquals(x, y) || x is not null && y is not null && string.Equals(x.Key, y.Key, StringComparison.OrdinalIgnoreCase);
        public int GetHashCode(ShelfVolume obj)
            => StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Key);
    }
}
