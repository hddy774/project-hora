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

internal static class CombatProgressionTests
{
    public static void KoreanLargeNumberFormatting()
    {
        Equal("9,999", KoreanNumberFormatter.Format(9_999), "만 미만 표기");
        Equal("2만", KoreanNumberFormatter.Format(20_000), "만 단위 표기");
        Equal("10만", KoreanNumberFormatter.Format(100_000), "십만 단위 표기");
        Equal("1억", KoreanNumberFormatter.Format(100_000_000), "억 단위 표기");
        Equal("1.23억", KoreanNumberFormatter.Format(123_456_789), "억 소수 표기");
        Equal("1조", KoreanNumberFormatter.Format(1_000_000_000_000), "조 단위 표기");
    }

    public static void QualityTiersAndBoundaryMultipliers()
    {
        Equal(8, QualityRules.All.Count, "품질 단계 수");
        True(new QualityValue(999).Tier == QualityTier.Scrap, "9.99 미만 폐품");
        True(new QualityValue(1_000).Tier == QualityTier.ExtremeLow, "10 이상 극하품");
        True(new QualityValue(2_999).Tier == QualityTier.ExtremeLow, "29.99 극하품");
        True(new QualityValue(3_000).Tier == QualityTier.Low, "30 이상 하품");
        True(new QualityValue(3_999).Tier == QualityTier.Low, "39.99 하품");
        True(new QualityValue(4_000).Tier == QualityTier.Medium, "40 이상 중품");
        True(new QualityValue(5_999).Tier == QualityTier.Medium, "59.99 중품");
        True(new QualityValue(6_000).Tier == QualityTier.High, "60 이상 상품");
        True(new QualityValue(7_999).Tier == QualityTier.High, "79.99 상품");
        True(new QualityValue(8_000).Tier == QualityTier.ExtremeHigh, "80 이상 극상품");
        True(new QualityValue(8_999).Tier == QualityTier.ExtremeHigh, "89.99 극상품");
        True(new QualityValue(9_000).Tier == QualityTier.Heaven, "90 이상 천품");
        True(new QualityValue(9_999).Tier == QualityTier.Heaven, "99.99 천품");
        True(new QualityValue(10_000).Tier == QualityTier.Flawless, "100 무결품");

        BoundaryRatio(999, 1_000, 5m, "폐품→극하품");
        BoundaryRatio(2_999, 3_000, 2m, "극하품→하품");
        BoundaryRatio(3_999, 4_000, 1.5m, "하품→중품");
        BoundaryRatio(5_999, 6_000, 1.5m, "중품→상품");
        BoundaryRatio(7_999, 8_000, 2m, "상품→극상품");
        BoundaryRatio(8_999, 9_000, 4m, "극상품→천품");
        BoundaryRatio(9_999, 10_000, 10m, "천품→무결품");
    }

