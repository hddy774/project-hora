using System.Globalization;
using System.Text.Json;
using Chronicles.Game.Cultivation;
using Chronicles.Game.Knowledge;

namespace Chronicles.Game.Tests.Features;

internal static class AgentCultivationFixture
{
    private static int sequence;
    internal static void Check(bool value, string message) => AgentPlanningFixture.Check(value, message);
    internal static Person Person(GameWorld world, long id) => world.Checkpoint().People.Single(p => p.Id == id);
    internal static void Accept(GameWorld world, long actor, string kind, long target = 0, long value = 0, string data = "") =>
        AgentPlanningFixture.Accept(world, "agent-cultivation:" + (++sequence).ToString(CultureInfo.InvariantCulture), kind, actor, target, value, data);

    // Short durations and isolated other actors are explicit functional setup, never the default growth preset.
    // The new adult starts through the public command without knowledge, work, money or skill grants.
    internal static (GameWorld World, long Pupil, long Teacher) Create(bool hiddenTeacherFacts = false)
    {
        var world = GameWorld.Create(42, new Dictionary<string, long>
        {
            ["birth.enabled"] = 0, ["ecology.foodUse"] = 0, ["ecology.disasterChance"] = 0,
            ["knowledge.misreadChance"] = 0, ["cultivation.entry"] = 1, ["cultivation.qi"] = 2,
            ["cultivation.foundation"] = 2, ["cultivation.breakthroughRisk"] = 0
        });
        Accept(world, 0, "create", data: "입문 기능 도제");
        var state = world.Checkpoint(); var pupil = state.People[^1]; var teacher = state.People[0];
        Check(pupil.Understanding.Count == 0 && state.Knowledge.Memories.All(m => m.PersonId != pupil.Id), "Public arrival inherited knowledge.");
        foreach (var p in state.People.Take(2)) { p.Action = ActionKind.Guard; p.Due = 10000; }
        teacher.Personality[4] = 100;
        if (hiddenTeacherFacts)
        {
            var food = state.Items.First(i => i.Owner == teacher.Id && i.Definition == "food");
            food.Quantity += 7; state.Materials["food"].Opening += 7;
            var authority = KnowledgeAuthority.Restore(state.Knowledge);
            var edition = state.Knowledge.Editions.First(e => e.WorkId == "path.1.0").Id;
            Check(authority.SeedInitialKnowledge(teacher.Id, edition, [2], 60, "fixture:hidden-entry-comparison").Applied,
                "Explicit month-zero hidden comparison knowledge failed.");
            state.Knowledge = authority.Checkpoint();
        }
        pupil.Personality = new int[14]; foreach (var axis in new[] { 1, 8, 11, 13 }) pupil.Personality[axis] = 100;
        AgentPlanningFixture.Stock(state, pupil.Id, "food", 100); // Declared finite test stock; no economy or growth claim.
        world = GameWorld.Restore(state); Accept(world, pupil.Id, "possess"); world.AdvanceTo(1);
        Accept(world, pupil.Id, "observe"); Accept(world, teacher.Id, "cancel");
        Accept(world, teacher.Id, "teach", pupil.Id, 10, "entry"); Accept(world, pupil.Id, "possess");
        world.AdvanceTo(world.Month + 10);
        state = world.Checkpoint(); teacher = state.People.Single(p => p.Id == teacher.Id);
        if (teacher.Action != ActionKind.Idle)
        { Accept(world, pupil.Id, "observe"); Accept(world, teacher.Id, "cancel"); Accept(world, pupil.Id, "possess"); }
        state = world.Checkpoint(); teacher = state.People.Single(p => p.Id == teacher.Id);
        teacher.Action = ActionKind.Guard; teacher.Due = state.Month + 1000;
        world = GameWorld.Restore(state);
        Check(Person(world, pupil.Id).Understanding.GetValueOrDefault("entry") == 1000, "Entry learning did not come from ten actual joint months.");
        return (world, pupil.Id, teacher.Id);
    }

