using Chronicles.Application;
using Chronicles.Core;
using Chronicles.Persistence;
using Chronicles.Queries;
using Chronicles.Simulation;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using static TestAssert;

internal static class WorldPersistenceTests
{
    public static void DefaultPopulationAndGoal()
    {
        var world = World.CreateDefault(42);
        Equal(10_000, world.Population, "기본 인구");
        var initialSnapshot = world.CreateSnapshot(1, 0);
        True(!initialSnapshot.RealmRankings[0].HasEnteredCultivation, "0세 경지 랭킹은 미입문 표시");
        Equal(10_000, initialSnapshot.Statistics.RealmDistribution[0].Count, "0세 미입문 분포");
        Equal(0, initialSnapshot.Statistics.RealmDistribution[1].Count, "0세 연기경 분포");
        for (var index = 0; index < world.Population; index++)
        {
            Equal(1, world.GetAgentSnapshot(new AgentId(index)).TargetRank, "모든 AI의 목표 순위");
            Equal(0L, world.GetAgentSnapshot(new AgentId(index)).AgeYears, "모든 AI의 시작 나이");
        }
    }

    public static void RankingRevisionCacheAvoidsUnchangedSorts()
    {
        var scores = new[]
        {
            12L,
            long.MaxValue,
            12L,
            -4L,
            long.MinValue,
            999L,
            -4L,
        };
        var radixOrder = new int[scores.Length];
        var radixScratch = new int[scores.Length];
        RankingRadixSorter.SortDescending(radixOrder, scores, radixScratch);
        var comparisonOrder = Enumerable.Range(0, scores.Length)
            .OrderByDescending(agent => scores[agent])
            .ThenBy(agent => agent)
            .ToArray();
        EqualSequence(comparisonOrder, radixOrder, "64비트 랭킹 radix 순서·동점 AgentId 계약");
        Equal(6, RankingRadixSorter.GetRequiredPassCount(scores), "signed 랭킹은 전체 64-bit 통과");

        var nonNegativeScores = new[] { 0L, 1L, 12L, 2_047L, 2_048L, 50_000L, 50_000L };
        RankingRadixSorter.SortDescending(radixOrder, nonNegativeScores, radixScratch);
        comparisonOrder = Enumerable.Range(0, nonNegativeScores.Length)
            .OrderByDescending(agent => nonNegativeScores[agent])
            .ThenBy(agent => agent)
            .ToArray();
        EqualSequence(comparisonOrder, radixOrder, "비음수 가변 pass radix 순서·동점 AgentId 계약");
        Equal(2, RankingRadixSorter.GetRequiredPassCount(nonNegativeScores),
            "5만 이하 점수는 2-pass");

        const int population = 512;
        var world = World.Create(0xCACE_0014UL, population);
        var initial = world.GetRankingCacheDiagnostics();

        Equal(1L, initial.CombatPower.SortCount, "초기 전투력 정렬 횟수");
        Equal(1L, initial.Realm.SortCount, "초기 경지 정렬 횟수");
        Equal(1L, initial.Aptitude.SortCount, "초기 자질 정렬 횟수");
        Equal(1L, initial.Wealth.SortCount, "초기 부자 정렬 횟수");
        True(!initial.CombatPower.IsDirty, "초기 전투력 cache 동기화");
        True(!initial.Realm.IsDirty, "초기 경지 cache 동기화");
        True(!initial.Aptitude.IsDirty, "초기 자질 cache 동기화");
        True(!initial.Wealth.IsDirty, "초기 부자 cache 동기화");

        for (var pulse = 0; pulse < 100; pulse++)
        {
            world.AdvanceTo(world.Tick);
        }

        var afterIdlePulses = world.GetRankingCacheDiagnostics();
        Equal(initial.TotalSortCount, afterIdlePulses.TotalSortCount, "무변경 pulse 정렬 0회");
        Equal(initial.CombatPower.DataRevision, afterIdlePulses.CombatPower.DataRevision, "무변경 전투력 revision");
        Equal(initial.Realm.DataRevision, afterIdlePulses.Realm.DataRevision, "무변경 경지 revision");
        Equal(initial.Aptitude.DataRevision, afterIdlePulses.Aptitude.DataRevision, "무변경 자질 revision");
        Equal(initial.Wealth.DataRevision, afterIdlePulses.Wealth.DataRevision, "무변경 부자 revision");

        world.AdvanceTo(new SimulationTick(10L * GameCalendar.TicksPerYear));
        var afterTenYears = world.GetRankingCacheDiagnostics();
        var elapsedMonthBoundaries = GameCalendar.GetMonthIndex(world.Tick);
        AssertMonthlySortBound(initial.CombatPower, afterTenYears.CombatPower, elapsedMonthBoundaries, "전투력");
        AssertMonthlySortBound(initial.Realm, afterTenYears.Realm, elapsedMonthBoundaries, "경지");
        AssertMonthlySortBound(initial.Aptitude, afterTenYears.Aptitude, elapsedMonthBoundaries, "자질");
        AssertMonthlySortBound(initial.Wealth, afterTenYears.Wealth, elapsedMonthBoundaries, "부자");
        Equal(
            world.TotalReincarnations > 0,
            initial.Aptitude.DataRevision != afterTenYears.Aptitude.DataRevision,
            "자질 revision은 환생으로만 변경");
        // 새 월의 환경·행동은 다음 월 의사결정 스냅샷을 dirty로 만들 수 있다. 관찰은 이를
        // 즉시 정렬하지 않고, 다음 결정론적 월 경계에서 범주별 한 번만 확정해야 한다.

        var hashBeforeObservation = world.ComputeStateHash();
        var snapshot = world.CreateSnapshot(population, 0);
        var decisionRankings = world.CreateCheckpoint().DecisionRankings!;
        AssertDecisionRankingOrder(snapshot.CombatPowerRankings, decisionRankings.CombatRanksByAgent, "전투력");
        AssertDecisionRankingOrder(snapshot.RealmRankings, decisionRankings.RealmRanksByAgent, "경지");
        AssertDecisionRankingOrder(snapshot.AptitudeRankings, decisionRankings.AptitudeRanksByAgent, "자질");
        AssertDecisionRankingOrder(snapshot.WealthRankings, decisionRankings.WealthRanksByAgent, "부자");
        Equal(hashBeforeObservation, world.ComputeStateHash(), "랭킹 관찰은 권위 상태 불변");
        Equal(
            afterTenYears.TotalSortCount,
            world.GetRankingCacheDiagnostics().TotalSortCount,
            "랭킹 관찰 중 정렬 0회");

        var checkpoint = world.CreateCheckpoint();
        Equal(WorldCheckpoint.CurrentSchemaVersion, checkpoint.SchemaVersion, "현재 인과·생애·조직 권위 schema");
        var restored = World.Restore(checkpoint);
        Equal(world.ComputeStateHash(), restored.ComputeStateHash(), "랭킹 cache 저장 제외·복원 상태 hash");
        var restoredSnapshot = restored.CreateSnapshot(population, 0);
        EqualSequence(
            snapshot.CombatPowerRankings.Select(entry => entry.AgentId.Value).ToArray(),
            restoredSnapshot.CombatPowerRankings.Select(entry => entry.AgentId.Value).ToArray(),
            "복원 전투력 랭킹");
        EqualSequence(
            snapshot.RealmRankings.Select(entry => entry.AgentId.Value).ToArray(),
            restoredSnapshot.RealmRankings.Select(entry => entry.AgentId.Value).ToArray(),
            "복원 경지 랭킹");
        EqualSequence(
            snapshot.AptitudeRankings.Select(entry => entry.AgentId.Value).ToArray(),
            restoredSnapshot.AptitudeRankings.Select(entry => entry.AgentId.Value).ToArray(),
            "복원 자질 랭킹");
        EqualSequence(
            snapshot.WealthRankings.Select(entry => entry.AgentId.Value).ToArray(),
            restoredSnapshot.WealthRankings.Select(entry => entry.AgentId.Value).ToArray(),
            "복원 부자 랭킹");
        var restoredInitial = restored.GetRankingCacheDiagnostics();
        Equal(4L, restoredInitial.TotalSortCount, "복원 파생 랭킹 범주별 1회 재구축");
        restored.AdvanceTo(restored.Tick);
        Equal(
            restoredInitial.TotalSortCount,
            restored.GetRankingCacheDiagnostics().TotalSortCount,
            "복원 직후 무변경 pulse 정렬 0회");

    }

