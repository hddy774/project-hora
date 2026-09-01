using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed class ContentAuthorityStore
{
    public const int CurrentSchemaRevision = 1;
    private const ulong InitialRealmSourcePrefix = 0xC011_0000_0000_0000UL;

    private readonly List<ContentInstanceCheckpoint> _instances = [];
    private readonly List<ContentProvenanceCheckpoint> _provenance = [];
    private readonly Dictionary<long, int> _instanceIndexById = [];
    private readonly Dictionary<
        (ContentDiscovererKind Kind, long Id, long Subject),
        ContentDiscoveryCheckpoint> _discoveries = [];
    private readonly HashSet<ulong> _appliedSourceResults = [];
    private readonly Dictionary<ContentOwnerRef, List<ContentInstanceId>> _instancesByOwner = [];
    private readonly Dictionary<ushort, List<ContentInstanceId>> _instancesByLocation = [];
    private long _nextInstanceId = 1;
    private long _nextProvenanceId = 1;
    private long _provenanceSequence;

    public int InstanceCount => _instances.Count;
    public int ProvenanceCount => _provenance.Count;
    public int DiscoveryCount => _discoveries.Count;

    public static ContentAuthorityStore CreateInitial(ulong worldSeed)
    {
        var store = new ContentAuthorityStore();
        var locationOffset = checked((int)(worldSeed
            % (ulong)WorldAtlasRules.ProductionLocationCount));
        for (var index = 0; index < ContentPopulationRules.InitialSecretRealmCount; index++)
        {
            var natural = index < ContentPopulationRules.InitialNaturalSecretRealmCount;
            var definition = ContentDefinitionRegistry.Get(
                natural ? "secret-realm.natural" : "secret-realm.artificial");
            var result = store.TryCreate(new CreateContentInstanceCommand(
                definition.Id,
                ContentOwnerRef.World,
                new ContentHolderRef(ContentHolderKind.WorldLocation, 0),
                checked((ushort)((locationOffset + index)
                    % WorldAtlasRules.ProductionLocationCount)),
                Quantity: 1,
                ContentQualityAvailability.DataUnavailable,
                QualityHundredths: 0,
                ConditionHundredths: 10_000,
                new ContentSourceResultId(InitialRealmSourcePrefix + checked((ulong)index + 1UL)),
                Tick: 0));
            if (!result.Applied)
            {
                throw new InvalidOperationException(
                    $"초기 비경 생성에 실패했습니다: {index} · {result.Status}");
            }
        }

        return store;
    }

    public static ContentAuthorityStore Restore(ContentAuthorityCheckpoint checkpoint)
    {
        ValidateCheckpoint(checkpoint);
        var store = new ContentAuthorityStore
        {
            _nextInstanceId = checkpoint.NextInstanceId,
            _nextProvenanceId = checkpoint.NextProvenanceId,
            _provenanceSequence = checkpoint.ProvenanceSequence,
        };
        store._instances.AddRange(checkpoint.Instances);
        store._provenance.AddRange(checkpoint.Provenance);
        foreach (var discovery in checkpoint.Discoveries)
        {
            store._discoveries.Add(
                (discovery.DiscovererKind, discovery.DiscovererId, discovery.SubjectInstanceId),
                discovery);
        }
        foreach (var provenance in checkpoint.Provenance)
        {
            store._appliedSourceResults.Add(provenance.SourceResultId);
        }
        store.RebuildIndexes();
        return store;
    }

    public ContentCommandResult TryCreate(CreateContentInstanceCommand command)
    {
        if (!ContentDefinitionRegistry.TryGet(command.DefinitionId, out var definition)
            || definition.Revision != ContentDefinitionRegistry.CurrentRevision)
        {
            return new ContentCommandResult(ContentCommandStatus.InvalidDefinition);
        }
        if (!command.Owner.IsValid)
        {
            return new ContentCommandResult(ContentCommandStatus.InvalidOwner);
        }
        if (!command.Holder.IsValid || !IsHolderCompatible(command.Owner, command.Holder))
        {
            return new ContentCommandResult(ContentCommandStatus.InvalidHolder);
        }
        if (!WorldAtlasRules.IsProductionLocation(command.LocationId))
        {
            return new ContentCommandResult(ContentCommandStatus.InvalidLocation);
        }
        if (!IsValidInitialQuantity(definition, command.Quantity))
        {
            return new ContentCommandResult(ContentCommandStatus.InvalidQuantity);
        }
        if (!IsValidQuality(
                command.QualityAvailability,
                command.QualityHundredths,
                command.ConditionHundredths))
        {
            return new ContentCommandResult(ContentCommandStatus.InvalidQuality);
        }
        if (command.Tick < 0)
        {
            return new ContentCommandResult(ContentCommandStatus.InvalidTransition);
        }
        var sourceStatus = RegisterSourceResult(command.SourceResultId);
        if (sourceStatus != ContentCommandStatus.Applied)
        {
            return new ContentCommandResult(sourceStatus);
        }

        var instanceId = new ContentInstanceId(_nextInstanceId++);
        var provenanceId = AppendProvenance(
            instanceId,
            command.Tick,
            command.SourceResultId,
            ContentProvenanceKind.Created,
            ContentOwnerRef.World,
            command.Owner,
            command.Quantity,
            parent: null);
        var instance = new ContentInstanceCheckpoint(
            instanceId.Value,
            command.DefinitionId.Value,
            definition.Revision,
            definition.InstanceKind,
            command.Owner.Kind,
            command.Owner.Id,
            command.Holder.Kind,
            command.Holder.Id,
            command.LocationId,
            command.Quantity,
            command.QualityAvailability,
            command.QualityHundredths,
            command.ConditionHundredths,
            ContentInstanceStatus.Active,
            AuthorityRevision: 1,
            provenanceId.Value);
        _instanceIndexById.Add(instanceId.Value, _instances.Count);
        _instances.Add(instance);
        AddToIndexes(instance);
        return new ContentCommandResult(
            ContentCommandStatus.Applied,
            instanceId,
            provenanceId,
            command.Quantity);
    }

    public ContentCommandResult TryTransfer(TransferContentCommand command)
    {
        if (!TryGetMutable(command.InstanceId, out var index, out var instance))
        {
            return new ContentCommandResult(ContentCommandStatus.InvalidInstance);
        }
        if (!command.ExpectedOwner.IsValid || !command.NewOwner.IsValid)
        {
            return new ContentCommandResult(ContentCommandStatus.InvalidOwner);
        }
        if (GetOwner(instance) != command.ExpectedOwner)
        {
            return new ContentCommandResult(ContentCommandStatus.OwnerMismatch);
        }
        if (!command.NewHolder.IsValid
            || !IsHolderCompatible(command.NewOwner, command.NewHolder))
        {
            return new ContentCommandResult(ContentCommandStatus.InvalidHolder);
        }
        if (!WorldAtlasRules.IsProductionLocation(command.NewLocationId))
        {
            return new ContentCommandResult(ContentCommandStatus.InvalidLocation);
        }
        if (instance.Status != ContentInstanceStatus.Active || command.Tick < 0)
        {
            return new ContentCommandResult(ContentCommandStatus.InvalidTransition);
        }
        var sourceStatus = RegisterSourceResult(command.SourceResultId);
        if (sourceStatus != ContentCommandStatus.Applied)
        {
            return new ContentCommandResult(sourceStatus);
        }

        var previousOwner = GetOwner(instance);
        RemoveFromIndexes(instance);
        var provenanceId = AppendProvenance(
            command.InstanceId,
            command.Tick,
            command.SourceResultId,
            ContentProvenanceKind.Transferred,
            previousOwner,
            command.NewOwner,
            instance.Quantity,
            new ContentProvenanceId(instance.LatestProvenanceId));
        instance = instance with
        {
            OwnerKind = command.NewOwner.Kind,
            OwnerId = command.NewOwner.Id,
            HolderKind = command.NewHolder.Kind,
            HolderId = command.NewHolder.Id,
            LocationId = command.NewLocationId,
            AuthorityRevision = checked(instance.AuthorityRevision + 1),
            LatestProvenanceId = provenanceId.Value,
        };
        _instances[index] = instance;
        AddToIndexes(instance);
        return new ContentCommandResult(
            ContentCommandStatus.Applied,
            command.InstanceId,
            provenanceId,
            instance.Quantity);
    }

    public ContentCommandResult TryReserve(ChangeContentStatusCommand command) =>
        TryChangeStatus(
            command,
            ContentInstanceStatus.Active,
            ContentInstanceStatus.Reserved,
            ContentProvenanceKind.Reserved);

    public ContentCommandResult TryReleaseReservation(ChangeContentStatusCommand command) =>
        TryChangeStatus(
            command,
            ContentInstanceStatus.Reserved,
            ContentInstanceStatus.Active,
            ContentProvenanceKind.ReservationReleased);

    public ContentCommandResult TryConsume(ConsumeContentCommand command)
    {
        if (!TryGetMutable(command.InstanceId, out var index, out var instance))
        {
            return new ContentCommandResult(ContentCommandStatus.InvalidInstance);
        }
        if (GetOwner(instance) != command.ExpectedOwner)
        {
            return new ContentCommandResult(ContentCommandStatus.OwnerMismatch);
        }
        if (instance.Kind == ContentInstanceKind.SecretRealm
            || instance.Status != ContentInstanceStatus.Active)
        {
            return new ContentCommandResult(ContentCommandStatus.InvalidTransition);
        }
        if (command.Quantity <= 0 || command.Quantity > instance.Quantity)
        {
            return new ContentCommandResult(ContentCommandStatus.InvalidQuantity);
        }
        if (command.Tick < 0)
        {
            return new ContentCommandResult(ContentCommandStatus.InvalidTransition);
        }
        var sourceStatus = RegisterSourceResult(command.SourceResultId);
        if (sourceStatus != ContentCommandStatus.Applied)
        {
            return new ContentCommandResult(sourceStatus);
        }

        var remaining = instance.Quantity - command.Quantity;
        var provenanceId = AppendProvenance(
            command.InstanceId,
            command.Tick,
            command.SourceResultId,
            ContentProvenanceKind.Consumed,
            command.ExpectedOwner,
            command.ExpectedOwner,
            command.Quantity,
            new ContentProvenanceId(instance.LatestProvenanceId));
        instance = instance with
        {
            Quantity = remaining,
            ConditionHundredths = remaining == 0 ? (ushort)0 : instance.ConditionHundredths,
            Status = remaining == 0
                ? ContentInstanceStatus.Consumed
                : ContentInstanceStatus.Active,
            AuthorityRevision = checked(instance.AuthorityRevision + 1),
            LatestProvenanceId = provenanceId.Value,
        };
        if (remaining == 0)
        {
            RemoveFromIndexes(_instances[index]);
        }
        _instances[index] = instance;
        return new ContentCommandResult(
            ContentCommandStatus.Applied,
            command.InstanceId,
            provenanceId,
            command.Quantity);
    }

    public ContentCommandResult TryDestroy(ChangeContentStatusCommand command)
    {
        if (!TryGetMutable(command.InstanceId, out var index, out var instance))
        {
            return new ContentCommandResult(ContentCommandStatus.InvalidInstance);
        }
        if (GetOwner(instance) != command.ExpectedOwner)
        {
            return new ContentCommandResult(ContentCommandStatus.OwnerMismatch);
        }
        if (instance.Kind == ContentInstanceKind.SecretRealm
            || instance.Status != ContentInstanceStatus.Active
            || command.Tick < 0)
        {
            return new ContentCommandResult(ContentCommandStatus.InvalidTransition);
        }
        var sourceStatus = RegisterSourceResult(command.SourceResultId);
        if (sourceStatus != ContentCommandStatus.Applied)
        {
            return new ContentCommandResult(sourceStatus);
        }

        var provenanceId = AppendProvenance(
            command.InstanceId,
            command.Tick,
            command.SourceResultId,
            ContentProvenanceKind.Destroyed,
            command.ExpectedOwner,
            command.ExpectedOwner,
            instance.Quantity,
            new ContentProvenanceId(instance.LatestProvenanceId));
        instance = instance with
        {
            Quantity = 0,
            ConditionHundredths = 0,
            Status = ContentInstanceStatus.Destroyed,
            AuthorityRevision = checked(instance.AuthorityRevision + 1),
            LatestProvenanceId = provenanceId.Value,
        };
        RemoveFromIndexes(_instances[index]);
        _instances[index] = instance;
        return new ContentCommandResult(
            ContentCommandStatus.Applied,
            command.InstanceId,
            provenanceId);
    }

    public ContentCommandResult TryDiscover(DiscoverContentCommand command)
    {
        if (!command.Discoverer.IsValid)
        {
            return new ContentCommandResult(ContentCommandStatus.InvalidOwner);
        }
        if (command.State is ContentDiscoveryState.Unknown || command.Tick < 0)
        {
            return new ContentCommandResult(ContentCommandStatus.InvalidTransition);
        }
        if (!TryGetMutable(command.SubjectId, out var index, out var instance)
            || instance.Kind != ContentInstanceKind.SecretRealm)
        {
            return new ContentCommandResult(ContentCommandStatus.InvalidInstance);
        }
        if (instance.Status != ContentInstanceStatus.Active)
        {
            return new ContentCommandResult(ContentCommandStatus.AccessBlocked);
        }

        var key = (command.Discoverer.Kind, command.Discoverer.Id, command.SubjectId.Value);
        if (_discoveries.TryGetValue(key, out var current)
            && current.State >= command.State)
        {
            return new ContentCommandResult(ContentCommandStatus.AlreadyKnown);
        }
        var sourceStatus = RegisterSourceResult(command.SourceResultId);
        if (sourceStatus != ContentCommandStatus.Applied)
        {
            return new ContentCommandResult(sourceStatus);
        }

        var provenanceKind = command.State == ContentDiscoveryState.AccessGranted
            ? ContentProvenanceKind.AccessGranted
            : ContentProvenanceKind.Discovered;
        var provenanceId = AppendProvenance(
            command.SubjectId,
            command.Tick,
            command.SourceResultId,
            provenanceKind,
            GetOwner(instance),
            GetOwner(instance),
            quantity: 0,
            new ContentProvenanceId(instance.LatestProvenanceId));
        instance = instance with
        {
            AuthorityRevision = checked(instance.AuthorityRevision + 1),
            LatestProvenanceId = provenanceId.Value,
        };
        _instances[index] = instance;
        _discoveries[key] = new ContentDiscoveryCheckpoint(
            command.Discoverer.Kind,
            command.Discoverer.Id,
            command.SubjectId.Value,
            command.State,
            provenanceId.Value,
            current.Revision + 1,
            command.Tick);
        return new ContentCommandResult(
            ContentCommandStatus.Applied,
            command.SubjectId,
            provenanceId);
    }

    public ContentCommandResult TryCollapse(ChangeContentStatusCommand command)
    {
        if (!TryGetMutable(command.InstanceId, out var index, out var instance))
        {
            return new ContentCommandResult(ContentCommandStatus.InvalidInstance);
        }
        if (instance.Kind != ContentInstanceKind.SecretRealm
            || GetOwner(instance) != command.ExpectedOwner
            || instance.Status != ContentInstanceStatus.Active
            || command.Tick < 0)
        {
            return new ContentCommandResult(ContentCommandStatus.InvalidTransition);
        }
        var sourceStatus = RegisterSourceResult(command.SourceResultId);
        if (sourceStatus != ContentCommandStatus.Applied)
        {
            return new ContentCommandResult(sourceStatus);
        }

        var provenanceId = AppendProvenance(
            command.InstanceId,
            command.Tick,
            command.SourceResultId,
            ContentProvenanceKind.Collapsed,
            command.ExpectedOwner,
            command.ExpectedOwner,
            quantity: 1,
            new ContentProvenanceId(instance.LatestProvenanceId));
        instance = instance with
        {
            ConditionHundredths = 0,
            Status = ContentInstanceStatus.Collapsed,
            AuthorityRevision = checked(instance.AuthorityRevision + 1),
            LatestProvenanceId = provenanceId.Value,
        };
        RemoveFromIndexes(_instances[index]);
        _instances[index] = instance;
        return new ContentCommandResult(
            ContentCommandStatus.Applied,
            command.InstanceId,
            provenanceId);
    }

    public ContentInstanceCheckpoint GetInstance(ContentInstanceId id)
    {
        if (!_instanceIndexById.TryGetValue(id.Value, out var index))
        {
            throw new KeyNotFoundException($"콘텐츠 Instance를 찾을 수 없습니다: {id}");
        }
        return _instances[index];
    }

    public bool TryGetInstance(ContentInstanceId id, out ContentInstanceCheckpoint instance)
    {
        if (_instanceIndexById.TryGetValue(id.Value, out var index))
        {
            instance = _instances[index];
            return true;
        }

        instance = default;
        return false;
    }

    public IReadOnlyList<ContentInstanceId> GetByOwner(ContentOwnerRef owner) =>
        _instancesByOwner.TryGetValue(owner, out var values)
            ? values.AsReadOnly()
            : Array.Empty<ContentInstanceId>();

    public IReadOnlyList<ContentInstanceId> GetAtLocation(ushort locationId)
    {
        if (!WorldAtlasRules.IsProductionLocation(locationId))
        {
            throw new ArgumentOutOfRangeException(nameof(locationId));
        }
        return _instancesByLocation.TryGetValue(locationId, out var values)
            ? values.AsReadOnly()
            : Array.Empty<ContentInstanceId>();
    }

    public bool TryFindUndiscoveredSecretRealm(
        ContentDiscovererRef discoverer,
        ushort locationId,
        ulong selector,
        out ContentInstanceId instanceId)
    {
        if (!discoverer.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(discoverer));
        }
        if (!WorldAtlasRules.IsProductionLocation(locationId))
        {
            throw new ArgumentOutOfRangeException(nameof(locationId));
        }
        if (!_instancesByLocation.TryGetValue(locationId, out var candidates)
            || candidates.Count == 0)
        {
            instanceId = default;
            return false;
        }

        var start = checked((int)(selector % (ulong)candidates.Count));
        for (var offset = 0; offset < candidates.Count; offset++)
        {
            var candidateId = candidates[(start + offset) % candidates.Count];
            var instance = _instances[_instanceIndexById[candidateId.Value]];
            if (instance.Kind == ContentInstanceKind.SecretRealm
                && instance.Status == ContentInstanceStatus.Active
                && GetDiscoveryState(discoverer, candidateId) == ContentDiscoveryState.Unknown)
            {
                instanceId = candidateId;
                return true;
            }
        }

        instanceId = default;
        return false;
    }

    public WorldContentSnapshot CreateSnapshot(
        ContentDiscovererRef viewer,
        bool includeHidden,
        int secretRealmOffset = 0,
        int secretRealmLimit = 100)
    {
        if (!viewer.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(viewer));
        }
        ArgumentOutOfRangeException.ThrowIfNegative(secretRealmOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(secretRealmLimit);

        var natural = 0;
        var artificial = 0;
        var active = 0;
        var publicCount = 0;
        var viewerKnown = 0;
        var materialStacks = 0;
        var items = 0;
        var records = 0;
        var visibleCount = 0;
        List<(ContentInstanceCheckpoint Instance, ContentDiscoveryState State)>? visible =
            secretRealmLimit > 0 ? [] : null;
        foreach (var instance in _instances)
        {
            if (instance.Status is ContentInstanceStatus.Active or ContentInstanceStatus.Reserved)
            {
                active++;
            }
            switch (instance.Kind)
            {
                case ContentInstanceKind.SecretRealm:
                {
                    var definition = GetDefinition(instance);
                    if (definition.SecretRealmNature == SecretRealmNature.Natural)
                    {
                        natural++;
                    }
                    else
                    {
                        artificial++;
                    }

                    var publicState = GetDiscoveryState(
                        ContentDiscovererRef.Observer,
                        new ContentInstanceId(instance.InstanceId));
                    if (publicState >= ContentDiscoveryState.Located)
                    {
                        publicCount++;
                    }
                    var viewerState = GetDiscoveryState(
                        viewer,
                        new ContentInstanceId(instance.InstanceId));
                    if (viewerState >= ContentDiscoveryState.Located)
                    {
                        viewerKnown++;
                    }
                    if (includeHidden || viewerState >= ContentDiscoveryState.Located)
                    {
                        visibleCount++;
                        visible?.Add((instance, viewerState));
                    }
                    break;
                }
                case ContentInstanceKind.MaterialStack:
                    materialStacks++;
                    break;
                case ContentInstanceKind.Item:
                case ContentInstanceKind.CreatedWork:
                    items++;
                    break;
                case ContentInstanceKind.PhysicalRecord:
                    records++;
                    break;
                default:
                    throw new InvalidDataException(
                        $"알 수 없는 콘텐츠 Instance 종류입니다: {instance.Kind}");
            }
        }

        var rows = visible is null
            ? Array.Empty<ContentInstanceSnapshot>()
            : visible
                .OrderBy(item => item.Instance.InstanceId)
                .Skip(secretRealmOffset)
                .Take(secretRealmLimit)
                .Select(item => CreateSnapshot(item.Instance, item.State))
                .ToArray();
        return new WorldContentSnapshot(
            _provenanceSequence,
            ContentDefinitionRegistry.All.Count,
            _instances.Count,
            active,
            natural,
            artificial,
            publicCount,
            viewerKnown,
            materialStacks,
            items,
            records,
            rows,
            secretRealmOffset,
            visibleCount,
            includeHidden);
    }

    public ContentDiscoveryState GetDiscoveryState(
        ContentDiscovererRef discoverer,
        ContentInstanceId subjectId) =>
        _discoveries.TryGetValue(
            (discoverer.Kind, discoverer.Id, subjectId.Value),
            out var state)
                ? state.State
                : ContentDiscoveryState.Unknown;

    public ContentAuthorityCheckpoint CreateCheckpoint() => new(
        CurrentSchemaRevision,
        _nextInstanceId,
        _nextProvenanceId,
        _provenanceSequence,
        _instances.ToArray(),
        _provenance.ToArray(),
        _discoveries.Values
            .OrderBy(value => value.DiscovererKind)
            .ThenBy(value => value.DiscovererId)
            .ThenBy(value => value.SubjectInstanceId)
            .ToArray());

    public ulong ComputeStateHash()
    {
        var hash = 14_695_981_039_346_656_037UL;
        static void Add(ref ulong value, ulong item)
        {
            const ulong prime = 1_099_511_628_211UL;
            for (var shift = 0; shift < 64; shift += 8)
            {
                value ^= (byte)(item >> shift);
                value *= prime;
            }
        }

        var checkpoint = CreateCheckpoint();
        Add(ref hash, checked((ulong)checkpoint.SchemaRevision));
        Add(ref hash, checked((ulong)checkpoint.NextInstanceId));
        Add(ref hash, checked((ulong)checkpoint.NextProvenanceId));
        Add(ref hash, checked((ulong)checkpoint.ProvenanceSequence));
        foreach (var instance in checkpoint.Instances)
        {
            Add(ref hash, checked((ulong)instance.InstanceId));
            Add(ref hash, instance.DefinitionId);
            Add(ref hash, checked((ulong)instance.DefinitionRevision));
            Add(ref hash, (byte)instance.Kind);
            Add(ref hash, (byte)instance.OwnerKind);
            Add(ref hash, unchecked((ulong)instance.OwnerId));
            Add(ref hash, (byte)instance.HolderKind);
            Add(ref hash, unchecked((ulong)instance.HolderId));
            Add(ref hash, instance.LocationId);
            Add(ref hash, checked((ulong)instance.Quantity));
            Add(ref hash, (byte)instance.QualityAvailability);
            Add(ref hash, instance.QualityHundredths);
            Add(ref hash, instance.ConditionHundredths);
            Add(ref hash, (byte)instance.Status);
            Add(ref hash, checked((ulong)instance.AuthorityRevision));
            Add(ref hash, checked((ulong)instance.LatestProvenanceId));
        }
        foreach (var provenance in checkpoint.Provenance)
        {
            Add(ref hash, checked((ulong)provenance.ProvenanceId));
            Add(ref hash, checked((ulong)provenance.Sequence));
            Add(ref hash, checked((ulong)provenance.Tick));
            Add(ref hash, provenance.SourceResultId);
            Add(ref hash, (byte)provenance.Kind);
            Add(ref hash, checked((ulong)provenance.InstanceId));
            Add(ref hash, (byte)provenance.PreviousOwnerKind);
            Add(ref hash, unchecked((ulong)provenance.PreviousOwnerId));
            Add(ref hash, (byte)provenance.CurrentOwnerKind);
            Add(ref hash, unchecked((ulong)provenance.CurrentOwnerId));
            Add(ref hash, checked((ulong)provenance.Quantity));
            Add(ref hash, checked((ulong)provenance.ParentProvenanceId));
        }
        foreach (var discovery in checkpoint.Discoveries)
        {
            Add(ref hash, (byte)discovery.DiscovererKind);
            Add(ref hash, unchecked((ulong)discovery.DiscovererId));
            Add(ref hash, checked((ulong)discovery.SubjectInstanceId));
            Add(ref hash, (byte)discovery.State);
            Add(ref hash, checked((ulong)discovery.SourceProvenanceId));
            Add(ref hash, checked((ulong)discovery.Revision));
            Add(ref hash, checked((ulong)discovery.UpdatedTick));
        }
        return hash;
    }

    public static void ValidateCheckpoint(ContentAuthorityCheckpoint checkpoint) =>
        ContentAuthorityCheckpointValidator.Validate(checkpoint);

    private ContentCommandResult TryChangeStatus(
        ChangeContentStatusCommand command,
        ContentInstanceStatus expectedStatus,
        ContentInstanceStatus nextStatus,
        ContentProvenanceKind provenanceKind)
    {
        if (!TryGetMutable(command.InstanceId, out var index, out var instance))
        {
            return new ContentCommandResult(ContentCommandStatus.InvalidInstance);
        }
        if (GetOwner(instance) != command.ExpectedOwner)
        {
            return new ContentCommandResult(ContentCommandStatus.OwnerMismatch);
        }
        if (instance.Kind == ContentInstanceKind.SecretRealm
            || instance.Status != expectedStatus
            || command.Tick < 0)
        {
            return new ContentCommandResult(ContentCommandStatus.InvalidTransition);
        }
        var sourceStatus = RegisterSourceResult(command.SourceResultId);
        if (sourceStatus != ContentCommandStatus.Applied)
        {
            return new ContentCommandResult(sourceStatus);
        }

        var provenanceId = AppendProvenance(
            command.InstanceId,
            command.Tick,
            command.SourceResultId,
            provenanceKind,
            command.ExpectedOwner,
            command.ExpectedOwner,
            instance.Quantity,
            new ContentProvenanceId(instance.LatestProvenanceId));
        instance = instance with
        {
            Status = nextStatus,
            AuthorityRevision = checked(instance.AuthorityRevision + 1),
            LatestProvenanceId = provenanceId.Value,
        };
        _instances[index] = instance;
        return new ContentCommandResult(
            ContentCommandStatus.Applied,
            command.InstanceId,
            provenanceId,
            instance.Quantity);
    }

    private ContentProvenanceId AppendProvenance(
        ContentInstanceId instanceId,
        long tick,
        ContentSourceResultId sourceResultId,
        ContentProvenanceKind kind,
        ContentOwnerRef previousOwner,
        ContentOwnerRef currentOwner,
        long quantity,
        ContentProvenanceId? parent)
    {
        var id = new ContentProvenanceId(_nextProvenanceId++);
        _provenance.Add(new ContentProvenanceCheckpoint(
            id.Value,
            checked(++_provenanceSequence),
            tick,
            sourceResultId.Value,
            kind,
            instanceId.Value,
            previousOwner.Kind,
            previousOwner.Id,
            currentOwner.Kind,
            currentOwner.Id,
            quantity,
            parent?.Value ?? 0));
        return id;
    }

    private ContentCommandStatus RegisterSourceResult(ContentSourceResultId sourceResultId)
    {
        if (sourceResultId.Value == 0)
        {
            return ContentCommandStatus.InvalidSource;
        }
        return _appliedSourceResults.Add(sourceResultId.Value)
            ? ContentCommandStatus.Applied
            : ContentCommandStatus.DuplicateSource;
    }

    private bool TryGetMutable(
        ContentInstanceId id,
        out int index,
        out ContentInstanceCheckpoint instance)
    {
        if (_instanceIndexById.TryGetValue(id.Value, out index))
        {
            instance = _instances[index];
            return true;
        }
        instance = default;
        return false;
    }

    private void RebuildIndexes()
    {
        _instanceIndexById.Clear();
        _instancesByOwner.Clear();
        _instancesByLocation.Clear();
        for (var index = 0; index < _instances.Count; index++)
        {
            var instance = _instances[index];
            _instanceIndexById.Add(instance.InstanceId, index);
            if (IsIndexed(instance))
            {
                AddToIndexes(instance);
            }
        }
    }

    private void AddToIndexes(ContentInstanceCheckpoint instance)
    {
        var id = new ContentInstanceId(instance.InstanceId);
        var owner = GetOwner(instance);
        if (!_instancesByOwner.TryGetValue(owner, out var ownerValues))
        {
            ownerValues = [];
            _instancesByOwner.Add(owner, ownerValues);
        }
        ownerValues.Add(id);
        if (!_instancesByLocation.TryGetValue(instance.LocationId, out var locationValues))
        {
            locationValues = [];
            _instancesByLocation.Add(instance.LocationId, locationValues);
        }
        locationValues.Add(id);
    }

    private void RemoveFromIndexes(ContentInstanceCheckpoint instance)
    {
        var id = new ContentInstanceId(instance.InstanceId);
        if (_instancesByOwner.TryGetValue(GetOwner(instance), out var ownerValues))
        {
            ownerValues.Remove(id);
        }
        if (_instancesByLocation.TryGetValue(instance.LocationId, out var locationValues))
        {
            locationValues.Remove(id);
        }
    }

    private static ContentOwnerRef GetOwner(ContentInstanceCheckpoint instance) =>
        new(instance.OwnerKind, instance.OwnerId);

    private static ContentDefinition GetDefinition(ContentInstanceCheckpoint instance) =>
        ContentDefinitionRegistry.TryGet(
            new ContentDefinitionId(instance.DefinitionId),
            out var definition)
                ? definition
                : throw new InvalidDataException(
                    $"콘텐츠 Definition이 없습니다: {instance.DefinitionId:X16}");

    private static ContentInstanceSnapshot CreateSnapshot(
        ContentInstanceCheckpoint instance,
        ContentDiscoveryState viewerState)
    {
        var definition = GetDefinition(instance);
        return new ContentInstanceSnapshot(
            new ContentInstanceId(instance.InstanceId),
            definition.Id,
            definition.KoreanName,
            instance.Kind,
            definition.SecretRealmNature,
            GetOwner(instance),
            new ContentHolderRef(instance.HolderKind, instance.HolderId),
            instance.LocationId,
            instance.Quantity,
            instance.QualityAvailability,
            instance.QualityHundredths,
            instance.ConditionHundredths,
            instance.Status,
            viewerState,
            instance.AuthorityRevision,
            new ContentProvenanceId(instance.LatestProvenanceId));
    }

    private static bool IsValidInitialQuantity(ContentDefinition definition, long quantity) =>
        definition.Stackable ? quantity > 0 : quantity == 1;

    private static bool IsIndexed(ContentInstanceCheckpoint instance) =>
        instance.Status is ContentInstanceStatus.Active or ContentInstanceStatus.Reserved;

    private static bool IsValidQuality(
        ContentQualityAvailability availability,
        ushort quality,
        ushort condition) =>
        condition <= 10_000
        && availability switch
        {
            ContentQualityAvailability.DataUnavailable => quality == 0,
            ContentQualityAvailability.Specified => quality <= 10_000,
            _ => false,
        };

    private static bool IsHolderCompatible(ContentOwnerRef owner, ContentHolderRef holder) =>
        holder.Kind switch
        {
            ContentHolderKind.WorldLocation => true,
            ContentHolderKind.PersonalInventory =>
                owner.Kind == ContentOwnerKind.Agent && owner.Id == holder.Id,
            ContentHolderKind.CityWarehouse =>
                owner.Kind == ContentOwnerKind.City && owner.Id == holder.Id,
            ContentHolderKind.OrganizationWarehouse =>
                owner.Kind == ContentOwnerKind.Organization && owner.Id == holder.Id,
            ContentHolderKind.SecretRealmInterior => true,
            _ => false,
        };
}
