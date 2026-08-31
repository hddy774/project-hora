using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed partial class World
{
    private const long RematchAvoidanceTicks = 600;

    // Alpha 1.1.3에서 확정되지 않은 전투 시간 배율이다. 한 행동의 기준 간격이며
    // 전투가 상위 경지에서 수십 년~수천 년 이어질 수 있도록 이후 밸런스 검토 대상으로 둔다.
    private static readonly long[] CombatBaseActionMonths =
    [
        1,      // 연기경
        1,      // 축기경
        3,      // 금단경
        12,     // 원영경
        36,     // 화신경
        120,    // 연허경
        600,    // 합체경
        2_400,  // 도겁경
        12_000, // 대승경
        60_000, // 반선경
    ];

    private void ResolveSeekingChallenge(int challenger)
    {
        _totalChallenges++;
        _agents.ChallengesIssued[challenger]++;
        var defender = FindChallengeTarget(challenger);
        if (defender < 0)
        {
            _agents.Activities[challenger] = AgentActivity.Cultivating;
            return;
        }

        StartCombat(challenger, defender);
    }

    private int FindChallengeTarget(int challenger)
    {
        var challengerRank = _rankByAgent[challenger];
        if (challengerRank <= 1)
        {
            return -1;
        }

        var window = Math.Clamp(challengerRank / 100, 3, 50);
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var offset = 1 + _random.NextInt(Math.Min(window, challengerRank - 1));
            var targetRank = challengerRank - offset;
            var defender = _agentByRank[targetRank - 1];
            if (CanChallenge(challenger, defender))
            {
                return defender;
            }
        }

        return -1;
    }

    private bool CanChallenge(int challenger, int defender)
    {
        if (challenger == defender
            || _agents.Activities[defender] == AgentActivity.Fighting
            || _agents.Activities[defender] == AgentActivity.SeekingChallenge
            || _agents.LocationIds[challenger] != _agents.LocationIds[defender]
            || _agents.Health[defender] * 100 < _agents.MaximumHealth[defender] * 60
            || _agents.Qi[defender] * 100 < _agents.MaximumQi[defender] * 25)
        {
            return false;
        }

        return _agents.LastOpponents[challenger] != defender
            || Tick.Value - _agents.LastCombatTicks[challenger] >= RematchAvoidanceTicks;
    }

    private void StartCombat(int challenger, int defender)
    {
        _totalCombatsStarted++;
        _activeCombats++;
        _agents.Opponents[challenger] = defender;
        _agents.Opponents[defender] = challenger;
        _agents.CombatStartedTicks[challenger] = Tick.Value;
        _agents.CombatStartedTicks[defender] = Tick.Value;
        _agents.CombatActions[challenger] = 0;
        _agents.CombatActions[defender] = 0;
        _agents.CombatTurnOwners[challenger] = challenger;
        _agents.CombatTurnOwners[defender] = challenger;
        _agents.Guarding[challenger] = false;
        _agents.Guarding[defender] = false;
        _agents.Activities[challenger] = AgentActivity.Fighting;
        _agents.Activities[defender] = AgentActivity.Fighting;

        // 방어자의 기존 예약을 무효화한다. 도전자의 다음 예약은 현재 행동을 마친
        // 공통 스케줄러가 만들고, 이후부터는 전투 턴 소유자만 예약한다.
        ParkCombatant(defender);
        AddEvent(
            WorldEventKind.CombatStarted,
            challenger,
            _agents.Power[challenger].Total - _agents.Power[defender].Total,
            defender);
    }

    private bool ResolveCombatAction(int actor)
    {
        var target = _agents.Opponents[actor];
        if (target < 0 || _agents.Opponents[target] != actor)
        {
            _agents.CombatTurnOwners[actor] = -1;
            _agents.Activities[actor] = AgentActivity.Recovering;
            return false;
        }

        if (_agents.CombatTurnOwners[actor] != actor)
        {
            ParkCombatant(actor);
            return true;
        }

        _agents.CombatActions[actor]++;
        if (ShouldAttemptRetreat(actor, target) && TryRetreat(actor, target))
        {
            AddEvent(WorldEventKind.CombatRetreated, actor, _agents.Health[actor], target);
            FinishCombat(target, actor);
            return true;
        }

        var targetSeverityBefore = CombatInjuryRules.GetSeverity(_agents.InjuryPoints[target]);
        int damage;
        if (ShouldGuard(actor, target))
        {
            _agents.Guarding[actor] = true;
            damage = 0;
            AddEvent(WorldEventKind.CombatGuarded, actor, 0, target);
        }
        else if (CanCastSpell(actor) && _random.NextInt(100) < 46)
        {
            damage = ResolveSpellCast(actor, target);
        }
        else
        {
            damage = ResolvePhysicalAttack(actor, target);
        }

        var fatal = damage > 0 && ApplyCombatInjury(target, damage, targetSeverityBefore);
        if (fatal)
        {
            FinishCombat(actor, target, fatal: true);
            return true;
        }

        if (_agents.Health[target] <= 0)
        {
            FinishCombat(actor, target);
            return true;
        }

        PassCombatTurn(actor, target);
        return true;
    }

    private bool ShouldAttemptRetreat(int actor, int target)
    {
        var healthPercent = _agents.Health[actor] * 100 / Math.Max(1, _agents.MaximumHealth[actor]);
        var actorThreat = GetActualCombatThreat(actor);
        var targetThreat = GetActualCombatThreat(target);
        var threatDisadvantage = targetThreat * 100 / Math.Max(1L, actorThreat);
        return healthPercent < 35 || (healthPercent < 55 && threatDisadvantage > 115);
    }

    private bool TryRetreat(int actor, int target)
    {
        var actorStats = _agents.CombatAttributes[actor];
        var targetStats = _agents.CombatAttributes[target];
        var experienceDifference = IntegerExperienceAdvantage(actor, target);
        var chance = Math.Clamp(
            4_500
            + ((actorStats.Judgment - targetStats.Focus) / 4)
            + ((actorStats.AttackSpeedBasisPoints - targetStats.AttackSpeedBasisPoints) / 5)
            + experienceDifference,
            2_000,
            9_500);
        return _random.Chance(chance);
    }

    private bool ShouldGuard(int actor, int target)
    {
        var healthPercent = _agents.Health[actor] * 100 / Math.Max(1, _agents.MaximumHealth[actor]);
        var actorStats = _agents.CombatAttributes[actor];
        var targetStats = _agents.CombatAttributes[target];
        var baseChance = healthPercent < 45 ? 35 : 13;
        if (targetStats.Attack > actorStats.Defense)
        {
            baseChance += Math.Clamp((targetStats.Attack - actorStats.Defense) / 100, 0, 15);
        }

        baseChance += Math.Clamp((actorStats.Focus - targetStats.Judgment) / 200, -5, 8);
        return _random.NextInt(100) < Math.Clamp(baseChance, 5, 55);
    }

    private bool CanCastSpell(int actor) => _agents.Qi[actor] >= GetSpellQiCost(actor);

    private int ResolvePhysicalAttack(int actor, int target)
    {
        var attacker = _agents.CombatAttributes[actor];
        var defender = _agents.CombatAttributes[target];
        var offense = checked(
            attacker.Attack
            + (_agents.TechniqueMasteries[actor] / 3)
            + (_agents.TechniqueUnderstanding[actor] / 4));
        var damage = CalculateActualDamage(
            actor,
            target,
            offense,
            attacker.Penetration * 3 / 4,
            defender);
        damage = ApplyGuard(target, damage);
        _agents.Health[target] = Math.Max(0, _agents.Health[target] - damage);
        AddEvent(WorldEventKind.CombatPhysicalAttack, actor, damage, target);
        return damage;
    }

    private int ResolveSpellCast(int actor, int target)
    {
        var qiCost = GetSpellQiCost(actor);
        _agents.Qi[actor] -= qiCost;
        var attacker = _agents.CombatAttributes[actor];
        var defender = _agents.CombatAttributes[target];
        var offense = checked((int)Math.Min(
            int.MaxValue,
            ((long)attacker.Attack
                + (_agents.SpellMasteries[actor] / 2)
                + (_agents.SpellUnderstanding[actor] / 3))
            * attacker.SpellEfficiencyBasisPoints
            / 10_000));
        var damage = CalculateActualDamage(
            actor,
            target,
            offense,
            attacker.Penetration,
            defender);
        damage = ApplyGuard(target, damage);
        _agents.Health[target] = Math.Max(0, _agents.Health[target] - damage);
        AddEvent(WorldEventKind.CombatSpellCast, actor, damage, target);
        return damage;
    }

    private int CalculateActualDamage(
        int actor,
        int target,
        int offense,
        int penetration,
        CombatAttributes defender)
    {
        var attacker = _agents.CombatAttributes[actor];
        var evasionChance = Math.Clamp(
            defender.EvasionBasisPoints
            + (defender.DefenseSpeedBasisPoints / 40)
            - (attacker.Judgment / 12),
            0,
            6_500);
        if (_random.Chance(evasionChance))
        {
            return 0;
        }

        var defenseReduction = Math.Max(0, penetration - defender.PenetrationDefense);
        var effectiveDefense = Math.Max(0, defender.Defense - defenseReduction);
        // 동경지전도 수십 회 안팎의 유효타로 결론이 나도록 최소 피해를 둔다.
        // 상위 경지는 한 행동 간격 자체가 길어 전체 전투 기간은 계속 크게 늘어난다.
        var minimumDamage = Math.Max(1, _agents.MaximumHealth[target] / 40);
        var baseDamage = Math.Max(minimumDamage, offense - ((long)effectiveDefense * 2 / 3));
        var varianceBasisPoints = 9_000 + _random.NextInt(2_001);
        var damage = checked(baseDamage * varianceBasisPoints / 10_000);

        var criticalChance = Math.Clamp(
            attacker.CriticalChanceBasisPoints
            + (attacker.Judgment / 20)
            - defender.CriticalDefenseBasisPoints,
            100,
            5_000);
        if (_random.Chance(criticalChance))
        {
            var criticalMultiplier = Math.Max(
                10_000,
                attacker.CriticalPowerBasisPoints - defender.CriticalDefenseBasisPoints);
            damage = checked(damage * criticalMultiplier / 10_000);
        }

        return checked((int)Math.Clamp(damage, 1L, int.MaxValue));
    }

    private int ApplyGuard(int target, int damage)
    {
        if (!_agents.Guarding[target] || damage <= 0)
        {
            return damage;
        }

        _agents.Guarding[target] = false;
        var defenseSpeed = _agents.CombatAttributes[target].DefenseSpeedBasisPoints;
        var retainedBasisPoints = Math.Clamp(7_500 - (defenseSpeed / 8), 3_500, 6_500);
        return Math.Max(1, checked((int)((long)damage * retainedBasisPoints / 10_000)));
    }

    private bool ApplyCombatInjury(int target, int damage, InjurySeverity previousSeverity)
    {
        var damageRatio = checked((int)Math.Min(
            CombatInjuryRules.MaximumInjuryPoints,
            (long)damage * CombatInjuryRules.MaximumInjuryPoints
                / Math.Max(1, _agents.MaximumHealth[target])
                / 2));
        var lowHealthBonus = _agents.Health[target] * 4 < _agents.MaximumHealth[target]
            ? 500
            : 0;
        var nextPoints = _agents.Health[target] <= 0
            ? CombatInjuryRules.KnockoutInjuryPoints
            : Math.Min(
                CombatInjuryRules.MaximumInjuryPoints,
                _agents.InjuryPoints[target] + damageRatio + lowHealthBonus);
        _agents.InjuryPoints[target] = checked((ushort)nextPoints);

        var severity = CombatInjuryRules.GetSeverity(nextPoints);
        if (severity > previousSeverity && severity >= InjurySeverity.Severe)
        {
            AddEvent(WorldEventKind.CombatInjury, target, (int)severity, _agents.Opponents[target]);
        }

        return _random.Chance(CombatInjuryRules.GetFatalChanceBasisPoints(severity));
    }

    private void PassCombatTurn(int actor, int target)
    {
        _agents.CombatTurnOwners[actor] = target;
        _agents.CombatTurnOwners[target] = target;
        ParkCombatant(actor);
        ScheduleAgent(target, checked(Tick.Value + CalculateCombatActionDuration(target)));
    }

    private void ParkCombatant(int index) => _agents.NextDecisionTicks[index] = long.MaxValue;

    private void FinishCombat(int winner, int loser, bool fatal = false)
    {
        _totalCombatsCompleted++;
        _activeCombats = Math.Max(0, _activeCombats - 1);
        _agents.Wins[winner]++;
        _agents.Losses[loser]++;
        var combinedActions = checked(_agents.CombatActions[winner] + _agents.CombatActions[loser]);
        var winnerBaseGain = 150
            + Math.Min(500, _agents.CombatExperience[loser] / 1_000)
            + Math.Min(350, combinedActions * 3);
        var loserBaseGain = 70
            + Math.Min(220, _agents.CombatExperience[winner] / 2_000)
            + Math.Min(150, combinedActions);
        var winnerGain = ApplyCombatProgress(
            winner,
            winnerBaseGain,
            8 + Math.Min(12, combinedActions / 10));
        ApplyCombatProgress(
            loser,
            loserBaseGain,
            4 + Math.Min(8, combinedActions / 15));
        _agents.LastOpponents[winner] = loser;
        _agents.LastOpponents[loser] = winner;
        _agents.LastCombatTicks[winner] = Tick.Value;
        _agents.LastCombatTicks[loser] = Tick.Value;
        _agents.Opponents[winner] = -1;
        _agents.Opponents[loser] = -1;
        _agents.CombatTurnOwners[winner] = -1;
        _agents.CombatTurnOwners[loser] = -1;
        _agents.Guarding[winner] = false;
        _agents.Guarding[loser] = false;
        _agents.Activities[winner] = AgentActivity.Recovering;
        _agents.RecalculatePower(winner);
        ScheduleAgent(winner, checked(Tick.Value + CalculateActivityDuration(winner)));

        if (fatal)
        {
            _agents.RecalculatePower(loser);
            AddEvent(WorldEventKind.CombatEnded, winner, winnerGain, loser);
            BeginReincarnation(loser, combatDeath: true);
            return;
        }

        _agents.Health[loser] = Math.Max(1, _agents.Health[loser]);
        _agents.Activities[loser] = AgentActivity.Recovering;
        _agents.RecalculatePower(loser);
        ScheduleAgent(loser, checked(Tick.Value + CalculateActivityDuration(loser)));
        AddEvent(WorldEventKind.CombatEnded, winner, winnerGain, loser);
    }

    private long CalculateCombatActionDuration(int index)
    {
        var stageIndex = Math.Clamp((int)_agents.RealmStages[index], 0, CombatBaseActionMonths.Length - 1);
        var baseMonths = CombatBaseActionMonths[stageIndex];
        var stats = _agents.CombatAttributes[index];
        var speed = Math.Max(2_500, (stats.AttackSpeedBasisPoints + stats.DefenseSpeedBasisPoints) / 2);
        var adjustedMonths = Math.Max(1L, checked(baseMonths * 10_000 / speed));
        var rawTicks = checked(adjustedMonths * GameCalendar.TicksPerYear / GameCalendar.MonthsPerYear);
        return Math.Max(
            GameCalendar.GetNextMonthStart(Tick).Value - Tick.Value,
            rawTicks);
    }

    private int GetSpellQiCost(int index) => Math.Max(
        20,
        (_agents.MaximumQi[index] / 20)
        + ((int)new QualityValue(_agents.SpellQualities[index]).Tier * 18));

    private long GetActualCombatThreat(int index)
    {
        var stats = _agents.CombatAttributes[index];
        return checked(
            (long)stats.Attack
            + stats.Defense
            + (stats.MaximumHealth / 4)
            + (stats.MaximumQi / 6)
            + ((stats.Judgment + stats.Focus) / 2));
    }

    private int IntegerExperienceAdvantage(int actor, int target)
    {
        var actorStats = _agents.CombatAttributes[actor];
        var targetStats = _agents.CombatAttributes[target];
        var difference = checked(
            actorStats.Judgment + actorStats.Focus
            - targetStats.Judgment - targetStats.Focus);
        return Math.Clamp(difference / 5, -1_200, 1_200);
    }

    private int ApplyCombatProgress(int index, int baseExperienceGain, int baseUnderstandingGain)
    {
        var roleScores = _agents.GetAptitudeRoleScores(index);
        var before = _agents.CombatExperience[index];
        _agents.CombatExperience[index] = CombatProgressionRules.ApplyExperienceGain(
            before,
            baseExperienceGain,
            AptitudeCombatBonusFormula.GetCombatExperienceGainBasisPoints(roleScores));
        _agents.CombatUnderstanding[index] = CombatProgressionRules.ApplyUnderstandingGain(
            _agents.CombatUnderstanding[index],
            baseUnderstandingGain,
            AptitudeCombatBonusFormula.GetCombatUnderstandingEffectBasisPoints(roleScores));
        return checked(_agents.CombatExperience[index] - before);
    }
}
