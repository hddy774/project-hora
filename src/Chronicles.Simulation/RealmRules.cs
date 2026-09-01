using Chronicles.Core;

namespace Chronicles.Simulation;

public readonly record struct RealmProfile(
    RealmStage Stage,
    long MaximumLifespanYears)
{
    public const int CurrentRevision = 2;

    public string StableId => Stage switch
    {
        RealmStage.QiRefining => "realm.qi_refining",
        RealmStage.FoundationEstablishment => "realm.foundation_establishment",
        RealmStage.GoldenCore => "realm.golden_core",
        RealmStage.NascentSoul => "realm.nascent_soul",
        RealmStage.SpiritTransformation => "realm.spirit_transformation",
        RealmStage.VoidRefinement => "realm.void_refinement",
        RealmStage.BodyIntegration => "realm.body_integration",
        RealmStage.TribulationCrossing => "realm.tribulation_crossing",
        RealmStage.Mahayana => "realm.mahayana",
        RealmStage.HalfImmortal => "realm.half_immortal",
        _ => throw new ArgumentOutOfRangeException(nameof(Stage)),
    };

    public int Revision => Stage is >= RealmStage.QiRefining and <= RealmStage.HalfImmortal
        ? CurrentRevision
        : throw new ArgumentOutOfRangeException(nameof(Stage));

    public bool RequiresStabilization => Stage is >= RealmStage.FoundationEstablishment
        and <= RealmStage.HalfImmortal;

    public decimal TargetCultivationYears => MaximumLifespanYears / 3m;
}

public readonly record struct RealmComponentDefinition(string StableId, string KoreanName);

public static class CultivationRules
{
    public const int FormulaRevision = 4;
    public const int EntryAgeYears = 5;
    public const int RequiredEntryWork = 30_000;
    public const int BaseEntryGain = 10_000;
    public const int BaselineAptitudeHundredths = 5_000;
    public const int BaseMonthlyWork = 1_000;
    public const int EntryGateCoreFloorHundredths = 4_000;
    public const int EntryGateThresholdHundredths = 6_000;
    public const int EntryGateSupportWeightBasisPoints = 1_500;
    public const int EntryGateSupportCapHundredths = 1_500;
    public const int EntryGateSlotCount = 3;
    public const int BaseEntryGateTemperingGainHundredths = 100;
    public const int LimitedCarryoverPercent = 25;
    public const int RealmComponentSlotCount = 4;
    public const int FormationSlotCount = 10;
    public const int NeutralFactorBasisPoints = 10_000;
    public const int MinimumEntryFactorBasisPoints = 2_500;
    public const int MaximumEntryFactorBasisPoints = 20_000;
    public const int ProvisionalStabilizationThresholdHundredths = 3_000;

    // Alpha 1.1.3에서 정확한 층별 배열은 미확정이다. 합계 1,000을 유지하는 임시 곡선이다.
    private static readonly int[] ProvisionalFloorWeights = [80, 90, 100, 105, 110, 115, 125, 135, 140];

    private static readonly RealmComponentDefinition[][] Components =
    [
        [],
        [
            new("foundation.capacity", "축기 용량"),
            new("foundation.quality", "축기 품질"),
            new("foundation.stability", "축기 안정성"),
        ],
        [
            new("golden_core.expansion", "금단 팽창"),
            new("golden_core.refinement", "금단 정련"),
            new("golden_core.reinforcement", "금단 강화"),
            new("golden_core.circulation", "금단 순환"),
        ],
        [
            new("nascent_soul.maturity", "원영 성숙"),
            new("nascent_soul.stability", "원영 안정"),
            new("nascent_soul.independence", "원영 독립"),
        ],
        [
            new("deity.ascension", "신혼 승화"),
            new("deity.manifestation", "화신 현현"),
            new("deity.law_resonance", "법칙 공명"),
        ],
        [], [], [], [], [],
    ];

