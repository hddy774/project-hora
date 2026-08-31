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
    int Y);

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
    string Name,
    RealmStage Realm,
    int RealmLayer,
    AgentActivity Activity,
    long CombatPower);

public sealed record MapLocationSnapshot(
    ushort Id,
    string Name,
    BiomeType Biome,
    int X,
    int Y,
    int Population,
    IReadOnlyList<MonsterHabitatSnapshot> Monsters,
    IReadOnlyList<MapAgentSummary> Agents);

public sealed record WorldMapSnapshot(
    IReadOnlyList<MapLocationSnapshot> Locations,
    int TotalMonsterHunts,
    int TotalMonsterVictories,
    ushort? DetailedLocationId = null)
{
    public static WorldMapSnapshot Empty { get; } = new(
        Array.Empty<MapLocationSnapshot>(),
        0,
        0);
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
    public const int CurrentRevision = 1;
    public const ushort CityLocationId = 0;
    public const int LocationCount = 9;
    public const int MonstersPerWildBiome = 3;
    public const int InitialMonsterPopulation = 300;
    public const int MaximumMonsterPopulation = 500;
    public const int MonthlyMonsterRegeneration = 3;
    public const int DetailedAgentLimit = 500;

    private static readonly MapLocationDefinition[] Locations =
    [
        new(CityLocationId, "만상성", BiomeType.City, 1, 1),
        new(1, "북청림", BiomeType.Forest, 0, 0),
        new(2, "천운림", BiomeType.Forest, 1, 0),
        new(3, "동고림", BiomeType.Forest, 2, 0),
        new(4, "백로평원", BiomeType.Plains, 0, 1),
        new(5, "청풍평원", BiomeType.Plains, 2, 1),
        new(6, "적운산맥", BiomeType.Mountains, 0, 2),
        new(7, "천벽산맥", BiomeType.Mountains, 1, 2),
        new(8, "뇌명산맥", BiomeType.Mountains, 2, 2),
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
        var distance = Math.Abs(source.X - destination.X) + Math.Abs(source.Y - destination.Y);
        return Math.Max(1, (distance * 2) + 1);
    }
}
