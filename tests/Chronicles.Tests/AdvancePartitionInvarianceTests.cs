using Chronicles.Core;
using Chronicles.Persistence;
using Chronicles.Runtime;
using Chronicles.Simulation;

internal static class AdvancePartitionInvarianceTests
{
    private const ulong Seed = 0xD15EA5EUL;
    private const int Population = 96;
    private static readonly SimulationTick Target = GameCalendar.FromYears(4);

    public static void Run()
    {
        AssertIndexedDecisionSchedule();
        AssertCausalRankHasNoSecondOneBasedOffset();
        AssertSchema30MigrationAndSchema31Validation();
        var baseline = AdvanceSingleCall();

        AssertEquivalent(baseline, AdvanceByCalendarMonth(), "월 경계 분할");
        AssertEquivalent(baseline, AdvanceByIrregularChunks(), "무작위 크기 분할");

        foreach (var speed in RuntimeSpeedRules.Supported)
        {
            AssertEquivalent(
                baseline,
                AdvanceByRuntimePulse(speed),
                $"80ms runtime pulse {speed}x 분할");
        }

        AssertEquivalent(baseline, AdvanceAcrossCheckpoint(), "중간 checkpoint 복원 분할");
    }

    private static void AssertIndexedDecisionSchedule()
    {
        var schedule = new AgentDecisionSchedule(6);
        schedule.Schedule(4, 30);
        schedule.Schedule(1, 20);
        schedule.Schedule(3, 20);
        schedule.Schedule(0, 10);
        schedule.Schedule(5, 40);
        schedule.Schedule(2, 50);
        schedule.Schedule(2, 5);
        schedule.Schedule(4, 60);
        TestAssert.True(schedule.Remove(3), "대기 일정 제거");
        TestAssert.True(!schedule.Remove(3), "이미 제거된 일정 중복 제거 거부");

        var order = new List<(int Agent, long Tick)>();
        while (schedule.TryPeek(out var agent, out var tick))
        {
            TestAssert.Equal(agent, schedule.Dequeue(), "peek/dequeue 인물 일치");
            order.Add((agent, tick));
        }

        TestAssert.True(order.SequenceEqual(new[]
        {
            (Agent: 2, Tick: 5L),
            (Agent: 0, Tick: 10L),
            (Agent: 1, Tick: 20L),
            (Agent: 5, Tick: 40L),
            (Agent: 4, Tick: 60L),
        }), "indexed 일정 변경·제거·동점 순서");
    }

    private static void AssertSchema30MigrationAndSchema31Validation()
    {
        var current = World.Create(Seed, 8).CreateCheckpoint();
        var legacy = current with
        {
            SchemaVersion = 30,
            DecisionRankings = null,
        };
        var migrated = World.Restore(legacy).CreateCheckpoint();
        TestAssert.Equal(WorldCheckpoint.CurrentSchemaVersion, migrated.SchemaVersion,
            "schema 30은 재저장 시 월 경계 랭킹 schema 31로 승격");
        TestAssert.True(migrated.DecisionRankings is not null,
            "schema 30 이전은 결정 랭킹 스냅샷을 재구성");

        TestAssert.Throws<InvalidDataException>(
            () => World.Restore(current with { DecisionRankings = null }),
            "schema 31 결정 랭킹 누락 거부");
        var duplicateRanks = current.DecisionRankings!.CombatRanksByAgent.ToArray();
        duplicateRanks[0] = duplicateRanks[1];
        TestAssert.Throws<InvalidDataException>(
            () => World.Restore(current with
            {
                DecisionRankings = current.DecisionRankings with
                {
                    CombatRanksByAgent = duplicateRanks,
                },
            }),
            "schema 31 중복 inverse rank 거부");
    }

    private static void AssertCausalRankHasNoSecondOneBasedOffset()
    {
        var seedWorld = World.Create(Seed, 1);
        var checkpoint = seedWorld.CreateCheckpoint();
        var agent = checkpoint.Agents[0] with
        {
            Activity = AgentActivity.TrainingTechnique,
            NextDecisionTick = 1,
        };
        var world = World.Restore(checkpoint with { Agents = [agent] });
        world.AdvanceTo(new SimulationTick(1));

        var snapshot = world.GetAgentSnapshot(new AgentId(0), includeCausal: true);
        var observedRank = snapshot.Causal!.PerceivedFacts.Single(
            fact => fact.Kind == PerceivedFactKind.ObservedCombatRank);
        TestAssert.Equal(snapshot.Rank, checked((int)observedRank.NumericValue),
            "인과 관찰 랭킹은 이미 1-based인 inverse rank를 다시 +1하지 않음");
    }

