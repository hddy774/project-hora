using System.Globalization;
using System.Text.Json;
using Chronicles.Game.Inventory;

namespace Chronicles.Game.Ecology;

public static class EcologyEngine
{
    public static EcologyState Initialize(EcologyContext context)
    {
        var state = new EcologyState { Initialized = true, LastSettledMonth = context.Month - 1,
            InventoryEventCursor = context.Inventory.Events.Count,
            Stocks = StockSnapshot(context), Places = PlaceSnapshot(context),
            RecordedJobs = context.Inventory.Jobs.Where(j => j.Status is "completed" or "failed" or "cancelled").Select(j => j.Id).ToList(),
            RecordedContracts = context.Inventory.Contracts.Where(c => c.Status != "open").Select(c => c.Id).ToList() };
        Validate(context, state); return state;
    }

    public static EcologyState RecordConsumption(EcologyContext context, EcologyState input, long person, string definition, int quantity)
    {
        if (quantity == 0) return input;
        if (quantity < 0 || !InventoryDefinitions.Items.ContainsKey(definition)) throw new ArgumentOutOfRangeException(nameof(quantity));
        var actor = context.People.Single(p => p.Id == person);
        var state = Copy(input);
        var id = state.NextId++;
        state.Consumption.Add(new(id, context.Month, actor.Id, actor.Household, actor.Sect, actor.Place, definition,
            quantity, $"physical-consumption:{id}:person:{person}:month:{context.Month}"));
        return state;
    }

    /// <summary>Samples actual authorities; never transfers property, consumes stock, regenerates a source or creates an operator.</summary>
    public static EcologyState Settle(EcologyContext context, EcologyState input)
    {
        Validate(context, input);
        if (input.LastSettledMonth >= context.Month) return input;
        var state = Copy(input); var facts = new EcologyFacts(context);
        var events = context.Inventory.Events.Skip(state.InventoryEventCursor).Select((e, i) => (Event: e, Index: state.InventoryEventCursor + i)).ToArray();
        foreach (var entry in events.Where(e => e.Event.Kind is "physical-consumption" or "physical-loss"))
        {
            var flow = JsonSerializer.Deserialize<PhysicalFlowEvidence>(JsonSerializer.SerializeToUtf8Bytes(entry.Event))!;
            if (flow.Quantity <= 0 || string.IsNullOrEmpty(flow.Definition) || flow.Place < 0) continue;
            var source = flow.Sequence > 0 ? $"inventory-flow:{flow.Sequence}" : $"inventory-event:{entry.Index}";
            if (flow.IsLoss)
                state.Losses.Add(new(state.NextId++, entry.Event.Month, flow.Owner, flow.Place, flow.Definition, flow.Quantity, source, flow.OwnerObserved));
            else
            {
                var actor = context.People.Single(p => p.Id == entry.Event.Actor);
                var job = context.Inventory.Jobs.FirstOrDefault(j => j.Inputs.Contains(entry.Event.Subject));
                state.Consumption.Add(new(state.NextId++, entry.Event.Month, actor.Id, actor.Household, actor.Sect, flow.Place,
                    flow.Definition, flow.Quantity, job is null ? source : $"inventory-job:{job.Id}:{source}"));
            }
        }
        var productionStart = state.Production.Count; var tradeStart = state.Trades.Count;
        RecordProduction(context, state, events.Select(x => x.Event).ToArray());
        RecordTrades(context, state);
        var consumption = state.Consumption.Where(c => c.Id > input.ConsumptionCursor && c.Month <= context.Month).ToArray();
        var listings = PublicListings(context);
        RecordDemand(context, state, facts, consumption);
        RecordMarkets(context, state, facts, consumption, listings, state.Production.Skip(productionStart).ToArray(), state.Trades.Skip(tradeStart).ToArray());
        RecordNaturalFlows(context, state, events);
        RecordSocialChanges(context, state);
        RecordFacilities(context, state, consumption, listings);
        state.Stocks = StockSnapshot(context); state.Places = PlaceSnapshot(context);
        state.InventoryEventCursor = context.Inventory.Events.Count; state.LastSettledMonth = context.Month;
        state.ConsumptionCursor = state.Consumption.Count == 0 ? state.ConsumptionCursor : Math.Max(state.ConsumptionCursor, state.Consumption.Max(c => c.Id));
        Prune(context, state); Validate(context, state); return state;
    }

