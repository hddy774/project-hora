using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed partial class World
{
    public void AdvanceTo(SimulationTick targetTick)
    {
        if (targetTick.Value < Tick.Value)
        {
            throw new ArgumentOutOfRangeException(nameof(targetTick), "세계 시간을 되돌릴 수 없습니다.");
        }

        while (_decisionSchedule.TryPeek(out _, out var priorityTick)
            && priorityTick <= targetTick.Value)
        {
            var dueTick = priorityTick;
            var dueCount = 0;
            var dueAgents = _cultivationParallel.DueAgents;
            while (_decisionSchedule.TryPeek(out _, out var groupedTick)
                && groupedTick == dueTick)
            {
                dueAgents[dueCount++] = _decisionSchedule.Dequeue();
            }

            var due = new SimulationTick(dueTick);
            var actionMonth = GameCalendar.GetMonthIndex(due);
            AdvanceMonthlyBoundariesThrough(actionMonth);
            Tick = due;
            PrepareParallelCultivationWork(
                dueAgents.AsSpan(0, dueCount),
                dueTick,
                actionMonth);
            for (var dueIndex = 0; dueIndex < dueCount; dueIndex++)
            {
                ProcessScheduledAgent(dueAgents[dueIndex], dueTick, actionMonth);
            }
        }

        var targetMonth = GameCalendar.GetMonthIndex(targetTick);
        AdvanceMonthlyBoundariesThrough(targetMonth);
        Tick = targetTick;
        _worldChronicle.AdvanceTo(Tick);
    }

    private void ProcessScheduledAgent(int agentIndex, long dueTick, long actionMonth)
    {
        if (_agents.NextDecisionTicks[agentIndex] != dueTick)
        {
            _cultivationParallel.Discard(agentIndex);
            return;
        }

        if (_agents.LifeStates[agentIndex] == PersonLifeState.Living
            && HasReachedMaximumLifespan(agentIndex))
        {
            _cultivationParallel.Discard(agentIndex);
            BeginReincarnation(agentIndex);
            return;
        }

        if (_agents.Activities[agentIndex] == AgentActivity.Fighting)
        {
            var opponent = _agents.Opponents[agentIndex];
            if ((uint)opponent < (uint)Population
                && _agents.LifeStates[opponent] == PersonLifeState.Living
                && HasReachedMaximumLifespan(opponent))
            {
                _cultivationParallel.Discard(agentIndex);
                BeginReincarnation(opponent);
                return;
            }
        }

        if (_agents.Activities[agentIndex] == AgentActivity.Reincarnating)
        {
            _cultivationParallel.Discard(agentIndex);
            ResolveReincarnationStage(agentIndex);
            return;
        }

        if (_agents.LastActionMonths[agentIndex] >= actionMonth)
        {
            _cultivationParallel.Discard(agentIndex);
            ScheduleAgent(
                agentIndex,
                GameCalendar.GetMonthStart(checked(_agents.LastActionMonths[agentIndex] + 1)).Value);
            return;
        }

        var precomputedCultivation = default(CultivationWorkResult);
        var hasPrecomputedCultivation = false;
        if (_cultivationParallel.HasCandidate(agentIndex))
        {
            hasPrecomputedCultivation = _cultivationParallel.TryAcquire(
                agentIndex,
                CreateCultivationWorkFingerprint(agentIndex, dueTick),
                out precomputedCultivation);
        }
        _agents.LastActionMonths[agentIndex] = actionMonth;
        _agents.LastActionTicks[agentIndex] = dueTick;
        _agents.ActionsTaken[agentIndex] = checked(_agents.ActionsTaken[agentIndex] + 1);
        if (_agents.Activities[agentIndex] == AgentActivity.Cultivating)
        {
            EnsureCultivationPlan(agentIndex, actionMonth);
        }
        var resolvedActivity = _agents.Activities[agentIndex];
        var tracksRealmRanking = resolvedActivity == AgentActivity.Cultivating;
        var enteredCultivationBefore = tracksRealmRanking && HasEnteredCultivation(agentIndex);
        var realmRankingScoreBefore = tracksRealmRanking
            ? GetRealmRankingScore(agentIndex)
            : 0;
        var tracksWealthRanking = CanChangeSpiritStoneBalance(resolvedActivity);
        var spiritStonesBefore = tracksWealthRanking
            ? _agents.SpiritStones[agentIndex]
            : 0;
        var tracksPowerInputs = CanChangePowerInputsWithoutInternalRecalculation(
            resolvedActivity);
        var powerInputsBefore = tracksPowerInputs
            ? CaptureActivityPowerInputs(agentIndex, resolvedActivity)
            : default;
        var alreadyScheduled = ResolveActivity(
            agentIndex,
            actionMonth,
            hasPrecomputedCultivation ? precomputedCultivation : null);
        TryResolveBottleneckAfterAction(agentIndex, resolvedActivity);
        MarkActionRankingChanges(
            agentIndex,
            tracksRealmRanking,
            enteredCultivationBefore,
            realmRankingScoreBefore,
            tracksWealthRanking,
            spiritStonesBefore);
        if (tracksPowerInputs
            && powerInputsBefore != CaptureActivityPowerInputs(agentIndex, resolvedActivity))
        {
            RecalculatePowerAndUpdatePeak(agentIndex);
        }
        if (resolvedActivity == AgentActivity.Cultivating
            && TryContinueCultivationPlan(agentIndex, actionMonth))
        {
            var plan = _agents.CultivationPlans[agentIndex];
            if (plan.CompletedMonths <= 1 || plan.CompletedMonths % 12 == 0)
            {
                AddPersonalActionLog(agentIndex, resolvedActivity, actionMonth);
            }
            return;
        }

        ObserveSocialInteraction(agentIndex, resolvedActivity);
        AddPersonalActionLog(agentIndex, resolvedActivity, actionMonth);
        if (alreadyScheduled)
        {
            return;
        }

        SelectNextActivity(agentIndex);
        var nextTick = checked(dueTick + CalculateActivityDuration(agentIndex));
        ScheduleAgent(agentIndex, nextTick);
    }

    /// <summary>
    /// 호출을 몇 번으로 나눴는지와 무관하게 월별 권위 전이를 정확히 한 번씩 처리한다.
    /// AI가 참조하는 랭킹은 새 달이 시작될 때 직전 달의 결과로 확정되며, 같은 달 안에서는
    /// 고정된다. 따라서 관찰 query나 runtime pulse 경계가 다음 행동의 결과를 바꾸지 않는다.
    /// </summary>
    private void AdvanceMonthlyBoundariesThrough(long targetMonth)
    {
        while (_lastMonthlyBoundaryMonth < targetMonth)
        {
            var month = checked(_lastMonthlyBoundaryMonth + 1);
            Tick = GameCalendar.GetMonthStart(month);

            // 생성 직후의 0월 랭킹은 생성자에서 이미 확정했다. 이후에는 직전 월의
            // dirty 입력을 새 월의 의사결정 스냅샷으로 한 번만 정렬한다.
            if (month > 0)
            {
                RefreshRankings(recordLeaderChange: true);
            }

            AdvanceCityIndustry(month);
            RegenerateMonsters(month);
            AdvanceOrganizationSystem(month);
            AdvanceGeneratedWorldContent(month);
            _lastMonthlyBoundaryMonth = month;
        }
    }

    private void AdvanceCityIndustry(long currentMonth)
    {
        // 월드 생성 경로에서만 초기 수선자 수요를 투입한다. CityMarket 단위 계약과
        // 복원된 거래 원장은 외부에서 임의 수요가 추가되지 않는다.
        _market.SeedInitialCultivatorDemand();
        var pillsBefore = _market.PillsProduced;
        var treasuresBefore = _market.TreasuresProduced;
        _market.AdvanceIndustry(currentMonth);
        _totalPillsCrafted = checked(
            _totalPillsCrafted + (int)(_market.PillsProduced - pillsBefore));
        _totalTreasuresCrafted = checked(
            _totalTreasuresCrafted + (int)(_market.TreasuresProduced - treasuresBefore));
    }

    private void ScheduleAgent(int index, long tick, bool lifecycleBoundary = false)
    {
        if (_agents.LifeStates[index] == PersonLifeState.Living)
        {
            var maximumLifespanTick = GetMaximumLifespanTick(index);
            if (maximumLifespanTick <= tick)
            {
                tick = maximumLifespanTick;
                lifecycleBoundary = true;
            }
        }

        var scheduledTick = new SimulationTick(tick);
        if (!lifecycleBoundary
            && GameCalendar.GetMonthIndex(scheduledTick) <= _agents.LastActionMonths[index])
        {
            scheduledTick = GameCalendar.GetMonthStart(
                checked(_agents.LastActionMonths[index] + 1));
        }

        _agents.NextDecisionTicks[index] = scheduledTick.Value;
        _decisionSchedule.Schedule(index, scheduledTick.Value);
    }

}
