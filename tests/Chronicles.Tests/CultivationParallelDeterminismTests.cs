using Chronicles.Core;
using Chronicles.Persistence;
using Chronicles.Simulation;

internal static class CultivationParallelDeterminismTests
{
    private static readonly ulong[] Seeds =
    [
        0x23_0001UL,
        0x23_0002UL,
        0x23_0003UL,
        0x23_0004UL,
        0x23_0005UL,
    ];

    public static void Run()
    {
        long eligibleCandidates = 0;
        long parallelCandidates = 0;
        long parallelBatches = 0;
        foreach (var seed in Seeds)
        {
            var source = World.Create(seed, 192);
            source.ConfigureCultivationParallelismForTesting(enabled: false);
            source.AdvanceTo(GameCalendar.FromYears(20));
            var checkpoint = source.CreateCheckpoint();
            var serial = World.Restore(checkpoint);
            var parallel = World.Restore(checkpoint);
            serial.ConfigureCultivationParallelismForTesting(enabled: false);
            parallel.ConfigureCultivationParallelismForTesting(
                enabled: true,
                minimumBatchSize: 2);

            var nextMonth = GameCalendar.GetNextMonthStart(serial.Tick);
            serial.AdvanceTo(nextMonth);
            parallel.AdvanceTo(nextMonth);
            AssertEquivalent(serial, parallel, $"seed {seed} 월 경계");

            var century = GameCalendar.FromYears(100);
            serial.AdvanceTo(century);
            parallel.AdvanceTo(century);
            AssertEquivalent(serial, parallel, $"seed {seed} 100년");

            var diagnostics = parallel.GetCultivationParallelDiagnostics();
            eligibleCandidates = checked(
                eligibleCandidates + diagnostics.EligibleCandidateCount);
            parallelCandidates = checked(
                parallelCandidates + diagnostics.ParallelCandidateCount);
            parallelBatches = checked(parallelBatches + diagnostics.ParallelBatchCount);
        }

        TestAssert.True(eligibleCandidates > 0,
            "장기 수행 active plan이 순수 계산 후보로 실제 수집됨");
        if (Environment.ProcessorCount > 1)
        {
            TestAssert.True(parallelBatches > 0 && parallelCandidates > 0,
                "다중 코어 환경에서 수행 Work가 실제 병렬 경로를 통과함");
        }

        AssertMillenniumDeterminism();
    }

    private static void AssertMillenniumDeterminism()
    {
        var checkpoint = World.Create(0x23_FFFFUL, 24).CreateCheckpoint();
        var serial = World.Restore(checkpoint);
        var parallel = World.Restore(checkpoint);
        serial.ConfigureCultivationParallelismForTesting(enabled: false);
        parallel.ConfigureCultivationParallelismForTesting(
            enabled: true,
            minimumBatchSize: 2);
        var millennium = GameCalendar.FromYears(1_000);
        serial.AdvanceTo(millennium);
        parallel.AdvanceTo(millennium);
        AssertEquivalent(serial, parallel, "1,000년 소형 세계");
    }

    private static void AssertEquivalent(World expected, World actual, string scope)
    {
        var expectedCheckpoint = expected.CreateCheckpoint();
        var actualCheckpoint = actual.CreateCheckpoint();
        if (expected.ComputeStateHash() != actual.ComputeStateHash())
        {
            for (var index = 0; index < expectedCheckpoint.Agents.Length; index++)
            {
                var left = expectedCheckpoint.Agents[index];
                var right = actualCheckpoint.Agents[index];
                var leftCultivation = left.Cultivation!.Value;
                var rightCultivation = right.Cultivation!.Value;
                if (left.CultivationProgress != right.CultivationProgress
                    || left.CultivationState != right.CultivationState
                    || leftCultivation.PendingCarryoverWork != rightCultivation.PendingCarryoverWork
                    || leftCultivation.LastWorkBefore != rightCultivation.LastWorkBefore
                    || leftCultivation.LastWorkGain != rightCultivation.LastWorkGain
                    || leftCultivation.LatestTrace != rightCultivation.LatestTrace
                    || leftCultivation.AuthorityRevision != rightCultivation.AuthorityRevision
                    || !leftCultivation.ComponentValues.SequenceEqual(rightCultivation.ComponentValues)
                    || !leftCultivation.ComponentLimits.SequenceEqual(rightCultivation.ComponentLimits)
                    || leftCultivation.ActionPlan != rightCultivation.ActionPlan
                    || left.NextDecisionTick != right.NextDecisionTick
                    || left.ActionsTaken != right.ActionsTaken)
                {
                    throw new InvalidOperationException(
                        $"{scope} agent {index} differs: " +
                        $"progress {left.CultivationProgress}/{right.CultivationProgress}, " +
                        $"state {left.CultivationState}/{right.CultivationState}, " +
                        $"next {left.NextDecisionTick}/{right.NextDecisionTick}, " +
                        $"actions {left.ActionsTaken}/{right.ActionsTaken}, " +
                        $"cultivation {left.Cultivation}/{right.Cultivation}");
                }
            }
        }
        TestAssert.Equal(expected.ComputeStateHash(), actual.ComputeStateHash(),
            $"{scope} 상태 hash");
        TestAssert.Equal(expectedCheckpoint.RandomState, actualCheckpoint.RandomState,
            $"{scope} RNG state");
        TestAssert.Equal(expectedCheckpoint.EventSequence, actualCheckpoint.EventSequence,
            $"{scope} 사건 sequence");
        TestAssert.Equal(expectedCheckpoint.ChronicleSequence, actualCheckpoint.ChronicleSequence,
            $"{scope} 연대기 sequence");
        TestAssert.True(
            WorldSaveCodec.Encode(expectedCheckpoint)
                .SequenceEqual(WorldSaveCodec.Encode(actualCheckpoint)),
            $"{scope} checkpoint wire");
    }
}