    public static IReadOnlyList<PublicMarketListing> PublicListings(EcologyContext context)
    {
        var facts = new EcologyFacts(context);
        return context.Inventory.Contracts.Where(c => c.Status == "open" && c.Kind is "sale" or "auction" && c.Due > context.Month &&
            facts.People.TryGetValue(c.Seller, out var p) && p.Status == LifeStatus.Alive &&
            facts.Items.TryGetValue(c.Item, out var item) && item.Reserved == c.Reservation && facts.Location(item) == c.Destination)
            .OrderBy(c => c.Id).Select(c => new PublicMarketListing(c.Id, c.Kind, c.Seller, c.Destination,
                c.AdvertisedDefinition, c.AdvertisedName, c.AdvertisedQuantity,
                c.Kind == "auction" && c.Escrow > 0 ? c.Escrow : c.Price, c.Due, context.Month, $"public-contract:{c.Id}")).ToArray();
    }

    public static EconomyView View(EcologyContext context, EcologyState state, long? actor)
    {
        var observer = actor is null;
        var person = observer ? null : context.People.FirstOrDefault(p => p.Id == actor && p.Status == LifeStatus.Alive);
        if (!observer && person is null) throw new ArgumentException("An economy view requires a current living actor.", nameof(actor));
        var place = person?.Place;
        var listings = PublicListings(context).Where(l => observer || l.Place == place).ToArray();
        var prices = state.Markets.Where(m => observer || m.Place == place && (m.PublicSupply > 0 || m.TradedQuantity > 0)).Select(m => new PublicPriceSample(m.Month, m.Place,
            m.Definition, m.PublicSupply, m.TradedQuantity, m.AskPerUnitMilli, m.PaidPerUnitMilli,
            m.Sources.Where(s => s.StartsWith("public-", StringComparison.Ordinal)).ToArray())).ToArray();
        var facilities = state.Facilities.Select(f =>
        {
            var live = f.Operators.Where(id => context.People.Any(p => p.Id == id && p.Status == LifeStatus.Alive && p.Place == f.Place)).ToArray();
            return f with { Active = f.Active && live.Length > 0, Operators = f.Active && live.Length > 0 ? live : f.Operators, Sources = f.Sources.ToArray() };
        })
            .Where(f => observer || f.Active && f.Place == place &&
            (f.Public || f.Operators.Contains(person!.Id))).Select(f => observer ? f : f with
            {
                Operators = f.Public ? f.Operators.Where(id => id == person!.Id || person.KnownPeople.Contains(id)).ToArray() : [person!.Id],
                Sources = f.Public ? f.Sources : context.Inventory.Observations.Where(o => o.Observer == person!.Id && o.Place == f.Place)
                    .Select(o => $"personal-source:{o.Source}:{o.Month}:{o.Evidence}").ToArray()
            }).ToArray();
        return new(context.Month, observer, observer ? state.Markets.Select(m => m with { Sources = m.Sources.ToArray() }).ToArray() : [],
            state.Demand.Where(d => observer || d.Sector == "person" && d.Subject == actor).ToArray(), listings, prices,
            state.Consumption.Where(c => observer || c.Person == actor).ToArray(), state.Losses.Where(l => observer || l.Owner == actor && l.OwnerObserved).ToArray(),
            observer ? state.NaturalFlows.Select(f => f with
            { NaturalSources = f.NaturalSources.ToArray(), Sources = f.Sources.ToArray() }).ToArray() : [],
            observer ? state.Changes.ToArray() : [], facilities,
            context.Inventory.Observations.Where(o => observer || o.Observer == actor).ToArray());
    }

