using System.Security.Cryptography;
using System.Text.Json;
using Chronicles.Game.Cultivation;

namespace Chronicles.Game.Tests.Features;

public sealed class CultivationStrategyComparisonScenario : IFeatureScenario
{
    public string Name => "INV-004/006/018/023 R06 short functional strategy comparison: actual months resources quality risk and saved continuation";
    private sealed record PhaseEvidence(long Month, string Phase, long Due);
    private sealed record StrategyEvidence(string Strategy, string InitialStateHash, long LogicalMonths,
        long QiEnergyConsumed, int UnopenedQiConsumed, long PillConsumed, int PillRemaining, int PreparedRiskBps,
        int PreparedQuality, int FinalFormationQuality, int FinalDamage, int FinalHealth,
        long PrepareEvent, long FormationEvent, long[] RecoveryEvents, long[] StabilizationEvents,
        long[] ReservedPhysicalItems, PhaseEvidence[] Phases, long SavedMonth, string SavedStateHash,
        string SavedBytesSha256, string FinalStateHash, bool RestoredContinuationMatches);

    private static readonly IReadOnlyDictionary<string, long> ShortLaws = new Dictionary<string, long>(StringComparer.Ordinal)
    {
        ["birth.enabled"] = 0, ["ecology.foodUse"] = 0, ["ecology.regrowth"] = 0,
        ["ecology.spoilage"] = 0, ["ecology.disasterChance"] = 0,
        ["cultivation.entry"] = 1, ["cultivation.qi"] = 1, ["cultivation.foundation"] = 1,
        ["cultivation.upper"] = 100, ["cultivation.recovery"] = 0, ["cultivation.sourceCost"] = 2,
        ["cultivation.breakthroughRisk"] = 300, ["economy.craftDuration"] = 1, ["inventory.extractionWork"] = 1
    };

    private static void Send(GameWorld world, string id, long actor, string kind, string data = "", long target = 0)
    {
        var result = world.Execute(new(id, actor, kind, target, Data: data));
        CultivationFixture.Check(result.Accepted, id + ": " + result.Message);
    }

    private static (GameState State, long Actor, long Pill, long PillJob, object[] Inputs) CommonInitialState()
    {
        var world = GameWorld.Create(42, ShortLaws); var state = world.Checkpoint(); var actor = state.People[0].Id;
        state.People[1].Action = ActionKind.Wait; state.People[1].Due = 100;
        world = GameWorld.Restore(state); Send(world, "r06.common.control", actor, "possess");
        foreach (var resource in new[] { "herb", "wood" })
        {
            Send(world, "r06.common.gather." + resource, actor, "gather", resource);
            world.AdvanceTo(world.Checkpoint().People.Single(p => p.Id == actor).Due);
        }
        state = world.Checkpoint(); state.RandomState = 1; world = GameWorld.Restore(state);
        Send(world, "r06.common.craft.pill", actor, "craft", "pill");
        var jobId = world.Checkpoint().People.Single(p => p.Id == actor).ActionTarget;
        world.AdvanceTo(world.Checkpoint().People.Single(p => p.Id == actor).Due);
        state = world.Checkpoint(); var pill = state.Items.Single(i => i.Owner == actor && i.Definition == "pill");
        var job = state.Inventory.Jobs.Single(j => j.Id == jobId);
        CultivationFixture.Check(job.Status == "completed" && job.Consumed.Sum(i => i.Quantity) == 4 && pill.Quantity == 1,
            "one real common pill comes from actual gathering, paid ingredients, labor and successful production");
        var person = state.People.Single(p => p.Id == actor);
        WorldScenarioFixtures.SetCultivation(state, person, Realm.Qi, 9, true);
        state.Cultivation[person.BodyId] = state.Cultivation[person.BodyId] with { Damage = 500 };
        person.StructureDamage = 5; person.BondStability = 95; person.Health = 50;
        state.RandomState = 1;
        Invariants.Validate(state);
        return (state, actor, pill.Id, job.Id,
            job.Consumed.Select(i => (object)new { definition = i.Definition, quantity = i.Quantity }).ToArray());
    }