    public static IReadOnlyList<ProvisionalBalanceDefinition> ProvisionalBalances { get; } =
    [
        new("cultivation.floor_weights", FormulaRevision, "80/90/100/105/110/115/125/135/140", "9층 합계 1,000 임시 성장 곡선", true),
        new("cultivation.aptitude_composite", FormulaRevision, "6대 선천 자질 산술평균", "50.00 기준 수행 기간 보존", true),
        new("cultivation.component_thresholds", FormulaRevision, "3→4 40.00, 6→7 60.00, 대원만 75.00", "Typed Component Gate 임시값", true),
        new("cultivation.method_mastery", FormulaRevision, "삼도축기요결 숙련도 25.00", "연기→축기 Method Preflight 임시값", true),
        new("cultivation.method_understanding", FormulaRevision, "삼도축기요결 이해도 25.00", "Method content·이해·실행 숙련 Preflight 임시값", true),
        new("cultivation.stabilization", FormulaRevision, "대경지 성공 후 1회 평가", "고정 연수 없이 최소 1회 commit", true),
        new("cultivation.stabilization_target", FormulaRevision, "Realm Component 평균 30.00", "Stabilization Target Set 임시값", true),
        new("cultivation.environment_factor", FormulaRevision, "영맥 품질 0.80~1.20", "환경·영맥 일반 수행 계수 임시값", true),
        new("cultivation.foundation_source", FormulaRevision, "인도 내부 기반·지도 영맥·천도 제2층천 현현", "지도·천도 Source 가용성 임시 PC 매핑", true),
        new("cultivation.entry_gate_tempering", FormulaRevision, "실패 Gate당 월 1.00 + 오성/100", "고정 선천자질 때문에 생기는 영구 입문 정체 방지", true),
    ];

    public static int GetInnateAptitudeCompositeHundredths(Aptitudes aptitudes) =>
        (aptitudes.SpiritualRoot.Hundredths
            + aptitudes.Bone.Hundredths
            + aptitudes.Constitution.Hundredths
            + aptitudes.Comprehension.Hundredths
            + aptitudes.Mind.Hundredths
            + aptitudes.Soul.Hundredths) / 6;

    public static int GetInnateAptitudeCompositeHundredths(
        ushort spiritualRoot,
        ushort bone,
        ushort constitution,
        ushort comprehension,
        ushort mind,
        ushort soul) =>
        (spiritualRoot + bone + constitution + comprehension + mind + soul) / 6;

    public static int GetMonthlyRealmWorkGain(int aptitudeCompositeHundredths)
    {
        var rateBasisPoints = Math.Clamp(
            checked(aptitudeCompositeHundredths * 10_000 / BaselineAptitudeHundredths),
            2_500,
            20_000);
        return checked(BaseMonthlyWork * rateBasisPoints / 10_000);
    }

    public static int GetMonthlyRealmWorkGain(
        int aptitudeCompositeHundredths,
        int techniqueQualityHundredths,
        int environmentQualityHundredths,
        int bodyConditionBasisPoints,
        int temporaryFactorBasisPoints = NeutralFactorBasisPoints)
    {
        var baseGain = GetMonthlyRealmWorkGain(aptitudeCompositeHundredths);
        var methodFactor = 8_000 + (Math.Clamp(
            techniqueQualityHundredths,
            0,
            QualityValue.MaximumHundredths) * 4 / 10);
        var environmentFactor = GetEnvironmentFactorBasisPoints(environmentQualityHundredths);
        var bodyFactor = Math.Clamp(5_000 + (bodyConditionBasisPoints / 2), 5_000, 10_000);
        var temporaryFactor = Math.Clamp(temporaryFactorBasisPoints, 8_000, 12_500);
        long gain = baseGain;
        gain = gain * methodFactor / NeutralFactorBasisPoints;
        gain = gain * environmentFactor / NeutralFactorBasisPoints;
        gain = gain * bodyFactor / NeutralFactorBasisPoints;
        gain = gain * temporaryFactor / NeutralFactorBasisPoints;
        return checked((int)Math.Clamp(gain, 1, int.MaxValue));
    }

    public static int GetEntryWorkGain(
        int aptitudeCompositeHundredths,
        int techniqueQualityHundredths,
        int bodyCompositeHundredths,
        int environmentFactorBasisPoints = NeutralFactorBasisPoints,
        int temporaryFactorBasisPoints = NeutralFactorBasisPoints)
    {
        var aptitudeFactor = 5_000 + aptitudeCompositeHundredths;
        var methodFactor = 8_000 + (techniqueQualityHundredths * 4 / 10);
        var environmentFactor = Math.Clamp(environmentFactorBasisPoints, 8_000, 12_000);
        var bodyFactor = 5_000 + (bodyCompositeHundredths / 2);
        var temporaryFactor = Math.Clamp(temporaryFactorBasisPoints, 8_000, 12_500);
        long raw = BaseEntryGain;
        raw = raw * aptitudeFactor / 10_000;
        raw = raw * methodFactor / 10_000;
        raw = raw * environmentFactor / 10_000;
        raw = raw * bodyFactor / 10_000;
        raw = raw * temporaryFactor / 10_000;
        const int minimum = BaseEntryGain * MinimumEntryFactorBasisPoints / NeutralFactorBasisPoints;
        const int maximum = BaseEntryGain * MaximumEntryFactorBasisPoints / NeutralFactorBasisPoints;
        return checked((int)Math.Clamp(raw, minimum, maximum));
    }

