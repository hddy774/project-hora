using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed partial class World
{
    private void ResolveCultivation(int index)
    {
        switch (_agents.CultivationStates[index])
        {
            case CultivationState.Completed:
                _agents.CultivationProgress[index] = GetCultivationRequirement(index);
                return;
            case CultivationState.Stabilizing:
                _agents.CultivationStates[index] = RealmRules.IsFinalStage(
                    _agents.RealmStages[index],
                    _agents.RealmLayers[index])
                    ? CultivationState.Completed
                    : CultivationState.Progressing;
                return;
            case CultivationState.PreparingBreakthrough:
                _agents.CultivationStates[index] = CultivationState.PreflightReady;
                return;
            case CultivationState.PreflightReady:
                ResolveBreakthroughAttempt(index);
                return;
            case CultivationState.Bottleneck:
                return;
            case CultivationState.AttemptingBreakthrough:
                throw new InvalidOperationException("돌파 시도 상태는 하나의 행동 안에서 종료되어야 합니다.");
            case CultivationState.Progressing:
                AccumulateCultivationWork(index);
                return;
            default:
                throw new InvalidOperationException("알 수 없는 수행 상태입니다.");
        }
    }

    private void AccumulateCultivationWork(int index)
    {
        var spiritualRoot = GetQualityEffect(_agents.SpiritualRoots[index]);
        var comprehension = GetQualityEffect(_agents.Comprehensions[index]);
        var mind = GetQualityEffect(_agents.Minds[index]);
        var soul = GetQualityEffect(_agents.Souls[index]);
        var technique = GetQualityEffect(_agents.TechniqueQualities[index]);
        var rateBasisPoints = Math.Clamp(
            10_000
            + ((spiritualRoot - 50) * 40)
            + ((comprehension - 50) * 40)
            + ((mind - 50) * 20)
            + ((soul - 50) * 10)
            + ((technique - 50) * 10),
            3_000,
            18_000);
        var actionTicks = CalculateActivityDuration(index);
        var gain = Math.Max(1, checked((int)(actionTicks * rateBasisPoints / 10_000)));
        if (_agents.GrowthPills[index] > 0)
        {
            _agents.GrowthPills[index]--;
            gain = checked(gain + Math.Max(1, gain / 2));
        }

        var requirement = GetCultivationRequirement(index);
        _agents.CultivationProgress[index] = Math.Min(
            requirement,
            checked(_agents.CultivationProgress[index] + gain));
        if (_agents.CultivationProgress[index] < requirement)
        {
            return;
        }

        _agents.CultivationStates[index] = RealmRules.IsFinalStage(
            _agents.RealmStages[index],
            _agents.RealmLayers[index])
            ? CultivationState.Completed
            : CultivationState.PreparingBreakthrough;
    }

    private void ResolveBreakthroughAttempt(int index)
    {
        var preflightFailure = EvaluateBreakthroughPreflight(index);
        if (preflightFailure != CultivationBottleneckCause.None)
        {
            _agents.CultivationStates[index] = CultivationState.Bottleneck;
            _agents.CultivationBottleneckCauses[index] = preflightFailure;
            return;
        }

        _agents.CultivationStates[index] = CultivationState.AttemptingBreakthrough;
        var spiritualRoot = GetQualityEffect(_agents.SpiritualRoots[index]);
        var comprehension = GetQualityEffect(_agents.Comprehensions[index]);
        var mind = GetQualityEffect(_agents.Minds[index]);
        var soul = GetQualityEffect(_agents.Souls[index]);
        var stagePenalty = ((int)_agents.RealmStages[index] * 550) + (_agents.RealmLayers[index] * 80);
        var constitution = GetQualityEffect(_agents.Constitutions[index]);
        var breakthroughPillBonus = 0;
        if (_agents.BreakthroughPills[index] > 0)
        {
            _agents.BreakthroughPills[index]--;
            breakthroughPillBonus = 1_500;
        }

        var chance = Math.Clamp(
            4_500
            + (comprehension * 30)
            + (spiritualRoot * 20)
            + (constitution * 10)
            + (mind * 10)
            + (soul * 5)
            + breakthroughPillBonus
            - stagePenalty,
            1_200,
            9_500);

        if (_random.Chance(chance))
        {
            _agents.CultivationProgress[index] = 0;
            var previousRealm = _agents.RealmStages[index];
            var previousMinorStage = (MinorStage)_agents.RealmLayers[index];
            var bottleneckFailures = _agents.BottleneckFailures[index];
            AdvanceRealm(index);
            _agents.CultivationStates[index] = CultivationState.Stabilizing;
            _agents.CultivationBottleneckCauses[index] = CultivationBottleneckCause.None;
            if (bottleneckFailures > 0)
            {
                AddChronicle(index, ChronicleEventKind.BottleneckBreakthrough, bottleneckFailures);
                AddEvent(WorldEventKind.BottleneckBreakthrough, index, bottleneckFailures);
            }

            if (_agents.RealmStages[index] != previousRealm)
            {
                AddChronicle(index, ChronicleEventKind.MajorRealmBreakthrough);
                AddEvent(
                    WorldEventKind.MajorRealmBreakthrough,
                    index,
                    EncodeRealm(_agents.RealmStages[index], _agents.RealmLayers[index]));
            }
            else if ((MinorStage)_agents.RealmLayers[index] != previousMinorStage)
            {
                AddChronicle(index, ChronicleEventKind.MinorStageBreakthrough);
                AddEvent(
                    WorldEventKind.MinorStageBreakthrough,
                    index,
                    EncodeRealm(_agents.RealmStages[index], _agents.RealmLayers[index]));
            }

            _agents.BottleneckFailures[index] = 0;
            _totalBreakthroughs++;
            return;
        }

        if (_agents.BottleneckFailures[index] == 0)
        {
            AddChronicle(index, ChronicleEventKind.BreakthroughFailure, chance);
        }

        _agents.BottleneckFailures[index] = checked((ushort)(_agents.BottleneckFailures[index] + 1));
        _agents.CultivationStates[index] = CultivationState.Bottleneck;
        _agents.CultivationBottleneckCauses[index] = SelectFailureBottleneckCause(index);
        var requirement = GetCultivationRequirement(index);
        _agents.CultivationProgress[index] = requirement * 3 / 4;
        _agents.Health[index] = Math.Max(1, _agents.Health[index] - (_agents.MaximumHealth[index] / 8));
        AddEvent(WorldEventKind.BreakthroughFailed, index, chance);
    }

    private CultivationBottleneckCause EvaluateBreakthroughPreflight(int index)
    {
        if (_agents.CultivationProgress[index] < GetCultivationRequirement(index))
        {
            return CultivationBottleneckCause.Stability;
        }

        if (_agents.Health[index] * 2 < _agents.MaximumHealth[index])
        {
            return CultivationBottleneckCause.Body;
        }

        if (_agents.Qi[index] * 4 < _agents.MaximumQi[index])
        {
            return CultivationBottleneckCause.SpiritualRoot;
        }

        return CultivationBottleneckCause.None;
    }

    private CultivationBottleneckCause SelectFailureBottleneckCause(int index)
    {
        var cause = CultivationBottleneckCause.Knowledge;
        var score = (GetQualityEffect(_agents.Comprehensions[index])
            + GetQualityEffect(_agents.TechniqueQualities[index])) / 2;

        ConsiderBottleneckCause(
            CultivationBottleneckCause.Body,
            (GetQualityEffect(_agents.Bones[index])
                + GetQualityEffect(_agents.Constitutions[index])) / 2,
            ref cause,
            ref score);
        ConsiderBottleneckCause(
            CultivationBottleneckCause.SpiritualRoot,
            GetQualityEffect(_agents.SpiritualRoots[index]),
            ref cause,
            ref score);
        var breakthroughPillPrice = _market.GetPrice(EconomyRules.BreakthroughPillGoodId);
        var resourceScore = _agents.BreakthroughPills[index] > 0
            ? int.MaxValue
            : _agents.SpiritStones[index] >= breakthroughPillPrice ? 60 : 20;
        ConsiderBottleneckCause(
            CultivationBottleneckCause.Resources,
            resourceScore,
            ref cause,
            ref score);
        ConsiderBottleneckCause(
            CultivationBottleneckCause.Stability,
            (GetQualityEffect(_agents.Minds[index]) + GetQualityEffect(_agents.Souls[index])) / 2,
            ref cause,
            ref score);
        return cause;
    }

    private static void ConsiderBottleneckCause(
        CultivationBottleneckCause candidate,
        int candidateScore,
        ref CultivationBottleneckCause selected,
        ref int selectedScore)
    {
        if (candidateScore < selectedScore)
        {
            selected = candidate;
            selectedScore = candidateScore;
        }
    }

    private void TryResolveBottleneckAfterAction(int index, AgentActivity resolvedActivity)
    {
        if (_agents.CultivationStates[index] != CultivationState.Bottleneck)
        {
            return;
        }

        var resolved = _agents.CultivationBottleneckCauses[index] switch
        {
            CultivationBottleneckCause.Knowledge =>
                resolvedActivity is AgentActivity.TrainingTechnique or AgentActivity.TrainingSpell,
            CultivationBottleneckCause.Body =>
                resolvedActivity == AgentActivity.Recovering
                && _agents.Health[index] * 100 >= _agents.MaximumHealth[index] * 80,
            CultivationBottleneckCause.SpiritualRoot =>
                resolvedActivity == AgentActivity.Recovering
                && _agents.Qi[index] * 100 >= _agents.MaximumQi[index] * 80,
            CultivationBottleneckCause.Resources => _agents.BreakthroughPills[index] > 0,
            CultivationBottleneckCause.Stability =>
                resolvedActivity == AgentActivity.Recovering
                && _agents.Health[index] * 100 >= _agents.MaximumHealth[index] * 80
                && _agents.Qi[index] * 100 >= _agents.MaximumQi[index] * 80,
            _ => false,
        };
        if (!resolved)
        {
            return;
        }

        _agents.CultivationProgress[index] = GetCultivationRequirement(index);
        _agents.CultivationStates[index] = CultivationState.PreparingBreakthrough;
        _agents.CultivationBottleneckCauses[index] = CultivationBottleneckCause.None;
    }

    private int GetCultivationRequirement(int index) =>
        RealmRules.GetMinorStageRequiredWork(_agents.RealmStages[index]);

    private void AdvanceRealm(int index)
    {
        if (_agents.RealmLayers[index] < RealmRules.MinorStageCount)
        {
            _agents.RealmLayers[index]++;
            return;
        }

        if (_agents.RealmStages[index] < RealmStage.HalfImmortal)
        {
            _agents.RealmStages[index]++;
            _agents.RealmLayers[index] = 1;
        }
    }
}
