using Chronicles.Core;

namespace Chronicles.Simulation;

/// <summary>
/// 세계 생성 시드에서 파생된 정의 카탈로그다. 게임플레이 RNG를 소비하지 않으며,
/// 도메인별 시드를 분리해 한 영역의 수량 변경이 다른 영역의 결과를 바꾸지 않는다.
/// </summary>
public sealed class WorldGenerationCatalog
{
    private readonly ulong _worldSeed;
    private readonly List<GeneratedItemDefinitionCheckpoint> _items;
    private readonly List<GeneratedBookDefinitionCheckpoint> _books;
    private readonly Dictionary<ulong, int> _itemIndexById;
    private readonly Dictionary<ulong, int> _bookIndexById;
    private long _nextItemCatalogOrdinal;
    private long _nextBookCatalogOrdinal;
    private long _lastContentGenerationMonth;
    private long _nextContentGenerationMonth;

    private WorldGenerationCatalog(
        ulong worldSeed,
        WorldGenerationConfiguration configuration,
        long nextItemCatalogOrdinal,
        long nextBookCatalogOrdinal,
        long lastContentGenerationMonth,
        IEnumerable<GeneratedItemDefinitionCheckpoint> items,
        IEnumerable<GeneratedBookDefinitionCheckpoint> books,
        IEnumerable<GeneratedMonsterDefinitionCheckpoint> monsters)
    {
        _worldSeed = worldSeed;
        Configuration = configuration;
        _nextItemCatalogOrdinal = nextItemCatalogOrdinal;
        _nextBookCatalogOrdinal = nextBookCatalogOrdinal;
        _lastContentGenerationMonth = lastContentGenerationMonth;
        _nextContentGenerationMonth = GetNextContentGenerationMonth(
            configuration,
            lastContentGenerationMonth);
        _items = items.ToList();
        _books = books.ToList();
        Monsters = monsters.ToArray();
        _itemIndexById = _items
            .Select((value, index) => (value.GeneratedId, index))
            .ToDictionary(value => value.GeneratedId, value => value.index);
        _bookIndexById = _books
            .Select((value, index) => (value.ContentId, index))
            .ToDictionary(value => value.ContentId, value => value.index);
        Map = new WorldMapCatalog(worldSeed, configuration, Monsters);
        Geography = WorldGeographyRules.CreateBlueprint(worldSeed, configuration);
    }

    public WorldGenerationConfiguration Configuration { get; }
    internal ulong WorldSeed => _worldSeed;
    public IReadOnlyList<GeneratedItemDefinitionCheckpoint> Items => _items;
    public IReadOnlyList<GeneratedBookDefinitionCheckpoint> Books => _books;
    public IReadOnlyList<GeneratedMonsterDefinitionCheckpoint> Monsters { get; }
    public WorldMapCatalog Map { get; }
    public WorldGeographyBlueprint Geography { get; }
    public long LastContentGenerationMonth => _lastContentGenerationMonth;

    public static WorldGenerationCatalog Create(
        ulong worldSeed,
        WorldGenerationConfiguration configuration)
    {
        WorldGenerationRules.Validate(configuration);
        var items = new List<GeneratedItemDefinitionCheckpoint>(
            configuration.InitialRandomItemDefinitionCount
            + (configuration.IncludeFixedUniqueItems
                ? WorldGenerationRules.FixedUniqueItemTemplates.Count
                : 0));
        long nextItemOrdinal = 1;
        if (configuration.IncludeFixedUniqueItems)
        {
            for (var index = 0;
                index < WorldGenerationRules.FixedUniqueItemTemplates.Count;
                index++)
            {
                items.Add(CreateFixedUniqueItem(index, checked((int)nextItemOrdinal++)));
            }
        }
        for (var index = 0; index < configuration.InitialRandomItemDefinitionCount; index++)
        {
            items.Add(CreateRandomItem(
                worldSeed,
                configuration,
                checked((int)nextItemOrdinal++),
                createdMonth: 0));
        }

        var books = new List<GeneratedBookDefinitionCheckpoint>(
            configuration.InitialBookContentCount);
        long nextBookOrdinal = 1;
        for (var index = 0; index < configuration.InitialBookContentCount; index++)
        {
            books.Add(CreateBook(
                worldSeed,
                configuration,
                checked((int)nextBookOrdinal++),
                createdMonth: 0));
        }

        var monsters = CreateMonsters(worldSeed, configuration);
        return new WorldGenerationCatalog(
            worldSeed,
            configuration,
            nextItemOrdinal,
            nextBookOrdinal,
            lastContentGenerationMonth: 0,
            items,
            books,
            monsters);
    }

