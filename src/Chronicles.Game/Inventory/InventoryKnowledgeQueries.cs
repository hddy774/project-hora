namespace Chronicles.Game.Inventory;

/// <summary>A caller-authorized personal observation, never an inferred view of an unknown physical property.</summary>
public sealed record KnownItemFact(long Item, string Label, int ObservedQuantity, int? KnownQuality, ItemDefect? KnownDefects, long ObservedMonth);
public sealed record KnownItemGroup(string Label, int? KnownQuality, ItemDefect? KnownDefects, long ObservedQuantity,
    long EarliestObservation, IReadOnlyList<long> Items);

public sealed record AdvertisedInventoryOffer(long Contract, string Kind, long Seller, int Place, string Definition,
    string Name, int Quantity, long Price, long? MinimumBid, long Due, long OwnDeposit);

public static class InventoryKnowledgeQueries
{
    /// <summary>Latest authorized personal source snapshots. Never looks up mutable natural stock or fills unknown legacy facts.</summary>
    public static IReadOnlyList<SourceObservation> KnownSources(InventoryLedger ledger, long observer, int? place = null)
    {
        ArgumentNullException.ThrowIfNull(ledger); ArgumentOutOfRangeException.ThrowIfNegativeOrZero(observer);
        return ledger.Observations.Select((observation, index) => (observation, index)).Where(row => row.observation.Observer == observer)
            .GroupBy(row => row.observation.Source).Select(group => group.OrderByDescending(row => row.observation.Month).ThenByDescending(row => row.index).First().observation)
            .Where(observation => observation.Definition.Length > 0 && observation.Activity.Length > 0 && (place is null || observation.Place == place))
            .OrderBy(observation => observation.Place).ThenBy(observation => observation.Source).ToArray();
    }

    /// <summary>Current local public advertisements, including one's own listings. Does not inspect the seller's inventory, quality or beliefs.</summary>
    public static IReadOnlyList<AdvertisedInventoryOffer> LocalOffers(InventoryState state, long viewer)
    {
        ArgumentNullException.ThrowIfNull(state);
        var person = state.People.SingleOrDefault(p => p.Id == viewer && p.Status == LifeStatus.Alive)
            ?? throw new ArgumentOutOfRangeException(nameof(viewer));
        return state.Ledger.Contracts.Where(c => c.Status == "open" && c.Kind is "sale" or "auction" && c.Due > state.Month &&
                c.Destination == person.Place && c.AdvertisedDefinition.Length > 0 && c.AdvertisedQuantity > 0)
            .OrderBy(c => c.Due).ThenBy(c => c.Id).Select(c => new AdvertisedInventoryOffer(c.Id, c.Kind, c.Seller, c.Destination,
                c.AdvertisedDefinition, c.AdvertisedName, c.AdvertisedQuantity, c.Price,
                c.Kind != "auction" || c.Buyer == 0 ? c.Price : c.Escrow == long.MaxValue ? null : c.Escrow + 1,
                c.Due, c.Buyer == viewer ? c.Escrow : 0)).ToArray();
    }

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
