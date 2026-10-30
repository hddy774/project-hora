using Chronicles.Game.Cultivation;

namespace Chronicles.Game.Tests.Features;

public sealed class EntryPreparationKnownRouteScenario : IFeatureScenario
{
    public string Name => "INV-009/010 입문 준비 조회: 실제 entry 전수만 감응 선택을 열고 Qi 공법은 만들지 않는다";

    public void Run()
    {
        var empty = GameWorld.Create(42, new Dictionary<string, long> { ["birth.enabled"] = 0 });
        QueryFixture.Accepted(empty, "create", data: "미학습 입문자");
        var novice = empty.Checkpoint().People[^1].Id;
        QueryFixture.Accepted(empty, "possess", novice);
        QueryFixture.Check(empty.View(novice).Detail!.Potential!.KnownNextRoutes.Count == 0,
            "An arrival without actual personal entry knowledge received a sensing choice.");

        var (world, pupil, _) = AgentCultivationFixture.Create();
        var before = world.StateHash(); var detail = world.View(pupil).Detail!;
        var route = detail.Potential!.KnownNextRoutes.Single();
        QueryFixture.Check(route.Path == "path.1.0" && route.TargetRealm == Realm.Qi &&
            route.Condition.Contains("별도 학습", StringComparison.Ordinal), "Actual entry did not project its distinct sensing choice.");
        QueryFixture.Check(detail.Understanding!.GetValueOrDefault("path.1.0") == 0 &&
            !world.Checkpoint().Knowledge.Memories.Any(m => m.PersonId == pupil && m.WorkId == "path.1.0"),
            "A sensing choice manufactured Qi-method knowledge.");
        QueryFixture.Check(route.ParentQualityCarried is null && route.MinimumSupplyToStart is null && route.Missing is null &&
            detail.Potential.HasActualRecoverySupply is null, "Personal sensing exposed authority appraisal or live site preflight.");
        QueryFixture.Check(world.StateHash() == before, "Entry projection changed authority, IDs or RNG.");
        var rejected = world.Execute(new("entry-query-incomplete", pupil, "cultivationprepare", Data: route.Path));
        QueryFixture.Check(!rejected.Accepted && world.StateHash() == before,
            "A known choice bypassed actual incomplete preparation or changed rejected-command authority/RNG.");
    }
}

public sealed class EntryPreparationSaveScenario : IFeatureScenario
{
    public string Name => "INV-004/006 입문 준비 조회: 토납과 감응을 분리하고 실제 준비·개시 공급과 저장을 유지한다";

    public void Run()
    {
        var (world, pupil, _) = AgentCultivationFixture.Create();
        AgentCultivationFixture.ReadyEntry(world, pupil);
        var state = world.Checkpoint(); var person = state.People.Single(p => p.Id == pupil);
        QueryFixture.Check(person.Realm == Realm.Mortal && state.Cultivation[person.BodyId].Attempt is null,
            "Ordinary entry cultivation silently started sensing.");
        var route = world.View(pupil).Detail!.Potential!.KnownNextRoutes.Single();
        var result = world.Execute(new("entry-query-ready", pupil, "cultivationprepare", 0, Data: route.Path));
        QueryFixture.Check(result.Accepted, result.Message);
        var attempt = world.Checkpoint().Cultivation[person.BodyId].Attempt;
        QueryFixture.Check(attempt is { Kind: AttemptKind.Entry, Phase: AttemptPhase.Prepared } &&
            attempt.Path == route.Path && attempt.Knowledge.WorkId == "entry", "Actual entry source was not adopted by preparation.");
        var saved = GameCodec.Decode(GameCodec.Encode(world));
        QueryFixture.Check(saved.StateHash() == world.StateHash() &&
            QueryFixture.Json(saved.View(pupil).Detail!.Potential) == QueryFixture.Json(world.View(pupil).Detail!.Potential),
            "Prepared entry or its personal choice changed across save/load.");
        var before = world.Checkpoint(); var qi = before.Places[person.Place].Qi;
        var opened = before.CultivationSupply.GetValueOrDefault(person.Place).Total;
        var started = world.Execute(new("entry-query-start", pupil, "cultivationstart"));
        QueryFixture.Check(started.Accepted, started.Message);
        var after = world.Checkpoint();
        QueryFixture.Check(after.Places[person.Place].Qi < qi ||
            after.CultivationSupply.GetValueOrDefault(person.Place).Total < opened, "Entry start did not pay actual elemental supply.");
        QueryFixture.Check(!after.Knowledge.Memories.Any(m => m.PersonId == pupil && m.WorkId == "path.1.0") &&
            !after.Knowledge.Mastery.Any(m => m.PersonId == pupil && m.WorkId == "path.1.0"),
            "Entry preparation/start created an unlearned Qi method or mastery.");
        QueryFixture.Check(GameCodec.Decode(GameCodec.Encode(world)).StateHash() == world.StateHash(),
            "Paid entry start changed across save/load.");
        Invariants.Validate(after);
    }
}

public sealed class EntryPreparationHiddenSiteScenario : IFeatureScenario
{
    public string Name => "INV-009 입문 준비 조회: 미관측 현재 공급·사용권 변화는 개인 감응 선택을 바꾸지 않는다";

    public void Run()
    {
        var (world, pupil, teacher) = AgentCultivationFixture.Create();
        var before = QueryFixture.Json(world.View(pupil).Detail!.Potential);
        var state = world.Checkpoint(); var p = state.People.Single(x => x.Id == pupil);
        var place = state.Places[p.Place]; place.Qi = 0; place.Owner = teacher; place.Operator = teacher;
        place.Delegate = 0; place.BlockedUntil = state.Month + 20;
        var changed = GameWorld.Restore(state); var hash = changed.StateHash();
        QueryFixture.Check(before == QueryFixture.Json(changed.View(pupil).Detail!.Potential),
            "Hidden site supply/rights influenced the personal entry choice.");
        QueryFixture.Check(changed.StateHash() == hash, "Personal entry query changed hidden-world authority or RNG.");
    }
}