    public static WorldGenerationCatalog Restore(
        ulong worldSeed,
        WorldGenerationCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        WorldGenerationRules.Validate(checkpoint.Configuration);
        if (checkpoint.SchemaRevision != WorldGenerationRules.CurrentRevision
            || checkpoint.NextItemCatalogOrdinal <= 0
            || checkpoint.NextBookCatalogOrdinal <= 0
            || checkpoint.LastContentGenerationMonth < 0
            || checkpoint.Items is null
            || checkpoint.Books is null
            || checkpoint.Monsters is null)
        {
            throw new InvalidDataException("세계 생성 카탈로그 checkpoint가 잘못되었습니다.");
        }

        ValidateItems(
            checkpoint.Configuration,
            checkpoint.Items,
            checkpoint.NextItemCatalogOrdinal,
            checkpoint.LastContentGenerationMonth);
        ValidateBooks(
            checkpoint.Books,
            checkpoint.NextBookCatalogOrdinal,
            checkpoint.LastContentGenerationMonth);
        ValidateMonsters(checkpoint.Configuration, checkpoint.Monsters);
        return new WorldGenerationCatalog(
            worldSeed,
            checkpoint.Configuration,
            checkpoint.NextItemCatalogOrdinal,
            checkpoint.NextBookCatalogOrdinal,
            checkpoint.LastContentGenerationMonth,
            checkpoint.Items,
            checkpoint.Books,
            checkpoint.Monsters);
    }

    public WorldGenerationCheckpoint CreateCheckpoint() => new(
        WorldGenerationRules.CurrentRevision,
        Configuration,
        _nextItemCatalogOrdinal,
        _nextBookCatalogOrdinal,
        _lastContentGenerationMonth,
        _items.ToArray(),
        _books.ToArray(),
        Monsters.ToArray());

    public bool TryGetItem(ulong generatedId, out GeneratedItemDefinitionCheckpoint value)
    {
        if (_itemIndexById.TryGetValue(generatedId, out var index))
        {
            value = _items[index];
            return true;
        }
        value = default;
        return false;
    }

    public GeneratedItemDefinitionCheckpoint ResolveItem(
        ContentDefinitionId baseDefinition,
        ulong generatedId,
        long instanceId)
    {
        if (generatedId != 0 && TryGetItem(generatedId, out var exact))
        {
            return exact;
        }

        // schema 27~29의 실물에는 생성 ID가 없다. 고정 유니크는 명시적으로
        // 생성 ID가 연결된 단 하나의 실물에만 표시되어야 하므로 이전 fallback에서 제외한다.
        var candidates = _items.Where(value =>
            !value.IsFixedUnique && value.BaseDefinitionId == baseDefinition.Value).ToArray();
        if (candidates.Length == 0)
        {
            throw new InvalidDataException($"생성 아이템 범주가 없습니다: {baseDefinition}");
        }
        var selector = WorldGenerationRules.Value(
            WorldGenerationRules.DomainSeed(
                _worldSeed,
                WorldGenerationDomain.Items,
                Configuration.RandomizeItems),
            Math.Max(0, instanceId - 1),
            baseDefinition.Value);
        return candidates[WorldGenerationRules.Select(selector, candidates.Length)];
    }

    public bool TryGetBook(
        KnowledgeContentId contentId,
        out GeneratedBookDefinitionCheckpoint value)
    {
        if (_bookIndexById.TryGetValue(contentId.Value, out var index))
        {
            value = _books[index];
            return true;
        }
        value = default;
        return false;
    }

    public GeneratedWorldExpansion AdvanceContentGeneration(long currentMonth)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(currentMonth);
        if (!Configuration.EnableOngoingContentGeneration)
        {
            _lastContentGenerationMonth = currentMonth;
            return GeneratedWorldExpansion.Empty;
        }

