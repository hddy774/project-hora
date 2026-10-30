using Chronicles.Core;

namespace Chronicles.Simulation;

public enum FormulaOwner : byte
{
    Cultivation,
    Realm,
    Aptitude,
    Combat,
    SkillProgression,
    Economy,
}

public readonly record struct FormulaRegistryEntry(
    string StableId,
    int Revision,
    FormulaOwner Owner,
    string Purpose,
    bool RequiresReview)
{
    public RegistryStableId RegistryId =>
        AuthorityRegistry.CreateStableId("cmip.formula", StableId);
}

public sealed record WorldBalanceConfiguration(
    bool DeveloperModeEnabled = false,
    int CultivationWorkPercent = 100,
    int CombatAggressionPercent = 100,
    int EconomyActivityPercent = 100,
    int MonsterDangerPercent = 100)
{
    public static WorldBalanceConfiguration Default { get; } = new();
}

public static class WorldBalanceRules
{
    public const string ReviewId = "PC-TEMP-DEVELOPER-BALANCE-017";
    public const int MinimumPercent = 25;
    public const int MaximumPercent = 400;

    public static void Validate(WorldBalanceConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ValidatePercent(configuration.CultivationWorkPercent);
        ValidatePercent(configuration.CombatAggressionPercent);
        ValidatePercent(configuration.EconomyActivityPercent);
        ValidatePercent(configuration.MonsterDangerPercent);
    }

    public static int ApplyPercent(int value, int percent) =>
        checked((int)Math.Clamp(
            checked((long)value * percent / 100),
            1L,
            int.MaxValue));

    public static long ApplyPercent(long value, int percent) =>
        Math.Max(1L, checked(value * percent / 100));

    private static void ValidatePercent(int percent)
    {
        if (percent is < MinimumPercent or > MaximumPercent)
        {
            throw new ArgumentOutOfRangeException(
                nameof(percent),
                $"세계 설정 배율은 {MinimumPercent}%~{MaximumPercent}%여야 합니다.");
        }
    }
}

public static class FormulaRegistry
{
    private static readonly FormulaRegistryEntry[] Entries = BuildEntries();
    private static readonly Dictionary<string, FormulaRegistryEntry> ById =
        Entries.ToDictionary(entry => entry.StableId, StringComparer.Ordinal);

    public static IReadOnlyList<FormulaRegistryEntry> All => Entries;

    public static FormulaRegistryEntry Get(string stableId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stableId);
        return ById.TryGetValue(stableId, out var definition)
            ? definition
            : throw new KeyNotFoundException($"등록되지 않은 공식 ID입니다: {stableId}");
    }

    private static FormulaRegistryEntry[] BuildEntries()
    {
        var cultivation = CultivationFormulaRegistry.All
            .Select(definition => new FormulaRegistryEntry(
                definition.StableId,
                definition.Revision,
                FormulaOwner.Cultivation,
                definition.Purpose,
                definition.RequiresReview));

        return cultivation.Concat(
        [
            new("F-REALM-001", CultivationRules.FormulaRevision, FormulaOwner.Realm,
                "대경지 수명·전체 기준 수행 기간·소경지 분배", true),
            new(AptitudeRules.SummaryFormulaId, AptitudeRules.FormulaRevision, FormulaOwner.Aptitude,
                "60개 선천 자질 Component의 6대 대표값", false),
            new("F-COMBAT-001", CombatPowerFormula.CurrentRevision, FormulaOwner.Combat,
                "표시 전투력 구성", false),
            new("F-SKILL-001", SkillProgressionRules.CurrentRevision, FormulaOwner.SkillProgression,
                "공법·법술 숙련도와 이해도 진행", false),
            new("F-ECON-001", EconomyRules.CurrentRevision, FormulaOwner.Economy,
                "도시 시장 가격·수급·생산", true),
            new(WorldBalanceRules.ReviewId, 1, FormulaOwner.Cultivation,
                "PC 세계 설정 모드의 다음 세계용 명시적 밸런스 배율", true),
        ]).ToArray();
    }
}
