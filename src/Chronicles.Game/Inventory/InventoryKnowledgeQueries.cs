namespace Chronicles.Game.Inventory;

/// <summary>A caller-authorized personal observation, never an inferred view of an unknown physical property.</summary>
public sealed record KnownItemFact(long Item, string Label, int ObservedQuantity, int? KnownQuality, ItemDefect? KnownDefects, long ObservedMonth);
public sealed record KnownItemGroup(string Label, int? KnownQuality, ItemDefect? KnownDefects, long ObservedQuantity,
    long EarliestObservation, IReadOnlyList<long> Items);

public static class InventoryKnowledgeQueries
{
    /// <summary>Groups supplied knowledge snapshots without reading world truth or merging physical instances.
    /// The host filters observations by viewer permission before calling this method.</summary>
    public static IReadOnlyList<KnownItemGroup> GroupKnownItems(IReadOnlyList<KnownItemFact> authorizedFacts)
    {
        ArgumentNullException.ThrowIfNull(authorizedFacts);
        if (authorizedFacts.Any(f => f.Item <= 0 || f.ObservedQuantity < 0 || f.ObservedMonth < 0) ||
            authorizedFacts.Select(f => f.Item).Distinct().Count() != authorizedFacts.Count)
            throw new ArgumentException("Known inventory requires one valid observation per physical item.", nameof(authorizedFacts));
        return authorizedFacts.GroupBy(f => (f.Label, f.KnownQuality, f.KnownDefects))
            .OrderBy(g => g.Key.Label, StringComparer.Ordinal).ThenBy(g => g.Key.KnownQuality).ThenBy(g => g.Key.KnownDefects)
            .Select(g => new KnownItemGroup(g.Key.Label, g.Key.KnownQuality, g.Key.KnownDefects,
                g.Sum(f => (long)f.ObservedQuantity), g.Min(f => f.ObservedMonth), g.Select(f => f.Item).Order().ToArray())).ToArray();
    }
}
