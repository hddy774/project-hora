using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed partial class World
{
    private const long RematchAvoidanceTicks = 600;
    private const int MaximumCombatActions = 48;

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
        _agents.Guarding[challenger] = false;
        _agents.Guarding[defender] = false;
        _agents.Activities[challenger] = AgentActivity.Fighting;
        _agents.Activities[defender] = AgentActivity.Fighting;
        ScheduleAgent(defender, checked(Tick.Value + CalculateCombatActionDuration(defender)));
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
            _agents.Activities[actor] = AgentActivity.Recovering;
            return false;
        }

        _agents.CombatActions[actor]++;
        if (ShouldAttemptRetreat(actor, target) && TryRetreat(actor, target))
        {
            AddEvent(WorldEventKind.CombatRetreated, actor, _agents.Health[actor], target);
            FinishCombat(target, actor);
            return true;
        }

        if (ShouldGuard(actor, target))
        {
            _agents.Guarding[actor] = true;
            AddEvent(WorldEventKind.CombatGuarded, actor, 0, target);
        }
        else if (CanCastSpell(actor) && _random.NextInt(100) < 46)
        {
            ResolveSpellCast(actor, target);
        }
        else
        {
            ResolvePhysicalAttack(actor, target);
        }

        if (_agents.Health[target] <= 0)
        {
            FinishCombat(actor, target);
            return true;
        }

        var combinedActions = _agents.CombatActions[actor] + _agents.CombatActions[target];
        if (combinedActions >= MaximumCombatActions)
        {
            var actorHealthRatio = _agents.Health[actor] * 10_000 / _agents.MaximumHealth[actor];
            var targetHealthRatio = _agents.Health[target] * 10_000 / _agents.MaximumHealth[target];
            if (actorHealthRatio == targetHealthRatio)
            {
                actorHealthRatio += _agents.Power[actor].Total >= _agents.Power[target].Total ? 1 : -1;
            }

            FinishCombat(actorHealthRatio > targetHealthRatio ? actor : target, actorHealthRatio > targetHealthRatio ? target : actor);
            return true;
        }

        return false;
    }

    private bool ShouldAttemptRetreat(int actor, int target)
    {
        var healthPercent = _agents.Health[actor] * 100 / _agents.MaximumHealth[actor];
        var powerDisadvantage = _agents.Power[target].Total * 100 / Math.Max(1L, _agents.Power[actor].Total);
        return healthPercent < 24 || (healthPercent < 40 && powerDisadvantage > 125);
    }

    private bool TryRetreat(int actor, int target)
    {
        var experienceDifference = IntegerExperienceAdvantage(actor, target);
        var chance = Math.Clamp(
            2_300
            + (GetQualityEffect(_agents.Comprehensions[actor]) * 35)
            + experienceDifference
            - (GetQualityEffect(_agents.Comprehensions[target]) * 15),
            800,
            8_500);
        return _random.Chance(chance);
    }

    private bool ShouldGuard(int actor, int target)
    {
        var healthPercent = _agents.Health[actor] * 100 / _agents.MaximumHealth[actor];
        var baseChance = healthPercent < 45 ? 35 : 13;
        if (_agents.Power[target].Spell > _agents.Power[target].Technique)
        {
            baseChance += GetQualityEffect(_agents.Comprehensions[actor]) / 10;
        }

        return _random.NextInt(100) < Math.Min(55, baseChance);
    }

    private bool CanCastSpell(int actor)
    {
        var qiCost = GetSpellQiCost(actor);
        return _agents.Qi[actor] >= qiCost;
    }

    private void ResolvePhysicalAttack(int actor, int target)
    {
        var baseDamage = 30
            + (GetQualityEffect(_agents.Bones[actor]) * 2)
            + (GetQualityEffect(_agents.TechniqueQualities[actor]) * 24)
            + (_agents.GetTreasureCombatEffect(actor, attack: true) * 5)
            + (_agents.TechniqueMasteries[actor] / 20)
            + (GetRealmCombatScale(actor) / 3);
        var defense = (GetQualityEffect(_agents.Constitutions[target]) * 2)
            + (GetQualityEffect(_agents.Bones[target]) / 2)
            + (_agents.GetTreasureCombatEffect(target, attack: false) * 5)
            + (GetRealmCombatScale(target) / 5);
        var damage = ApplyPowerRatioAndVariance(actor, target, Math.Max(12, baseDamage - (defense / 3)));
        damage = ApplyGuard(target, damage);
        _agents.Health[target] -= damage;
        AddEvent(WorldEventKind.CombatPhysicalAttack, actor, damage, target);
    }

    private void ResolveSpellCast(int actor, int target)
    {
        var qiCost = GetSpellQiCost(actor);
        _agents.Qi[actor] -= qiCost;
        var baseDamage = 38
            + (GetQualityEffect(_agents.SpiritualRoots[actor]) * 2)
            + GetQualityEffect(_agents.Comprehensions[actor])
            + (GetQualityEffect(_agents.SpellQualities[actor]) * 32)
            + (_agents.GetTreasureCombatEffect(actor, attack: true) * 5)
            + (_agents.SpellMasteries[actor] / 16)
            + (GetRealmCombatScale(actor) / 2);
        var defense = (GetQualityEffect(_agents.Constitutions[target]) * 2)
            + GetQualityEffect(_agents.SpiritualRoots[target])
            + (_agents.GetTreasureCombatEffect(target, attack: false) * 5)
            + (GetRealmCombatScale(target) / 4);
        var damage = ApplyPowerRatioAndVariance(actor, target, Math.Max(15, baseDamage - (defense / 3)));
        damage = ApplyGuard(target, damage);
        _agents.Health[target] -= damage;
        AddEvent(WorldEventKind.CombatSpellCast, actor, damage, target);
    }

    private int ApplyPowerRatioAndVariance(int actor, int target, int baseDamage)
    {
        var actorPower = _agents.Power[actor].Total;
        var targetPower = Math.Max(1L, _agents.Power[target].Total);
        var ratio = (int)Math.Clamp(actorPower * 100 / targetPower, 70L, 145L);
        var variance = 88 + _random.NextInt(25);
        var scaledDamage = checked((long)baseDamage * ratio * variance / 10_000);
        return checked((int)Math.Clamp(scaledDamage, 1L, int.MaxValue));
    }

    private int ApplyGuard(int target, int damage)
    {
        if (!_agents.Guarding[target])
        {
            return damage;
        }

        _agents.Guarding[target] = false;
        return Math.Max(1, damage * 55 / 100);
    }

    private void FinishCombat(int winner, int loser)
    {
        _totalCombatsCompleted++;
        _activeCombats--;
        _agents.Wins[winner]++;
        _agents.Losses[loser]++;
        var winnerGain = 150 + (int)Math.Min(350L, _agents.Power[loser].Total / 400);
        var loserGain = 70 + (int)Math.Min(180L, _agents.Power[winner].Total / 800);
        _agents.CombatExperience[winner] = checked(_agents.CombatExperience[winner] + winnerGain);
        _agents.CombatExperience[loser] = checked(_agents.CombatExperience[loser] + loserGain);
        _agents.Health[loser] = Math.Max(1, _agents.Health[loser]);
        _agents.LastOpponents[winner] = loser;
        _agents.LastOpponents[loser] = winner;
        _agents.LastCombatTicks[winner] = Tick.Value;
        _agents.LastCombatTicks[loser] = Tick.Value;
        _agents.Opponents[winner] = -1;
        _agents.Opponents[loser] = -1;
        _agents.Guarding[winner] = false;
        _agents.Guarding[loser] = false;
        _agents.Activities[winner] = AgentActivity.Recovering;
        _agents.Activities[loser] = AgentActivity.Recovering;
        _agents.RecalculatePower(winner);
        _agents.RecalculatePower(loser);
        ScheduleAgent(winner, checked(Tick.Value + CalculateActivityDuration(winner)));
        ScheduleAgent(loser, checked(Tick.Value + CalculateActivityDuration(loser)));
        AddEvent(WorldEventKind.CombatEnded, winner, winnerGain, loser);
    }

    private long CalculateCombatActionDuration(int index)
    {
        var speedAptitude = (GetQualityEffect(_agents.Bones[index]) + GetQualityEffect(_agents.Comprehensions[index])) / 2;
        var experienceReduction = Math.Min(5, _agents.CombatExperience[index] / 5_000);
        return Math.Max(5, 15 - (speedAptitude / 14) - experienceReduction);
    }

    private int GetSpellQiCost(int index) =>
        55 + ((int)new QualityValue(_agents.SpellQualities[index]).Tier * 18);

    private int GetRealmCombatScale(int index)
    {
        var stage = (int)_agents.RealmStages[index] + 1;
        return (stage * stage * 35) + (_agents.RealmLayers[index] * stage * 8);
    }

    private int IntegerExperienceAdvantage(int actor, int target)
    {
        var difference = _agents.CombatExperience[actor] - _agents.CombatExperience[target];
        return Math.Clamp(difference / 20, -1_200, 1_200);
    }
}