    public static void DetailedAptitudesAuthorityAndLazyProjection()
    {
        Equal(6, AptitudeRules.All.Count, "6대 자질 정의 수");
        Equal(60, AptitudeRules.All.Sum(group => group.Components.Count), "세부 자질 총 60개");
        Throws<ArgumentOutOfRangeException>(
            () => AptitudeRules.GetComponentOffset((AptitudeDomain)byte.MaxValue),
            "잘못된 자질 영역 거부");
        Equal(
            60,
            AptitudeRules.All.SelectMany(group => group.Components)
                .Select(component => component.StableId)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            "세부 자질 안정 ID 중복 금지");
        True(
            AptitudeRules.ProvisionalBalances.All(balance => balance.RequiresReview),
            "미확정 세부 자질 분포는 재검토 표식");

        var stopwatch = Stopwatch.StartNew();
        var world = World.CreateDefault(741103);
        var checkpoint = world.CreateCheckpoint();
        stopwatch.Stop();
        Equal(WorldCheckpoint.CurrentSchemaVersion, checkpoint.SchemaVersion, "윤회 계보·Teaching·인과·조직 저장 schema");

        long summaryTotal = 0;
        var summaryMinimum = int.MaxValue;
        var summaryMaximum = int.MinValue;
        foreach (var agent in checkpoint.Agents)
        {
            var details = agent.AptitudeDetails;
            True(details is not null, "신규 저장 세부 자질 존재");
            Equal(AptitudeRules.FormulaRevision, details!.FormulaRevision, "세부 자질 공식 revision");
            True(details.RequiresReview, "분포 임시값 재검토 상태 저장");
            Equal(AptitudeRules.TotalComponentCount, details.InnateComponents.Length, "Agent 세부 자질 길이");
            for (var domainIndex = 0; domainIndex < AptitudeRules.DomainCount; domainIndex++)
            {
                var domain = (AptitudeDomain)domainIndex;
                var summary = AptitudeRules.CalculateSummaryHundredths(
                    details.InnateComponents,
                    domain);
                Equal(TestCheckpointFactory.GetAptitudeSummary(agent, domain), summary, "Component 평균과 Summary 일치");
                summaryTotal += summary;
                summaryMinimum = Math.Min(summaryMinimum, summary);
                summaryMaximum = Math.Max(summaryMaximum, summary);
            }
        }

        var summaryMean = summaryTotal / (decimal)(checkpoint.Agents.Length * AptitudeRules.DomainCount);
        True(summaryMean is >= 4_800m and <= 5_200m, "10,000명 세부 자질 평균 중심 구간");
        True(summaryMinimum < 2_000 && summaryMaximum > 8_000, "10,000명 자질 분포 범위");

        var stateHash = world.ComputeStateHash();
        using (var runner = new SimulationRunner(world))
        {
            var hidden = runner.GetObservation(
                new AgentId(0),
                sections: ObservationSections.None);
            True(hidden.SelectedAgent.AptitudeDetails is null, "접힌 세부 자질 UI는 DTO 미생성");
            var visible = runner.GetObservation(
                new AgentId(0),
                sections: ObservationSections.AptitudeDetails);
            Equal(6, visible.SelectedAgent.AptitudeDetails!.Groups.Count, "펼친 세부 자질 UI 6개 내부 탭 DTO");
            True(
                visible.SelectedAgent.AptitudeDetails.Groups.All(group =>
                    group.DerivedAbilities.Count == 2
                    && group.DerivedAbilities.All(ability =>
                        ability.FormulaText.Contains("30%", StringComparison.Ordinal)
                        && ability.FormulaText.Contains("20%", StringComparison.Ordinal))),
                "각 자질 내부 탭의 수행/전투 파생 능력");
        }
        Equal(stateHash, world.ComputeStateHash(), "세부 자질 관찰은 권위 상태 무영향");

        var encoded = WorldSaveCodec.Encode(checkpoint);
        True(encoded.Length < 64 * 1024 * 1024, "10,000명 세부 자질 저장은 압축 payload 예산 이내");
        var restored = World.Restore(WorldSaveCodec.Decode(encoded));
        Equal(stateHash, restored.ComputeStateHash(), "세부 자질 저장 codec·hash 왕복");

        var legacySource = World.Create(741104, 10).CreateCheckpoint();
        var legacy = legacySource with
        {
            SchemaVersion = 17,
            Agents = legacySource.Agents
                .Select(agent => agent with { AptitudeDetails = null })
                .ToArray(),
        };
        var migrated = World.Restore(legacy);
        var migratedAgent = migrated.GetAgentSnapshot(new AgentId(0), includeAptitudeDetails: true);
        foreach (var group in migratedAgent.AptitudeDetails!.Groups)
        {
            True(
                group.Components.All(component => component.InnateValue == group.Summary),
                "schema 17 대표값은 손실 없이 균일 Component로 이전");
        }

        var componentAuthoritySource = World.Create(741105, 1).CreateCheckpoint();
        var highCoreComponents = AptitudeRules.CreateUniformComponents(new Aptitudes(
            QualityValue.FromWhole(50),
            QualityValue.FromWhole(50),
            QualityValue.FromWhole(50),
            QualityValue.FromWhole(50),
            QualityValue.FromWhole(50),
            QualityValue.FromWhole(50)));
        var lowCoreComponents = highCoreComponents.ToArray();
        var boneStart = AptitudeRules.GetComponentOffset(AptitudeDomain.Bone);
        highCoreComponents[boneStart + 1] = 10_000;
        highCoreComponents[boneStart + 6] = 10_000;
        highCoreComponents[boneStart + 2] = 0;
        highCoreComponents[boneStart + 3] = 0;
        lowCoreComponents[boneStart + 1] = 0;
        lowCoreComponents[boneStart + 6] = 0;
        lowCoreComponents[boneStart + 2] = 10_000;
        lowCoreComponents[boneStart + 3] = 10_000;
        var uniformSummaryAgent = TestCheckpointFactory.WithSynchronizedAptitudeDetails(
            componentAuthoritySource.Agents[0] with
            {
                SpiritualRoot = 5_000,
                Bone = 5_000,
                Constitution = 5_000,
                Comprehension = 5_000,
                Mind = 5_000,
                Soul = 5_000,
            });
        var highCoreAgent = uniformSummaryAgent with
        {
            AptitudeDetails = new AptitudeDetailCheckpoint(
                AptitudeRules.FormulaRevision,
                RequiresReview: true,
                highCoreComponents),
        };
        var lowCoreAgent = uniformSummaryAgent with
        {
            AptitudeDetails = new AptitudeDetailCheckpoint(
                AptitudeRules.FormulaRevision,
                RequiresReview: true,
                lowCoreComponents),
        };
        var highCoreWorld = World.Restore(componentAuthoritySource with { Agents = [highCoreAgent] });
        var lowCoreWorld = World.Restore(componentAuthoritySource with { Agents = [lowCoreAgent] });
        var highCore = highCoreWorld.GetAgentSnapshot(new AgentId(0), includeAptitudeDetails: true);
        var lowCore = lowCoreWorld.GetAgentSnapshot(new AgentId(0), includeAptitudeDetails: true);
        Equal(highCore.Aptitudes.Bone, lowCore.Aptitudes.Bone, "같은 근골 Summary");
        Console.WriteLine(
            $"APTITUDE COMPONENT AUTHORITY: 공법 역할 " +
            $"{highCore.AptitudeDetails!.RoleScores.TechniqueEffectHundredths}/" +
            $"{lowCore.AptitudeDetails!.RoleScores.TechniqueEffectHundredths} · 보정 " +
            $"{highCore.CombatPowerDetails.TechniqueAptitudeBonusBasisPoints}/" +
            $"{lowCore.CombatPowerDetails.TechniqueAptitudeBonusBasisPoints}");
        True(
            highCore.CombatPowerDetails.TechniqueAptitudeBonusBasisPoints
                > lowCore.CombatPowerDetails.TechniqueAptitudeBonusBasisPoints,
            "실제 공법 보정은 Summary가 아니라 관련 Component 파생 능력을 사용");

        Console.WriteLine(
            $"APTITUDE PERF: 10,000명·60 Component 초기화+checkpoint {stopwatch.Elapsed.TotalMilliseconds:N1} ms · " +
            $"압축 저장 {encoded.Length / 1024d / 1024d:N2} MiB · 평균 {summaryMean / 100m:0.00}");
    }

