using System.Globalization;
using System.Text.Json;
using Chronicles.Game.Cultivation;
using Chronicles.Game.Knowledge;
using Evidence = Chronicles.Game.Cultivation.KnowledgeEvidence;

namespace Chronicles.Game.Tests.Features;

public sealed class CultivationPreparationEvidenceScenario : IFeatureScenario
{
    public string Name => "INV-004/006/021 감응 출처 선택: entry 추가는 유효 Qi 자격·정수 품질을 낮추지 않는다";
    public void Run()
    {
        var actor = CultivationFixture.Actor(Realm.Mortal);
        var weak = actor with { InnateRoots = Elements.Uniform(1), Circulation = 0, Body = 0, Mind = 3000 };
        var qi = new Evidence("path.1.0", "valid-qi-edition", 7, 5000, 5000, 0, true, false, "actual independently learned Qi source r7");
        var entry = new Evidence("entry", "entry-edition", 2, 5000, 0, 0, true, false, "actual entry source r2");
        Check("Qi만", actor, [qi], qi, 5833);
        Check("낮은 입문", actor, [entry with { Understanding = 500 }, qi], qi, 5833);
        Check("반박된 입문", actor, [entry with { Error = 8000 }, qi], qi, 5833);
        Check("접근 불가 입문", actor, [entry with { Memorized = false }, qi], qi, 5833);
        Check("약한 기반 Qi만", weak, [qi], qi, 3000);
        Check("약한 기반·최소 입문", weak, [entry with { Understanding = 1000 }, qi], qi, 3000);
        Check("정수 반올림 동률", actor, [entry with { Understanding = 5001 }, qi], qi, 5833);
        Check("완전 동률·입력 역순", actor, [qi, entry], qi, 5833);
        var stronger = entry with { Understanding = 5004, Revision = 9 };
        Check("실제 더 나은 입문", actor, [qi, stronger], stronger, 5834);
        Check("오류 손실 포함 Qi 우선", actor, [entry with { Understanding = 9000, Error = 2000 }, qi], qi, 5833);
        var usableEntry = entry with { Understanding = 9000, Error = 1000 };
        Check("오류 손실 포함 입문 우선", actor, [qi, usableEntry], usableEntry, 6000);
        Check("오류 자격 상한", actor, [entry with { Understanding = 10000, Error = 5001 }, qi], qi, 5833);
        Check("음수 입문 품질이 Qi를 가리지 않음", weak, [entry with { Understanding = 1000, Error = 5000 }, qi], qi, 3000);
        Check("입문만", actor, [entry with { Understanding = 1000 }], entry with { Understanding = 1000 }, 5166);
        Check("두 후보 부적격", actor, [entry with { Error = 8000 }, qi with { Understanding = 999 }], null, null, "interpretation");
        Check("최선도 형성 부족", weak, [entry with { Understanding = 1000 }, qi with { Understanding = 1000 }], null, null, "formation");
        Check("두 후보 모두 음수 형성", weak, [entry with { Understanding = 1000, Error = 5000 }, qi with { Understanding = 1000, Error = 5000 }], null, null, "formation");
        Check("최신 실제 판본만 사용", actor, [qi, qi with { Revision = 8, Understanding = 999 }, entry with { Understanding = 500 }], null, null, "knowledge");

        static void Check(string label, CultivationActor actual, Evidence[] learned, Evidence? expected, int? quality, string? blocker = null)
        {
            var context = CultivationFixture.Context(2) with { Knowledge = learned };
            var state = CultivationFixture.State(actual, context.Policy, true);
            var input = CultivationFixture.Json(new { state, actual, context });
            var command = new PrepareCultivation("source-test:" + label, state.Revision, "path.1.0");
            var result = CultivationEngine.Execute(state, actual, command, context, 0);
            var otherSample = CultivationEngine.Execute(state, actual, command, context, 9999);
            var route = CultivationQueries.Potential(state, actual, context).KnownNextRoutes.Single(r => r.Path == "path.1.0");
            CultivationFixture.Check(CultivationFixture.Json(result) == CultivationFixture.Json(otherSample) &&
                CultivationFixture.Json(new { state, actual, context }) == input, label + ": sample-dependent selection or query/input mutation");
            CultivationFixture.Check(result.SupplyDebit.Total == 0 && result.Supports.Count == 0, label + ": unpaid preparation debit");
            if (expected is not null)
            {
                CultivationFixture.Check(result.Accepted && result.State.Attempt?.Knowledge == expected &&
                    result.State.Attempt.FormationQuality == quality && route.Missing.Count == 0,
                    label + ": chosen work/edition/revision/quality diverged from preflight: " + CultivationFixture.Json(result.Blockers));
                CultivationFixture.Check(route.Condition.Contains($"준비 지식 {expected.WorkId}·판본 {expected.EditionId}·개인 revision {expected.Revision}", StringComparison.Ordinal),
                    label + ": conditional query reported another adopted source");
            }
            else CultivationFixture.Check(!result.Accepted && result.Blockers.Any(b => b.Code == blocker) &&
                route.Missing.Any(b => b.Code == blocker) && CultivationFixture.Json(result.State) == CultivationFixture.Json(state), label + ": refusal must remain atomic and visible in supplied-context preflight");
        }
    }
}