    public static void DeterministicWorld()
    {
        var left = World.CreateDefault(20260830);
        var right = World.CreateDefault(20260830);
        Equal(left.ComputeStateHash(), right.ComputeStateHash(), "초기 상태 해시");
        left.AdvanceTo(new SimulationTick(500));
        right.AdvanceTo(new SimulationTick(500));
        Equal(left.ComputeStateHash(), right.ComputeStateHash(), "진행 상태 해시");

        var differentSeed = World.CreateDefault(20260831);
        True(left.ComputeStateHash() != differentSeed.ComputeStateHash(), "다른 seed는 다른 세계를 만들어야 합니다.");
    }

    public static void EventDrivenGrowth()
    {
        var world = World.Create(11, 1_000);
        var before = world.ComputeStateHash();
        world.AdvanceTo(new SimulationTick(220_000));
        True(before != world.ComputeStateHash(), "시간 진행 후 상태가 바뀌어야 합니다.");
        True(world.TotalBreakthroughs > 0, "장기 수행에서 돌파가 발생해야 합니다.");
        Equal(220_000L, world.Tick.Value, "도달 tick");
    }

    public static void LongRunCultivationHuntingAndCombat()
    {
        var world = World.Create(110011, 1_000);
        world.AdvanceTo(GameCalendar.FromYears(180));
        var entryBlocked = 0;
        var higherRealm = 0;
        var hunters = 0;
        var combatants = 0;
        var combatLearners = 0;
        var overLifespan = 0;
        for (var index = 0; index < world.Population; index++)
        {
            var agent = world.GetAgentSnapshot(new AgentId(index));
            if (agent.CultivationState is CultivationState.PreCultivation or CultivationState.EntryTraining
                || (agent.CultivationState == CultivationState.Bottleneck
                    && agent.BottleneckCause == CultivationBottleneckCause.EntryGate))
            {
                entryBlocked++;
            }

            if (agent.RealmStage > RealmStage.QiRefining)
            {
                higherRealm++;
            }

            if (agent.MonsterHunts > 0)
            {
                hunters++;
            }

            if (agent.CombatExperience > 0)
            {
                combatants++;
            }

            if (agent.CombatMastery > 0 && agent.CombatUnderstanding > 0)
            {
                combatLearners++;
            }

            if (agent.AgeYears > agent.MaximumLifespanYears)
            {
                overLifespan++;
            }
        }

        Console.WriteLine(
            $"SOAK: 입문 정체 {entryBlocked:N0} · 축기 이상 {higherRealm:N0} · 사냥 참여 {hunters:N0} · 전투 경험 {combatants:N0} · 사냥 {world.TotalMonsterHunts:N0} · 전투 {world.TotalCombatsCompleted:N0} · 사망 {world.TotalDeaths:N0}");
        True(entryBlocked < world.Population / 2, "장기 세계의 과반이 입문 Gate에 영구 정체되면 안 됩니다.");
        True(higherRealm > 0, "장기 수행에서 축기경 이상 수선자가 나와야 합니다.");
        True(hunters >= world.Population / 5, "요수 사냥 경험이 전체 인구에 의미 있게 퍼져야 합니다.");
        True(combatants >= world.Population / 2, "수선자 간 전투 경험이 전체 인구에 퍼져야 합니다.");
        True(combatLearners >= world.Population / 2, "전투 숙련도와 이해도가 전체 인구에 퍼져야 합니다.");
        True(world.TotalMonsterHunts > 0, "장기 세계에서 요수 사냥이 발생해야 합니다.");
        True(world.TotalCombatsCompleted > 0, "장기 세계에서 수선자 전투가 완료되어야 합니다.");
        Equal(0, overLifespan, "현재 수명을 넘긴 생존자가 관찰되면 안 됩니다.");
    }

