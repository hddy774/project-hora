using System.Numerics;

namespace Chronicles.Simulation;

internal sealed class SpatialAuthorityStore
{
    private readonly ulong[] _knownLocationBits;
    private readonly ushort[] _lastDiscoveredLocationIds;
    private readonly long[] _lastDiscoveryMonths;
    private readonly MovementRouteState[] _routes;
    private readonly long[] _spiritVeinRemainingSupply;
    private readonly int[] _cultivationEnvironmentQualityHundredths =
        new int[WorldAtlasRules.ProductionLocationCount];
    private readonly int[] _populationByLocation =
        new int[WorldAtlasRules.ProductionLocationCount];

    public SpatialAuthorityStore(int population)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(population);
        _knownLocationBits = new ulong[population];
        _lastDiscoveredLocationIds = new ushort[population];
        _lastDiscoveryMonths = new long[population];
        _routes = new MovementRouteState[population];
        _spiritVeinRemainingSupply = new long[WorldAtlasRules.SpiritVeinSourceCount];

        for (var source = 0; source < _spiritVeinRemainingSupply.Length; source++)
        {
            var definition = WorldAtlasRules.AllSpiritVeins[source];
            _spiritVeinRemainingSupply[source] = WorldAtlasRules
                .GetSpiritVeinProfile(definition.ProfileId)
                .InitialSupply;
        }
        RebuildCultivationEnvironmentCache();

