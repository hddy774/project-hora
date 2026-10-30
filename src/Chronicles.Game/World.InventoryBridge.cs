using System.Globalization;
using System.Text.Json;
using Chronicles.Game.Inventory;

namespace Chronicles.Game;

public sealed partial class GameWorld
{
    partial void BeginInventoryPractice(Person p, string workId, int months, string sourceId, ref long activityId, ref string? error);
    partial void FinishInventoryPractice(Person p, long activityId, bool successful, string receipt, string observation, ref string? error);
    partial void CancelInventoryPractice(Person p, long activityId, string sourceId);

    private string InventoryNonce(string kind, long actor = 0) => string.Create(CultureInfo.InvariantCulture,
        $"inventory:{Month}:{Revision}:{state.NextId}:{actor}:{kind}");

    private void InitializeInventory()
    {
        if (state.Inventory.Initialized) { ProjectInventoryContracts(); return; }
        state.Inventory.Initialized = true;
        state.Inventory.ExtractionWorkPerUnit = Math.Max(1, 100 / IntLaw("economy.gatherYield"));
        foreach (var place in state.Places)
        {
            var profiles = InventorySourceProfiles(place); var totalWeight = profiles.Sum(x => x.Weight);
            var total = Math.Min(place.Source, IntLaw("ecology.sourceCapacity")); var allocated = 0;
            foreach (var profile in profiles)
            {
                var capacity = IntLaw("ecology.sourceCapacity") * (long)profile.Weight / totalWeight;
                var amount = total * (long)profile.Weight / totalWeight; allocated += (int)amount;
                state.Inventory.Sources.Add(new() { Id = Id(), Place = place.Id, Definition = profile.Definition,
                    Activity = InventoryDefinitions.ExtractionActivity(profile.Definition)!, Remaining = amount, Capacity = capacity,
                    LastMonth = Month, Origin = $"창세 자연 원천·지점 {place.Id}·{profile.Definition}" });
            }
            var first = state.Inventory.Sources.First(s => s.Place == place.Id);
            first.Remaining += total - allocated;
            first.Capacity += IntLaw("ecology.sourceCapacity") - state.Inventory.Sources.Where(s => s.Place == place.Id).Sum(s => s.Capacity);
        }
        MigrateInventoryContracts();
        ConfigureInventoryRegeneration(); ProjectInventorySources(); ProjectInventoryContracts();
    }
    private static List<(string Definition, int Weight)> InventorySourceProfiles(Place place)
    {
        var result = new List<(string, int)> { ("food", 8), ("herb", 3), ("ore", 1), ("wood", 2) };
        foreach (var route in InventoryDefinitions.Resources.Where(r => r.Producer.StartsWith("source.", StringComparison.Ordinal)))
        {
            var family = int.Parse(route.Definition.AsSpan(9, 2), CultureInfo.InvariantCulture);
            var available = family switch
            {
                0 or 2 or 15 => place.Terrain == "산지", 3 or 12 => place.Terrain is "평야" or "숲" or "습지",
                4 => place.Terrain == "숲", 5 or 7 => place.Terrain is "숲" or "산지",
                6 => place.Terrain is "하천" or "습지", 8 => place.Terrain is "숲" or "습지",
                11 => place.Terrain is "평야" or "숲", _ => false
            };
            if (available) result.Add((route.Definition, 1));
        }
        return result.OrderBy(x => x.Item1, StringComparer.Ordinal).ToList();
    }
    private void ConfigureInventoryRegeneration()
    {
        foreach (var place in state.Places)
        {
            var sources = state.Inventory.Sources.Where(s => s.Place == place.Id).OrderBy(s => s.Id).ToArray();
            var renewable = sources.Where(s => s.Activity == "source.gathering" || s.Activity == "source.farming").ToArray();
            foreach (var source in sources) source.Regeneration = 0;
            for (var i = 0; i < renewable.Length; i++)
                renewable[i].Regeneration = IntLaw("ecology.regrowth") / renewable.Length + (i < IntLaw("ecology.regrowth") % renewable.Length ? 1 : 0);
        }
    }
    private void MigrateInventoryContracts()
    {
        foreach (var old in state.Contracts.OrderBy(c => c.Id))
        {
            var reservation = old.Status == "open" ? Id() : 0;
            var contract = new InventoryContract { Id = old.Id, Kind = old.Kind, Seller = old.Seller, Buyer = old.Buyer,
                Carrier = old.Carrier, Item = old.Item, Origin = old.Destination, Destination = old.Destination,
                Price = old.Price, Escrow = old.Escrow, Due = old.Due, Status = old.Status, Reservation = reservation };
            if (items.TryGetValue(old.Item, out var item))
            {
                contract.Origin = item.Place < 0 ? people[item.Owner].Place : item.Place;
                if (old.Status == "open")
                {
                    item.Reserved = reservation;
                    state.Inventory.Reservations.Add(new() { Id = reservation, Actor = old.Seller, Purpose = old.Kind, Items = [item.Id] });
                    if (old.Kind == "transport")
                    {
                        item.Place = -1;
                        state.Inventory.Metadata.Add(new() { Item = item.Id, Custodian = old.Carrier, Carrier = old.Carrier, Provenance = [item.Source] });
                        contract.Labor = Id(); state.Inventory.Labor.Add(new() { Id = contract.Labor, Person = old.Carrier,
                            Purpose = "transport", Start = Math.Max(0, Math.Min(Month, old.Due - 1)), End = Math.Max(Month + 1, old.Due), Units = 100 });
                    }
                }
            }
            state.Inventory.Contracts.Add(contract);
        }
        foreach (var p in state.People.Where(p => p.Action == ActionKind.Craft && state.Items.Any(i => i.Reserved == p.ActionTarget)))
        {
            var input = state.Items.Where(i => i.Reserved == p.ActionTarget).ToArray(); var reservation = p.ActionTarget; var jobId = Id(); var labor = Id();
            state.Inventory.Reservations.Add(new() { Id = reservation, Actor = p.Id, Purpose = "legacy-production", Items = input.Select(i => i.Id).ToList() });
            state.Inventory.Labor.Add(new() { Id = labor, Person = p.Id, Purpose = "production", Start = Math.Max(0, Math.Min(Month, p.Due - 1)), End = Math.Max(Month + 1, p.Due), Units = 100 });
            var job = new ProductionJob { Id = jobId, Actor = p.Id, Recipe = p.ActionData, Place = p.Place, Due = p.Due,
                Labor = labor, Reservation = reservation, Status = "processing", Inputs = input.Select(i => i.Id).ToList(),
                Quality = (int)(input.Sum(i => (long)i.Quantity * i.Quality) / input.Sum(i => (long)i.Quantity)),
                Defects = input.Aggregate(ItemDefect.None, (flags, i) => flags | i.Defects), Provenance = input.Select(i => i.Source).ToList() };
            foreach (var item in input) { job.Consumed.Add(new(item.Definition, item.Quantity)); Material(item.Definition).Consumption += item.Quantity; Remove(item); }
            state.Inventory.Jobs.Add(job); p.ActionTarget = jobId; p.ActionData = "inventory:" + job.Recipe;
        }
        Reindex();
    }
    private InventoryState CurrentInventory()
    {
        var inventory = InventoryState.FromWorld(state, state.Inventory);
        foreach (var labor in inventory.Ledger.Labor.Where(l => l.Purpose == "world-action")) labor.Active = false;
        foreach (var p in inventory.People.Where(p => p.Status == LifeStatus.Alive && p.Action != ActionKind.Idle && p.Due > Month))
        {
            if (inventory.Ledger.Jobs.Any(j => j.Actor == p.Id && j.Id == p.ActionTarget && j.Status is "reserved" or "processing") ||
                inventory.Ledger.Contracts.Any(c => c.Carrier == p.Id && c.Id == p.ActionTarget && c.Status == "open")) continue;
            var row = inventory.Ledger.Labor.LastOrDefault(l => l.Person == p.Id && l.Purpose == "world-action" && l.End == p.Due);
            if (row is null) inventory.Ledger.Labor.Add(new() { Id = inventory.NextId++, Person = p.Id, Purpose = "world-action", Start = Month, End = p.Due, Units = 100 });
            else row.Active = true;
        }
        return inventory;
    }
    private void ApplyInventory(InventoryState inventory)
    {
        state.Inventory = inventory.ApplyTo(state); Reindex(); ProjectInventorySources(); ProjectInventoryContracts();
    }
    private string? RunInventory(InventoryCommand command, out long subject)
    {
        var result = InventoryEngine.Apply(CurrentInventory(), command); subject = result.Subject;
        if (!result.Accepted) return result.Message;
        ApplyInventory(result.State); return null;
    }
    private string? ReserveInventoryMaterials(Person p, IReadOnlyDictionary<string, int> materials, string purpose, out long reservation) =>
        RunInventory(new ReserveInventoryMaterials(InventoryNonce(purpose, p.Id), p.Id, purpose, materials), out reservation);
    private string? ReserveInventoryPhysical(Person p, long item, int quantity, string purpose, out long reservation, out long physical)
    {
        var error = RunInventory(new ReserveInventoryPhysical(InventoryNonce(purpose, p.Id), p.Id, purpose, item, quantity), out reservation);
        var reservationId = reservation; physical = error is null ? state.Inventory.Reservations.Single(r => r.Id == reservationId).Items.Single() : 0; return error;
    }
    private string? ConsumeInventoryReservation(Person p, long reservation) =>
        RunInventory(new ConsumeInventoryAssets(InventoryNonce("consume-reservation", p.Id), p.Id, reservation), out _);
    private string? ReleaseInventoryReservation(Person p, long reservation) =>
        RunInventory(new ReleaseInventoryAssets(InventoryNonce("release-reservation", p.Id), p.Id, reservation), out _);

