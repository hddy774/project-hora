using Chronicles.Core;

namespace Chronicles.Simulation;

/// <summary>
/// 의미 내용, 판본, 물리 기록물 binding, 접근과 믿음을 분리해 보존한다.
/// 긴 본문 문자열은 인물마다 복제하지 않고 Stable Content/Edition Ref만 저장한다.
/// </summary>
public sealed class KnowledgeAuthorityStore
{
    public const int CurrentSchemaRevision = 1;

    private readonly int _population;
    private readonly List<KnowledgeContentCheckpoint> _contents = [];
    private readonly List<KnowledgeEditionCheckpoint> _editions = [];
    private readonly List<KnowledgeRecordBindingCheckpoint> _recordBindings = [];
    private readonly List<KnowledgeAccessCheckpoint> _access = [];
    private readonly List<KnowledgeProvenanceCheckpoint> _provenance = [];
    private readonly Dictionary<ulong, int> _contentIndexById = [];
    private readonly Dictionary<long, int> _editionIndexById = [];
    private readonly Dictionary<long, int> _bindingIndexByRecord = [];
    private readonly Dictionary<(KnowledgeAccessorKind Kind, long Id, ulong Content), int>
        _accessIndexByOwnerAndContent = [];
    private readonly Dictionary<long, int> _physicalRecordCountByEdition = [];
    private readonly Dictionary<long, int> _accessCountByEdition = [];
    private readonly HashSet<ulong> _appliedSourceResults = [];
    private long _nextEditionId = 1;
    private long _nextProvenanceId = 1;
    private long _provenanceSequence;