        if (_nextContentGenerationMonth <= _lastContentGenerationMonth
            || currentMonth < _nextContentGenerationMonth)
        {
            return GeneratedWorldExpansion.Empty;
        }

        var intervalMonths = checked(
            (long)Configuration.ContentGenerationIntervalYears * GameCalendar.MonthsPerYear);
        var due = checked(
            ((currentMonth - _nextContentGenerationMonth) / intervalMonths) + 1);
        var itemCount = checked((int)Math.Min(
            int.MaxValue,
            due * Configuration.ItemDefinitionsPerInterval));
        var bookCount = checked((int)Math.Min(
            int.MaxValue,
            due * Configuration.BookContentsPerInterval));
        var generatedItems = itemCount == 0
            ? Array.Empty<GeneratedItemDefinitionCheckpoint>()
            : new GeneratedItemDefinitionCheckpoint[itemCount];
        var generatedBooks = bookCount == 0
            ? Array.Empty<GeneratedBookDefinitionCheckpoint>()
            : new GeneratedBookDefinitionCheckpoint[bookCount];
        _items.EnsureCapacity(checked(_items.Count + itemCount));
        _books.EnsureCapacity(checked(_books.Count + bookCount));
        _itemIndexById.EnsureCapacity(checked(_itemIndexById.Count + itemCount));
        _bookIndexById.EnsureCapacity(checked(_bookIndexById.Count + bookCount));
        var itemWrite = 0;
        var bookWrite = 0;
        for (long cycle = 1; cycle <= due; cycle++)
        {
            var createdMonth = checked(_lastContentGenerationMonth + (cycle * intervalMonths));
            for (var index = 0; index < Configuration.ItemDefinitionsPerInterval; index++)
            {
                var item = CreateRandomItem(
                    _worldSeed,
                    Configuration,
                    checked((int)_nextItemCatalogOrdinal++),
                    createdMonth);
                _itemIndexById.Add(item.GeneratedId, _items.Count);
                _items.Add(item);
                generatedItems[itemWrite++] = item;
            }
            for (var index = 0; index < Configuration.BookContentsPerInterval; index++)
            {
                var book = CreateBook(
                    _worldSeed,
                    Configuration,
                    checked((int)_nextBookCatalogOrdinal++),
                    createdMonth);
                _bookIndexById.Add(book.ContentId, _books.Count);
                _books.Add(book);
                generatedBooks[bookWrite++] = book;
            }
        }
        _lastContentGenerationMonth = checked(_lastContentGenerationMonth + (due * intervalMonths));
        _nextContentGenerationMonth = GetNextContentGenerationMonth(
            Configuration,
            _lastContentGenerationMonth);
        return new GeneratedWorldExpansion(generatedItems, generatedBooks);
    }

    private static long GetNextContentGenerationMonth(
        WorldGenerationConfiguration configuration,
        long lastContentGenerationMonth)
    {
        if (!configuration.EnableOngoingContentGeneration)
        {
            return long.MaxValue;
        }
        var intervalMonths = checked(
            (long)configuration.ContentGenerationIntervalYears * GameCalendar.MonthsPerYear);
        return lastContentGenerationMonth > long.MaxValue - intervalMonths
            ? long.MaxValue
            : lastContentGenerationMonth + intervalMonths;
    }

    public ulong ComputeStateHash()
    {
        var hash = 14695981039346656037UL;
        Add(ref hash, checked((ulong)WorldGenerationRules.CurrentRevision));
        AddConfigurationHash(ref hash, Configuration);
        Add(ref hash, checked((ulong)_nextItemCatalogOrdinal));
        Add(ref hash, checked((ulong)_nextBookCatalogOrdinal));
        Add(ref hash, checked((ulong)_lastContentGenerationMonth));
        foreach (var value in _items)
        {
            Add(ref hash, value.GeneratedId);
            Add(ref hash, value.BaseDefinitionId);
            Add(ref hash, checked((ulong)value.CatalogOrdinal));
            Add(ref hash, value.IsFixedUnique ? 1UL : 0UL);
            Add(ref hash, value.FixedUniqueTemplateOrdinal);
            Add(ref hash, value.Name.PrefixToken);
            Add(ref hash, value.Name.CoreToken);
            Add(ref hash, value.Name.SuffixToken);
            Add(ref hash, (byte)value.PrimaryEffect);
            Add(ref hash, value.PrimaryStrengthHundredths);
            Add(ref hash, (byte)value.SecondaryEffect);
            Add(ref hash, value.SecondaryStrengthHundredths);
            Add(ref hash, value.QualityHundredths);
            Add(ref hash, checked((ulong)value.CreatedMonth));
        }
        foreach (var value in _books)
        {
            Add(ref hash, value.ContentId);
            Add(ref hash, checked((ulong)value.CatalogOrdinal));
            Add(ref hash, (byte)value.Kind);
            Add(ref hash, value.Name.PrefixToken);
            Add(ref hash, value.Name.CoreToken);
            Add(ref hash, value.Name.SuffixToken);
            Add(ref hash, (byte)value.PrimaryEffect);
            Add(ref hash, value.PrimaryStrengthHundredths);
            Add(ref hash, (byte)value.SecondaryEffect);
            Add(ref hash, value.SecondaryStrengthHundredths);
            Add(ref hash, checked((ulong)value.CreatedMonth));
        }
        foreach (var value in Monsters)
        {
            Add(ref hash, value.SpeciesId);
            Add(ref hash, (byte)value.Biome);
            Add(ref hash, value.Name.PrefixToken);
            Add(ref hash, value.Name.CoreToken);
            Add(ref hash, value.Name.SuffixToken);
            Add(ref hash, checked((ulong)value.BaseDanger));
            Add(ref hash, (byte)value.DropMaterial);
            Add(ref hash, (byte)value.Trait);
        }
        return hash;
    }

    private static GeneratedItemDefinitionCheckpoint CreateFixedUniqueItem(
        int templateIndex,
        int catalogOrdinal)
    {
        var template = WorldGenerationRules.FixedUniqueItemTemplates[templateIndex];
        var definition = ContentDefinitionRegistry.Get(template.BaseDefinitionRef);
        return new GeneratedItemDefinitionCheckpoint(
            AuthorityRegistry.CreateStableId(
                "cmip.generated-item.fixed",
                template.StableRef).Value,
            definition.Id.Value,
            catalogOrdinal,
            IsFixedUnique: true,
            checked((ushort)(templateIndex + 1)),
            default,
            template.Effect,
            template.StrengthHundredths,
            SecondaryEffectFor(definition.StableRef),
            template.StrengthHundredths,
            QualityValue.MaximumHundredths,
            CreatedMonth: 0);
    }

    private static GeneratedItemDefinitionCheckpoint CreateRandomItem(
        ulong worldSeed,
        WorldGenerationConfiguration configuration,
        int catalogOrdinal,
        long createdMonth)
    {
        var seed = WorldGenerationRules.DomainSeed(
            worldSeed,
            WorldGenerationDomain.Items,
            configuration.RandomizeItems);
        var value = WorldGenerationRules.Value(seed, catalogOrdinal - 1);
        var definitionRef = ItemDefinitionRefs[(catalogOrdinal - 1) % ItemDefinitionRefs.Length];
        var definition = ContentDefinitionRegistry.Get(definitionRef);
        var primary = PrimaryEffectFor(definitionRef, value);
        var secondary = SecondaryEffectFor(definitionRef, value >> 7);
        var quality = checked((ushort)(1_000 + WorldGenerationRules.Select(value >> 11, 7_000)));
        return new GeneratedItemDefinitionCheckpoint(
            AuthorityRegistry.CreateStableId(
                "cmip.generated-item.procedural",
                $"{seed:X16}.{catalogOrdinal:00000000}").Value,
            definition.Id.Value,
            catalogOrdinal,
            IsFixedUnique: false,
            FixedUniqueTemplateOrdinal: 0,
            CreateName(value),
            primary,
            CalculateStrength(value >> 19, quality),
            secondary,
            CalculateStrength(value >> 31, quality),
            quality,
            createdMonth);
    }

    private static GeneratedBookDefinitionCheckpoint CreateBook(
        ulong worldSeed,
        WorldGenerationConfiguration configuration,
        int catalogOrdinal,
        long createdMonth)
    {
        var seed = WorldGenerationRules.DomainSeed(
            worldSeed,
            WorldGenerationDomain.Books,
            configuration.RandomizeBooks);
        var value = WorldGenerationRules.Value(seed, catalogOrdinal - 1);
        var kind = (catalogOrdinal & 1) == 1
            ? KnowledgeContentKind.Technique
            : KnowledgeContentKind.Spell;
        var primarySelector = WorldGenerationRules.Select(value >> 5, 3);
        var primary = kind == KnowledgeContentKind.Technique
            ? primarySelector switch
            {
                0 => GeneratedBookEffectKind.TechniqueTraining,
                1 => GeneratedBookEffectKind.TechniqueUnderstanding,
                _ => GeneratedBookEffectKind.CultivationInsight,
            }
            : primarySelector switch
            {
                0 => GeneratedBookEffectKind.SpellTraining,
                1 => GeneratedBookEffectKind.SpellUnderstanding,
                _ => GeneratedBookEffectKind.CombatInsight,
            };
        var secondary = (GeneratedBookEffectKind)WorldGenerationRules.Select(value >> 17, 6);
        return new GeneratedBookDefinitionCheckpoint(
            AuthorityRegistry.CreateStableId(
                "cmip.knowledge-content.generated",
                $"{seed:X16}.{catalogOrdinal:00000000}").Value,
            catalogOrdinal,
            kind,
            CreateName(value),
            primary,
            checked((ushort)(500 + WorldGenerationRules.Select(value >> 23, 4_501))),
            secondary,
            checked((ushort)(250 + WorldGenerationRules.Select(value >> 37, 2_251))),
            createdMonth);
    }

    private static GeneratedMonsterDefinitionCheckpoint[] CreateMonsters(
        ulong worldSeed,
        WorldGenerationConfiguration configuration)
    {
        var count = checked(configuration.MonsterSpeciesPerBiome * 3);
        var result = new GeneratedMonsterDefinitionCheckpoint[count];
        var seed = WorldGenerationRules.DomainSeed(
            worldSeed,
            WorldGenerationDomain.Monsters,
            configuration.RandomizeMonsters);
        for (var index = 0; index < result.Length; index++)
        {
            var value = WorldGenerationRules.Value(seed, index);
            var biome = (BiomeType)(1 + (index / configuration.MonsterSpeciesPerBiome));
            var dangerBase = biome switch
            {
                BiomeType.Forest => 800,
                BiomeType.Plains => 700,
                BiomeType.Mountains => 1_100,
                _ => throw new InvalidDataException("요수 지형이 잘못되었습니다."),
            };
            result[index] = new GeneratedMonsterDefinitionCheckpoint(
                checked((ushort)index),
                biome,
                CreateName(value),
                checked(dangerBase + WorldGenerationRules.Select(value >> 13, 1_601)),
                WorldGenerationRules.Select(value >> 29, 2) == 0
                    ? MaterialId.PillMaterial
                    : MaterialId.TreasureMaterial,
                (GeneratedMonsterTrait)WorldGenerationRules.Select(value >> 41, 6));
        }
        return result;
    }

    private static GeneratedNameProfile CreateName(ulong value) => new(
        checked((ushort)(value & 0xFFFF)),
        checked((ushort)((value >> 16) & 0xFFFF)),
        checked((ushort)((value >> 32) & 0xFFFF)));

    private static ushort CalculateStrength(ulong value, ushort quality) =>
        checked((ushort)Math.Clamp(
            250 + (quality / 2) + WorldGenerationRules.Select(value, 1_001),
            0,
            QualityValue.MaximumHundredths));

    private static GeneratedItemEffectKind PrimaryEffectFor(
        string definitionRef,
        ulong selector = 0) => definitionRef switch
        {
            "item.artifact.offense" => WorldGenerationRules.Select(selector, 2) == 0
                ? GeneratedItemEffectKind.Attack
                : GeneratedItemEffectKind.Penetration,
            "item.artifact.defense" => WorldGenerationRules.Select(selector, 2) == 0
                ? GeneratedItemEffectKind.Defense
                : GeneratedItemEffectKind.Evasion,
            "item.pill.growth" => WorldGenerationRules.Select(selector, 2) == 0
                ? GeneratedItemEffectKind.CultivationEfficiency
                : GeneratedItemEffectKind.MaximumQi,
            "item.pill.breakthrough" => WorldGenerationRules.Select(selector, 2) == 0
                ? GeneratedItemEffectKind.BreakthroughStability
                : GeneratedItemEffectKind.MaximumHealth,
            _ => GeneratedItemEffectKind.Attack,
        };

    private static GeneratedItemEffectKind SecondaryEffectFor(
        string definitionRef,
        ulong selector = 0) => definitionRef switch
        {
            "item.artifact.offense" => GeneratedItemEffectKind.Penetration,
            "item.artifact.defense" => GeneratedItemEffectKind.MaximumHealth,
            "item.pill.growth" => GeneratedItemEffectKind.CultivationEfficiency,
            "item.pill.breakthrough" => GeneratedItemEffectKind.BreakthroughStability,
            _ => WorldGenerationRules.Select(selector, 2) == 0
                ? GeneratedItemEffectKind.MaximumQi
                : GeneratedItemEffectKind.MaximumHealth,
        };

    private static void ValidateItems(
        WorldGenerationConfiguration configuration,
        GeneratedItemDefinitionCheckpoint[] values,
        long nextCatalogOrdinal,
        long lastGeneratedMonth)
    {
        if (values.Length == 0
            || values.Any(value => value.GeneratedId == 0
                || value.BaseDefinitionId == 0
                || value.CatalogOrdinal <= 0
                || value.CreatedMonth < 0
                || value.CreatedMonth > lastGeneratedMonth
                || !Enum.IsDefined(value.PrimaryEffect)
                || !Enum.IsDefined(value.SecondaryEffect)
                || value.QualityHundredths > QualityValue.MaximumHundredths
                || value.PrimaryStrengthHundredths > QualityValue.MaximumHundredths
                || value.SecondaryStrengthHundredths > QualityValue.MaximumHundredths)
            || values.Select(value => value.GeneratedId).Distinct().Count() != values.Length
            || values.Select(value => value.CatalogOrdinal).Distinct().Count() != values.Length
            || nextCatalogOrdinal != checked((long)values.Max(value => value.CatalogOrdinal) + 1)
            || values.Count(value => value.IsFixedUnique)
                != (configuration.IncludeFixedUniqueItems
                    ? WorldGenerationRules.FixedUniqueItemTemplates.Count
                    : 0)
            || values.Any(value => value.IsFixedUnique
                ? value.FixedUniqueTemplateOrdinal is 0
                    || value.FixedUniqueTemplateOrdinal
                        > WorldGenerationRules.FixedUniqueItemTemplates.Count
                : value.FixedUniqueTemplateOrdinal != 0))
        {
            throw new InvalidDataException("생성 아이템 카탈로그가 잘못되었습니다.");
        }

        foreach (var value in values)
        {
            if (!ContentDefinitionRegistry.TryGet(
                    new ContentDefinitionId(value.BaseDefinitionId),
                    out var definition)
                || definition.Kind != ContentDefinitionKind.Item)
            {
                throw new InvalidDataException(
                    $"생성 아이템 기본 Definition이 잘못되었습니다: {value.BaseDefinitionId:X16}");
            }
        }
    }

    private static void ValidateBooks(
        GeneratedBookDefinitionCheckpoint[] values,
        long nextCatalogOrdinal,
        long lastGeneratedMonth)
    {
        if (values.Length < 2
            || values.Any(value => value.ContentId == 0
                || value.CatalogOrdinal <= 0
                || value.CreatedMonth < 0
                || value.CreatedMonth > lastGeneratedMonth
                || value.Kind is not (KnowledgeContentKind.Technique or KnowledgeContentKind.Spell)
                || !Enum.IsDefined(value.PrimaryEffect)
                || !Enum.IsDefined(value.SecondaryEffect)
                || value.PrimaryStrengthHundredths > QualityValue.MaximumHundredths
                || value.SecondaryStrengthHundredths > QualityValue.MaximumHundredths)
            || values.Select(value => value.ContentId).Distinct().Count() != values.Length
            || values.Select(value => value.CatalogOrdinal).Distinct().Count() != values.Length
            || nextCatalogOrdinal != checked((long)values.Max(value => value.CatalogOrdinal) + 1))
        {
            throw new InvalidDataException("생성 서적 카탈로그가 잘못되었습니다.");
        }
    }

    private static void ValidateMonsters(
        WorldGenerationConfiguration configuration,
        GeneratedMonsterDefinitionCheckpoint[] values)
    {
        if (values.Length != configuration.MonsterSpeciesPerBiome * 3
            || values.Where((value, index) =>
                    value.SpeciesId != index
                    || value.BaseDanger <= 0
                    || value.Biome
                        != (BiomeType)(1 + (index / configuration.MonsterSpeciesPerBiome))
                    || !Enum.IsDefined(value.DropMaterial)
                    || !Enum.IsDefined(value.Trait))
                .Any()
            || values.Select(value => value.SpeciesId).Distinct().Count() != values.Length)
        {
            throw new InvalidDataException("생성 요수 카탈로그가 잘못되었습니다.");
        }
    }

    private static void AddConfigurationHash(
        ref ulong hash,
        WorldGenerationConfiguration value)
    {
        Add(ref hash, value.RandomizeMap ? 1UL : 0UL);
        Add(ref hash, value.RandomizeMonsters ? 1UL : 0UL);
        Add(ref hash, value.RandomizeItems ? 1UL : 0UL);
        Add(ref hash, value.RandomizeBooks ? 1UL : 0UL);
        Add(ref hash, value.IncludeFixedUniqueItems ? 1UL : 0UL);
        Add(ref hash, checked((ulong)value.InitialRandomItemDefinitionCount));
        Add(ref hash, checked((ulong)value.InitialItemInstanceCount));
        Add(ref hash, checked((ulong)value.InitialBookContentCount));
        Add(ref hash, checked((ulong)value.InitialBookEditionCount));
        Add(ref hash, checked((ulong)value.InitialPhysicalBookCount));
        Add(ref hash, checked((ulong)value.MonsterSpeciesPerBiome));
        Add(ref hash, checked((ulong)value.InitialMonsterPopulationPerHabitat));
        Add(ref hash, value.EnableOngoingContentGeneration ? 1UL : 0UL);
        Add(ref hash, checked((ulong)value.ContentGenerationIntervalYears));
        Add(ref hash, checked((ulong)value.ItemDefinitionsPerInterval));
        Add(ref hash, checked((ulong)value.BookContentsPerInterval));
    }

    private static void Add(ref ulong hash, ulong value)
    {
        unchecked
        {
            for (var index = 0; index < 8; index++)
            {
                hash ^= (byte)value;
                hash *= 1099511628211UL;
                value >>= 8;
            }
        }
    }

    private static readonly string[] ItemDefinitionRefs =
    [
        "item.artifact.offense",
        "item.artifact.defense",
        "item.pill.growth",
        "item.pill.breakthrough",
    ];
}