    internal static void ReadyEntry(GameWorld world, long pupil)
    {
        for (var i = 0; i < 4; i++)
        {
            var state = world.Checkpoint(); var p = state.People.Single(p => p.Id == pupil);
            if (state.Cultivation[p.BodyId].EntryWork >= state.Cultivation[p.BodyId].GrowthBasis?.EntryWork) return;
            Accept(world, pupil, "cultivate"); world.AdvanceTo(world.Month + 1);
        }
        throw new InvalidOperationException("Actual entry preparation did not finish in the bounded functional setup.");
    }

    internal static void Until(GameWorld world, Func<GameState, bool> condition, int months)
    {
        for (var i = 0; i < months && !condition(world.Checkpoint()); i++) world.AdvanceTo(world.Month + 1);
        Check(condition(world.Checkpoint()), "Bounded cultivation goal did not reach the expected actual boundary.");
    }
    internal static void Validate(GameWorld world) => Invariants.Validate(world.Checkpoint());
}

public sealed class AgentEntrySourceScenario : IFeatureScenario
{
    public string Name => "INV-004/006/018 AI 수행 연결: 실제 entry 전수·토납 뒤 감응하고 Qi 공법·숙련은 자동 지급하지 않는다";
    public void Run()
    {
        var (world, pupil, _) = AgentCultivationFixture.Create();
        var before = world.StateHash(); var rejected = world.Execute(new("entry-too-early", pupil, "breakthrough"));
        AgentCultivationFixture.Check(!rejected.Accepted && world.StateHash() == before, "Incomplete entry preparation consumed IDs, RNG or reservations.");
        AgentCultivationFixture.ReadyEntry(world, pupil);
        var p = AgentCultivationFixture.Person(world, pupil);
        AgentCultivationFixture.Check(p.Realm == Realm.Mortal && p.Action == ActionKind.Idle && world.Checkpoint().Cultivation[p.BodyId].Attempt is null,
            "A player ordinary cultivation month silently initiated a breakthrough.");
        var memory = world.Checkpoint().Knowledge.Memories.Last(m => m.PersonId == pupil && m.WorkId == "entry");
        AgentCultivationFixture.Accept(world, pupil, "observe"); world.AdvanceTo(world.Month + 1);
        var prepared = world.Checkpoint().Cultivation[p.BodyId].Attempt;
        AgentCultivationFixture.Check(prepared is { Phase: AttemptPhase.Prepared, Kind: AttemptKind.Entry } &&
            prepared.Knowledge.WorkId == "entry" && prepared.Knowledge.Revision == memory.Revision && prepared.Knowledge.Source == memory.SourceId,
            "AI readiness did not issue the actual preparation command with the personal entry source.");
        var replay = GameCodec.Decode(GameCodec.Encode(world));
        for (var i = 0; i < 5; i++)
        {
            world.AdvanceTo(world.Month + 1); replay.AdvanceTo(replay.Month + 1);
            AgentCultivationFixture.Check(world.StateHash() == replay.StateHash(), "Saved entry preparation, RNG, paid source or stabilization diverged.");
        }
        var state = world.Checkpoint(); p = state.People.Single(p => p.Id == pupil);
        AgentCultivationFixture.Check(p.Realm == Realm.Qi && p.Layer == 1 && p.Lifespan >= state.Laws["life.qi"] &&
            state.Cultivation[p.BodyId].Formations.Single().Knowledge is { WorkId: "entry" } source && source.Revision == memory.Revision,
            "Actual sensing did not form Qi1 from the adopted entry revision.");
        AgentCultivationFixture.Check(!state.Knowledge.Memories.Any(m => m.PersonId == pupil && m.WorkId == "path.1.0") &&
            !state.Knowledge.Mastery.Any(m => m.PersonId == pupil && m.WorkId == "path.1.0") && state.Cultivation[p.BodyId].RootGrowth.Total == 0,
            "Entry manufactured a Qi method, practical mastery or training progress.");
        AgentCultivationFixture.Validate(world);
    }
}