    public static void ActiveRankingCompetition()
    {
        var topDuel = World.Create(773, 2);
        topDuel.AdvanceTo(GameCalendar.FromYears(30));
        True(topDuel.TotalChallenges > 0, "2위도 1위에게 도전할 수 있어야 합니다.");
        True(topDuel.TotalCombatsStarted > 0, "1·2위만 있는 세계에서도 실제 전투가 시작되어야 합니다.");

        var world = World.CreateDefault(774);
        world.AdvanceTo(new SimulationTick(20_000));
        True(world.TotalChallenges >= 1_000, "충분한 상위권 도전이 발생해야 합니다.");
        True(world.TotalCombatsStarted >= 500, "실제 전투가 지속해서 시작되어야 합니다.");
        True(world.TotalCombatsCompleted >= 400, "실제 전투 결과가 누적되어야 합니다.");
        Equal(world.TotalCombatsStarted, world.TotalCombatsCompleted + world.ActiveCombats, "전투 수 보존");
        True(world.ActiveCombats <= world.Population / 2, "한 AI는 한 전투에만 참여해야 합니다.");

        var totalWins = 0;
        var totalLosses = 0;
        var experiencedAgents = 0;
        for (var index = 0; index < world.Population; index++)
        {
            var agent = world.GetAgentSnapshot(new AgentId(index));
            totalWins += agent.Wins;
            totalLosses += agent.Losses;
            if (agent.CombatExperience > 0)
            {
                experiencedAgents++;
                True(agent.CombatPower.Experience > 0, "전투 경험은 전투력에 반영되어야 합니다.");
            }
        }

        Equal(world.TotalCombatsCompleted, totalWins, "완료 전투와 승리 합계");
        Equal(world.TotalCombatsCompleted, totalLosses, "완료 전투와 패배 합계");
        True(experiencedAgents >= 500, "전투 경험을 얻은 AI가 전 인구에 퍼져야 합니다.");

        var events = world.CreateSnapshot(20, 512).RecentEvents;
        True(events.Any(item => item.Kind == WorldEventKind.CombatEnded), "관찰 사건에 전투 결과가 있어야 합니다.");
    }