    public static void SkillMasteryUnderstandingAndImmutableQuality()
    {
        Equal(100L, SkillProgressionRules.GetRequiredExperience(10), "숙련 0.10 요구 경험");
        Equal(10_000L, SkillProgressionRules.GetRequiredExperience(100), "숙련 1.00 요구 경험");
        Equal(100_000_000L, SkillProgressionRules.GetRequiredExperience(10_000), "숙련 100.00 요구 경험");
        Equal((ushort)100, SkillProgressionRules.GetMasteryHundredths(10_000), "경험→숙련 제곱근 곡선");
        True(
            SkillProgressionRules.GetRequiredExperience(9_001) - SkillProgressionRules.GetRequiredExperience(9_000)
                > SkillProgressionRules.GetRequiredExperience(1_001) - SkillProgressionRules.GetRequiredExperience(1_000),
            "고숙련 구간의 다음 0.01 요구 경험이 더 커야 합니다.");

        var original = World.Create(991122, 500);
        var checkpoint = original.CreateCheckpoint();
        var originalQualities = checkpoint.Agents.Select(agent => agent.TechniqueQuality).ToArray();
        var prepared = checkpoint.Agents.Select(agent =>
            TestCheckpointFactory.WithSynchronizedAptitudeDetails(agent with
            {
                Comprehension = 10_000,
                TechniqueMastery = 0,
                Activity = AgentActivity.TrainingTechnique,
                NextDecisionTick = 1,
                SkillProgress = new SkillProgressCheckpoint(0, 0, 0, 0),
            })).ToArray();
        var world = World.Restore(checkpoint with { Agents = prepared });
        world.AdvanceTo(new SimulationTick(1));
        var enlightenmentCount = 0;
        for (var index = 0; index < world.Population; index++)
        {
            var agent = world.GetAgentSnapshot(new AgentId(index));
            Equal(originalQualities[index], agent.TechniqueQuality.Hundredths, "수련·깨달음은 공법 품질 불변");
            True(agent.TechniqueTrainingExperience > 0, "공법 수련 경험 누적");
            True(agent.TechniqueMastery > 0, "누적 경험으로 숙련도 상승");
            True(
                world.GetAgentChronicle(new AgentId(index)).All(item =>
                    item.Kind is not ChronicleEventKind.TechniqueEnlightenment
                        and not ChronicleEventKind.SpellEnlightenment),
                "깨달음은 영구 연대기에 기록하지 않아야 합니다.");
            if (world.GetAgentPersonalLog(new AgentId(index))
                .Any(item => item.WorldEventKind == WorldEventKind.TechniqueEnlightenment))
            {
                enlightenmentCount++;
                True(agent.TechniqueUnderstanding > 0, "깨달음은 이해도만 상승");
            }
        }

        True(enlightenmentCount > 0, "다수 수련자 중 공법 깨달음이 발생해야 합니다.");
    }