public sealed class CultivationPreparationContractScenario : IFeatureScenario
{
    public string Name => "INV-004/006/023 감응 출처 선택: 1% 실습·전환·상위 경지 계약과 동결 출처 유지";
    public void Run()
    {
        var h = new CultivationFixture.Harness(Realm.Mortal);
        h.Context = h.Context with { Knowledge = [new("entry", "one-percent", 1, 100, 0, 0, true, false, "actual one-percent memory")] };
        h.Run((id, revision) => new PracticeCultivation(id, revision, "path.1.0", Elements.Uniform(1)), 1);
        CultivationFixture.Check(h.State.EntryWork > 0 && h.State.Attempt is null, "Ordinary one-percent practice was raised to preparation's ten-percent gate or started an attempt.");
        var consulted = h.Context.Knowledge[0] with { Understanding = 9000, Memorized = false, CanConsult = true };
        h.State = h.State with { EntryWork = CultivationEngine.EntryTarget(h.State, h.Context.Policy) };
        h.Context = h.Context with { Knowledge = [consulted] };
        h.Run((id, revision) => new PrepareCultivation(id, revision, "path.1.0"));
        var frozen = CultivationFixture.RoundTrip(h.State);
        h.State = frozen;
        h.Context = h.Context with { Knowledge = [consulted with { CanConsult = false }, new("path.1.0", "later-qi", 12, 10000, 9000, 0, true, false, "later actual learning")] };
        CultivationFixture.Check(h.Reject(new StartCultivation(h.Id(), h.State.Revision)).Blockers.Any(b => b.Code == "consult") &&
            h.State.Attempt!.Knowledge == consulted, "Starting silently replaced a frozen source with a newly better work.");

        h = new CultivationFixture.Harness(Realm.Qi, true);
        var original = h.Context.Knowledge.Single(k => k.WorkId == "path.2.0");
        h.Context = h.Context with { Knowledge = [new("entry", "perfect-entry", 100, 10000, 10000, 0, true, false, "entry cannot replace upper method"), original] };
        h.Run((id, revision) => new PrepareCultivation(id, revision, "path.2.0"));
        CultivationFixture.Check(h.State.Attempt!.Knowledge == original, "Upper preparation selected entry evidence.");
        h = new CultivationFixture.Harness(Realm.Qi, true);
        var conversion = h.Context.Knowledge.Single(k => k.WorkId == "path.1.1");
        h.Context = h.Context with { Knowledge = [new("entry", "entry", 5, 10000, 10000, 0, true, false, "entry"), conversion with { Practice = 0 }] };
        CultivationFixture.Check(h.Reject(new PrepareCultivation(h.Id(), h.State.Revision, "path.1.1", Conversion: true)).Blockers.Any(b => b.Code == "practice"), "Conversion practical gate was removed.");
        h.Context = h.Context with { Knowledge = [conversion] };
        h.Run((id, revision) => new PrepareCultivation(id, revision, "path.1.1", Conversion: true));
        CultivationFixture.Check(h.State.Attempt!.Knowledge == conversion, "Conversion no longer uses the selected method's actual evidence.");
    }
}

