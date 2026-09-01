using Chronicles.Core;

namespace Chronicles.Simulation;

internal static class ContentAuthorityCheckpointValidator
{
    public static void Validate(ContentAuthorityCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ValidateHeader(checkpoint);

        var instanceIds = new HashSet<long>();
        var instanceKinds = new Dictionary<long, ContentInstanceKind>();
        var latestByInstance = new Dictionary<long, long>();
        long maximumInstanceId = 0;
        foreach (var instance in checkpoint.Instances)
        {
            if (instance.InstanceId <= 0 || !instanceIds.Add(instance.InstanceId))
            {
                throw new InvalidDataException("콘텐츠 Instance ID가 중복되거나 잘못되었습니다.");
            }
            maximumInstanceId = Math.Max(maximumInstanceId, instance.InstanceId);
            ValidateInstance(instance);
            instanceKinds.Add(instance.InstanceId, instance.Kind);
            latestByInstance.Add(instance.InstanceId, instance.LatestProvenanceId);
        }
        if (checkpoint.NextInstanceId <= maximumInstanceId)
        {
            throw new InvalidDataException("콘텐츠 다음 Instance ID가 과거를 가리킵니다.");
        }

        var sourceIds = new HashSet<ulong>();
        var provenanceInstance = ValidateProvenance(
            checkpoint,
            instanceIds,
            sourceIds,
            out var maximumProvenanceId,
            out var finalSequence);
        if (checkpoint.NextProvenanceId <= maximumProvenanceId
            || checkpoint.ProvenanceSequence != finalSequence)
        {
            throw new InvalidDataException("콘텐츠 provenance sequence가 잘못되었습니다.");
        }
        foreach (var (instanceId, latest) in latestByInstance)
        {
            if (!provenanceInstance.TryGetValue(latest, out var provenanceSubject)
                || provenanceSubject != instanceId)
            {
                throw new InvalidDataException("콘텐츠 최신 provenance 참조가 고아입니다.");
            }
        }

        ValidateDiscoveries(checkpoint, instanceKinds, provenanceInstance);
    }

    private static void ValidateHeader(ContentAuthorityCheckpoint checkpoint)
    {
        if (checkpoint.SchemaRevision != ContentAuthorityStore.CurrentSchemaRevision
            || checkpoint.NextInstanceId <= 0
            || checkpoint.NextProvenanceId <= 0
            || checkpoint.ProvenanceSequence < 0
            || checkpoint.Instances is null
            || checkpoint.Provenance is null
            || checkpoint.Discoveries is null)
        {
            throw new InvalidDataException("콘텐츠 checkpoint header가 올바르지 않습니다.");
        }
    }

    private static void ValidateInstance(ContentInstanceCheckpoint instance)
    {
        var owner = new ContentOwnerRef(instance.OwnerKind, instance.OwnerId);
        var holder = new ContentHolderRef(instance.HolderKind, instance.HolderId);
        if (instance.DefinitionId == 0
            || !ContentDefinitionRegistry.TryGet(
                new ContentDefinitionId(instance.DefinitionId),
                out var definition)
            || instance.DefinitionRevision != definition.Revision
            || instance.Kind != definition.InstanceKind
            || !owner.IsValid
            || !holder.IsValid
            || !IsHolderCompatible(owner, holder)
            || !WorldAtlasRules.IsProductionLocation(instance.LocationId)
            || instance.AuthorityRevision <= 0
            || instance.LatestProvenanceId <= 0
            || !IsValidPersistedQuantity(instance)
            || !IsValidQuality(instance))
        {
            throw new InvalidDataException($"콘텐츠 Instance가 잘못되었습니다: {instance.InstanceId}");
        }
    }

