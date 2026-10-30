using Chronicles.Core;

namespace Chronicles.Simulation;

public enum WorldGenerationDomain : byte
{
    Map,
    Monsters,
    Items,
    Books,
    OngoingContent,
}

public enum GeneratedItemEffectKind : byte
{
    Attack,
    Defense,
    MaximumHealth,
    MaximumQi,
    CultivationEfficiency,
    BreakthroughStability,
    Penetration,
    Evasion,
}

public enum GeneratedBookEffectKind : byte
{
    TechniqueTraining,
    SpellTraining,
    TechniqueUnderstanding,
    SpellUnderstanding,
    CultivationInsight,
    CombatInsight,
}

public enum GeneratedMonsterTrait : byte
{
    Swift,
    Armored,
    Venomous,
    Regenerating,
    Piercing,
    SpiritDraining,
}

public readonly record struct GeneratedNameProfile(
    ushort PrefixToken,
    ushort CoreToken,
    ushort SuffixToken);

public sealed record WorldGenerationConfiguration(
    bool RandomizeMap = true,
    bool RandomizeMonsters = true,
    bool RandomizeItems = true,
    bool RandomizeBooks = true,
    bool IncludeFixedUniqueItems = true,
    int InitialRandomItemDefinitionCount = ContentPopulationRules.ApprovedItemDefinitionCount,
    int InitialItemInstanceCount = ContentPopulationRules.ApprovedItemInstanceCount,
    int InitialBookContentCount = ContentPopulationRules.ApprovedBookContentCount,
    int InitialBookEditionCount = ContentPopulationRules.ApprovedEditionCount,
    int InitialPhysicalBookCount = ContentPopulationRules.ApprovedPhysicalBookCount,
    int MonsterSpeciesPerBiome = WorldMapRules.MonstersPerWildBiome,
    int InitialMonsterPopulationPerHabitat = WorldMapRules.InitialMonsterPopulation,
    bool EnableOngoingContentGeneration = true,
    int ContentGenerationIntervalYears = 10,
    int ItemDefinitionsPerInterval = 4,
    int BookContentsPerInterval = 2)
{
    public static WorldGenerationConfiguration Default { get; } = new();
}

public static class WorldGenerationRules
{
    public const int CurrentRevision = 1;
    public const string ReviewId = "PC-TEMP-WORLD-GENERATION-021";
    public const string NameAndEffectReviewId = "PC-TEMP-PROCEDURAL-NAME-EFFECT-021";
    public const string OngoingGrowthReviewId = "PC-TEMP-CONTENT-GROWTH-021";

    // PC_TEMP_REVIEW: 아래 상한과 성장 주기는 대규모 SOAK 결과로 다음 버전에서 재검토한다.
    public const int MaximumRandomItemDefinitionCount = 2_000;
    public const int MaximumInitialItemInstanceCount = 100_000;
    public const int MaximumBookContentCount = 2_000;
    public const int MaximumBookEditionCount = 5_000;
    public const int MaximumPhysicalBookCount = 100_000;
    public const int MaximumMonsterSpeciesPerBiome = 12;
    public const int MaximumMonsterPopulationPerHabitat = 100_000;
    public const int MaximumContentGenerationIntervalYears = 10_000;
    public const int MaximumDefinitionsPerInterval = 100;