    public static int GetEnvironmentFactorBasisPoints(int environmentQualityHundredths) =>
        8_000 + (Math.Clamp(
            environmentQualityHundredths,
            0,
            QualityValue.MaximumHundredths) * 4 / 10);

    public static CultivationGateSnapshot EvaluateEntryGate(
        int gateNumber,
        int coreChannelHundredths,
        int supportCompositeHundredths,
        int temperingBonusHundredths = 0,
        bool explicitBlocker = false)
    {
        if (gateNumber is < 1 or > EntryGateSlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(gateNumber));
        }

        var effectiveCore = Math.Min(
            QualityValue.MaximumHundredths,
            checked(coreChannelHundredths + temperingBonusHundredths));
        var support = Math.Min(
            supportCompositeHundredths * EntryGateSupportWeightBasisPoints / 10_000,
            EntryGateSupportCapHundredths);
        var score = effectiveCore + support;
        var corePassed = effectiveCore >= EntryGateCoreFloorHundredths;
        var scorePassed = score >= EntryGateThresholdHundredths;
        return new CultivationGateSnapshot(
            gateNumber,
            coreChannelHundredths,
            supportCompositeHundredths,
            temperingBonusHundredths,
            score,
            corePassed,
            scorePassed,
            corePassed && scorePassed && !explicitBlocker);
    }

    public static BreakthroughClass GetAttemptClass(int floor, bool greatPerfection) =>
        greatPerfection
            ? BreakthroughClass.MajorRealm
            : floor switch
            {
                3 => BreakthroughClass.EarlyMiddleBottleneck,
                6 => BreakthroughClass.MiddleLateBottleneck,
                9 => BreakthroughClass.FloorNineCompletion,
                _ => BreakthroughClass.NormalFloor,
            };

    public static CarryoverPolicy GetCarryoverPolicy(int sourceFloor) => sourceFloor switch
    {
        3 or 6 => CarryoverPolicy.LimitedCarryover,
        9 => CarryoverPolicy.NoCarryover,
        _ => CarryoverPolicy.FullCarryover,
    };

    public static int GetBreakthroughChanceBasisPoints(
        BreakthroughClass attemptClass,
        RealmStage realm,
        CultivationAttemptEffectResolution effectResolution,
        out int baseChance,
        out int modifier)
    {
        int minimum;
        int maximum;
        int minimumModifier;
        int maximumModifier;
        switch (attemptClass)
        {
            case BreakthroughClass.NormalFloor:
            case BreakthroughClass.FloorNineCompletion:
                baseChance = 9_900;
                minimum = 9_700;
                maximum = 9_950;
                minimumModifier = -200;
                maximumModifier = 50;
                break;
            case BreakthroughClass.EarlyMiddleBottleneck:
                baseChance = 8_500;
                minimum = 7_000;
                maximum = 9_500;
                minimumModifier = -1_500;
                maximumModifier = 1_000;
                break;
            case BreakthroughClass.MiddleLateBottleneck:
                baseChance = 7_500;
                minimum = 5_500;
                maximum = 9_200;
                minimumModifier = -2_000;
                maximumModifier = 1_700;
                break;
            case BreakthroughClass.MajorRealm:
                (baseChance, minimum, maximum) = GetMajorChanceRange(realm);
                minimumModifier = minimum - baseChance;
                maximumModifier = maximum - baseChance;
                break;
            default:
                baseChance = 10_000;
                minimum = 10_000;
                maximum = 10_000;
                minimumModifier = 0;
                maximumModifier = 0;
                break;
        }

        modifier = effectResolution.Availability is CultivationValueAvailability.Actual
            or CultivationValueAvailability.ActualZero
                ? Math.Clamp(
                    effectResolution.RawModifierBasisPoints,
                    minimumModifier,
                    maximumModifier)
                : 0;
        return Math.Clamp(baseChance + modifier, minimum, maximum);
    }

    public static int GetRequiredComponentThresholdHundredths(int sourceFloor, bool greatPerfection) =>
        greatPerfection ? 7_500 : sourceFloor switch
        {
            3 => 4_000,
            6 => 6_000,
            _ => 0,
        };

    public static IReadOnlyList<RealmComponentDefinition> GetComponents(RealmStage realm) =>
        Components[(int)realm];

    private static (int Base, int Minimum, int Maximum) GetMajorChanceRange(RealmStage realm) => realm switch
    {
        RealmStage.QiRefining => (8_000, 6_000, 9_500),
        RealmStage.FoundationEstablishment => (7_000, 5_000, 9_000),
        RealmStage.GoldenCore => (6_000, 4_000, 8_500),
        RealmStage.NascentSoul => (5_000, 3_000, 8_000),
        RealmStage.SpiritTransformation => (4_500, 2_500, 7_500),
        RealmStage.VoidRefinement => (4_000, 2_000, 7_000),
        RealmStage.BodyIntegration => (3_500, 1_500, 6_500),
        RealmStage.TribulationCrossing => (3_000, 1_000, 6_000),
        RealmStage.Mahayana => (2_500, 800, 5_500),
        _ => (10_000, 10_000, 10_000),
    };
}

