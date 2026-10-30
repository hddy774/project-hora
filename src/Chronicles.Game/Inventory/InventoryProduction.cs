namespace Chronicles.Game.Inventory;

public static partial class InventoryEngine
{
    private static AssetReservation Reservation(InventoryState state, long actor, string purpose)
    {
        var reservation = new AssetReservation { Id = Id(state), Actor = actor, Purpose = purpose };
        state.Ledger.Reservations.Add(reservation); return reservation;
    }
    private static bool Available(InventoryState state, long actor, Item item)
    {
        try { Accessible(state, actor, item.Id); return true; }
        catch (RuleViolation) { return false; }
    }
    private static List<Item> ReserveInputs(InventoryState state, long actor, IReadOnlyDictionary<string, int> inputs, AssetReservation reservation)
    {
        var result = new List<Item>();
        foreach (var ingredient in inputs.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var remaining = ingredient.Value;
            foreach (var item in state.Items.Where(i => i.Owner == actor && i.Definition == ingredient.Key).OrderBy(i => i.Id).ToArray())
            {
                if (!Available(state, actor, item)) continue;
                Require(item.BoundSoul == 0 || item.BoundSoul == PersonAt(state, actor).SoulId, "Ingredient belongs to another soul.");
                var count = Math.Min(remaining, item.Quantity);
                var selected = count == item.Quantity ? item : SplitItem(state, item, count);
                selected.Reserved = reservation.Id; reservation.Items.Add(selected.Id); result.Add(selected);
                remaining -= count; if (remaining == 0) break;
            }
            Require(remaining == 0, $"Insufficient accessible stock: {ingredient.Key}.");
        }
        return result;
    }
    private static void FreeReservation(InventoryState state, long id)
    {
        var reservation = state.Ledger.Reservations.Single(r => r.Id == id);
        foreach (var item in state.Items.Where(i => i.Reserved == id)) item.Reserved = 0;
        reservation.Active = false;
    }
    private static void FreeLabor(InventoryState state, long id)
    { if (id != 0) state.Ledger.Labor.Single(l => l.Id == id).Active = false; }
    private static MaterialFlow Flow(InventoryState state, string definition)
    {
        if (!state.Materials.TryGetValue(definition, out var flow)) state.Materials.Add(definition, flow = new());
        return flow;
    }
    private static void ConsumePhysical(InventoryState state, Item item, int quantity, bool loss = false, long actor = 0)
    {
        Require(quantity > 0 && quantity <= item.Quantity, "Invalid physical consumption.");
        state.Ledger.Events.Add(new(state.Month, loss ? "physical-loss" : "physical-consumption", actor, item.Id,
            "Actual stock decrement before removing the physical instance.", checked(state.Ledger.NextEventSequence++), item.Definition,
            quantity, Location(state, item.Id), item.Owner, item.Source, loss, item.Quality, item.Damage, item.Defects));
        if (loss) Flow(state, item.Definition).Loss = checked(Flow(state, item.Definition).Loss + quantity);
        else Flow(state, item.Definition).Consumption = checked(Flow(state, item.Definition).Consumption + quantity);
        item.Quantity -= quantity; if (item.Quantity == 0) Remove(state, item);
    }
    private static Item Produce(InventoryState state, long owner, string definition, int quantity, int quality,
        ItemDefect defects, long boundSoul, string source, IReadOnlyList<string> provenance, bool extraction = false)
    {
        InventoryDefinitions.Definition(definition);
        var item = new Item { Id = Id(state), Owner = owner, Definition = definition, Quantity = quantity, Quality = quality,
            Defects = defects, BoundSoul = boundSoul, Place = -1, Source = source };
        state.Items.Add(item); Detail(state, item).Provenance = [.. provenance, source];
        var flow = Flow(state, definition);
        if (extraction) flow.Extraction = checked(flow.Extraction + quantity); else flow.Production = checked(flow.Production + quantity);
        return item;
    }
    private static void WorkDuration(InventoryState state, long due, int units, long required)
    {
        Require(due > state.Month && units is > 0 and <= 100 && checked((due - state.Month) * units) >= required,
            "Reserved labor duration is insufficient for the actual work.");
    }
    private static long StartJob(InventoryState state, StartProduction command)
    {
        Require(InventoryDefinitions.Recipes.TryGetValue(command.Recipe, out var recipe), "Unknown production recipe.");
        var actor = PersonAt(state, command.Actor);
        WorkDuration(state, command.Due, command.LaborUnits, state.Ledger.ProductionWork);
        var labor = AddLabor(state, actor.Id, $"production:{command.Recipe}", state.Month, command.Due,
            command.LaborUnits, command.Facility, command.FacilityCapacity);
        var reservation = Reservation(state, actor.Id, "production");
        var inputs = ReserveInputs(state, actor.Id, recipe!.Inputs, reservation);
        var quantity = inputs.Sum(i => (long)i.Quantity);
        var bindings = inputs.Where(i => i.BoundSoul != 0).Select(i => i.BoundSoul).Distinct().ToArray();
        Require(bindings.Length <= 1, "Different soul bindings cannot be combined.");
        var job = new ProductionJob { Id = Id(state), Actor = actor.Id, Recipe = recipe.Id, Place = actor.Place,
            OutputDefinition = recipe.Output, OutputQuantity = recipe.Quantity,
            Due = command.Due, Labor = labor, Reservation = reservation.Id, Inputs = inputs.Select(i => i.Id).ToList(),
            Quality = (int)(inputs.Sum(i => (long)i.Quality * i.Quantity) / Math.Max(1, quantity)),
            Damage = inputs.Max(i => i.Damage), Defects = inputs.Aggregate(ItemDefect.None, (defects, item) => defects | item.Defects),
            BoundSoul = bindings.SingleOrDefault(), Provenance = inputs.SelectMany(i => Metadata(state, i.Id)?.Provenance ?? [i.Source])
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList() };
        state.Ledger.Jobs.Add(job); return job.Id;
    }
    private static ProductionJob Job(InventoryState state, long actor, long id)
    {
        var job = state.Ledger.Jobs.SingleOrDefault(j => j.Id == id);
        Require(job is not null && job.Actor == actor && job.Status is "reserved" or "processing", "Production is absent or already terminal.");
        Require(PersonAt(state, actor).Place == job!.Place, "Production requires presence at its work location.");
        return job;
    }
    private static long BeginJob(InventoryState state, BeginProduction command)
    {
        var job = Job(state, command.Actor, command.Job);
        Require(job.Status == "reserved" && state.Month == state.Ledger.Labor.Single(l => l.Id == job.Labor).Start,
            "Processing must begin at its reserved labor start; delayed work needs a new reservation.");
        foreach (var id in job.Inputs)
        {
            var item = ItemAt(state, id);
            Require(item.Reserved == job.Reservation && item.Owner == command.Actor && Location(state, id) == job.Place, "Reserved ingredient became inaccessible.");
            job.Consumed.Add(new(item.Definition, item.Quantity)); ConsumePhysical(state, item, item.Quantity, actor: command.Actor);
        }
        job.Status = "processing"; Event(state, "process", job.Actor, job.Id, "Inputs consumed at the process boundary."); return job.Id;
    }
    private static long FinishJob(InventoryState state, FinishProduction command)
    {
        var job = Job(state, command.Actor, command.Job);
        Require(job.Status == "processing" && state.Month >= job.Due, "Processing and its reserved duration must finish first.");
        long subject = job.Id;
        if (job.Source != 0)
        {
            var source = state.Ledger.Sources.Single(s => s.Id == job.Source);
            if (job.OutputDefinition.Length == 0) job.OutputDefinition = source.Definition;
            Require(source.Definition == job.OutputDefinition, "Natural source changed after the extraction commitment.");
            Require(source.Remaining >= job.OutputQuantity, "Reserved natural stock is missing."); source.Remaining -= job.OutputQuantity;
            Event(state, "source-depletion", job.Actor, source.Id, $"job:{job.Id}:quantity:{job.OutputQuantity}:success:{command.Succeeded}");
            if (command.Succeeded)
                subject = Produce(state, job.Actor, job.OutputDefinition, job.OutputQuantity, job.Quality, ItemDefect.None, 0,
                    $"extraction:{source.Id}:job:{job.Id}", [source.Origin], true).Id;
        }
        else if (command.Succeeded && job.Recipe == "repair")
        {
            var target = ItemAt(state, job.Target);
            Require(target.Reserved == job.Reservation && Location(state, target.Id) == job.Place, "Repair target moved.");
            target.Damage = Math.Max(0, target.Damage - 25); subject = target.Id;
        }
        else if (command.Succeeded && job.Recipe == "medical-treatment")
        {
            var patient = PersonAt(state, job.Target);
            Require(patient.Place == job.Place, "Patient left the actual treatment location.");
            patient.Health = Math.Min(100, patient.Health + 10 + job.Quality / 500); subject = patient.Id;
            var doctor = PersonAt(state, job.Actor); doctor.Skills["medicine"] = Math.Min(10000, doctor.Skills.GetValueOrDefault("medicine") + 1);
        }
        else if (command.Succeeded && job.Recipe == "appraisal")
        {
            var item = ItemAt(state, job.Target);
            Require(item.Reserved == job.Reservation && Location(state, item.Id) == job.Place, "Appraisal target left its custody.");
            var skill = PersonAt(state, job.Actor).Skills.GetValueOrDefault("appraisal");
            state.Ledger.Appraisals.Add(new(job.Actor, item.Id, item.Definition, skill >= 5000 ? item.Quality : item.Quality / 1000 * 1000,
                item.Damage, skill >= 1000 ? item.Defects : item.Defects & ItemDefect.MissingPages, state.Month));
            PersonAt(state, job.Actor).Skills["appraisal"] = Math.Min(10000, skill + 1); subject = item.Id;
        }
        else if (command.Succeeded && job.Recipe == "exploration")
        {
            var source = state.Ledger.Sources.Single(s => s.Id == job.Target);
            state.Ledger.Observations.Add(new(job.Actor, source.Id, source.Place, source.Remaining, state.Month, $"actual exploration:job:{job.Id}", source.Definition, source.Activity));
            subject = source.Id;
        }
        else if (command.Succeeded && job.Recipe == "array-maintenance")
        {
            var array = state.Ledger.Arrays.Single(a => a.Id == job.Target);
            Require(IntactArray(state, array), "Array frame must be repaired before funded upkeep can activate it.");
            array.Condition = Math.Min(100 - ItemAt(state, array.Item).Damage, array.Condition + 25); array.PaidUntil = checked(state.Month + 12); array.Active = true;
            state.Ledger.Effects.Add(new($"array:{array.Id}:job:{job.Id}", array.Place, "qi.place", 10, state.Month, array.PaidUntil));
            state.Ledger.Effects.Add(new($"array:{array.Id}:defense:job:{job.Id}", array.Place, "defense.place", 10, state.Month, array.PaidUntil));
            subject = array.Id;
        }
        else if (command.Succeeded)
        {
            var recipe = InventoryDefinitions.Recipes[job.Recipe];
            if (job.OutputDefinition.Length == 0) { job.OutputDefinition = recipe.Output; job.OutputQuantity = recipe.Quantity; }
            var output = Produce(state, job.Actor, job.OutputDefinition, job.OutputQuantity, job.Quality, job.Defects, job.BoundSoul,
                $"production:job:{job.Id}", job.Provenance); output.Damage = job.Damage;
            subject = output.Id;
            if (command.AwardSkill)
            {
                var actor = PersonAt(state, job.Actor); actor.Skills[recipe.Skill] = Math.Min(10000, actor.Skills.GetValueOrDefault(recipe.Skill) + 1);
            }
        }
        job.Status = command.Succeeded ? "completed" : "failed"; FreeReservation(state, job.Reservation); FreeLabor(state, job.Labor);
        SpillOverflow(state, job.Actor);
        Event(state, job.Status, job.Actor, job.Id, command.Succeeded ? "Single atomic output settlement." : "Consumed inputs remain consumed."); return subject;
    }
    private static long CancelJob(InventoryState state, CancelProduction command)
    {
        var job = state.Ledger.Jobs.SingleOrDefault(j => j.Id == command.Job);
        Require(job is not null && job.Actor == command.Actor && job.Status is "reserved" or "processing", "Only active production can be cancelled.");
        if (job!.Source != 0 && job.Status == "processing")
        {
            var source = state.Ledger.Sources.Single(s => s.Id == job.Source); source.Remaining -= job.OutputQuantity;
            Event(state, "source-depletion", job.Actor, source.Id, $"job:{job.Id}:quantity:{job.OutputQuantity}:cancelled-processing");
        }
        job.Status = "cancelled"; FreeReservation(state, job.Reservation); FreeLabor(state, job.Labor);
        Event(state, "cancelled", job.Actor, job.Id, $"Unused stock released; {job.Consumed.Sum(c => c.Quantity)} consumed units are not refunded."); return job.Id;
    }
    private static long Extract(InventoryState state, ExtractResource command)
    {
        var actor = PersonAt(state, command.Actor); var source = state.Ledger.Sources.SingleOrDefault(s => s.Id == command.Source);
        Require(source is not null && source.Place == actor.Place && command.Quantity > 0, "Natural source is absent or remote.");
        var activity = InventoryDefinitions.ExtractionActivity(source!.Definition);
        Require(activity is not null && activity == source.Activity && source.Activity.StartsWith("source.", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(source.Origin), "Source activity does not produce this resource.");
        var reserved = state.Ledger.Jobs.Where(j => j.Source == source!.Id && j.Status is "reserved" or "processing").Sum(j => (long)j.OutputQuantity);
        Require(source!.Remaining - reserved >= command.Quantity, "Finite source has insufficient unreserved stock.");
        WorkDuration(state, command.Due, command.LaborUnits, checked((long)command.Quantity * state.Ledger.ExtractionWorkPerUnit));
        var reservation = Reservation(state, actor.Id, "extraction");
        var labor = AddLabor(state, actor.Id, source.Activity, state.Month, command.Due, command.LaborUnits);
        var job = new ProductionJob { Id = Id(state), Actor = actor.Id, Recipe = "extraction", Source = source.Id, Place = actor.Place,
            Due = command.Due, Labor = labor, Reservation = reservation.Id, OutputDefinition = source.Definition, OutputQuantity = command.Quantity, Quality = 5000 };
        state.Ledger.Jobs.Add(job); return job.Id;
    }
    private static long Consume(InventoryState state, ConsumeItem command)
    {
        var item = Accessible(state, command.Actor, command.Item, false); var actor = PersonAt(state, command.Actor);
        var definition = InventoryDefinitions.Definition(item.Definition);
        Require(definition.Effect.Length > 0 && item.Damage < 100 &&
            (item.Defects & (ItemDefect.BrokenCircuit | ItemDefect.LostCore)) == 0 &&
            (item.BoundSoul == 0 || item.BoundSoul == actor.SoulId), "Item has no usable effect.");
        var effect = item.Definition == "soulWard" ? "soulProtection" : definition.Effect;
        Require(effect != "soulProtection" || !state.Ledger.Effects.Any(e => e.Target == actor.Id && e.Kind == effect && e.End > state.Month), "Soul protection is already active.");
        var magnitude = Math.Max(1, definition.Magnitude * (5000 + item.Quality) / 10000);
        var cause = $"item:{item.Id}:unit:{item.Quantity}";
        var duration = effect is "defense" or "breakthrough" ? 1 : effect == "soulProtection" ? 12 : 0;
        state.Ledger.Effects.Add(new(cause, actor.Id, effect, magnitude, state.Month, checked(state.Month + duration)));
        if (effect is "healing" or "nutrition") actor.Health = Math.Min(100, actor.Health + magnitude);
        if (effect == "growth") actor.Work = checked(actor.Work + magnitude);
        if (item.Defects.HasFlag(ItemDefect.Contamination))
        {
            actor.Health = Math.Max(1, actor.Health - 20);
            state.Ledger.Effects.Add(new(cause + ":contamination", actor.Id, "toxicity", 20, state.Month, state.Month));
        }
        ConsumePhysical(state, item, 1, actor: actor.Id); Event(state, "use", actor.Id, item.Id, cause); return item.Id;
    }
    private static long Repair(InventoryState state, RepairItem command)
    {
        var target = Accessible(state, command.Actor, command.Item, true, false);
        Require(target.Reserved == 0 && target.Damage > 0 && target.Quantity == 1 &&
            InventoryDefinitions.Definition(target.Definition).OccupiedSlots.Count > 0 && state.Items.All(i => i.Container != target.Id), "Repair requires an unreserved damaged individual item with empty storage.");
        WorkDuration(state, command.Due, 100, state.Ledger.ProfessionalWork);
        var labor = AddLabor(state, command.Actor, "repair", state.Month, command.Due, 100);
        var reservation = Reservation(state, command.Actor, "repair");
        target.Reserved = reservation.Id; reservation.Items.Add(target.Id);
        var inputs = ReserveInputs(state, command.Actor, new Dictionary<string, int>(StringComparer.Ordinal) { ["ore"] = 1 }, reservation);
        var job = new ProductionJob { Id = Id(state), Actor = command.Actor, Recipe = "repair", Target = target.Id,
            Place = PersonAt(state, command.Actor).Place, Due = command.Due, Labor = labor, Reservation = reservation.Id,
            Inputs = inputs.Select(i => i.Id).ToList() };
        state.Ledger.Jobs.Add(job); return job.Id;
    }
    private static ProductionJob ProfessionalJob(InventoryState state, long actorId, string activity, long target, long due,
        IReadOnlyDictionary<string, int> materials)
    {
        WorkDuration(state, due, 100, state.Ledger.ProfessionalWork);
        var actor = PersonAt(state, actorId); var labor = AddLabor(state, actor.Id, activity, state.Month, due, 100);
        var reservation = Reservation(state, actor.Id, activity); var inputs = ReserveInputs(state, actor.Id, materials, reservation);
        var units = inputs.Sum(i => (long)i.Quantity);
        var job = new ProductionJob { Id = Id(state), Actor = actor.Id, Recipe = activity, Target = target, Place = actor.Place,
            Due = due, Labor = labor, Reservation = reservation.Id, Inputs = inputs.Select(i => i.Id).ToList(),
            Quality = units == 0 ? 0 : (int)(inputs.Sum(i => (long)i.Quantity * i.Quality) / units) };
        state.Ledger.Jobs.Add(job); return job;
    }
    private static long Treat(InventoryState state, TreatPerson command)
    {
        var patient = PersonAt(state, command.Patient);
        Require(patient.Place == PersonAt(state, command.Actor).Place && (patient.Id == command.Actor || command.PatientConsented), "Treatment requires actual presence and patient agreement.");
        return ProfessionalJob(state, command.Actor, "medical-treatment", patient.Id, command.Due,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["herb"] = 2 }).Id;
    }
    private static long Appraise(InventoryState state, AppraisePhysical command)
    {
        var item = Accessible(state, command.Actor, command.Item, false);
        var job = ProfessionalJob(state, command.Actor, "appraisal", item.Id, command.Due,
            new Dictionary<string, int>(StringComparer.Ordinal));
        item.Reserved = job.Reservation; state.Ledger.Reservations.Single(r => r.Id == job.Reservation).Items.Add(item.Id);
        return job.Id;
    }
    private static long Explore(InventoryState state, ExploreNaturalSource command)
    {
        var source = state.Ledger.Sources.SingleOrDefault(s => s.Id == command.Source);
        Require(source is not null && source.Place == PersonAt(state, command.Actor).Place, "Exploration needs an actual local source.");
        return ProfessionalJob(state, command.Actor, "exploration", command.Source, command.Due,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["food"] = 1 }).Id;
    }
    private static long Install(InventoryState state, InstallArray command)
    {
        var item = Accessible(state, command.Actor, command.Item);
        Require(command.HasInstallationRight && command.Range is > 0 and <= 10 && item.Quantity == 1 &&
            InventoryDefinitions.Definition(item.Definition).Item.Kind == ItemKind.Array && item.Damage < 100 &&
            (item.Defects & (ItemDefect.BrokenCircuit | ItemDefect.LostCore)) == 0, "Installation needs a valid array and actual installation rights.");
        Move(state, item, PersonAt(state, command.Actor), 0, true);
        var array = new ArrayInstallation { Id = Id(state), Item = item.Id, Owner = command.Actor,
            Place = item.Place, Range = command.Range, Condition = 100 - item.Damage };
        state.Ledger.Arrays.Add(array); return array.Id;
    }
    private static ArrayInstallation ArrayAt(InventoryState state, long actor, long id)
    {
        var array = state.Ledger.Arrays.SingleOrDefault(a => a.Id == id);
        Require(array is not null && array.Owner == actor && array.Place == PersonAt(state, actor).Place, "Array is absent, remote, or not owned.");
        return array!;
    }
    private static bool IntactArray(InventoryState state, ArrayInstallation array)
    {
        var item = ItemAt(state, array.Item);
        return item.Damage < 100 && (item.Defects & (ItemDefect.BrokenCircuit | ItemDefect.LostCore)) == 0;
    }
    private static long Maintain(InventoryState state, MaintainArray command)
    {
        var array = ArrayAt(state, command.Actor, command.Installation);
        Require(IntactArray(state, array), "Array frame requires physical repair before upkeep.");
        Require(!state.Ledger.Jobs.Any(j => j.Target == array.Id && j.Recipe == "array-maintenance" && j.Status is "reserved" or "processing"), "Array maintenance is already committed.");
        WorkDuration(state, command.Due, 100, state.Ledger.ProfessionalWork);
        var labor = AddLabor(state, command.Actor, "array-maintenance", state.Month, command.Due, 100);
        var reservation = Reservation(state, command.Actor, "array-maintenance");
        var inputs = ReserveInputs(state, command.Actor, new Dictionary<string, int>(StringComparer.Ordinal) { ["resource.02.0"] = 1 }, reservation);
        var job = new ProductionJob { Id = Id(state), Actor = command.Actor, Recipe = "array-maintenance", Target = array.Id,
            Place = array.Place, Due = command.Due, Labor = labor, Reservation = reservation.Id, Inputs = inputs.Select(i => i.Id).ToList() };
        state.Ledger.Jobs.Add(job); return job.Id;
    }
    private static long Dismantle(InventoryState state, DismantleArray command)
    {
        var array = ArrayAt(state, command.Actor, command.Installation);
        Require(!state.Ledger.Jobs.Any(j => j.Target == array.Id && j.Recipe == "array-maintenance" && j.Status is "reserved" or "processing"), "Maintenance must be cancelled before dismantling.");
        state.Ledger.Arrays.Remove(array); state.Ledger.Effects.RemoveAll(e => e.Cause.StartsWith($"array:{array.Id}:", StringComparison.Ordinal));
        return array.Item;
    }

    /// <summary>Pure, deduplicated contributions; the host applies realm/combat/range policy to these sources.</summary>
    public static IReadOnlyList<InventoryEffect> ActiveEffects(InventoryState state, long target, string kind)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Ledger.Effects.Where(e => e.Target == target && e.Kind == kind && e.Start <= state.Month && e.End > state.Month)
            .DistinctBy(e => e.Cause).OrderBy(e => e.Cause, StringComparer.Ordinal).ToArray();
    }
}