    public static void CombatPowerAndRanking()
    {
        var world = World.CreateDefault(77);
        var snapshot = world.CreateSnapshot(1_000, 0);
        Equal(1_000, snapshot.CombatPowerRankings.Count, "전투력 TOP 1000");
        Equal(1_000, snapshot.RealmRankings.Count, "경지 TOP 1000");
        Equal(1_000, snapshot.AptitudeRankings.Count, "자질 TOP 1000");
        Equal(1_000, snapshot.WealthRankings.Count, "부자 TOP 1000");

        var previousPower = long.MaxValue;
        foreach (var entry in snapshot.CombatPowerRankings)
        {
            True(entry.Category == RankingCategory.CombatPower, "전투력 랭킹 범주");
            Equal(0L, entry.CombatPower.Aptitude, "자질은 직접 전투력에 합산하지 않아야 합니다.");
            Equal(entry.CombatPower.Total, checked(
                entry.CombatPower.Realm
                + entry.CombatPower.Aptitude
                + entry.CombatPower.Technique
                + entry.CombatPower.Spell
                + entry.CombatPower.Experience
                + entry.CombatPower.Treasure), "전투력 합계");
            True(entry.CombatPower.Total <= previousPower, "랭킹은 전투력 내림차순이어야 합니다.");
            previousPower = entry.CombatPower.Total;
        }

        var previousRealmScore = long.MaxValue;
        foreach (var entry in snapshot.RealmRankings)
        {
            True(entry.Category == RankingCategory.Realm, "경지 랭킹 범주");
            True(entry.Score <= previousRealmScore, "경지 랭킹은 대경지·소경지·진척 내림차순이어야 합니다.");
            previousRealmScore = entry.Score;
        }

        var previousAptitude = int.MaxValue;
        foreach (var entry in snapshot.AptitudeRankings)
        {
            True(entry.Category == RankingCategory.Aptitude, "자질 랭킹 범주");
            True(entry.AptitudeScore <= previousAptitude, "자질 랭킹은 6대 자질 합계 내림차순이어야 합니다.");
            previousAptitude = entry.AptitudeScore;
        }

        var previousWealth = long.MaxValue;
        foreach (var entry in snapshot.WealthRankings)
        {
            True(entry.Category == RankingCategory.Wealth, "부자 랭킹 범주");
            True(entry.Score <= previousWealth, "부자 랭킹은 보유 영석 내림차순이어야 합니다.");
            Equal(entry.Rank, world.GetAgentSnapshot(entry.AgentId).WealthRank, "부자 공식 순위");
            previousWealth = entry.Score;
        }
    }

