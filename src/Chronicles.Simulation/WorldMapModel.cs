using Chronicles.Core;

namespace Chronicles.Simulation;

public enum BiomeType : byte
{
    City,
    Forest,
    Plains,
    Mountains,
}

public readonly record struct MapLocationDefinition(
    ushort Id,
    string KoreanName,
    BiomeType Biome,
    int X,
    int Y,
    byte HeavenlyLayer,
    AtlasNodeId AtlasNodeId);

public readonly record struct MonsterSpeciesDefinition(
    ushort Id,
    string KoreanName,
    BiomeType Biome,
    int BaseDanger,
    MaterialId DropMaterial);

public readonly record struct MonsterHabitatSnapshot(
    MonsterSpeciesDefinition Species,
    int Population);

public readonly record struct MapAgentSummary(
    AgentId AgentId,
    int CombatRank,
    string Name,
    bool HasEnteredCultivation,
    RealmStage Realm,
    int RealmLayer,
    AgentActivity Activity,
    long CombatPower);

public readonly record struct MapRegionActivitySnapshot(
    int Cultivation,
    int Combat,
    int Economy,
    int Traveling,
    int Recovering,
    int Reincarnating)
{
    public int Total => checked(
        Cultivation + Combat + Economy + Traveling + Recovering + Reincarnating);
}

public sealed record MapLocationSnapshot(
    ushort Id,
    string Name,
    BiomeType Biome,
    int X,
    int Y,
    int Population,
    IReadOnlyList<MonsterHabitatSnapshot> Monsters,
    long AverageCombatPower,
    long MaximumCombatPower,
    MapAgentSummary? StrongestAgent,
    bool HasCultivator,
    RealmStage HighestRealm,
    int HighestRealmLayer,
    AtlasLocationContextSnapshot Atlas,
    MapRegionActivitySnapshot Activities,
    int AgentPageOffset,
    IReadOnlyList<MapAgentSummary> Agents);

public sealed record WorldMapSnapshot(
    IReadOnlyList<MapLocationSnapshot> Locations,
    int TotalMonsterHunts,
    int TotalMonsterVictories,
    ushort? DetailedLocationId = null,
    int DetailedAgentOffset = 0,
    WorldAtlasSnapshot? Atlas = null)
{
    public static WorldMapSnapshot Empty { get; } = new(
        Array.Empty<MapLocationSnapshot>(),
        0,
        0,
        Atlas: WorldAtlasSnapshot.Empty);
}

public readonly record struct MapAgentCheckpoint(
    ushort LocationId,
    ushort TravelDestinationId,
    int MonsterHunts,
    int MonsterVictories);

public readonly record struct WorldMapCheckpoint(
    int TotalMonsterHunts,
    int TotalMonsterVictories,
    int[]? MonsterPopulations = null,
    long LastRegenerationMonth = 0);

public static class WorldMapRules
{
    public const int CurrentRevision = 2;
    public const ushort CityLocationId = 0;
    public const ushort SecondHeavenCityLocationId = 9;
    public const int LocationCount = WorldAtlasRules.ProductionLocationCount;
    public const int MonstersPerWildBiome = 3;
    public const int InitialMonsterPopulation = 300;
    public const int MaximumMonsterPopulation = 500;
    public const int MonthlyMonsterRegeneration = 3;
    public const int DetailedAgentPageSize = 100;

    private static readonly MapLocationDefinition[] Locations =
    [
        new(CityLocationId, "만상성", BiomeType.City, 1, 1, 1, new AtlasNodeId(1_000)),
        new(1, "북청림", BiomeType.Forest, 0, 0, 1, new AtlasNodeId(1_001)),
        new(2, "천운림", BiomeType.Forest, 1, 0, 1, new AtlasNodeId(1_002)),
        new(3, "동고림", BiomeType.Forest, 2, 0, 1, new AtlasNodeId(1_003)),
        new(4, "백로평원", BiomeType.Plains, 0, 1, 1, new AtlasNodeId(1_004)),
        new(5, "청풍평원", BiomeType.Plains, 2, 1, 1, new AtlasNodeId(1_005)),
        new(6, "적운산맥", BiomeType.Mountains, 0, 2, 1, new AtlasNodeId(1_006)),
        new(7, "천벽산맥", BiomeType.Mountains, 1, 2, 1, new AtlasNodeId(1_007)),
        new(8, "뇌명산맥", BiomeType.Mountains, 2, 2, 1, new AtlasNodeId(1_008)),
        new(SecondHeavenCityLocationId, "청명성", BiomeType.City, 1, 1, 2, new AtlasNodeId(1_009)),
        new(10, "유광림", BiomeType.Forest, 0, 0, 2, new AtlasNodeId(1_010)),
        new(11, "성진림", BiomeType.Forest, 1, 0, 2, new AtlasNodeId(1_011)),
        new(12, "자하림", BiomeType.Forest, 2, 0, 2, new AtlasNodeId(1_012)),
        new(13, "은하평원", BiomeType.Plains, 0, 1, 2, new AtlasNodeId(1_013)),
        new(14, "운해평원", BiomeType.Plains, 2, 1, 2, new AtlasNodeId(1_014)),
        new(15, "태허산맥", BiomeType.Mountains, 0, 2, 2, new AtlasNodeId(1_015)),
        new(16, "천문산맥", BiomeType.Mountains, 1, 2, 2, new AtlasNodeId(1_016)),
        new(17, "성락산맥", BiomeType.Mountains, 2, 2, 2, new AtlasNodeId(1_017)),
    ];

