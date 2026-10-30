using Chronicles.Game.Ecology;
using Chronicles.Game.Inventory;

namespace Chronicles.Game;

public sealed partial class GameWorld
{
    private EcologyRules CurrentEcologyRules() => new()
    {
        HistoryMonths = checked((int)state.Laws.GetValueOrDefault("economy.historyMonths", 24)),
        MaximumChanges = checked((int)state.Laws.GetValueOrDefault("ecology.historyChanges", 512)),
        FacilityActivityMonths = checked((int)state.Laws.GetValueOrDefault("economy.facilityActivityMonths", 6)),
        FoodReserveMonths = checked((int)state.Laws.GetValueOrDefault("economy.foodReserveMonths", 3)),
        MaximumSaleUnits = checked((int)state.Laws.GetValueOrDefault("economy.saleBatch", 12)),
        DefaultUnitPrice = state.Laws.GetValueOrDefault("economy.defaultUnitPrice", 1)
    };
    private EcologyContext CurrentEcology() => new(Month, Revision, state.People, state.Groups, state.Places, state.Items,
        state.Inventory, state.Events, Law("ecology.foodUse"), CurrentEcologyRules(),
        new(Law("birth.enabled") != 0, IntLaw("birth.minimumRealm"), Law("birth.maturity"), Law("birth.bothParents") != 0, Law("birth.martialEquivalent") != 0),
        new SortedDictionary<long, long>(state.People.ToDictionary(p => p.Id, PeopleMaturityMonths)));

    // Call after inventory initialization in Create/Restore, before later physical consumption is recorded.
    private void InitializeEcology()
    {
        if (!state.Ecology.Initialized) state.Ecology = EcologyEngine.Initialize(CurrentEcology());
    }

    // Call only after common Consume succeeds. Inventory production inputs are sampled from their actual process receipts.
    private void RecordEcologyConsumption(Person person, string definition, int quantity)
    {
        if (quantity == 0) return;
        if (quantity < 0 || !InventoryDefinitions.Items.ContainsKey(definition)) throw new ArgumentOutOfRangeException(nameof(quantity));
        InitializeEcology();
        // The common world already owns this successful physical transaction; append its witness once in that transaction.
        var id = state.Ecology.NextId++;
        state.Ecology.Consumption.Add(new(id, Month, person.Id, person.Household, person.Sect, person.Place, definition, quantity,
            $"common-consumption:{id}:person:{person.Id}:month:{Month}"));
    }

    // End of Step, after actual work, movement, inheritance and life effects. Repeated sampling in a month is a no-op.
    private void SettleEcology()
    {
        InitializeEcology(); var context = CurrentEcology(); state.Ecology = EcologyEngine.Settle(context, state.Ecology);
        foreach (var place in state.Places)
        {
            // These labels describe observed activity; they never grant facility capacity, items, rights or workers.
            foreach (var label in EcologyFacilityLabels) place.Facilities.Remove(label);
            foreach (var facility in state.Ecology.Facilities.Where(f => f.Place == place.Id && f.Active && f.Public))
                if (!place.Facilities.Contains(facility.Name)) place.Facilities.Add(facility.Name);
            place.Facilities.Sort(StringComparer.Ordinal);
        }
    }
    private static readonly string[] EcologyFacilityLabels = ["가계 생활 거점", "노점 시장", "노천 공방", "교역 취락"];

    // Inventory remains the sole source authority; its regeneration allocator may use this seasonal budget.
    private int EcologyRegrowthBudget(Place place)
    {
        var baseline = Law("ecology.regrowth");
        var strength = Math.Clamp(state.Laws.GetValueOrDefault("ecology.seasonStrength", 25), 0, 100);
        var season = (int)(Month % 12 / 3);
        var shift = season switch { 0 => -strength, 1 => strength, 2 when place.Terrain is "하천" or "습지" => strength / 2, _ => 0 };
        return checked((int)(baseline * (100 + shift) / 100));
    }

    internal EconomyView EconomyDetail() => EcologyEngine.View(CurrentEcology(), state.Ecology, state.Controlled == 0 ? null : state.Controlled);
    internal void ValidateEcologyDomain() => EcologyEngine.Validate(CurrentEcology(), state.Ecology);

