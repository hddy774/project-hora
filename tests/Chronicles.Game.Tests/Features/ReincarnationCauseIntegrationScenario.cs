namespace Chronicles.Game.Tests;

public sealed class ReincarnationConcurrentCauseScenario : IFeatureScenario
{
    public string Name => "INV-004/005/006/013 윤회 공개 동시 원인: 실제 12·7의 단일 우선 선택과 활성·완성 경계·대체 설명·안정 키를 저장하고 재처리한다";

    public void Run()
    {
        foreach (var (mask, expected) in new[] { (8191L, 12), (1L << 6, 7), (0L, 0) })
        {
            var world = Initial(); var actor = world.Checkpoint().People[0];
            Accept(world, new("capacity-limit", 0, "law", Value: 9, Data: "world.capacityRealm"));
            Accept(world, new("active-routes", 0, "law", Value: mask, Data: "soul.enabledRoutes"));
            Accept(world, new("observe-ending", actor.Id, "possess"));
            var before = world.Checkpoint(); var saved = GameCodec.Decode(GameCodec.Encode(world));
            world.AdvanceTo(1); saved.AdvanceTo(1); var ended = world.Checkpoint();
            var soul = ended.Souls.Single(s => s.Id == actor.SoulId);
            Check(world.StateHash() == saved.StateHash() && soul.Route == expected &&
                ended.People[0].Status == LifeStatus.Departed && ended.DeathCount == 0 && ended.Souls.Count == before.Souls.Count,
                "활성 경로 선택은 단일 여정만 배정하며 실제 반선 배척·세계 이탈을 사망으로 바꾸지 않는다.");
            Check(soul.SourceRealm == Realm.HalfImmortal && soul.SourceLayer == 9 &&
                soul.Evidence.Contains("원인키 life:" + actor.BodyId + ":1", StringComparison.Ordinal) &&
                soul.Evidence.Contains("RC-12:", StringComparison.Ordinal) &&
                soul.Evidence.IndexOf("RC-12:", StringComparison.Ordinal) < soul.Evidence.IndexOf("RC-07:", StringComparison.Ordinal) &&
                soul.Evidence.Contains("RC-07:", StringComparison.Ordinal) &&
                ended.RandomState == before.RandomState,
                "종료 직전 구조·같은 tick 실제 두 원인·기존 우선순위·안정 키를 보존하고 난수를 추가 소비하지 않는다.");
            Check(soul.Status == (expected == 0 ? "ended" : "waiting") &&
                ended.Events.Count(e => e.Actor == actor.Id && e.Kind == "세계 이탈") == 1 &&
                ended.Events.Single(e => e.Actor == actor.Id && e.Kind == "세계 이탈").Source == "life:" + actor.BodyId + ":1",
                "비활성 경로도 실제 현생 종료는 보존하며 원인/여정 종결은 한 번이다.");
            var exact = world.StateHash(); var evidence = soul.Evidence;
            Accept(world, new("capacity-limit", 0, "law", Value: 9, Data: "world.capacityRealm"));
            world.AdvanceTo(1);
            Check(world.StateHash() == exact && GameCodec.Decode(GameCodec.Encode(world)).Checkpoint().Souls
                .Single(s => s.Id == actor.SoulId).Evidence == evidence, "같은 명령·같은 시각 재처리는 여정·원인 키를 다시 만들지 않는다.");
        }

        var incomplete = Initial(complete: false); var below = incomplete.Checkpoint().People[0];
        Accept(incomplete, new("incomplete-capacity", 0, "law", Value: 9, Data: "world.capacityRealm"));
        Accept(incomplete, new("only-complete-route", 0, "law", Value: 1L << 6, Data: "soul.enabledRoutes"));
        Accept(incomplete, new("control-incomplete", below.Id, "possess")); incomplete.AdvanceTo(1);
        var ineligible = incomplete.Checkpoint().Souls.Single(s => s.Id == below.SoulId);
        Check(ineligible.Route == 0 && !ineligible.Evidence.Contains("RC-07:", StringComparison.Ordinal) &&
            incomplete.Checkpoint().People[0].Status == LifeStatus.Departed, "미완성 반선에게 조건이 맞지 않은 RC07을 잠재 원인으로 추가하지 않는다.");

        // Explicit finite initial media, then actual paid exploration and physical ward use produce both timers.
        var guarded = Initial(recovery: true); var investigator = guarded.Checkpoint().People[0];
        Accept(guarded, new("real-seal-explore-control", investigator.Id, "possess"));
        Accept(guarded, new("real-seal-explore", investigator.Id, "explore")); guarded.AdvanceTo(1);
        var ward = guarded.Checkpoint().Items.Single(i => i.Owner == investigator.Id && i.Definition == "soulWard").Id;
        Accept(guarded, new("real-maintainable-pact", investigator.Id, "use", ward));
        var actual = guarded.Checkpoint();
        Check(actual.People[0].Sealed && actual.People[0].SealedUntil == 2 && actual.People[0].SoulPactUntil == 2,
            "잠재 조건 거절 검사는 실제 탐색 금제와 실제 물품 소비로 만든 혼약 기한을 사용한다.");
        Accept(guarded, new("capacity-observer", 0, "observe"));
        Accept(guarded, new("actual-capacity-with-tools", 0, "law", Value: 9, Data: "world.capacityRealm"));
        Accept(guarded, new("actual-capacity-control", investigator.Id, "possess"));
        var prepared = GameCodec.Decode(GameCodec.Encode(guarded)); guarded.AdvanceTo(2); prepared.AdvanceTo(2);
        var terminated = guarded.Checkpoint(); var guardedSoul = terminated.Souls.Single(s => s.Id == investigator.SoulId);
        Check(guarded.StateHash() == prepared.StateHash() && guardedSoul.Route == 12 &&
            !guardedSoul.Evidence.Contains("RC-04:", StringComparison.Ordinal) && !guardedSoul.Evidence.Contains("RC-11:", StringComparison.Ordinal) &&
            terminated.Items.Any(i => i.Definition == "array") && terminated.Items.All(i => i.Id != ward),
            "실제 복구 도구가 있는 금제와 건강한 기한 혼약을 가짜 종료 원인으로 기록하거나 비승/배척 때 소모하지 않는다.");
        Invariants.Validate(terminated);
    }