    private static readonly MonsterSpeciesDefinition[] Monsters =
    [
        new(0, "청목요랑", BiomeType.Forest, 900, MaterialId.PillMaterial),
        new(1, "독안개사", BiomeType.Forest, 1_200, MaterialId.PillMaterial),
        new(2, "철피웅", BiomeType.Forest, 1_600, MaterialId.TreasureMaterial),
        new(3, "질풍영록", BiomeType.Plains, 800, MaterialId.PillMaterial),
        new(4, "적각우", BiomeType.Plains, 1_100, MaterialId.TreasureMaterial),
        new(5, "황사호", BiomeType.Plains, 1_500, MaterialId.TreasureMaterial),
        new(6, "철갑암원", BiomeType.Mountains, 1_300, MaterialId.TreasureMaterial),
        new(7, "뇌익조", BiomeType.Mountains, 1_800, MaterialId.TreasureMaterial),
        new(8, "화린망", BiomeType.Mountains, 2_400, MaterialId.PillMaterial),
    ];

    // 이동 인접성은 불변 정의다. 행동을 고를 때마다 전체 지도를 다시 순회하고 List를
    // 만들지 않도록 시작 시 한 번만 계산한다.
    private static readonly ushort[][] ConnectedLocations = BuildConnectedLocations();
    private static readonly ushort[][] FirstLayerAccessibleLocations =
        BuildFirstLayerAccessibleLocations();

    public static IReadOnlyList<MapLocationDefinition> AllLocations => Locations;
    public static IReadOnlyList<MonsterSpeciesDefinition> AllMonsters => Monsters;

    public static MapLocationDefinition GetLocation(ushort id)
    {
        if (id >= Locations.Length || Locations[id].Id != id)
        {
            throw new ArgumentOutOfRangeException(nameof(id));
        }

        return Locations[id];
    }

    public static bool IsCity(ushort locationId) => GetLocation(locationId).Biome == BiomeType.City;

    public static bool AreDirectlyConnected(ushort sourceId, ushort destinationId)
    {
        var source = GetLocation(sourceId);
        var destination = GetLocation(destinationId);
        if ((sourceId == CityLocationId && destinationId == SecondHeavenCityLocationId)
            || (sourceId == SecondHeavenCityLocationId && destinationId == CityLocationId))
        {
            return true;
        }

        return source.HeavenlyLayer == destination.HeavenlyLayer
            && Math.Abs(source.X - destination.X) + Math.Abs(source.Y - destination.Y) == 1;
    }

    public static IReadOnlyList<ushort> GetConnectedLocations(ushort locationId)
    {
        _ = GetLocation(locationId);
        return ConnectedLocations[locationId];
    }

    public static IReadOnlyList<ushort> GetAccessibleConnectedLocations(
        ushort locationId,
        RealmStage realm)
    {
        _ = GetLocation(locationId);
        return WorldAtlasRules.CanEnterLayer(realm, 2)
            ? ConnectedLocations[locationId]
            : FirstLayerAccessibleLocations[locationId];
    }

    private static ushort[][] BuildConnectedLocations()
    {
        var result = new ushort[LocationCount][];
        for (ushort locationId = 0; locationId < LocationCount; locationId++)
        {
            var connected = new List<ushort>(5);
            for (ushort candidate = 0; candidate < LocationCount; candidate++)
            {
                if (candidate != locationId && AreDirectlyConnected(locationId, candidate))
                {
                    connected.Add(candidate);
                }
            }
            result[locationId] = connected.ToArray();
        }
        return result;
    }

    private static ushort[][] BuildFirstLayerAccessibleLocations()
    {
        var result = new ushort[LocationCount][];
        for (ushort locationId = 0; locationId < LocationCount; locationId++)
        {
            result[locationId] = ConnectedLocations[locationId]
                .Where(candidate => Locations[candidate].HeavenlyLayer == 1)
                .ToArray();
        }
        return result;
    }

    public static MonsterSpeciesDefinition GetMonster(BiomeType biome, int selection)
    {
        var offset = biome switch
        {
            BiomeType.Forest => 0,
            BiomeType.Plains => MonstersPerWildBiome,
            BiomeType.Mountains => MonstersPerWildBiome * 2,
            _ => throw new ArgumentOutOfRangeException(nameof(biome), "도시에는 야생 요수가 없습니다."),
        };
        return Monsters[offset + Math.Abs(selection % MonstersPerWildBiome)];
    }

    public static IReadOnlyList<MonsterSpeciesDefinition> GetMonsters(BiomeType biome)
    {
        if (biome == BiomeType.City)
        {
            return Array.Empty<MonsterSpeciesDefinition>();
        }

        var first = biome switch
        {
            BiomeType.Forest => 0,
            BiomeType.Plains => MonstersPerWildBiome,
            BiomeType.Mountains => MonstersPerWildBiome * 2,
            _ => throw new ArgumentOutOfRangeException(nameof(biome)),
        };
        return Monsters.AsSpan(first, MonstersPerWildBiome).ToArray();
    }

    public static int GetMonsterPopulationOffset(ushort locationId, int localMonsterIndex)
    {
        var location = GetLocation(locationId);
        if (location.Biome == BiomeType.City
            || (uint)localMonsterIndex >= MonstersPerWildBiome)
        {
            throw new ArgumentOutOfRangeException(nameof(localMonsterIndex));
        }

        return checked((locationId * MonstersPerWildBiome) + localMonsterIndex);
    }

    public static int GetTravelMonths(ushort sourceId, ushort destinationId)
    {
        var source = GetLocation(sourceId);
        var destination = GetLocation(destinationId);
        if (!AreDirectlyConnected(sourceId, destinationId))
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
}