    public static void Validate(EcologyContext context, EcologyState state)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(state);
        Require(state.Schema == 1 && state.NextId > 0 && state.LastSettledMonth >= -1 && state.LastSettledMonth <= context.Month,
            "Ecology schema, clock or identity counter is invalid.");
        Require(context.Rules.HistoryMonths > 0 && context.Rules.MaximumChanges > 0 && context.Rules.FacilityActivityMonths >= 0 &&
            context.Rules.FoodReserveMonths > 0 && context.Rules.MaximumSaleUnits > 0 && context.Rules.DefaultUnitPrice > 0 && context.FoodUse >= 0,
            "Ecology rules require positive supported units.");
        Require(state.InventoryEventCursor >= 0 && state.InventoryEventCursor <= context.Inventory.Events.Count && state.ConsumptionCursor >= 0 &&
            state.ConsumptionCursor < state.NextId, "Ecology receipt cursor is invalid.");
        var ids = state.Consumption.Select(c => c.Id).Concat(state.Losses.Select(c => c.Id)).Concat(state.Changes.Select(c => c.Id)).Concat(state.Facilities.Select(f => f.Id)).ToArray();
        Require(ids.Distinct().Count() == ids.Length && ids.All(id => id > 0 && id < state.NextId), "Ecology evidence IDs must be stable and distinct.");
        Require(state.Consumption.All(c => c.Quantity > 0 && c.Month <= context.Month && context.People.Any(p => p.Id == c.Person) &&
            context.Places.Any(p => p.Id == c.Place) && InventoryDefinitions.Items.ContainsKey(c.Definition) && c.Source.Length > 0), "Consumption must reference real people, stock definitions and sources.");
        Require(state.Losses.All(l => l.Quantity > 0 && l.Month <= context.Month && context.People.Any(p => p.Id == l.Owner) &&
            context.Places.Any(p => p.Id == l.Place) && InventoryDefinitions.Items.ContainsKey(l.Definition)), "Physical loss evidence requires actual former owners and stock.");
        Require(state.RecordedJobs.Distinct().Count() == state.RecordedJobs.Count && state.RecordedJobs.All(id => context.Inventory.Jobs.Any(j => j.Id == id)) &&
            state.RecordedContracts.Distinct().Count() == state.RecordedContracts.Count && state.RecordedContracts.All(id => context.Inventory.Contracts.Any(c => c.Id == id)),
            "Recorded economic transitions require actual inventory jobs or contracts.");
        Require(state.Markets.GroupBy(m => (m.Month, m.Place, m.Definition)).All(g => g.Count() == 1) && state.Markets.All(m =>
            m.Month <= context.Month && context.Places.Any(p => p.Id == m.Place && p.Province == m.Province) &&
            m.Needed >= 0 && m.Consumed >= 0 && m.Produced >= 0 && m.PhysicalStock >= 0 && m.PublicSupply >= 0 && m.Turnover >= 0 &&
            m.ConcentrationBasisPoints is >= 0 and <= 10000 && m.Sources.Count > 0), "Regional samples require actual bounded flows and provenance.");
        Require(state.NaturalFlows.All(f => f.Opening >= 0 && f.Regeneration >= 0 && f.Extraction >= 0 && f.Loss >= 0 && f.Closing >= 0 &&
            checked(f.Opening + f.Regeneration - f.Extraction - f.Loss + f.ObservedAdjustment) == f.Closing &&
            f.NaturalSources.All(id => context.Inventory.Sources.Any(s => s.Id == id && s.Place == f.Place))), "Natural stock flow does not reconcile to its real sources.");
        Require(state.Facilities.All(f => f.Founded <= f.LastActivity && f.LastActivity <= context.Month && f.Sources.Count > 0 && f.Operators.Count > 0 &&
            f.Operators.All(id => context.People.Any(p => p.Id == id))),
            "A facility requires actual operators and dated activity evidence.");
        Require(state.Demand.All(d => d.Needed >= 0 && d.Consumed >= 0 && d.AccessibleStock >= 0 && d.AvailableMoney >= 0 && d.CarryUnits >= 0) &&
            state.Trades.All(t => t.Quantity > 0 && t.Price > 0 && context.Inventory.Contracts.Any(c => c.Id == t.Contract && c.Status == "completed" && c.Buyer == t.Buyer)),
            "Demand and trade evidence cannot invent purchasing power or settlements.");
    }

    private static void RecordProduction(EcologyContext context, EcologyState state, InventoryEvent[] events)
    {
        foreach (var job in context.Inventory.Jobs.Where(j => j.Status is "completed" or "failed" or "cancelled" && !state.RecordedJobs.Contains(j.Id)).OrderBy(j => j.Id))
        {
            state.RecordedJobs.Add(job.Id);
            if (job.Status != "completed") continue;
            var output = job.Source != 0 ? context.Inventory.Sources.First(s => s.Id == job.Source).Definition :
                InventoryDefinitions.Recipes.GetValueOrDefault(job.Recipe)?.Output;
            var quantity = job.Source != 0 ? job.OutputQuantity : InventoryDefinitions.Recipes.GetValueOrDefault(job.Recipe)?.Quantity ?? 0;
            if (output is null || quantity <= 0) continue;
            var month = events.LastOrDefault(e => e.Kind == "completed" && e.Subject == job.Id)?.Month ?? context.Month;
            state.Production.Add(new(job.Id, job.Actor, job.Place, output, quantity, job.Quality, month, $"inventory-job:{job.Id}"));
        }
    }

    private static void RecordTrades(EcologyContext context, EcologyState state)
    {
        foreach (var contract in context.Inventory.Contracts.Where(c => c.Status != "open" && !state.RecordedContracts.Contains(c.Id)).OrderBy(c => c.Id))
        {
            state.RecordedContracts.Add(contract.Id);
            if (contract.Status != "completed" || contract.Kind == "transport" || contract.AdvertisedQuantity <= 0) continue;
            // Typed audit fields are copied at the inventory boundary. Old saves with no paid evidence stay unknown.
            var paid = JsonSerializer.Deserialize<PaidSettlementEvidence>(JsonSerializer.SerializeToUtf8Bytes(contract))!;
            if (paid.PaidAmount is not > 0 || paid.PaidQuantity is not > 0 || paid.EndedMonth is null) continue;
            state.Trades.Add(new(contract.Id, contract.Destination, contract.AdvertisedDefinition, paid.PaidQuantity.Value,
                paid.PaidAmount.Value, paid.PaidQuality, contract.Seller, contract.Buyer, paid.EndedMonth.Value, $"public-paid-contract:{contract.Id}"));
        }
    }

    private static void RecordDemand(EcologyContext context, EcologyState state, EcologyFacts facts, EcologyConsumption[] consumption)
    {
        foreach (var person in context.People.Where(p => p.Status == LifeStatus.Alive).OrderBy(p => p.Id))
        {
            var accessible = context.Items.Where(i => i.Definition == "food" && facts.Accessible(person, i)).Sum(i => (long)i.Quantity);
            state.Demand.Add(new(context.Month, person.Place, "food", "person", person.Id, context.FoodUse,
                consumption.Where(c => c.Person == person.Id && c.Definition == "food").Sum(c => (long)c.Quantity), accessible,
                person.Money, facts.FreeCarry(person), $"living-person:{person.Id}:month:{context.Month}"));
        }
        foreach (var home in context.People.Where(p => p.Status == LifeStatus.Alive && p.Household != 0).GroupBy(p => (p.Place, p.Household)).OrderBy(g => g.Key.Place).ThenBy(g => g.Key.Household))
        {
            var members = home.Select(p => p.Id).ToHashSet();
            state.Demand.Add(new(context.Month, home.Key.Place, "food", "household", home.Key.Household, home.LongCount() * context.FoodUse,
                consumption.Where(c => members.Contains(c.Person) && c.Definition == "food").Sum(c => (long)c.Quantity),
                context.Items.Where(i => i.Definition == "food" && home.Any(p => facts.Accessible(p, i))).Sum(i => (long)i.Quantity),
                home.Sum(p => p.Money), home.Sum(facts.FreeCarry), $"actual-household:{home.Key.Household}:residents:{home.Count()}"));
        }
        foreach (var used in consumption.Where(c => c.Source.StartsWith("inventory-job:", StringComparison.Ordinal)))
            state.Demand.Add(new(context.Month, used.Place, used.Definition, "production", used.Person, used.Quantity, used.Quantity,
                0, 0, 0, used.Source));
        foreach (var group in context.Groups.Where(g => !g.Dissolved && g.Kind != GroupKind.Family))
            foreach (var members in context.People.Where(p => p.Status == LifeStatus.Alive && group.Members.Contains(p.Id)).GroupBy(p => p.Place))
                state.Demand.Add(new(context.Month, members.Key, "food", "organization-members", group.Id, members.LongCount() * context.FoodUse,
                    consumption.Where(c => members.Any(p => p.Id == c.Person) && c.Definition == "food").Sum(c => (long)c.Quantity),
                    context.Items.Where(i => i.Definition == "food" && members.Any(p => facts.Accessible(p, i))).Sum(i => (long)i.Quantity),
                    group.Money, members.Sum(facts.FreeCarry), $"actual-membership:{group.Id}:member-needs-not-extra-consumers"));
    }

    private static void RecordMarkets(EcologyContext context, EcologyState state, EcologyFacts facts, EcologyConsumption[] consumption,
        IReadOnlyList<PublicMarketListing> listings, EcologyProduction[] production, EcologyTrade[] trades)
    {
        var keys = listings.Select(l => (l.Place, l.Definition)).Concat(consumption.Select(c => (c.Place, c.Definition)))
            .Concat(production.Select(p => (p.Place, p.Definition))).Concat(trades.Select(t => (t.Place, t.Definition)))
            .Concat(context.Items.Select(i => (Place: facts.Location(i), i.Definition)))
            .Concat(context.People.Where(p => p.Status == LifeStatus.Alive).Select(p => (p.Place, Definition: "food")))
            .Distinct().OrderBy(k => k.Place).ThenBy(k => k.Definition, StringComparer.Ordinal);
        foreach (var key in keys)
        {
            var offers = listings.Where(l => l.Place == key.Place && l.Definition == key.Definition).ToArray();
            var paid = trades.Where(t => t.Place == key.Place && t.Definition == key.Definition).ToArray();
            var used = consumption.Where(c => c.Place == key.Place && c.Definition == key.Definition).ToArray();
            var produced = production.Where(p => p.Place == key.Place && p.Definition == key.Definition).ToArray();
            var needed = key.Definition == "food" ? context.People.LongCount(p => p.Status == LifeStatus.Alive && p.Place == key.Place) * context.FoodUse :
                state.Demand.Where(d => d.Month == context.Month && d.Place == key.Place && d.Definition == key.Definition && d.Sector == "production").Sum(d => d.Needed);
            var supply = offers.Sum(o => (long)o.Quantity); var turnover = paid.Sum(t => t.Price); var sold = paid.Sum(t => (long)t.Quantity);
            var ask = supply == 0 ? (long?)null : Milli(offers.Sum(o => o.Price), supply);
            var price = sold == 0 ? (long?)null : Milli(turnover, sold);
            var known = paid.Where(t => t.Quality is not null).ToArray();
            var adjusted = known.Length == 0 ? (long?)null : (long)Math.Min(long.MaxValue, known.Sum(t =>
                (decimal)t.Price * 5000000 / Math.Max(1000, t.Quality!.Value)) / known.Sum(t => (long)t.Quantity));
            var basis = price ?? ask ?? state.Markets.LastOrDefault(m => m.Place == key.Place && m.Definition == key.Definition)?.IndicativePriceMilli;
            var physical = context.Items.Where(i => i.Definition == key.Definition && facts.Location(i) == key.Place).Sum(i => (long)i.Quantity);
            var available = physical;
            var effectiveDemand = Math.Max(needed, used.Sum(c => (long)c.Quantity));
            var pressure = (int)Math.Clamp((effectiveDemand - available) * 25 / Math.Max(1, effectiveDemand + available), -25, 25);
            var indicative = basis is null ? null : (long?)Math.Max(1, (long)Math.Min(long.MaxValue, (decimal)basis.Value * (100 + pressure) / 100));
            var concentration = supply == 0 ? 0 : (int)Math.Min(10000, offers.GroupBy(o => o.Seller).Sum(g =>
                { var share = g.Sum(o => (long)o.Quantity) * 10000 / supply; return share * share / 10000; }));
            var sources = offers.Select(o => o.Source).Concat(paid.Select(t => t.Source)).Concat(produced.Select(p => p.Source))
                .Concat(used.Select(c => c.Source)).Append($"living-demand:{key.Place}:{context.Month}").Distinct(StringComparer.Ordinal).ToArray();
            state.Markets.Add(new(context.Month, key.Place, context.Places.Single(p => p.Id == key.Place).Province, key.Definition,
                needed, used.Sum(c => (long)c.Quantity), produced.Sum(p => (long)p.Quantity), physical, supply, offers.Select(o => o.Seller).Distinct().Count(),
                sold, turnover, ask, price, adjusted, indicative, concentration, sources));
            var previous = state.Markets.LastOrDefault(m => m.Place == key.Place && m.Definition == key.Definition && m.Month < context.Month);
            if (concentration == 10000 && supply > 0 && previous?.ConcentrationBasisPoints != 10000)
                Change(state, context.Month, key.Place, EcologicalChangeKind.Monopoly, previous?.ConcentrationBasisPoints ?? 0, concentration, sources[0]);
        }
    }

    private static void RecordNaturalFlows(EcologyContext context, EcologyState state,
        (InventoryEvent Event, int Index)[] events)
    {
        foreach (var place in context.Places.OrderBy(p => p.Id))
        {
            var rows = context.Inventory.Sources.Where(s => s.Place == place.Id).ToArray(); var ids = rows.Select(s => s.Id).ToHashSet();
            var related = events.Where(e => ids.Contains(e.Event.Subject)).ToArray();
            var opening = state.Stocks.Where(s => s.Place == place.Id).Sum(s => s.Remaining); var closing = rows.Sum(s => s.Remaining);
            var growth = related.Where(e => e.Event.Kind == "source-regeneration").Sum(e => Number(e.Event.Detail));
            var loss = related.Where(e => e.Event.Kind == "source-loss").Sum(e => Number(e.Event.Detail));
            var drain = related.Where(e => e.Event.Kind == "source-depletion").Sum(e => NamedNumber(e.Event.Detail, "quantity"));
            var previous = state.Places.FirstOrDefault(p => p.Place == place.Id);
            var sources = related.Select(e => $"inventory-event:{e.Index}:{e.Event.Kind}").Append($"physical-place:{place.Id}:month:{context.Month}").ToArray();
            state.NaturalFlows.Add(new(context.Month, place.Id, opening, growth, drain, loss, closing,
                checked(closing - (opening + growth - drain - loss)), previous?.Qi ?? place.Qi, place.Qi, place.BlockedUntil,
                rows.Select(s => s.Id).Order().ToArray(), sources));
            var disaster = context.Events.LastOrDefault(e => e.Kind == "재해" && e.TargetKind == "place" && e.Target == place.Id && e.Month > state.LastSettledMonth);
            if (loss > 0) Change(state, context.Month, place.Id, disaster is null ? EcologicalChangeKind.Depletion : EcologicalChangeKind.Disaster,
                opening, closing, disaster is null ? string.Join(",", sources) : $"world-event:{disaster.Id}:" + string.Join(",", sources));
            if (drain > 0 && closing < opening) Change(state, context.Month, place.Id, EcologicalChangeKind.Depletion, opening, closing, string.Join(",", sources));
            if (growth > 0 && closing > opening) Change(state, context.Month, place.Id, EcologicalChangeKind.Recovery, opening, closing, string.Join(",", sources));
            if ((previous?.BlockedUntil ?? 0) <= context.Month - 1 && place.BlockedUntil > context.Month)
                Change(state, context.Month, place.Id, EcologicalChangeKind.RouteClosed, previous?.BlockedUntil ?? 0, place.BlockedUntil, sources[^1]);
            if ((previous?.BlockedUntil ?? 0) > context.Month - 1 && place.BlockedUntil <= context.Month)
                Change(state, context.Month, place.Id, EcologicalChangeKind.RouteRecovered, previous!.BlockedUntil, place.BlockedUntil, sources[^1]);
        }
    }

    private static void RecordSocialChanges(EcologyContext context, EcologyState state)
    {
        foreach (var current in PlaceSnapshot(context))
        {
            var previous = state.Places.FirstOrDefault(p => p.Place == current.Place);
            if (previous is null) continue;
            if (previous.Teachers.Length > 0 && current.Teachers.Length == 0)
                Change(state, context.Month, current.Place, EcologicalChangeKind.EducationLoss, previous.Teachers.Length, 0,
                    "actual-entry-knowledge-holders-departed; surviving-readable-media-not-ruled-out");
            if (previous.Households.Except(current.Households).Any())
                Change(state, context.Month, current.Place, EcologicalChangeKind.HouseholdLoss, previous.Households.Length, current.Households.Length, "actual-household-presence-changed");
            var residents = context.People.Where(p => p.Status == LifeStatus.Alive && p.Place == current.Place).ToArray();
            var adults = residents.Where(p => context.Birth is { } birth && context.Month - p.Born >= birth.Maturity).ToArray();
            if (context.Birth is { Enabled: true, MinimumRealm: > 0 } law && adults.Length >= 2 && adults.Any(p => p.ParentOne != 0) &&
                adults.Where(p => law.BothParents || p.Gestates).Any() && adults.Where(p => law.BothParents || p.Gestates)
                    .All(p => (int)p.Realm < law.MinimumRealm && !(law.MartialEquivalent && p.Martial == 10)) &&
                !state.Changes.Any(c => c.Place == current.Place && c.Kind == EcologicalChangeKind.FertilityBottleneck && c.Month >= context.Month - context.Rules.FacilityActivityMonths))
                Change(state, context.Month, current.Place, EcologicalChangeKind.FertilityBottleneck, adults.Length, 0,
                    $"observed-adult-descendant-realms-below-current-parent-law:{law.MinimumRealm}; other-birth-conditions-not-inferred");
        }
    }

    private static void RecordFacilities(EcologyContext context, EcologyState state, EcologyConsumption[] consumption,
        IReadOnlyList<PublicMarketListing> listings)
    {
        var previouslyActive = state.Facilities.Where(f => f.Active).Select(f => f.Id).ToHashSet();
        var candidates = new List<(int Place, EcologicalFacilityKind Kind, string Name, long[] Operators, string[] Sources, bool Public)>();
        foreach (var place in context.Places)
        {
            var residents = context.People.Where(p => p.Status == LifeStatus.Alive && p.Place == place.Id).ToArray();
            var used = consumption.Where(c => c.Place == place.Id && residents.Any(p => p.Id == c.Person)).ToArray();
            var offers = listings.Where(l => l.Place == place.Id && residents.Any(p => p.Id == l.Seller)).ToArray();
            var jobs = context.Inventory.Jobs.Where(j => j.Place == place.Id && j.Source == 0 &&
                (InventoryDefinitions.Recipes.ContainsKey(j.Recipe) || j.Recipe is "repair" or "medical-treatment" or "appraisal" or "array-maintenance") &&
                j.Status is "processing" or "completed" && j.Due >= context.Month - context.Rules.FacilityActivityMonths &&
                residents.Any(p => p.Id == j.Actor)).ToArray();
            if (used.Length > 0 && residents.Any(p => p.Household != 0)) candidates.Add((place.Id, EcologicalFacilityKind.HouseholdCamp,
                "가계 생활 거점", used.Select(c => c.Person).Distinct().Order().ToArray(), used.Select(c => c.Source).ToArray(), true));
            if (offers.Length > 0) candidates.Add((place.Id, EcologicalFacilityKind.Market, "노점 시장",
                offers.Select(l => l.Seller).Distinct().Order().ToArray(), offers.Select(l => l.Source).ToArray(), true));
            if (jobs.Length > 0) candidates.Add((place.Id, EcologicalFacilityKind.Workshop, "노천 공방",
                jobs.Select(j => j.Actor).Distinct().Order().ToArray(), jobs.Select(j => $"inventory-job:{j.Id}:labor:{j.Labor}").ToArray(), true));
            if (residents.Where(p => p.Household != 0).Select(p => p.Household).Distinct().Count() >= 2 && offers.Length > 0 && jobs.Length > 0)
                candidates.Add((place.Id, EcologicalFacilityKind.Settlement, "교역 취락", residents.Select(p => p.Id).Order().ToArray(),
                    offers.Select(o => o.Source).Concat(jobs.Select(j => $"inventory-job:{j.Id}")).ToArray(), true));
            var observations = context.Inventory.Observations.Where(o => o.Place == place.Id && residents.Any(p => p.Id == o.Observer) &&
                o.Month >= context.Month - context.Rules.FacilityActivityMonths).ToArray();
            if (observations.Length > 0) candidates.Add((place.Id, EcologicalFacilityKind.ExploredSite, "비경·자연 탐색지",
                observations.Select(o => o.Observer).Distinct().Order().ToArray(), observations.Select(o => $"personal-source:{o.Source}:{o.Month}:{o.Evidence}").ToArray(), false));
        }
        for (var i = 0; i < state.Facilities.Count; i++) state.Facilities[i] = state.Facilities[i] with { Active = false };
        foreach (var candidate in candidates)
        {
            var index = state.Facilities.FindIndex(f => f.Place == candidate.Place && f.Kind == candidate.Kind);
            var existing = index < 0 ? null : state.Facilities[index];
            var updated = new EcologicalFacility(existing?.Id ?? state.NextId++, candidate.Place, candidate.Kind, candidate.Name,
                existing?.Founded ?? context.Month, context.Month, true, candidate.Public, candidate.Operators, candidate.Sources);
            if (index < 0) state.Facilities.Add(updated); else state.Facilities[index] = updated;
            if (existing is null || !previouslyActive.Contains(updated.Id))
                Change(state, context.Month, updated.Place, EcologicalChangeKind.FacilityOpened, 0, 1,
                    $"actual-facility:{updated.Id}:operators:{string.Join(',', updated.Operators)}:" + string.Join(',', updated.Sources));
        }
        foreach (var facility in state.Facilities.Where(f => !f.Active && previouslyActive.Contains(f.Id)))
            Change(state, context.Month, facility.Place, EcologicalChangeKind.FacilityClosed, 1, 0, $"actual-facility:{facility.Id}:no-current-operating-activity");
    }

    private static List<EcologyStockSnapshot> StockSnapshot(EcologyContext context) => context.Inventory.Sources.OrderBy(s => s.Id)
        .Select(s => new EcologyStockSnapshot(s.Id, s.Place, s.Definition, s.Remaining, s.Capacity, context.Month)).ToList();
    private static List<EcologyPlaceSnapshot> PlaceSnapshot(EcologyContext context) => context.Places.OrderBy(p => p.Id).Select(place =>
    {
        var residents = context.People.Where(p => p.Status == LifeStatus.Alive && p.Place == place.Id).ToArray();
        return new EcologyPlaceSnapshot(place.Id, residents.Length, residents.Where(p => p.Household != 0).Select(p => p.Household).Distinct().Order().ToArray(),
            residents.Where(p => p.Understanding.GetValueOrDefault("entry") > 0).Select(p => p.Id).Order().ToArray(), place.Qi, place.BlockedUntil, context.Month);
    }).ToList();
    private static void Change(EcologyState state, long month, int place, EcologicalChangeKind kind, long before, long after, string source) =>
        state.Changes.Add(new(state.NextId++, month, place, kind, before, after, source));
    private static void Prune(EcologyContext context, EcologyState state)
    {
        var cutoff = context.Month - context.Rules.HistoryMonths + 1;
        state.Markets.RemoveAll(x => x.Month < cutoff); state.NaturalFlows.RemoveAll(x => x.Month < cutoff); state.Demand.RemoveAll(x => x.Month < cutoff);
        state.Consumption.RemoveAll(x => x.Month < cutoff); state.Production.RemoveAll(x => x.Month < cutoff); state.Trades.RemoveAll(x => x.Month < cutoff);
        state.Losses.RemoveAll(x => x.Month < cutoff);
        if (state.Changes.Count > context.Rules.MaximumChanges) state.Changes.RemoveRange(0, state.Changes.Count - context.Rules.MaximumChanges);
    }
    private static long Number(string value) => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) ? amount : 0;
    private static long Milli(long amount, long quantity) => (long)Math.Min(long.MaxValue, (decimal)amount * 1000 / quantity);
    private static long NamedNumber(string detail, string key)
    {
        var fields = detail.Split(':'); var index = Array.IndexOf(fields, key);
        return index >= 0 && index + 1 < fields.Length ? Number(fields[index + 1]) : 0;
    }
    private static EcologyState Copy(EcologyState state) => JsonSerializer.Deserialize<EcologyState>(JsonSerializer.SerializeToUtf8Bytes(state))!;
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }

    // Structural DTOs keep the reader source-compatible with older inventories without inventing missing audit facts.
    private sealed record PaidSettlementEvidence(long? PaidAmount, int? PaidQuantity, int? PaidQuality, long? EndedMonth);
    private sealed record PhysicalFlowEvidence(long Sequence = 0, string Definition = "", int Quantity = 0,
        int Place = -1, long Owner = 0, string ItemSource = "", bool IsLoss = false, bool OwnerObserved = false);
}

