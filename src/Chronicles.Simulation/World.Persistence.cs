using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed partial class World
{
    private World(
        WorldCheckpoint checkpoint,
        IReadOnlyCollection<AgentId>? chronicleRetentionWatchlist)
    {
        ValidateCheckpoint(checkpoint);
        Seed = checkpoint.Seed;
        Tick = new SimulationTick(checkpoint.Tick);
        _balance = checkpoint.Balance ?? WorldBalanceConfiguration.Default;
        WorldBalanceRules.Validate(_balance);
        _generation = checkpoint.SchemaVersion >= 30
            ? WorldGenerationCatalog.Restore(checkpoint.Seed, checkpoint.Generation!)
            : WorldGenerationCatalog.Create(
                checkpoint.Seed,
                WorldGenerationConfiguration.Default);
        _random = new DeterministicRandom(checkpoint.RandomState);
        _agents = new AgentStore(checkpoint.Agents.Length);
        _decisionSchedule = new AgentDecisionSchedule(checkpoint.Agents.Length);
        _cultivationParallel = new CultivationParallelCoordinator(checkpoint.Agents.Length);
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
        _rankingWorkspace = new RankingSortWorkspace(checkpoint.Agents.Length);
        _worldChronicle = new WorldCenturyChronicle(
            checkpoint.Agents.Length,
            GameCalendar.GetYear(Tick));
        _worldChronicle.EntryRecorded += WorldChronicle_EntryRecorded;
        _monsterPopulations = checkpoint.Map?.MonsterPopulations is { } populations
            && populations.Length
                == WorldMapRules.LocationCount * _generation.Map.SpeciesPerWildBiome
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
            var preserveLegacyForHashCompatibility = checkpoint.SchemaVersion < 32
                && chronicleRetentionWatchlist is null;
            RestoreAgent(
                index,
                checkpoint.Agents[index],
                checkpoint.SchemaVersion,
                preserveLegacyForHashCompatibility
                    || chronicleRetentionWatchlist?.Contains(new AgentId(index)) == true);
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
        _organizations = checkpoint.SchemaVersion >= 26
            ? OrganizationStore.Restore(
                checkpoint.Agents.Length,
                checkpoint.Organizations!,
                trimHistory: chronicleRetentionWatchlist is not null)
            : new OrganizationStore(checkpoint.Agents.Length);
        _content = checkpoint.SchemaVersion >= 27
            ? ContentAuthorityStore.Restore(checkpoint.Content!, _generation)
            : ContentAuthorityStore.CreateInitial(_generation);
        _knowledge = checkpoint.SchemaVersion >= 28
            ? KnowledgeAuthorityStore.Restore(
                checkpoint.Knowledge!,
                checkpoint.Agents.Length,
                _content,
                _generation)
            : checkpoint.SchemaVersion == 27
                ? new KnowledgeAuthorityStore(checkpoint.Agents.Length)
                : KnowledgeAuthorityStore.CreateInitial(
                    checkpoint.Seed,
                    checkpoint.Agents.Length,
                    _content,
                    _generation);
        _laws = checkpoint.SchemaVersion >= 29
            ? LawAuthorityStore.Restore(checkpoint.Laws!, checkpoint.Agents.Length)
            : LawAuthorityStore.CreateReviewCatalog(checkpoint.Agents.Length);

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

        if (checkpoint.SchemaVersion >= 31)
        {
            RestoreDecisionRankings(checkpoint.DecisionRankings!);
            _lastMonthlyBoundaryMonth = checkpoint.DecisionRankings!.LastProcessedMonth;
        }
        else
        {
            // schema 30까지는 호출 종료 때 현재 수치로 랭킹을 정렬했다. 기존 저장은
            // 그 계약으로 한 번 재구성하고, 현재 월부터 새 월 경계 계약을 적용한다.
            RefreshRankings(recordLeaderChange: false);
            _lastMonthlyBoundaryMonth = GameCalendar.GetMonthIndex(Tick);
        }
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

    public static World Restore(
        WorldCheckpoint checkpoint,
        IReadOnlyCollection<AgentId>? chronicleRetentionWatchlist = null) =>
        new(checkpoint, chronicleRetentionWatchlist);

    internal void TrimOrganizationHistoriesForLoadedSession() =>
        _organizations.TrimHistoriesToLimit();

    private void RestoreDecisionRankings(DecisionRankingCheckpoint checkpoint)
    {
        RestoreRanking(checkpoint.CombatRanksByAgent, _rankByAgent, _agentByRank);
        RestoreRanking(checkpoint.RealmRanksByAgent, _realmRankByAgent, _agentByRealmRank);
        RestoreRanking(checkpoint.AptitudeRanksByAgent, _aptitudeRankByAgent, _agentByAptitudeRank);
        RestoreRanking(checkpoint.WealthRanksByAgent, _wealthRankByAgent, _agentByWealthRank);
        _combatPowerRankingCache.MarkSorted();
        _realmRankingCache.MarkSorted();
        _aptitudeRankingCache.MarkSorted();
        _wealthRankingCache.MarkSorted();
        if (checkpoint.CombatDirty)
        {
            _combatPowerRankingCache.MarkDirty();
        }
        if (checkpoint.RealmDirty)
        {
            _realmRankingCache.MarkDirty();
        }
        if (checkpoint.AptitudeDirty)
        {
            _aptitudeRankingCache.MarkDirty();
        }
        if (checkpoint.WealthDirty)
        {
            _wealthRankingCache.MarkDirty();
        }
    }

    private static void RestoreRanking(
        int[] sourceRanksByAgent,
        int[] ranksByAgent,
        int[] agentsByRank)
    {
        Array.Copy(sourceRanksByAgent, ranksByAgent, sourceRanksByAgent.Length);
        for (var agent = 0; agent < ranksByAgent.Length; agent++)
        {
            agentsByRank[ranksByAgent[agent] - 1] = agent;
        }
    }

    public WorldAuthorityCheckpoint CreateAuthorityCheckpoint()
    {
        var checkpoint = CreateCheckpoint();
        return new WorldAuthorityCheckpoint(checkpoint, ComputeStateHash());
    }

    internal WorldAuthorityCheckpoint CreateStreamingAuthorityCheckpoint()
    {
        var checkpoint = CreateCheckpoint(materializeHistories: false);
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

    public WorldCheckpoint CreateCheckpoint() => CreateCheckpoint(materializeHistories: true);

    private WorldCheckpoint CreateCheckpoint(bool materializeHistories)
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
                materializeHistories
                    ? _agents.PersonalLogs[index].ToArray()
                    : _agents.PersonalLogs[index],
                materializeHistories
                    ? _agents.Chronicles[index].ToArray()
                    : _agents.Chronicles[index],
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
                    _agents.FoundationFormations.CreateCheckpoint(index),
                    _agents.CultivationPlans[index],
                    _agents.CultivationExperience[index],
                    _agents.FormationInstallationExperience[index]),
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
                _agents.GetLifeAuthorityCheckpoint(index),
                _agents.PersonalCenturyChronicles.IsProtected(index),
                _agents.PersonalCenturyChronicles.CreateCompletedCheckpoint(
                    index,
                    materializeHistories),
                _agents.PersonalCenturyChronicles.CreateAccumulatorCheckpoint(index));
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
            _balance,
            _organizations.CreateCheckpoint(),
            _content.CreateCheckpoint(),
            _knowledge.CreateCheckpoint(),
            _laws.CreateCheckpoint(),
            _generation.CreateCheckpoint(),
            new DecisionRankingCheckpoint(
                _lastMonthlyBoundaryMonth,
                _rankByAgent.ToArray(),
                _realmRankByAgent.ToArray(),
                _aptitudeRankByAgent.ToArray(),
                _wealthRankByAgent.ToArray(),
                _combatPowerRankingCache.IsDirty,
                _realmRankingCache.IsDirty,
                _aptitudeRankingCache.IsDirty,
                _wealthRankingCache.IsDirty));
    }

    private void RestoreAgent(
        int index,
        AgentCheckpoint checkpoint,
        int schemaVersion,
        bool requestedChronicleProtection)
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
            _agents.CultivationPlans[index] = current.ActionPlan
                ?? CultivationActionPlanState.Empty;
            _agents.CultivationExperience[index] = current.CultivationExperience;
            _agents.FormationInstallationExperience[index] =
                current.FormationInstallationExperience;
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
            _agents.CultivationPlans[index] = CultivationActionPlanState.Empty;
            _agents.CultivationExperience[index] = 0;
            _agents.FormationInstallationExperience[index] = 0;
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

        var preserveChronicle = requestedChronicleProtection
            || (schemaVersion >= 32 && checkpoint.PreserveDetailedChronicle);
        var rebuildingLegacyArchive = schemaVersion < 32;
        _agents.PersonalCenturyChronicles.SetProtected(
            index,
            preserveChronicle || rebuildingLegacyArchive);
        foreach (var entry in checkpoint.Chronicle)
        {
            if (entry.Kind is ChronicleEventKind.TechniqueEnlightenment
                or ChronicleEventKind.SpellEnlightenment)
            {
                continue;
            }

            _agents.Chronicles[index].Add(entry);
            if (rebuildingLegacyArchive)
            {
                _agents.PersonalCenturyChronicles.Record(index, entry);
            }
        }

        if (rebuildingLegacyArchive)
        {
            _agents.PersonalCenturyChronicles.SetProtected(index, preserveChronicle);
        }
        else
        {
            _agents.PersonalCenturyChronicles.Restore(
                index,
                checkpoint.PersonalCenturyChronicle!,
                checkpoint.PersonalCenturyAccumulator);
        }
    }

}