    public KnowledgeAuthorityStore(int population)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(population);
        _population = population;
    }

    public int ContentCount => _contents.Count;
    public int EditionCount => _editions.Count;
    public int PhysicalRecordCount => _recordBindings.Count;
    public int AccessCount => _access.Count;
    public int ProvenanceCount => _provenance.Count;
    public long AuthorityRevision => _provenanceSequence;

    public static KnowledgeAuthorityStore CreateInitial(
        ulong worldSeed,
        int population,
        ContentAuthorityStore contentStore)
    {
        ArgumentNullException.ThrowIfNull(contentStore);
        var store = new KnowledgeAuthorityStore(population);
        for (var ordinal = 0; ordinal < KnowledgePopulationRules.InitialContentCount; ordinal++)
        {
            var kind = ordinal < KnowledgePopulationRules.TechniqueContentCount
                ? KnowledgeContentKind.Technique
                : KnowledgeContentKind.Spell;
            var contentId = new KnowledgeContentId(AuthorityRegistry.CreateStableId(
                "cmip.knowledge-content.initial",
                $"{worldSeed:X16}.{ordinal + 1:000}").Value);
            store.AddInitialContent(
                contentId,
                kind,
                ordinal + 1,
                new ContentSourceResultId(
                    KnowledgePopulationRules.InitialContentSourcePrefix
                    + checked((ulong)ordinal + 1UL)));
        }

        for (var index = 0; index < KnowledgePopulationRules.InitialEditionCount; index++)
        {
            var contentIndex = index < KnowledgePopulationRules.InitialContentCount
                ? index
                : index - KnowledgePopulationRules.InitialContentCount;
            var contentId = new KnowledgeContentId(store._contents[contentIndex].ContentId);
            var parent = index < KnowledgePopulationRules.InitialContentCount
                ? (KnowledgeEditionId?)null
                : new KnowledgeEditionId(contentIndex + 1L);
            var relation = parent is null
                ? KnowledgeEditionRelation.Original
                : KnowledgeEditionRelation.Copy;
            var result = store.TryCreateEdition(new CreateKnowledgeEditionCommand(
                contentId,
                parent,
                relation,
                ContributorAgent: -1,
                KnowledgeValueAvailability.DataUnavailable,
                ConfidenceHundredths: 0,
                new ContentSourceResultId(
                    KnowledgePopulationRules.InitialContentSourcePrefix
                    + checked(1_000UL + (ulong)index + 1UL)),
                Tick: 0));
            if (!result.Applied)
            {
                throw new InvalidOperationException(
                    $"초기 지식 판본 생성에 실패했습니다: {index} · {result.Status}");
            }
        }

        var locationOffset = checked((int)(worldSeed
            % (ulong)WorldAtlasRules.ProductionLocationCount));
        for (var index = 0; index < KnowledgePopulationRules.InitialPhysicalRecordCount; index++)
        {
            var edition = store._editions[index % store._editions.Count];
            var knowledge = store._contents[store._contentIndexById[edition.ContentId]];
            var definition = ContentDefinitionRegistry.Get(
                knowledge.Kind == KnowledgeContentKind.Technique
                    ? "book.technique"
                    : "book.spell");
            var source = new ContentSourceResultId(
                KnowledgePopulationRules.InitialRecordSourcePrefix
                + checked((ulong)index + 1UL));
            var created = contentStore.TryCreate(new CreateContentInstanceCommand(
                definition.Id,
                ContentOwnerRef.World,
                new ContentHolderRef(ContentHolderKind.WorldLocation, 0),
                checked((ushort)((locationOffset + index)
                    % WorldAtlasRules.ProductionLocationCount)),
                Quantity: 1,
                ContentQualityAvailability.Specified,
                EconomyRules.InitialSkillMaximumHundredths,
                ConditionHundredths: QualityValue.MaximumHundredths,
                source,
                Tick: 0));
            if (!created.Applied || created.InstanceId is not { } recordId)
            {
                throw new InvalidOperationException(
                    $"초기 물리 기록물 생성에 실패했습니다: {index} · {created.Status}");
            }

            var bound = store.TryBindPhysicalRecord(
                new BindKnowledgeRecordCommand(
                    recordId,
                    new KnowledgeEditionId(edition.EditionId),
                    (PhysicalRecordFormat)(index % 3),
                    source,
                    Tick: 0),
                contentStore);
            if (!bound.Applied)
            {
                throw new InvalidOperationException(
                    $"초기 물리 기록물 binding에 실패했습니다: {index} · {bound.Status}");
            }
        }

        return store;
    }

    public static KnowledgeAuthorityStore Restore(
        KnowledgeAuthorityCheckpoint checkpoint,
        int population,
        ContentAuthorityStore contentStore)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(contentStore);
        ValidateCheckpoint(checkpoint, population, contentStore);
        var store = new KnowledgeAuthorityStore(population)
        {
            _nextEditionId = checkpoint.NextEditionId,
            _nextProvenanceId = checkpoint.NextProvenanceId,
            _provenanceSequence = checkpoint.ProvenanceSequence,
        };
        store._contents.AddRange(checkpoint.Contents);
        store._editions.AddRange(checkpoint.Editions);
        store._recordBindings.AddRange(checkpoint.RecordBindings);
        store._access.AddRange(checkpoint.Access);
        store._provenance.AddRange(checkpoint.Provenance);
        store.RebuildIndexes();
        return store;
    }

    public KnowledgeCommandResult TryCreateEdition(CreateKnowledgeEditionCommand command)
    {
        if (!_contentIndexById.TryGetValue(command.ContentId.Value, out var contentIndex))
        {
            return new KnowledgeCommandResult(KnowledgeCommandStatus.InvalidContent);
        }
        if (!IsValidValue(command.ConfidenceAvailability, command.ConfidenceHundredths))
        {
            return new KnowledgeCommandResult(KnowledgeCommandStatus.InvalidConfidence);
        }

        KnowledgeEditionCheckpoint? parent = null;
        if (command.ParentEditionId is { } parentId)
        {
            if (!_editionIndexById.TryGetValue(parentId.Value, out var parentIndex))
            {
                return new KnowledgeCommandResult(KnowledgeCommandStatus.InvalidEdition);
            }
            parent = _editions[parentIndex];
        }
        if (command.Relation == KnowledgeEditionRelation.Original
                ? parent is not null
                : parent is null || parent.Value.ContentId != command.ContentId.Value)
        {
            return new KnowledgeCommandResult(KnowledgeCommandStatus.InvalidRelation);
        }
        if (command.ContributorAgent < -1 || command.ContributorAgent >= _population
            || command.Tick < 0)
        {
            return new KnowledgeCommandResult(KnowledgeCommandStatus.InvalidTransition);
        }
        var sourceStatus = ValidateNewSource(command.SourceResultId);
        if (sourceStatus != KnowledgeCommandStatus.Applied)
        {
            return new KnowledgeCommandResult(sourceStatus);
        }

        var editionId = new KnowledgeEditionId(_nextEditionId++);
        var provenanceId = AppendProvenance(
            command.Tick,
            command.SourceResultId,
            KnowledgeProvenanceKind.EditionCreated,
            command.ContentId,
            editionId,
            physicalRecord: null,
            accessor: null,
            parent?.LatestProvenanceId ?? _contents[contentIndex].LatestProvenanceId);
        var edition = new KnowledgeEditionCheckpoint(
            editionId.Value,
            command.ContentId.Value,
            command.ParentEditionId?.Value ?? 0,
            command.Relation,
            command.ContributorAgent,
            command.ConfidenceAvailability,
            command.ConfidenceHundredths,
            command.Tick,
            provenanceId.Value);
        _editionIndexById.Add(editionId.Value, _editions.Count);
        _editions.Add(edition);
        _contents[contentIndex] = _contents[contentIndex] with
        {
            LatestProvenanceId = provenanceId.Value,
        };
        return new KnowledgeCommandResult(
            KnowledgeCommandStatus.Applied,
            command.ContentId,
            editionId,
            provenanceId);
    }

    public KnowledgeCommandResult TryBindPhysicalRecord(
        BindKnowledgeRecordCommand command,
        ContentAuthorityStore contentStore)
    {
        ArgumentNullException.ThrowIfNull(contentStore);
        if (!_editionIndexById.TryGetValue(command.EditionId.Value, out var editionIndex))
        {
            return new KnowledgeCommandResult(KnowledgeCommandStatus.InvalidEdition);
        }
        if (!contentStore.TryGetInstance(command.PhysicalRecordInstanceId, out var record)
            || record.Kind != ContentInstanceKind.PhysicalRecord
            || record.Status is not (ContentInstanceStatus.Active or ContentInstanceStatus.Reserved))
        {
            return new KnowledgeCommandResult(KnowledgeCommandStatus.InvalidPhysicalRecord);
        }
        if (_bindingIndexByRecord.ContainsKey(command.PhysicalRecordInstanceId.Value))
        {
            return new KnowledgeCommandResult(KnowledgeCommandStatus.DuplicateBinding);
        }
        if (!Enum.IsDefined(command.Format) || command.Tick < 0)
        {
            return new KnowledgeCommandResult(KnowledgeCommandStatus.InvalidTransition);
        }
        var sourceStatus = ValidateNewSource(command.SourceResultId);
        if (sourceStatus != KnowledgeCommandStatus.Applied)
        {
            return new KnowledgeCommandResult(sourceStatus);
        }

        var edition = _editions[editionIndex];
        var contentId = new KnowledgeContentId(edition.ContentId);
        var provenanceId = AppendProvenance(
            command.Tick,
            command.SourceResultId,
            KnowledgeProvenanceKind.PhysicalRecordBound,
            contentId,
            command.EditionId,
            command.PhysicalRecordInstanceId,
            accessor: null,
            edition.LatestProvenanceId);
        var binding = new KnowledgeRecordBindingCheckpoint(
            command.PhysicalRecordInstanceId.Value,
            command.EditionId.Value,
            command.Format,
            command.Tick,
            provenanceId.Value);
        _bindingIndexByRecord.Add(binding.PhysicalRecordInstanceId, _recordBindings.Count);
        _recordBindings.Add(binding);
        Increment(_physicalRecordCountByEdition, command.EditionId.Value);
        return new KnowledgeCommandResult(
            KnowledgeCommandStatus.Applied,
            contentId,
            command.EditionId,
            provenanceId);
    }

    public KnowledgeCommandResult TryGrantAccess(GrantKnowledgeAccessCommand command)
    {
        if (!IsValidAccessor(command.Accessor))
        {
            return new KnowledgeCommandResult(KnowledgeCommandStatus.InvalidAccessor);
        }
        if (!_editionIndexById.TryGetValue(command.EditionId.Value, out var editionIndex))
        {
            return new KnowledgeCommandResult(KnowledgeCommandStatus.InvalidEdition);
        }
        if (command.AccessState == KnowledgeAccessState.None
            || !Enum.IsDefined(command.AccessState)
            || !Enum.IsDefined(command.ClaimState)
            || !IsValidValue(
                command.UnderstandingAvailability,
                command.UnderstandingHundredths)
            || !IsValidValue(command.ConfidenceAvailability, command.ConfidenceHundredths)
            || command.Tick < 0)
        {
            return new KnowledgeCommandResult(KnowledgeCommandStatus.InvalidTransition);
        }

        var edition = _editions[editionIndex];
        var contentId = new KnowledgeContentId(edition.ContentId);
        var key = (command.Accessor.Kind, command.Accessor.Id, contentId.Value);
        KnowledgeAccessCheckpoint? previous = null;
        if (_accessIndexByOwnerAndContent.TryGetValue(key, out var accessIndex))
        {
            previous = _access[accessIndex];
            if (command.AccessState < previous.Value.AccessState
                || !IsValidClaimTransition(previous.Value.ClaimState, command.ClaimState))
            {
                return new KnowledgeCommandResult(KnowledgeCommandStatus.InvalidTransition);
            }
        }
        var sourceStatus = ValidateNewSource(command.SourceResultId);
        if (sourceStatus != KnowledgeCommandStatus.Applied)
        {
            return new KnowledgeCommandResult(sourceStatus);
        }

        var provenanceId = AppendProvenance(
            command.Tick,
            command.SourceResultId,
            previous is { ClaimState: KnowledgeBeliefState.Misunderstood }
                && command.ClaimState == KnowledgeBeliefState.Corrected
                    ? KnowledgeProvenanceKind.ClaimCorrected
                    : KnowledgeProvenanceKind.AccessGranted,
            contentId,
            command.EditionId,
            physicalRecord: null,
            command.Accessor,
            previous?.SourceProvenanceId ?? edition.LatestProvenanceId);
        var current = new KnowledgeAccessCheckpoint(
            command.Accessor.Kind,
            command.Accessor.Id,
            contentId.Value,
            command.EditionId.Value,
            command.AccessState,
            command.ClaimState,
            command.UnderstandingAvailability,
            command.UnderstandingHundredths,
            command.ConfidenceAvailability,
            command.ConfidenceHundredths,
            command.Tick,
            provenanceId.Value,
            checked((previous?.Revision ?? 0) + 1));
        if (previous is null)
        {
            _accessIndexByOwnerAndContent.Add(key, _access.Count);
            _access.Add(current);
            Increment(_accessCountByEdition, command.EditionId.Value);
        }
        else
        {
            if (previous.Value.EditionId != command.EditionId.Value)
            {
                Decrement(_accessCountByEdition, previous.Value.EditionId);
                Increment(_accessCountByEdition, command.EditionId.Value);
            }
            _access[accessIndex] = current;
        }
        return new KnowledgeCommandResult(
            KnowledgeCommandStatus.Applied,
            contentId,
            command.EditionId,
            provenanceId);
    }

    public bool TrySelectUnaccessedEdition(
        KnowledgeAccessorRef accessor,
        KnowledgeContentKind kind,
        ulong selector,
        out KnowledgeEditionId editionId)
    {
        if (!IsValidAccessor(accessor) || !Enum.IsDefined(kind) || _editions.Count == 0)
        {
            editionId = default;
            return false;
        }

        var start = checked((int)(selector % (ulong)_editions.Count));
        for (var offset = 0; offset < _editions.Count; offset++)
        {
            var edition = _editions[(start + offset) % _editions.Count];
            if (_contents[_contentIndexById[edition.ContentId]].Kind == kind
                && !_accessIndexByOwnerAndContent.ContainsKey(
                    (accessor.Kind, accessor.Id, edition.ContentId)))
            {
                editionId = new KnowledgeEditionId(edition.EditionId);
                return true;
            }
        }

        editionId = default;
        return false;
    }

    public WorldKnowledgeSnapshot CreateSnapshot(int editionOffset = 0, int editionLimit = 100)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(editionOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(editionLimit);
        var rows = editionLimit == 0
            ? Array.Empty<KnowledgeEditionSnapshot>()
            : _editions
                .Skip(editionOffset)
                .Take(editionLimit)
                .Select(CreateEditionSnapshot)
                .ToArray();
        return new WorldKnowledgeSnapshot(
            AuthorityRevision,
            _contents.Count,
            _editions.Count,
            _recordBindings.Count,
            _access.Count,
            _access.Count(value => value.ClaimState == KnowledgeBeliefState.Misunderstood),
            _editions.Count(value => value.Relation == KnowledgeEditionRelation.Restored),
            rows,
            Math.Min(editionOffset, _editions.Count),
            _editions.Count);
    }

    public KnowledgeAuthorityCheckpoint CreateCheckpoint() => new(
        CurrentSchemaRevision,
        _nextEditionId,
        _nextProvenanceId,
        _provenanceSequence,
        _contents.ToArray(),
        _editions.ToArray(),
        _recordBindings.ToArray(),
        _access.ToArray(),
        _provenance.ToArray());

    public ulong ComputeStateHash()
    {
        const ulong offset = 14695981039346656037UL;
        var hash = offset;
        Add(ref hash, checked((ulong)_population));
        Add(ref hash, checked((ulong)_nextEditionId));
        Add(ref hash, checked((ulong)_nextProvenanceId));
        Add(ref hash, checked((ulong)_provenanceSequence));
        foreach (var value in _contents)
        {
            Add(ref hash, value.ContentId);
            Add(ref hash, checked((ulong)value.Revision));
            Add(ref hash, (byte)value.Kind);
            Add(ref hash, checked((ulong)value.CatalogOrdinal));
            Add(ref hash, unchecked((ulong)value.OriginatorAgent));
            Add(ref hash, checked((ulong)value.CreatedTick));
            Add(ref hash, checked((ulong)value.LatestProvenanceId));
        }
        foreach (var value in _editions)
        {
            Add(ref hash, checked((ulong)value.EditionId));
            Add(ref hash, value.ContentId);
            Add(ref hash, checked((ulong)value.ParentEditionId));
            Add(ref hash, (byte)value.Relation);
            Add(ref hash, unchecked((ulong)value.ContributorAgent));
            Add(ref hash, (byte)value.ConfidenceAvailability);
            Add(ref hash, value.ConfidenceHundredths);
            Add(ref hash, checked((ulong)value.CreatedTick));
            Add(ref hash, checked((ulong)value.LatestProvenanceId));
        }
        foreach (var value in _recordBindings)
        {
            Add(ref hash, checked((ulong)value.PhysicalRecordInstanceId));
            Add(ref hash, checked((ulong)value.EditionId));
            Add(ref hash, (byte)value.Format);
            Add(ref hash, checked((ulong)value.BoundTick));
            Add(ref hash, checked((ulong)value.SourceProvenanceId));
        }
        foreach (var value in _access)
        {
            Add(ref hash, (byte)value.AccessorKind);
            Add(ref hash, checked((ulong)value.AccessorId));
            Add(ref hash, value.ContentId);
            Add(ref hash, checked((ulong)value.EditionId));
            Add(ref hash, (byte)value.AccessState);
            Add(ref hash, (byte)value.ClaimState);
            Add(ref hash, (byte)value.UnderstandingAvailability);
            Add(ref hash, value.UnderstandingHundredths);
            Add(ref hash, (byte)value.ConfidenceAvailability);
            Add(ref hash, value.ConfidenceHundredths);
            Add(ref hash, checked((ulong)value.UpdatedTick));
            Add(ref hash, checked((ulong)value.SourceProvenanceId));
            Add(ref hash, checked((ulong)value.Revision));
        }
        foreach (var value in _provenance)
        {
            Add(ref hash, checked((ulong)value.ProvenanceId));
            Add(ref hash, checked((ulong)value.Sequence));
            Add(ref hash, checked((ulong)value.Tick));
            Add(ref hash, value.SourceResultId);
            Add(ref hash, (byte)value.Kind);
            Add(ref hash, value.ContentId);
            Add(ref hash, checked((ulong)value.EditionId));
            Add(ref hash, checked((ulong)value.PhysicalRecordInstanceId));
            Add(ref hash, (byte)value.AccessorKind);
            Add(ref hash, unchecked((ulong)value.AccessorId));
            Add(ref hash, checked((ulong)value.ParentProvenanceId));
        }
        return hash;
    }

    public static void ValidateCheckpoint(
        KnowledgeAuthorityCheckpoint checkpoint,
        int population,
        ContentAuthorityStore contentStore)
    {
        ArgumentNullException.ThrowIfNull(contentStore);
        ValidateCheckpointCore(checkpoint, population, instanceId =>
            contentStore.TryGetInstance(new ContentInstanceId(instanceId), out var record)
            && record.Kind == ContentInstanceKind.PhysicalRecord);
    }

    public static void ValidateCheckpoint(
        KnowledgeAuthorityCheckpoint checkpoint,
        int population,
        ContentAuthorityCheckpoint contentCheckpoint)
    {
        ArgumentNullException.ThrowIfNull(contentCheckpoint);
        var physicalRecords = contentCheckpoint.Instances
            .Where(value => value.Kind == ContentInstanceKind.PhysicalRecord)
            .Select(value => value.InstanceId)
            .ToHashSet();
        ValidateCheckpointCore(checkpoint, population, physicalRecords.Contains);
    }

    private static void ValidateCheckpointCore(
        KnowledgeAuthorityCheckpoint checkpoint,
        int population,
        Func<long, bool> isPhysicalRecord)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(population);
        if (checkpoint.SchemaRevision != CurrentSchemaRevision
            || checkpoint.Contents is null
            || checkpoint.Editions is null
            || checkpoint.RecordBindings is null
            || checkpoint.Access is null
            || checkpoint.Provenance is null
            || checkpoint.NextEditionId <= 0
            || checkpoint.NextProvenanceId <= 0
            || checkpoint.ProvenanceSequence < 0)
        {
            throw new InvalidDataException("지식 권위 checkpoint header가 올바르지 않습니다.");
        }

        var contents = new Dictionary<ulong, KnowledgeContentCheckpoint>();
        foreach (var value in checkpoint.Contents)
        {
            if (value.ContentId == 0
                || value.Revision <= 0
                || !Enum.IsDefined(value.Kind)
                || value.CatalogOrdinal <= 0
                || value.OriginatorAgent < -1
                || value.OriginatorAgent >= population
                || value.CreatedTick < 0
                || value.LatestProvenanceId <= 0
                || !contents.TryAdd(value.ContentId, value))
            {
                throw new InvalidDataException("지식 Content Identity가 올바르지 않습니다.");
            }
        }

        var editions = new Dictionary<long, KnowledgeEditionCheckpoint>();
        foreach (var value in checkpoint.Editions)
        {
            if (value.EditionId <= 0
                || !contents.ContainsKey(value.ContentId)
                || !Enum.IsDefined(value.Relation)
                || value.ContributorAgent < -1
                || value.ContributorAgent >= population
                || !IsValidValue(value.ConfidenceAvailability, value.ConfidenceHundredths)
                || value.CreatedTick < 0
                || value.LatestProvenanceId <= 0
                || !editions.TryAdd(value.EditionId, value))
            {
                throw new InvalidDataException("지식 Edition이 올바르지 않습니다.");
            }
            if (value.Relation == KnowledgeEditionRelation.Original
                    ? value.ParentEditionId != 0
                    : value.ParentEditionId <= 0
                        || !editions.TryGetValue(value.ParentEditionId, out var parent)
                        || parent.ContentId != value.ContentId)
            {
                throw new InvalidDataException("지식 Edition 계보가 올바르지 않습니다.");
            }
        }
        var expectedNextEdition = editions.Count == 0 ? 1 : editions.Keys.Max() + 1;
        if (checkpoint.NextEditionId != expectedNextEdition)
        {
            throw new InvalidDataException("지식 Edition sequence가 올바르지 않습니다.");
        }

        var bindings = new HashSet<long>();
        foreach (var value in checkpoint.RecordBindings)
        {
            if (value.PhysicalRecordInstanceId <= 0
                || !editions.ContainsKey(value.EditionId)
                || !Enum.IsDefined(value.Format)
                || value.BoundTick < 0
                || value.SourceProvenanceId <= 0
                || !bindings.Add(value.PhysicalRecordInstanceId)
                || !isPhysicalRecord(value.PhysicalRecordInstanceId))
            {
                throw new InvalidDataException("지식 물리 기록물 binding이 올바르지 않습니다.");
            }
        }

        var accessKeys = new HashSet<(KnowledgeAccessorKind, long, ulong)>();
        foreach (var value in checkpoint.Access)
        {
            var accessor = new KnowledgeAccessorRef(value.AccessorKind, value.AccessorId);
            if (!IsValidAccessor(accessor, population)
                || !contents.ContainsKey(value.ContentId)
                || !editions.TryGetValue(value.EditionId, out var edition)
                || edition.ContentId != value.ContentId
                || value.AccessState == KnowledgeAccessState.None
                || !Enum.IsDefined(value.AccessState)
                || !Enum.IsDefined(value.ClaimState)
                || !IsValidValue(
                    value.UnderstandingAvailability,
                    value.UnderstandingHundredths)
                || !IsValidValue(value.ConfidenceAvailability, value.ConfidenceHundredths)
                || value.UpdatedTick < 0
                || value.SourceProvenanceId <= 0
                || value.Revision <= 0
                || !accessKeys.Add((value.AccessorKind, value.AccessorId, value.ContentId)))
            {
                throw new InvalidDataException("지식 접근 상태가 올바르지 않습니다.");
            }
        }

        var provenanceIds = new HashSet<long>();
        var sourceIds = new HashSet<ulong>();
        long previousSequence = 0;
        foreach (var value in checkpoint.Provenance)
        {
            if (value.ProvenanceId <= 0
                || value.Sequence != previousSequence + 1
                || value.Tick < 0
                || value.SourceResultId == 0
                || !Enum.IsDefined(value.Kind)
                || !contents.ContainsKey(value.ContentId)
                || (value.EditionId != 0 && !editions.ContainsKey(value.EditionId))
                || value.ParentProvenanceId < 0
                || (value.ParentProvenanceId > 0
                    && !provenanceIds.Contains(value.ParentProvenanceId))
                || !provenanceIds.Add(value.ProvenanceId)
                || !sourceIds.Add(value.SourceResultId))
            {
                throw new InvalidDataException("지식 provenance가 올바르지 않습니다.");
            }
            previousSequence = value.Sequence;
        }
        if (checkpoint.ProvenanceSequence != previousSequence
            || checkpoint.NextProvenanceId != previousSequence + 1
            || checkpoint.Contents.Any(value => !provenanceIds.Contains(value.LatestProvenanceId))
            || checkpoint.Editions.Any(value => !provenanceIds.Contains(value.LatestProvenanceId))
            || checkpoint.RecordBindings.Any(value =>
                !provenanceIds.Contains(value.SourceProvenanceId))
            || checkpoint.Access.Any(value => !provenanceIds.Contains(value.SourceProvenanceId)))
        {
            throw new InvalidDataException("지식 provenance 참조가 올바르지 않습니다.");
        }
    }

    private void AddInitialContent(
        KnowledgeContentId contentId,
        KnowledgeContentKind kind,
        int catalogOrdinal,
        ContentSourceResultId source)
    {
        var sourceStatus = ValidateNewSource(source);
        if (sourceStatus != KnowledgeCommandStatus.Applied)
        {
            throw new InvalidOperationException("초기 지식 Content source가 중복되었습니다.");
        }
        var provenanceId = AppendProvenance(
            tick: 0,
            source,
            KnowledgeProvenanceKind.ContentAuthored,
            contentId,
            edition: null,
            physicalRecord: null,
            accessor: null,
            parentProvenanceId: 0);
        var value = new KnowledgeContentCheckpoint(
            contentId.Value,
            Revision: 1,
            kind,
            catalogOrdinal,
            OriginatorAgent: -1,
            CreatedTick: 0,
            provenanceId.Value);
        _contentIndexById.Add(contentId.Value, _contents.Count);
        _contents.Add(value);
    }

    private KnowledgeEditionSnapshot CreateEditionSnapshot(KnowledgeEditionCheckpoint edition)
    {
        var content = _contents[_contentIndexById[edition.ContentId]];
        return new KnowledgeEditionSnapshot(
            new KnowledgeEditionId(edition.EditionId),
            new KnowledgeContentId(edition.ContentId),
            content.Kind,
            content.CatalogOrdinal,
            edition.Relation,
            edition.ParentEditionId == 0
                ? null
                : new KnowledgeEditionId(edition.ParentEditionId),
            edition.ConfidenceAvailability,
            edition.ConfidenceHundredths,
            _physicalRecordCountByEdition.GetValueOrDefault(edition.EditionId),
            _accessCountByEdition.GetValueOrDefault(edition.EditionId));
    }

    private KnowledgeCommandStatus ValidateNewSource(ContentSourceResultId source)
    {
        if (source.Value == 0)
        {
            return KnowledgeCommandStatus.InvalidSource;
        }
        return _appliedSourceResults.Add(source.Value)
            ? KnowledgeCommandStatus.Applied
            : KnowledgeCommandStatus.DuplicateSource;
    }

    private KnowledgeProvenanceId AppendProvenance(
        long tick,
        ContentSourceResultId source,
        KnowledgeProvenanceKind kind,
        KnowledgeContentId content,
        KnowledgeEditionId? edition,
        ContentInstanceId? physicalRecord,
        KnowledgeAccessorRef? accessor,
        long parentProvenanceId)
    {
        var id = new KnowledgeProvenanceId(_nextProvenanceId++);
        _provenance.Add(new KnowledgeProvenanceCheckpoint(
            id.Value,
            ++_provenanceSequence,
            tick,
            source.Value,
            kind,
            content.Value,
            edition?.Value ?? 0,
            physicalRecord?.Value ?? 0,
            accessor?.Kind ?? KnowledgeAccessorKind.Agent,
            accessor?.Id ?? -1,
            parentProvenanceId));
        return id;
    }

    private void RebuildIndexes()
    {
        for (var index = 0; index < _contents.Count; index++)
        {
            _contentIndexById.Add(_contents[index].ContentId, index);
        }
        for (var index = 0; index < _editions.Count; index++)
        {
            _editionIndexById.Add(_editions[index].EditionId, index);
        }
        for (var index = 0; index < _recordBindings.Count; index++)
        {
            var value = _recordBindings[index];
            _bindingIndexByRecord.Add(value.PhysicalRecordInstanceId, index);
            Increment(_physicalRecordCountByEdition, value.EditionId);
        }
        for (var index = 0; index < _access.Count; index++)
        {
            var value = _access[index];
            _accessIndexByOwnerAndContent.Add(
                (value.AccessorKind, value.AccessorId, value.ContentId),
                index);
            Increment(_accessCountByEdition, value.EditionId);
        }
        foreach (var value in _provenance)
        {
            _appliedSourceResults.Add(value.SourceResultId);
        }
    }

    private bool IsValidAccessor(KnowledgeAccessorRef accessor) =>
        IsValidAccessor(accessor, _population);

    private static bool IsValidAccessor(KnowledgeAccessorRef accessor, int population) =>
        accessor.IsValid
        && (accessor.Kind != KnowledgeAccessorKind.Agent || accessor.Id < population);

    private static bool IsValidValue(
        KnowledgeValueAvailability availability,
        ushort hundredths) => availability switch
    {
        KnowledgeValueAvailability.DataUnavailable => hundredths == 0,
        KnowledgeValueAvailability.Specified => hundredths <= QualityValue.MaximumHundredths,
        _ => false,
    };

    private static bool IsValidClaimTransition(
        KnowledgeBeliefState previous,
        KnowledgeBeliefState next) => previous switch
    {
        KnowledgeBeliefState.Unknown => true,
        KnowledgeBeliefState.Incomplete => next is KnowledgeBeliefState.Incomplete
            or KnowledgeBeliefState.Correct
            or KnowledgeBeliefState.Corrected,
        KnowledgeBeliefState.Misunderstood => next is KnowledgeBeliefState.Misunderstood
            or KnowledgeBeliefState.Corrected,
        KnowledgeBeliefState.Correct => next == KnowledgeBeliefState.Correct,
        KnowledgeBeliefState.Corrected => next == KnowledgeBeliefState.Corrected,
        _ => false,
    };

    private static void Increment(Dictionary<long, int> values, long key) =>
        values[key] = checked(values.GetValueOrDefault(key) + 1);

    private static void Decrement(Dictionary<long, int> values, long key)
    {
        var next = checked(values[key] - 1);
        if (next == 0)
        {
            values.Remove(key);
        }
        else
        {
            values[key] = next;
        }
    }

    private static void Add(ref ulong hash, ulong value)
    {
        hash ^= value;
        hash *= 1099511628211UL;
    }
}