    private EcologyDecisionInput EcologyDecisionFor(Person person)
    {
        var context = CurrentEcology(); var facts = new EcologyFacts(context);
        var own = state.Items.Where(i => i.Owner == person.Id && facts.Accessible(person, i)).OrderBy(i => i.Id).Select(item =>
        {
            var appraisal = state.Inventory.Appraisals.LastOrDefault(a => a.Appraiser == person.Id && a.Item == item.Id && a.Month <= Month);
            var available = facts.Accessible(person, item);
            return new EcologicalOwnStock(item.Id, item.Definition, item.Quantity, appraisal?.EstimatedQuality,
                InventoryDefinitions.Definition(item.Definition).Item.Weight, available, person.Equipment.ContainsValue(item.Id),
                available && item.Container == 0 && item.BoundSoul == 0 && !state.Inventory.Arrays.Any(a => a.Item == item.Id));
        }).ToArray();
        var sources = InventoryKnowledgeQueries.KnownSources(state.Inventory, person.Id).Where(o => !string.IsNullOrEmpty(o.Definition))
            .Select(o => new EcologicalKnownSource(o.Source, o.Place, o.Definition,
                o.Remaining, o.Month, $"personal-source:{o.Source}:{o.Month}:{o.Evidence}")).ToList();
        if (!sources.Any(s => s.Place == person.Place && s.Definition == "food") && person.ObservedPlaces.TryGetValue(person.Place, out var local))
            sources.Add(new(0, person.Place, "food", local.Source == 0 ? 0 : null, local.Month, $"personal-local-observation:{local.Place}:{local.Month}"));
        var knownPlaces = person.KnownPlaces.Select(id =>
        {
            var observed = person.ObservedPlaces.GetValueOrDefault(id);
            return new EcologicalKnownPlace(id, person.ObservedPlaces.ContainsKey(id) ? observed.Source : null, observed.Month,
                state.Places[person.Place].Neighbors.Contains(id), person.Beliefs.TryGetValue($"blocked:{id}", out var b) && b.Expires > Month);
        }).ToArray();
        var recipes = InventoryDefinitions.Recipes.Values.Where(r => person.Understanding.GetValueOrDefault("recipe:" + r.Id) > 0).OrderBy(r => r.Id, StringComparer.Ordinal)
            .Select(r => new EcologicalRecipe(r.Id, r.Output, r.Quantity, r.Inputs)).ToArray();
        var dependents = state.People.Count(p => p.Status == LifeStatus.Alive && p.Guardian == person.Id && p.Place == person.Place && Month - p.Born < PeopleMaturityMonths(p));
        var planned = person.Plan.FirstOrDefault(step => step.Action == "craft" && recipes.Any(r => r.Id == step.Data));
        var workNeeds = planned is null ? [] : recipes.Single(r => r.Id == planned.Data).Inputs.Select(i => new EcologicalWorkNeed(i.Key, i.Value, planned.Data)).ToArray();
        var view = EcologyEngine.View(context, state.Ecology, person.Id);
        return new(person.Id, Month, person.Place, person.Money, facts.FreeCarry(person), checked(Law("ecology.foodUse") * (dependents + 1)),
            own, view.Listings, view.PublicPrices, sources, knownPlaces, recipes, context.Rules, workNeeds);
    }

    // Before the fallback livelihood/gather branch in Decide, after urgent health and care obligations.
    private bool TryEcologyPlan(Person person)
    {
        if (person.Action != ActionKind.Idle || person.Status != LifeStatus.Alive || person.Health < 10) return false;
        InitializeEcology(); var decision = EcologyPlanner.Decide(EcologyDecisionFor(person));
        if (decision is null) return false;
        var commandId = $"ecology-ai:{person.BodyId}:{Month}:{Revision}:{state.NextId}:{decision.Kind}";
        string? error;
        if (decision.Kind == "offer")
        {
            // Split+offer use one isolated inventory candidate. An unsuccessful offer cannot leak a split or an ID.
            var current = CurrentInventory(); var item = current.Items.Single(i => i.Id == decision.Target); var target = item.Id;
            if (decision.Quantity < item.Quantity)
            {
                var split = InventoryEngine.Apply(current, new SplitInventoryItem(commandId + ":split", person.Id, item.Id, decision.Quantity));
                if (!split.Accepted) return false; current = split.State; target = split.Subject;
            }
            var offer = InventoryEngine.Apply(current, new OfferItem(commandId, person.Id, target, decision.Price, Month + 12));
            if (!offer.Accepted) return false;
            ApplyInventory(offer.State); person.Action = ActionKind.Guard; person.Due = Month + 1;
            person.ActionTarget = offer.Subject; person.ActionData = "ecology:market"; error = null;
        }
        else if (decision.Kind is "trade" or "craft" or "gather")
        {
            var command = new GameCommand(commandId, person.Id, decision.Kind, decision.Target,
                Data: decision.Kind == "craft" ? decision.Recipe : decision.Definition);
            if (!TryInventoryCommand(command, person, out error)) return false;
            if (error is null && decision.Kind == "trade")
            {
                person.Action = ActionKind.Guard; person.Due = Month + 1; person.ActionTarget = decision.Target; person.ActionData = "ecology:purchase";
                if (decision.Recipe.Length > 0) person.Plan.RemoveAll(step => step.Action is "gather" or "mining" && step.Data == decision.Definition);
            }
        }
        else error = Start(person, "travel", decision.Target, "");
        if (error is not null)
        { Remember(person, "ecology:lastFailure", error, decision.Evidence, 100); return false; }
        person.Goal = decision.Goal; person.Reason = decision.Evidence; return true;
    }
}