internal static class PreparationPublicFixture
{
    // The physical book and finite food are declared fixture custody/opening stock. All personal
    // knowledge, breathing work, preflight, payment and formation below use actual public commands.
    internal static (GameWorld World, long Pupil) QiOnly(bool misread = false)
    {
        var world = GameWorld.Create(42, new Dictionary<string, long>
        {
            ["birth.enabled"] = 0, ["ecology.foodUse"] = 0, ["ecology.disasterChance"] = 0,
            ["knowledge.misreadChance"] = misread ? 10000 : 0, ["cultivation.entry"] = 1,
            ["cultivation.qi"] = 2, ["cultivation.breakthroughRisk"] = 0
        });
        AgentCultivationFixture.Accept(world, 0, "create", data: "Qi 단독 학습 경계 도제");
        var state = world.Checkpoint(); var pupil = state.People[^1]; var teacher = state.People[0];
        foreach (var other in state.People.Take(2)) { other.Action = ActionKind.Guard; other.Due = 10000; }
        pupil.Personality = new int[14]; foreach (var axis in new[] { 1, 8, 10, 11, 13 }) pupil.Personality[axis] = 100;
        AgentPlanningFixture.Stock(state, pupil.Id, "food", 100);
        var book = state.Items.First(i => i.Owner == teacher.Id && i.Definition == "path.1.0"); book.Owner = pupil.Id;
        if (state.Inventory.Metadata.FirstOrDefault(m => m.Item == book.Id) is { } metadata) metadata.Custodian = pupil.Id;
        var authority = KnowledgeAuthority.Restore(state.Knowledge);
        CultivationFixture.Check(authority.SynchronizeMedium(book.Id, pupil.Id, pupil.Place, 3, [], false, false, 0,
            "fixture:declared-qi-book-custody").Applied, "Declared physical book custody is inconsistent.");
        state.Knowledge = authority.Checkpoint(); world = GameWorld.Restore(state);
        AgentCultivationFixture.Accept(world, pupil.Id, "possess");
        AgentCultivationFixture.Accept(world, pupil.Id, "study", book.Id, 10); world.AdvanceTo(10);
        state = world.Checkpoint();
        CultivationFixture.Check(state.Knowledge.Memories.Last(m => m.PersonId == pupil.Id && m.WorkId == "path.1.0").Comprehension == 10 &&
            state.Knowledge.Memories.All(m => m.PersonId != pupil.Id || m.WorkId != "entry"), "Real ten-month Qi reading granted entry memory or skipped learning.");
        return (world, pupil.Id);
    }
}

