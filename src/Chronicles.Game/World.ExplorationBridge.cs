using System.Globalization;
using Chronicles.Game.Exploration;
using Chronicles.Game.Inventory;
using Chronicles.Game.People;

namespace Chronicles.Game;

public sealed partial class GameWorld
{
    private const string ExplorationPrefix = "exploration:";
    private SecretEntryRules ExplorationRules() => new(state.LawRevision, IntLaw("exploration.openPeriod"),
        IntLaw("exploration.openMonths"), IntLaw("exploration.entryMonths"), IntLaw("exploration.capacity"),
        (Realm)IntLaw("exploration.minimumRealm"), IntLaw("exploration.foodCost"), IntLaw("exploration.toolWear"),
        IntLaw("exploration.risk"), IntLaw("exploration.injury"));

    internal void InitializeExploration()
    {
        if (state.Exploration.Initialized) return;
        if (Month != 0 || state.Exploration.Sites.Count != 0 || state.Exploration.Entries.Count != 0)
            throw new InvalidDataException("비경 원천은 실제 창세에서만 초기화합니다.");
        string[] rewards = ["herb", "ore", "wood", "ink", "resource.03.3"];
        foreach (var place in state.Places.Where(p => p.Id % 10 == 8).OrderBy(p => p.Id))
            state.Exploration.Sites.Add(new() { Place = place.Id, Name = $"{place.Province + 1}주 숨은 유적", Offset = place.Province,
                RewardDefinition = rewards[place.Province % rewards.Length], InitialStock = IntLaw("exploration.rewardQuantity"),
                Remaining = IntLaw("exploration.rewardQuantity"), OriginRevision = state.LawRevision,
                Source = $"창세 지점 {place.Id} 내부의 별도 유한 비경 원천·일반 자연 탐색 cache와 독립" });
        state.Exploration.Initialized = true;
    }

    internal static void ValidateExplorationState(GameState snapshot) => ExplorationAuthority.Validate(snapshot);

    private SecretEntry? OccupiedExploration(Person person) => state.Exploration.Entries.FirstOrDefault(e =>
        e.Person == person.Id && e.Body == person.BodyId && ExplorationAuthority.Occupies(e));

    internal bool ShareExplorationSpace(Person first, Person second)
    {
        var one = OccupiedExploration(first); var two = OccupiedExploration(second);
        return first.Place == second.Place && (one is null && two is null || one is not null && two is not null && one.Place == two.Place);
    }

    internal string? ValidateExplorationInteraction(GameCommand command, Person person)
    {
        if (OccupiedExploration(person) is not null && command.Kind is not ("returnSecret" or "cancel" or "observe" or "possess"))
            return "현재 조사 중인 비경에서 먼저 실제 귀환을 요청하세요.";
        // Domain-local request, item, contract and policy IDs are deliberately not interpreted as people.
        if (command.Kind is "attack" or "partner" or "conceive" or "adopt" or "teach" or "requestTeaching" or
            "requestAssessment" or "requestReputation" or "requestOath" or "treat" or "loan")
            if (person.KnownPeople.Contains(command.Target) && people.TryGetValue(command.Target, out var other) && !ShareExplorationSpace(person, other))
                return "실행할 수 없는 대상입니다.";
        return null;
    }

    internal string? ValidateExplorationAction(Person person)
    {
        if (!person.ActionData.StartsWith(ExplorationPrefix, StringComparison.Ordinal)) return "비경 작업 표식이 없습니다.";
        var e = OccupiedExploration(person);
        if (e is null || person.Action != ActionKind.Explore || person.ActionTarget != e.Id || e.Soul != person.SoulId ||
            e.Place != person.Place || person.ActionData != ExplorationPrefix + e.Id.ToString(CultureInfo.InvariantCulture) ||
            e.Due != person.Due || e.Due <= Month)
            return "실제 몸·입장·진척·시각과 일치하지 않는 비경 작업입니다.";
        return null;
    }

