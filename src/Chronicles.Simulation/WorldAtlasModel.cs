using Chronicles.Core;

namespace Chronicles.Simulation;

public enum AtlasNodeKind : byte
{
    HeavenlyLayer,
    Continent,
    GrandRegion,
    Area,
    Location,
}

public enum AtlasImplementationState : byte
{
    Production,
    Planned,
}

public readonly record struct AtlasNodeDefinition(
    AtlasNodeId Id,
    AtlasNodeId? ParentId,
    AtlasNodeKind Kind,
    string KoreanName,
    byte HeavenlyLayer,
    AtlasImplementationState ImplementationState,
    int Revision);

public readonly record struct HeavenlyLayerDefinition(
    byte Number,
    string KoreanName,
    AtlasNodeId NodeId,
    AtlasImplementationState ImplementationState);

public readonly record struct SpiritVeinProfileDefinition(
    byte Id,
    string KoreanName,
    ushort QualityHundredths,
    long MonthlyYield,
    long InitialSupply);

public readonly record struct SpiritVeinSourceDefinition(
    SpiritVeinSourceId Id,
    AtlasNodeId LocationNodeId,
    ushort LocationId,
    byte ProfileId);

public enum MovementStatus : byte
{
    Idle,
    Planned,
    InTransit,
    Arrived,
    Blocked,
    Rerouted,
    Interrupted,
}

public enum MovementResolutionCode : byte
{
    None,
    Planned,
    Arrived,
    RouteRevisionChanged,
    DestinationUnavailable,
    AccessDenied,
    InformationStale,
    TravelerDied,
    Rerouted,
}

public readonly record struct MovementRouteState(
    ushort OriginLocationId,
    ushort DestinationLocationId,
    int PlannedAtlasRevision,
    long StartedTick,
    long DueTick,
    MovementStatus Status,
    MovementResolutionCode Resolution)
{
    public static MovementRouteState Idle(ushort locationId) => new(
        locationId,
        locationId,
        WorldAtlasRules.CurrentRevision,
        0,
        0,
        MovementStatus.Idle,
        MovementResolutionCode.None);
}

public readonly record struct SpatialKnowledgeSnapshot(
    int KnownLocationCount,
    int TotalProductionLocationCount,
    ushort? LastDiscoveredLocationId,
    long LastDiscoveryMonth,
    bool IsCurrentLocationKnown);

public readonly record struct AtlasLocationContextSnapshot(
    byte HeavenlyLayer,
    string HeavenlyLayerName,
    string ContinentName,
    string GrandRegionName,
    string AreaName,
    AtlasNodeId LocationNodeId,
    int SpiritVeinSourceCount,
    long RemainingSpiritVeinSupply,
    long MonthlySpiritVeinYield);

public readonly record struct AtlasLayerSnapshot(
    byte Number,
    string Name,
    AtlasImplementationState ImplementationState,
    int LocationCount,
    int Population,
    int SpiritVeinSourceCount,
    long RemainingSpiritVeinSupply);

public sealed record WorldAtlasSnapshot(
    int Revision,
    IReadOnlyList<AtlasLayerSnapshot> Layers,
    IReadOnlyList<AtlasNodeDefinition> Hierarchy,
    int ProductionLocationCount,
    int SpiritVeinSourceCount,
    long RemainingSpiritVeinSupply)
{
    public static WorldAtlasSnapshot Empty { get; } = new(
        0,
        Array.Empty<AtlasLayerSnapshot>(),
        Array.Empty<AtlasNodeDefinition>(),
        0,
        0,
        0);
}

public readonly record struct WorldAtlasCheckpoint(
    int Revision,
    ulong[] KnownLocationBits,
    ushort[] LastDiscoveredLocationIds,
    long[] LastDiscoveryMonths,
    MovementRouteState[] Routes,
    long[] SpiritVeinRemainingSupply);

public static class WorldAtlasRules
{
    public const int CurrentRevision = 1;
    public const int HeavenlyLayerCount = 5;
    public const int ProductionHeavenlyLayerCount = 2;
    public const int LocationsPerProductionLayer = 9;
    public const int ProductionLocationCount =
        ProductionHeavenlyLayerCount * LocationsPerProductionLayer;
    public const int SpiritVeinSourceCount = 900;
    public const int SpiritVeinsPerLocation =
        SpiritVeinSourceCount / ProductionLocationCount;
    public const ushort UnknownLocationId = ushort.MaxValue;
    public const int InformationStaleAfterMonths = 120;

    private static readonly HeavenlyLayerDefinition[] HeavenlyLayers =
    [
        new(1, "제1층천 · 현진천", new AtlasNodeId(1), AtlasImplementationState.Production),
        new(2, "제2층천 · 청명천", new AtlasNodeId(2), AtlasImplementationState.Production),
        new(3, "제3층천 · 예정", new AtlasNodeId(3), AtlasImplementationState.Planned),
        new(4, "제4층천 · 예정", new AtlasNodeId(4), AtlasImplementationState.Planned),
        new(5, "제5층천 · 예정", new AtlasNodeId(5), AtlasImplementationState.Planned),
    ];

    private static readonly SpiritVeinProfileDefinition[] SpiritVeinProfiles =
    [
        new(0, "미세 영맥", 2_500, 8, 80_000),
        new(1, "하품 영맥", 3_500, 12, 120_000),
        new(2, "중품 영맥", 5_000, 18, 180_000),
        new(3, "상품 영맥", 7_000, 28, 280_000),
        new(4, "극상품 영맥", 8_500, 45, 450_000),
        new(5, "천품 영맥", 9_500, 80, 800_000),
    ];