    private static long Energy(GameState state, int place) =>
        state.Places[place].Qi * (5000 / state.Laws["cultivation.sourceCost"]) + state.CultivationSupply.GetValueOrDefault(place).Total;

    private static StrategyEvidence RunStrategy(GameState initial, long actor, long pill, string key, string label, bool recover, bool support)
    {
        var world = GameWorld.Restore(initial); var start = world.Month; var initialHash = world.StateHash();
        var opening = world.Checkpoint(); var person = opening.People.Single(p => p.Id == actor);
        var body = person.BodyId; var place = person.Place; var oldFormation = opening.Cultivation[body].Formations.Single();
        var eventFloor = opening.NextId; var phases = new List<PhaseEvidence>();
        if (recover)
        {
            Send(world, "r06." + key + ".recover", actor, "cultivationrecover");
            world.AdvanceTo(start + 1);
            var healed = world.Checkpoint();
            CultivationFixture.Check(healed.Cultivation[body].Damage == 0 && healed.People.Single(p => p.Id == actor).Health == 52 &&
                healed.Cultivation[body].Structure!.Total == opening.Cultivation[body].Structure!.Total,
                "recovery spends a real month/source, improves current injury and never grants acquired growth");
        }
        Send(world, "r06." + key + ".prepare", actor, "breakthrough", "path.2.0", support ? pill : 0);
        var prepared = world.Checkpoint().Cultivation[body].Attempt!;
        phases.Add(new(world.Month - start, prepared.Phase.ToString(), prepared.Due - start));
        GameWorld? restored = null; long savedMonth = -1; var savedHash = ""; var savedBytes = "";
        var directory = Directory.CreateTempSubdirectory("seonro-r06-");
        try
        {
            for (var step = 0; step < 12; step++)
            {
                var current = world.Checkpoint().Cultivation[body].Attempt!;
                if (current.Phase == AttemptPhase.Completed) break;
                CultivationFixture.Check(current.Phase is not (AttemptPhase.Failed or AttemptPhase.Recovering or AttemptPhase.Cancelled),
                    "explicit fixed functional sample successfully exercises this valid strategy");
                world.AdvanceTo(world.Month + 1);
                if (restored is not null)
                {
                    restored.AdvanceTo(world.Month);
                    CultivationFixture.Check(restored.StateHash() == world.StateHash(), "each remaining real boundary matches the file-restored continuation");
                }
                current = world.Checkpoint().Cultivation[body].Attempt!;
                phases.Add(new(world.Month - start, current.Phase.ToString(), current.Due - start));
                if (current.Phase == AttemptPhase.Started && restored is null)
                {
                    var path = Path.Combine(directory.FullName, "strategy.seonro");
                    savedMonth = world.Month - start; savedHash = world.StateHash();
                    GameSaveStore.SaveAtomic(world, path); savedBytes = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
                    restored = GameSaveStore.Load(path);
                    CultivationFixture.Check(restored.StateHash() == savedHash && world.StateHash() == savedHash,
                        "actual file save/readback preserves pending formation without changing world or RNG");
                }
            }
        }
        finally { directory.Delete(true); }
        var final = world.Checkpoint(); person = final.People.Single(p => p.Id == actor);
        var metadata = final.Cultivation[body]; var formation = metadata.Formations[^1];
        CultivationFixture.Check(restored is not null && metadata.Attempt!.Phase == AttemptPhase.Completed &&
            person.Realm == Realm.Foundation && person.Action == ActionKind.Idle && metadata.Formations.Count == 2 &&
            JsonSerializer.Serialize(metadata.Formations[0]) == JsonSerializer.Serialize(oldFormation) &&
            formation.Quality == prepared.FormationQuality && metadata.Structure!.Total == 0,
            "one new actual formation is stabilized; historical quality stays frozen and no extra growth is manufactured");
        CultivationFixture.Check(final.People.Count == opening.People.Count && final.BirthCount == opening.BirthCount &&
            final.DeathCount == opening.DeathCount && final.CreatedMoney == opening.CreatedMoney,
            "short strategy comparison creates no population or currency origin");
        Invariants.Validate(final);
        var events = final.Events.Where(e => e.Id >= eventFloor && e.Actor == actor).ToArray();
        return new(label, initialHash, final.Month - start, Energy(opening, place) - Energy(final, place),
            opening.Places[place].Qi - final.Places[place].Qi,
            final.Materials["pill"].Consumption - opening.Materials["pill"].Consumption,
            final.Items.Where(i => i.Owner == actor && i.Definition == "pill").Sum(i => i.Quantity),
            prepared.Risk, prepared.FormationQuality, formation.Quality, metadata.Damage, person.Health,
            events.Single(e => e.Kind == "돌파 준비").Id, events.Single(e => e.Kind == "형성").Id,
            events.Where(e => e.Kind == "회복").Select(e => e.Id).ToArray(),
            events.Where(e => e.Kind == "안착").Select(e => e.Id).ToArray(),
            prepared.ReservedItems.ToArray(), phases.ToArray(), savedMonth, savedHash, savedBytes, world.StateHash(),
            restored!.StateHash() == world.StateHash());
    }

