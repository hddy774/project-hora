using Chronicles.Core;

namespace Chronicles.Simulation;

public readonly record struct RealmProfile(
    RealmStage Stage,
    long MaximumLifespanYears)
{
    public const int CurrentRevision = 1;

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

    public bool RequiresStabilization => Stage is >= RealmStage.QiRefining and <= RealmStage.HalfImmortal;

    public decimal TargetCultivationYears => MaximumLifespanYears / 3m;
}

public static class RealmRules
{
    public const int MinorStageCount = 4;

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
        // 대승경 수명은 사용자 확정값이 없으므로 도겁경 수명을 그대로 유지한다.
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

    public static int GetMinorStageRequiredWork(RealmStage stage)
    {
        var lifespanTicks = checked(GetProfile(stage).MaximumLifespanYears * GameCalendar.TicksPerYear);
        const int divisor = 3 * MinorStageCount;
        return checked((int)((lifespanTicks + (divisor / 2)) / divisor));
    }

    public static bool IsFinalStage(RealmStage stage, int minorStage) =>
        stage == RealmStage.HalfImmortal && minorStage == MinorStageCount;

    public static bool IsValidMinorStage(RealmStage stage, int minorStage)
    {
        _ = GetProfile(stage);
        return minorStage is >= 1 and <= MinorStageCount;
    }
}