public static class FoundationMethodRules
{
    public const string StableId = "method.foundation.three_paths";
    public const int CurrentRevision = 1;
    public const int ProvisionalMasteryThresholdHundredths = 2_500;
    public const int ProvisionalUnderstandingThresholdHundredths = 2_500;
}

public static class RealmRules
{
    public const int MinorStageCount = 9;

    private static readonly RealmProfile[] Profiles =
    [
        new(RealmStage.QiRefining, 150),
        new(RealmStage.FoundationEstablishment, 300),
        new(RealmStage.GoldenCore, 600),
        new(RealmStage.NascentSoul, 1_200),
        new(RealmStage.SpiritTransformation, 3_000),
        new(RealmStage.VoidRefinement, 9_000),
        new(RealmStage.BodyIntegration, 20_000),
        new(RealmStage.TribulationCrossing, 100_000),
        // 대승경 수명은 사용자 확정값이 없어 기존 도겁경 수명을 임시로 유지한다.
        new(RealmStage.Mahayana, 100_000),
        new(RealmStage.HalfImmortal, 1_000_000),
    ];

    public static IReadOnlyList<RealmProfile> All => Profiles;

    public static RealmProfile GetProfile(RealmStage stage)
    {
        var index = (int)stage;
        if ((uint)index >= (uint)Profiles.Length || Profiles[index].Stage != stage)
        {
            throw new ArgumentOutOfRangeException(nameof(stage));
        }

        return Profiles[index];
    }

    public static int GetMinorStageRequiredWork(RealmStage stage, int floor)
    {
        if (floor is < 1 or > MinorStageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(floor));
        }

        var profile = GetProfile(stage);
        var work = checked(profile.MaximumLifespanYears * 4L * GetFloorWeight(floor));
        return checked((int)work);
    }

    public static int GetMinorStageRequiredWork(RealmStage stage) =>
        GetMinorStageRequiredWork(stage, 1);

    public static long GetTotalRealmRequiredWork(RealmStage stage)
    {
        long total = 0;
        for (var floor = 1; floor <= MinorStageCount; floor++)
        {
            total += GetMinorStageRequiredWork(stage, floor);
        }

        return total;
    }

    public static bool IsFinalStage(RealmStage stage, int minorStage) =>
        stage == RealmStage.HalfImmortal && minorStage == MinorStageCount;

    public static bool IsValidMinorStage(RealmStage stage, int minorStage)
    {
        _ = GetProfile(stage);
        return minorStage is >= 1 and <= MinorStageCount;
    }

    private static int GetFloorWeight(int floor) => floor switch
    {
        1 => 80,
        2 => 90,
        3 => 100,
        4 => 105,
        5 => 110,
        6 => 115,
        7 => 125,
        8 => 135,
        9 => 140,
        _ => throw new ArgumentOutOfRangeException(nameof(floor)),
    };
}
