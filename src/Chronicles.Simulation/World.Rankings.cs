namespace Chronicles.Simulation;

/// <summary>
/// 월 경계의 의사결정 랭킹, 역순위와 상위권 변동 기록을 한 작업집합으로 유지한다.
/// 관찰 query는 이 파생 cache를 읽기만 하며 정렬이나 권위 전이를 유발하지 않는다.
/// </summary>
public sealed partial class World
{
    private void RefreshRankings(bool recordLeaderChange)
    {
        var previousLeader = _agentByRank[0];
        Span<int> previousCombatTop = stackalloc int[Math.Min(10, Population)];
        Span<int> previousRealmTop = stackalloc int[Math.Min(10, Population)];
        if (recordLeaderChange)
        {
            _agentByRank.AsSpan(0, previousCombatTop.Length).CopyTo(previousCombatTop);
            _agentByRealmRank.AsSpan(0, previousRealmTop.Length).CopyTo(previousRealmTop);
            _rankingRisenCount = 0;
            _rankingFallenCount = 0;
            _rankingUnchangedCount = 0;
            _rankingTotalAbsoluteMovement = 0;
        }

        var combatPowerDirty = _combatPowerRankingCache.IsDirty;
        var realmDirty = _realmRankingCache.IsDirty;
        var aptitudeDirty = _aptitudeRankingCache.IsDirty;
        var wealthDirty = _wealthRankingCache.IsDirty;
        RefreshDirtyRankingCategories(
            combatPowerDirty,
            realmDirty,
            aptitudeDirty,
            wealthDirty,
            recordLeaderChange);

        if (recordLeaderChange)
        {
            RecordEliteRankingMovement(
                combatPowerDirty,
                realmDirty,
                previousCombatTop,
                previousRealmTop);
            _rankingMovementFromTick = _lastRankingRefreshTick;
            _lastRankingRefreshTick = Tick.Value;
            _rankingUnchangedCount = Population - _rankingRisenCount - _rankingFallenCount;
        }

        RecordRankingHistory(recordLeaderChange);
        var leader = _agentByRank[0];
        if (recordLeaderChange && leader != previousLeader)
        {
            AddEvent(WorldEventKind.NewLeader, leader, _agents.Power[leader].Total, previousLeader);
        }
    }

    private void RecordEliteRankingMovement(
        bool combatPowerDirty,
        bool realmDirty,
        ReadOnlySpan<int> previousCombatTop,
        ReadOnlySpan<int> previousRealmTop)
    {
        ReadOnlySpan<int> eliteLimits = [1, 3, 5, 10];
        for (var tier = 0; tier < eliteLimits.Length; tier++)
        {
            var count = Math.Min(eliteLimits[tier], Population);
            var combatChanged = 0;
            var realmChanged = 0;
            for (var offset = 0; offset < count; offset++)
            {
                combatChanged += combatPowerDirty && _agentByRank[offset] != previousCombatTop[offset]
                    ? 1
                    : 0;
                realmChanged += realmDirty && _agentByRealmRank[offset] != previousRealmTop[offset]
                    ? 1
                    : 0;
            }
            _eliteCombatChangedCounts[tier] = combatChanged;
            _eliteRealmChangedCounts[tier] = realmChanged;
        }
    }

    private void RecordRankingHistory(bool recordLeaderChange)
    {
        if (!_rankingHistory.Record(
                Tick,
                _agentByRank,
                _agentByRealmRank,
                _agentByWealthRank,
                force: !recordLeaderChange && _rankingHistory.Frames.Count == 0))
        {
            return;
        }

        _worldChronicle.RecordRankingSample(
            Tick,
            _agentByRank,
            _agentByRealmRank,
            _agentByAptitudeRank,
            _agentByWealthRank,
            agent => _agents.Names[agent]);
    }

    private void RefreshDirtyRankingCategories(
        bool combatPowerDirty,
        bool realmDirty,
        bool aptitudeDirty,
        bool wealthDirty,
        bool recordMovement)
    {
        if (combatPowerDirty)
        {
            RefreshCombatRanking(recordMovement);
        }
        if (realmDirty)
        {
            RefreshRealmRanking();
        }
        if (aptitudeDirty)
        {
            RefreshAptitudeRanking();
        }
        if (wealthDirty)
        {
            RefreshWealthRanking();
        }
    }

    private void RefreshCombatRanking(bool recordMovement)
    {
        var scores = _rankingWorkspace.Scores;
        for (var agent = 0; agent < Population; agent++)
        {
            scores[agent] = _agents.Power[agent].Total;
        }
        _rankingWorkspace.Sort(_agentByRank);
        _combatPowerRankingCache.MarkSorted();

        var risen = 0;
        var fallen = 0;
        long totalMovement = 0;
        for (var offset = 0; offset < _agentByRank.Length; offset++)
        {
            var agent = _agentByRank[offset];
            var currentRank = offset + 1;
            if (recordMovement)
            {
                var previousRank = _rankByAgent[agent];
                risen += currentRank < previousRank ? 1 : 0;
                fallen += currentRank > previousRank ? 1 : 0;
                totalMovement += Math.Abs((long)currentRank - previousRank);
            }
            _rankByAgent[agent] = currentRank;
        }
        if (recordMovement)
        {
            _rankingRisenCount += risen;
            _rankingFallenCount += fallen;
            _rankingTotalAbsoluteMovement += totalMovement;
        }
    }

    private void RefreshRealmRanking()
    {
        var scores = _rankingWorkspace.Scores;
        for (var agent = 0; agent < Population; agent++)
        {
            scores[agent] = GetRealmRankingScore(agent);
        }
        _rankingWorkspace.Sort(_agentByRealmRank);
        _realmRankingCache.MarkSorted();
        UpdateInverseRanking(_agentByRealmRank, _realmRankByAgent);
    }