    public static void WealthRankingHistoryAndWorldChronicle()
    {
        var world = World.Create(7901, 1_000);
        for (var year = 1; year <= 210; year++)
        {
            world.AdvanceTo(GameCalendar.FromYears(year));
        }

        var snapshot = world.CreateSnapshot(
            1_000,
            100,
            ObservationSections.StatisticsSummary
                | ObservationSections.WealthRanking
                | ObservationSections.WorldChronicle);
        Equal(1_000, snapshot.WealthRankings.Count, "부자 랭킹 조회 크기");
        var windows = snapshot.Statistics.RankingMovementWindows
            ?? Array.Empty<RankingMovementWindowSnapshot>();
        Equal(6 * 7, windows.Count, "6개 기간 × 7개 상위권 구간");
        int[] expectedTiers = [1, 3, 5, 10, 100, 500, 1_000];
        foreach (var period in Enum.GetValues<RankingMovementPeriod>())
        {
            var periodRows = windows.Where(item => item.Period == period).ToArray();
            Equal(expectedTiers.Length, periodRows.Length, $"{period} 구간 수");
            True(periodRows.Select(item => item.TopCount).SequenceEqual(expectedTiers), $"{period} 구간 순서");
            foreach (var row in periodRows)
            {
                True(row.FromTick.Value <= row.ToTick.Value, "랭킹 기간 순서");
                True(row.ComparedCount is >= 1 and <= 1_000, "랭킹 비교 인원 범위");
                True(row.ObservationIntervalCount >= 1, "랭킹 연간 관측 횟수");
                var maximumPositionChanges = checked((long)row.ComparedCount * row.ObservationIntervalCount);
                True(row.CombatPowerChangedCount is >= 0 && row.CombatPowerChangedCount <= maximumPositionChanges, "전투력 누적 자리 변동 범위");
                True(row.RealmChangedCount is >= 0 && row.RealmChangedCount <= maximumPositionChanges, "경지 누적 자리 변동 범위");
                True(row.WealthChangedCount is >= 0 && row.WealthChangedCount <= maximumPositionChanges, "부자 누적 자리 변동 범위");
                True(row.CombatPowerChangeBasisPoints is >= 0 and <= 10_000, "전투력 변동률 범위");
                True(row.RealmChangeBasisPoints is >= 0 and <= 10_000, "경지 변동률 범위");
                True(row.WealthChangeBasisPoints is >= 0 and <= 10_000, "부자 변동률 범위");
            }
        }

        True(
            windows.Any(row => row.ObservationIntervalCount > 1
                && (row.CombatPowerChangedCount > row.ComparedCount
                    || row.RealmChangedCount > row.ComparedCount
                    || row.WealthChangedCount > row.ComparedCount)),
            "다년 기간 자리 변동은 현재와 기준점의 차이가 아니라 연간 실제 변경 횟수를 누적");

        Equal(2, snapshot.PermanentWorldChronicle.Count, "완료된 100년마다 세계 역사서 한 권");
        var firstCentury = snapshot.PermanentWorldChronicle[0];
        Equal(0L, firstCentury.StartYear, "첫 세계 역사서 시작 연도");
        Equal(99L, firstCentury.EndYear, "첫 세계 역사서 종료 연도");
        True(
            firstCentury.RealmBreakthroughCounts.Sum() > 0,
            "세계 역사서는 대경지별 돌파 횟수를 압축");
        True(
            firstCentury.RealmBreakthroughUniqueCounts
                .Zip(firstCentury.RealmBreakthroughCounts)
                .All(pair => pair.First <= pair.Second),
            "대경지별 고유 돌파 인원은 돌파 횟수를 넘지 않음");
        True(
            firstCentury.CombatPowerPositionChanges.Any(value => value > 0)
                || firstCentury.RealmPositionChanges.Any(value => value > 0)
                || firstCentury.AptitudePositionChanges.Any(value => value > 0)
                || firstCentury.WealthPositionChanges.Any(value => value > 0),
            "세계 역사서는 TOP 1·3·5·10 실제 자리 변경을 압축");
        True(
            !string.IsNullOrWhiteSpace(firstCentury.CombatPowerLeader.Name)
                && !string.IsNullOrWhiteSpace(firstCentury.RealmLeader.Name)
                && !string.IsNullOrWhiteSpace(firstCentury.AptitudeLeader.Name)
                && !string.IsNullOrWhiteSpace(firstCentury.WealthLeader.Name),
            "세계 역사서는 세기 말 각 랭킹 1위를 보존");
        True(
            firstCentury.DeathCount >= firstCentury.UniqueDeathCount
                && firstCentury.ReincarnationEntryCount >= firstCentury.UniqueReincarnationEntryCount
                && firstCentury.RebirthCount >= firstCentury.UniqueRebirthCount,
            "세계 역사서는 사망·윤회·환생 횟수와 고유 인원을 함께 보존");
        var chronicleTail = world.CreateSnapshot(
            rankingCount: 1,
            eventCount: 0,
            sections: ObservationSections.WorldChronicle,
            worldChronicleCount: 5);
        Equal(snapshot.PermanentWorldChronicle.Count, chronicleTail.PermanentWorldChronicleCount, "연대기 전체 저장 수");
        Equal(Math.Min(5, snapshot.PermanentWorldChronicle.Count), chronicleTail.PermanentWorldChronicle.Count, "연대기 UI 꼬리 조회 크기");
        if (chronicleTail.PermanentWorldChronicle.Count > 0)
        {
            Equal(
                snapshot.PermanentWorldChronicle[^1].Sequence,
                chronicleTail.PermanentWorldChronicle[^1].Sequence,
                "연대기 UI 꼬리 조회 최신 사건");
        }
        var checkpoint = world.CreateCheckpoint();
        True(checkpoint.RankingHistory?.Frames is { Length: > 1 }, "기간별 랭킹 이력 저장");
        Equal(0, checkpoint.WorldChronicle?.Length ?? -1, "schema 17은 개인 사건형 구 세계 연대기를 비움");
        True(
            checkpoint.CenturyChronicle?.Entries.Length == snapshot.PermanentWorldChronicle.Count,
            "100년 세계 역사서 저장 수");
        var restored = World.Restore(WorldSaveCodec.Decode(WorldSaveCodec.Encode(checkpoint)));
        Equal(world.ComputeStateHash(), restored.ComputeStateHash(), "랭킹 이력·세계 연대기 저장 상태 hash");
    }

