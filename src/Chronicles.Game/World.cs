using System.Globalization;

namespace Chronicles.Game;

public sealed record GameCommand(string Id, long Actor, string Kind, long Target = 0,
    long Value = 0, string Data = "", long ExpectedRevision = -1);

/// <summary>Single authority shared by the headless, desktop and Android hosts.</summary>
public sealed partial class GameWorld
{
    private GameState state;
    private Dictionary<long, Person> people = [];
    private Dictionary<long, Item> items = [];
    private Dictionary<long, Soul> souls = [];
    private Dictionary<long, Group> groups = [];
    private Dictionary<long, List<Item>> ownedItems = [];
    private Dictionary<int, Person[]> residents = [];
    private Dictionary<long, Person[]> dependents = [];

    private GameWorld(GameState value) { state = value; Reindex(); }
    public long Month => state.Month;
    public long Revision => state.Revision;
    public long Controlled => state.Controlled;
    public long ViewGeneration => state.ViewGeneration;
    public GameState Checkpoint() => GameCodec.Clone(state);
    public string StateHash() => GameCodec.Hash(state);
    public static GameWorld Restore(GameState value)
    {
        var copy = GameCodec.Clone(value);
        Invariants.Validate(copy);
        return new(copy);
    }

    public static GameWorld Create(ulong seed = 42, IReadOnlyDictionary<string, long>? overrides = null)
    {
        var s = new GameState { Seed = seed, RandomState = seed ^ 0x9e3779b97f4a7c15UL };
        if (s.RandomState == 0) s.RandomState = 1;
        if (overrides is not null)
            foreach (var (key, value) in overrides) s.Laws[key] = value;
        if (Laws.Validate(s.Laws) is { } error) throw new ArgumentException(error, nameof(overrides));
        var w = new GameWorld(s);
        string[] terrains = ["평야", "산지", "숲", "하천", "습지"];
        for (var i = 0; i < 100; i++)
        {
            var place = new Place { Id = i, Province = i / 10, Name = $"{i / 10 + 1}주 {i % 10 + 1}지점",
                Terrain = terrains[i % 5], Element = i % 5,
                Source = w.IntLaw("ecology.sourceCapacity"), Qi = w.IntLaw("ecology.sourceCapacity"), Turbidity = w.Roll(100) };
            if (i % 10 > 0) place.Neighbors.Add(i - 1);
            if (i % 10 < 9) place.Neighbors.Add(i + 1);
            if (i >= 10) place.Neighbors.Add(i - 10);
            if (i < 90) place.Neighbors.Add(i + 10);
            place.Neighbors.Sort();
            s.Places.Add(place);
        }
        for (var i = 0; i < 2; i++)
        {
            var p = new Person { Id = w.Id(), BodyId = w.Id(), SoulId = w.Id(),
                Name = i == 0 ? "청명" : "연화", Gestates = i == 1, Born = -w.Law("founder.age"),
                Lifespan = w.Law("founder.lifespan"), Realm = Realm.HalfImmortal, Layer = 5, GreatPerfection = false,
                Money = w.Law("founder.initialMoney"), KnownPlaces = [0],
                Roots = Enumerable.Range(0, 5).Select(_ => 4000 + w.Roll(2001)).ToArray(),
                Aptitudes = Enumerable.Range(0, 6).Select(_ => 4000 + w.Roll(2001)).ToArray(),
                Personality = Enumerable.Range(0, 14).Select(_ => 20 + w.Roll(61)).ToArray() };
            p.Understanding["entry"] = 10000;
            foreach (var r in Catalog.Recipes) p.Understanding[r.Skill] = 1500;
            foreach (var path in Catalog.Paths)
            {
                p.Understanding[path.Id] = 3000;
            }
            w.BindSlot(p); s.People.Add(p);
            s.Souls.Add(new Soul { Id = p.SoulId, Root = p.SoulId, CurrentPerson = p.Id,
                SourceRealm = Realm.HalfImmortal, SourceLayer = 5, Evidence = "창세 조건" });
            s.CreatedMoney += p.Money;
            w.AddItem(p.Id, "food", w.IntLaw("founder.initialFood"), 5000, "창세 초기 자산", "opening");
            w.AddItem(p.Id, "tool", 1, 5000, "창세 초기 자산", "opening");
            foreach (var path in Catalog.Paths) w.AddItem(p.Id, path.Id, 1, 5000, "창세 초기 지식 매체", "opening");
        }
        w.Reindex();
        s.People[0].Partner = s.People[1].Id; s.People[1].Partner = s.People[0].Id;
        var home = new Group { Id = w.Id(), Kind = GroupKind.Family, Name = "시조 가계", Leader = s.People[0].Id,
            Members = s.People.Select(p => p.Id).ToList() };
        s.Groups.Add(home);
        foreach (var p in s.People)
        {
            p.Household = home.Id;
            p.KnownPeople = s.People.Select(x => x.Id).ToList();
            w.Observe(p);
        }
        w.Reindex();
        w.InitializePeopleDomain();
        w.InitializeInventory();
        w.InitializeKnowledge();
        w.InitializeCultivation();
        w.InitializeCurrency();
        w.InitializeEcology();
        w.InitializeExploration();
        s.DomainSchema = 2;
        w.Event("창세", 0, 0, "두 반선 시조가 세계에 들어왔다. 종문·숨은 주민 없음.", "초기 프로필");
        w.Census();
        Invariants.Validate(s);
        return w;
    }