    private static GameWorld Initial(bool complete = true, bool recovery = false)
    {
        var world = GameWorld.Create(42, new Dictionary<string, long>
        {
            ["birth.enabled"] = 0, ["ecology.foodUse"] = 0, ["ecology.disasterChance"] = 0,
            ["cultivation.recovery"] = 0, ["hazard.sealChance"] = 10000, ["hazard.sealDuration"] = 1,
            ["soul.pactDuration"] = 1
        });
        var state = world.Checkpoint(); var actor = state.People[0];
        actor.Place = recovery ? 8 : 0; actor.KnownPlaces = [actor.Place];
        WorldScenarioFixtures.SetCultivation(state, actor, Realm.HalfImmortal, complete ? 9 : 8, complete);
        WorldScenarioFixtures.SetCultivation(state, state.People[1], Realm.Spirit, 9, true);
        state.People[1].Action = ActionKind.Wait; state.People[1].Due = 10000;
        if (recovery)
            foreach (var definition in new[] { "array", "soulWard" })
            {
                state.Items.Add(new() { Id = state.NextId++, Definition = definition, Quantity = 1, Owner = actor.Id,
                    Place = -1, Quality = 5000, Source = "동시 원인 부정 검사의 명시 유한 초기 실물" });
                if (!state.Materials.TryGetValue(definition, out var flow)) state.Materials.Add(definition, flow = new());
                flow.Opening++;
            }
        return GameWorld.Restore(state);
    }

    private static void Accept(GameWorld world, GameCommand command)
    { var result = world.Execute(command); Check(result.Accepted, command.Kind + ": " + result.Message); }

    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
}
