using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed partial class World
{
    private World(WorldCheckpoint checkpoint)
    {
        ValidateCheckpoint(checkpoint);
        Seed = checkpoint.Seed;
        Tick = new SimulationTick(checkpoint.Tick);
        _balance = checkpoint.Balance ?? WorldBalanceConfiguration.Default;
        WorldBalanceRules.Validate(_balance);
        _random = new DeterministicRandom(checkpoint.RandomState);
        _agents = new AgentStore(checkpoint.Agents.Length);
        _causal = checkpoint.SchemaVersion >= 25
            ? AgentCausalStore.Restore(checkpoint.Agents.Length, checkpoint.Causal!)
            : new AgentCausalStore(checkpoint.Agents.Length);
        _relations = checkpoint.SchemaVersion >= 21 && checkpoint.Relations is { } relations
            ? SparseRelationStore.Restore(checkpoint.Agents.Length, relations)
            : new SparseRelationStore(checkpoint.Agents.Length);
        _market = new CityMarket(checkpoint.Market, checkpoint.SchemaVersion, checkpoint.Agents.Length);
        _agentByRank = new int[checkpoint.Agents.Length];
        _rankByAgent = new int[checkpoint.Agents.Length];
        _agentByRealmRank = new int[checkpoint.Agents.Length];
        _realmRankByAgent = new int[checkpoint.Agents.Length];
        _agentByAptitudeRank = new int[checkpoint.Agents.Length];
        _aptitudeRankByAgent = new int[checkpoint.Agents.Length];
        _agentByWealthRank = new int[checkpoint.Agents.Length];
        _wealthRankByAgent = new int[checkpoint.Agents.Length];
        _worldChronicle = new WorldCenturyChronicle(
            checkpoint.Agents.Length,
            GameCalendar.GetYear(Tick));
        _worldChronicle.EntryRecorded += WorldChronicle_EntryRecorded;
        _monsterPopulations = checkpoint.Map?.MonsterPopulations is
            { Length: WorldMapRules.LocationCount * WorldMapRules.MonstersPerWildBiome } populations
                ? populations.ToArray()
                : CreateInitialMonsterPopulations();
        _eventSequence = checkpoint.EventSequence;
        _chronicleSequence = checkpoint.ChronicleSequence;
        _personalLogSequence = checkpoint.PersonalLogSequence;
        _totalBreakthroughs = checkpoint.TotalBreakthroughs;
        _totalChallenges = checkpoint.TotalChallenges;
        _totalCombatsStarted = checkpoint.TotalCombatsStarted;
        _totalCombatsCompleted = checkpoint.TotalCombatsCompleted;
        _activeCombats = checkpoint.ActiveCombats;
        _totalDeaths = checkpoint.TotalDeaths;
        _totalReincarnations = checkpoint.TotalReincarnations;
        _totalMissionsCompleted = checkpoint.TotalMissionsCompleted;
        _totalTreasuresCrafted = checkpoint.TotalTreasuresCrafted;
        _totalPillsCrafted = checkpoint.TotalPillsCrafted;
        _totalMaterialsSold = checkpoint.TotalMaterialsSold;
        _totalMonsterHunts = checkpoint.Map?.TotalMonsterHunts ?? 0;
        _totalMonsterVictories = checkpoint.Map?.TotalMonsterVictories ?? 0;
        _lastMonsterRegenerationMonth = checkpoint.Map?.LastRegenerationMonth ?? 0;
        _lastRankingRefreshTick = checkpoint.LastRankingRefreshTick;
        _rankingMovementFromTick = checkpoint.RankingMovementFromTick;
        _rankingRisenCount = checkpoint.RankingRisenCount;
        _rankingFallenCount = checkpoint.RankingFallenCount;
        _rankingUnchangedCount = checkpoint.RankingUnchangedCount;
        _rankingTotalAbsoluteMovement = checkpoint.RankingTotalAbsoluteMovement;
        if (checkpoint.EliteCombatChangedCounts is { Length: 4 } eliteCombat)
        {
            Array.Copy(eliteCombat, _eliteCombatChangedCounts, 4);
        }
        if (checkpoint.EliteRealmChangedCounts is { Length: 4 } eliteRealm)
        {
            Array.Copy(eliteRealm, _eliteRealmChangedCounts, 4);
        }

        for (var index = 0; index < checkpoint.Agents.Length; index++)
        {
            RestoreAgent(index, checkpoint.Agents[index], checkpoint.SchemaVersion);
            if (checkpoint.SchemaVersion < 25)
            {
                _causal.StartLife(
                    index,
                    CreateCausalObservation(index, initialRank: index + 1),
                    _agents.DecisionTraces[index],
                    AgentMemoryKind.Birth);
            }
            _agentByRank[index] = index;
            _agentByRealmRank[index] = index;
            _agentByAptitudeRank[index] = index;
            _agentByWealthRank[index] = index;
            _agents.RecalculatePower(index);
        }

        _atlas = checkpoint.Atlas is { } atlas
            ? SpatialAuthorityStore.Restore(atlas, _agents.LocationIds)
            : SpatialAuthorityStore.CreateLegacy(
                _agents.LocationIds,
                GameCalendar.GetMonthIndex(Tick));

        if (checkpoint.SchemaVersion >= 23)
        {
            _reincarnation = ReincarnationStore.Restore(
                checkpoint.Reincarnation!,
                checkpoint.Tick);
        }
        else
        {
            _reincarnation = MigrateLegacyReincarnationStore(checkpoint);
        }

        if (checkpoint.SchemaVersion < 10)
        {
            RepairLegacyCombatTurnAuthority();
        }

        for (var index = 0; index < checkpoint.Agents.Length; index++)
        {
            if (_agents.NextDecisionTicks[index] != long.MaxValue)
            {
                ScheduleAgent(index, _agents.NextDecisionTicks[index]);
            }
        }

        foreach (var worldEvent in checkpoint.Events.TakeLast(WorldLogCapacity))
        {
            _events.Enqueue(checkpoint.SchemaVersion >= 15
                ? worldEvent
                : worldEvent with
                {
                    PrimaryName = _agents.Names[worldEvent.PrimaryAgent.Value],
                    SecondaryName = worldEvent.SecondaryAgent is { } secondary
                        ? _agents.Names[secondary.Value]
                        : null,
                });
        }

        RefreshRankings(recordLeaderChange: false);
        if (checkpoint.SchemaVersion >= 15 && checkpoint.RankingHistory is { } rankingHistory)
        {
            _rankingHistory.Restore(rankingHistory);
        }
        if (checkpoint.SchemaVersion >= 17 && checkpoint.CenturyChronicle is { } centuryChronicle)
        {
            _worldChronicle = WorldCenturyChronicle.Restore(Population, centuryChronicle);
            _worldChronicle.EntryRecorded += WorldChronicle_EntryRecorded;
        }
        else
        {
            _worldChronicle.RestoreLegacyRiverResidents(
                _agents.Activities.Count(activity => activity == AgentActivity.Reincarnating));
        }
    }

    public static World Restore(WorldCheckpoint checkpoint) => new(checkpoint);

    public WorldAuthorityCheckpoint CreateAuthorityCheckpoint()
    {
        var checkpoint = CreateCheckpoint();
        return new WorldAuthorityCheckpoint(checkpoint, ComputeStateHash());
    }

    public static void ValidateCheckpointForPersistence(WorldCheckpoint checkpoint) =>
        ValidateCheckpoint(checkpoint);

    private ReincarnationStore MigrateLegacyReincarnationStore(WorldCheckpoint checkpoint)
    {
        var store = new ReincarnationStore(Population);
        for (var index = 0; index < Population; index++)
        {
            var inRiver = _agents.Activities[index] == AgentActivity.Reincarnating;
            var journeyStartedTick = inRiver
                ? _agents.Chronicles[index]
                    .Where(entry => entry.LifeNumber == _agents.LifeNumbers[index]
                        && entry.Kind == ChronicleEventKind.ReincarnationEntered)
                    .Select(entry => (long?)entry.Tick.Value)
                    .LastOrDefault()
                : null;
            if (inRiver && journeyStartedTick is null)
            {
                throw new InvalidDataException(
                    $"Agent {index}의 기존 윤회 시작 기록이 없습니다.");
            }

            var endKind = inRiver && _agents.Chronicles[index].Any(entry =>
                entry.LifeNumber == _agents.LifeNumbers[index]
                && entry.Kind == ChronicleEventKind.CombatDeath)
                    ? LifeEndKind.Combat
                    : LifeEndKind.Lifespan;
            store.MigrateLegacyCurrentLife(
                index,
                _agents.LifeNumbers[index],
                _agents.SoulIdentityIds[index],
                _agents.BodyIdentityIds[index],
                _agents.PersonIdentityIds[index],
                _agents.Names[index],
                _agents.BirthTicks[index],
                _agents.RealmStages[index],
                _agents.RealmLayers[index],
                _agents.Power[index].Total,
                Tick.Value,
                inRiver,
                journeyStartedTick,
                endKind);
            if (inRiver)
            {
                var journey = store.GetCurrentJourney(index)
                    ?? throw new InvalidDataException("기존 윤회 여정 이전에 실패했습니다.");
                _agents.NextDecisionTicks[index] = journey.NextTransitionTick;
            }
        }

        return store;
    }

    private static bool IsValidReincarnationRuntimeLink(WorldCheckpoint checkpoint)
    {
        var reincarnation = checkpoint.Reincarnation!;
        var completedJourneys = 0;
        var allLineagesComplete = reincarnation.CompleteLineage.All(value => value);
        var lineageByAgent = new List<LifeLineageEntry>?[checkpoint.Agents.Length];
        var activeJourneyByAgent = new ReincarnationJourneyState?[checkpoint.Agents.Length];
        var activeJourneyCounts = new byte[checkpoint.Agents.Length];
        foreach (var entry in reincarnation.Lineage)
        {
            (lineageByAgent[entry.AgentIndex] ??= []).Add(entry);
        }
        foreach (var journey in reincarnation.Journeys)
        {
            if (journey.CompletedTick is null)
            {
                activeJourneyCounts[journey.AgentIndex]++;
                activeJourneyByAgent[journey.AgentIndex] = journey;
            }
            else
            {
                completedJourneys = checked(completedJourneys + 1);
            }
        }

        for (var index = 0; index < checkpoint.Agents.Length; index++)
        {
            var agent = checkpoint.Agents[index];
            var lineage = lineageByAgent[index];
            if (lineage is null || lineage.Count == 0)
            {
                return false;
            }

            var current = lineage[^1];
            var activeJourney = activeJourneyByAgent[index];
            var inRiver = agent.Activity == AgentActivity.Reincarnating;
            if (current.LifeNumber != agent.LifeNumber
                || current.SoulId != agent.Identity!.Value.SoulId
                || current.BodyId != agent.Identity.Value.BodyId
                || current.PersonId != agent.Identity.Value.PersonId
                || current.PersonName != agent.Name
                || current.BirthTick != agent.BirthTick
                || (inRiver ? activeJourneyCounts[index] != 1 : activeJourneyCounts[index] != 0)
                || (inRiver && activeJourney!.NextTransitionTick != agent.NextDecisionTick)
                || (inRiver && current.DeathTick is null)
                || (!inRiver && current.DeathTick is not null))
            {
                return false;
            }
        }

        var expectedReincarnations = checkpoint.Agents.Sum(agent => agent.LifeNumber - 1);
        return expectedReincarnations == checkpoint.TotalReincarnations
            && checkpoint.TotalDeaths == checkpoint.TotalReincarnations
                + activeJourneyCounts.Sum(count => count)
            && (!allLineagesComplete || completedJourneys == checkpoint.TotalReincarnations);
    }

    private void RepairLegacyCombatTurnAuthority()
    {
        for (var left = 0; left < Population; left++)
        {
            var right = _agents.Opponents[left];
            if (_agents.Activities[left] != AgentActivity.Fighting
                || right <= left
                || right >= Population
                || _agents.Activities[right] != AgentActivity.Fighting
                || _agents.Opponents[right] != left)
            {
                continue;
            }

            var leftTick = _agents.NextDecisionTicks[left];
            var rightTick = _agents.NextDecisionTicks[right];
            var owner = leftTick < rightTick || (leftTick == rightTick && left < right)
                ? left
                : right;
            var waiting = owner == left ? right : left;
            _agents.CombatTurnOwners[left] = owner;
            _agents.CombatTurnOwners[right] = owner;
            _agents.NextDecisionTicks[owner] = Math.Min(leftTick, rightTick);
            _agents.NextDecisionTicks[waiting] = long.MaxValue;
        }
    }

    public WorldCheckpoint CreateCheckpoint()
    {
        var agents = new AgentCheckpoint[Population];
        for (var index = 0; index < Population; index++)
        {
            agents[index] = new AgentCheckpoint(
                _agents.Names[index],
                _agents.SpiritualRoots[index],
                _agents.Bones[index],
                _agents.Constitutions[index],
                _agents.Comprehensions[index],
                _agents.Minds[index],
                _agents.Souls[index],
                _agents.BirthTicks[index],
                _agents.LifeNumbers[index],
                _agents.BottleneckFailures[index],
                _agents.RealmStages[index],
                _agents.RealmLayers[index],
                _agents.CultivationProgress[index],
                _agents.CultivationStates[index],
                _agents.CultivationBottleneckCauses[index],
                _agents.TechniqueQualities[index],
                _agents.TechniqueMasteries[index],
                _agents.SpellQualities[index],
                _agents.SpellMasteries[index],
                _agents.CombatExperience[index],
                _agents.Wins[index],
                _agents.Losses[index],
                _agents.Health[index],
                _agents.MaximumHealth[index],
                _agents.Qi[index],
                _agents.MaximumQi[index],
                _agents.Activities[index],
                _agents.NextDecisionTicks[index],
                _agents.LastActionMonths[index],
                _agents.LastActionTicks[index],
                _agents.ActionsTaken[index],
                _agents.TargetRanks[index],
                _agents.Opponents[index],
                _agents.LastOpponents[index],
                _agents.LastCombatTicks[index],
                _agents.CombatStartedTicks[index],
                checked((byte)Math.Min(byte.MaxValue, _agents.CombatActions[index])),
                _agents.Guarding[index],
                _agents.ChallengesIssued[index],
                _agents.SpiritStones[index],
                _agents.GrowthPills[index],
                _agents.BreakthroughPills[index],
                _agents.MissionsCompleted[index],
                _agents.ActiveMissionMaterials[index],
                _agents.CopyMaterialInventory(index),
                _agents.CopyEquippedTreasureQualities(index),
                _agents.PersonalLogs[index].ToArray(),
                _agents.Chronicles[index].ToArray(),
                new CultivationCheckpoint(
                    _agents.PendingCultivationWork[index],
                    _agents.LastCultivationWorkBefore[index],
                    _agents.LastCultivationWorkGain[index],
                    _agents.FloorNineCompleted[index],
                    _agents.GreatPerfectionAchieved[index],
                    _agents.EntryGatePassMasks[index],
                    _agents.FoundationRoutes[index],
                    _agents.CopyCultivationComponentValues(index),
                    _agents.CopyCultivationComponentLimits(index),
                    _agents.CopyFormationValues(index),
                    _agents.CultivationTraces[index],
                    _agents.CopyEntryGateTemperings(index),
                    _agents.CultivationAuthorityRevisions[index],
                    _agents.FoundationFormations.CreateCheckpoint(index)),
                new SkillProgressCheckpoint(
                    _agents.TechniqueTrainingExperience[index],
                    _agents.TechniqueUnderstanding[index],
                    _agents.SpellTrainingExperience[index],
                    _agents.SpellUnderstanding[index]),
                new CombatAuthorityCheckpoint(
                    _agents.CombatActions[index],
                    _agents.CombatTurnOwners[index],
                    _agents.InjuryPoints[index]),
                new MapAgentCheckpoint(
                    _agents.LocationIds[index],
                    _agents.TravelDestinationIds[index],
                    _agents.MonsterHunts[index],
                    _agents.MonsterVictories[index]),
                new CombatProgressCheckpoint(
                    _agents.CombatUnderstanding[index]),
                new AptitudeDetailCheckpoint(
                    AptitudeRules.FormulaRevision,
                    RequiresReview: true,
                    _agents.CopyAptitudeComponents(index)),
                new PersonIdentityCheckpoint(
                    _agents.SoulIdentityIds[index],
                    _agents.BodyIdentityIds[index],
                    _agents.PersonIdentityIds[index],
                    _agents.LifeStates[index]),
                new AgentDecisionCheckpoint(
                    _agents.DecisionAuthorityRevisions[index],
                    _agents.DecisionTraces[index]),
                new TeachingCheckpoint(
                    _agents.TeachingAuthorityRevisions[index],
                    _agents.TeachingTraces[index],
                    _agents.LearningAuthorityRevisions[index],
                    _agents.LearningTraces[index],
                    _agents.TechniqueMisunderstandingPoints[index],
                    _agents.SpellMisunderstandingPoints[index]),
                _agents.GetLifeAuthorityCheckpoint(index));
        }

        return new WorldCheckpoint(
            WorldCheckpoint.CurrentSchemaVersion,
            Seed,
            _random.State,
            Tick.Value,
            agents,
            _events.ToArray(),
            _eventSequence,
            _chronicleSequence,
            _personalLogSequence,
            _totalBreakthroughs,
            _totalChallenges,
            _totalCombatsStarted,
            _totalCombatsCompleted,
            _activeCombats,
            _totalDeaths,
            _totalReincarnations,
            _totalMissionsCompleted,
            _totalTreasuresCrafted,
            _totalPillsCrafted,
            _totalMaterialsSold,
            _lastRankingRefreshTick,
            _rankingMovementFromTick,
            _rankingRisenCount,
            _rankingFallenCount,
            _rankingUnchangedCount,
            _rankingTotalAbsoluteMovement,
            _market.CreateCheckpoint(),
            new WorldMapCheckpoint(
                _totalMonsterHunts,
                _totalMonsterVictories,
                _monsterPopulations.ToArray(),
                _lastMonsterRegenerationMonth),
            _eliteCombatChangedCounts.ToArray(),
            _eliteRealmChangedCounts.ToArray(),
            Array.Empty<WorldEvent>(),
            _rankingHistory.CreateCheckpoint(),
            _worldChronicle.CreateCheckpoint(),
            _relations.CreateCheckpoint(),
            _reincarnation.CreateCheckpoint(),
            _causal.CreateCheckpoint(),
            _atlas.CreateCheckpoint(),
            _balance);
    }

    private void RestoreAgent(int index, AgentCheckpoint checkpoint, int schemaVersion)
    {
        _agents.Names[index] = checkpoint.Name;
        _agents.SpiritualRoots[index] = checkpoint.SpiritualRoot;
        _agents.Bones[index] = checkpoint.Bone;
        _agents.Constitutions[index] = checkpoint.Constitution;
        _agents.Comprehensions[index] = checkpoint.Comprehension;
        _agents.Minds[index] = checkpoint.Mind;
        _agents.Souls[index] = checkpoint.Soul;
        if (schemaVersion >= 18)
        {
            _agents.RestoreAptitudeComponents(
                index,
                checkpoint.AptitudeDetails!.InnateComponents);
        }
        else
        {
            _agents.RestoreLegacyAptitudeComponents(
                index,
                new Aptitudes(
                    new QualityValue(checkpoint.SpiritualRoot),
                    new QualityValue(checkpoint.Bone),
                    new QualityValue(checkpoint.Constitution),
                    new QualityValue(checkpoint.Comprehension),
                    new QualityValue(checkpoint.Mind),
                    new QualityValue(checkpoint.Soul)));
        }
        _agents.BirthTicks[index] = checkpoint.BirthTick;
        _agents.LifeNumbers[index] = checkpoint.LifeNumber;
        if (schemaVersion >= 20 && checkpoint.Identity is { } identity)
        {
            _agents.SoulIdentityIds[index] = identity.SoulId;
            _agents.BodyIdentityIds[index] = identity.BodyId;
            _agents.PersonIdentityIds[index] = identity.PersonId;
            _agents.LifeStates[index] = identity.LifeState;
        }
        else
        {
            _agents.SoulIdentityIds[index] = PersonCausalRules.CreateSoulId(index);
            _agents.BodyIdentityIds[index] = PersonCausalRules.CreateBodyId(index, checkpoint.LifeNumber);
            _agents.PersonIdentityIds[index] = PersonCausalRules.CreatePersonId(index, checkpoint.LifeNumber);
            _agents.LifeStates[index] = checkpoint.Activity == AgentActivity.Reincarnating
                ? PersonLifeState.ReincarnationRiver
                : PersonLifeState.Living;
        }
        if (schemaVersion >= 25 && checkpoint.LifeAuthority is { } lifeAuthority)
        {
            _agents.RestoreLifeAuthority(index, lifeAuthority);
        }
        else
        {
            _agents.RestoreLegacyLifeAuthority(index, Tick.Value);
        }
        _agents.BottleneckFailures[index] = checkpoint.BottleneckFailures;
        _agents.RealmStages[index] = checkpoint.RealmStage;
        var migratedFloor = schemaVersion == 7
            ? MigrateLegacyFloor(checkpoint.RealmLayer)
            : checkpoint.RealmLayer;
        _agents.RealmLayers[index] = migratedFloor;
        _agents.CultivationProgress[index] = schemaVersion == 7
            ? MigrateLegacyCultivationWork(
                checkpoint.RealmStage,
                checkpoint.RealmLayer,
                migratedFloor,
                checkpoint.CultivationProgress)
            : checkpoint.CultivationProgress;
        _agents.CultivationStates[index] = checkpoint.CultivationState;
        _agents.CultivationBottleneckCauses[index] = checkpoint.CultivationBottleneckCause;
        var cultivation = checkpoint.Cultivation;
        if (cultivation is { } current)
        {
            _agents.PendingCultivationWork[index] = current.PendingCarryoverWork;
            _agents.LastCultivationWorkBefore[index] = current.LastWorkBefore;
            _agents.LastCultivationWorkGain[index] = current.LastWorkGain;
            _agents.FloorNineCompleted[index] = current.FloorNineCompleted;
            _agents.GreatPerfectionAchieved[index] = current.GreatPerfectionAchieved;
            _agents.EntryGatePassMasks[index] = current.EntryGatePassMask;
            _agents.FoundationRoutes[index] = current.FoundationRoute;
            _agents.RestoreCultivationArrays(
                index,
                current.ComponentValues,
                current.ComponentLimits,
                current.FormationValues,
                current.EntryGateTempering);
            _agents.CultivationTraces[index] = current.LatestTrace;
            _agents.FoundationFormations.Restore(index, current.FoundationFormation);
            _agents.CultivationAuthorityRevisions[index] = schemaVersion >= 19
                ? current.AuthorityRevision
                : Math.Max(0, current.LatestTrace.AuthorityRevisionAfter);
        }
        else
        {
            var legacyCompleted = checkpoint.CultivationState == CultivationState.Completed;
            _agents.FloorNineCompleted[index] = legacyCompleted;
            _agents.GreatPerfectionAchieved[index] = legacyCompleted;
            _agents.EntryGatePassMasks[index] = 0b111;
            _agents.FoundationFormations.Reset(index);
            _agents.CultivationTraces[index] = CultivationTraceState.Empty;
            _agents.CultivationAuthorityRevisions[index] = 0;
            InitializeRealmComponents(index);
        }
        _agents.TechniqueQualities[index] = checkpoint.TechniqueQuality;
        _agents.TechniqueMasteries[index] = schemaVersion < 9
            ? checked((ushort)Math.Min(10_000, checkpoint.TechniqueMastery * 10))
            : checkpoint.TechniqueMastery;
        _agents.SpellQualities[index] = checkpoint.SpellQuality;
        _agents.SpellMasteries[index] = schemaVersion < 9
            ? checked((ushort)Math.Min(10_000, checkpoint.SpellMastery * 10))
            : checkpoint.SpellMastery;
        if (checkpoint.SkillProgress is { } skillProgress)
        {
            _agents.TechniqueTrainingExperience[index] = skillProgress.TechniqueExperience;
            _agents.TechniqueUnderstanding[index] = skillProgress.TechniqueUnderstanding;
            _agents.SpellTrainingExperience[index] = skillProgress.SpellExperience;
            _agents.SpellUnderstanding[index] = skillProgress.SpellUnderstanding;
        }
        else
        {
            _agents.TechniqueTrainingExperience[index] = SkillProgressionRules.GetRequiredExperience(
                _agents.TechniqueMasteries[index]);
            _agents.TechniqueUnderstanding[index] = 0;
            _agents.SpellTrainingExperience[index] = SkillProgressionRules.GetRequiredExperience(
                _agents.SpellMasteries[index]);
            _agents.SpellUnderstanding[index] = 0;
        }
        _agents.CombatExperience[index] = checkpoint.CombatExperience;
        _agents.CombatUnderstanding[index] = schemaVersion >= 13
            ? checkpoint.CombatProgress!.Value.UnderstandingHundredths
            : (ushort)0;
        _agents.Wins[index] = checkpoint.Wins;
        _agents.Losses[index] = checkpoint.Losses;
        _agents.Health[index] = checkpoint.Health;
        _agents.MaximumHealth[index] = checkpoint.MaximumHealth;
        _agents.Qi[index] = checkpoint.Qi;
        _agents.MaximumQi[index] = checkpoint.MaximumQi;
        _agents.Activities[index] = checkpoint.Activity;
        if (schemaVersion >= 20 && checkpoint.Decision is { } decision)
        {
            _agents.RestoreDecision(index, decision.AuthorityRevision, decision.Latest);
        }
        else
        {
            var supportingGoal = GetSupportingGoal(
                checkpoint.Activity,
                checkpoint.CultivationState == CultivationState.Bottleneck);
            var reason = checkpoint.Activity switch
            {
                AgentActivity.Reincarnating => DecisionReasonCode.ReincarnationHandoff,
                AgentActivity.Recovering => DecisionReasonCode.ForcedRecovery,
                _ => DecisionReasonCode.GrowthBalance,
            };
            _agents.RestoreDecision(
                index,
                1,
                new AgentDecisionTraceState(
                    checkpoint.LastActionTick,
                    1,
                    AgentGoalKind.ReachRankOne,
                    supportingGoal,
                    checkpoint.Activity,
                    reason,
                    null,
                    DecisionResolutionKind.Selected));
        }
        if (schemaVersion >= 22 && checkpoint.Teaching is { } teaching)
        {
            _agents.RestoreTeaching(
                index,
                teaching.AuthorityRevision,
                teaching.Latest,
                teaching.LearningAuthorityRevision,
                teaching.LatestLearning,
                teaching.TechniqueMisunderstandingPoints,
                teaching.SpellMisunderstandingPoints);
        }
        else
        {
            _agents.RestoreTeaching(
                index,
                0,
                TeachingTraceState.Empty,
                0,
                TeachingTraceState.Empty,
                techniqueMisunderstanding: 0,
                spellMisunderstanding: 0);
        }
        _agents.NextDecisionTicks[index] = checkpoint.NextDecisionTick;
        _agents.LastActionMonths[index] = checkpoint.LastActionMonth;
        _agents.LastActionTicks[index] = checkpoint.LastActionTick;
        _agents.ActionsTaken[index] = checkpoint.ActionsTaken;
        _agents.TargetRanks[index] = checkpoint.TargetRank;
        _agents.Opponents[index] = checkpoint.Opponent;
        _agents.LastOpponents[index] = checkpoint.LastOpponent;
        _agents.LastCombatTicks[index] = checkpoint.LastCombatTick;
        _agents.CombatStartedTicks[index] = checkpoint.CombatStartedTick;
        if (checkpoint.CombatAuthority is { } combatAuthority)
        {
            _agents.CombatActions[index] = combatAuthority.ActionCount;
            _agents.CombatTurnOwners[index] = combatAuthority.TurnOwner;
            _agents.InjuryPoints[index] = combatAuthority.InjuryPoints;
        }
        else
        {
            _agents.CombatActions[index] = checkpoint.CombatActions;
            _agents.CombatTurnOwners[index] = checkpoint.Activity == AgentActivity.Fighting
                ? index
                : -1;
            _agents.InjuryPoints[index] = 0;
        }
        _agents.Guarding[index] = checkpoint.Guarding;
        _agents.ChallengesIssued[index] = checkpoint.ChallengesIssued;
        _agents.SpiritStones[index] = checkpoint.SpiritStones;
        _agents.GrowthPills[index] = checkpoint.GrowthPills;
        _agents.BreakthroughPills[index] = checkpoint.BreakthroughPills;
        _agents.MissionsCompleted[index] = checkpoint.MissionsCompleted;
        _agents.ActiveMissionMaterials[index] = schemaVersion >= 14
            || checkpoint.ActiveMissionMaterial == byte.MaxValue
                ? checkpoint.ActiveMissionMaterial
                : checkpoint.ActiveMissionMaterial < 10
                    || checkpoint.ActiveMissionMaterial is >= 20 and < 25
                        ? (byte)MaterialId.TreasureMaterial
                        : (byte)MaterialId.PillMaterial;
        if (checkpoint.Map is { } map)
        {
            _agents.LocationIds[index] = map.LocationId;
            _agents.TravelDestinationIds[index] = map.TravelDestinationId;
            _agents.MonsterHunts[index] = map.MonsterHunts;
            _agents.MonsterVictories[index] = map.MonsterVictories;
        }
        else
        {
            _agents.LocationIds[index] = WorldMapRules.CityLocationId;
            _agents.TravelDestinationIds[index] = WorldMapRules.CityLocationId;
            _agents.MonsterHunts[index] = 0;
            _agents.MonsterVictories[index] = 0;
        }
        _agents.RestoreEconomyState(
            index,
            checkpoint.MaterialInventory,
            checkpoint.EquippedTreasureQualities);
        _agents.PersonalLogs[index].AddRange(checkpoint.PersonalLog.Where(entry =>
            entry.ChronicleEventKind is not ChronicleEventKind.TechniqueEnlightenment
                and not ChronicleEventKind.SpellEnlightenment));
        _agents.Chronicles[index].AddRange(checkpoint.Chronicle.Where(entry =>
            entry.Kind is not ChronicleEventKind.TechniqueEnlightenment
                and not ChronicleEventKind.SpellEnlightenment));
    }

    private static void ValidateCheckpoint(WorldCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        WorldBalanceRules.Validate(checkpoint.Balance ?? WorldBalanceConfiguration.Default);
        if (checkpoint.SchemaVersion is < 7 or > WorldCheckpoint.CurrentSchemaVersion)
        {
            throw new InvalidDataException($"지원하지 않는 세계 schema {checkpoint.SchemaVersion}입니다.");
        }

        if (checkpoint.Agents is not { Length: > 0 } || checkpoint.Tick < 0)
        {
            throw new InvalidDataException("세계 체크포인트의 인구 또는 시간이 잘못되었습니다.");
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

        if (checkpoint.SchemaVersion >= 11
            && (checkpoint.Map is not { } map
                || map.TotalMonsterHunts < 0
                || map.TotalMonsterVictories < 0
                || map.TotalMonsterVictories > map.TotalMonsterHunts
                || (checkpoint.SchemaVersion >= 14
                    && (map.MonsterPopulations is not { } monsterPopulations
                        || (monsterPopulations.Length
                                != WorldMapRules.LocationCount * WorldMapRules.MonstersPerWildBiome
                            && !(checkpoint.Atlas is null
                                && monsterPopulations.Length
                                    == 9 * WorldMapRules.MonstersPerWildBiome))
                        || map.MonsterPopulations.Any(value =>
                            value is < 0 or > WorldMapRules.MaximumMonsterPopulation)
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
                || agent.PersonalLog.Length > PersonalLogCapacity
                || agent.Chronicle is not { Length: > 0 }
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
                && !IsValidCultivationCheckpoint(agent.Cultivation, checkpoint.SchemaVersion))
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
        int schemaVersion)
    {
        if (checkpoint is not { } value
            || value.PendingCarryoverWork < 0
            || value.LastWorkBefore < 0
            || value.LastWorkGain < 0
            || value.AuthorityRevision < 0
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

    private static bool IsValidPersonCausalCheckpoint(
        int agentIndex,
        AgentCheckpoint agent,
        long maximumTick)
    {
        if (agent.Identity is not { } identity
            || agent.Decision is not { } decision
            || identity.SoulId != PersonCausalRules.CreateSoulId(agentIndex)
            || identity.BodyId != PersonCausalRules.CreateBodyId(agentIndex, agent.LifeNumber)
            || identity.PersonId != PersonCausalRules.CreatePersonId(agentIndex, agent.LifeNumber)
            || !Enum.IsDefined(identity.LifeState)
            || (identity.LifeState == PersonLifeState.ReincarnationRiver)
                != (agent.Activity == AgentActivity.Reincarnating)
            || decision.AuthorityRevision < 1
            || decision.Latest.AuthorityRevision != decision.AuthorityRevision
            || decision.Latest.Tick < 0
            || decision.Latest.Tick > maximumTick
            || decision.Latest.PrimaryGoal != AgentGoalKind.ReachRankOne
            || !Enum.IsDefined(decision.Latest.SupportingGoal)
            || !Enum.IsDefined(decision.Latest.SelectedActivity)
            || !Enum.IsDefined(decision.Latest.SelectedReason)
            || !Enum.IsDefined(decision.Latest.Resolution)
            || (decision.Latest.AlternativeActivity is { } alternative
                && !Enum.IsDefined(alternative)))
        {
            return false;
        }

        return true;
    }

    private static bool IsValidLifeAuthorityLink(
        AgentCheckpoint agent,
        PersonLifeAuthorityCheckpoint life)
    {
        if (agent.Identity is not { } identity
            || life.LatestTransition.PersonId != identity.PersonId
            || life.LatestTransition.BodyId != identity.BodyId
            || life.LatestTransition.CurrentPhase != life.LifePhase
            || life.LatestTransition.CurrentBodyState != life.BodyState
            || life.LatestTransition.CurrentSoulState != life.SoulState)
        {
            return false;
        }

        return identity.LifeState switch
        {
            PersonLifeState.Living => life.LifePhase == CurrentLifePhase.Living
                && life.BodyState == BodyLifeState.Living
                && life.SoulState == SoulEmbodimentState.Embodied,
            PersonLifeState.ReincarnationRiver =>
                life.LifePhase == CurrentLifePhase.ReincarnationHandoff
                && life.BodyState is BodyLifeState.Dead or BodyLifeState.Destroyed
                && life.SoulState == SoulEmbodimentState.Crossing,
            _ => false,
        };
    }

    private static bool IsValidCausalRuntimeLink(WorldCheckpoint checkpoint)
    {
        var causal = checkpoint.Causal!;
        for (var agentIndex = 0; agentIndex < checkpoint.Agents.Length; agentIndex++)
        {
            var agent = checkpoint.Agents[agentIndex];
            if (agent.Identity is not { } identity || agent.Decision is not { } decision)
            {
                return false;
            }

            var path = causal.CurrentPaths[agentIndex];
            if (path.AuthorityRevision != decision.Latest.AuthorityRevision
                || path.Goal != decision.Latest.SupportingGoal
                || path.Action != decision.Latest.SelectedActivity
                || path.AlternativeAction != decision.Latest.AlternativeActivity
                || path.Reason != decision.Latest.SelectedReason
                || (path.Resolution != AgentCausalRules.MapResolution(decision.Latest.Resolution)
                    && !path.IsWaiting
                    && !path.WasInterrupted)
                || path.StartedTick != decision.Latest.Tick
                || path.StartedTick > checkpoint.Tick
                || path.GoalId != AgentCausalRules.CreateGoalId(identity.PersonId, path.Goal)
                || path.PlanId != AgentCausalRules.CreatePlanId(identity.PersonId, path.AuthorityRevision)
                || path.TaskId != AgentCausalRules.CreateTaskId(identity.PersonId, path.AuthorityRevision)
                || path.ActionId != AgentCausalRules.CreateActionId(identity.PersonId, path.AuthorityRevision)
                || path.ThreadId != AgentCausalRules.CreateThreadId(identity.PersonId)
                || path.EventDefinitionId != AgentCausalRules.CreateEventDefinitionId(path.Action)
                || path.EventInstanceId != AgentCausalRules.CreateEventInstanceId(
                    identity.PersonId,
                    path.AuthorityRevision)
                || path.ResultId != AgentCausalRules.CreateResultId(identity.PersonId, path.AuthorityRevision)
                || path.EventRecordId != AgentCausalRules.CreateEventRecordId(
                    identity.PersonId,
                    path.AuthorityRevision)
                || (path.Resolution == CausalResolutionCode.Committed
                    ? path.FailureScope != CausalFailureScope.None
                    : path.FailureScope == CausalFailureScope.None))
            {
                return false;
            }

            var factStart = agentIndex * AgentCausalRules.PerceivedFactCapacity;
            if (causal.PerceivedFactCounts[agentIndex] != AgentCausalRules.PerceivedFactCapacity)
            {
                return false;
            }

            for (var offset = 0; offset < AgentCausalRules.PerceivedFactCapacity; offset++)
            {
                var fact = causal.PerceivedFacts[factStart + offset];
                var expectedKind = (PerceivedFactKind)offset;
                if (fact.Kind != expectedKind
                    || fact.Id != AgentCausalRules.CreateFactId(identity.PersonId, expectedKind)
                    || fact.ObserverPersonId != identity.PersonId
                    || fact.SubjectPersonId != identity.PersonId
                    || fact.ObservedTick < 0
                    || fact.ObservedTick > checkpoint.Tick
                    || fact.SourceRecordId != path.EventRecordId)
                {
                    return false;
                }
            }

            var memoryStart = agentIndex * AgentCausalRules.MemoryCapacityPerAgent;
            var populated = 0;
            for (var offset = 0; offset < AgentCausalRules.MemoryCapacityPerAgent; offset++)
            {
                var memory = causal.Memories[memoryStart + offset];
                if (memory.Id.Value == 0)
                {
                    continue;
                }

                populated++;
                if (memory.LifeNumber != agent.LifeNumber
                    || memory.FirstTick < agent.BirthTick
                    || memory.LastReinforcedTick < memory.FirstTick
                    || memory.LastReinforcedTick > checkpoint.Tick
                    || memory.StrengthHundredths is 0 or > 10_000
                    || memory.ReinforcementCount == 0
                    || memory.SourceResultId.Value == 0
                    || memory.SourceRecordId.Value == 0)
                {
                    return false;
                }
            }

            if (populated != causal.MemoryCounts[agentIndex])
            {
                return false;
            }

            var claim = causal.LatestKnowledgeClaims[agentIndex];
            if (claim.Id.Value != 0
                && (claim.LifeNumber != agent.LifeNumber
                    || claim.RecipientPersonId != identity.PersonId
                    || claim.TeachingProvenanceId.Value == 0
                    || claim.SourceResultId.Value == 0
                    || claim.CreatedTick < agent.BirthTick
                    || claim.CreatedTick > checkpoint.Tick
                    || claim.AuthorityRevision <= 0))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidTeachingCheckpoint(
        int owner,
        AgentCheckpoint agent,
        WorldCheckpoint checkpoint,
        IReadOnlyDictionary<ulong, RelationStateCheckpoint> relationById)
    {
        if (agent.Teaching is not { } teaching
            || teaching.AuthorityRevision < 0
            || teaching.LearningAuthorityRevision < 0
            || teaching.AuthorityRevision == long.MaxValue
            || teaching.LearningAuthorityRevision == long.MaxValue
            || teaching.Latest.AuthorityRevision != teaching.AuthorityRevision
            || teaching.LatestLearning.AuthorityRevision != teaching.LearningAuthorityRevision
            || !IsValidTeachingRevisionParity(
                teaching.AuthorityRevision,
                teaching.Latest.IsActive)
            || !IsValidTeachingRevisionParity(
                teaching.LearningAuthorityRevision,
                teaching.LatestLearning.IsActive)
            || (agent.Activity == AgentActivity.Teaching) != teaching.Latest.IsActive
            || teaching.TechniqueMisunderstandingPoints > QualityValue.MaximumHundredths
            || teaching.SpellMisunderstandingPoints > QualityValue.MaximumHundredths
            || !TeachingRules.IsValidTrace(
                teaching.Latest,
                checkpoint.Agents.Length,
                checkpoint.Tick)
            || !TeachingRules.IsValidTrace(
                teaching.LatestLearning,
                checkpoint.Agents.Length,
                checkpoint.Tick))
        {
            return false;
        }

        return IsValidTeachingTraceOwner(
                owner,
                agent,
                teaching.Latest,
                checkpoint,
                relationById,
                ownerIsTeacher: true)
            && IsValidTeachingTraceOwner(
                owner,
                agent,
                teaching.LatestLearning,
                checkpoint,
                relationById,
                ownerIsTeacher: false);
    }

    private static bool IsValidTeachingRevisionParity(
        long revision,
        bool isActive) => revision == 0
        ? !isActive
        : isActive
            ? (revision & 1L) == 1L
            : (revision & 1L) == 0L;

    private static string DescribeTeachingCheckpoint(AgentCheckpoint agent)
    {
        if (agent.Teaching is not { } teaching)
        {
            return "Teaching 없음";
        }

        var outgoing = teaching.Latest;
        var incoming = teaching.LatestLearning;
        return $"activity={agent.Activity}, person={agent.Identity?.PersonId.Value ?? 0}, "
            + $"outRev={teaching.AuthorityRevision}/{outgoing.AuthorityRevision}, "
            + $"outActive={outgoing.IsActive}, outLearner={outgoing.LearnerAgent}, "
            + $"outTeacherPerson={outgoing.TeacherPersonId.Value}, "
            + $"outLearnerPerson={outgoing.LearnerPersonId.Value}, "
            + $"inRev={teaching.LearningAuthorityRevision}/{incoming.AuthorityRevision}, "
            + $"inActive={incoming.IsActive}, inLearner={incoming.LearnerAgent}, "
            + $"inTeacherPerson={incoming.TeacherPersonId.Value}, "
            + $"inLearnerPerson={incoming.LearnerPersonId.Value}";
    }

    private static bool IsValidTeachingTraceOwner(
        int owner,
        AgentCheckpoint ownerCheckpoint,
        TeachingTraceState trace,
        WorldCheckpoint checkpoint,
        IReadOnlyDictionary<ulong, RelationStateCheckpoint> relationById,
        bool ownerIsTeacher)
    {
        if (trace.AuthorityRevision == 0)
        {
            return true;
        }

        var teacher = ownerIsTeacher
            ? owner
            : RelationRules.GetOtherAgent(trace.RelationId, trace.LearnerAgent);
        relationById.TryGetValue(trace.RelationId.Value, out var relationState);
        if (teacher < 0
            || teacher >= checkpoint.Agents.Length
            || trace.LearnerAgent == teacher
            || trace.RelationId != RelationRules.CreateId(teacher, trace.LearnerAgent)
            || trace.TargetKnowledgeRef != TeachingRules.CreateKnowledgeRef(
                trace.TeacherPersonId,
                trace.TargetKind,
                trace.SourceQuality)
            || trace.ProvenanceId != TeachingRules.CreateProvenanceId(
                trace.RelationId,
                trace.TargetKnowledgeRef,
                trace.StartedTick)
            || !IsValidHistoricalPersonId(
                trace.TeacherPersonId,
                teacher,
                checkpoint.Agents[teacher].LifeNumber)
            || !IsValidHistoricalPersonId(
                trace.LearnerPersonId,
                trace.LearnerAgent,
                checkpoint.Agents[trace.LearnerAgent].LifeNumber)
            || relationState is null
            || (ownerIsTeacher
                ? trace.TeacherPersonId != ownerCheckpoint.Identity!.Value.PersonId
                : trace.LearnerAgent != owner
                    || trace.LearnerPersonId != ownerCheckpoint.Identity!.Value.PersonId))
        {
            return false;
        }

        if (!trace.IsActive)
        {
            return true;
        }

        if (!IsValidActiveTeachingRuntimeState(
                trace,
                teacher,
                relationState,
                checkpoint))
        {
            return false;
        }

        var counterpart = ownerIsTeacher
            ? checkpoint.Agents[trace.LearnerAgent].Teaching?.LatestLearning
            : checkpoint.Agents[teacher].Teaching?.Latest;
        return checkpoint.Agents[teacher].Activity == AgentActivity.Teaching
            && checkpoint.Agents[teacher].Identity is { } teacherIdentity
            && trace.TeacherPersonId == teacherIdentity.PersonId
            && checkpoint.Agents[trace.LearnerAgent].Identity is { } learnerIdentity
            && trace.LearnerPersonId == learnerIdentity.PersonId
            && counterpart is { } matching
            && AreMatchingTeachingContexts(trace, matching)
            && (relationState.Roles & RelationRole.MasterDisciple) != 0
            && relationState.MasterAgent == teacher;
    }

    private static bool IsValidActiveTeachingRuntimeState(
        TeachingTraceState trace,
        int teacher,
        RelationStateCheckpoint relation,
        WorldCheckpoint checkpoint)
    {
        if (trace.AuthorityRevision == long.MaxValue
            || trace.SessionsCompleted == int.MaxValue
            || trace.LastResult != TeachingResultKind.None
            || trace.LastResultTick != 0
            || trace.LastGain != 0
            || checkpoint.Agents[trace.LearnerAgent].Teaching is not { } learnerTeaching)
        {
            return false;
        }

        var expectedQuality = trace.TargetKind == TeachingTargetKind.Technique
            ? checkpoint.Agents[teacher].TechniqueQuality
            : checkpoint.Agents[teacher].SpellQuality;
        var expectedMisunderstanding = trace.TargetKind == TeachingTargetKind.Technique
            ? learnerTeaching.TechniqueMisunderstandingPoints
            : learnerTeaching.SpellMisunderstandingPoints;
        var storedKnowledgeRef = trace.TargetKind == TeachingTargetKind.Technique
            ? relation.TechniqueKnowledgeRef
            : relation.SpellKnowledgeRef;
        var expectedSessions = storedKnowledgeRef == trace.TargetKnowledgeRef
            ? trace.TargetKind == TeachingTargetKind.Technique
                ? relation.TechniqueTeachingSessions
                : relation.SpellTeachingSessions
            : 0;
        var expectedTransmission = storedKnowledgeRef == trace.TargetKnowledgeRef
            && (trace.TargetKind == TeachingTargetKind.Technique
                ? relation.TechniqueTransmissionEstablished
                : relation.SpellTransmissionEstablished);
        return trace.SourceQuality == expectedQuality
            && trace.MisunderstandingPoints == expectedMisunderstanding
            && trace.SessionsCompleted == expectedSessions
            && trace.TransmissionEstablished == expectedTransmission;
    }

    private static bool AreMatchingTeachingContexts(
        TeachingTraceState left,
        TeachingTraceState right) =>
        left.IsActive
        && right.IsActive
        && left.LearnerAgent == right.LearnerAgent
        && left.TeacherPersonId == right.TeacherPersonId
        && left.LearnerPersonId == right.LearnerPersonId
        && left.RelationId == right.RelationId
        && left.TargetKind == right.TargetKind
        && left.SourceQuality == right.SourceQuality
        && left.TargetKnowledgeRef == right.TargetKnowledgeRef
        && left.ProvenanceId == right.ProvenanceId
        && left.StartedTick == right.StartedTick
        && left.LastResultTick == right.LastResultTick
        && left.LastResult == right.LastResult
        && left.LastGain == right.LastGain
        && left.SessionsCompleted == right.SessionsCompleted
        && left.MisunderstandingPoints == right.MisunderstandingPoints
        && left.TransmissionEstablished == right.TransmissionEstablished;

    private static bool IsValidHistoricalPersonId(
        PersonIdentityId personId,
        int agent,
        int currentLifeNumber)
    {
        var encodedAgent = (uint)personId.Value;
        var lifeNumber = personId.Value >> 32;
        return encodedAgent == checked((uint)agent + 1U)
            && lifeNumber >= 1
            && lifeNumber <= (ulong)currentLifeNumber;
    }

    private static bool IsValidWorldEvent(
        WorldEvent entry,
        long maximumSequence,
        long maximumTick,
        int population) =>
        entry.Sequence is >= 1
        && entry.Sequence <= maximumSequence
        && entry.Tick.Value >= 0
        && entry.Tick.Value <= maximumTick
        && Enum.IsDefined(entry.Kind)
        && entry.PrimaryAgent.Value >= 0
        && entry.PrimaryAgent.Value < population
        && (entry.SecondaryAgent is not { } secondary
            || (secondary.Value >= 0 && secondary.Value < population));

    private static bool IsStrictlyIncreasingSequence(WorldEvent[] events)
    {
        for (var index = 1; index < events.Length; index++)
        {
            if (events[index - 1].Sequence >= events[index].Sequence)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidWorldCenturyChronicle(
        WorldCenturyChronicleCheckpoint? checkpoint,
        long maximumTick,
        int population)
    {
        if (checkpoint is not { } value
            || value.Sequence < 0
            || value.Entries is null
            || value.Current is null)
        {
            return false;
        }

        var currentYear = GameCalendar.GetYear(new SimulationTick(maximumTick));
        var expectedCenturyIndex = currentYear / WorldCenturyChronicle.YearsPerVolume;
        if (value.Sequence != value.Entries.Length
            || value.Entries.LongLength != expectedCenturyIndex)
        {
            return false;
        }

        for (var index = 0; index < value.Entries.Length; index++)
        {
            var entry = value.Entries[index];
            var expectedStartYear = checked((long)index * WorldCenturyChronicle.YearsPerVolume);
            if (entry is null
                || entry.Sequence != index + 1L
                || entry.CenturyNumber != index + 1L
                || entry.StartYear != expectedStartYear
                || entry.EndYear != expectedStartYear + WorldCenturyChronicle.YearsPerVolume - 1
                || !IsValidCenturyRealmCounts(
                    entry.RealmBreakthroughCounts,
                    entry.RealmBreakthroughUniqueCounts,
                    population)
                || !IsValidPositionChangeTotals(entry.CombatPowerPositionChanges)
                || !IsValidPositionChangeTotals(entry.RealmPositionChanges)
                || !IsValidPositionChangeTotals(entry.AptitudePositionChanges)
                || !IsValidPositionChangeTotals(entry.WealthPositionChanges)
                || !IsValidChronicleLeader(entry.CombatPowerLeader, population)
                || !IsValidChronicleLeader(entry.RealmLeader, population)
                || !IsValidChronicleLeader(entry.AptitudeLeader, population)
                || !IsValidChronicleLeader(entry.WealthLeader, population)
                || entry.DeathCount < 0
                || entry.UniqueDeathCount is < 0 || entry.UniqueDeathCount > entry.DeathCount
                || entry.ReincarnationEntryCount < 0
                || entry.UniqueReincarnationEntryCount is < 0
                    || entry.UniqueReincarnationEntryCount > entry.ReincarnationEntryCount
                || entry.RebirthCount < 0
                || entry.UniqueRebirthCount is < 0 || entry.UniqueRebirthCount > entry.RebirthCount
                || entry.UniqueDeathCount > population
                || entry.UniqueReincarnationEntryCount > population
                || entry.UniqueRebirthCount > population
                || entry.ReincarnationRiverResidentsAtEnd is < 0
                    || entry.ReincarnationRiverResidentsAtEnd > population
                || entry.CombatsCompleted < 0
                || entry.MonsterHunts < 0
                || entry.MissionsCompleted < 0
                || entry.SpiritStonesMined < 0
                || entry.TreasuresCrafted < 0
                || entry.PillsCrafted < 0)
            {
                return false;
            }
        }

        var current = value.Current;
        var wordCount = checked((population + 63) / 64);
        if (current.CenturyIndex != expectedCenturyIndex
            || !IsValidCenturyRealmCounts(
                current.RealmBreakthroughCounts,
                current.RealmBreakthroughUniqueCounts,
                population)
            || current.RealmBreakthroughAgentBits is not { Length: var realmBitCount }
            || realmBitCount != RealmRules.All.Count
            || current.RealmBreakthroughAgentBits.Any(bits =>
                !IsValidAgentBits(bits, wordCount, population))
            || !IsValidPositionChangeTotals(current.CombatPowerPositionChanges)
            || !IsValidPositionChangeTotals(current.RealmPositionChanges)
            || !IsValidPositionChangeTotals(current.AptitudePositionChanges)
            || !IsValidPositionChangeTotals(current.WealthPositionChanges)
            || !IsValidRankingCache(current.PreviousCombatPowerTop, population)
            || !IsValidRankingCache(current.PreviousRealmTop, population)
            || !IsValidRankingCache(current.PreviousAptitudeTop, population)
            || !IsValidRankingCache(current.PreviousWealthTop, population)
            || !IsValidChronicleLeader(current.CombatPowerLeader, population)
            || !IsValidChronicleLeader(current.RealmLeader, population)
            || !IsValidChronicleLeader(current.AptitudeLeader, population)
            || !IsValidChronicleLeader(current.WealthLeader, population)
            || current.LastRankingSampleYear is < 0
            || current.LastRankingSampleYear > currentYear
            || current.DeathCount < 0
            || current.UniqueDeathCount is < 0 || current.UniqueDeathCount > current.DeathCount
            || !IsValidAgentBits(current.DeathAgentBits, wordCount, population)
            || current.ReincarnationEntryCount < 0
            || current.UniqueReincarnationEntryCount is < 0
                || current.UniqueReincarnationEntryCount > current.ReincarnationEntryCount
            || !IsValidAgentBits(current.ReincarnationEntryAgentBits, wordCount, population)
            || current.RebirthCount < 0
            || current.UniqueRebirthCount is < 0 || current.UniqueRebirthCount > current.RebirthCount
            || !IsValidAgentBits(current.RebirthAgentBits, wordCount, population)
            || current.ReincarnationRiverResidents is < 0
                || current.ReincarnationRiverResidents > population
            || current.CombatsCompleted < 0
            || current.MonsterHunts < 0
            || current.MissionsCompleted < 0
            || current.SpiritStonesMined < 0
            || current.TreasuresCrafted < 0
            || current.PillsCrafted < 0)
        {
            return false;
        }

        for (var realm = 0; realm < RealmRules.All.Count; realm++)
        {
            if (WorldCenturyChronicle.CountSetBits(current.RealmBreakthroughAgentBits[realm])
                    != current.RealmBreakthroughUniqueCounts[realm])
            {
                return false;
            }
        }

        return WorldCenturyChronicle.CountSetBits(current.DeathAgentBits)
                == current.UniqueDeathCount
            && WorldCenturyChronicle.CountSetBits(current.ReincarnationEntryAgentBits)
                == current.UniqueReincarnationEntryCount
            && WorldCenturyChronicle.CountSetBits(current.RebirthAgentBits)
                == current.UniqueRebirthCount;
    }

    private static bool IsValidCenturyRealmCounts(
        int[]? counts,
        int[]? uniqueCounts,
        int population) =>
        counts is { Length: var countLength }
        && countLength == RealmRules.All.Count
        && uniqueCounts is { Length: var uniqueLength }
        && uniqueLength == RealmRules.All.Count
        && counts.All(value => value >= 0)
        && uniqueCounts.All(value => value is >= 0 && value <= population)
        && counts.Zip(uniqueCounts).All(pair => pair.First >= pair.Second);

    private static bool IsValidPositionChangeTotals(long[]? values) =>
        values is { Length: WorldCenturyChronicle.RankingTierCount }
        && values.All(value => value >= 0);

    private static bool IsValidChronicleLeader(WorldChronicleLeader leader, int population) =>
        leader.AgentId.Value is >= 0
        && leader.AgentId.Value < population
        && !string.IsNullOrWhiteSpace(leader.Name);

    private static bool IsValidRankingCache(int[]? agents, int population) =>
        agents is { Length: > 0 and <= WorldCenturyChronicle.RankingCacheSize }
        && agents.All(agent => agent is >= 0 && agent < population)
        && agents.Distinct().Count() == agents.Length;

    private static bool IsValidAgentBits(ulong[]? bits, int wordCount, int population)
    {
        if (bits is not { Length: var length } || length != wordCount)
        {
            return false;
        }

        var usedBits = population & 63;
        if (usedBits == 0)
        {
            return true;
        }

        var validMask = (1UL << usedBits) - 1;
        return (bits[^1] & ~validMask) == 0;
    }

    private static bool IsValidRankingHistory(
        RankingHistoryCheckpoint? checkpoint,
        long maximumTick,
        int population,
        bool requirePositionChangeTotals)
    {
        if (checkpoint?.Frames is not { Length: > 0 and <= 64 } frames)
        {
            return false;
        }

        var expectedLength = Math.Min(RankingHistory.MaximumTrackedRank, population);
        var previousTick = -1L;
        long[]? previousCombatTotals = null;
        long[]? previousRealmTotals = null;
        long[]? previousWealthTotals = null;
        foreach (var frame in frames)
        {
            if (frame.Tick <= previousTick
                || frame.Tick > maximumTick
                || frame.CombatPowerTop is not { Length: var combatLength }
                || combatLength != expectedLength
                || frame.RealmTop is not { Length: var realmLength }
                || realmLength != expectedLength
                || frame.WealthTop is not { Length: var wealthLength }
                || wealthLength != expectedLength
                || !IsValidRankingFrame(frame.CombatPowerTop, population)
                || !IsValidRankingFrame(frame.RealmTop, population)
                || !IsValidRankingFrame(frame.WealthTop, population)
                || !IsValidPositionChangeTotals(
                    frame.CombatPowerPositionChangeTotals,
                    previousCombatTotals,
                    requirePositionChangeTotals)
                || !IsValidPositionChangeTotals(
                    frame.RealmPositionChangeTotals,
                    previousRealmTotals,
                    requirePositionChangeTotals)
                || !IsValidPositionChangeTotals(
                    frame.WealthPositionChangeTotals,
                    previousWealthTotals,
                    requirePositionChangeTotals))
            {
                return false;
            }

            previousTick = frame.Tick;
            previousCombatTotals = frame.CombatPowerPositionChangeTotals;
            previousRealmTotals = frame.RealmPositionChangeTotals;
            previousWealthTotals = frame.WealthPositionChangeTotals;
        }

        return true;
    }

    private static bool IsValidRankingFrame(int[] ranking, int population)
    {
        var seen = new HashSet<int>();
        return ranking.All(agent => agent >= 0 && agent < population && seen.Add(agent));
    }

    private static bool IsValidPositionChangeTotals(
        long[]? totals,
        long[]? previousTotals,
        bool required)
    {
        if (totals is null)
        {
            return !required;
        }
        if (totals.Length != 7 || totals.Any(value => value < 0))
        {
            return false;
        }

        return previousTotals is null
            || totals.Zip(previousTotals).All(pair => pair.First >= pair.Second);
    }
}