    private void RefreshAptitudeRanking()
    {
        var scores = _rankingWorkspace.Scores;
        for (var agent = 0; agent < Population; agent++)
        {
            scores[agent] = _agents.GetAptitudes(agent).RankingScore;
        }
        _rankingWorkspace.Sort(_agentByAptitudeRank);
        _aptitudeRankingCache.MarkSorted();
        UpdateInverseRanking(_agentByAptitudeRank, _aptitudeRankByAgent);
    }

    private void RefreshWealthRanking()
    {
        var scores = _rankingWorkspace.Scores;
        for (var agent = 0; agent < Population; agent++)
        {
            scores[agent] = _agents.SpiritStones[agent];
        }
        _rankingWorkspace.Sort(_agentByWealthRank);
        _wealthRankingCache.MarkSorted();
        UpdateInverseRanking(_agentByWealthRank, _wealthRankByAgent);
    }

    private long GetRealmRankingScore(int index)
    {
        if (!HasEnteredCultivation(index))
        {
            return _agents.CultivationProgress[index];
        }

        var stageAndFloor = ((long)(int)_agents.RealmStages[index] * RealmRules.MinorStageCount)
            + _agents.RealmLayers[index];
        var completionTier = _agents.GreatPerfectionAchieved[index]
            ? 2
            : _agents.FloorNineCompleted[index] ? 1 : 0;
        return checked((((stageAndFloor * 3) + completionTier) * 1_000_000_000L)
            + _agents.CultivationProgress[index]);
    }

    private static void UpdateInverseRanking(int[] orderedAgents, int[] rankByAgent)
    {
        for (var offset = 0; offset < orderedAgents.Length; offset++)
        {
            rankByAgent[orderedAgents[offset]] = offset + 1;
        }
    }

    private void MarkActionRankingChanges(
        int index,
        bool tracksRealmRanking,
        bool enteredCultivationBefore,
        long realmRankingScoreBefore,
        bool tracksWealthRanking,
        long spiritStonesBefore)
    {
        if (tracksRealmRanking
            && (enteredCultivationBefore != HasEnteredCultivation(index)
                || realmRankingScoreBefore != GetRealmRankingScore(index)))
        {
            MarkRealmRankingDirty();
        }
        if (tracksWealthRanking && spiritStonesBefore != _agents.SpiritStones[index])
        {
            MarkWealthRankingDirty();
        }
    }

    private static bool CanChangeSpiritStoneBalance(AgentActivity activity) => activity is
        AgentActivity.Cultivating
        or AgentActivity.DoingCityMission
        or AgentActivity.TradingInCity
        or AgentActivity.CraftingTreasure
        or AgentActivity.CraftingPill
        or AgentActivity.MiningSpiritStones;

    private static bool CanChangePowerInputsWithoutInternalRecalculation(AgentActivity activity) => activity is
        AgentActivity.Cultivating
        or AgentActivity.TrainingTechnique
        or AgentActivity.TrainingSpell
        or AgentActivity.TradingInCity
        or AgentActivity.HuntingMonster;

    private ActivityPowerInputSnapshot CaptureActivityPowerInputs(
        int index,
        AgentActivity activity) => activity switch
        {
            AgentActivity.Cultivating => new(
                ((ulong)_agents.RealmStages[index] << 8) | _agents.RealmLayers[index],
                0),
            AgentActivity.TrainingTechnique => new(
                PackSkillPowerInputs(
                    _agents.TechniqueQualities[index],
                    _agents.TechniqueMasteries[index],
                    _agents.TechniqueUnderstanding[index]),
                0),
            AgentActivity.TrainingSpell => new(
                PackSkillPowerInputs(
                    _agents.SpellQualities[index],
                    _agents.SpellMasteries[index],
                    _agents.SpellUnderstanding[index]),
                0),
            AgentActivity.TradingInCity => new(
                PackSkillPowerInputs(
                    _agents.TechniqueQualities[index],
                    _agents.TechniqueMasteries[index],
                    _agents.TechniqueUnderstanding[index]),
                PackSkillPowerInputs(
                    _agents.SpellQualities[index],
                    _agents.SpellMasteries[index],
                    _agents.SpellUnderstanding[index])),
            AgentActivity.HuntingMonster => new(
                ((ulong)(uint)_agents.CombatExperience[index] << 16)
                    | _agents.CombatUnderstanding[index],
                0),
            _ => default,
        };

    private static ulong PackSkillPowerInputs(
        ushort quality,
        ushort mastery,
        ushort understanding) =>
        ((ulong)quality << 32) | ((ulong)mastery << 16) | understanding;

    private void MarkCombatPowerRankingDirty() => _combatPowerRankingCache.MarkDirty();
    private void MarkRealmRankingDirty() => _realmRankingCache.MarkDirty();
    private void MarkAptitudeRankingDirty() => _aptitudeRankingCache.MarkDirty();
    private void MarkWealthRankingDirty() => _wealthRankingCache.MarkDirty();

    private void MarkAllRankingsDirty()
    {
        MarkCombatPowerRankingDirty();
        MarkRealmRankingDirty();
        MarkAptitudeRankingDirty();
        MarkWealthRankingDirty();
    }

    internal RankingCacheDiagnostics GetRankingCacheDiagnostics() => new(
        _combatPowerRankingCache.GetDiagnostics(),
        _realmRankingCache.GetDiagnostics(),
        _aptitudeRankingCache.GetDiagnostics(),
        _wealthRankingCache.GetDiagnostics());

    private readonly record struct ActivityPowerInputSnapshot(ulong Primary, ulong Secondary);
}
