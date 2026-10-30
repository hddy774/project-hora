namespace Chronicles.Simulation;

internal sealed class FoundationFormationAuthorityStore
{
    private readonly int[] _methodRevisions;
    private readonly FoundationProcedureStep[] _completedSteps;
    private readonly FoundationRoute[] _routes;
    private readonly FoundationSourceKind[] _sourceKinds;
    private readonly ushort[] _locationIds;
    private readonly int[] _sourceRevisions;
    private readonly long[] _frozenTicks;
    private readonly int[] _sourceCounts;
    private readonly long[] _remainingSupplies;
    private readonly CultivationValueAvailability[] _availabilities;

    public FoundationFormationAuthorityStore(int population)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(population);
        _methodRevisions = new int[population];
        _completedSteps = new FoundationProcedureStep[population];
        _routes = new FoundationRoute[population];
        _sourceKinds = new FoundationSourceKind[population];
        _locationIds = new ushort[population];
        _sourceRevisions = new int[population];
        _frozenTicks = new long[population];
        _sourceCounts = new int[population];
        _remainingSupplies = new long[population];
        _availabilities = new CultivationValueAvailability[population];
        Array.Fill(_availabilities, CultivationValueAvailability.NotApplicable);
    }

    public void Reset(int index)
    {
        ValidateIndex(index);
        _methodRevisions[index] = 0;
        _completedSteps[index] = FoundationProcedureStep.None;
        _routes[index] = FoundationRoute.None;
        _sourceKinds[index] = FoundationSourceKind.None;
        _locationIds[index] = WorldAtlasRules.UnknownLocationId;
        _sourceRevisions[index] = 0;
        _frozenTicks[index] = 0;
        _sourceCounts[index] = 0;
        _remainingSupplies[index] = 0;
        _availabilities[index] = CultivationValueAvailability.NotApplicable;
    }

    public void Commit(int index, FoundationFormationCheckpoint checkpoint)
    {
        ValidateIndex(index);
        ValidateCheckpoint(checkpoint);
        _methodRevisions[index] = checkpoint.MethodRevision;
        _completedSteps[index] = checkpoint.CompletedSteps;
        _routes[index] = checkpoint.Source.Route;
        _sourceKinds[index] = checkpoint.Source.Kind;
        _locationIds[index] = checkpoint.Source.LocationId;
        _sourceRevisions[index] = checkpoint.Source.SourceRevision;
        _frozenTicks[index] = checkpoint.Source.FrozenTick;
        _sourceCounts[index] = checkpoint.Source.SourceCount;
        _remainingSupplies[index] = checkpoint.Source.RemainingSupply;
        _availabilities[index] = checkpoint.Source.Availability;
    }

    public void Restore(int index, FoundationFormationCheckpoint? checkpoint)
    {
        if (checkpoint is { } value)
        {
            Commit(index, value);
        }
        else
        {
            Reset(index);
        }
    }

    public FoundationFormationCheckpoint? CreateCheckpoint(int index)
    {
        ValidateIndex(index);
        if (_methodRevisions[index] == 0)
        {
            return null;
        }

        return new FoundationFormationCheckpoint(
            _methodRevisions[index],
            _completedSteps[index],
            new FoundationSourceSnapshot(
                _routes[index],
                _sourceKinds[index],
                _locationIds[index],
                _sourceRevisions[index],
                _frozenTicks[index],
                _sourceCounts[index],
                _remainingSupplies[index],
                _availabilities[index]));
    }

    public static void ValidateCheckpoint(FoundationFormationCheckpoint checkpoint)
    {
        var source = checkpoint.Source;
        if (checkpoint.MethodRevision != FoundationMethodRules.CurrentRevision
            || checkpoint.CompletedSteps != FoundationProcedureStep.Complete
            || source.Route == FoundationRoute.None
            || !Enum.IsDefined(source.Route)
            || source.Kind == FoundationSourceKind.None
            || !Enum.IsDefined(source.Kind)
            || !WorldAtlasRules.IsProductionLocation(source.LocationId)
            || source.SourceRevision <= 0
            || source.FrozenTick < 0
            || source.SourceCount <= 0
            || source.RemainingSupply < 0
            || source.Availability is not CultivationValueAvailability.Actual
                and not CultivationValueAvailability.ActualZero
            || !IsValidSourceShape(source))
        {
            throw new InvalidDataException("축기 형성 권위 체크포인트가 잘못되었습니다.");
        }
    }

    private static bool IsValidSourceShape(FoundationSourceSnapshot source) => source.Route switch
    {
        FoundationRoute.Human =>
            source.Kind == FoundationSourceKind.InternalBodyAndSpiritualRoot
            && source.SourceCount == 2
            && source.RemainingSupply == 0,
        FoundationRoute.Earth =>
            source.Kind == FoundationSourceKind.LocationSpiritVeins
            && source.SourceCount == WorldAtlasRules.SpiritVeinsPerLocation
            && source.RemainingSupply > 0,
        FoundationRoute.Heaven =>
            source.Kind == FoundationSourceKind.ManifestationEnvironmentWindow
            && source.SourceCount == 1
            && source.RemainingSupply == 0,
        _ => false,
    };

    private void ValidateIndex(int index)
    {
        if ((uint)index >= (uint)_methodRevisions.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}