public readonly record struct GeneratedWorldExpansion(
    GeneratedItemDefinitionCheckpoint[] Items,
    GeneratedBookDefinitionCheckpoint[] Books)
{
    public static GeneratedWorldExpansion Empty { get; } = new(
        Array.Empty<GeneratedItemDefinitionCheckpoint>(),
        Array.Empty<GeneratedBookDefinitionCheckpoint>());

    public bool IsEmpty => Items.Length == 0 && Books.Length == 0;
}

public sealed class WorldMapCatalog
{
    private readonly MapLocationDefinition[] _locations;
    private readonly MonsterSpeciesDefinition[] _monsters;

    internal WorldMapCatalog(
        ulong worldSeed,
        WorldGenerationConfiguration configuration,
        IReadOnlyList<GeneratedMonsterDefinitionCheckpoint> monsters)
    {
        SpeciesPerWildBiome = configuration.MonsterSpeciesPerBiome;
        _locations = CreateLocations(worldSeed, configuration);
        _monsters = monsters.Select(value => new MonsterSpeciesDefinition(
            value.SpeciesId,
            GeneratedWorldNameFormatter.FormatMonster(value.Name),
            value.Biome,
            value.BaseDanger,
            value.DropMaterial,
            value.Trait)).ToArray();
    }

