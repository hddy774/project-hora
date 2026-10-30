using Chronicles.Core;

namespace Chronicles.Simulation;

public enum GeographyImplementationState : byte
{
    Production,
    Planned,
}

public enum NaturalTerrainType : byte
{
    Plains,
    Forest,
    Mountains,
    River,
}

public enum PointSocietyKind : byte
{
    None,
    Mortal,
    Cultivator,
}

public enum PointStrongholdKind : byte
{
    None,
    City,
    Sect,
    Clan,
}

public enum MineResourceKind : byte
{
    None,
    SpiritStone,
    TreasureMaterial,
    Mixed,
}

public readonly record struct ContinentTileDefinition(
    AtlasNodeId Id,
    string KoreanName,
    int WidthInProvinceTiles,
    int HeightInProvinceTiles,
    int ProvinceTileDistanceLi,
    GeographyImplementationState ImplementationState);

public readonly record struct ProvinceTileDefinition(
    ushort Id,
    AtlasNodeId NodeId,
    AtlasNodeId ContinentId,
    string KoreanName,
    int X,
    int Y,
    int WidthInPointTiles,
    int HeightInPointTiles,
    bool HasPointDetail,
    GeographyImplementationState ImplementationState);

public readonly record struct PointSettlementProfile(
    PointSocietyKind Society,
    PointStrongholdKind Stronghold,
    byte LargeCityCount,
    byte SmallCityCount,
    byte VillageCount,
    byte SectCount,
    byte ClanCount,
    string PolityName)
{
    public bool HasSettlement =>
        LargeCityCount + SmallCityCount + VillageCount + SectCount + ClanCount > 0;

    public bool HasMajorStronghold => Stronghold != PointStrongholdKind.None;
}

public readonly record struct PointResourceProfile(
    bool HasSpiritVein,
    MineResourceKind Mine,
    int EstimatedBeastPopulation,
    byte LivingSpeciesDiversity,
    int OrthogonalTravelDistanceLi);

public readonly record struct PointHeavenViewDefinition(
    ushort PhysicalPointId,
    AtlasNodeId NodeId,
    ushort ProvinceId,
    byte HeavenlyLayer,
    int X,
    int Y,
    NaturalTerrainType Terrain,
    string KoreanName,
    string Description,
    PointSettlementProfile Settlements,
    PointResourceProfile Resources);

public sealed record WorldGeographyBlueprint(
    int Revision,
    ContinentTileDefinition Continent,
    IReadOnlyList<ProvinceTileDefinition> Provinces,
    ushort ActiveProvinceId,
    IReadOnlyList<PointHeavenViewDefinition> FirstHeavenPoints,
    IReadOnlyList<PointHeavenViewDefinition> SecondHeavenPoints)
{
    public static WorldGeographyBlueprint Empty { get; } = new(
        0,
        default,
        Array.Empty<ProvinceTileDefinition>(),
        0,
        Array.Empty<PointHeavenViewDefinition>(),
        Array.Empty<PointHeavenViewDefinition>());
}

/// <summary>
/// Immutable PC geography baseline. The physical province/point coordinates are shared,
/// while each heavenly layer owns a distinct view node and settlement/resource profile.
/// Mutable control, ownership and depletion remain separate authority concerns.
/// </summary>
public static class WorldGeographyRules
{
    public const int CurrentRevision = 1;
    public const int ProvinceGridWidth = 10;
    public const int ProvinceGridHeight = 10;
    public const int ProvinceCount = ProvinceGridWidth * ProvinceGridHeight;
    public const int PointGridWidth = 10;
    public const int PointGridHeight = 10;
    public const int PointsPerProvince = PointGridWidth * PointGridHeight;
    public const ushort OcheonProvinceId = 44;
    public const int ProductionRegionGridWidth = 3;
    public const int ProductionRegionGridHeight = 3;
    public const int ProductionRegionOriginX =
        (PointGridWidth - ProductionRegionGridWidth) / 2;
    public const int ProductionRegionOriginY =
        (PointGridHeight - ProductionRegionGridHeight) / 2;

    // PC_TEMP_REVIEW GEO-020-01: 축척과 정착지 밀도는 다음 버전 지리 균형 검토 대상이다.
    public const int ProvinceTileDistanceLi = 12_000;
    public const int BasePointDistanceLi = 240;

    private static readonly ContinentTileDefinition Continent = new(
        new AtlasNodeId(100_000),
        "하천대륙",
        ProvinceGridWidth,
        ProvinceGridHeight,
        ProvinceTileDistanceLi,
        GeographyImplementationState.Production);
    private static readonly ProvinceTileDefinition[] Provinces = BuildProvinces();
    private static readonly PointHeavenViewDefinition[] FirstHeavenPoints =
        BuildPoints(1, worldSeed: 0, randomized: false);
    private static readonly PointHeavenViewDefinition[] SecondHeavenPoints =
        BuildPoints(2, worldSeed: 0, randomized: false);

