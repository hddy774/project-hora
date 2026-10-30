using Chronicles.Core;
using Chronicles.Simulation;

internal static class CultivationAttemptContractTests
{
    public static void Run()
    {
        ApprovedEffectCatalogStartsEmpty();
        UnapprovedEffectsNeverChangeChance();
        FailureDefaultsAreExplicitAndClean();
        ExternalFactorBoundsAreDeterministic();
    }

    private static void ApprovedEffectCatalogStartsEmpty()
    {
        TestAssert.Equal(0, CultivationAttemptEffectRegistry.All.Count,
            "Alpha 1.1.3 승인 Concrete Attempt Effect 수");
        var resolution = CultivationAttemptEffectRegistry.Resolve(
            BreakthroughClass.MajorRealm,
            ReadOnlySpan<CultivationAttemptEffectCandidate>.Empty);
        TestAssert.Equal(CultivationValueAvailability.Unapproved, resolution.Availability,
            "승인 Catalog 0개는 ActualZero가 아니라 Unapproved");
        TestAssert.Equal(0, resolution.RawModifierBasisPoints, "숨은 보정 0");
    }

    private static void UnapprovedEffectsNeverChangeChance()
    {
        var unapproved = new CultivationAttemptEffectCandidate(
            AuthorityRegistry.CreateStableId("cmip.cultivation.attempt-effect", "unapproved.test"),
            Revision: 1,
            Applicable: true);
        var resolution = CultivationAttemptEffectRegistry.Resolve(
            BreakthroughClass.MajorRealm,
            [unapproved]);
        var finalChance = CultivationRules.GetBreakthroughChanceBasisPoints(
            BreakthroughClass.MajorRealm,
            RealmStage.QiRefining,
            resolution,
            out var baseChance,
            out var modifier);

        TestAssert.Equal(CultivationValueAvailability.Unapproved, resolution.Availability,
            "미등록 Effect 상태");
        TestAssert.Equal(8_000, baseChance, "연기→축기 BaseChance");
        TestAssert.Equal(0, modifier, "미등록 Effect 보정 금지");
        TestAssert.Equal(baseChance, finalChance, "승인 Effect 0개 시 최종 확률은 BaseChance");
    }

    private static void FailureDefaultsAreExplicitAndClean()
    {
        var failure = CultivationFailureRiskRegistry.Resolve(BreakthroughClass.MajorRealm);
        TestAssert.Equal(CultivationFailureEffectKind.CleanFailure, failure.Effect,
            "승인 실패 효과가 없을 때 CleanFailure");
        TestAssert.Equal(0, failure.WorkLost, "숨은 Work 손실 금지");
        TestAssert.Equal(0, failure.ComponentLostHundredths, "숨은 Component 손실 금지");
        TestAssert.Equal(0, failure.InjuryPoints, "숨은 부상 금지");
        TestAssert.True(!failure.FatalBodyLoss, "숨은 육신 사망 금지");
        TestAssert.Equal(0L, failure.RetryBlockMonths, "전역 고정 재시도 대기 금지");
        TestAssert.Equal(CultivationValueAvailability.ActualZero, failure.Availability,
            "확정된 0과 미승인 값을 구분");
    }

    private static void ExternalFactorBoundsAreDeterministic()
    {
        TestAssert.Equal(8_000, CultivationRules.GetEnvironmentFactorBasisPoints(0),
            "환경 최저 계수");
        TestAssert.Equal(10_000, CultivationRules.GetEnvironmentFactorBasisPoints(5_000),
            "환경 중립 계수");
        TestAssert.Equal(12_000, CultivationRules.GetEnvironmentFactorBasisPoints(10_000),
            "환경 최고 계수");
        TestAssert.Equal(1_000, CultivationRules.GetMonthlyRealmWorkGain(
            aptitudeCompositeHundredths: 5_000,
            techniqueQualityHundredths: 5_000,
            environmentQualityHundredths: 5_000,
            bodyConditionBasisPoints: 10_000),
            "자질·공법·환경 중립값의 기준 월 Work");
        TestAssert.Equal("method.foundation.three_paths", FoundationMethodRules.StableId,
            "삼도축기요결 안정 ID");
        TestAssert.Equal(1, FoundationMethodRules.CurrentRevision,
            "삼도축기요결 revision");
        TestAssert.Equal(500, FoundationMethodRules.ProvisionalMasteryThresholdHundredths,
            "축기법 임시 숙련 사전검증 5.00");
        TestAssert.Equal(500, FoundationMethodRules.ProvisionalUnderstandingThresholdHundredths,
            "축기법 임시 이해 사전검증 5.00");
    }
}