    private string? BeginInventoryJob(Person p, InventoryCommand request, ActionKind action, string? recipe = null)
    {
        if (p.Action != ActionKind.Idle || p.Health < 10) return "기존 행동과 건강을 확인하세요.";
        var started = InventoryEngine.Apply(CurrentInventory(), request);
        if (!started.Accepted) return started.Message;
        var begun = InventoryEngine.Apply(started.State, new BeginProduction(request.Id + ":process", p.Id, started.Subject));
        if (!begun.Accepted) return begun.Message;
        var job = begun.State.Ledger.Jobs.Single(j => j.Id == started.Subject);
        if (recipe is not null)
        {
            string? error = "정본 제작 지식 연결이 필요합니다."; var activity = 0L;
            BeginInventoryPractice(p, "recipe:" + recipe, checked((int)(job.Due - Month)), request.Id, ref activity, ref error);
            if (error is not null) return error; job.KnowledgeActivity = activity;
        }
        ApplyInventory(begun.State); p.Action = action; p.ActionTarget = job.Id; p.ActionData = "inventory:" + job.Recipe;
        p.Due = job.Due; p.ActionRevision = state.LawRevision; return null;
    }
    private bool TryInventoryCommand(GameCommand c, Person p, out string? error)
    {
        error = null;
        try
        {
            switch (c.Kind)
            {
                case "craft":
                    if (!InventoryDefinitions.Recipes.ContainsKey(c.Data)) { error = "제작 정의를 확인하세요."; return true; }
                    error = BeginInventoryJob(p, new StartProduction(c.Id, p.Id, c.Data, Month + Law("economy.craftDuration")), ActionKind.Craft, c.Data); return true;
                case "gather": case "mining":
                    var definition = c.Data.Length == 0 ? c.Kind == "mining" ? "ore" : "food" : c.Data;
                    var source = state.Inventory.Sources.FirstOrDefault(s => s.Place == p.Place && (c.Target == 0 ? s.Definition == definition : s.Id == c.Target));
                    if (source is null || (source.Definition.StartsWith("resource.", StringComparison.Ordinal) && !state.Inventory.Observations.Any(o => o.Observer == p.Id && o.Source == source.Id)))
                    { error = "실제로 발견한 현지 원천이 필요합니다."; return true; }
                    var requested = Math.Min(source.Remaining, IntLaw("economy.gatherYield"));
                    var due = Month + Math.Max(1, (requested * state.Inventory.ExtractionWorkPerUnit + 99) / 100);
                    error = BeginInventoryJob(p, new ExtractResource(c.Id, p.Id, source.Id, (int)requested, due), ActionKind.Gather); return true;
                case "farm":
                    if (!InventoryInstallationRight(p)) { error = "경작 권한이 없습니다."; return true; }
                    error = BeginInventoryJob(p, new StartProduction(c.Id, p.Id, "farm.food", Month + Law("economy.craftDuration")), ActionKind.Farm, "farm.food"); return true;
                case "repair": error = BeginInventoryJob(p, new RepairItem(c.Id, p.Id, c.Target, Month + 1), ActionKind.Heal); return true;
                case "treat":
                    var patient = c.Target == 0 ? p : people.GetValueOrDefault(c.Target);
                    var consent = patient is not null && patient.Status == LifeStatus.Alive && (patient.Id == p.Id ||
                        (patient.KnownPeople.Contains(p.Id) && patient.Personality[4] >= 40));
                    error = BeginInventoryJob(p, new TreatPerson(c.Id, p.Id, patient?.Id ?? 0, Month + 1, consent), ActionKind.Heal); return true;
                case "appraise": error = BeginInventoryJob(p, new AppraisePhysical(c.Id, p.Id, c.Target, Month + 1), ActionKind.Explore); return true;
                case "exploreSource":
                    var target = state.Inventory.Sources.FirstOrDefault(s => s.Place == p.Place && (c.Target != 0 ? s.Id == c.Target : !state.Inventory.Observations.Any(o => o.Observer == p.Id && o.Source == s.Id)));
                    error = target is null ? "탐색할 현지 원천이 없습니다." : BeginInventoryJob(p, new ExploreNaturalSource(c.Id, p.Id, target.Id, Month + 1), ActionKind.Explore); return true;
                case "offer": error = RunInventory(new OfferItem(c.Id, p.Id, c.Target, c.Value, Month + 12, c.Data == "auction"), out _); return true;
                case "bid": error = RunInventory(new PlaceBid(c.Id, p.Id, c.Target, c.Value), out _); return true;
                case "trade": error = RunInventory(new BuyItem(c.Id, p.Id, c.Target), out _); return true;
                case "withdraw": error = RunInventory(new WithdrawOffer(c.Id, p.Id, c.Target), out _); return true;
                case "equip": error = RunInventory(new EquipItem(c.Id, p.Id, c.Target, c.Value, c.Data == "ground"), out _); return true;
                case "unequip": error = RunInventory(new UnequipItem(c.Id, p.Id, c.Target, c.Value, c.Data == "ground"), out _); return true;
                case "store": error = RunInventory(new StoreItem(c.Id, p.Id, c.Target, c.Value, c.Data == "ground"), out _); return true;
                case "split": error = RunInventory(new SplitInventoryItem(c.Id, p.Id, c.Target, checked((int)c.Value)), out _); return true;
                case "merge": error = RunInventory(new MergeStacks(c.Id, p.Id, c.Target, c.Value), out _); return true;
                case "use":
                    error = items.TryGetValue(c.Target, out var used) && InventoryDefinitions.Definition(used.Definition).Item.Kind == ItemKind.Array
                        ? RunInventory(new InstallArray(c.Id, p.Id, c.Target, InventoryInstallationRight(p)), out _)
                        : RunInventory(new ConsumeItem(c.Id, p.Id, c.Target), out _);
                    if (error is null) ProjectInventoryPersonalEffects(p); return true;
                case "bind":
                    error = p.Realm < Realm.GoldenCore ? "금단 이상의 실제 구조가 필요합니다." : RunInventory(new BindItem(c.Id, p.Id, c.Target,
                        $"현생 {p.Id}·혼 {p.SoulId}·{p.Realm} {p.Layer}층·월 {Month}"), out _); return true;
                case "installArray": error = RunInventory(new InstallArray(c.Id, p.Id, c.Target, InventoryInstallationRight(p)), out _); return true;
                case "upkeepArray": error = BeginInventoryJob(p, new MaintainArray(c.Id, p.Id, c.Target, Month + 1), ActionKind.Craft); return true;
                case "dismantleArray": error = RunInventory(new DismantleArray(c.Id, p.Id, c.Target), out _); return true;
                case "transport": error = BeginInventoryTransport(c, p); return true;
                case "cancel":
                    var job = state.Inventory.Jobs.SingleOrDefault(j => j.Id == p.ActionTarget && j.Actor == p.Id && j.Status is "reserved" or "processing");
                    if (job is not null)
                    {
                        error = RunInventory(new CancelProduction(c.Id, p.Id, job.Id), out _);
                        if (error is null) { if (job.KnowledgeActivity != 0) CancelInventoryPractice(p, job.KnowledgeActivity, c.Id); ClearInventoryAction(p); }
                        return true;
                    }
                    var transport = state.Inventory.Contracts.SingleOrDefault(t => t.Id == p.ActionTarget && t.Carrier == p.Id && t.Kind == "transport" && t.Status == "open");
                    if (transport is null) return false;
                    error = RunInventory(new FinishTransport(c.Id, p.Id, transport.Id, false), out _); if (error is null) ClearInventoryAction(p); return true;
                default: return false;
            }
        }
        catch (Exception exception) when (exception is OverflowException or InvalidDataException or JsonException)
        { error = exception.Message; return true; }
    }
    private bool InventoryInstallationRight(Person p)
    {
        var place = state.Places[p.Place];
        return place.Owner == 0 || place.Owner == p.Id || place.Operator == p.Id ||
            (place.Sovereign != 0 && groups.TryGetValue(place.Sovereign, out var group) && !group.Dissolved && group.Leader == p.Id);
    }
    private sealed record InventoryTransportOrder(long Carrier, long Recipient, long Fee);
    private string? BeginInventoryTransport(GameCommand command, Person sender)
    {
        var order = command.Data.Length == 0 ? new InventoryTransportOrder(sender.Id, sender.Id, 0) : JsonSerializer.Deserialize<InventoryTransportOrder>(command.Data);
        if (order is null || !people.TryGetValue(order.Carrier, out var carrier) || sender.Action != ActionKind.Idle || carrier.Action != ActionKind.Idle)
            return "실제 운송 당사자와 노동을 확인하세요.";
        var destination = checked((int)command.Value); var route = !carrier.Sealed && carrier.Health >= 10 && destination is >= 0 and < 100 &&
            carrier.KnownPlaces.Contains(destination) && state.Places[carrier.Place].Neighbors.Contains(destination) && state.Places[destination].BlockedUntil <= Month;
        var consent = carrier.Id == sender.Id || (carrier.KnownPeople.Contains(sender.Id) && carrier.Personality[4] >= 50 &&
            (order.Fee > 0 || (carrier.Household != 0 && carrier.Household == sender.Household)));
        var error = RunInventory(new BeginTransport(command.Id, sender.Id, command.Target, carrier.Id, order.Recipient, destination,
            Month + Law("economy.travelDuration"), order.Fee, route, consent), out var contract);
        if (error is null) { carrier.Action = ActionKind.Guard; carrier.ActionTarget = contract; carrier.Due = Month + Law("economy.travelDuration"); carrier.ActionData = "inventory:transport"; }
        return error;
    }
    private static void ClearInventoryAction(Person p)
    { p.Action = ActionKind.Idle; p.ActionTarget = 0; p.ActionData = ""; p.Due = 0; }
    private bool CompleteInventory(Person p)
    {
        var job = state.Inventory.Jobs.SingleOrDefault(j => j.Id == p.ActionTarget && j.Actor == p.Id);
        if (job is not null)
        {
            if (job.Status is not ("reserved" or "processing")) { ClearInventoryAction(p); return true; }
            var successful = job.Recipe is "repair" or "medical-treatment" or "appraisal" or "exploration" or "array-maintenance" or "extraction" || Roll(10000) >= 500;
            var error = RunInventory(new FinishProduction(InventoryNonce("complete", p.Id), p.Id, job.Id, successful, job.KnowledgeActivity == 0), out var physical);
            if (error is not null)
            {
                RunInventory(new CancelProduction(InventoryNonce("interrupted", p.Id), p.Id, job.Id), out _);
                if (job.KnowledgeActivity != 0) CancelInventoryPractice(p, job.KnowledgeActivity, "physical-interruption");
                Remember(p, "inventory:interruption", error, "실제 공정 재검사", 100);
            }
            else
            {
                if (job.KnowledgeActivity != 0)
                {
                    string? practiceError = null;
                    FinishInventoryPractice(p, job.KnowledgeActivity, successful, $"job:{job.Id}:physical:{physical}",
                        successful ? "실제 재료 소비와 실물 결과" : "실제 소비 후 제작 실패", ref practiceError);
                    if (practiceError is not null) Remember(p, "inventory:practice", practiceError, "실제 지식 정산", 100);
                }
                if (job.Source != 0) Gain(p, "gather", job.OutputQuantity);
                if (p.Action == ActionKind.Farm && !state.Places[p.Place].Facilities.Contains("경작지")) state.Places[p.Place].Facilities.Add("경작지");
                if (job.Recipe == "exploration")
                { foreach (var id in state.Places[p.Place].Neighbors) if (!p.KnownPlaces.Contains(id)) p.KnownPlaces.Add(id); p.KnownPlaces.Sort(); Observe(p, true); }
                Event("제작", p.Id, physical, successful ? "실제 예약·노동·소비·결과 정산" : "실제 공정 실패·소비량 보존", $"inventory:job:{job.Id}");
            }
            ClearInventoryAction(p); return true;
        }
        var cargo = state.Inventory.Contracts.SingleOrDefault(c => c.Id == p.ActionTarget && c.Carrier == p.Id && c.Kind == "transport");
        if (cargo is null) return false;
        if (cargo.Status == "open")
        {
            var route = p.Place == cargo.Origin && !p.Sealed && state.Places[p.Place].Neighbors.Contains(cargo.Destination) && state.Places[cargo.Destination].BlockedUntil <= Month;
            if (route) { state.Places[p.Place].PeopleRevision++; p.Place = cargo.Destination; state.Places[p.Place].PeopleRevision++; p.LastPeopleRevision = -1; RefreshPopulationIndices(); Observe(p); }
            RunInventory(new FinishTransport(InventoryNonce("delivery", p.Id), p.Id, cargo.Id, route), out _);
        }
        ClearInventoryAction(p); return true;
    }
    private void SettleInventory()
    {
        ConfigureInventoryRegeneration();
        var error = RunInventory(new Inventory.SettleInventory(InventoryNonce("settle"), false), out _);
        if (error is not null) throw new InvalidDataException(error);
    }
    private void ProjectInventorySources()
    { foreach (var place in state.Places) place.Source = checked((int)state.Inventory.Sources.Where(s => s.Place == place.Id).Sum(s => s.Remaining)); }
    private void ProjectInventoryContracts() => state.Contracts = state.Inventory.Contracts.Select(c => new Contract { Id = c.Id,
        Kind = c.Kind, Status = c.Status, Seller = c.Seller, Buyer = c.Buyer, Carrier = c.Carrier, Item = c.Item,
        Price = c.Price, Escrow = c.Escrow, Destination = c.Destination, Due = c.Due }).ToList();
    private void ProjectInventoryPersonalEffects(Person p)
    {
        foreach (var effect in state.Inventory.Effects.Where(e => e.Target == p.Id && e.Start == Month))
        {
            if (effect.Kind == "soulProtection") { p.SoulPactUntil = Month + Law("soul.pactDuration"); p.SoulPactSource = effect.Cause; }
            if (effect.Kind == "defense" && !p.Traits.Any(t => t.Source == effect.Cause)) p.Traits.Add(new("호신부", effect.Cause, Month, effect.End, true));
        }
    }
    private (bool Operational, int Quality, int QiBonus, int DefenseBonus, IReadOnlyList<string> Causes) CultivationFormationAt(Place place, Person p)
    {
        var frames = state.Inventory.Arrays.Where(a => a.Active && a.PaidUntil > Month && a.Place == place.Id && p.Place == place.Id)
            .Select(a => (Array: a, Item: items.GetValueOrDefault(a.Item))).Where(a => a.Item is not null && a.Item.Damage < 100 &&
                (a.Item.Defects & (ItemDefect.BrokenCircuit | ItemDefect.LostCore)) == 0).DistinctBy(a => a.Item!.Id).ToArray();
        return (frames.Length > 0, frames.Length == 0 ? 0 : (int)frames.Average(f => f.Item!.Quality * f.Array.Condition / 100),
            frames.Length * 10, frames.Length * 10, frames.Select(f => "item:" + f.Item!.Id.ToString(CultureInfo.InvariantCulture)).ToArray());
    }
    private int InventoryBreakthroughBonus(Person p) => state.Inventory.Effects.Where(e => e.Target == p.Id && e.Kind == "breakthrough" &&
        e.Start <= Month && e.End > Month).DistinctBy(e => e.Cause).Sum(e => e.Magnitude);
    private void ValidateInventoryBridge()
    {
        _ = InventoryState.FromWorld(state, state.Inventory);
        if (state.Contracts.Count != state.Inventory.Contracts.Count || state.Contracts.Zip(state.Inventory.Contracts).Any(pair =>
            pair.First.Id != pair.Second.Id || pair.First.Kind != pair.Second.Kind || pair.First.Status != pair.Second.Status ||
            pair.First.Item != pair.Second.Item || pair.First.Seller != pair.Second.Seller || pair.First.Buyer != pair.Second.Buyer ||
            pair.First.Carrier != pair.Second.Carrier || pair.First.Price != pair.Second.Price || pair.First.Escrow != pair.Second.Escrow ||
            pair.First.Due != pair.Second.Due || pair.First.Destination != pair.Second.Destination))
            throw new InvalidDataException("Inventory contract projection differs from its authoritative ledger.");
    }
}