    public static void WorldStatisticsQuery()
    {
        var world = World.CreateDefault(808);
        world.AdvanceTo(new SimulationTick(2_000));
        var before = world.ComputeStateHash();
        var statistics = world.CreateStatistics();

        Equal(11, statistics.RealmDistribution.Count, "미입문 포함 경지 분포 구간 수");
        Equal(world.Population, statistics.RealmDistribution.Sum(item => item.Count), "경지 분포 전체 인구");
        Equal(world.Population, statistics.CombatPowerDistribution.Sum(item => item.Count), "전투력 분포 전체 인구");
        Equal((long)world.TotalCombatsCompleted, statistics.TotalWins, "통계 승리 수");
        Equal((long)world.TotalCombatsCompleted, statistics.TotalLosses, "통계 패배 수");
        Equal(world.TotalDeaths, statistics.TotalDeaths, "통계 사망 수");
        Equal(
            world.Population,
            statistics.RankingMovement.RisenCount
                + statistics.RankingMovement.FallenCount
                + statistics.RankingMovement.UnchangedCount,
            "랭킹 변동 인구 보존");
        Equal(before, world.ComputeStateHash(), "통계 조회는 세계 상태를 바꾸면 안 됩니다.");
    }

    public static void SaveCodecRoundTrip()
    {
        var original = World.CreateDefault(314159);
        original.AdvanceTo(new SimulationTick(2_500));
        var checkpoint = original.CreateCheckpoint();
        var encoded = WorldSaveCodec.Encode(checkpoint);
        var decoded = WorldSaveCodec.Decode(encoded);
        True(
            WorldSaveCodec.StreamingJsonSizeLimitBytes > 512L * 1024 * 1024,
            "장기 세계 슬롯 저장은 기존 512 MiB JSON 상한을 초과해 지원");
        Equal(checkpoint.RandomState, decoded.RandomState, "저장 왕복 RNG 상태");
        Equal(checkpoint.EventSequence, decoded.EventSequence, "저장 왕복 사건 sequence");
        Equal(checkpoint.PersonalLogSequence, decoded.PersonalLogSequence, "저장 왕복 개인 로그 sequence");
        Equal(checkpoint.TotalChallenges, decoded.TotalChallenges, "저장 왕복 도전 합계");
        Equal(checkpoint.TotalMissionsCompleted, decoded.TotalMissionsCompleted, "저장 왕복 임무 합계");
        Equal(checkpoint.LastRankingRefreshTick, decoded.LastRankingRefreshTick, "저장 왕복 랭킹 갱신 시점");
        Equal(checkpoint.RankingRisenCount, decoded.RankingRisenCount, "저장 왕복 순위 상승 집계");
        Equal(checkpoint.Market.TreasurySpiritStones, decoded.Market.TreasurySpiritStones, "저장 왕복 도시 영석");
        Equal(checkpoint.Market.PurchaseTransactions, decoded.Market.PurchaseTransactions, "저장 왕복 도시 구매 거래");
        Equal(checkpoint.Market.MaterialsProcessed, decoded.Market.MaterialsProcessed, "저장 왕복 도시 가공 누계");
        True(checkpoint.Market.Stock.SequenceEqual(decoded.Market.Stock), "저장 왕복 시장 재고");
        True(checkpoint.Market.Prices.SequenceEqual(decoded.Market.Prices), "저장 왕복 시장 가격");
        Equal(checkpoint.Agents[0].Name, decoded.Agents[0].Name, "저장 왕복 첫 Agent 이름");
        Equal(checkpoint.Agents[0].Soul, decoded.Agents[0].Soul, "저장 왕복 첫 Agent 영혼");
        True(
            checkpoint.Agents[0].CombatProgress == decoded.Agents[0].CombatProgress,
            "저장 왕복 첫 Agent 전투 성장");
        True(
            checkpoint.Agents[0].MaterialInventory.SequenceEqual(decoded.Agents[0].MaterialInventory),
            "저장 왕복 첫 Agent 재료");
        True(
            checkpoint.Agents[0].EquippedTreasureQualities.SequenceEqual(decoded.Agents[0].EquippedTreasureQualities),
            "저장 왕복 첫 Agent 법보");
        True(
            checkpoint.Agents[0].Chronicle.SequenceEqual(decoded.Agents[0].Chronicle),
            "저장 왕복 첫 Agent 일대기");
        True(
            checkpoint.Agents[0].PersonalLog.SequenceEqual(decoded.Agents[0].PersonalLog),
            "저장 왕복 첫 Agent 개인 로그");
        Equal(checkpoint.Agents[^1].Name, decoded.Agents[^1].Name, "저장 왕복 마지막 Agent 이름");
        True(
            checkpoint.Agents[^1].Chronicle.SequenceEqual(decoded.Agents[^1].Chronicle),
            "저장 왕복 마지막 Agent 일대기");
        Equal(checkpoint.Events.Length, decoded.Events.Length, "저장 왕복 사건 수");
        Equal(checkpoint.WorldChronicle?.Length ?? 0, decoded.WorldChronicle?.Length ?? 0, "저장 왕복 구 세계 연대기 수");
        Equal(
            checkpoint.CenturyChronicle?.Entries.Length ?? 0,
            decoded.CenturyChronicle?.Entries.Length ?? 0,
            "저장 왕복 100년 세계 역사서 수");
        Equal(
            checkpoint.CenturyChronicle?.Current.CenturyIndex ?? -1,
            decoded.CenturyChronicle?.Current.CenturyIndex ?? -1,
            "저장 왕복 현재 세계 역사서 세기");
        Equal(checkpoint.RankingHistory?.Frames.Length ?? 0, decoded.RankingHistory?.Frames.Length ?? 0, "저장 왕복 랭킹 이력 수");
        if (checkpoint.Events.Length > 0)
        {
            Equal(checkpoint.Events[0], decoded.Events[0], "저장 왕복 첫 사건");
            Equal(checkpoint.Events[^1], decoded.Events[^1], "저장 왕복 마지막 사건");
        }

        var restored = World.Restore(decoded);
        Equal(original.ComputeStateHash(), restored.ComputeStateHash(), "저장 왕복 상태 해시");
        Equal(original.TotalCombatsCompleted, restored.TotalCombatsCompleted, "저장 왕복 완료 전투");
        Equal(original.ActiveCombats, restored.ActiveCombats, "저장 왕복 진행 중 전투");

        original.AdvanceTo(new SimulationTick(3_000));
        restored.AdvanceTo(new SimulationTick(3_000));
        Equal(original.ComputeStateHash(), restored.ComputeStateHash(), "복원 후 계속 진행 상태 해시");
        True(encoded.Length < 8_000_000, "10,000명 저장 파일은 합리적인 크기여야 합니다.");
    }