    private static Dictionary<long, long> ValidateProvenance(
        ContentAuthorityCheckpoint checkpoint,
        HashSet<long> instanceIds,
        HashSet<ulong> sourceIds,
        out long maximumProvenanceId,
        out long finalSequence)
    {
        var provenanceIds = new HashSet<long>();
        var provenanceInstance = new Dictionary<long, long>();
        maximumProvenanceId = 0;
        finalSequence = 0;
        foreach (var provenance in checkpoint.Provenance)
        {
            if (provenance.ProvenanceId <= 0
                || !provenanceIds.Add(provenance.ProvenanceId)
                || provenance.Sequence != finalSequence + 1
                || provenance.Tick < 0
                || provenance.SourceResultId == 0
                || !sourceIds.Add(provenance.SourceResultId)
                || !instanceIds.Contains(provenance.InstanceId)
                || provenance.Quantity < 0
                || !new ContentOwnerRef(
                    provenance.PreviousOwnerKind,
                    provenance.PreviousOwnerId).IsValid
                || !new ContentOwnerRef(
                    provenance.CurrentOwnerKind,
                    provenance.CurrentOwnerId).IsValid
                || !HasValidParent(provenance, provenanceInstance))
            {
                throw new InvalidDataException("콘텐츠 provenance 계보가 잘못되었습니다.");
            }
            finalSequence = provenance.Sequence;
            maximumProvenanceId = Math.Max(maximumProvenanceId, provenance.ProvenanceId);
            provenanceInstance.Add(provenance.ProvenanceId, provenance.InstanceId);
        }
        return provenanceInstance;
    }

    private static bool HasValidParent(
        ContentProvenanceCheckpoint provenance,
        Dictionary<long, long> provenanceInstance) =>
        provenance.ParentProvenanceId == 0
        || (provenance.ParentProvenanceId < provenance.ProvenanceId
            && provenanceInstance.TryGetValue(
                provenance.ParentProvenanceId,
                out var parentSubject)
            && parentSubject == provenance.InstanceId);

    private static void ValidateDiscoveries(
        ContentAuthorityCheckpoint checkpoint,
        Dictionary<long, ContentInstanceKind> instanceKinds,
        Dictionary<long, long> provenanceInstance)
    {
        var discoveryKeys = new HashSet<(ContentDiscovererKind, long, long)>();
        foreach (var discovery in checkpoint.Discoveries)
        {
            var discoverer = new ContentDiscovererRef(
                discovery.DiscovererKind,
                discovery.DiscovererId);
            var key = (
                discovery.DiscovererKind,
                discovery.DiscovererId,
                discovery.SubjectInstanceId);
            if (!discoverer.IsValid
                || !discoveryKeys.Add(key)
                || discovery.State == ContentDiscoveryState.Unknown
                || !Enum.IsDefined(discovery.State)
                || discovery.Revision <= 0
                || discovery.UpdatedTick < 0
                || !provenanceInstance.TryGetValue(
                    discovery.SourceProvenanceId,
                    out var provenanceSubject)
                || provenanceSubject != discovery.SubjectInstanceId
                || !instanceKinds.TryGetValue(
                    discovery.SubjectInstanceId,
                    out var instanceKind)
                || instanceKind != ContentInstanceKind.SecretRealm)
            {
                throw new InvalidDataException("콘텐츠 발견 상태가 잘못되었습니다.");
            }
        }
    }

    private static bool IsValidPersistedQuantity(ContentInstanceCheckpoint instance) =>
        instance.Kind switch
        {
            ContentInstanceKind.SecretRealm => instance.Quantity == 1,
            ContentInstanceKind.MaterialStack => instance.Status is ContentInstanceStatus.Active
                or ContentInstanceStatus.Reserved
                    ? instance.Quantity > 0
                    : instance.Quantity == 0,
            _ => instance.Status is ContentInstanceStatus.Active or ContentInstanceStatus.Reserved
                ? instance.Quantity == 1
                : instance.Quantity == 0,
        };

    private static bool IsValidQuality(ContentInstanceCheckpoint instance) =>
        instance.ConditionHundredths <= 10_000
        && instance.QualityAvailability switch
        {
            ContentQualityAvailability.DataUnavailable => instance.QualityHundredths == 0,
            ContentQualityAvailability.Specified => instance.QualityHundredths <= 10_000,
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