    internal void DiscoverExplorationEntrance(Person person)
    {
        var site = state.Exploration.Sites.FirstOrDefault(s => s.Place == person.Place);
        if (site is null) return;
        ObserveEntrance(person, site, false, $"실제 유료 현지 탐색·몸 {person.BodyId}·월 {Month}");
        Event("비경 발견", person.Id, 0, $"{site.Name} 입구와 개방 문양을 현지 탐색으로 확인했다. 내부 보상은 미상이다.", $"exploration:survey:{person.BodyId}:{Month}:{site.Place}");
    }

    private void ObserveEntrance(Person person, SecretSite site, bool inside, string source)
    {
        var rules = ExplorationRules();
        state.Exploration.Observations.RemoveAll(o => o.Person == person.Id && o.Place == site.Place);
        state.Exploration.Observations.Add(new(person.Id, person.BodyId, site.Place, Month, rules.Period, rules.OpenMonths,
            site.Offset, rules.MinimumRealm, rules.Capacity, rules.Food, state.Exploration.Entries.Count(e => e.Place == site.Place && ExplorationAuthority.Occupies(e)),
            inside ? site.RewardDefinition : null, inside ? site.Remaining : null, source));
    }

    internal bool TryExplorationCommand(GameCommand command, Person person, out string? error)
    {
        error = null;
        if (command.Kind == "returnSecret")
        {
            if (OccupiedExploration(person) is null) error = "현재 참여한 비경 입장이 없습니다.";
            else CancelExploration(person);
            return true;
        }
        if (command.Kind != "enterSecret") return false;
        var rules = ExplorationRules(); var site = state.Exploration.Sites.FirstOrDefault(s => s.Place == command.Target);
        var known = state.Exploration.Observations.Any(o => o.Person == person.Id && o.Body == person.BodyId && o.Place == command.Target);
        if (person.Action != ActionKind.Idle || person.Health < 10 || person.Sealed || OccupiedExploration(person) is not null ||
            site is null || !known || person.Place != command.Target || !PoliticsLaborAvailable(person, Month, Month + rules.Months))
        { error = "직접 발견한 현지 입구·현재 건강·빈 행동 시간을 확인하세요."; return true; }
        if (PeopleActionError(person, "travel") is { } mobility) { error = mobility; return true; }
        if (PeopleActionError(person, "craft") is { } tools) { error = tools; return true; }
        if (state.Pregnancies.Any(p => p.Mother == person.Id && !p.Completed && p.Due <= Month + rules.Months))
        { error = "예정된 실제 출생 전에 입장·귀환을 마칠 수 있어야 합니다."; return true; }
        if (state.Inventory.Contracts.Any(c => c.Status == "open" && (c.Seller == person.Id || c.Buyer == person.Id || c.Carrier == person.Id)))
        { error = "진행 중인 실물 출품·거래·운송 계약을 먼저 정산하세요."; return true; }
        if (!ExplorationAuthority.Open(Month, site.Offset, rules.Period, rules.OpenMonths) || person.Realm < rules.MinimumRealm ||
            state.Exploration.Entries.Count(e => e.Place == site.Place && ExplorationAuthority.Occupies(e)) >= rules.Capacity)
        { error = "현장에서 입구의 현재 개방·자격·수용 조건을 충족하지 못했습니다."; return true; }
        var tool = Owned(person.Id).Where(i => i.Definition == "tool" && i.Damage < 100 && i.Defects == ItemDefect.None &&
            InventoryAccessible(person, i.Id, out _)).OrderBy(i => i.Id).FirstOrDefault();
        if (tool is null || AvailableInventory(person, "food") < rules.Food)
        { error = "사용 가능한 실제 조사 도구와 준비 식량이 필요합니다."; return true; }
        // Compose all physical changes on one isolated inventory candidate, including AI's direct shared dispatch.
        var id = state.Exploration.NextId; var source = ExplorationPrefix + id.ToString(CultureInfo.InvariantCulture);
        var reservedTool = InventoryEngine.Apply(CurrentInventory(), new ReserveInventoryPhysical(source + ":tool", person.Id, "비경 조사 도구", tool.Id, 1));
        if (!reservedTool.Accepted) { error = reservedTool.Message; return true; }
        var toolReservation = reservedTool.State.Ledger.Reservations.Single(r => r.Id == reservedTool.Subject);
        var physical = toolReservation.Items.Single();
        var reservedFood = InventoryEngine.Apply(reservedTool.State, new ReserveInventoryMaterials(source + ":food", person.Id, "비경 준비 식량", new Dictionary<string, int> { ["food"] = rules.Food }));
        if (!reservedFood.Accepted) { error = reservedFood.Message; return true; }
        var firstSequence = reservedFood.State.Ledger.NextEventSequence;
        var consumed = InventoryEngine.Apply(reservedFood.State, new ConsumeInventoryAssets(source + ":paid", person.Id, reservedFood.Subject));
        if (!consumed.Accepted) { error = consumed.Message; return true; }
        var interpretation = KnowledgeFor(person, "entry");
        var understanding = interpretation?.Comprehension ?? 0;
        var risk = Math.Clamp(rules.Risk - understanding * 20 - tool.Quality / 10 +
            PeopleFortuneModifier(person, FortuneEventKind.Adversity) - PeopleFortuneModifier(person, FortuneEventKind.Escape), 0, 10000);
        var entry = new SecretEntry { Id = id, Person = person.Id, Body = person.BodyId, Soul = person.SoulId, Place = site.Place,
            Started = Month, Due = Month + 1, Status = SecretEntryStatus.Active, Rules = rules, Tool = physical,
            ToolReservation = toolReservation.Id, FoodReservation = reservedFood.Subject, FoodConsumptionSequence = firstSequence,
            FoodFinishedSequence = consumed.State.Ledger.NextEventSequence - 1, ToolQuality = tool.Quality,
            KnowledgeSource = interpretation is null ? "실제 개인 입문 해독 지식 없음" : KnowledgeSource(person, "entry"),
            Understanding = understanding, Risk = risk, Source = source };
        ApplyInventory(consumed.State);
        entry.Roll = Roll(10000); state.Exploration.NextId = checked(id + 1); state.Exploration.Entries.Add(entry);
        person.Action = ActionKind.Explore; person.ActionTarget = id; person.ActionData = source;
        person.ActionRevision = rules.Revision; person.Due = entry.Due;
        Event("비경 입장", person.Id, 0, $"{site.Name}에 입장했다. 실제 준비 식량 {rules.Food}개와 조사 도구를 사용한다.", source);
        return true;
    }