public sealed class AgentCultivationTeachingScenario : IFeatureScenario
{
    public string Name => "INV-004/008/017/018 AI 수행 연결: 입문 후 정책 이해도까지 실제 재전수하고 다음 경지 학습·돌파를 이어간다";
    public void Run()
    {
        var (world, pupil, teacher) = AgentCultivationFixture.Create(); AgentCultivationFixture.ReadyEntry(world, pupil);
        AgentCultivationFixture.Accept(world, pupil, "observe");
        AgentCultivationFixture.Until(world, s => s.People.Single(p => p.Id == pupil).Realm == Realm.Qi &&
            s.Cultivation[s.People.Single(p => p.Id == pupil).BodyId].Attempt?.Phase == AttemptPhase.Completed, 8);
        var state = world.Checkpoint();
        AgentCultivationFixture.Check(state.People.Single(p => p.Id == teacher).LessonRequests.Any(r => r.Pupil == pupil && r.Knowledge == "path.1.0") &&
            !state.Knowledge.Memories.Any(m => m.PersonId == pupil && m.WorkId == "path.1.0"), "Unknown Qi method skipped the real request/wait boundary.");
        AgentCultivationFixture.Accept(world, teacher, "cancel");
        AgentCultivationFixture.Until(world, s => s.Knowledge.Memories.Any(m => m.PersonId == pupil && m.WorkId == "path.1.0"), 5);
        state = world.Checkpoint(); var first = state.Knowledge.Memories.Last(m => m.PersonId == pupil && m.WorkId == "path.1.0");
        AgentCultivationFixture.Check(first.Comprehension == 1 && !state.Knowledge.Mastery.Any(m => m.PersonId == pupil && m.WorkId == "path.1.0"),
            "The first actual lesson skipped the one-percent learning boundary or granted practice.");
        var replay = GameCodec.Decode(GameCodec.Encode(world));
        for (var i = 0; i < 65 && world.Checkpoint().People.Single(p => p.Id == pupil).Realm < Realm.Foundation; i++)
        {
            world.AdvanceTo(world.Month + 1); replay.AdvanceTo(replay.Month + 1);
            AgentCultivationFixture.Check(world.StateHash() == replay.StateHash(), "Repeated teaching/next-realm planning failed deterministic resume.");
            var current = world.Checkpoint();
            if ((current.Knowledge.Memories.LastOrDefault(m => m.PersonId == pupil && m.WorkId == "path.1.0")?.Comprehension ?? 0) < 10)
                AgentCultivationFixture.Check(!current.Knowledge.Activities.Any(a => a.ActorId == pupil && a.WorkId == "path.1.0" && a.Kind == KnowledgeActivityKind.Practice),
                    "AI trained a Qi method at the production-only one-percent threshold.");
        }
        state = world.Checkpoint(); var p = state.People.Single(p => p.Id == pupil);
        var lessons = state.Knowledge.Activities.Where(a => a.ActorId == pupil && a.WorkId == "path.1.0" && a.Kind == KnowledgeActivityKind.Teaching && a.Status == KnowledgeActivityStatus.Completed).ToArray();
        var next = state.Cultivation[p.BodyId].Attempt;
        AgentCultivationFixture.Check(lessons.Length >= 10 && lessons.All(a => a.TeacherId == teacher && a.DueMonth > a.StartedMonth) &&
            state.Knowledge.Memories.Last(m => m.PersonId == pupil && m.WorkId == "path.1.0").Comprehension >= 10 &&
            p.Realm == Realm.Foundation && next is { Kind: AttemptKind.RealmBreakthrough, Knowledge.WorkId: "path.2.0" } && next.Knowledge.Understanding >= 1000,
            "AI used production's one-percent threshold, omitted paid repeated lessons, or never chose the next real breakthrough.");
        AgentCultivationFixture.Check(p.Decisions.All(d => d.Expansions <= state.Laws["ai.planBudget"]), "Learning/entry planning exceeded its actual search budget.");
        AgentCultivationFixture.Validate(world);
    }
}