    public static void Validate(WorldGenerationConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration.InitialRandomItemDefinitionCount is < 4
            or > MaximumRandomItemDefinitionCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuration),
                $"무작위 아이템 종류는 4~{MaximumRandomItemDefinitionCount:N0}이어야 합니다.");
        }
        if (configuration.InitialItemInstanceCount is < 0
            or > MaximumInitialItemInstanceCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuration),
                $"초기 아이템 실물은 0~{MaximumInitialItemInstanceCount:N0}이어야 합니다.");
        }
        if (configuration.IncludeFixedUniqueItems
            && configuration.InitialItemInstanceCount < FixedUniqueItemTemplates.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuration),
                $"고정 유니크를 포함하려면 초기 아이템 실물이 {FixedUniqueItemTemplates.Count:N0}개 이상이어야 합니다.");
        }
        if (configuration.InitialBookContentCount is < 2 or > MaximumBookContentCount
            || configuration.InitialBookEditionCount < configuration.InitialBookContentCount
            || configuration.InitialBookEditionCount > MaximumBookEditionCount
            || configuration.InitialPhysicalBookCount is < 0 or > MaximumPhysicalBookCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuration),
                "서적 내용·판본·실물 수량의 범위가 잘못되었습니다.");
        }
        if (configuration.MonsterSpeciesPerBiome is < 1 or > MaximumMonsterSpeciesPerBiome
            || configuration.InitialMonsterPopulationPerHabitat is < 0
                or > MaximumMonsterPopulationPerHabitat)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuration),
                "요수 종족 수 또는 서식 개체 수의 범위가 잘못되었습니다.");
        }
        if (configuration.ContentGenerationIntervalYears is < 1
            or > MaximumContentGenerationIntervalYears
            || configuration.ItemDefinitionsPerInterval is < 0
                or > MaximumDefinitionsPerInterval
            || configuration.BookContentsPerInterval is < 0
                or > MaximumDefinitionsPerInterval)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuration),
                "지속 생성 주기 또는 회차별 생성 수량의 범위가 잘못되었습니다.");
        }
    }

    internal static ulong DomainSeed(
        ulong worldSeed,
        WorldGenerationDomain domain,
        bool randomized)
    {
        unchecked
        {
            return Mix(
                (randomized ? worldSeed : 0x434D_4950_4649_5845UL)
                ^ (((ulong)domain + 1UL) * 0x9E37_79B9_7F4A_7C15UL));
        }
    }

    internal static ulong Value(ulong domainSeed, long ordinal, ulong salt = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
        unchecked
        {
            return Mix(domainSeed
                ^ (((ulong)ordinal + 1UL) * 0xBF58_476D_1CE4_E5B9UL)
                ^ salt);
        }
    }

    internal static int Select(ulong value, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        return checked((int)(value % (ulong)count));
    }

    private static ulong Mix(ulong value)
    {
        unchecked
        {
            value ^= value >> 30;
            value *= 0xBF58_476D_1CE4_E5B9UL;
            value ^= value >> 27;
            value *= 0x94D0_49BB_1331_11EBUL;
            return value ^ (value >> 31);
        }
    }

    internal static IReadOnlyList<FixedUniqueItemTemplate> FixedUniqueItemTemplates { get; } =
    [
        new("fixed-unique.heaven-cleaving-sword", "천열검", "item.artifact.offense",
            GeneratedItemEffectKind.Attack, 10_000),
        new("fixed-unique.void-mirror", "태허명경", "item.artifact.defense",
            GeneratedItemEffectKind.Defense, 10_000),
        new("fixed-unique.primordial-growth-pill", "태초생생단", "item.pill.growth",
            GeneratedItemEffectKind.CultivationEfficiency, 10_000),
        new("fixed-unique.nine-cycle-pill", "구전파경단", "item.pill.breakthrough",
            GeneratedItemEffectKind.BreakthroughStability, 10_000),
    ];

    internal readonly record struct FixedUniqueItemTemplate(
        string StableRef,
        string KoreanName,
        string BaseDefinitionRef,
        GeneratedItemEffectKind Effect,
        ushort StrengthHundredths);
}

public readonly record struct GeneratedItemDefinitionCheckpoint(
    ulong GeneratedId,
    ulong BaseDefinitionId,
    int CatalogOrdinal,
    bool IsFixedUnique,
    ushort FixedUniqueTemplateOrdinal,
    GeneratedNameProfile Name,
    GeneratedItemEffectKind PrimaryEffect,
    ushort PrimaryStrengthHundredths,
    GeneratedItemEffectKind SecondaryEffect,
    ushort SecondaryStrengthHundredths,
    ushort QualityHundredths,
    long CreatedMonth);

public readonly record struct GeneratedBookDefinitionCheckpoint(
    ulong ContentId,
    int CatalogOrdinal,
    KnowledgeContentKind Kind,
    GeneratedNameProfile Name,
    GeneratedBookEffectKind PrimaryEffect,
    ushort PrimaryStrengthHundredths,
    GeneratedBookEffectKind SecondaryEffect,
    ushort SecondaryStrengthHundredths,
    long CreatedMonth);

public readonly record struct GeneratedMonsterDefinitionCheckpoint(
    ushort SpeciesId,
    BiomeType Biome,
    GeneratedNameProfile Name,
    int BaseDanger,
    MaterialId DropMaterial,
    GeneratedMonsterTrait Trait);