public sealed class CultivationPreparationPublicScenario : IFeatureScenario
{
    public string Name => "INV-004/006/008 감응 출처 선택: 실제 전수 Qi 우선·조회 무변화·동결 저장·단일 해소 난수";
    public void Run()
    {
        var (world, pupil, teacher) = AgentCultivationFixture.Create();
        AgentCultivationFixture.Accept(world, pupil, "observe"); AgentCultivationFixture.Accept(world, teacher, "cancel");
        AgentCultivationFixture.Accept(world, teacher, "teach", pupil, 20, "path.1.0"); AgentCultivationFixture.Accept(world, pupil, "possess");
        world.AdvanceTo(world.Month + 20);
        AgentCultivationFixture.Accept(world, pupil, "observe"); AgentCultivationFixture.Accept(world, teacher, "cancel");
        AgentCultivationFixture.Accept(world, pupil, "possess");
        var state = world.Checkpoint(); var t = state.People.Single(p => p.Id == teacher); t.Action = ActionKind.Guard; t.Due = state.Month + 1000;
        world = GameWorld.Restore(state); AgentCultivationFixture.ReadyEntry(world, pupil);
        state = world.Checkpoint(); var p = state.People.Single(p => p.Id == pupil);
        var learned = state.Knowledge.Memories.Last(m => m.PersonId == pupil && m.WorkId == "path.1.0");
        var hash = world.StateHash(); var rng = state.RandomState;
        var view = JsonSerializer.Serialize(world.View(pupil)); world.View(pupil);
        CultivationFixture.Check(world.StateHash() == hash && world.Checkpoint().RandomState == rng && JsonSerializer.Serialize(world.View(pupil)) == view,
            "Personal query changed authority or RNG while choosing a source.");
        var replay = GameCodec.Decode(GameCodec.Encode(world));
        var command = new GameCommand("public-evidence:prepare", pupil, "breakthrough", Data: "path.1.0");
        CultivationFixture.Check(world.Execute(command).Accepted && replay.Execute(command).Accepted && world.StateHash() == replay.StateHash() && world.Checkpoint().RandomState == rng,
            "Preparation consumed RNG or diverged after save.");
        var attempt = world.Checkpoint().Cultivation[p.BodyId].Attempt!;
        CultivationFixture.Check(attempt.Knowledge.WorkId == learned.WorkId && attempt.Knowledge.EditionId == learned.EditionId.ToString(CultureInfo.InvariantCulture) &&
            attempt.Knowledge.Revision == learned.Revision && attempt.Knowledge.Source == learned.SourceId,
            "Public preparation ignored its stronger actual Qi evidence.");
        world.AdvanceTo(world.Month + 1); replay.AdvanceTo(replay.Month + 1);
        state = world.Checkpoint();
        CultivationFixture.Check(state.Cultivation[p.BodyId].Attempt?.Phase == AttemptPhase.Started && (world.Month + 1) % 12 != 0,
            "Fixture is not isolated from annual random events at the resolution boundary.");
        // The GameWorld stream is xorshift64*, distinct from the Core SplitMix64 constructor stream.
        var expectedRng = state.RandomState;
        expectedRng ^= expectedRng >> 12; expectedRng ^= expectedRng << 25; expectedRng ^= expectedRng >> 27;
        world.AdvanceTo(world.Month + 1); replay.AdvanceTo(replay.Month + 1);
        CultivationFixture.Check(world.Checkpoint().RandomState == expectedRng, "A valid actual formation did not consume exactly one resolution sample.");
        for (var i = 0; i < 2; i++)
        { world.AdvanceTo(world.Month + 1); replay.AdvanceTo(replay.Month + 1); CultivationFixture.Check(world.StateHash() == replay.StateHash(), "Saved source/formation/RNG diverged."); }
        var formation = world.Checkpoint().Cultivation[p.BodyId].Formations.Single();
        CultivationFixture.Check(formation.Knowledge == attempt.Knowledge && formation.Quality == attempt.FormationQuality,
            "Formation changed the frozen work/edition/revision or quality.");
        AgentCultivationFixture.Validate(world);
    }
}