public sealed class AgentCultivationRelearningScenario : IFeatureScenario
{
    public string Name => "INV-004/006/018/024 AI 수행 연결: 명시 Qi 대원만 경계에서 실제 오독·준비 거절 뒤 재독해하고 과거 해석을 보존한다";
    public void Run()
    {
        var (world, pupil, teacher) = AgentCultivationFixture.Create();
        AgentCultivationFixture.Accept(world, pupil, "observe"); AgentCultivationFixture.Accept(world, teacher, "cancel");
        AgentCultivationFixture.Accept(world, teacher, "teach", pupil, 10, "path.1.0"); AgentCultivationFixture.Accept(world, pupil, "possess");
        world.AdvanceTo(world.Month + 10);
        AgentCultivationFixture.Accept(world, pupil, "observe"); AgentCultivationFixture.Accept(world, teacher, "cancel");
        AgentCultivationFixture.Accept(world, 0, "law", value: 10000, data: "knowledge.misreadChance");
        var state = world.Checkpoint(); var p = state.People.Single(p => p.Id == pupil);
        // This one failure-boundary setup is declared, not claimed as an earned realm or a growth result.
        WorldScenarioFixtures.SetCultivation(state, p, Realm.Qi, 9, true);
        foreach (var other in state.People.Where(p => p.Id != pupil)) { other.Action = ActionKind.Guard; other.Due = state.Month + 1000; }
        var book = state.Items.First(i => i.Owner == teacher && i.Definition == "path.2.0"); book.Owner = pupil;
        if (state.Inventory.Metadata.FirstOrDefault(m => m.Item == book.Id) is { } metadata) metadata.Custodian = pupil;
        var authority = KnowledgeAuthority.Restore(state.Knowledge);
        AgentCultivationFixture.Check(authority.SynchronizeMedium(book.Id, pupil, p.Place, 3, [], false, false, state.Month,
            "fixture:declared-foundation-book-transfer").Applied, "Declared physical medium transfer was inconsistent.");
        state.Knowledge = authority.Checkpoint(); state.Controlled = pupil;
        world = GameWorld.Restore(state);
        AgentCultivationFixture.Accept(world, pupil, "study", book.Id, 10); world.AdvanceTo(world.Month + 10);
        var bad = world.Checkpoint().Knowledge.Memories.Last(m => m.PersonId == pupil && m.WorkId == "path.2.0");
        AgentCultivationFixture.Check(bad.Comprehension == 10 && bad.Belief == KnowledgeBelief.Unverified, "Actual initial misreading fixture did not preserve an untested belief.");
        AgentCultivationFixture.Accept(world, pupil, "observe"); world.AdvanceTo(world.Month + 1);
        state = world.Checkpoint(); p = state.People.Single(p => p.Id == pupil);
        AgentCultivationFixture.Check(p.Beliefs.GetValueOrDefault("cultivation:lastFailure")?.Source.Contains(":codes:interpretation", StringComparison.Ordinal) == true &&
            state.Cultivation[p.BodyId].Attempt is null && state.Knowledge.Memories.Last(m => m.PersonId == pupil && m.WorkId == "path.2.0").Claim == bad.Claim,
            "A real preflight rejection either went unobserved or rewrote personal interpretation without learning.");
        var replay = GameCodec.Decode(GameCodec.Encode(world));
        for (var i = 0; i < 20 && world.Checkpoint().Cultivation[p.BodyId].Attempt is null; i++)
        {
            world.AdvanceTo(world.Month + 1); replay.AdvanceTo(replay.Month + 1);
            AgentCultivationFixture.Check(world.StateHash() == replay.StateHash(), "Saved preflight rejection/relearning diverged.");
        }
        state = world.Checkpoint(); var corrected = state.Knowledge.Memories.Last(m => m.PersonId == pupil && m.WorkId == "path.2.0");
        AgentCultivationFixture.Check(corrected.Claim != bad.Claim && corrected.Comprehension >= 10 &&
            state.Knowledge.Memories.Any(m => m.PersonId == pupil && m.Revision == bad.Revision && m.Claim == bad.Claim) &&
            state.Knowledge.Activities.Count(a => a.ActorId == pupil && a.WorkId == "path.2.0" && a.Kind == KnowledgeActivityKind.Study && a.Status == KnowledgeActivityStatus.Completed) >= 11 &&
            state.Cultivation[p.BodyId].Attempt?.Knowledge.Revision == corrected.Revision,
            "Actual reading time/rights were skipped, the old wrong interpretation disappeared, or preparation reused the wrong revision.");
        AgentCultivationFixture.Validate(world);
    }
}

