namespace Chronicles.Game.Cultivation;

public static partial class CultivationEngine
{
    private static CultivationResult MartialTrain(CultivationState state, CultivationActor actor, TrainMartial command, CultivationContext context)
    {
        if (command.Intensity is <= 0 or > 20000 || context.Month <= state.Martial.LastMonth)
            return Reject(state, "martial-time", "무예 강도/월 경계 오류", "다음 월·유효 강도로 단련");
        if (ActiveAttempt(state) || state.PendingTrial is not null || context.Month <= state.LastTrainingMonth)
            return Reject(state, "capacity", "동일 월의 수행/형성 활동 용량 사용 중", "현재 공정 종결 또는 다음 월");
        if (actor.MartialStage == 10)
            return Reject(state, "martial-complete", "무인 독립 10경지 완성", "실전·회복·수선 등 다른 행동");
        // No root, realm, spiritual supply, bloodline or spiritual quality enters martial training.
        var work = (long)(actor.Body + actor.Mind) / 2 * command.Intensity / 10000 * actor.Health / 100;
        var injury = Math.Clamp(state.Martial.Injury + Math.Max(0, command.Intensity - 10000) / 10 - context.Policy.RecoveryPerMonth / 10, 0, 10000);
        var required = context.Policy.MartialMonthsPerStage * (actor.MartialStage + 1L) * context.Policy.FlowPerMonth;
        return Accept(state with { Martial = new(Math.Min(required, state.Martial.Work + work * (10000 - injury) / 10000), injury, context.Month),
            LastTrainingMonth = context.Month }, new(HealthChange: injury > state.Martial.Injury ? -1 : 0),
            notices: [new("무예 단련", "영근 비사용·독립 단계 단련", "실제 육신·정신 훈련")]);
    }

    private static CultivationResult MartialBreakthrough(CultivationState state, CultivationActor actor, CultivationContext context, int sample)
    {
        if (ActiveAttempt(state) || state.PendingTrial is not null || actor.MartialStage >= 10)
            return Reject(state, "martial", "현재 무예 돌파 불가", "현재 공정/단계 확인");
        var required = context.Policy.MartialMonthsPerStage * (actor.MartialStage + 1L) * context.Policy.FlowPerMonth;
        if (state.Martial.Work < required || actor.Health < 50 || state.Martial.Injury > context.Policy.FailureDamage)
            return Reject(state, "martial-foundation", "단련·육신·부상 조건 부족", "실제 단련·회복");
        if (sample < context.Policy.BaseRisk + state.Martial.Injury / 5)
            return Accept(state with { Martial = state.Martial with { Work = state.Martial.Work * 9 / 10,
                Injury = Math.Min(10000, state.Martial.Injury + context.Policy.FailureDamage) } }, new(HealthChange: -context.Policy.FailureHealthCost),
                notices: [new("무예 돌파 실패", "부상·재단련 필요", "실제 단련 판정")]);
        var stage = actor.MartialStage + 1;
        var years = Math.Min(context.Policy.MartialMaximumYears, 80 + (context.Policy.MartialMaximumYears - 80) * stage / 10);
        return Accept(state with { Martial = state.Martial with { Work = 0 } }, new(MartialStage: stage, LifespanFloorMonths: years * 12L),
            notices: [new("무예 돌파", $"무인 {stage}/10·기본수명 상한 {context.Policy.MartialMaximumYears}년, 수선 경지/출산 자격과 별도", "실제 단련 판정")]);
    }
}