    internal bool CompleteExploration(Person person)
    {
        if (!person.ActionData.StartsWith(ExplorationPrefix, StringComparison.Ordinal)) return false;
        var entry = OccupiedExploration(person) ?? throw new InvalidDataException("비경 작업의 실제 입장이 없습니다.");
        if (entry.Due > Month) return true;
        var site = state.Exploration.Sites.Single(s => s.Place == entry.Place);
        if (entry.Body != person.BodyId || entry.Soul != person.SoulId || person.Place != entry.Place ||
            !items.TryGetValue(entry.Tool, out var tool) || tool.Reserved != entry.ToolReservation || tool.Damage >= 100 || tool.Defects != ItemDefect.None)
        { EndExploration(person, entry, SecretEntryStatus.Aborted, "실제 몸·위치·조사 도구를 상실했다."); return true; }
        if (entry.Status == SecretEntryStatus.Returning)
        { EndExploration(person, entry, SecretEntryStatus.Returned, "준비 식량과 이미 들인 시간을 되돌리지 않고 실제 귀환했다."); return true; }
        entry.Progress++;
        tool.Damage = Math.Min(100, tool.Damage + entry.Rules.ToolWear);
        if (!entry.RiskApplied)
        {
            entry.RiskApplied = true;
            if (entry.Roll < entry.Risk)
            {
                entry.Injury = Math.Min(person.Health, entry.Rules.Injury); person.Health -= entry.Injury;
                ObserveEntrance(person, site, true, entry.Source + ":실제 내부 위험·몸의 피해");
                EndExploration(person, entry, SecretEntryStatus.Failed, "고정된 한 번의 실제 위험 결과로 조사에 실패했다.");
                if (person.Health == 0) EndLife(person, 3, "비경의 실제 환경 위험으로 육신 붕괴");
                return true;
            }
        }
        ObserveEntrance(person, site, true, entry.Source + ":실제 내부 조사 진척 " + entry.Progress.ToString(CultureInfo.InvariantCulture));
        if (entry.Progress >= entry.Rules.Months)
        {
            if (site.Remaining > 0)
            {
                var amount = site.Remaining;
                var reward = AddItem(person.Id, site.RewardDefinition, amount, 5000, $"{entry.Source}·{site.Source}·유한 선점", "extraction")!;
                site.Remaining -= amount; entry.RewardQuantity = amount; entry.RewardItem = reward.Id;
            }
            ObserveEntrance(person, site, true, entry.Source + ":실제 최종 선점 결과");
            EndExploration(person, entry, SecretEntryStatus.Completed, entry.RewardQuantity > 0 ? "실제 남은 원천을 먼저 선점하여 귀환했다." : "다른 실제 입장자가 먼저 선점하여 보상이 소진되었다.");
        }
        else if (tool.Damage >= 100) EndExploration(person, entry, SecretEntryStatus.Failed, "조사 도구의 실제 내구가 소진되었다.");
        else
        {
            entry.Due = Month + 1; person.Due = entry.Due;
            Event("비경 조사", person.Id, 0, $"{site.Name} 조사 {entry.Progress}/{entry.Rules.Months}개월·같은 몸의 다음 달 작업을 계속 점유한다.", entry.Source);
        }
        NormalizeInventoryPhysicalState();
        return true;
    }