    public static WorldGeographyBlueprint Blueprint { get; } = new(
        CurrentRevision,
        Continent,
        Provinces,
        OcheonProvinceId,
        FirstHeavenPoints,
        SecondHeavenPoints);

    public static WorldGeographyBlueprint CreateBlueprint(
        ulong worldSeed,
        WorldGenerationConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        WorldGenerationRules.Validate(configuration);
        if (!configuration.RandomizeMap)
        {
            return Blueprint;
        }
        return new WorldGeographyBlueprint(
            CurrentRevision,
            Continent,
            Provinces,
            OcheonProvinceId,
            BuildPoints(1, worldSeed, randomized: true),
            BuildPoints(2, worldSeed, randomized: true));
    }

    public static ProvinceTileDefinition GetProvince(ushort provinceId)
    {
        if (provinceId >= Provinces.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(provinceId));
        }

        return Provinces[provinceId];
    }

    public static PointHeavenViewDefinition GetPoint(
        byte heavenlyLayer,
        ushort physicalPointId)
    {
        if (physicalPointId >= PointsPerProvince)
        {
            throw new ArgumentOutOfRangeException(nameof(physicalPointId));
        }

        return heavenlyLayer switch
        {
            1 => FirstHeavenPoints[physicalPointId],
            2 => SecondHeavenPoints[physicalPointId],
            _ => throw new ArgumentOutOfRangeException(nameof(heavenlyLayer)),
        };
    }

    public static IReadOnlyList<PointHeavenViewDefinition> GetPoints(byte heavenlyLayer) =>
        heavenlyLayer switch
        {
            1 => FirstHeavenPoints,
            2 => SecondHeavenPoints,
            _ => Array.Empty<PointHeavenViewDefinition>(),
        };

    /// <summary>
    /// Resolves the centered 3×3 Production activity region embedded in Ocheon Province.
    /// The returned coordinates are the existing activity-region coordinates, not a new
    /// geography authority. Points outside that compatibility region remain unconnected.
    /// </summary>
    public static bool TryGetProductionRegionCoordinates(
        ushort physicalPointId,
        out int regionX,
        out int regionY)
    {
        if (physicalPointId >= PointsPerProvince)
        {
            regionX = 0;
            regionY = 0;
            return false;
        }

        var pointX = physicalPointId % PointGridWidth;
        var pointY = physicalPointId / PointGridWidth;
        regionX = pointX - ProductionRegionOriginX;
        regionY = pointY - ProductionRegionOriginY;
        return regionX is >= 0 and < ProductionRegionGridWidth
            && regionY is >= 0 and < ProductionRegionGridHeight;
    }

    public static ushort GetProductionPointId(int regionX, int regionY)
    {
        if (regionX is < 0 or >= ProductionRegionGridWidth)
        {
            throw new ArgumentOutOfRangeException(nameof(regionX));
        }
        if (regionY is < 0 or >= ProductionRegionGridHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(regionY));
        }

        return checked((ushort)(
            ((regionY + ProductionRegionOriginY) * PointGridWidth)
            + regionX
            + ProductionRegionOriginX));
    }

    public static bool AreAdjacentIncludingDiagonal(
        PointHeavenViewDefinition left,
        PointHeavenViewDefinition right) =>
        left.HeavenlyLayer == right.HeavenlyLayer
        && left.PhysicalPointId != right.PhysicalPointId
        && Math.Abs(left.X - right.X) <= 1
        && Math.Abs(left.Y - right.Y) <= 1;

    private static ProvinceTileDefinition[] BuildProvinces()
    {
        var result = new ProvinceTileDefinition[ProvinceCount];
        for (ushort id = 0; id < result.Length; id++)
        {
            var x = id % ProvinceGridWidth;
            var y = id / ProvinceGridWidth;
            var active = id == OcheonProvinceId;
            result[id] = new ProvinceTileDefinition(
                id,
                new AtlasNodeId(101_000 + id),
                Continent.Id,
                active ? "오천주" : $"하천 {y + 1}-{x + 1}주",
                x,
                y,
                PointGridWidth,
                PointGridHeight,
                active,
                active
                    ? GeographyImplementationState.Production
                    : GeographyImplementationState.Planned);
        }

        return result;
    }

    private static PointHeavenViewDefinition[] BuildPoints(
        byte layer,
        ulong worldSeed,
        bool randomized)
    {
        var result = new PointHeavenViewDefinition[PointsPerProvince];
        var mapSeed = WorldGenerationRules.DomainSeed(
            worldSeed,
            WorldGenerationDomain.Map,
            randomized);
        for (ushort pointId = 0; pointId < result.Length; pointId++)
        {
            var x = pointId % PointGridWidth;
            var y = pointId / PointGridWidth;
            var terrain = GetTerrain(x, y, layer, mapSeed, randomized);
            var settlements = GetSettlements(x, y, layer, mapSeed, randomized);
            var resources = GetResources(
                pointId,
                x,
                y,
                layer,
                terrain,
                settlements,
                mapSeed,
                randomized);
            var layerName = layer == 1 ? "제1층천" : "제2층천";
            result[pointId] = new PointHeavenViewDefinition(
                pointId,
                new AtlasNodeId(200_000 + (layer * 10_000) + pointId),
                OcheonProvinceId,
                layer,
                x,
                y,
                terrain,
                randomized
                    ? CreatePointName(mapSeed, pointId, layer, terrain)
                    : $"오천 {x + 1}-{y + 1} 지점",
                CreateDescription(layerName, terrain, settlements, resources),
                settlements,
                resources);
        }

        return result;
    }

    private static NaturalTerrainType GetTerrain(
        int x,
        int y,
        byte layer,
        ulong mapSeed,
        bool randomized)
    {
        var riverColumn = randomized
            ? 2 + WorldGenerationRules.Select(
                WorldGenerationRules.Value(mapSeed, layer, 0x5249_5645_52UL),
                5)
            : 3;
        if (x >= riverColumn && x <= riverColumn + 1 && y is >= 1 and <= 8)
        {
            return NaturalTerrainType.River;
        }

        var selector = randomized
            ? WorldGenerationRules.Select(
                WorldGenerationRules.Value(
                    mapSeed,
                    checked((layer * PointsPerProvince) + (y * PointGridWidth) + x),
                    0x5445_5252_4149_4EUL),
                5)
            : ((x * 17) + (y * 31) + (layer * 7)) % 5;
        return selector switch
        {
            0 => NaturalTerrainType.Mountains,
            1 or 2 => NaturalTerrainType.Forest,
            _ => NaturalTerrainType.Plains,
        };
    }

    private static PointSettlementProfile GetSettlements(
        int x,
        int y,
        byte layer,
        ulong mapSeed,
        bool randomized)
    {
        var originSelector = randomized
            ? WorldGenerationRules.Value(
                mapSeed,
                layer,
                0x5345_5454_4C45UL)
            : 0UL;
        if (layer == 1)
        {
            var origin = randomized
                ? 1 + WorldGenerationRules.Select(originSelector, 2)
                : 1;
            if (IsAnchor(x, y, spacing: 3, origin))
            {
                var capital = x == 4 && y == 4;
                return new PointSettlementProfile(
                    PointSocietyKind.Mortal,
                    PointStrongholdKind.City,
                    checked((byte)(capital ? 1 : 0)),
                    checked((byte)(capital ? 2 : 1)),
                    checked((byte)(2 + ((x + y) % 3))),
                    0,
                    0,
                    "오천국");
            }

            var villageOffset = randomized
                ? WorldGenerationRules.Select(originSelector >> 11, 7)
                : 0;
            if ((x + (y * 2) + villageOffset) % 7 == 0)
            {
                return new PointSettlementProfile(
                    PointSocietyKind.Mortal,
                    PointStrongholdKind.None,
                    0,
                    0,
                    checked((byte)(1 + ((x + y) % 4))),
                    0,
                    0,
                    "오천국");
            }

            return EmptySettlement;
        }

        var cultivatorOrigin = randomized
            ? 1 + WorldGenerationRules.Select(originSelector, 3)
            : 2;
        if (IsAnchor(x, y, spacing: 4, cultivatorOrigin))
        {
            var selector = ((x / 4) + (y / 4)) % 3;
            return selector switch
            {
                0 => new PointSettlementProfile(
                    PointSocietyKind.Cultivator,
                    PointStrongholdKind.City,
                    0,
                    1,
                    0,
                    0,
                    0,
                    "오천선국"),
                1 => new PointSettlementProfile(
                    PointSocietyKind.Cultivator,
                    PointStrongholdKind.Sect,
                    0,
                    0,
                    0,
                    1,
                    0,
                    string.Empty),
                _ => new PointSettlementProfile(
                    PointSocietyKind.Cultivator,
                    PointStrongholdKind.Clan,
                    0,
                    0,
                    0,
                    0,
                    1,
                    string.Empty),
            };
        }

        return EmptySettlement;
    }

    private static PointResourceProfile GetResources(
        ushort pointId,
        int x,
        int y,
        byte layer,
        NaturalTerrainType terrain,
        PointSettlementProfile settlements,
        ulong mapSeed,
        bool randomized)
    {
        var resourceValue = randomized
            ? WorldGenerationRules.Value(
                mapSeed,
                checked((layer * PointsPerProvince) + pointId),
                0x5245_534F_5552_4345UL)
            : checked((ulong)(pointId + layer));
        var hasVein = resourceValue % (ulong)(layer == 1 ? 6 : 4) == 0
            || terrain == NaturalTerrainType.Mountains;
        var mineSelector = randomized
            ? WorldGenerationRules.Select(resourceValue >> 13, 11)
            : (pointId + (layer * 3)) % 11;
        var mine = mineSelector switch
        {
            0 => MineResourceKind.Mixed,
            1 or 2 => MineResourceKind.SpiritStone,
            3 => MineResourceKind.TreasureMaterial,
            _ => MineResourceKind.None,
        };
        var baseBeasts = terrain switch
        {
            NaturalTerrainType.Forest => 180,
            NaturalTerrainType.Mountains => 130,
            NaturalTerrainType.River => 90,
            _ => 70,
        };
        // 도시가 있어도 주변 자연에 영수가 남는다. 정착지는 개체 수만 줄인다.
        var beastVariation = randomized
            ? WorldGenerationRules.Select(resourceValue >> 29, 41)
            : (x * 13 + y * 7 + layer) % 41;
        var beasts = Math.Max(12, baseBeasts - (settlements.HasSettlement ? 50 : 0)
            + beastVariation);
        var distance = BasePointDistanceLi + (terrain switch
        {
            NaturalTerrainType.Mountains => 160,
            NaturalTerrainType.Forest => 80,
            NaturalTerrainType.River => 40,
            _ => 0,
        });
        return new PointResourceProfile(
            hasVein,
            mine,
            beasts,
            checked((byte)(3 + (randomized
                ? WorldGenerationRules.Select(resourceValue >> 43, 8)
                : ((x + y + layer) % 8)))),
            distance);
    }

    private static string CreatePointName(
        ulong mapSeed,
        ushort pointId,
        byte layer,
        NaturalTerrainType terrain)
    {
        var value = WorldGenerationRules.Value(
            mapSeed,
            checked((layer * PointsPerProvince) + pointId),
            0x504F_494E_545FUL);
        string[] prefixes = ["청", "적", "현", "백", "자", "유", "금", "은", "벽", "천", "운", "성"];
        string[] cores = ["명", "하", "운", "천", "광", "령", "진", "월", "해", "풍", "뢰", "화"];
        var suffix = terrain switch
        {
            NaturalTerrainType.Plains => "평",
            NaturalTerrainType.Forest => "림",
            NaturalTerrainType.Mountains => "산",
            NaturalTerrainType.River => "천",
            _ => "지",
        };
        return string.Concat(
            prefixes[WorldGenerationRules.Select(value, prefixes.Length)],
            cores[WorldGenerationRules.Select(value >> 17, cores.Length)],
            suffix,
            " 지점");
    }

    private static string CreateDescription(
        string layerName,
        NaturalTerrainType terrain,
        PointSettlementProfile settlements,
        PointResourceProfile resources)
    {
        var terrainText = terrain switch
        {
            NaturalTerrainType.Plains => "넓은 평야",
            NaturalTerrainType.Forest => "깊은 숲",
            NaturalTerrainType.Mountains => "험준한 산맥",
            NaturalTerrainType.River => "큰 하천과 범람원",
            _ => throw new ArgumentOutOfRangeException(nameof(terrain)),
        };
        var society = settlements.Society switch
        {
            PointSocietyKind.Mortal => "범인의 취락과 국가 질서",
            PointSocietyKind.Cultivator => "수선자의 거점과 선가 질서",
            _ => "정착지 없이 생령만 머무는 자연",
        };
        var resource = resources.HasSpiritVein
            ? "자연 영맥이 영기를 배출한다."
            : "확인된 영맥은 없다.";
        return $"{layerName}의 {terrainText}. {society}. {resource}";
    }

    private static bool IsAnchor(int x, int y, int spacing, int origin) =>
        x >= origin
        && y >= origin
        && (x - origin) % spacing == 0
        && (y - origin) % spacing == 0;

    private static PointSettlementProfile EmptySettlement => new(
        PointSocietyKind.None,
        PointStrongholdKind.None,
        0,
        0,
        0,
        0,
        0,
        string.Empty);
}