    private void Reindex()
    {
        people = state.People.ToDictionary(p => p.Id);
        items = state.Items.ToDictionary(p => p.Id);
        souls = state.Souls.ToDictionary(p => p.Id);
        groups = state.Groups.ToDictionary(p => p.Id);
        ownedItems = state.Items.GroupBy(i => i.Owner).ToDictionary(g => g.Key, g => g.ToList());
        RefreshPopulationIndices();
    }
    private void RefreshPopulationIndices()
    {
        residents = state.People.Where(p => p.Status == LifeStatus.Alive).GroupBy(p => p.Place).ToDictionary(g => g.Key, g => g.ToArray());
        dependents = state.People.Where(p => p.Status == LifeStatus.Alive && p.Guardian != 0).GroupBy(p => p.Guardian).ToDictionary(g => g.Key, g => g.ToArray());
    }
    private long Id() => state.NextId++;
    private long Law(string key) => state.Laws[key];
    private int IntLaw(string key) => checked((int)Law(key));
    private int Roll(int maximum)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximum);
        var x = state.RandomState;
        x ^= x >> 12; x ^= x << 25; x ^= x >> 27;
        state.RandomState = x;
        return (int)((x * 2685821657736338717UL) % (uint)maximum);
    }
    private void Event(string kind, long actor, long target, string message, string source)
    {
        var targetKind = kind switch
        {
            "출생" or "도려" or "거래" or "전투" or "입양" or "제안 거절" or "배움 요청" or "돌봄" or "전수" or
                "개인 요청" or "인물 평가" or "진술 전달" or "서약 성립" or "서약 파기" => "person",
            "재해" or "발견" or "고대 금제" => "place",
            "소비" or "제작" => "item",
            "사망" or "세계 이탈" or "무예" => "condition",
            _ => "group"
        };
        var entry = new GameEvent(Id(), Month, kind, actor, target, message, source, targetKind);
        RecordSocialEvent(entry);
        if (kind is not "배움 요청" and not "혼약 만료" and not "제안 거절" and not "소비" and not "출품" and not "제작")
            state.Events.Add(entry);
        void Append(Person p)
        {
            p.Journal.Add(entry);
            if (p.Journal.Count > 100) p.Journal.RemoveAt(0);
        }
        if (people.TryGetValue(actor, out var active)) Append(active);
        if (targetKind == "person" && target != actor && people.TryGetValue(target, out var recipient)) Append(recipient);
        if (targetKind == "place" && residents.TryGetValue((int)target, out var local))
            foreach (var p in local.Where(p => p.Id != actor && p.Status == LifeStatus.Alive)) Append(p);
        RecordPeopleSocialEvent(entry);
    }

    // Public commands execute on a candidate. Rejection cannot leak partial reservations, RNG or IDs.
    public CommandResult Execute(GameCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.Id) || command.Id.Length > 128)
            return new(command.Id, false, "명령 식별자가 필요합니다.", CommandContextRevision);
        if (state.Controlled != 0 && command.Kind is not ("observe" or "possess") && command.Actor != state.Controlled)
            return new(command.Id, false, "실행할 수 없는 대상입니다.", CommandContextRevision);
        if (state.Controlled != 0 && command.Kind == "possess" && command.Actor != state.Controlled &&
            !people[state.Controlled].KnownPeople.Contains(command.Actor))
            return new(command.Id, false, "조작할 수 없는 대상입니다.", CommandContextRevision);
        var signature = GameCodec.CommandHash(command);
        if (state.Commands.TryGetValue(command.Id, out var old))
            return state.CommandSignatures[command.Id] == signature ? ProjectCommandResult(old) :
                new(command.Id, false, "같은 명령 ID의 내용이 다릅니다.", CommandContextRevision);
        if (command.ExpectedRevision >= 0 && command.ExpectedRevision != CommandContextRevision)
            return new(command.Id, false, "상태가 바뀌었습니다. 다시 확인하세요.", CommandContextRevision);
        var candidate = new GameWorld(GameCodec.Clone(state));
        candidate.scalarExecutionVerification = scalarExecutionVerification;
        candidate.inventoryProjection = inventoryProjection;
        var error = candidate.Apply(command);
        if (error is not null) return new(command.Id, false, error, CommandContextRevision);
        candidate.SettleKnowledge();
        candidate.state.Revision++;
        var result = new CommandResult(command.Id, true, "명령을 적용했습니다.", candidate.Revision);
        candidate.state.Commands.Add(command.Id, result);
        candidate.state.CommandSignatures.Add(command.Id, signature);
        Invariants.Validate(candidate.state);
        state = candidate.state; Reindex();
        return ProjectCommandResult(result);
    }

    private string? Apply(GameCommand c)
    {
        if (c.Kind == "observe")
        {
            state.Controlled = 0; state.ViewGeneration++; return null;
        }
        if (c.Kind == "possess")
        {
            if (state.Controlled != 0 && c.Actor != state.Controlled && !people[state.Controlled].KnownPeople.Contains(c.Actor))
                return "조작할 수 없는 대상입니다.";
            if (!people.TryGetValue(c.Actor, out var person) || person.Status != LifeStatus.Alive)
                return "조작할 수 없는 대상입니다.";
            state.Controlled = person.Id; state.ViewGeneration++;
            return null;
        }
        if (c.Kind == "law")
        {
            return ChangeLaws(new Dictionary<string, long>(StringComparer.Ordinal) { [c.Data] = c.Value }, c.Id);
        }
        if (c.Kind == "lawBatch") return ImportLaws(c);
        if (c.Kind == "create") return CreateExplicit(c);
        if (!people.TryGetValue(c.Actor, out var p) || p.Status != LifeStatus.Alive ||
            (state.Controlled != 0 && state.Controlled != p.Id)) return "실행할 수 없는 대상입니다.";
        if (ValidateExplorationInteraction(c, p) is { } spaceError) return spaceError;
        if (PeopleActionError(p, c.Kind) is { } capabilityError) return capabilityError;
        if (TryPeopleSocialCommand(c, p, out var socialError)) return socialError;
        if (c.Kind == "cancel") return Cancel(p);
        if (TryExplorationCommand(c, p, out var domainError) || TryCultivationCommand(c, p, out domainError) || TryKnowledgeCommand(c, p, out domainError) ||
            TryInventoryCommand(c, p, out domainError) || TryPoliticsCommand(c, p, out domainError) ||
            TryCurrencyCommand(c, p, out domainError)) return domainError;
        if (c.Kind == "partner") return Partner(p, c.Target);
        if (c.Kind == "conceive") return Conceive(p, c.Target);
        if (c.Kind == "adopt") return Adopt(p, c.Target);
        if (c.Kind == "attack") return Attack(p, c.Target);
        if (c.Kind == "ascend") return Ascend(p);
        return Start(p, c.Kind, c.Target, c.Data);
    }

    private string? Start(Person p, string kind, long target, string data)
    {
        if (PeopleActionError(p, kind) is { } capabilityError) return capabilityError;
        var command = new GameCommand($"ai:{p.BodyId}:{Month}:{Revision}:{kind}:{state.NextId}", p.Id, kind, target, Data: data);
        if (ValidateExplorationInteraction(command, p) is { } spaceError) return spaceError;
        if (TryExplorationCommand(command, p, out var domainError) || TryCultivationCommand(command, p, out domainError) || TryStartKnowledge(p, kind, target, data, out domainError) ||
            TryInventoryCommand(command, p, out domainError)) return domainError;
        if (p.Action != ActionKind.Idle) return "기존 작업이 진행 중입니다.";
        if (!PoliticsLaborAvailable(p, Month, Month + 1)) return "이미 다른 공동 작업의 인력으로 예약되어 있습니다.";
        if (p.Sealed && actionNameRequiresMovement(kind)) return "현재 봉인되어 이동할 수 없습니다.";
        if (p.Health < 10 && kind != "heal") return "먼저 회복이 필요합니다.";
        // Domain-owned work must enter its typed adapter; enum aliases/numeric strings cannot
        // enter an old completion path without a real reservation, cultivation attempt or lesson.
        var action = kind switch
        {
            "travel" => ActionKind.Travel, "explore" => ActionKind.Explore, "heal" => ActionKind.Heal,
            "guard" => ActionKind.Guard, "wait" => ActionKind.Wait, "care" => ActionKind.Care,
            _ => ActionKind.Idle
        };
        if (action == ActionKind.Idle) return "알 수 없는 행동입니다.";
        if (action == ActionKind.Guard && ValidateCombatGuard(p, target) is { } guardError) return guardError;
        if (action == ActionKind.Travel)
        {
            if (target < 0 || target >= 100 || !p.KnownPlaces.Contains((int)target) ||
                !state.Places[p.Place].Neighbors.Contains((int)target) || state.Places[(int)target].BlockedUntil > Month)
                return "이동 가능한 경로가 아닙니다.";
        }
        if (action == ActionKind.Care && ValidateCare(p, target) is { } careError) return careError;
        var previous = (p.Action, p.ActionTarget, p.ActionData, p.ActionRevision, p.Due);
        p.Action = action; p.ActionTarget = target; p.ActionData = data;
        p.ActionRevision = state.LawRevision;
        p.Due = Month + (action == ActionKind.Travel ? Law("economy.travelDuration") : 1);
        try { ValidateActionOwner(p); }
        catch (InvalidDataException)
        {
            (p.Action, p.ActionTarget, p.ActionData, p.ActionRevision, p.Due) = previous;
            return "실제로 시작한 작업의 출처와 예약을 확인하세요.";
        }
        return null;
    }

    private static bool actionNameRequiresMovement(string kind) => kind.Equals("travel", StringComparison.OrdinalIgnoreCase);

    private string? Cancel(Person p)
    {
        var command = new GameCommand($"cancel:{p.BodyId}:{Month}:{Revision}:{state.NextId}", p.Id, "cancel");
        if (CancelExploration(p)) return null;
        if (TryCancelCombat(p, out var combatError)) return combatError;
        if (TryPeopleSocialCommand(command, p, out var socialError)) return socialError;
        if (TryCultivationCommand(command, p, out var domainError)) return domainError;
        if (CancelKnowledge(p)) return null;
        if (TryInventoryCommand(command, p, out domainError) || TryCancelPoliticsWork(p, out domainError)) return domainError;
        if (p.Action == ActionKind.Idle) return "취소할 작업이 없습니다.";
        if (p.Irreversible) { EndLife(p, 3, "파단 후 형성 중단"); return null; }
        foreach (var i in state.Items.Where(i => i.Reserved == p.ActionTarget && p.Action == ActionKind.Craft)) i.Reserved = 0;
        p.Action = ActionKind.Idle; p.ActionTarget = 0; p.ActionData = ""; p.Due = 0;
        return null;
    }

    public void AdvanceTo(long targetMonth)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(targetMonth, Month);
        while (Month < targetMonth) Step();
    }

    private void Step()
    {
        state.Month++;
        SettleExploration();
        Regenerate();
        SettleCultivation();
        foreach (var pregnancy in state.Pregnancies.Where(x => !x.Completed && x.Due <= Month).ToArray()) Birth(pregnancy);
        SettleInventory();
        SettlePolitics();
        SettleKnowledge();
        RefreshPopulationIndices();
        using var living = BeginExecutionMonth();
        for (var index = 0; index < living.Count; index++)
        {
            if (!TryExecutionPerson(living.At(index), out var p)) continue;
            if (Month - p.Born >= p.Lifespan) { EndLife(p, 1, "수명 소진"); continue; }
            Maintain(p);
            if (p.Status != LifeStatus.Alive) continue;
            ResolveWorldConditions(p);
            if (p.Status != LifeStatus.Alive) continue;
            if (ExecutionActionDue(p)) Complete(p);
            if (p.Status != LifeStatus.Alive) continue;
            Observe(p);
            if (p.Action == ActionKind.Idle && state.Controlled != p.Id) { Decide(p); RecordDecision(p); }
        }
        SettleKnowledge();
        SettlePeopleDomain();
        SettlePeopleSocial();
        SettleExploration();
        SettleEcology();
        TrimCombatHistory();
        state.Revision++;
        Census();
    }

    private void Census()
    {
        var living = state.People.Where(p => p.Status == LifeStatus.Alive).ToArray();
        state.Census.Add(new(Month, living.Length, living.Count(p => p.Realm == Realm.Mortal && p.Martial == 0),
            living.Count(p => p.Martial > 0), living.Count(p => p.Realm > Realm.Mortal),
            (int)state.BirthCount, (int)state.DeathCount,
            state.People.Sum(p => p.Money) + state.Groups.Sum(g => g.Money) + state.Contracts.Sum(c => c.Escrow) +
                state.Politics.Preparations.Sum(p => p.Escrow),
            state.Items.Where(i => i.Definition == "food").Sum(i => (long)i.Quantity)));
    }

    private string? CreateExplicit(GameCommand c)
    {
        if (state.Controlled != 0 || c.Target < 0 || c.Target >= 100)
            return "관찰 관리에서 이름과 위치를 지정하세요.";
        if (!TryParsePeopleCreation(c, out var profile, out var profileError)) return profileError;
        var p = new Person { Id = Id(), BodyId = Id(), SoulId = Id(), Name = profile.Name, Species = profile.Species, Place = (int)c.Target,
            Born = Month - profile.MaturityMonths, Gestates = profile.Gestates, Lifespan = profile.LifespanMonths,
            Aptitudes = Enumerable.Range(0, 6).Select(_ => 4000 + Roll(2001)).ToArray(),
            Roots = Enumerable.Range(0, 5).Select(_ => 4000 + Roll(2001)).ToArray(),
            Personality = Enumerable.Range(0, 14).Select(_ => 20 + Roll(61)).ToArray(), KnownPlaces = [(int)c.Target] };
        BindSlot(p); state.People.Add(p); people.Add(p.Id, p);
        state.Places[p.Place].PeopleRevision++;
        var soul = new Soul { Id = p.SoulId, Root = p.SoulId, CurrentPerson = p.Id, Evidence = "명시적 중도 편입·외부 기원" };
        state.Souls.Add(soul); souls.Add(soul.Id, soul);
        InitializePeoplePerson(p);
        InitializeCultivationPerson(p);
        Event("편입", p.Id, 0, $"{p.Name} 명시 편입·자산 지급 없음", c.Id);
        Observe(p);
        return null;
    }
}
