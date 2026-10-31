using Chronicles.Game.Cultivation;
using Chronicles.Game.Knowledge;
using KnowledgeEvidence = Chronicles.Game.Cultivation.KnowledgeEvidence;

namespace Chronicles.Game;

public sealed partial class GameWorld
{
    private const string CultivationPlanGoal = "cultivation";
    // Availability of a personal source is separate from the preparation understanding threshold.
    // A one-percent memory can still lead to real further learning or ordinary practice.
    private bool HasCultivationPlanningSource(Person p) => KnowledgeFor(p, "entry") is not null || Catalog.Paths.Any(path =>
        path.Realm == (p.Realm == Realm.Mortal ? Realm.Qi : p.Realm) && KnowledgeFor(p, path.Id) is not null);

    private string MortalCultivationPlanPath(Person p) => Catalog.Paths.FirstOrDefault(path =>
        path.Realm == Realm.Qi && KnowledgeFor(p, path.Id) is not null)?.Id ?? "path.1.0";

    private KnowledgeEvidence? CultivationPlanEvidence(Person p, string path) => CultivationEngine.PreparationKnowledge(
        state.Cultivation[p.BodyId], CultivationActorFor(p), path, CultivationContextFor(p));

    private static bool RecordedCultivationEvidence(string source, KnowledgeEvidence evidence) =>
        source.Contains($":{evidence.WorkId}:{evidence.Revision}:edition:{evidence.EditionId}:codes:", StringComparison.Ordinal) ||
        source.Contains($":{evidence.WorkId}:{evidence.Revision}:codes:", StringComparison.Ordinal);

    private static bool RecordedCultivationCode(string source, string code)
    {
        var offset = source.IndexOf(":codes:", StringComparison.Ordinal);
        return offset >= 0 && source[(offset + 7)..].Split(',').Contains(code, StringComparer.Ordinal);
    }

    private int CultivationLearningMinimum() => Math.Max(state.Knowledge.Rules.PracticeMinimumComprehension,
        (CultivationPolicyForWorld().MinimumUnderstanding + 99) / 100);

    private bool CultivationNeedsRelearning(Person p, string work)
    {
        var key = "cultivation:refresh:" + work;
        if (!p.Beliefs.TryGetValue(key, out var failure) || failure.Expires <= Month) return false;
        if (long.TryParse(failure.Value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var revision) &&
            KnowledgeFor(p, work) is { } learned && learned.PersonalRevision > revision)
        { p.Beliefs.Remove(key); return false; }
        return true;
    }

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
        var entryPath = p.Realm == Realm.Mortal ? MortalCultivationPlanPath(p) : "";
        var entryEvidence = p.Realm == Realm.Mortal ? CultivationPlanEvidence(p, entryPath) : null;
        var current = p.Realm == Realm.Mortal ? entryEvidence?.WorkId ?? "entry" : CurrentCultivationPath(p, "") ?? $"path.{(int)p.Realm}.0";
        var known = KnowledgeFor(p, current);
        if (known is null || known.Comprehension < CultivationLearningMinimum() || known.Belief == KnowledgeBelief.Contradicted || CultivationNeedsRelearning(p, current))
            return RequestPlanningKnowledge(p, current, CultivationPlanGoal);
        var ready = p.Realm == Realm.Mortal ? progress.EntryWork >= CultivationEngine.EntryTarget(progress, policy) : p.GreatPerfection;
        if (!ready || p.Realm == Realm.HalfImmortal)
        {
            p.Reason = "실제로 배운 현재 경지 지식·구도 성향·완료한 수행 습성";
            return Start(p, "cultivate", 0, "") is null;
        }
        var target = p.Realm == Realm.Mortal ? entryPath : NextKnownCultivationPath(p) ?? $"path.{(int)p.Realm + 1}.0";
        var evidence = entryEvidence ?? CultivationPlanEvidence(p, target);
        var requiredWork = evidence?.WorkId ?? target;
        var targetKnowledge = KnowledgeFor(p, requiredWork);
        if (targetKnowledge is null || targetKnowledge.Comprehension < CultivationLearningMinimum() || targetKnowledge.Belief == KnowledgeBelief.Contradicted || CultivationNeedsRelearning(p, requiredWork))
            return RequestPlanningKnowledge(p, requiredWork, CultivationPlanGoal);
        var blockedKey = "plan:blocked:cultivation:" + target;
        if (p.Beliefs.TryGetValue(blockedKey, out var blocked) && blocked.Expires > Month)
        {
            var memory = state.Knowledge.Memories.FindLast(m => m.PersonId == p.Id && m.WorkId == requiredWork && m.Chapter == 1);
            if ((evidence is not null && !RecordedCultivationEvidence(blocked.Source, evidence)) ||
                (memory is not null && memory.LearnedMonth > blocked.Observed)) p.Beliefs.Remove(blockedKey);
            else if (RecordedCultivationCode(blocked.Source, "control") || RecordedCultivationCode(blocked.Source, "sensing"))
            {
                p.Reason = "실제 준비 검사에서 확인한 육신·정신 제어 부족을 단련하며 재검사 기한을 기다림";
                return Start(p, "martial", 0, "") is null;
            }
            else return false;
        }
        cultivationPreparationFeedback = null;
        var error = Start(p, "breakthrough", 0, target);
        var codes = cultivationPreparationFeedback is { } feedback && feedback.Body == p.BodyId ?
            feedback.Blockers.Select(b => b.Code).ToArray() : [];
        cultivationPreparationFeedback = null;
        if (error is null)
        {
            p.Reason = "자기 학습 revision·준비 노동을 바탕으로 실제 자격 검사와 감응/돌파 준비를 선택";
            Remember(p, "cultivation:prepared", target, "실제 준비 명령·시도 " + state.Cultivation[p.BodyId].Attempt!.Id, 100);
            return true;
        }
        var source = $"actual-cultivation-preflight:{p.BodyId}:{Month}:{requiredWork}:{targetKnowledge.PersonalRevision}:edition:{targetKnowledge.EditionId}:codes:{string.Join(',', codes)}";
        BlockPlanningFact(p, "cultivation:" + target, error, source);
        Remember(p, "cultivation:lastFailure", error, source, 100);
        if (codes.Any(code => code is "interpretation" or "knowledge" or "consult"))
        {
            Remember(p, "cultivation:refresh:" + requiredWork, PlanningNumber(targetKnowledge.PersonalRevision), source, 100);
            // Preserve the selected goal's observed repair step; an unrelated production request must not
            // replace this correction and indefinitely monopolize the saved plan.
            return RequestPlanningKnowledge(p, requiredWork, CultivationPlanGoal);
        }
        p.Reason = "실제 준비 거절을 기록하여 같은 전제를 즉시 반복하지 않음: " + error;
        return false;
    }
}