    private static World AdvanceSingleCall()
    {
        var world = World.Create(Seed, Population);
        world.AdvanceTo(Target);
        return world;
    }

    private static World AdvanceByCalendarMonth()
    {
        var world = World.Create(Seed, Population);
        var targetMonth = GameCalendar.GetMonthIndex(Target);
        for (var month = 1L; month <= targetMonth; month++)
        {
            world.AdvanceTo(GameCalendar.GetMonthStart(month));
        }
        if (world.Tick.Value < Target.Value)
        {
            world.AdvanceTo(Target);
        }
        return world;
    }

    private static World AdvanceByIrregularChunks()
    {
        ReadOnlySpan<int> chunkPattern = [1, 17, 163, 41, 333, 2, 89, 701, 29, 257];
        var world = World.Create(Seed, Population);
        var patternIndex = 0;
        while (world.Tick.Value < Target.Value)
        {
            var next = Math.Min(
                Target.Value,
                checked(world.Tick.Value + chunkPattern[patternIndex % chunkPattern.Length]));
            world.AdvanceTo(new SimulationTick(next));
            patternIndex++;
        }
        return world;
    }

    private static World AdvanceByRuntimePulse(int speed)
    {
        var world = World.Create(Seed, Population);
        var ticksPerPulse = SimulationPacing.GetTicksPerPulse(speed);
        while (world.Tick.Value < Target.Value)
        {
            world.AdvanceTo(new SimulationTick(Math.Min(
                Target.Value,
                checked(world.Tick.Value + ticksPerPulse))));
        }
        return world;
    }

    private static World AdvanceAcrossCheckpoint()
    {
        var world = World.Create(Seed, Population);
        world.AdvanceTo(new SimulationTick(2_633));
        world = World.Restore(WorldSaveCodec.Decode(
            WorldSaveCodec.Encode(world.CreateCheckpoint())));

        ReadOnlySpan<int> chunkPattern = [53, 5, 811, 97, 1, 389];
        var patternIndex = 0;
        while (world.Tick.Value < Target.Value)
        {
            var next = Math.Min(
                Target.Value,
                checked(world.Tick.Value + chunkPattern[patternIndex % chunkPattern.Length]));
            world.AdvanceTo(new SimulationTick(next));
            patternIndex++;
        }
        return world;
    }

    private static void AssertEquivalent(World expected, World actual, string path)
    {
        var expectedCheckpoint = expected.CreateCheckpoint();
        var actualCheckpoint = actual.CreateCheckpoint();
        TestAssert.Equal(expected.ComputeStateHash(), actual.ComputeStateHash(), $"{path} 상태 hash");
        TestAssert.Equal(expectedCheckpoint.RandomState, actualCheckpoint.RandomState, $"{path} RNG state");
        TestAssert.Equal(expectedCheckpoint.EventSequence, actualCheckpoint.EventSequence, $"{path} 사건 sequence");
        TestAssert.Equal(expectedCheckpoint.ChronicleSequence, actualCheckpoint.ChronicleSequence, $"{path} 연대기 sequence");
        TestAssert.True(expectedCheckpoint.Events.SequenceEqual(actualCheckpoint.Events), $"{path} 세계 사건");
        TestAssert.True(
            WorldSaveCodec.Encode(expectedCheckpoint).SequenceEqual(WorldSaveCodec.Encode(actualCheckpoint)),
            $"{path} checkpoint·세계 연대기 wire");

        var sections = ObservationSections.AllRankings;
        var expectedSnapshot = expected.CreateSnapshot(Population, 0, sections);
        var actualSnapshot = actual.CreateSnapshot(Population, 0, sections);
        AssertRanking(expectedSnapshot.CombatPowerRankings, actualSnapshot.CombatPowerRankings, path, "전투력");
        AssertRanking(expectedSnapshot.RealmRankings, actualSnapshot.RealmRankings, path, "경지");
        AssertRanking(expectedSnapshot.AptitudeRankings, actualSnapshot.AptitudeRankings, path, "자질");
        AssertRanking(expectedSnapshot.WealthRankings, actualSnapshot.WealthRankings, path, "부자");
    }

    private static void AssertRanking(
        IReadOnlyList<RankingEntry> expected,
        IReadOnlyList<RankingEntry> actual,
        string path,
        string category)
    {
        TestAssert.True(
            expected.Select(entry => entry.AgentId).SequenceEqual(actual.Select(entry => entry.AgentId)),
            $"{path} {category} 랭킹");
    }
}
