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
        state.Inventory.BaseCarryCapacity = checked((int)state.Laws.GetValueOrDefault("inventory.carryCapacity", 500));
        state.Inventory.ProductionWork = checked((int)state.Laws.GetValueOrDefault("inventory.productionWork", 100));
        state.Inventory.ProfessionalWork = checked((int)state.Laws.GetValueOrDefault("inventory.professionalWork", 100));
        state.Inventory.ExtractionWorkPerUnit = checked((int)state.Laws.GetValueOrDefault("inventory.extractionWork", 25));
        foreach (var place in state.Places)
        {
            var profiles = InventorySourceProfiles(place); var totalWeight = profiles.Sum(x => x.Weight);
            var total = Math.Min(place.Source, IntLaw("ecology.sourceCapacity")); var allocated = 0;
            foreach (var profile in profiles)
            {
                var capacity = IntLaw("ecology.sourceCapacity") * (long)profile.Weight / totalWeight;
                var amount = total * (long)profile.Weight / totalWeight; allocated += (int)amount;
                state.Inventory.Sources.Add(new() { Id = Id(), Place = place.Id, Definition = profile.Definition,
                    Activity = InventoryDefinitions.ExtractionActivity(profile.Definition)!, Remaining = amount, Capacity = capacity, EcologyWeight = profile.Weight,
                    LastMonth = Month, Origin = $"창세 자연 원천·지점 {place.Id}·{profile.Definition}" });
            }
            var first = state.Inventory.Sources.First(s => s.Place == place.Id);
            first.Remaining += total - allocated;
            first.Capacity += IntLaw("ecology.sourceCapacity") - state.Inventory.Sources.Where(s => s.Place == place.Id).Sum(s => s.Capacity);
        }
        MigrateInventoryContracts();
        NormalizeInventoryPhysicalState(); ConfigureInventoryRegeneration(); ProjectInventorySources(); ProjectInventoryContracts();
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
            var sources = state.Inventory.Sources.Where(s => s.Place == place.Id && s.EcologyWeight > 0).OrderBy(s => s.Id).ToArray();
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
            var contract = new InventoryContract { Id = old.Id, Kind = old.Kind, Seller = old.Seller, Buyer = old.Kind == "transport" && old.Buyer == 0 ? old.Seller : old.Buyer,
                Carrier = old.Carrier, Item = old.Item, Origin = old.Destination, Destination = old.Destination,
                Price = old.Price, Escrow = old.Escrow, Due = old.Due, Status = old.Status, Reservation = reservation };
            if (items.TryGetValue(old.Item, out var item))
            {
                contract.Origin = item.Place < 0 ? people[item.Owner].Place : item.Place;
                if (old.Status == "open")
                {
                    contract.AdvertisedDefinition = item.Definition; contract.AdvertisedName = InventoryDefinitions.Definition(item.Definition).Item.Name;
                    contract.AdvertisedQuantity = item.Quantity;
                }
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
                OutputDefinition = InventoryDefinitions.Recipes[p.ActionData].Output, OutputQuantity = InventoryDefinitions.Recipes[p.ActionData].Quantity,
                Labor = labor, Reservation = reservation, Status = "processing", Inputs = input.Select(i => i.Id).ToList(),
                Quality = (int)(input.Sum(i => (long)i.Quantity * i.Quality) / input.Sum(i => (long)i.Quantity)),
                Damage = input.Max(i => i.Damage), BoundSoul = input.Where(i => i.BoundSoul != 0).Select(i => i.BoundSoul).Distinct().SingleOrDefault(),
                Defects = input.Aggregate(ItemDefect.None, (flags, i) => flags | i.Defects), Provenance = input.Select(i => i.Source).ToList() };
            foreach (var item in input) { job.Consumed.Add(new(item.Definition, item.Quantity)); Material(item.Definition).Consumption += item.Quantity; Remove(item); }
            state.Inventory.Jobs.Add(job); p.ActionTarget = jobId; p.ActionData = "inventory:" + job.Recipe;
        }
        Reindex();
    }
    private InventoryState CurrentInventory()
    {
        var inventory = InventoryState.FromWorld(state, state.Inventory);
        var carry = checked((int)state.Laws.GetValueOrDefault("inventory.carryCapacity", 500));
        var production = checked((int)state.Laws.GetValueOrDefault("inventory.productionWork", 100));
        var professional = checked((int)state.Laws.GetValueOrDefault("inventory.professionalWork", 100));
        var extraction = checked((int)state.Laws.GetValueOrDefault("inventory.extractionWork", 25));
        if (inventory.Ledger.BaseCarryCapacity != carry || inventory.Ledger.ProductionWork != production ||
            inventory.Ledger.ProfessionalWork != professional || inventory.Ledger.ExtractionWorkPerUnit != extraction)
        {
            var tuned = InventoryEngine.Apply(inventory, new TuneInventoryProfile(InventoryNonce("laws"), carry, production, professional, extraction));
            if (!tuned.Accepted) throw new InvalidDataException(tuned.Message); inventory = tuned.State;
        }
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
    private bool InventoryAccessible(Person p, long id, out Item item, ItemKind? kind = null)
    {
        var view = new InventoryState { Month = Month, NextId = state.NextId, Revision = Revision,
            Items = state.Items, People = state.People, Materials = state.Materials, Ledger = state.Inventory };
        if (items.TryGetValue(id, out var found) && (kind is null || InventoryDefinitions.Definition(found.Definition).Item.Kind == kind) &&
            InventoryEngine.IsAccessible(view, p.Id, id)) { item = found; return true; }
        item = null!; return false;
    }
    private void RecordInventoryLoss(Item item, int quantity)
    {
        if (quantity <= 0) return;
        var view = new InventoryState { Items = state.Items, People = state.People, Ledger = state.Inventory };
        var place = InventoryEngine.Location(view, item.Id);
        var observed = InventoryEngine.IsAccessible(view, item.Owner, item.Id, requireUncommitted: false);
        state.Inventory.Events.Add(new(Month, "physical-loss", 0, item.Id, "Actual physical loss before decrement.",
            checked(state.Inventory.NextEventSequence++), item.Definition, quantity, place, item.Owner, item.Source, true,
            item.Quality, item.Damage, item.Defects, observed));
    }
    private int AvailableInventory(Person p, string definition) => Owned(p.Id).Where(i => i.Definition == definition &&
        InventoryAccessible(p, i.Id, out _)).Sum(i => i.Quantity);
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
                    if (source is null || (source.Definition.StartsWith("resource.", StringComparison.Ordinal) && !state.Inventory.Observations.Any(o => o.Observer == p.Id && o.Source == source.Id && o.Definition == source.Definition && o.Activity == source.Activity)))
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
                    var target = state.Inventory.Sources.FirstOrDefault(s => s.Place == p.Place && (c.Target != 0 ? s.Id == c.Target : !state.Inventory.Observations.Any(o => o.Observer == p.Id && o.Source == s.Id && o.Definition.Length > 0 && o.Activity.Length > 0)));
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
        if (p.Due > Month) return false;
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
            var route = items.TryGetValue(cargo.Item, out var physicalCargo) && physicalCargo.Place == -1 && p.Place == cargo.Origin && !p.Sealed && state.Places[p.Place].Neighbors.Contains(cargo.Destination) && state.Places[cargo.Destination].BlockedUntil <= Month;
            if (route && p.Realm >= Realm.Mahayana && p.Place / 10 != cargo.Destination / 10 && Math.Max(p.Place, cargo.Destination) >= 90 &&
                Roll(10000) < Law("hazard.portalChance"))
            { EndLife(p, 10, "실제 운송 통로 붕괴·육신 유지 불가"); return true; }
            if (route) { state.Places[p.Place].PeopleRevision++; p.Place = cargo.Destination; state.Places[p.Place].PeopleRevision++; p.LastPeopleRevision = -1; RefreshPopulationIndices(); Observe(p); }
            var error = RunInventory(new FinishTransport(InventoryNonce("delivery", p.Id), p.Id, cargo.Id, route), out _);
            if (error is not null) RunInventory(new FinishTransport(InventoryNonce("delivery-failed", p.Id), p.Id, cargo.Id, false), out _);
        }
        ClearInventoryAction(p); return true;
    }
    private void SettleInventory()
    {
        var externalLimits = state.Places.Where(place => place.Source < state.Inventory.Sources.Where(s => s.Place == place.Id).Sum(s => s.Remaining))
            .ToDictionary(place => place.Id, place => (long)place.Source);
        ConfigureInventoryRegeneration();
        var error = RunInventory(new Inventory.SettleInventory(InventoryNonce("settle"), false), out _);
        if (error is not null) throw new InvalidDataException(error);
        ReconcileInventorySources(externalLimits); NormalizeInventoryPhysicalState(); ProjectInventorySources();
    }
    private void ReconcileInventorySources(IReadOnlyDictionary<int, long> externalLimits)
    {
        foreach (var place in state.Places)
        {
            var rows = state.Inventory.Sources.Where(s => s.Place == place.Id).ToArray();
            var governed = rows.Where(s => s.EcologyWeight > 0).ToArray(); var weight = governed.Sum(s => s.EcologyWeight);
            var capacities = rows.ToDictionary(s => s.Id, s => s.Capacity);
            foreach (var row in governed) capacities[row.Id] = (long)IntLaw("ecology.sourceCapacity") * row.EcologyWeight / weight;
            if (governed.Length > 0) capacities[governed[0].Id] += IntLaw("ecology.sourceCapacity") - governed.Sum(s => capacities[s.Id]);
            var desired = state.Inventory.Sources.Where(s => s.Place == place.Id).ToDictionary(s => s.Id, s => Math.Min(s.Remaining, capacities[s.Id]));
            var loss = Math.Max(0, desired.Values.Sum() - externalLimits.GetValueOrDefault(place.Id, long.MaxValue));
            foreach (var id in desired.Keys.OrderDescending().ToArray()) { var take = Math.Min(loss, desired[id]); desired[id] -= take; loss -= take; }
            foreach (var job in state.Inventory.Jobs.Where(j => j.Source != 0 && desired.ContainsKey(j.Source) && j.Status is "reserved" or "processing").ToArray())
            {
                var claimed = state.Inventory.Jobs.Where(j => j.Source == job.Source && j.Status is "reserved" or "processing").Sum(j => (long)j.OutputQuantity);
                if (claimed > desired[job.Source])
                {
                    var worker = people[job.Actor];
                    var cancellation = RunInventory(new CancelProduction(InventoryNonce("source-lost", worker.Id), worker.Id, job.Id), out _);
                    if (cancellation is not null) throw new InvalidDataException(cancellation); if (worker.ActionTarget == job.Id) ClearInventoryAction(worker);
                }
            }
            foreach (var source in state.Inventory.Sources.Where(s => s.Place == place.Id))
            {
                var remaining = Math.Min(source.Remaining, desired[source.Id]);
                if (remaining < source.Remaining) state.Inventory.Events.Add(new(Month, "source-loss", 0, source.Id, NumberInventory(source.Remaining - remaining)));
                source.Remaining = remaining; source.Capacity = capacities[source.Id];
            }
        }
    }
    private static string NumberInventory(long value) => value.ToString(CultureInfo.InvariantCulture);
    private void NormalizeInventoryPhysicalState()
    {
        long RootId(Item item)
        { while (item.Container != 0) item = items[item.Container]; return item.Id; }
        int ActualPlace(Item item)
        {
            var root = items[RootId(item)]; if (root.Place >= 0) return root.Place;
            var metadata = state.Inventory.Metadata.FirstOrDefault(m => m.Item == root.Id);
            var holder = metadata?.Carrier is > 0 ? metadata.Carrier : metadata?.Custodian is > 0 ? metadata.Custodian : root.Owner;
            return people[holder].Place;
        }
        foreach (var item in state.Items)
        {
            if (item.Damage < 100 && (item.Defects & (ItemDefect.BrokenCircuit | ItemDefect.LostCore)) == 0) continue;
            foreach (var array in state.Inventory.Arrays.Where(a => a.Item == item.Id))
            {
                array.Condition = 0; array.Active = false;
                state.Inventory.Effects.RemoveAll(e => e.Cause.StartsWith($"array:{array.Id}:", StringComparison.Ordinal));
            }
            foreach (var person in state.People)
                foreach (var slot in person.Equipment.Where(e => e.Value == item.Id).Select(e => e.Key).ToArray()) person.Equipment.Remove(slot);
            foreach (var child in state.Items.Where(i => i.Container == item.Id).ToArray())
            {
                var place = ActualPlace(child); var tree = state.Items.Where(i => RootId(i) == RootId(child)).ToArray();
                child.Container = 0; child.Place = place;
                foreach (var entry in tree.Where(i => RootId(i) == child.Id)) entry.Place = place;
            }
        }
        foreach (var person in state.People)
        {
            var roots = state.Items.Where(i => i.Container == 0 && i.Place == -1 &&
                (state.Inventory.Metadata.FirstOrDefault(m => m.Item == i.Id)?.Carrier is > 0 ? state.Inventory.Metadata.First(m => m.Item == i.Id).Carrier :
                    state.Inventory.Metadata.FirstOrDefault(m => m.Item == i.Id)?.Custodian is > 0 ? state.Inventory.Metadata.First(m => m.Item == i.Id).Custodian : i.Owner) == person.Id).OrderByDescending(i => i.Id).ToArray();
            var capacity = state.Inventory.BaseCarryCapacity + person.Equipment.Values.Distinct().Sum(id => InventoryDefinitions.Definition(items[id].Definition).Item.Capacity);
            long Weight(Item root) => state.Items.Where(i => RootId(i) == root.Id).Sum(i => (long)i.Quantity * InventoryDefinitions.Definition(i.Definition).Item.Weight);
            var held = roots.Sum(Weight);
            foreach (var root in roots.Where(root => !person.Equipment.ContainsValue(root.Id)))
            {
                if (held <= capacity) break;
                foreach (var item in state.Items.Where(i => RootId(i) == root.Id)) item.Place = person.Place;
                held -= Weight(root); state.Inventory.Events.Add(new(Month, "overflow-storage", person.Id, root.Id, "실제 지점 초과분 보존"));
            }
        }
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
        var frames = state.Inventory.Arrays.Where(a => a.Active && a.PaidUntil > Month && p.Place == place.Id && InventoryArrayInRange(a.Place, place.Id, a.Range))
            .Select(a => (Array: a, Item: items.GetValueOrDefault(a.Item))).Where(a => a.Item is not null && a.Item.Damage < 100 &&
                (a.Item.Defects & (ItemDefect.BrokenCircuit | ItemDefect.LostCore)) == 0).DistinctBy(a => a.Item!.Id).ToArray();
        return (frames.Length > 0, frames.Length == 0 ? 0 : (int)frames.Average(f => f.Item!.Quality * f.Array.Condition / 100),
            frames.Length * 10, frames.Length * 10, frames.Select(f => "item:" + f.Item!.Id.ToString(CultureInfo.InvariantCulture)).ToArray());
    }
    private bool InventoryArrayInRange(int origin, int destination, int range)
    {
        var visited = new HashSet<int> { origin }; var pending = new Queue<(int Place, int Distance)>(); pending.Enqueue((origin, 0));
        while (pending.TryDequeue(out var next))
        {
            if (next.Place == destination) return true; if (next.Distance >= range) continue;
            foreach (var neighbor in state.Places[next.Place].Neighbors) if (visited.Add(neighbor)) pending.Enqueue((neighbor, next.Distance + 1));
        }
        return false;
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