    public int SpeciesPerWildBiome { get; }
    public IReadOnlyList<MapLocationDefinition> Locations => _locations;
    public IReadOnlyList<MonsterSpeciesDefinition> Monsters => _monsters;

    public MapLocationDefinition GetLocation(ushort id)
    {
        if (id >= _locations.Length || _locations[id].Id != id)
        {
            throw new ArgumentOutOfRangeException(nameof(id));
        }
        return _locations[id];
    }

    public MonsterSpeciesDefinition GetMonster(BiomeType biome, int selection)
    {
        if (biome == BiomeType.City)
        {
            throw new ArgumentOutOfRangeException(nameof(biome));
        }
        var offset = checked(((int)biome - 1) * SpeciesPerWildBiome);
        return _monsters[offset + Math.Abs(selection % SpeciesPerWildBiome)];
    }

    public int GetMonsterPopulationOffset(ushort locationId, int localMonsterIndex)
    {
        if (locationId >= _locations.Length
            || _locations[locationId].Id != locationId
            || _locations[locationId].Biome == BiomeType.City
            || (uint)localMonsterIndex >= SpeciesPerWildBiome)
        {
            throw new ArgumentOutOfRangeException(nameof(localMonsterIndex));
        }
        return checked((locationId * SpeciesPerWildBiome) + localMonsterIndex);
    }