        for (var agent = 0; agent < population; agent++)
        {
            InitializeAgent(agent, WorldMapRules.CityLocationId, currentMonth: 0);
        }
        _populationByLocation[WorldMapRules.CityLocationId] = population;
    }

    private SpatialAuthorityStore(WorldAtlasCheckpoint checkpoint, int population)
    {
        ValidateCheckpoint(checkpoint, population);
        _knownLocationBits = checkpoint.KnownLocationBits.ToArray();
        _lastDiscoveredLocationIds = checkpoint.LastDiscoveredLocationIds.ToArray();
        _lastDiscoveryMonths = checkpoint.LastDiscoveryMonths.ToArray();
        _routes = checkpoint.Routes.ToArray();
        _spiritVeinRemainingSupply = checkpoint.SpiritVeinRemainingSupply.ToArray();
        RebuildCultivationEnvironmentCache();
    }

    public static int Revision => WorldAtlasRules.CurrentRevision;
    public ReadOnlySpan<int> PopulationByLocation => _populationByLocation;

    public static SpatialAuthorityStore Restore(
        WorldAtlasCheckpoint checkpoint,
        ReadOnlySpan<ushort> locations)
    {
        var store = new SpatialAuthorityStore(checkpoint, locations.Length);
        store.RebuildPopulationIndex(locations);
        return store;
    }

    public static SpatialAuthorityStore CreateLegacy(
        ReadOnlySpan<ushort> locations,
        long currentMonth)
    {
        var store = new SpatialAuthorityStore(locations.Length);
        for (var agent = 0; agent < locations.Length; agent++)
        {
            store.InitializeAgent(agent, locations[agent], currentMonth);
        }
        store.RebuildPopulationIndex(locations);
        return store;
    }

    public void InitializeAgent(int agent, ushort locationId, long currentMonth)
    {
        ValidateAgent(agent);
        if (!WorldAtlasRules.IsProductionLocation(locationId))
        {
            throw new ArgumentOutOfRangeException(nameof(locationId));
        }

        var known = 1UL << locationId;
        foreach (var neighbor in WorldMapRules.GetConnectedLocations(locationId))
        {
            if (WorldMapRules.GetLocation(neighbor).HeavenlyLayer
                == WorldMapRules.GetLocation(locationId).HeavenlyLayer)
            {
                known |= 1UL << neighbor;
            }
        }

        _knownLocationBits[agent] = known;
        _lastDiscoveredLocationIds[agent] = locationId;
        _lastDiscoveryMonths[agent] = currentMonth;
        _routes[agent] = MovementRouteState.Idle(locationId);
    }

    public MovementResolutionCode PlanRoute(
        int agent,
        ushort currentLocationId,
        ushort destinationLocationId,
        RealmStage realm,
        long startedTick,
        long dueTick)
    {
        ValidateAgent(agent);
        ArgumentOutOfRangeException.ThrowIfNegative(startedTick);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(dueTick, startedTick);

        var resolution = ValidateRoute(
            currentLocationId,
            destinationLocationId,
            realm,
            WorldAtlasRules.CurrentRevision);
        if (resolution != MovementResolutionCode.Planned)
        {
            _routes[agent] = new MovementRouteState(
                currentLocationId,
                destinationLocationId,
                WorldAtlasRules.CurrentRevision,
                startedTick,
                dueTick,
                MovementStatus.Blocked,
                resolution);
            return resolution;
        }

        _routes[agent] = new MovementRouteState(
            currentLocationId,
            destinationLocationId,
            WorldAtlasRules.CurrentRevision,
            startedTick,
            dueTick,
            MovementStatus.InTransit,
            MovementResolutionCode.Planned);
        return MovementResolutionCode.Planned;
    }

    public MovementResolutionCode CompleteRoute(
        int agent,
        ushort currentLocationId,
        RealmStage realm,
        long currentTick,
        bool travelerAlive,
        out ushort destinationLocationId)
    {
        ValidateAgent(agent);
        ArgumentOutOfRangeException.ThrowIfNegative(currentTick);
        var route = _routes[agent];
        destinationLocationId = currentLocationId;
        if (!travelerAlive)
        {
            _routes[agent] = route with
            {
                Status = MovementStatus.Interrupted,
                Resolution = MovementResolutionCode.TravelerDied,
            };
            return MovementResolutionCode.TravelerDied;
        }

        if (route.Status != MovementStatus.InTransit
            || route.OriginLocationId != currentLocationId)
        {
            _routes[agent] = route with
            {
                Status = MovementStatus.Blocked,
                Resolution = MovementResolutionCode.InformationStale,
            };
            return MovementResolutionCode.InformationStale;
        }

        var validation = ValidateRoute(
            currentLocationId,
            route.DestinationLocationId,
            realm,
            route.PlannedAtlasRevision);
        if (validation != MovementResolutionCode.Planned)
        {
            _routes[agent] = route with
            {
                Status = MovementStatus.Blocked,
                Resolution = validation,
            };
            return validation;
        }

        destinationLocationId = route.DestinationLocationId;
        _routes[agent] = route with
        {
            Status = MovementStatus.Arrived,
            Resolution = MovementResolutionCode.Arrived,
            DueTick = currentTick,
        };
        return MovementResolutionCode.Arrived;
    }

    public void RecordArrival(
        int agent,
        ushort originLocationId,
        ushort destinationLocationId,
        long currentMonth)
    {
        ValidateAgent(agent);
        if (originLocationId != destinationLocationId)
        {
            _populationByLocation[originLocationId]--;
            _populationByLocation[destinationLocationId]++;
        }

        Discover(agent, destinationLocationId, currentMonth);
        foreach (var neighbor in WorldMapRules.GetConnectedLocations(destinationLocationId))
        {
            Discover(agent, neighbor, currentMonth);
        }
    }

    public MovementResolutionCode Reroute(
        int agent,
        ushort currentLocationId,
        RealmStage realm,
        long startedTick,
        long dueTick,
        out ushort destinationLocationId)
    {
        ValidateAgent(agent);
        foreach (var candidate in GetPerceivedDestinations(agent, currentLocationId, realm))
        {
            if (candidate == currentLocationId)
            {
                continue;
            }

            destinationLocationId = candidate;
            _routes[agent] = new MovementRouteState(
                currentLocationId,
                candidate,
                WorldAtlasRules.CurrentRevision,
                startedTick,
                dueTick,
                MovementStatus.Rerouted,
                MovementResolutionCode.Rerouted);
            _routes[agent] = _routes[agent] with { Status = MovementStatus.InTransit };
            return MovementResolutionCode.Rerouted;
        }

        destinationLocationId = currentLocationId;
        return MovementResolutionCode.DestinationUnavailable;
    }

    public IReadOnlyList<ushort> GetPerceivedDestinations(
        int agent,
        ushort currentLocationId,
        RealmStage realm)
    {
        ValidateAgent(agent);
        // AI는 전체 Atlas를 훑지 않고 현재 지점에 직접 연결되며 경지상 진입 가능한
        // 지점만 본다. 인접 배열은 불변 캐시이므로 행동마다 컬렉션을 만들지 않는다.
        return WorldMapRules.GetAccessibleConnectedLocations(currentLocationId, realm);
    }

    public bool IsKnown(int agent, ushort locationId)
    {
        ValidateAgent(agent);
        return WorldAtlasRules.IsProductionLocation(locationId)
            && (_knownLocationBits[agent] & (1UL << locationId)) != 0;
    }

    public SpatialKnowledgeSnapshot GetKnowledgeSnapshot(
        int agent,
        ushort currentLocationId)
    {
        ValidateAgent(agent);
        var last = _lastDiscoveredLocationIds[agent];
        return new SpatialKnowledgeSnapshot(
            BitOperations.PopCount(_knownLocationBits[agent]),
            WorldAtlasRules.ProductionLocationCount,
            last == WorldAtlasRules.UnknownLocationId ? null : last,
            _lastDiscoveryMonths[agent],
            IsKnown(agent, currentLocationId));
    }

    public MovementRouteState GetRoute(int agent)
    {
        ValidateAgent(agent);
        return _routes[agent];
    }

    public void InterruptMovement(int agent, MovementResolutionCode resolution)
    {
        ValidateAgent(agent);
        if (resolution is not MovementResolutionCode.TravelerDied
            and not MovementResolutionCode.DestinationUnavailable
            and not MovementResolutionCode.RouteRevisionChanged)
        {
            throw new ArgumentOutOfRangeException(nameof(resolution));
        }
        _routes[agent] = _routes[agent] with
        {
            Status = MovementStatus.Interrupted,
            Resolution = resolution,
        };
    }

    public AtlasLocationContextSnapshot GetLocationContext(ushort locationId)
    {
        var location = WorldMapRules.GetLocation(locationId);
        var locationNode = WorldAtlasRules.GetLocationNode(locationId);
        var area = GetNode(locationNode.ParentId!.Value);
        var grandRegion = GetNode(area.ParentId!.Value);
        var continent = GetNode(grandRegion.ParentId!.Value);
        var layer = WorldAtlasRules.GetHeavenlyLayer(location.HeavenlyLayer);
        long supply = 0;
        long monthlyYield = 0;
        foreach (var source in WorldAtlasRules.GetSpiritVeins(locationId))
        {
            var remaining = _spiritVeinRemainingSupply[source.Id.Value];
            supply = checked(supply + remaining);
            if (remaining > 0)
            {
                monthlyYield = checked(
                    monthlyYield
                    + WorldAtlasRules.GetSpiritVeinProfile(source.ProfileId).MonthlyYield);
            }
        }

        return new AtlasLocationContextSnapshot(
            location.HeavenlyLayer,
            layer.KoreanName,
            continent.KoreanName,
            grandRegion.KoreanName,
            area.KoreanName,
            locationNode.Id,
            WorldAtlasRules.SpiritVeinsPerLocation,
            supply,
            monthlyYield);
    }

    public bool TryExtractSpiritStones(
        ushort locationId,
        long requested,
        out long extracted)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requested);
        extracted = 0;
        foreach (var source in WorldAtlasRules.GetSpiritVeins(locationId))
        {
            var available = _spiritVeinRemainingSupply[source.Id.Value];
            if (available <= 0)
            {
                continue;
            }

            var take = Math.Min(available, requested - extracted);
            _spiritVeinRemainingSupply[source.Id.Value] -= take;
            extracted += take;
            if (extracted == requested)
            {
                break;
            }
        }

        if (extracted > 0)
        {
            RebuildCultivationEnvironment(locationId);
        }

        return extracted > 0;
    }

    public int GetCultivationEnvironmentQualityHundredths(ushort locationId)
    {
        if (!WorldAtlasRules.IsProductionLocation(locationId))
        {
            throw new ArgumentOutOfRangeException(nameof(locationId));
        }

        return _cultivationEnvironmentQualityHundredths[locationId];
    }

    public long GetRemainingSpiritStoneSupply(ushort locationId)
    {
        long total = 0;
        foreach (var source in WorldAtlasRules.GetSpiritVeins(locationId))
        {
            total = checked(total + _spiritVeinRemainingSupply[source.Id.Value]);
        }
        return total;
    }

    public WorldAtlasSnapshot CreateSnapshot()
    {
        var layers = new AtlasLayerSnapshot[WorldAtlasRules.HeavenlyLayerCount];
        long totalSupply = 0;
        for (byte layerNumber = 1; layerNumber <= WorldAtlasRules.HeavenlyLayerCount; layerNumber++)
        {
            var layer = WorldAtlasRules.GetHeavenlyLayer(layerNumber);
            var locationStart = (layerNumber - 1) * WorldAtlasRules.LocationsPerProductionLayer;
            var locationCount = layer.ImplementationState == AtlasImplementationState.Production
                ? WorldAtlasRules.LocationsPerProductionLayer
                : 0;
            var population = 0;
            long layerSupply = 0;
            if (locationCount > 0)
            {
                for (var location = locationStart; location < locationStart + locationCount; location++)
                {
                    population += _populationByLocation[location];
                    foreach (var source in WorldAtlasRules.GetSpiritVeins(checked((ushort)location)))
                    {
                        layerSupply = checked(
                            layerSupply + _spiritVeinRemainingSupply[source.Id.Value]);
                    }
                }
            }

            totalSupply = checked(totalSupply + layerSupply);
            layers[layerNumber - 1] = new AtlasLayerSnapshot(
                layerNumber,
                layer.KoreanName,
                layer.ImplementationState,
                locationCount,
                population,
                locationCount * WorldAtlasRules.SpiritVeinsPerLocation,
                layerSupply);
        }

        return new WorldAtlasSnapshot(
            WorldAtlasRules.CurrentRevision,
            layers,
            WorldAtlasRules.AllNodes,
            WorldAtlasRules.ProductionLocationCount,
            WorldAtlasRules.SpiritVeinSourceCount,
            totalSupply);
    }

    public WorldAtlasCheckpoint CreateCheckpoint() => new(
        WorldAtlasRules.CurrentRevision,
        _knownLocationBits.ToArray(),
        _lastDiscoveredLocationIds.ToArray(),
        _lastDiscoveryMonths.ToArray(),
        _routes.ToArray(),
        _spiritVeinRemainingSupply.ToArray());

    public void AddStateHash(ref ulong hash)
    {
        Add(ref hash, checked((ulong)WorldAtlasRules.CurrentRevision));
        for (var agent = 0; agent < _knownLocationBits.Length; agent++)
        {
            Add(ref hash, _knownLocationBits[agent]);
            Add(ref hash, _lastDiscoveredLocationIds[agent]);
            Add(ref hash, unchecked((ulong)_lastDiscoveryMonths[agent]));
            var route = _routes[agent];
            Add(ref hash, route.OriginLocationId);
            Add(ref hash, route.DestinationLocationId);
            Add(ref hash, checked((ulong)route.PlannedAtlasRevision));
            Add(ref hash, unchecked((ulong)route.StartedTick));
            Add(ref hash, unchecked((ulong)route.DueTick));
            Add(ref hash, (ulong)route.Status);
            Add(ref hash, (ulong)route.Resolution);
        }
        foreach (var supply in _spiritVeinRemainingSupply)
        {
            Add(ref hash, checked((ulong)supply));
        }
    }

    public static void ValidateCheckpoint(WorldAtlasCheckpoint checkpoint, int population)
    {
        if (checkpoint.Revision != WorldAtlasRules.CurrentRevision
            || checkpoint.KnownLocationBits is not { } known
            || known.Length != population
            || known.Any(bits => (bits >> WorldAtlasRules.ProductionLocationCount) != 0)
            || checkpoint.LastDiscoveredLocationIds is not { } discovered
            || discovered.Length != population
            || discovered.Any(value => value != WorldAtlasRules.UnknownLocationId
                && !WorldAtlasRules.IsProductionLocation(value))
            || checkpoint.LastDiscoveryMonths is not { } months
            || months.Length != population
            || months.Any(value => value < 0)
            || checkpoint.Routes is not { } routes
            || routes.Length != population
            || routes.Any(route => !IsValidRouteState(route))
            || checkpoint.SpiritVeinRemainingSupply is not { } supplies
            || supplies.Length != WorldAtlasRules.SpiritVeinSourceCount
            || supplies.Any(value => value < 0))
        {
            throw new InvalidDataException("Atlas 권위 체크포인트가 잘못되었습니다.");
        }

        for (var source = 0; source < supplies.Length; source++)
        {
            var definition = WorldAtlasRules.AllSpiritVeins[source];
            if (supplies[source] > WorldAtlasRules
                .GetSpiritVeinProfile(definition.ProfileId)
                .InitialSupply)
            {
                throw new InvalidDataException("영맥 공급이 초기 권위량을 초과했습니다.");
            }
        }
    }

    private void Discover(int agent, ushort locationId, long currentMonth)
    {
        if (!WorldAtlasRules.IsProductionLocation(locationId))
        {
            return;
        }

        var mask = 1UL << locationId;
        if ((_knownLocationBits[agent] & mask) != 0)
        {
            return;
        }

        _knownLocationBits[agent] |= mask;
        _lastDiscoveredLocationIds[agent] = locationId;
        _lastDiscoveryMonths[agent] = currentMonth;
    }

    private void RebuildCultivationEnvironmentCache()
    {
        for (ushort locationId = 0;
             locationId < WorldAtlasRules.ProductionLocationCount;
             locationId++)
        {
            RebuildCultivationEnvironment(locationId);
        }
    }

    private void RebuildCultivationEnvironment(ushort locationId)
    {
        var qualityTotal = 0;
        var activeCount = 0;
        foreach (var source in WorldAtlasRules.GetSpiritVeins(locationId))
        {
            if (_spiritVeinRemainingSupply[source.Id.Value] <= 0)
            {
                continue;
            }

            qualityTotal += WorldAtlasRules.GetSpiritVeinProfile(source.ProfileId)
                .QualityHundredths;
            activeCount++;
        }

        _cultivationEnvironmentQualityHundredths[locationId] = activeCount == 0
            ? 0
            : qualityTotal / activeCount;
    }

    private void RebuildPopulationIndex(ReadOnlySpan<ushort> locations)
    {
        Array.Clear(_populationByLocation);
        foreach (var location in locations)
        {
            if (!WorldAtlasRules.IsProductionLocation(location))
            {
                throw new InvalidDataException("인물의 Atlas 위치가 구현 범위를 벗어났습니다.");
            }
            _populationByLocation[location]++;
        }
    }

    private static MovementResolutionCode ValidateRoute(
        ushort currentLocationId,
        ushort destinationLocationId,
        RealmStage realm,
        int plannedRevision)
    {
        if (plannedRevision != WorldAtlasRules.CurrentRevision)
        {
            return MovementResolutionCode.RouteRevisionChanged;
        }
        if (!WorldAtlasRules.IsProductionLocation(destinationLocationId))
        {
            return MovementResolutionCode.DestinationUnavailable;
        }
        if (!WorldMapRules.AreDirectlyConnected(currentLocationId, destinationLocationId))
        {
            return MovementResolutionCode.InformationStale;
        }

        return WorldAtlasRules.CanEnterLayer(
            realm,
            WorldMapRules.GetLocation(destinationLocationId).HeavenlyLayer)
                ? MovementResolutionCode.Planned
                : MovementResolutionCode.AccessDenied;
    }

    private static bool IsValidRouteState(MovementRouteState route) =>
        WorldAtlasRules.IsProductionLocation(route.OriginLocationId)
        && WorldAtlasRules.IsProductionLocation(route.DestinationLocationId)
        && route.PlannedAtlasRevision == WorldAtlasRules.CurrentRevision
        && route.StartedTick >= 0
        && route.DueTick >= route.StartedTick
        && Enum.IsDefined(route.Status)
        && Enum.IsDefined(route.Resolution);

    private static AtlasNodeDefinition GetNode(Chronicles.Core.AtlasNodeId id) =>
        WorldAtlasRules.AllNodes.Single(node => node.Id == id);

    private void ValidateAgent(int agent)
    {
        if ((uint)agent >= (uint)_knownLocationBits.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(agent));
        }
    }

    private static void Add(ref ulong hash, ulong value)
    {
        hash ^= value;
        hash *= 1099511628211UL;
    }
}