    public static void AptitudeIndirectCombatBalance()
    {
        static Aptitudes UniformAptitudes(int hundredths)
        {
            var value = new QualityValue(checked((ushort)hundredths));
            return new Aptitudes(value, value, value, value, value, value);
        }

        var lowAptitudes = UniformAptitudes(0);
        var neutralAptitudes = UniformAptitudes(5_000);
        var highAptitudes = UniformAptitudes(10_000);
        var lowBonuses = AptitudeCombatBonusFormula.Calculate(lowAptitudes);
        var neutralBonuses = AptitudeCombatBonusFormula.Calculate(neutralAptitudes);
        var highBonuses = AptitudeCombatBonusFormula.Calculate(highAptitudes);
        Equal(5_000, lowBonuses.TechniqueEffectBasisPoints, "최저 자질 공법 효과 하한");
        Equal(5_000, lowBonuses.SpellEffectBasisPoints, "최저 자질 법술 효과 하한");
        Equal(5_000, lowBonuses.CombatMasteryEffectBasisPoints, "최저 자질 전투 숙련 효과 하한");
        Equal(5_000, lowBonuses.CombatUnderstandingEffectBasisPoints, "최저 자질 전투 이해 효과 하한");
        Equal(5_000, lowBonuses.CombatExperienceGainBasisPoints, "최저 자질 전투 경험 획득 하한");
        Equal(10_000, neutralBonuses.TechniqueEffectBasisPoints, "자질 50.00 공법 효과 기준");
        Equal(10_000, neutralBonuses.SpellEffectBasisPoints, "자질 50.00 법술 효과 기준");
        Equal(10_000, neutralBonuses.CombatMasteryEffectBasisPoints, "자질 50.00 전투 숙련 효과 기준");
        Equal(10_000, neutralBonuses.CombatUnderstandingEffectBasisPoints, "자질 50.00 전투 이해 효과 기준");
        Equal(10_000, neutralBonuses.CombatExperienceGainBasisPoints, "자질 50.00 전투 경험 획득 기준");
        Equal(20_000, highBonuses.TechniqueEffectBasisPoints, "최고 자질 공법 효과 상한");
        Equal(20_000, highBonuses.SpellEffectBasisPoints, "최고 자질 법술 효과 상한");
        Equal(20_000, highBonuses.CombatMasteryEffectBasisPoints, "최고 자질 전투 숙련 효과 상한");
        Equal(20_000, highBonuses.CombatUnderstandingEffectBasisPoints, "최고 자질 전투 이해 효과 상한");
        Equal(20_000, highBonuses.CombatExperienceGainBasisPoints, "최고 자질 전투 경험 획득 상한");

        static CombatPowerBreakdown Power(Aptitudes aptitudes) => CombatPowerFormula.Calculate(
            RealmStage.QiRefining,
            1,
            aptitudes,
            QualityValue.FromWhole(50),
            5_000,
            5_000,
            QualityValue.FromWhole(50),
            5_000,
            5_000,
            0,
            0);

        var lowPower = Power(lowAptitudes);
        var neutralPower = Power(neutralAptitudes);
        var highPower = Power(highAptitudes);
        Equal(0L, lowPower.Aptitude, "최저 자질 직접 전투력");
        Equal(0L, neutralPower.Aptitude, "중립 자질 직접 전투력");
        Equal(0L, highPower.Aptitude, "최고 자질 직접 전투력");
        True(highPower.Technique <= (neutralPower.Technique * 2) + 1, "최고 자질 공법 전투력 보정 200% 상한");
        True(highPower.Spell <= (neutralPower.Spell * 2) + 1, "최고 자질 법술 전투력 보정 200% 상한");
        True(lowPower.Technique >= neutralPower.Technique / 2, "최저 자질 공법 전투력 보정 50% 하한");
        True(lowPower.Spell >= neutralPower.Spell / 2, "최저 자질 법술 전투력 보정 50% 하한");

        Equal(1_000, CombatProgressionRules.GetMasteryHundredths(10_000), "전투 경험의 숙련도 변환");
        Equal(50, CombatProgressionRules.ApplyExperienceGain(0, 100, 5_000), "최저 자질 전투 경험 50% 획득");
        Equal(100, CombatProgressionRules.ApplyExperienceGain(0, 100, 10_000), "기준 자질 전투 경험 100% 획득");
        Equal(200, CombatProgressionRules.ApplyExperienceGain(0, 100, 20_000), "최고 자질 전투 경험 200% 획득");
        Equal((ushort)50, CombatProgressionRules.ApplyUnderstandingGain(0, 100, 5_000), "최저 자질 전투 이해 50% 획득");
        Equal((ushort)100, CombatProgressionRules.ApplyUnderstandingGain(0, 100, 10_000), "기준 자질 전투 이해 100% 획득");
        Equal((ushort)200, CombatProgressionRules.ApplyUnderstandingGain(0, 100, 20_000), "최고 자질 전투 이해 200% 획득");
        Equal(50, SkillProgressionRules.ApplyExperienceBonus(100, 5_000), "최저 자질 공법·법술 경험 50% 획득");
        Equal(100, SkillProgressionRules.ApplyExperienceBonus(100, 10_000), "기준 자질 공법·법술 경험 100% 획득");
        Equal(200, SkillProgressionRules.ApplyExperienceBonus(100, 20_000), "최고 자질 공법·법술 경험 200% 획득");
        Equal((ushort)75, SkillProgressionRules.ApplyUnderstandingGain(0, 10_000, 5_000), "최저 자질 공법·법술 이해 50% 성장");
        Equal((ushort)150, SkillProgressionRules.ApplyUnderstandingGain(0, 10_000, 10_000), "기준 자질 공법·법술 이해 100% 성장");
        Equal((ushort)300, SkillProgressionRules.ApplyUnderstandingGain(0, 10_000, 20_000), "최고 자질 공법·법술 이해 200% 성장");

        static CombatAttributes Attributes(Aptitudes aptitudes) => CombatAttributeFormula.Calculate(
            RealmStage.QiRefining,
            1,
            aptitudes,
            QualityValue.FromWhole(50),
            5_000,
            5_000,
            QualityValue.FromWhole(50),
            5_000,
            5_000,
            1_000,
            1_000,
            0,
            0);

        var neutralAttributes = Attributes(neutralAptitudes);
        var highAttributes = Attributes(highAptitudes);
        True(highAttributes.Attack > neutralAttributes.Attack, "영근·오성 자질은 공격/법술 효과 보너스");
        True(highAttributes.Defense > neutralAttributes.Defense, "근골·체질 자질은 방어/공법 효과 보너스");
        True(highAttributes.MaximumHealth > neutralAttributes.MaximumHealth, "근골·체질 자질은 최대 생명력 보너스");
        True(highAttributes.MaximumQi > neutralAttributes.MaximumQi, "영근·오성 자질은 최대 영력 보너스");
        True(highAttributes.Judgment > neutralAttributes.Judgment, "오성·정신 자질은 판단력 보너스");
        True(highAttributes.Focus > neutralAttributes.Focus, "오성·정신 자질은 집중력 보너스");
        True(
            highAttributes.CultivationEfficiencyBasisPoints > neutralAttributes.CultivationEfficiencyBasisPoints,
            "영근·오성·정신 자질은 수행 효율 보너스");
    }