    internal bool CancelExploration(Person person)
    {
        var entry = OccupiedExploration(person);
        if (entry is null) return false;
        if (entry.Status == SecretEntryStatus.Returning) return true;
        entry.Status = SecretEntryStatus.Returning; entry.Due = Month + 1; person.Due = entry.Due;
        Event("비경 귀환 요청", person.Id, 0, "실제 입장 자리와 도구를 유지한 채 한 달 귀환 작업을 시작했다.", entry.Source);
        return true;
    }

    internal void ReleaseExplorationForLifeEnd(Person person)
    {
        if (OccupiedExploration(person) is { } entry) EndExploration(person, entry, SecretEntryStatus.Aborted, "현생 종결로 실제 입장과 도구 예약을 해제했다.");
    }

    private void EndExploration(Person person, SecretEntry entry, SecretEntryStatus status, string reason)
    {
        if (!ExplorationAuthority.Occupies(entry)) return;
        if (state.Inventory.Reservations.Any(r => r.Id == entry.ToolReservation && r.Active))
        {
            var released = InventoryEngine.Apply(CurrentInventory(), new ReleaseInventoryAssets(entry.Source + ":release", person.Id, entry.ToolReservation));
            if (!released.Accepted) throw new InvalidDataException(released.Message);
            ApplyInventory(released.State);
        }
        entry.Status = status; entry.Ended = Month; entry.EndReason = reason;
        if (person.ActionData == entry.Source)
        {
            person.Action = ActionKind.Idle; person.ActionTarget = 0; person.ActionData = ""; person.Due = 0;
            foreach (var row in state.Inventory.Labor.Where(r => r.Person == person.Id && r.Purpose == "world-action")) row.Active = false;
        }
        Event("비경 결과", person.Id, 0, reason, entry.Source);
    }