internal sealed class EcologyFacts
{
    private readonly EcologyContext context;
    public Dictionary<long, Person> People { get; }
    public Dictionary<long, Item> Items { get; }
    private readonly Dictionary<long, ItemMetadata> metadata;
    public EcologyFacts(EcologyContext value)
    { context = value; People = value.People.ToDictionary(p => p.Id); Items = value.Items.ToDictionary(i => i.Id); metadata = value.Inventory.Metadata.ToDictionary(m => m.Item); }
    private Item Root(Item item)
    {
        var seen = new HashSet<long>();
        while (item.Container != 0)
        { if (!seen.Add(item.Id)) throw new InvalidDataException("Ecology cannot inspect a cyclic physical container."); item = Items[item.Container]; }
        return item;
    }
    public int Location(Item item)
    {
        var root = Root(item); if (root.Place >= 0) return root.Place;
        var detail = metadata.GetValueOrDefault(root.Id); var holder = detail?.Carrier > 0 ? detail.Carrier : detail?.Custodian > 0 ? detail.Custodian : root.Owner;
        return People[holder].Place;
    }
    public bool Accessible(Person p, Item item)
    {
        if (item.Owner != p.Id || item.Reserved != 0 || p.Equipment.ContainsValue(item.Id) || Location(item) != p.Place) return false;
        var root = Root(item); var detail = metadata.GetValueOrDefault(root.Id);
        var holder = detail?.Carrier > 0 ? detail.Carrier : detail?.Custodian > 0 ? detail.Custodian : root.Owner;
        if (root.Place < 0 && holder != p.Id && !(detail?.UseRights.Contains(p.Id) ?? false)) return false;
        while (item.Container != 0) { item = Items[item.Container]; if (item.Reserved != 0) return false; }
        return true;
    }
    public long FreeCarry(Person p)
    {
        var capacity = context.Inventory.BaseCarryCapacity + p.Equipment.Values.Distinct().Where(Items.ContainsKey)
            .Sum(id => metadata.GetValueOrDefault(id)?.Capacity ?? InventoryDefinitions.Definition(Items[id].Definition).Item.Capacity);
        var weight = context.Items.Where(i =>
        {
            var root = Root(i); var detail = metadata.GetValueOrDefault(root.Id);
            return root.Place < 0 && (detail?.Carrier > 0 ? detail.Carrier : detail?.Custodian > 0 ? detail.Custodian : root.Owner) == p.Id;
        }).Sum(i => checked((long)i.Quantity * InventoryDefinitions.Definition(i.Definition).Item.Weight));
        return Math.Max(0, capacity - weight);
    }
}