public sealed class CultivationPreparationQiOnlyAiScenario : IFeatureScenario
{
    public string Name => "INV-004/006/018 감응 출처 선택: entry 없는 실제 Qi 학습·토납 뒤 두 AI caller를 지나 준비한다";
    public void Run()
    {
        var (world, pupil) = PreparationPublicFixture.QiOnly(); AgentCultivationFixture.ReadyEntry(world, pupil);
        var state = world.Checkpoint(); var p = state.People.Single(p => p.Id == pupil);
        var memory = state.Knowledge.Memories.Last(m => m.PersonId == pupil && m.WorkId == "path.1.0");
        CultivationFixture.Check(p.Action == ActionKind.Idle && state.Cultivation[p.BodyId].Attempt is null &&
            state.Knowledge.Memories.All(m => m.PersonId != pupil || m.WorkId != "entry"), "Ordinary Qi-backed breathing granted entry knowledge or initiated a player breakthrough.");
        AgentCultivationFixture.Accept(world, pupil, "observe"); var replay = GameCodec.Decode(GameCodec.Encode(world));
        world.AdvanceTo(world.Month + 1); replay.AdvanceTo(replay.Month + 1);
        state = world.Checkpoint(); var attempt = state.Cultivation[p.BodyId].Attempt;
        CultivationFixture.Check(world.StateHash() == replay.StateHash() && attempt is { Phase: AttemptPhase.Prepared, Kind: AttemptKind.Entry } &&
            attempt.Knowledge.WorkId == "path.1.0" && attempt.Knowledge.Revision == memory.Revision && attempt.Knowledge.Source == memory.SourceId &&
            !state.People.Where(x => x.Id != pupil).SelectMany(x => x.LessonRequests).Any(r => r.Pupil == pupil && r.Knowledge == "entry"),
            "Known Qi was blocked by the missing-entry caller/score or replaced by an unnecessary entry request.");
        AgentCultivationFixture.Validate(world);
    }
}

public sealed class CultivationPreparationRelearningSourceScenario : IFeatureScenario
{
    public string Name => "INV-004/006/018 감응 출처 선택: 실제 Qi 오독 거부·재학습 대상도 같은 개인 판본이다";
    public void Run()
    {
        var (world, pupil) = PreparationPublicFixture.QiOnly(true); AgentCultivationFixture.ReadyEntry(world, pupil);
        var state = world.Checkpoint(); var p = state.People.Single(p => p.Id == pupil);
        var bad = state.Knowledge.Memories.Last(m => m.PersonId == pupil && m.WorkId == "path.1.0");
        AgentCultivationFixture.Accept(world, pupil, "observe"); world.AdvanceTo(world.Month + 1);
        state = world.Checkpoint(); p = state.People.Single(p => p.Id == pupil);
        var failure = p.Beliefs.GetValueOrDefault("cultivation:lastFailure");
        CultivationFixture.Check(state.Cultivation[p.BodyId].Attempt is null && failure is not null &&
            failure.Source.Contains($":path.1.0:{bad.Revision}:edition:{bad.EditionId}:codes:interpretation", StringComparison.Ordinal) &&
            p.Beliefs.ContainsKey("cultivation:refresh:path.1.0") && !p.Beliefs.ContainsKey("cultivation:refresh:entry") &&
            state.Knowledge.Activities.Any(a => a.ActorId == pupil && a.WorkId == "path.1.0" && a.Kind == KnowledgeActivityKind.Study && a.Status == KnowledgeActivityStatus.Active),
            "A real Qi preflight failure was attributed to entry or did not request the actual adopted work.");
        var replay = GameCodec.Decode(GameCodec.Encode(world));
        for (var i = 0; i < 15 && world.Checkpoint().Cultivation[p.BodyId].Attempt is null; i++)
        { world.AdvanceTo(world.Month + 1); replay.AdvanceTo(replay.Month + 1); CultivationFixture.Check(world.StateHash() == replay.StateHash(), "Source repair failed save/resume."); }
        state = world.Checkpoint(); var corrected = state.Knowledge.Memories.Last(m => m.PersonId == pupil && m.WorkId == "path.1.0");
        CultivationFixture.Check(state.Cultivation[p.BodyId].Attempt is { Knowledge.WorkId: "path.1.0" } prepared &&
            prepared.Knowledge.Revision == corrected.Revision && prepared.Knowledge.Source == corrected.SourceId && corrected.Revision > bad.Revision &&
            state.Knowledge.Memories.Any(m => m.PersonId == pupil && m.Revision == bad.Revision && m.Claim == bad.Claim) &&
            state.Knowledge.Memories.All(m => m.PersonId != pupil || m.WorkId != "entry"), "Repair rewrote history, manufactured entry, or prepared another personal revision.");
        AgentCultivationFixture.Validate(world);
    }
}