    public static void SkillCombatScaleRegression()
    {
        var aptitudes = new Aptitudes(
            new QualityValue(5_000),
            new QualityValue(5_000),
            new QualityValue(5_000),
            new QualityValue(5_000),
            new QualityValue(5_000),
            new QualityValue(5_000));
        var flawless = CombatPowerFormula.Calculate(
            RealmStage.QiRefining,
            1,
            aptitudes,
            new QualityValue(10_000),
            1_901,
            0,
            new QualityValue(0),
            0,
            0,
            0,
            0);
        Equal(100L, flawless.Realm, "연기경 1층 전투력 기준값");
        var heaven = CombatPowerFormula.Calculate(
            RealmStage.QiRefining,
            1,
            aptitudes,
            new QualityValue(9_999),
            1_901,
            0,
            new QualityValue(0),
            0,
            0,
            0,
            0);
        True(flawless.Technique < 50_000, "무결품 공법 하나가 연기경에서 억 단위 전투력을 만들면 안 됩니다.");
        True(
            flawless.Technique >= heaven.Technique * 9
                && flawless.Technique <= heaven.Technique * 11,
            "천품→무결품의 약 10배 품질 격차는 축소된 전투 기여에서도 유지되어야 합니다.");

        var attributes = CombatAttributeFormula.Calculate(
            RealmStage.QiRefining,
            1,
            aptitudes,
            new QualityValue(10_000),
            10_000,
            10_000,
            new QualityValue(10_000),
            10_000,
            10_000,
            0,
            0);
        True(attributes.MaximumHealth < 50_000, "무결품 공법의 연기경 최대 체력 폭증 방지");
        True(attributes.Attack < 50_000, "무결품 법술의 연기경 실전 공격력 폭증 방지");
    }

    public static void CombatAuthorityAndInjury()
    {
        var neutral = QualityValue.FromWhole(50);
        var neutralAptitudes = new Aptitudes(neutral, neutral, neutral, neutral, neutral, neutral);
        var neutralStats = CombatAttributeFormula.Calculate(
            RealmStage.QiRefining,
            1,
            neutralAptitudes,
            neutral,
            5_000,
            0,
            neutral,
            5_000,
            0,
            0,
            0);
        Equal(10_000, neutralStats.CultivationEfficiencyBasisPoints, "선천 수행자질 50.00의 수행 효율 기준");
        True(neutralStats.Attack > 0 && neutralStats.Defense > 0, "실전 공격·방어 속성");
        True(CombatInjuryRules.GetSeverity(0) == InjurySeverity.None, "무부상 경계");
        True(CombatInjuryRules.GetSeverity(5_000) == InjurySeverity.Severe, "중상 경계");
        True(CombatInjuryRules.GetSeverity(10_000) == InjurySeverity.Fatal, "치명상 경계");
        Equal(10_000, CombatInjuryRules.GetFatalChanceBasisPoints(InjurySeverity.Fatal), "치명상 사망 확률");

        var world = World.Create(8128, 1_000);
        world.AdvanceTo(new SimulationTick(20_000));
        var checkpoint = world.CreateCheckpoint();
        var activePairs = checkpoint.Agents
            .Select((agent, index) => (Agent: agent, Index: index))
            .Where(item => item.Agent.Opponent > item.Index)
            .ToArray();
        True(activePairs.Length > 0, "장기간 진행 중인 교대 전투");
        foreach (var pair in activePairs)
        {
            var opponent = checkpoint.Agents[pair.Agent.Opponent];
            Equal(
                pair.Agent.CombatAuthority!.Value.TurnOwner,
                opponent.CombatAuthority!.Value.TurnOwner,
                "양측의 단일 턴 소유자");
            var owner = pair.Agent.CombatAuthority.Value.TurnOwner;
            True(owner == pair.Index || owner == pair.Agent.Opponent, "턴 소유자는 전투 당사자여야 합니다.");
        }

        True(world.TotalDeaths > 0, "부상에 의한 전투 사망 누계");
        True(
            Enumerable.Range(0, world.Population)
                .Any(index => world.GetAgentChronicle(new AgentId(index))
                    .Any(entry => entry.Kind == ChronicleEventKind.CombatDeath)),
            "전투 사망 영구 연대기");
    }

