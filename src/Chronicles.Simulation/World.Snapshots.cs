using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed partial class World
{
    public AgentSnapshot GetAgentSnapshot(
        AgentId agentId,
        bool includeAptitudeDetails = false,
        bool includeRelations = false,
        bool includeTeaching = false,
        bool includeCausal = false,
        CultivationProjectionSections cultivationSections = CultivationProjectionSections.Pipeline)
    {
        var index = ValidateAgent(agentId);
        var combatAptitudes = _agents.GetContextAptitudes(index, combat: true);
        var aptitudeBonuses = AptitudeCombatBonusFormula.Calculate(
            _agents.GetAptitudeRoleScores(index));
        return new AgentSnapshot(
            agentId,
            _agents.Names[index],
            _rankByAgent[index],
            _realmRankByAgent[index],
            _aptitudeRankByAgent[index],
            _wealthRankByAgent[index],
            _agents.TargetRanks[index],
            GetAgeYears(index),
            RealmRules.GetProfile(_agents.RealmStages[index]).MaximumLifespanYears,
            _agents.LifeNumbers[index],
            _agents.RealmStages[index],
            _agents.RealmLayers[index],
            _agents.CultivationProgress[index],
            GetCultivationRequirement(index),
            _agents.CultivationStates[index],
            _agents.CultivationBottleneckCauses[index],
            _agents.GetAptitudes(index),
            new QualityValue(_agents.TechniqueQualities[index]),
            _agents.TechniqueMasteries[index],
            _agents.TechniqueTrainingExperience[index],
            _agents.TechniqueUnderstanding[index],
            new QualityValue(_agents.SpellQualities[index]),
            _agents.SpellMasteries[index],
            _agents.SpellTrainingExperience[index],
            _agents.SpellUnderstanding[index],
            _agents.CombatExperience[index],
            CombatProgressionRules.GetMasteryHundredths(_agents.CombatExperience[index]),
            _agents.CombatUnderstanding[index],
            _agents.Wins[index],
            _agents.Losses[index],
            _agents.Health[index],
            _agents.MaximumHealth[index],
            _agents.Qi[index],
            _agents.MaximumQi[index],
            _agents.CombatAttributes[index],
            _agents.InjuryPoints[index],
            CombatInjuryRules.GetSeverity(_agents.InjuryPoints[index]),
            _agents.Activities[index],
            new SimulationTick(_agents.NextDecisionTicks[index]),
            _agents.Opponents[index] < 0 ? null : new AgentId(_agents.Opponents[index]),
            new SimulationTick(_agents.LastCombatTicks[index]),
            _agents.ChallengesIssued[index],
            _agents.ActionsTaken[index],
            _agents.LastActionMonths[index] < 0
                ? null
                : GameCalendar.GetDate(new SimulationTick(_agents.LastActionTicks[index])),
            _agents.LocationIds[index],
            _agents.TravelDestinationIds[index],
            _agents.MonsterHunts[index],
            _agents.MonsterVictories[index],
            _agents.SpiritStones[index],
            _agents.GrowthPills[index],
            _agents.BreakthroughPills[index],
            _agents.MissionsCompleted[index],
            GetActiveMissionSnapshot(index),
            _agents.GetInventoryItemCount(index),
            GetMaterialHoldingSnapshots(index),
            GetEquippedTreasureSnapshots(index),
            _agents.Power[index],
            CreateCultivationProjection(index, cultivationSections),
            CombatPowerFormula.CalculateDetailed(
                _agents.RealmStages[index],
                _agents.RealmLayers[index],
                combatAptitudes,
                aptitudeBonuses,
                new QualityValue(_agents.TechniqueQualities[index]),
                _agents.TechniqueMasteries[index],
                _agents.TechniqueUnderstanding[index],
                new QualityValue(_agents.SpellQualities[index]),
                _agents.SpellMasteries[index],
                _agents.SpellUnderstanding[index],
                _agents.CombatExperience[index],
                CombatProgressionRules.GetMasteryHundredths(_agents.CombatExperience[index]),
                _agents.CombatUnderstanding[index],
                _agents.GetTreasurePower(index)),
            _agents.GetPersonIdentity(index),
            _agents.GetDecisionSnapshot(index),
            includeTeaching
                ? CreateTeachingSnapshot(index)
                : null,
            includeAptitudeDetails
                ? _agents.CreateAptitudeDetailSnapshot(index)
                : null,
            includeRelations
                ? CreateRelationSnapshots(index)
                : null,
            _agents.GetLifeAuthoritySnapshot(index),
            includeCausal
                ? _causal.GetSnapshot(index)
                : null,
            _atlas.GetKnowledgeSnapshot(index, _agents.LocationIds[index]),
            _atlas.GetRoute(index));
    }

    public WorldSnapshot CreateSnapshot(
        int rankingCount = 1_000,
        int eventCount = 100,
        ObservationSections sections = ObservationSections.All,
        ushort? detailedMapLocationId = null,
        int detailedMapAgentOffset = 0,
        int worldChronicleCount = int.MaxValue,
        int contentSecretRealmOffset = 0,
        int contentSecretRealmLimit = 100,
        int knowledgeEditionOffset = 0,
        int knowledgeEditionLimit = 100,
        int lawCandidateOffset = 0,
        int lawCandidateLimit = 54,
        int contentItemOffset = 0,
        int contentItemLimit = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rankingCount);
        ArgumentOutOfRangeException.ThrowIfNegative(eventCount);
        ArgumentOutOfRangeException.ThrowIfNegative(worldChronicleCount);
        ArgumentOutOfRangeException.ThrowIfNegative(contentSecretRealmOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(contentSecretRealmLimit);
        ArgumentOutOfRangeException.ThrowIfNegative(knowledgeEditionOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(knowledgeEditionLimit);
        ArgumentOutOfRangeException.ThrowIfNegative(lawCandidateOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(lawCandidateLimit);
        ArgumentOutOfRangeException.ThrowIfNegative(contentItemOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(contentItemLimit);
        var actualRankingCount = Math.Min(rankingCount, Population);
        var combatPowerRankings = (sections & ObservationSections.CombatPowerRanking) != 0
            ? CreateRankingEntries(
                RankingCategory.CombatPower,
                _agentByRank,
                actualRankingCount)
            : Array.Empty<RankingEntry>();
        var realmRankings = (sections & ObservationSections.RealmRanking) != 0
            ? CreateRankingEntries(
                RankingCategory.Realm,
                _agentByRealmRank,
                actualRankingCount)
            : Array.Empty<RankingEntry>();
        var aptitudeRankings = (sections & ObservationSections.AptitudeRanking) != 0
            ? CreateRankingEntries(
                RankingCategory.Aptitude,
                _agentByAptitudeRank,
                actualRankingCount)
            : Array.Empty<RankingEntry>();
        var wealthRankings = (sections & ObservationSections.WealthRanking) != 0
            ? CreateRankingEntries(
                RankingCategory.Wealth,
                _agentByWealthRank,
                actualRankingCount)
            : Array.Empty<RankingEntry>();
        var requestedStatistics = sections & ObservationSections.Statistics;
        var statistics = requestedStatistics != ObservationSections.None
            ? CreateStatistics(requestedStatistics)
            : WorldStatistics.Empty;
        var market = (sections & ObservationSections.Market) != 0
            ? _market.CreateSnapshot((sections & ObservationSections.MarketGoods) != 0)
            : CityMarketSnapshot.Empty;
        var map = (sections & ObservationSections.Map) != 0
            ? CreateMapSnapshot(detailedMapLocationId, detailedMapAgentOffset)
            : WorldMapSnapshot.Empty;
        var organizations = (sections & ObservationSections.Organization) != 0
            ? _organizations.CreateSnapshot(_agents.Names)
            : WorldOrganizationSnapshot.Empty;
        var content = (sections & ObservationSections.Content) != 0
            ? _content.CreateSnapshot(
                ContentDiscovererRef.Observer,
                includeHidden: _balance.DeveloperModeEnabled,
                contentSecretRealmOffset,
                contentSecretRealmLimit,
                contentItemOffset,
                contentItemLimit) with
                {
                    Knowledge = _knowledge.CreateSnapshot(
                        knowledgeEditionOffset,
                        knowledgeEditionLimit),
                    Laws = _laws.CreateSnapshot(
                        lawCandidateOffset,
                        lawCandidateLimit),
                }
            : WorldContentSnapshot.Empty;

        var recentEvents = (sections & ObservationSections.WorldLog) != 0
            ? _events.Skip(Math.Max(0, _events.Count - eventCount)).ToArray()
            : Array.Empty<WorldEvent>();
        var permanentWorldChronicle = (sections & ObservationSections.WorldChronicle) != 0
            ? _worldChronicle.GetTail(worldChronicleCount)
            : Array.Empty<WorldCenturyChronicleEntry>();
        var agentNames = new Dictionary<int, string>(actualRankingCount * 4);
        foreach (var ranking in combatPowerRankings
                     .Concat(realmRankings)
                     .Concat(aptitudeRankings)
                     .Concat(wealthRankings))
        {
            agentNames[ranking.AgentId.Value] = ranking.Name;
        }

        return new WorldSnapshot(
            Tick,
            Population,
            combatPowerRankings,
            realmRankings,
            aptitudeRankings,
            wealthRankings,
            statistics,
            market,
            map,
            agentNames,
            recentEvents,
            permanentWorldChronicle,
            _worldChronicle.Count,
            _totalBreakthroughs,
            _totalChallenges,
            _totalCombatsStarted,
            _totalCombatsCompleted,
            _activeCombats,
            _totalDeaths,
            _totalReincarnations,
            LeaderId,
            organizations,
            content);
    }

    private RankingEntry[] CreateRankingEntries(
        RankingCategory category,
        int[] orderedAgents,
        int count)
    {
        var entries = new RankingEntry[count];
        for (var offset = 0; offset < count; offset++)
        {
            var agentIndex = orderedAgents[offset];
            var aptitudeScore = _agents.GetAptitudes(agentIndex).RankingScore;
            var score = category switch
            {
                RankingCategory.CombatPower => _agents.Power[agentIndex].Total,
                RankingCategory.Realm => GetRealmRankingScore(agentIndex),
                RankingCategory.Aptitude => aptitudeScore,
                RankingCategory.Wealth => _agents.SpiritStones[agentIndex],
                _ => throw new ArgumentOutOfRangeException(nameof(category)),
            };

            entries[offset] = new RankingEntry(
                category,
                offset + 1,
                new AgentId(agentIndex),
                _agents.Names[agentIndex],
                HasEnteredCultivation(agentIndex),
                _agents.RealmStages[agentIndex],
                _agents.RealmLayers[agentIndex],
                _agents.GreatPerfectionAchieved[agentIndex],
                _agents.Activities[agentIndex],
                _agents.Power[agentIndex],
                aptitudeScore,
                score);
        }

        return entries;
    }

}