public sealed record WorldGenerationCheckpoint(
    int SchemaRevision,
    WorldGenerationConfiguration Configuration,
    long NextItemCatalogOrdinal,
    long NextBookCatalogOrdinal,
    long LastContentGenerationMonth,
    GeneratedItemDefinitionCheckpoint[] Items,
    GeneratedBookDefinitionCheckpoint[] Books,
    GeneratedMonsterDefinitionCheckpoint[] Monsters);

public readonly record struct GeneratedItemDefinitionSnapshot(
    ulong GeneratedId,
    int CatalogOrdinal,
    bool IsFixedUnique,
    string DisplayName,
    GeneratedItemEffectKind PrimaryEffect,
    ushort PrimaryStrengthHundredths,
    GeneratedItemEffectKind SecondaryEffect,
    ushort SecondaryStrengthHundredths,
    ushort QualityHundredths,
    long CreatedMonth);

public readonly record struct GeneratedBookDefinitionSnapshot(
    KnowledgeContentId ContentId,
    int CatalogOrdinal,
    string DisplayName,
    GeneratedBookEffectKind PrimaryEffect,
    ushort PrimaryStrengthHundredths,
    GeneratedBookEffectKind SecondaryEffect,
    ushort SecondaryStrengthHundredths,
    long CreatedMonth);

public static class GeneratedWorldNameFormatter
{
    private static readonly string[] ItemPrefixes =
    [
        "청명", "적하", "태허", "현천", "자전", "백련", "유광", "무극",
        "성진", "천뢰", "벽해", "금오", "은월", "구천", "혼원", "만상",
    ];
    private static readonly string[] ItemCores =
    [
        "운", "령", "진", "혼", "명", "허", "성", "화", "검", "경", "단", "인",
    ];
    private static readonly string[] ItemSuffixes =
    [
        "검", "환", "경", "갑", "패", "종", "단", "로", "주", "인", "기", "보",
    ];
    private static readonly string[] BookPrefixes =
    [
        "태상", "현문", "청허", "구전", "적명", "혼원", "자하", "천성",
        "만법", "무극", "금단", "성하", "벽운", "유명", "광한", "진령",
    ];
    private static readonly string[] BookCores =
    [
        "양생", "연기", "귀원", "축기", "어검", "호체", "진화", "신행", "파진", "관상", "응신", "통맥",
    ];
    private static readonly string[] BookSuffixes =
    [
        "경", "결", "록", "전", "요", "편", "법", "술", "도", "장", "비해", "진해",
    ];
    private static readonly string[] MonsterPrefixes =
    [
        "청목", "적각", "철피", "유광", "흑풍", "백린", "자전", "황사",
        "천뢰", "빙혼", "독운", "금익", "은월", "화린", "암혈", "벽해",
    ];
    private static readonly string[] MonsterCores =
    [
        "요", "영", "마", "환", "천", "독", "암", "뢰", "풍", "염", "빙", "산",
    ];
    private static readonly string[] MonsterSuffixes =
    [
        "랑", "사", "웅", "록", "우", "호", "원", "조", "망", "린", "표", "응",
    ];

    public static string FormatItem(GeneratedNameProfile value) =>
        Format(value, ItemPrefixes, ItemCores, ItemSuffixes);

    public static string FormatFixedUniqueItem(ushort templateOrdinal)
    {
        if (templateOrdinal == 0
            || templateOrdinal > WorldGenerationRules.FixedUniqueItemTemplates.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(templateOrdinal));
        }
        return WorldGenerationRules.FixedUniqueItemTemplates[templateOrdinal - 1].KoreanName;
    }

    public static string FormatBook(GeneratedNameProfile value) =>
        Format(value, BookPrefixes, BookCores, BookSuffixes);

    public static string FormatMonster(GeneratedNameProfile value) =>
        Format(value, MonsterPrefixes, MonsterCores, MonsterSuffixes);

    private static string Format(
        GeneratedNameProfile value,
        string[] prefixes,
        string[] cores,
        string[] suffixes) =>
        string.Concat(
            prefixes[value.PrefixToken % prefixes.Length],
            cores[value.CoreToken % cores.Length],
            suffixes[value.SuffixToken % suffixes.Length]);
}