    public static void LegacyCombatTurnMigration()
    {
        var world = World.Create(8129, 1_000);
        world.AdvanceTo(new SimulationTick(20_000));
        var current = world.CreateCheckpoint();
        True(current.ActiveCombats > 0, "migration fixture 진행 전투");
        var legacyAgents = current.Agents
            .Select(agent => agent with { CombatAuthority = null })
            .ToArray();
        var restored = World.Restore(current with
        {
            SchemaVersion = 9,
            Agents = legacyAgents,
        });
        var migrated = restored.CreateCheckpoint();
        var migratedPairs = migrated.Agents
            .Select((agent, index) => (Agent: agent, Index: index))
            .Where(item => item.Agent.Opponent > item.Index)
            .ToArray();
        True(migratedPairs.Length > 0, "구 저장 진행 전투 쌍 유지");
        foreach (var pair in migratedPairs)
        {
            var opponent = migrated.Agents[pair.Agent.Opponent];
            var owner = pair.Agent.CombatAuthority!.Value.TurnOwner;
            Equal(owner, opponent.CombatAuthority!.Value.TurnOwner, "구 저장 쌍의 공통 turn owner");
            True(owner == pair.Index || owner == pair.Agent.Opponent, "turn owner는 전투 당사자");
            var scheduledCount = (pair.Agent.NextDecisionTick == long.MaxValue ? 0 : 1)
                + (opponent.NextDecisionTick == long.MaxValue ? 0 : 1);
            Equal(1, scheduledCount, "구 저장 전투는 turn owner 하나만 예약");
            True(migrated.Agents[owner].NextDecisionTick != long.MaxValue, "turn owner 예약 유지");
        }

        var nextCombatTick = migratedPairs
            .SelectMany(pair => new[]
            {
                pair.Agent.NextDecisionTick,
                migrated.Agents[pair.Agent.Opponent].NextDecisionTick,
            })
            .Where(tick => tick != long.MaxValue)
            .Min();
        restored.AdvanceTo(new SimulationTick(nextCombatTick));
        Equal(
            restored.TotalCombatsStarted,
            restored.TotalCombatsCompleted + restored.ActiveCombats,
            "구 저장 전투 복구 후 수 보존");
    }

    public static void CombatProgressionSaveAndMigration()
    {
        var world = World.Create(8130, 1_000);
        world.AdvanceTo(new SimulationTick(20_000));
        var checkpoint = world.CreateCheckpoint();
        True(checkpoint.SchemaVersion == WorldCheckpoint.CurrentSchemaVersion, "윤회 계보·인과·조직 저장 현재 schema");
        True(
            checkpoint.Agents.Any(agent => agent.CombatProgress is { UnderstandingHundredths: > 0 }),
            "전투 이해도 저장 대상 발생");

        var decoded = WorldSaveCodec.Decode(WorldSaveCodec.Encode(checkpoint));
        True(
            checkpoint.Agents
                .Select(agent => agent.CombatProgress)
                .SequenceEqual(decoded.Agents.Select(agent => agent.CombatProgress)),
            "전투 이해도 codec 왕복");
        var restored = World.Restore(decoded);
        Equal(world.ComputeStateHash(), restored.ComputeStateHash(), "전투 성장 저장 상태 hash 왕복");

        var legacy = checkpoint with
        {
            SchemaVersion = 12,
            Agents = checkpoint.Agents
                .Select(agent => agent with { CombatProgress = null })
                .ToArray(),
        };
        var migrated = World.Restore(legacy);
        for (var index = 0; index < migrated.Population; index++)
        {
            var agent = migrated.GetAgentSnapshot(new AgentId(index));
            Equal(0, agent.CombatUnderstanding, "schema 12 전투 이해도 기본값");
            Equal(
                (int)CombatProgressionRules.GetMasteryHundredths(agent.CombatExperience),
                agent.CombatMastery,
                "schema 12 기존 경험에서 전투 숙련도 재구성");
        }
    }

    static void BoundaryRatio(int lowerHundredths, int upperHundredths, decimal expected, string message)
    {
        var lower = QualityRules.GetEffectiveHundredths(new QualityValue(checked((ushort)lowerHundredths)));
        var upper = QualityRules.GetEffectiveHundredths(new QualityValue(checked((ushort)upperHundredths)));
        var ratio = (decimal)upper / lower;
        True(Math.Abs(ratio - expected) < 0.01m, $"{message} 경계 배율: expected≈{expected}, actual={ratio}");
    }
}