    private static readonly AtlasNodeDefinition[] Hierarchy = BuildHierarchy();
    private static readonly SpiritVeinSourceDefinition[] SpiritVeins = BuildSpiritVeins();

    public static IReadOnlyList<HeavenlyLayerDefinition> AllHeavenlyLayers => HeavenlyLayers;
    public static IReadOnlyList<AtlasNodeDefinition> AllNodes => Hierarchy;
    public static IReadOnlyList<SpiritVeinProfileDefinition> AllSpiritVeinProfiles =>
        SpiritVeinProfiles;
    public static IReadOnlyList<SpiritVeinSourceDefinition> AllSpiritVeins => SpiritVeins;

    public static AtlasNodeDefinition GetLocationNode(ushort locationId)
    {
        if (locationId >= ProductionLocationCount)
        {
            throw new ArgumentOutOfRangeException(nameof(locationId));
        }

        return Hierarchy.Single(node =>
            node.Kind == AtlasNodeKind.Location
            && node.Id.Value == 1_000 + locationId);
    }

    public static HeavenlyLayerDefinition GetHeavenlyLayer(byte number)
    {
        if (number is < 1 or > HeavenlyLayerCount)
        {
            throw new ArgumentOutOfRangeException(nameof(number));
        }

        return HeavenlyLayers[number - 1];
    }

    public static SpiritVeinProfileDefinition GetSpiritVeinProfile(byte id)
    {
        if (id >= SpiritVeinProfiles.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(id));
        }

        return SpiritVeinProfiles[id];
    }

    public static ReadOnlySpan<SpiritVeinSourceDefinition> GetSpiritVeins(ushort locationId)
    {
        if (locationId >= ProductionLocationCount)
        {
            throw new ArgumentOutOfRangeException(nameof(locationId));
        }

        return SpiritVeins.AsSpan(
            locationId * SpiritVeinsPerLocation,
            SpiritVeinsPerLocation);
    }

    public static bool IsProductionLocation(ushort locationId) =>
        locationId < ProductionLocationCount;

    public static bool CanEnterLayer(RealmStage realm, byte layerNumber) => layerNumber switch
    {
        1 => true,
        2 => realm >= RealmStage.FoundationEstablishment,
        _ => false,
    };

    private static AtlasNodeDefinition[] BuildHierarchy()
    {
        var nodes = new List<AtlasNodeDefinition>(
            HeavenlyLayerCount + (ProductionHeavenlyLayerCount * 13));
        foreach (var layer in HeavenlyLayers)
        {
            nodes.Add(new AtlasNodeDefinition(
                layer.NodeId,
                null,
                AtlasNodeKind.HeavenlyLayer,
                layer.KoreanName,
                layer.Number,
                layer.ImplementationState,
                CurrentRevision));
        }

        for (byte layer = 1; layer <= ProductionHeavenlyLayerCount; layer++)
        {
            var continentId = new AtlasNodeId(10 + layer);
            nodes.Add(new AtlasNodeDefinition(
                continentId,
                GetHeavenlyLayer(layer).NodeId,
                AtlasNodeKind.Continent,
                layer == 1 ? "만상대륙" : "청명대륙",
                layer,
                AtlasImplementationState.Production,
                CurrentRevision));

            var grandRegionIds = new[]
            {
                new AtlasNodeId(100 + ((layer - 1) * 2)),
                new AtlasNodeId(101 + ((layer - 1) * 2)),
            };
            for (var region = 0; region < grandRegionIds.Length; region++)
            {
                nodes.Add(new AtlasNodeDefinition(
                    grandRegionIds[region],
                    continentId,
                    AtlasNodeKind.GrandRegion,
                    region == 0 ? "북천주" : "남천주",
                    layer,
                    AtlasImplementationState.Production,
                    CurrentRevision));
            }

            for (var local = 0; local < LocationsPerProductionLayer; local++)
            {
                var locationId = ((layer - 1) * LocationsPerProductionLayer) + local;
                var areaId = new AtlasNodeId(200 + locationId);
                nodes.Add(new AtlasNodeDefinition(
                    areaId,
                    grandRegionIds[local < 5 ? 0 : 1],
                    AtlasNodeKind.Area,
                    $"{(layer == 1 ? "현진" : "청명")}{local + 1}권역",
                    layer,
                    AtlasImplementationState.Production,
                    CurrentRevision));
                nodes.Add(new AtlasNodeDefinition(
                    new AtlasNodeId(1_000 + locationId),
                    areaId,
                    AtlasNodeKind.Location,
                    $"L{locationId:00}",
                    layer,
                    AtlasImplementationState.Production,
                    CurrentRevision));
            }
        }

        return nodes.ToArray();
    }

    private static SpiritVeinSourceDefinition[] BuildSpiritVeins()
    {
        var sources = new SpiritVeinSourceDefinition[SpiritVeinSourceCount];
        for (var index = 0; index < sources.Length; index++)
        {
            var locationId = checked((ushort)(index / SpiritVeinsPerLocation));
            sources[index] = new SpiritVeinSourceDefinition(
                new SpiritVeinSourceId(index),
                new AtlasNodeId(1_000 + locationId),
                locationId,
                checked((byte)(index % SpiritVeinProfiles.Length)));
        }

        return sources;
    }
}
