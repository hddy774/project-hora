using Chronicles.Game.Cultivation;
using Chronicles.Game.Knowledge;

namespace Chronicles.Game;

public sealed partial class GameWorld
{
    private const string CultivationPlanGoal = "cultivation";

    private int CultivationLearningMinimum() => Math.Max(state.Knowledge.Rules.PracticeMinimumComprehension,
        (CultivationPolicyForWorld().MinimumUnderstanding + 99) / 100);

    // Chooses an action from personal learning and the body's saved work. The shared command performs
    // the actual eligibility preview, physical reservation and eventual one-sample resolution.
    private bool StartCultivationPlan(Person p)
    {
        var progress = state.Cultivation[p.BodyId];
        if (CultivationEngine.ActiveAttempt(progress) || progress.PendingTrial is not null)
        { ScheduleCultivation(p); return p.Action != ActionKind.Idle; }
        var policy = CultivationPolicyForWorld();
        if (progress.Damage > policy.FailureDamage || p.Health < 50)
        {
            p.Reason = "자기의 실제 육신·수행 손상을 회복한 뒤 준비 조건을 다시 확인";
            return Start(p, "cultivationrecover", 0, "") is null;
        }
        var current = p.Realm == Realm.Mortal ? "entry" : CurrentCultivationPath(p, "") ?? $"path.{(int)p.Realm}.0";
        var known = KnowledgeFor(p, current);
        if (known is null || known.Comprehension < CultivationLearningMinimum() || known.Belief == KnowledgeBelief.Contradicted)
            return RequestPlanningKnowledge(p, current, CultivationPlanGoal);
        var ready = p.Realm == Realm.Mortal ? progress.EntryWork >= CultivationEngine.EntryTarget(progress, policy) : p.GreatPerfection;
        if (!ready || p.Realm == Realm.HalfImmortal)
        {
            p.Reason = "실제로 배운 현재 경지 지식·구도 성향·완료한 수행 습성";
            return Start(p, "cultivate", 0, "") is null;
        }
        var target = p.Realm == Realm.Mortal ? "path.1.0" : NextKnownCultivationPath(p) ?? $"path.{(int)p.Realm + 1}.0";
        var requiredWork = p.Realm == Realm.Mortal ? "entry" : target;
        var targetKnowledge = KnowledgeFor(p, requiredWork);
        if (targetKnowledge is null || targetKnowledge.Comprehension < CultivationLearningMinimum() || targetKnowledge.Belief == KnowledgeBelief.Contradicted)
            return RequestPlanningKnowledge(p, requiredWork, CultivationPlanGoal);
        var blockedKey = "plan:blocked:cultivation:" + target;
        if (p.Beliefs.TryGetValue(blockedKey, out var blocked) && blocked.Expires > Month)
        {
            var memory = state.Knowledge.Memories.FindLast(m => m.PersonId == p.Id && m.WorkId == requiredWork && m.Chapter == 1);
            if (memory is not null && memory.LearnedMonth > blocked.Observed) p.Beliefs.Remove(blockedKey);
            else if (blocked.Value.Contains("control", StringComparison.Ordinal) || blocked.Value.Contains("sensing", StringComparison.Ordinal))
            {
                p.Reason = "실제 준비 검사에서 확인한 육신·정신 제어 부족을 단련하며 재검사 기한을 기다림";
                return Start(p, "martial", 0, "") is null;
            }
            else return false;
        }
        var error = Start(p, "breakthrough", 0, target);
        if (error is null)
        {
            p.Reason = "자기 학습 revision·준비 노동을 바탕으로 실제 자격 검사와 감응/돌파 준비를 선택";
            Remember(p, "cultivation:prepared", target, "실제 준비 명령·시도 " + state.Cultivation[p.BodyId].Attempt!.Id, 100);
            return true;
        }
        var source = $"actual-cultivation-preflight:{p.BodyId}:{Month}:{requiredWork}:{targetKnowledge.PersonalRevision}";
        BlockPlanningFact(p, "cultivation:" + target, error, source);
        Remember(p, "cultivation:lastFailure", error, source, 100);
        p.Reason = "실제 준비 거절을 기록하여 같은 전제를 즉시 반복하지 않음: " + error;
        return false;
    }
}