    public static void CorruptedSaveIsRejected()
    {
        var world = World.Create(2718, 250);
        world.AdvanceTo(new SimulationTick(200));
        var encoded = WorldSaveCodec.Encode(world.CreateCheckpoint());
        encoded[^1] ^= 0x5A;
        Throws<InvalidDataException>(() => WorldSaveCodec.Decode(encoded), "checksum 손상 저장");
    }

    public static void AtomicSaveFileRoundTrip()
    {
        var root = Path.Combine(Path.GetTempPath(), "ChroniclesTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "world.cmip");
        try
        {
            var world = World.Create(1618, 500);
            world.AdvanceTo(new SimulationTick(400));
            WorldSaveStore.Save(path, world.CreateCheckpoint());
            True(File.Exists(path), "원자 저장 결과 파일");
            True(!File.Exists(path + ".tmp"), "임시 저장 파일이 남으면 안 됩니다.");
            var restored = World.Restore(WorldSaveStore.Load(path));
            Equal(world.ComputeStateHash(), restored.ComputeStateHash(), "원자 저장 파일 상태 해시");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    public static void BoundedLogFilesNeverExceedFiveMiB()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"cmip-bounded-log-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var lines = Enumerable.Range(0, 2_400)
                .Select(index => $"{index:D4}|{new string('가', 2_200)}")
                .ToArray();
            var export = BoundedLogFileWriter.WriteNewSeries(directory, "manual", lines);
            True(export.FileCount >= 2, "5MiB 초과 수동 로그 분할");
            var exportPaths = Directory.GetFiles(directory, "manual-*.log");
            Equal(export.FileCount, exportPaths.Length, "수동 로그 결과 파일 수");
            True(
                exportPaths.All(path => new FileInfo(path).Length
                    <= BoundedLogFileWriter.MaximumFileBytes),
                "수동 로그 파일별 5MiB 상한");
            Equal(
                lines.Length,
                exportPaths.Sum(path => File.ReadLines(path).Count()),
                "수동 로그 행 보존");

            var firstBatch = lines[..1_200];
            var secondBatch = lines[1_200..];
            _ = BoundedLogFileWriter.AppendSeries(directory, "automatic", firstBatch);
            var automatic = BoundedLogFileWriter.AppendSeries(directory, "automatic", secondBatch);
            True(automatic.FileCount >= 1, "자동 로그 append 결과");
            var automaticPaths = Directory.GetFiles(directory, "automatic-*.log");
            True(automaticPaths.Length >= 2, "자동 로그 용량 회전");
            True(
                automaticPaths.All(path => new FileInfo(path).Length
                    <= BoundedLogFileWriter.MaximumFileBytes),
                "자동 로그 파일별 5MiB 상한");
            Equal(
                lines.Length,
                automaticPaths.Sum(path => File.ReadLines(path).Count()),
                "자동 로그 행 보존");

            using (var first = File.Create(Path.Combine(directory, "resume-001.log")))
            {
                first.SetLength(BoundedLogFileWriter.MaximumFileBytes - 50);
            }
            using (var second = File.Create(Path.Combine(directory, "resume-002.log")))
            {
                second.SetLength(BoundedLogFileWriter.MaximumFileBytes - 40);
            }

            _ = BoundedLogFileWriter.AppendSeries(
                directory,
                "resume",
                [new string('x', 80)]);
            var resumedPaths = Directory.GetFiles(directory, "resume-*.log");
            True(resumedPaths.Length == 3, "기존의 부분 사용된 다음 part도 잔여 용량을 재검사");
            True(
                resumedPaths.All(path => new FileInfo(path).Length
                    <= BoundedLogFileWriter.MaximumFileBytes),
                "재시작 append 뒤에도 파일별 5MiB 상한");

            var oversized = new string('한', checked((int)(
                BoundedLogFileWriter.MaximumFileBytes / 3 + 1_000)));
            var oversizedExport = BoundedLogFileWriter.WriteNewSeries(
                directory,
                "oversized-utf8",
                [oversized]);
            True(oversizedExport.FileCount >= 2, "단일 UTF-8 기록도 rune 경계에서 분할");
            var oversizedPaths = Directory.GetFiles(directory, "oversized-utf8-*.log");
            True(
                oversizedPaths.All(path => new FileInfo(path).Length
                    <= BoundedLogFileWriter.MaximumFileBytes),
                "단일 UTF-8 기록 파일별 5MiB 상한");
            True(
                File.ReadLines(oversizedPaths[0]).First().StartsWith("[분할 기록 1] ", StringComparison.Ordinal),
                "분할 기록 연속 표시");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    public static void LogOutputDirectoryContract()
    {
        var worldAutomatic = LogOutputDirectories.GetDirectory(new LogOutputTarget(
            LogOutputKind.World,
            LogOutputMode.Automatic));
        True(
            worldAutomatic.EndsWith(
                Path.Combine("logs", "world", "automatic"),
                StringComparison.OrdinalIgnoreCase),
            "세계 자동 로그 폴더");

        var performanceManual = LogOutputDirectories.GetDirectory(new LogOutputTarget(
            LogOutputKind.Performance,
            LogOutputMode.Manual));
        True(
            performanceManual.EndsWith(
                Path.Combine("logs", "performance", "manual"),
                StringComparison.OrdinalIgnoreCase),
            "성능 수동 로그 폴더");

        var agentId = new AgentId(42);
        var personalManual = LogOutputDirectories.GetDirectory(new LogOutputTarget(
            LogOutputKind.Personal,
            LogOutputMode.Manual,
            agentId));
        True(
            personalManual.EndsWith(
                Path.Combine("logs", "personal", agentId.ToString(), "manual"),
                StringComparison.OrdinalIgnoreCase),
            "개인별 수동 로그 폴더");
        Throws<ArgumentException>(
            () => LogOutputDirectories.GetDirectory(new LogOutputTarget(
                LogOutputKind.Personal,
                LogOutputMode.Automatic)),
            "인물 ID 없는 개인 로그 경로 거부");
    }

    public static void DefaultWorldPerformanceBudget()
    {
        const long targetTick = 20_000;
        WarmDefaultWorldPerformancePaths(targetTick);
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();

        var totalStopwatch = Stopwatch.StartNew();
        var startupStopwatch = Stopwatch.StartNew();
        var world = World.CreateDefault(20260830);
        startupStopwatch.Stop();
        var allocatedBeforeSimulation = GC.GetTotalAllocatedBytes(precise: true);
        var simulationStopwatch = Stopwatch.StartNew();
        world.AdvanceTo(new SimulationTick(targetTick));
        simulationStopwatch.Stop();
        var simulationAllocatedBytes = checked(
            GC.GetTotalAllocatedBytes(precise: true) - allocatedBeforeSimulation);
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        var liveHeapBytes = Math.Max(
            0,
            GC.GetGCMemoryInfo().HeapSizeBytes - GC.GetGCMemoryInfo().FragmentedBytes);
        var projectionStopwatch = Stopwatch.StartNew();
        var snapshot = world.CreateSnapshot(150, 150);
        projectionStopwatch.Stop();
        totalStopwatch.Stop();
        var checkpointStopwatch = Stopwatch.StartNew();
        var checkpoint = world.CreateCheckpoint();
        checkpointStopwatch.Stop();
        var totalActions = checkpoint.Agents.Sum(agent => (long)agent.ActionsTaken);
        var minimumActions = checkpoint.Agents.Min(agent => agent.ActionsTaken);
        var maximumActions = checkpoint.Agents.Max(agent => agent.ActionsTaken);
        var maximumActionAgent = checkpoint.Agents
            .Select((agent, index) => (Agent: agent, Index: index))
            .MaxBy(candidate => candidate.Agent.ActionsTaken);
        var restoreStopwatch = Stopwatch.StartNew();
        var restored = World.Restore(checkpoint);
        restoreStopwatch.Stop();
        var cultivationParallel = world.GetCultivationParallelDiagnostics();

        Console.WriteLine(
            $"PERF: {world.Population:N0}명 · 초기화 {startupStopwatch.Elapsed.TotalMilliseconds:N0} ms + " +
            $"{targetTick:N0} tick {simulationStopwatch.Elapsed.TotalMilliseconds:N0} ms + " +
            $"관찰 {projectionStopwatch.Elapsed.TotalMilliseconds:N1} ms = " +
            $"{totalStopwatch.Elapsed.TotalMilliseconds:N0} ms · checkpoint " +
            $"{checkpointStopwatch.Elapsed.TotalMilliseconds:N0} ms · schema {WorldCheckpoint.CurrentSchemaVersion} 복원 " +
            $"{restoreStopwatch.Elapsed.TotalMilliseconds:N0} ms · 수행 후보 " +
            $"{cultivationParallel.EligibleCandidateCount:N0}명 / 병렬 " +
            $"{cultivationParallel.ParallelCandidateCount:N0}명·{cultivationParallel.ParallelBatchCount:N0}묶음 / " +
            $"재검산 {cultivationParallel.FingerprintFallbackCount:N0}명 · " +
            $"행동 {totalActions:N0}회({minimumActions:N0}~{maximumActions:N0})");
        Console.WriteLine(
            $"PERF MEMORY: simulation 할당 {simulationAllocatedBytes / (1024d * 1024d):N2} MiB · " +
            $"tick당 {simulationAllocatedBytes / (double)targetTick:N0} B · " +
            $"강제 GC 후 live heap {liveHeapBytes / (1024d * 1024d):N2} MiB");
        Console.WriteLine(
            $"PERF ACTION MAX: A{maximumActionAgent.Index:D5} · " +
            $"{maximumActionAgent.Agent.Activity} · last month {maximumActionAgent.Agent.LastActionMonth} · " +
            $"next {maximumActionAgent.Agent.NextDecisionTick} · state " +
            $"{maximumActionAgent.Agent.CultivationState}");
        Equal(targetTick, snapshot.Tick.Value, "성능 검증 도달 tick");
        Equal(10_000, snapshot.Population, "성능 검증 인구");
        True(snapshot.TotalCombatsCompleted > 0, "성능 검증 중 전투가 완료되어야 합니다.");
        True(
            totalStopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"10,000명 시뮬레이션 성능 예산 10초를 초과했습니다: {totalStopwatch.Elapsed.TotalSeconds:N2}초");
        True(
            checkpointStopwatch.Elapsed + restoreStopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"10,000명 schema {WorldCheckpoint.CurrentSchemaVersion} checkpoint·복원 성능 예산 5초를 초과했습니다.");
        Equal(world.ComputeStateHash(), restored.ComputeStateHash(), "10,000명 현재 schema 복원 hash");
    }

    static void AssertMonthlySortBound(
        RankingCategoryCacheDiagnostics before,
        RankingCategoryCacheDiagnostics after,
        long elapsedMonthBoundaries,
        string category)
    {
        var revisionDelta = checked(after.DataRevision - before.DataRevision);
        var sortDelta = checked(after.SortCount - before.SortCount);
        True(sortDelta <= elapsedMonthBoundaries, $"{category} 월 경계당 최대 한 번 정렬");
        True((ulong)sortDelta <= revisionDelta, $"{category} dirty revision 없는 정렬 금지");
        Equal(revisionDelta > 0, sortDelta > 0, $"{category} 변경 revision 월 경계 반영");
    }

    static void AssertDecisionRankingOrder(
        IReadOnlyList<RankingEntry> ranking,
        int[] ranksByAgent,
        string category)
    {
        var expectedAgents = Enumerable.Range(0, ranksByAgent.Length)
            .OrderBy(agent => ranksByAgent[agent])
            .ToArray();
        EqualSequence(
            expectedAgents,
            ranking.Select(entry => entry.AgentId.Value).ToArray(),
            $"{category} 월 경계 의사결정 랭킹 순서");
        EqualSequence(
            Enumerable.Range(1, ranking.Count).ToArray(),
            ranking.Select(entry => entry.Rank).ToArray(),
            $"{category} 1-based 연속 순위");
    }

    static void WarmDefaultWorldPerformancePaths(long targetTick)
    {
        // 동일한 10,000명 작업집합을 한 번 통과시켜 tiered JIT/PGO가 정상 상태에
        // 도달하게 한다. 이 시간은 별도 cold-start 비용이며 steady-state budget에
        // 섞지 않는다.
        var warmup = World.CreateDefault(0x23_00_23UL);
        warmup.AdvanceTo(new SimulationTick(targetTick));
        _ = warmup.CreateSnapshot(16, 16);
        var checkpoint = warmup.CreateCheckpoint();
        _ = World.Restore(checkpoint).ComputeStateHash();
    }
}
