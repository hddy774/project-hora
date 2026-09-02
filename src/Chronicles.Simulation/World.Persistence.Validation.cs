using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed partial class World
{
    private static void ValidateCheckpoint(WorldCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        WorldBalanceRules.Validate(checkpoint.Balance ?? WorldBalanceConfiguration.Default);
        if (checkpoint.SchemaVersion >= 30)
        {
            if (checkpoint.Generation is null)
            {
                throw new InvalidDataException("세계 생성 카탈로그 권위 상태가 없습니다.");
            }
            _ = WorldGenerationCatalog.Restore(checkpoint.Seed, checkpoint.Generation);
            if (checkpoint.Generation.LastContentGenerationMonth
                > GameCalendar.GetMonthIndex(new SimulationTick(checkpoint.Tick)))
            {
                throw new InvalidDataException("세계 생성 cursor가 현재 세계 시간보다 앞서 있습니다.");
            }
        }
        if (checkpoint.SchemaVersion is < 7 or > WorldCheckpoint.CurrentSchemaVersion)
        {
            throw new InvalidDataException($"지원하지 않는 세계 schema {checkpoint.SchemaVersion}입니다.");
        }

        if (checkpoint.Agents is not { Length: > 0 } || checkpoint.Tick < 0)
        {
            throw new InvalidDataException("세계 체크포인트의 인구 또는 시간이 잘못되었습니다.");
        }

        if (checkpoint.SchemaVersion >= 31
            && (checkpoint.DecisionRankings is not { } decisionRankings
                || !IsValidDecisionRanking(decisionRankings, checkpoint.Agents.Length)
                || decisionRankings.LastProcessedMonth
                    > GameCalendar.GetMonthIndex(new SimulationTick(checkpoint.Tick))
                || (checkpoint.Tick == 0
                    ? decisionRankings.LastProcessedMonth is not (-1 or 0)
                    : decisionRankings.LastProcessedMonth
                        != GameCalendar.GetMonthIndex(new SimulationTick(checkpoint.Tick)))))
        {
            throw new InvalidDataException("월 경계 의사결정 랭킹 체크포인트가 잘못되었습니다.");
        }

        if (checkpoint.Events is null
            || checkpoint.Events.Length > WorldLogCapacity
            || checkpoint.EventSequence < 0
            || checkpoint.ChronicleSequence < 0
            || checkpoint.PersonalLogSequence < 0
            || checkpoint.Events.Any(entry =>
                entry.Sequence < 1
                || entry.Sequence > checkpoint.EventSequence
                || entry.Tick.Value > checkpoint.Tick
                || entry.PrimaryAgent.Value < 0
                || entry.PrimaryAgent.Value >= checkpoint.Agents.Length
                || (entry.SecondaryAgent is { } secondary
                    && (secondary.Value < 0 || secondary.Value >= checkpoint.Agents.Length))))
        {
            throw new InvalidDataException("세계 로그 또는 로그 sequence가 잘못되었습니다.");
        }

        if (checkpoint.TotalCombatsStarted != checkpoint.TotalCombatsCompleted + checkpoint.ActiveCombats)
        {
            throw new InvalidDataException("세계 체크포인트의 전투 수가 일치하지 않습니다.");
        }

        if (checkpoint.SchemaVersion >= 21
            && !SparseRelationStore.IsValidCheckpoint(
                checkpoint.Agents.Length,
                checkpoint.Relations,
                checkpoint.Tick))
        {
            throw new InvalidDataException("세계 체크포인트의 희소 관계 상태가 잘못되었습니다.");
        }

        if (checkpoint.SchemaVersion >= 21
            && checkpoint.Relations!.Relations.Any(relation =>
                checkpoint.Agents[relation.LeftAgent].Identity is not { } leftIdentity
                || checkpoint.Agents[relation.RightAgent].Identity is not { } rightIdentity
                || relation.LeftPersonId != leftIdentity.PersonId
                || relation.RightPersonId != rightIdentity.PersonId))
        {
            throw new InvalidDataException("희소 관계의 현재 생 Identity 연결이 잘못되었습니다.");
        }

        if (checkpoint.SchemaVersion >= 23
            && (!ReincarnationStore.IsValidCheckpoint(
                    checkpoint.Reincarnation,
                    checkpoint.Tick)
                || !IsValidReincarnationRuntimeLink(checkpoint)))
        {
            throw new InvalidDataException("세계 체크포인트의 윤회 계보·여정 상태가 잘못되었습니다.");
        }

        if (checkpoint.SchemaVersion >= 25
            && (!AgentCausalStore.IsValidCheckpoint(
                    checkpoint.Agents.Length,
                    checkpoint.Causal)
                || !IsValidCausalRuntimeLink(checkpoint)))
        {
            throw new InvalidDataException("세계 체크포인트의 AI 인과·인지·기억 상태가 잘못되었습니다.");
        }

        if (checkpoint.SchemaVersion >= 26
            && !OrganizationStore.IsValidCheckpoint(
                checkpoint.Agents.Length,
                checkpoint.Organizations!))
        {
            throw new InvalidDataException("세계 체크포인트의 조직 권위 상태가 잘못되었습니다.");
        }

        if (checkpoint.SchemaVersion >= 27)
        {
            if (checkpoint.Content is null)
            {
                throw new InvalidDataException("세계 체크포인트의 콘텐츠 권위 상태가 없습니다.");
            }
            ContentAuthorityStore.ValidateCheckpoint(checkpoint.Content);
        }

        if (checkpoint.SchemaVersion >= 28)
        {
            if (checkpoint.Knowledge is null || checkpoint.Content is null)
            {
                throw new InvalidDataException("세계 체크포인트의 지식 권위 상태가 없습니다.");
            }
            KnowledgeAuthorityStore.ValidateCheckpoint(
                checkpoint.Knowledge,
                checkpoint.Agents.Length,
                checkpoint.Content);
        }

        if (checkpoint.SchemaVersion >= 29)
        {
            if (checkpoint.Laws is null)
            {
                throw new InvalidDataException("세계 체크포인트의 법칙 권위 상태가 없습니다.");
            }
            LawAuthorityStore.ValidateCheckpoint(checkpoint.Laws, checkpoint.Agents.Length);
        }

        if (checkpoint.Atlas is { } atlas)
        {
            SpatialAuthorityStore.ValidateCheckpoint(atlas, checkpoint.Agents.Length);
        }

        IReadOnlyDictionary<ulong, RelationStateCheckpoint>? relationById = null;
        if (checkpoint.SchemaVersion >= 22)
        {
            relationById = checkpoint.Relations!.Relations.ToDictionary(
                relation => relation.Id.Value);
        }

        if (checkpoint.TotalMissionsCompleted < 0
            || checkpoint.TotalTreasuresCrafted < 0
            || checkpoint.TotalPillsCrafted < 0
            || checkpoint.TotalMaterialsSold < 0)
        {
            throw new InvalidDataException("세계 체크포인트의 경제 집계가 잘못되었습니다.");
        }

        var monsterSpeciesPerBiome = checkpoint.SchemaVersion >= 30
            ? checkpoint.Generation!.Configuration.MonsterSpeciesPerBiome
            : WorldMapRules.MonstersPerWildBiome;
        var maximumMonsterPopulation = checkpoint.SchemaVersion >= 30
            ? Math.Max(
                WorldMapRules.MaximumMonsterPopulation,
                checkpoint.Generation!.Configuration.InitialMonsterPopulationPerHabitat)
            : WorldMapRules.MaximumMonsterPopulation;
        if (checkpoint.SchemaVersion >= 11
            && (checkpoint.Map is not { } map
                || map.TotalMonsterHunts < 0
                || map.TotalMonsterVictories < 0
                || map.TotalMonsterVictories > map.TotalMonsterHunts
                || (checkpoint.SchemaVersion >= 14
                    && (map.MonsterPopulations is not { } monsterPopulations
                        || (monsterPopulations.Length
                                != WorldMapRules.LocationCount * monsterSpeciesPerBiome
                            && !(checkpoint.Atlas is null
                                && monsterPopulations.Length
                                    == 9 * monsterSpeciesPerBiome))
                        || map.MonsterPopulations.Any(value =>
                            value < 0 || value > maximumMonsterPopulation)
                        || map.LastRegenerationMonth < 0
                        || map.LastRegenerationMonth > GameCalendar.GetMonthIndex(
                            new SimulationTick(checkpoint.Tick))))))
        {
            throw new InvalidDataException("세계 체크포인트의 지도 집계가 잘못되었습니다.");
        }

        if (checkpoint.LastRankingRefreshTick < 0
            || checkpoint.LastRankingRefreshTick > checkpoint.Tick
            || checkpoint.RankingMovementFromTick < 0
            || checkpoint.RankingMovementFromTick > checkpoint.LastRankingRefreshTick
            || checkpoint.RankingRisenCount < 0
            || checkpoint.RankingFallenCount < 0
            || checkpoint.RankingUnchangedCount < 0
            || checkpoint.RankingRisenCount
                + checkpoint.RankingFallenCount
                + checkpoint.RankingUnchangedCount > checkpoint.Agents.Length
            || checkpoint.RankingTotalAbsoluteMovement < 0)
        {
            throw new InvalidDataException("세계 체크포인트의 순위 변동 통계가 잘못되었습니다.");
        }

        if (checkpoint.SchemaVersion >= 14
            && (checkpoint.EliteCombatChangedCounts is not { Length: 4 }
                || checkpoint.EliteRealmChangedCounts is not { Length: 4 }
                || checkpoint.EliteCombatChangedCounts.Any(value => value is < 0 or > 10)
                || checkpoint.EliteRealmChangedCounts.Any(value => value is < 0 or > 10)))
        {
            throw new InvalidDataException("상위권 순위 변동 통계가 잘못되었습니다.");
        }

        if (checkpoint.SchemaVersion >= 15
            && (checkpoint.Events.Any(entry => string.IsNullOrWhiteSpace(entry.PrimaryName))
                || !IsValidRankingHistory(
                    checkpoint.RankingHistory,
                    checkpoint.Tick,
                    checkpoint.Agents.Length,
                    requirePositionChangeTotals: checkpoint.SchemaVersion >= 16)
                || (checkpoint.SchemaVersion < 17
                    && (checkpoint.WorldChronicle is null
                        || checkpoint.WorldChronicle.Any(entry => !IsValidWorldEvent(
                            entry,
                            checkpoint.EventSequence,
                            checkpoint.Tick,
                            checkpoint.Agents.Length))
                        || checkpoint.WorldChronicle.Any(entry =>
                            string.IsNullOrWhiteSpace(entry.PrimaryName))
                        || !IsStrictlyIncreasingSequence(checkpoint.WorldChronicle)))
                || (checkpoint.SchemaVersion >= 17
                    && (checkpoint.WorldChronicle is not { Length: 0 }
                        || !IsValidWorldCenturyChronicle(
                            checkpoint.CenturyChronicle,
                            checkpoint.Tick,
                            checkpoint.Agents.Length)))))
        {
            throw new InvalidDataException("세계 연대기 또는 기간별 랭킹 이력이 잘못되었습니다.");
        }

        var marketGoodCount = checkpoint.Market?.Stock?.Length ?? -1;
        var validMarketGoodCount = marketGoodCount == EconomyRules.MarketGoodCount
            || (checkpoint.SchemaVersion < 14
                && marketGoodCount == EconomyRules.LegacyMarketGoodCount);
        if (checkpoint.Market is null
            || checkpoint.Market.TreasurySpiritStones < 0
            || checkpoint.Market.PurchaseTransactions < 0
            || checkpoint.Market.SaleTransactions < 0
            || checkpoint.Market.MissionDeliveries < 0
            || checkpoint.Market.MaterialsProcessed < 0
            || checkpoint.Market.PillsProduced < 0
            || checkpoint.Market.TreasuresProduced < 0
            || checkpoint.Market.TechniqueBooksProduced < 0
            || checkpoint.Market.SpellBooksProduced < 0
            || checkpoint.Market.Stock is not { Length: var stockLength }
            || !validMarketGoodCount
            || checkpoint.Market.Prices is not { Length: var priceLength }
            || priceLength != stockLength
            || checkpoint.Market.Supply is not { Length: var supplyLength }
            || supplyLength != stockLength
            || checkpoint.Market.Demand is not { Length: var demandLength }
            || demandLength != stockLength
            || checkpoint.Market.Stock.Any(value => value < 0)
            || checkpoint.Market.Prices.Any(value => value < 1)
            || checkpoint.Market.Supply.Any(value => value < 0)
            || checkpoint.Market.Demand.Any(value => value < 0)
            || checkpoint.Market.TreasureQualityHundredths < 0
            || (checkpoint.SchemaVersion < 14
                && marketGoodCount == EconomyRules.LegacyMarketGoodCount
                && checkpoint.Market.TreasureQualityHundredths
                    > (long)checkpoint.Market.Stock[32] * QualityValue.MaximumHundredths)
            || (checkpoint.SchemaVersion >= 14
                && (checkpoint.Market.QualityHundredths is not { Length: EconomyRules.MarketGoodCount }
                    || checkpoint.Market.QualityHundredths.Any(value => value < 0)
                    || !IsValidMarketQualityTotals(checkpoint.Market)
                    || checkpoint.Market.RemainingSpiritStoneOre < 0
                    || checkpoint.Market.SpiritStonesMined < 0
                    || checkpoint.Market.MiningMissionCount < 0
                    || checkpoint.Market.LastIndustryMonth < 0
                    || checkpoint.Market.LastIndustryMonth > GameCalendar.GetMonthIndex(
                        new SimulationTick(checkpoint.Tick))
                    || (checkpoint.SchemaVersion >= 24
                        && checkpoint.Market.SpiritStoneReserveTarget <= 0)
                    || checkpoint.Market.ProductionQueue is null
                    || checkpoint.Market.ProductionQueue.Length
                        > EconomyRules.MaximumConcurrentProduction
                    || checkpoint.Market.ProductionQueue.Any(order =>
                        !IsValidCityProductionOrder(order))
                    || (checkpoint.SchemaVersion >= 24
                        && !IsValidMarketCheckpointV24(
                            checkpoint.Market,
                            checkpoint.Agents.Length,
                            checkpoint.Tick)))))
        {
            throw new InvalidDataException("세계 체크포인트의 도시 시장 상태가 잘못되었습니다.");
        }

        if (checkpoint.Market.Authority is { } marketAuthority)
        {
            CityMarket.ValidateMarketAuthorityCheckpoint(
                marketAuthority,
                checkpoint.Agents.Length);
            for (var goodId = 0; goodId < EconomyRules.MarketGoodCount; goodId++)
            {
                if (marketAuthority.ReservedStock[goodId] > checkpoint.Market.Stock[goodId])
                {
                    throw new InvalidDataException("시장 예약량이 실제 재고를 초과했습니다.");
                }
            }
        }

        for (var index = 0; index < checkpoint.Agents.Length; index++)
        {
            var agent = checkpoint.Agents[index];
            var validMaterialCount = agent.MaterialInventory?.Length == EconomyRules.MaterialCount
                || (checkpoint.SchemaVersion < 14
                    && agent.MaterialInventory?.Length == EconomyRules.LegacyMaterialCount);
            var validTreasureCount = agent.EquippedTreasureQualities?.Length
                    == EconomyRules.TreasureSlotCount
                || (checkpoint.SchemaVersion < 14
                    && agent.EquippedTreasureQualities?.Length
                        == EconomyRules.LegacyTreasureSlotCount);
            var validMissionMaterialCount = agent.MaterialInventory?.Length
                ?? EconomyRules.MaterialCount;
            if (string.IsNullOrWhiteSpace(agent.Name)
                || agent.SpiritualRoot > QualityValue.MaximumHundredths
                || agent.Bone > QualityValue.MaximumHundredths
                || agent.Constitution > QualityValue.MaximumHundredths
                || agent.Comprehension > QualityValue.MaximumHundredths
                || agent.Mind > QualityValue.MaximumHundredths
                || agent.Soul > QualityValue.MaximumHundredths
                || agent.TechniqueQuality > QualityValue.MaximumHundredths
                || agent.SpellQuality > QualityValue.MaximumHundredths
                || (checkpoint.SchemaVersion >= 9
                    && (agent.TechniqueMastery > SkillProgressionRules.MaximumHundredths
                        || agent.SpellMastery > SkillProgressionRules.MaximumHundredths))
                || agent.CombatExperience < 0
                || agent.SpiritStones < 0
                || agent.GrowthPills < 0
                || agent.BreakthroughPills < 0
                || agent.MissionsCompleted < 0
                || (agent.ActiveMissionMaterial != byte.MaxValue
                    && agent.ActiveMissionMaterial >= validMissionMaterialCount)
                || agent.MaterialInventory is not { Length: var materialLength }
                || !validMaterialCount
                || agent.MaterialInventory.Any(count => count < 0)
                || agent.EquippedTreasureQualities is not { Length: var treasureLength }
                || !validTreasureCount
                || agent.EquippedTreasureQualities.Any(quality =>
                    quality != EconomyRules.EmptyTreasureQuality
                    && quality > QualityValue.MaximumHundredths)
                || !RealmRules.IsValidMinorStage(agent.RealmStage, agent.RealmLayer)
                || !Enum.IsDefined(agent.CultivationState)
                || !Enum.IsDefined(agent.CultivationBottleneckCause)
                || !Enum.IsDefined(agent.Activity)
                || agent.CultivationProgress < 0
                || agent.CultivationProgress > GetCheckpointCultivationRequirement(checkpoint.SchemaVersion, agent)
                || (agent.CultivationState == CultivationState.Bottleneck
                    && agent.CultivationBottleneckCause == CultivationBottleneckCause.None)
                || (agent.CultivationState != CultivationState.Bottleneck
                    && agent.CultivationBottleneckCause != CultivationBottleneckCause.None)
                || (agent.CultivationState == CultivationState.Completed
                    && !(agent.RealmStage == RealmStage.HalfImmortal
                        && (checkpoint.SchemaVersion == 7
                            ? agent.RealmLayer == 4
                            : agent.RealmLayer == RealmRules.MinorStageCount)))
                || agent.BirthTick < 0
                || agent.BirthTick > checkpoint.Tick
                || agent.LifeNumber < 1
                || agent.PersonalLog is null
                || agent.PersonalLog.Count > PersonalLogCapacity
                || (checkpoint.SchemaVersion < 32 && agent.Chronicle.Count == 0)
                || agent.TargetRank != 1
                || agent.NextDecisionTick <= checkpoint.Tick
                || agent.LastActionMonth < -1
                || agent.LastActionMonth > GameCalendar.GetMonthIndex(new SimulationTick(checkpoint.Tick))
                || agent.LastActionTick < 0
                || agent.LastActionTick > checkpoint.Tick
                || agent.ActionsTaken < 0
                || (agent.LastActionMonth < 0 && (agent.LastActionTick != 0 || agent.ActionsTaken != 0))
                || (agent.LastActionMonth >= 0
                    && GameCalendar.GetMonthIndex(new SimulationTick(agent.LastActionTick)) != agent.LastActionMonth)
                || (GameCalendar.GetMonthIndex(new SimulationTick(agent.NextDecisionTick))
                        <= agent.LastActionMonth
                    && !IsLifecycleBoundarySchedule(index, agent, checkpoint))
                || agent.Opponent < -1
                || agent.Opponent >= checkpoint.Agents.Length)
            {
                throw new InvalidDataException($"Agent {index} 체크포인트가 잘못되었습니다.");
            }

            if (checkpoint.SchemaVersion >= 8
                && !IsValidCultivationCheckpoint(
                    agent.Cultivation,
                    checkpoint.SchemaVersion,
                    checkpoint.Agents.Length,
                    checkpoint.Tick,
                    agent.Activity))
            {
                throw new InvalidDataException($"Agent {index}의 수행 V2 체크포인트가 잘못되었습니다.");
            }

            if (checkpoint.SchemaVersion >= 9 && !IsValidSkillProgress(agent.SkillProgress))
            {
                throw new InvalidDataException($"Agent {index}의 공법·법술 성장 체크포인트가 잘못되었습니다.");
            }

            if (checkpoint.SchemaVersion >= 10
                && !IsValidCombatAuthority(agent.CombatAuthority, checkpoint.Agents.Length))
            {
                throw new InvalidDataException($"Agent {index}의 전투 권위 체크포인트가 잘못되었습니다.");
            }

            if (checkpoint.SchemaVersion >= 11 && !IsValidMapAgent(agent.Map))
            {
                throw new InvalidDataException($"Agent {index}의 지도 체크포인트가 잘못되었습니다.");
            }

            if (checkpoint.SchemaVersion >= 13 && !IsValidCombatProgress(agent.CombatProgress))
            {
                throw new InvalidDataException($"Agent {index}의 전투 성장 체크포인트가 잘못되었습니다.");
            }

            if (checkpoint.SchemaVersion >= 18 && !IsValidAptitudeDetails(agent))
            {
                throw new InvalidDataException($"Agent {index}의 세부 선천자질 체크포인트가 잘못되었습니다.");
            }

            if (checkpoint.SchemaVersion >= 20
                && !IsValidPersonCausalCheckpoint(index, agent, checkpoint.Tick))
            {
                throw new InvalidDataException($"Agent {index}의 인물 신원·결정 추적 체크포인트가 잘못되었습니다.");
            }

            if (checkpoint.SchemaVersion >= 25
                && (agent.LifeAuthority is not { } lifeAuthority
                    || !PersonLifeAuthorityRules.IsValid(lifeAuthority)
                    || !IsValidLifeAuthorityLink(agent, lifeAuthority)))
            {
                throw new InvalidDataException($"Agent {index}의 생애·육신·외형 권위 체크포인트가 잘못되었습니다.");
            }

            if (checkpoint.SchemaVersion >= 32
                && !PersonalChronicleArchiveStore.IsValidCheckpoint(agent, checkpoint.Tick))
            {
                throw new InvalidDataException(
                    $"Agent {index}의 개인 100년 종합 연대기가 잘못되었습니다.");
            }

            if (checkpoint.SchemaVersion >= 22
                && !IsValidTeachingCheckpoint(
                    index,
                    agent,
                    checkpoint,
                    relationById!))
            {
                throw new InvalidDataException(
                    $"Agent {index}의 사제·Teaching 체크포인트가 잘못되었습니다. "
                    + DescribeTeachingCheckpoint(agent));
            }

            if (agent.PersonalLog.Any(entry =>
                    entry.Sequence < 1
                    || entry.Sequence > checkpoint.PersonalLogSequence
                    || entry.Tick.Value > checkpoint.Tick
                    || entry.LifeNumber < 1
                    || entry.LifeNumber > agent.LifeNumber
                    || !Enum.IsDefined(entry.Kind)
                    || (entry.Activity is { } activity && !Enum.IsDefined(activity))
                    || (entry.WorldEventKind is { } eventKind && !Enum.IsDefined(eventKind))
                    || (entry.ChronicleEventKind is { } chronicleKind && !Enum.IsDefined(chronicleKind))
                    || (entry.Kind == PersonalLogKind.ActionCompleted
                        && (entry.Activity is null
                            || entry.WorldEventKind is not null
                            || entry.ChronicleEventKind is not null))
                    || (entry.Kind == PersonalLogKind.WorldEvent
                        && (entry.Activity is not null
                            || entry.WorldEventKind is null
                            || entry.ChronicleEventKind is not null))
                    || (entry.Kind == PersonalLogKind.Chronicle
                        && (entry.Activity is not null
                            || entry.WorldEventKind is not null
                            || entry.ChronicleEventKind is null))
                    || (entry.RelatedAgent is { } related
                        && (related.Value < 0 || related.Value >= checkpoint.Agents.Length))))
            {
                throw new InvalidDataException($"Agent {index}의 개인 로그가 잘못되었습니다.");
            }

            if (agent.Chronicle.Any(entry =>
                    entry.LifeNumber < 1
                    || entry.Tick.Value > checkpoint.Tick
                    || entry.Sequence < 1
                    || entry.Sequence > checkpoint.ChronicleSequence))
            {
                throw new InvalidDataException($"Agent {index}의 일대기가 잘못되었습니다.");
            }

            if (agent.Activity == AgentActivity.Fighting
                && (agent.Opponent < 0 || checkpoint.Agents[agent.Opponent].Opponent != index))
            {
                throw new InvalidDataException($"Agent {index}의 전투 상대가 일치하지 않습니다.");
            }
        }
    }

    private static byte MigrateLegacyFloor(byte legacyFloor) => legacyFloor switch
    {
        1 => 1,
        2 => 4,
        3 => 7,
        4 => 9,
        _ => throw new InvalidDataException("기존 소경지 값이 잘못되었습니다."),
    };

    private static bool IsLifecycleBoundarySchedule(
        int index,
        AgentCheckpoint agent,
        WorldCheckpoint checkpoint)
    {
        var ownBoundary = checked(
            agent.BirthTick
            + ((long)RealmRules.GetProfile(agent.RealmStage).MaximumLifespanYears
                * GameCalendar.TicksPerYear));
        if (agent.NextDecisionTick == ownBoundary)
        {
            return true;
        }

        if (agent.Activity != AgentActivity.Fighting
            || (uint)agent.Opponent >= (uint)checkpoint.Agents.Length
            || agent.Opponent == index)
        {
            return false;
        }

        var opponent = checkpoint.Agents[agent.Opponent];
        if (!RealmRules.IsValidMinorStage(opponent.RealmStage, opponent.RealmLayer))
        {
            return false;
        }

        var opponentBoundary = checked(
            opponent.BirthTick
            + ((long)RealmRules.GetProfile(opponent.RealmStage).MaximumLifespanYears
                * GameCalendar.TicksPerYear));
        return agent.NextDecisionTick == opponentBoundary;
    }

    private static int MigrateLegacyCultivationWork(
        RealmStage realm,
        byte legacyFloor,
        byte destinationFloor,
        int progress)
    {
        var oldLifespanTicks = checked(RealmRules.GetProfile(realm).MaximumLifespanYears * GameCalendar.TicksPerYear);
        var oldRequirement = checked((int)((oldLifespanTicks + 6) / 12));
        var newRequirement = RealmRules.GetMinorStageRequiredWork(realm, destinationFloor);
        return oldRequirement == 0
            ? 0
            : checked((int)Math.Min(newRequirement, (long)progress * newRequirement / oldRequirement));
    }

    private static int GetCheckpointCultivationRequirement(int schemaVersion, AgentCheckpoint agent)
    {
        if (schemaVersion == 7)
        {
            var lifespanTicks = checked(RealmRules.GetProfile(agent.RealmStage).MaximumLifespanYears * GameCalendar.TicksPerYear);
            return checked((int)((lifespanTicks + 6) / 12));
        }

        return agent.CultivationState is CultivationState.PreCultivation or CultivationState.EntryTraining
            || (agent.CultivationState == CultivationState.Bottleneck
                && agent.CultivationBottleneckCause == CultivationBottleneckCause.EntryGate)
            ? CultivationRules.RequiredEntryWork
            : RealmRules.GetMinorStageRequiredWork(agent.RealmStage, agent.RealmLayer);
    }

    private static bool IsValidCultivationCheckpoint(
        CultivationCheckpoint? checkpoint,
        int schemaVersion,
        int population,
        long worldTick,
        AgentActivity activity)
    {
        if (checkpoint is not { } value
            || value.PendingCarryoverWork < 0
            || value.LastWorkBefore < 0
            || value.LastWorkGain < 0
            || value.AuthorityRevision < 0
            || value.CultivationExperience < 0
            || value.FormationInstallationExperience > QualityValue.MaximumHundredths
            || value.EntryGatePassMask > 0b111
            || !Enum.IsDefined(value.FoundationRoute)
            || value.ComponentValues is not { Length: CultivationRules.RealmComponentSlotCount }
            || value.ComponentLimits is not { Length: CultivationRules.RealmComponentSlotCount }
            || value.FormationValues is not { Length: CultivationRules.FormationSlotCount }
            || (schemaVersion >= 12
                && value.EntryGateTempering is not { Length: CultivationRules.EntryGateSlotCount })
            || (value.EntryGateTempering is { } tempering
                && (tempering.Length != CultivationRules.EntryGateSlotCount
                    || tempering.Any(item => item > QualityValue.MaximumHundredths))))
        {
            return false;
        }

        if (value.ActionPlan is { } plan
            && !IsValidCultivationActionPlan(plan, population, worldTick, activity))
        {
            return false;
        }

        for (var component = 0; component < CultivationRules.RealmComponentSlotCount; component++)
        {
            if (value.ComponentValues[component] > value.ComponentLimits[component]
                || value.ComponentLimits[component] > QualityValue.MaximumHundredths)
            {
                return false;
            }
        }

        var trace = value.LatestTrace;
        if (!value.FormationValues.All(item => item <= QualityValue.MaximumHundredths)
            || trace.FormulaRevision is < 0 or > CultivationRules.FormulaRevision
            || !Enum.IsDefined(trace.AttemptClass)
            || !Enum.IsDefined(trace.Outcome)
            || !Enum.IsDefined(trace.Blocker)
            || !Enum.IsDefined(trace.FormulaKind)
            || trace.AttemptEffectRegistryRevision < 0
            || trace.CandidateEffectCount < 0
            || trace.SelectedEffectCount < 0
            || trace.SelectedEffectCount > trace.CandidateEffectCount
            || !Enum.IsDefined(trace.AttemptEffectAvailability)
            || !Enum.IsDefined(trace.FailureEffect)
            || !Enum.IsDefined(trace.FailureEffectAvailability)
            || !Enum.IsDefined(trace.RngDomain)
            || (trace.FoundationProcedureSteps & ~FoundationProcedureStep.Complete) != 0
            || trace.EnvironmentFactorBasisPoints is < 0 or > 20_000
            || !Enum.IsDefined(trace.EnvironmentAvailability)
            || !Enum.IsDefined(trace.LawUnderstandingAvailability)
            || !Enum.IsDefined(trace.LuckFateAvailability)
            || trace.AuthorityRevisionBefore < 0
            || trace.AuthorityRevisionAfter < trace.AuthorityRevisionBefore)
        {
            return false;
        }

        if (value.FoundationFormation is { } formation)
        {
            try
            {
                FoundationFormationAuthorityStore.ValidateCheckpoint(formation);
            }
            catch (InvalidDataException)
            {
                return false;
            }

            if (formation.Source.Route != value.FoundationRoute
                || value.FoundationRoute == FoundationRoute.None)
            {
                return false;
            }
        }
        else if (value.FoundationRoute != FoundationRoute.None)
        {
            // schema 25 이전 PC 저장은 Route만 보유하므로 현재 구조에서도 읽는다.
        }

        if (schemaVersion < 19)
        {
            return true;
        }

        return trace.AuthorityRevisionAfter == value.AuthorityRevision
            && (value.AuthorityRevision == 0
                ? trace.FormulaKind == CultivationFormulaKind.None
                : trace.FormulaKind != CultivationFormulaKind.None
                    && trace.AuthorityRevisionAfter == trace.AuthorityRevisionBefore + 1);
    }

    private static bool IsValidCultivationActionPlan(
        CultivationActionPlanState plan,
        int population,
        long worldTick,
        AgentActivity activity)
    {
        if (plan.AuthorityRevision < 0
            || plan.StartedTick < 0
            || plan.StartedTick > worldTick
            || plan.PlannedEndTick < plan.StartedTick
            || plan.PlannedMonths < 0
            || plan.PlannedMonths > CultivationPlanRules.MaximumPlanMonths
            || plan.CompletedMonths < 0
            || plan.CompletedMonths > plan.PlannedMonths
            || plan.LocationId >= WorldMapRules.LocationCount
            || !Enum.IsDefined(plan.Intensity)
            || !Enum.IsDefined(plan.ResourcePolicy)
            || plan.SpiritStoneBudget < 0
            || plan.SpiritStonesConsumed < 0
            || plan.SpiritStonesConsumed > plan.SpiritStoneBudget
            || plan.GrowthPillBudget < 0
            || plan.GrowthPillsConsumed < 0
            || plan.GrowthPillsConsumed > plan.GrowthPillBudget
            || !Enum.IsDefined(plan.FormationSupport)
            || plan.FormationKnowledgeHundredths > QualityValue.MaximumHundredths
            || plan.FormationInstallationExperienceHundredths
                > QualityValue.MaximumHundredths
            || !Enum.IsDefined(plan.TeachingRole)
            || plan.TeachingPartnerAgent < -1
            || plan.TeachingPartnerAgent >= population
            || plan.FrozenTeachingBonusBasisPoints is < 10_000 or > 13_000
            || plan.FrozenConditionFactorBasisPoints is < 5_000 or > 20_000)
        {
            return false;
        }

        if (plan.AuthorityRevision == 0)
        {
            return !plan.IsActive
                && plan.PlannedMonths == 0
                && plan.CompletedMonths == 0;
        }

        return !plan.IsActive
            || (activity == AgentActivity.Cultivating
                && plan.PlannedMonths >= CultivationPlanRules.MinimumPlanMonths);
    }

    private static bool IsValidSkillProgress(SkillProgressCheckpoint? checkpoint) => checkpoint is { } value
        && value.TechniqueExperience is >= 0 and <= SkillProgressionRules.MaximumExperience
        && value.SpellExperience is >= 0 and <= SkillProgressionRules.MaximumExperience
        && value.TechniqueUnderstanding <= SkillProgressionRules.MaximumHundredths
        && value.SpellUnderstanding <= SkillProgressionRules.MaximumHundredths;

    private static bool IsValidMarketQualityTotals(CityMarketCheckpoint market)
    {
        var qualities = market.QualityHundredths!;
        for (var goodId = 0; goodId < EconomyRules.MarketGoodCount; goodId++)
        {
            var definition = EconomyRules.GetMarketGood(goodId);
            var isQualityGood = definition.Kind is MarketGoodKind.DefenseTreasure
                or MarketGoodKind.AttackTreasure
                or MarketGoodKind.TechniqueBook
                or MarketGoodKind.SpellBook;
            var maximum = checked((long)market.Stock[goodId] * QualityValue.MaximumHundredths);
            if (qualities[goodId] > maximum || (!isQualityGood && qualities[goodId] != 0))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidCityProductionOrder(CityProductionCheckpoint order)
    {
        if (order.GoodId < EconomyRules.RealmGoodStartId
            || order.GoodId >= EconomyRules.MarketGoodCount
            || order.CompletedMonths < 0
            || order.RequiredMonths != EconomyRules.CityProductionMonths
            || order.CompletedMonths >= order.RequiredMonths)
        {
            return false;
        }

        var definition = EconomyRules.GetMarketGood(order.GoodId);
        var usesQuality = definition.Kind is MarketGoodKind.DefenseTreasure
            or MarketGoodKind.AttackTreasure
            or MarketGoodKind.TechniqueBook
            or MarketGoodKind.SpellBook;
        return order.MaterialCost == EconomyRules.GetProductionMaterialCost(
                definition.Kind,
                definition.Realm)
            && order.SpiritStoneCost == EconomyRules.GetProductionSpiritStoneCost(definition)
            && (usesQuality
                ? order.QualityHundredths <= QualityValue.MaximumHundredths
                : order.QualityHundredths == EconomyRules.EmptyTreasureQuality);
    }

    private static bool IsValidMarketCheckpointV24(
        CityMarketCheckpoint market,
        int population,
        long maximumTick)
    {
        if (maximumTick > (long.MaxValue - 15L) / 16L)
        {
            return false;
        }

        var maximumSourceResultId = (maximumTick * 16L) + 15L;
        var windowLength = checked(
            EconomyRules.MarketGoodCount * EconomyRules.MarketFlowWindowMonths);
        if (market.RecentSupply is not { Length: EconomyRules.MarketGoodCount } recentSupply
            || market.RecentDemand is not { Length: EconomyRules.MarketGoodCount } recentDemand
            || market.SupplyWindowBuckets is not { Length: var supplyBucketLength }
            || supplyBucketLength != windowLength
            || market.DemandWindowBuckets is not { Length: var demandBucketLength }
            || demandBucketLength != windowLength
            || recentSupply.Any(value => value < 0)
            || recentDemand.Any(value => value < 0)
            || market.SupplyWindowBuckets.Any(value => value < 0)
            || market.DemandWindowBuckets.Any(value => value < 0)
            || market.CurrentFlowBucket < 0
            || market.CurrentFlowBucket >= EconomyRules.MarketFlowWindowMonths
            || market.AuthorityRevision <= 0
            || market.LastSettlementSourceByAgent is not { Length: var sourceLength }
            || sourceLength != population
            || market.LastSettlementSourceByAgent.Any(source => source is < -1 or 0)
            || market.LastSettlementSourceByAgent.Any(source =>
                source > maximumSourceResultId)
            || market.RecentTrades is not { Length: var tradeCount }
            || tradeCount > EconomyRules.RecentTradeCapacity
            || market.NextTradeSequence is < 1 or >= long.MaxValue)
        {
            return false;
        }

        for (var goodId = 0; goodId < EconomyRules.MarketGoodCount; goodId++)
        {
            long supply = 0;
            long demand = 0;
            for (var bucket = 0; bucket < EconomyRules.MarketFlowWindowMonths; bucket++)
            {
                var index = (bucket * EconomyRules.MarketGoodCount) + goodId;
                var supplyValue = market.SupplyWindowBuckets[index];
                var demandValue = market.DemandWindowBuckets[index];
                if (supply > long.MaxValue - supplyValue
                    || demand > long.MaxValue - demandValue)
                {
                    return false;
                }

                supply += supplyValue;
                demand += demandValue;
            }

            if (supply != recentSupply[goodId]
                || demand != recentDemand[goodId]
                || supply > market.Supply[goodId]
                || demand > market.Demand[goodId]
                || market.Prices[goodId] != EconomyRules.GetMarketPrice(
                    EconomyRules.GetMarketGood(goodId),
                    market.Stock[goodId],
                    market.Supply[goodId],
                    market.Demand[goodId],
                    supply,
                    demand))
            {
                return false;
            }
        }

        var previousSequence = 0L;
        var previousTick = -1L;
        foreach (var trade in market.RecentTrades)
        {
            var qualityGood = trade.GoodId >= 0
                && trade.GoodId < EconomyRules.MarketGoodCount
                && EconomyRules.GetMarketGood(trade.GoodId).Kind is
                    MarketGoodKind.DefenseTreasure
                    or MarketGoodKind.AttackTreasure
                    or MarketGoodKind.TechniqueBook
                    or MarketGoodKind.SpellBook;
            var validProduct = trade.Count > 0
                && trade.UnitPrice > 0
                && trade.UnitPrice <= long.MaxValue / trade.Count;
            var itemTotal = validProduct ? trade.UnitPrice * trade.Count : 0L;
            var missionBonus = trade.TotalSpiritStones >= itemTotal
                ? trade.TotalSpiritStones - itemTotal
                : -1L;
            var validTotal = trade.Kind switch
            {
                MarketTradeKind.Purchase or MarketTradeKind.Sale =>
                    validProduct && trade.TotalSpiritStones == itemTotal,
                MarketTradeKind.MissionDelivery =>
                    validProduct
                    && missionBonus is >= 0L and <= 90L
                    && missionBonus % 10L == 0L,
                _ => false,
            };
            var validAgent = trade.AgentId >= 0 && trade.AgentId < population;
            var validSource = validAgent
                && trade.SourceResultId > 0
                && trade.SourceResultId <= maximumSourceResultId
                && trade.SourceResultId <= market.LastSettlementSourceByAgent[trade.AgentId]
                && trade.SourceResultId / 16L == trade.Tick
                && trade.SourceResultId % 16L is > 0 and < 16;
            if (trade.Sequence <= 0
                || (previousSequence > 0 && trade.Sequence != previousSequence + 1L)
                || trade.Sequence >= market.NextTradeSequence
                || !validSource
                || trade.Tick < 0
                || trade.Tick > maximumTick
                || trade.Tick < previousTick
                || !validAgent
                || !Enum.IsDefined(trade.Kind)
                || trade.GoodId < 0
                || trade.GoodId >= EconomyRules.MarketGoodCount
                || !validProduct
                || !validTotal
                || (qualityGood
                    ? trade.QualityHundredths > QualityValue.MaximumHundredths
                    : trade.QualityHundredths != EconomyRules.EmptyTreasureQuality))
            {
                return false;
            }

            previousSequence = trade.Sequence;
            previousTick = trade.Tick;
        }

        return market.RecentTrades.Length == 0
            ? market.NextTradeSequence == 1
            : previousSequence + 1L == market.NextTradeSequence;
    }

    private static bool IsValidCombatAuthority(CombatAuthorityCheckpoint? checkpoint, int population) =>
        checkpoint is { } value
        && value.ActionCount >= 0
        && value.TurnOwner >= -1
        && value.TurnOwner < population
        && value.InjuryPoints <= CombatInjuryRules.MaximumInjuryPoints;

    private static bool IsValidMapAgent(MapAgentCheckpoint? checkpoint) => checkpoint is { } value
        && value.LocationId < WorldMapRules.LocationCount
        && value.TravelDestinationId < WorldMapRules.LocationCount
        && value.MonsterHunts >= 0
        && value.MonsterVictories >= 0
        && value.MonsterVictories <= value.MonsterHunts;

    private static bool IsValidCombatProgress(CombatProgressCheckpoint? checkpoint) =>
        checkpoint is { } value
        && value.UnderstandingHundredths <= CombatProgressionRules.MaximumHundredths;

    private static bool IsValidAptitudeDetails(AgentCheckpoint agent)
    {
        if (agent.AptitudeDetails is not { } details
            || details.FormulaRevision != AptitudeRules.FormulaRevision
            || !details.RequiresReview
            || details.InnateComponents is not { Length: AptitudeRules.TotalComponentCount }
            || details.InnateComponents.Any(value => value > QualityValue.MaximumHundredths))
        {
            return false;
        }

        ReadOnlySpan<ushort> components = details.InnateComponents;
        return AptitudeRules.CalculateSummaryHundredths(
                components,
                AptitudeDomain.SpiritualRoot) == agent.SpiritualRoot
            && AptitudeRules.CalculateSummaryHundredths(
                components,
                AptitudeDomain.Bone) == agent.Bone
            && AptitudeRules.CalculateSummaryHundredths(
                components,
                AptitudeDomain.Constitution) == agent.Constitution
            && AptitudeRules.CalculateSummaryHundredths(
                components,
                AptitudeDomain.Comprehension) == agent.Comprehension
            && AptitudeRules.CalculateSummaryHundredths(
                components,
                AptitudeDomain.Mind) == agent.Mind
            && AptitudeRules.CalculateSummaryHundredths(
                components,
                AptitudeDomain.Soul) == agent.Soul;
    }

}