    public void Run()
    {
        var (initial, actor, pill, pillJob, inputs) = CommonInitialState();
        var direct = RunStrategy(initial, actor, pill, "direct", "바로 형성", false, false);
        var recovered = RunStrategy(initial, actor, pill, "recover", "현재 손상 회복 후 형성", true, false);
        var supported = RunStrategy(initial, actor, pill, "recover-pill", "회복 후 실제 단약을 예약해 형성", true, true);
        var rows = new[] { direct, recovered, supported };
        CultivationFixture.Check(rows.Select(r => r.InitialStateHash).Distinct(StringComparer.Ordinal).Count() == 1,
            "every public strategy starts from the exact same complete state including physical stock and RNG");
        CultivationFixture.Check(direct.LogicalMonths == 5 && recovered.LogicalMonths == 6 && supported.LogicalMonths == 6 &&
            direct.QiEnergyConsumed == 20000 && recovered.QiEnergyConsumed == 25000 && supported.QiEnergyConsumed == 25000,
            "complete strategy duration includes recovery, real formation and all three stabilization months with conserved actual source debits");
        CultivationFixture.Check(recovered.FinalFormationQuality > direct.FinalFormationQuality && supported.FinalFormationQuality == recovered.FinalFormationQuality &&
            direct.PreparedRiskBps > recovered.PreparedRiskBps && recovered.PreparedRiskBps > supported.PreparedRiskBps &&
            direct.PillConsumed == 0 && recovered.PillConsumed == 0 && supported.PillConsumed == 1 &&
            direct.PillRemaining == 1 && recovered.PillRemaining == 1 && supported.PillRemaining == 0 &&
            supported.ReservedPhysicalItems.SequenceEqual([pill]),
            "faster/cheaper work trades away quality/risk; recovery costs time/source; additional lower risk consumes the same actually produced pill");
        var physical = initial.Items.Single(i => i.Id == pill);
        Console.WriteLine("R06_FUNCTIONAL_EVIDENCE " + JsonSerializer.Serialize(new
        {
            schema_version = 1, kind = "seonro-r06-short-functional-strategy-comparison", passed = true,
            version = Chronicles.Core.ProductInfo.Version, execution_claim = "짧은 공개 명령 기능 비교·실행 시간/성능/인구 성장 측정 없음",
            initial_month = initial.Month, actor, body = initial.People.Single(p => p.Id == actor).BodyId,
            declarations = new { realm = "Qi", layer = 9, great_perfection = true, health = 50, current_damage = 500, past_events_fabricated = false },
            laws = ShortLaws, actual_common_pill = new { item = pill, producer_job = pillJob, physical.Quality, physical.Source, consumed_inputs = inputs },
            strategies = rows
        }));
    }
}