public sealed class AgentCultivationFailureScenario : IFeatureScenario
{
    public string Name => "INV-004/006/018/024 AI 수행 연결: 실제 감응 실패·회복 후 새 시도를 하며 과거 실패와 출처를 보존한다";
    public void Run()
    {
        var (world, pupil, _) = AgentCultivationFixture.Create(); AgentCultivationFixture.ReadyEntry(world, pupil);
        AgentCultivationFixture.Accept(world, pupil, "observe");
        AgentCultivationFixture.Accept(world, 0, "law", value: 10000, data: "cultivation.breakthroughRisk"); world.AdvanceTo(world.Month + 1);
        var p = AgentCultivationFixture.Person(world, pupil); var firstId = world.Checkpoint().Cultivation[p.BodyId].Attempt!.Id;
        var replay = GameCodec.Decode(GameCodec.Encode(world));
        for (var i = 0; i < 2; i++)
        { world.AdvanceTo(world.Month + 1); replay.AdvanceTo(replay.Month + 1); AgentCultivationFixture.Check(world.StateHash() == replay.StateHash(), "Failed sensing replay changed RNG or receipts."); }
        var state = world.Checkpoint(); p = state.People.Single(p => p.Id == pupil);
        AgentCultivationFixture.Check(p.Realm == Realm.Mortal && state.Cultivation[p.BodyId].Attempt?.Phase == AttemptPhase.Recovering &&
            state.Events.Any(e => e.Actor == pupil && e.Kind == "형성 실패"), "Actual certain-risk fixture did not record failed sensing without a free realm.");
        AgentCultivationFixture.Accept(world, 0, "law", value: 0, data: "cultivation.breakthroughRisk");
        AgentCultivationFixture.Until(world, s => s.Cultivation[p.BodyId].Attempt?.Id != firstId, 10);
        state = world.Checkpoint(); var next = state.Cultivation[p.BodyId].Attempt!;
        AgentCultivationFixture.Check(next.Id != firstId && next.LawRevision == state.LawRevision && next.Knowledge.WorkId == "entry" &&
            state.Events.Any(e => e.Actor == pupil && e.Kind == "형성 실패") && state.People.Single(p => p.Id == pupil).Realm == Realm.Mortal,
            "Recovery reused the old frozen risk/receipt, erased failure, or granted a realm before a new actual attempt.");
        AgentCultivationFixture.Validate(world);
    }
}

public sealed class AgentCultivationInformationScenario : IFeatureScenario
{
    public string Name => "INV-006/008/021 AI 수행 연결: 숨은 타인 재고·지식 변경은 자기 입문 선택·시간·난수를 바꾸지 않는다";
    public void Run()
    {
        var (a, pupil, _) = AgentCultivationFixture.Create(); var (b, _, _) = AgentCultivationFixture.Create(true);
        AgentCultivationFixture.ReadyEntry(a, pupil); AgentCultivationFixture.ReadyEntry(b, pupil);
        AgentCultivationFixture.Accept(a, pupil, "observe"); AgentCultivationFixture.Accept(b, pupil, "observe");
        a.AdvanceTo(a.Month + 1); b.AdvanceTo(b.Month + 1);
        var left = AgentCultivationFixture.Person(a, pupil); var right = AgentCultivationFixture.Person(b, pupil);
        AgentCultivationFixture.Check(left.Action == ActionKind.Breakthrough && left.Action == right.Action && left.Due == right.Due &&
            left.Goal == right.Goal && left.Reason == right.Reason && JsonSerializer.Serialize(left.Decisions[^1]) == JsonSerializer.Serialize(right.Decisions[^1]) &&
            a.Checkpoint().RandomState == b.Checkpoint().RandomState,
            "Own entry readiness read another person's private method/stock or global knowledge revision.");
        foreach (var world in new[] { a, b })
        {
            var state = world.Checkpoint(); var p = state.People.Single(p => p.Id == pupil);
            var memory = state.Knowledge.Memories.Last(m => m.PersonId == pupil && m.WorkId == "entry");
            var adopted = state.Cultivation[p.BodyId].Attempt!.Knowledge;
            AgentCultivationFixture.Check(adopted.WorkId == "entry" && adopted.Revision == memory.Revision && adopted.Source == memory.SourceId,
                "The preparation substituted hidden global revision for its own actual entry source.");
        }
    }
}