    public int GetTravelMonths(ushort sourceId, ushort destinationId)
    {
        var source = GetLocation(sourceId);
        var destination = GetLocation(destinationId);
        if (!WorldMapRules.AreDirectlyConnected(sourceId, destinationId))
        {
            throw new InvalidOperationException("이동 경로는 Atlas에서 직접 연결된 두 지점이어야 합니다.");
        }
        if (source.HeavenlyLayer != destination.HeavenlyLayer)
        {
            return 12;
        }
        var distance = Math.Abs(source.X - destination.X) + Math.Abs(source.Y - destination.Y);
        return Math.Max(1, (distance * 2) + 1);
    }

    private static MapLocationDefinition[] CreateLocations(
        ulong worldSeed,
        WorldGenerationConfiguration configuration)
    {
        if (!configuration.RandomizeMap)
        {
            return WorldMapRules.AllLocations.ToArray();
        }

        var result = new MapLocationDefinition[WorldMapRules.LocationCount];
        var seed = WorldGenerationRules.DomainSeed(
            worldSeed,
            WorldGenerationDomain.Map,
            configuration.RandomizeMap);
        foreach (var baseline in WorldMapRules.AllLocations)
        {
            if (baseline.Biome == BiomeType.City)
            {
                result[baseline.Id] = baseline with
                {
                    KoreanName = baseline.HeavenlyLayer == 1
                        ? FormatLocationName(seed, baseline.Id, "성")
                        : FormatLocationName(seed, baseline.Id, "선성"),
                };
                continue;
            }
            var value = WorldGenerationRules.Value(seed, baseline.Id);
            var biome = (BiomeType)(1 + WorldGenerationRules.Select(value, 3));
            var suffix = biome switch
            {
                BiomeType.Forest => "림",
                BiomeType.Plains => "원",
                BiomeType.Mountains => "산",
                _ => throw new InvalidDataException("생성 지형이 잘못되었습니다."),
            };
            result[baseline.Id] = baseline with
            {
                KoreanName = FormatLocationName(seed, baseline.Id, suffix),
                Biome = biome,
            };
        }
        return result;
    }

    private static string FormatLocationName(ulong seed, ushort id, string suffix)
    {
        var value = WorldGenerationRules.Value(seed, id, 0x4C4F_4341_5449_4F4EUL);
        string[] prefixes = ["청", "적", "현", "백", "자", "유", "금", "은", "벽", "천", "운", "성"];
        string[] cores = ["명", "하", "운", "천", "광", "령", "진", "월", "해", "풍", "뢰", "화"];
        return string.Concat(
            prefixes[WorldGenerationRules.Select(value, prefixes.Length)],
            cores[WorldGenerationRules.Select(value >> 17, cores.Length)],
            suffix);
    }
}