    internal void SettleExploration()
    {
        foreach (var entry in state.Exploration.Entries.Where(ExplorationAuthority.Occupies).ToArray())
            if (people.TryGetValue(entry.Person, out var person) && (person.Status != LifeStatus.Alive || person.BodyId != entry.Body ||
                person.SoulId != entry.Soul || person.Place != entry.Place || !items.TryGetValue(entry.Tool, out var tool) || tool.Reserved != entry.ToolReservation))
                EndExploration(person, entry, SecretEntryStatus.Aborted, "실제 몸·위치·도구의 상실을 정산했다.");
    }

    public ExplorationView ExplorationDetail()
    {
        var observer = state.Controlled == 0; var rules = ExplorationRules();
        var actor = observer ? null : people[state.Controlled];
        var entrances = observer
            ? state.Exploration.Sites.Select(site => new EntranceView(site.Place, site.Name, Month, rules.Period, rules.OpenMonths, site.Offset,
                Catalog.RealmNames[(int)rules.MinimumRealm], rules.Capacity, rules.Food, state.Exploration.Entries.Count(e => e.Place == site.Place && ExplorationAuthority.Occupies(e)),
                site.RewardDefinition, site.Remaining, ExplorationAuthority.Open(Month, site.Offset, rules.Period, rules.OpenMonths),
                state.Exploration.Entries.Where(e => e.Place == site.Place && ExplorationAuthority.Occupies(e)).Select(e => e.Person).ToArray(), site.Source)).ToArray()
            : state.Exploration.Observations.Where(o => o.Person == actor!.Id && o.Body == actor.BodyId).OrderBy(o => o.Place).Select(o =>
                new EntranceView(o.Place, $"{o.Place / 10 + 1}주 숨은 유적", o.Month, o.Period, o.OpenMonths, o.Offset,
                    Catalog.RealmNames[(int)o.MinimumRealm], o.Capacity, o.FoodCost, o.ObservedOccupancy, o.RewardDefinition, o.ObservedStock,
                    null, null, o.Source)).ToArray();
        var entries = state.Exploration.Entries.Where(e => observer || e.Person == actor!.Id).OrderByDescending(e => e.Id).Select(e =>
            new SecretProgressView(e.Id, e.Place, e.Status.ToString(), e.Progress, e.Rules.Months, e.Started, e.Due, e.Ended, e.Rules.Food, e.Tool,
                e.Injury, e.RewardItem, e.RewardQuantity, "실제 식량·도구·개인 해독 지식 사용·위험은 불확실", e.EndReason)).ToArray();
        return new(entrances, entries);
    }

    internal bool TryExplorationInformationGoal(Person person)
    {
        var observation = state.Exploration.Observations.Where(o => o.Person == person.Id && o.Body == person.BodyId &&
            o.Place == person.Place && person.Realm >= o.MinimumRealm && ExplorationAuthority.Open(Month, o.Offset, o.Period, o.OpenMonths) &&
            o.ObservedStock != 0).OrderByDescending(o => o.Month).FirstOrDefault();
        if (observation is null || KnowledgeFor(person, "entry") is not { Comprehension: > 0 } || person.Health < 60 ||
            AvailableInventory(person, "food") < observation.FoodCost || OccupiedExploration(person) is not null) return false;
        var command = new GameCommand($"ai-secret:{person.BodyId}:{Month}:{Revision}", person.Id, "enterSecret", observation.Place);
        if (!TryExplorationCommand(command, person, out var error) || error is not null) return false;
        person.Goal = "정보 탐색·알려진 유적"; person.Reason = $"자신이 실제 탐색한 입구 {observation.Place}·관측 {observation.Month}·현재 개인 준비로 조건을 재검사했다.";
        return true;
    }
}
