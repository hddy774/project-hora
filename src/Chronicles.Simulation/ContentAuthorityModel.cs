using Chronicles.Core;

namespace Chronicles.Simulation;

public enum ContentDefinitionKind : byte
{
    SecretRealm,
    Material,
    Item,
    Book,
    CreatedWork,
}

public enum SecretRealmNature : byte
{
    None,
    Natural,
    Artificial,
}

public enum ContentInstanceKind : byte
{
    SecretRealm,
    MaterialStack,
    Item,
    PhysicalRecord,
    CreatedWork,
}

public enum ContentInstanceStatus : byte
{
    Active,
    Reserved,
    Consumed,
    Destroyed,
    Collapsed,
}

public enum ContentQualityAvailability : byte
{
    DataUnavailable,
    Specified,
}

public enum ContentOwnerKind : byte
{
    World,
    Agent,
    City,
    Organization,
}

public readonly record struct ContentOwnerRef(ContentOwnerKind Kind, long Id)
{
    public static ContentOwnerRef World => new(ContentOwnerKind.World, 0);

    public bool IsValid => Kind switch
    {
        ContentOwnerKind.World => Id == 0,
        ContentOwnerKind.Agent or ContentOwnerKind.City => Id >= 0,
        ContentOwnerKind.Organization => Id > 0,
        _ => false,
    };
}

public enum ContentHolderKind : byte
{
    WorldLocation,
    PersonalInventory,
    CityWarehouse,
    OrganizationWarehouse,
    SecretRealmInterior,
}

public readonly record struct ContentHolderRef(ContentHolderKind Kind, long Id)
{
    public bool IsValid => Kind switch
    {
        ContentHolderKind.WorldLocation => Id == 0,
        ContentHolderKind.PersonalInventory or ContentHolderKind.CityWarehouse => Id >= 0,
        ContentHolderKind.OrganizationWarehouse or ContentHolderKind.SecretRealmInterior => Id > 0,
        _ => false,
    };
}

public enum ContentDiscovererKind : byte
{
    Observer,
    Agent,
    Organization,
}

public readonly record struct ContentDiscovererRef(ContentDiscovererKind Kind, long Id)
{
    public static ContentDiscovererRef Observer => new(ContentDiscovererKind.Observer, 0);

    public bool IsValid => Kind switch
    {
        ContentDiscovererKind.Observer => Id == 0,
        ContentDiscovererKind.Agent => Id >= 0,
        ContentDiscovererKind.Organization => Id > 0,
        _ => false,
    };
}

public enum ContentDiscoveryState : byte
{
    Unknown,
    Aware,
    Located,
    AccessGranted,
}

public enum ContentProvenanceKind : byte
{
    Created,
    Discovered,
    AccessGranted,
    Transferred,
    Reserved,
    ReservationReleased,
    Consumed,
    Destroyed,
    Collapsed,
}

public enum ContentCommandStatus : byte
{
    Applied,
    InvalidDefinition,
    InvalidInstance,
    InvalidOwner,
    InvalidHolder,
    InvalidLocation,
    InvalidQuantity,
    InvalidQuality,
    InvalidTransition,
    OwnerMismatch,
    InvalidSource,
    DuplicateSource,
    AlreadyKnown,
    AccessBlocked,
}

public readonly record struct ContentDefinition(
    ContentDefinitionId Id,
    string StableRef,
    int Revision,
    string KoreanName,
    ContentDefinitionKind Kind,
    ContentInstanceKind InstanceKind,
    SecretRealmNature SecretRealmNature,
    bool Stackable);

public readonly record struct ContentInstanceCheckpoint(
    long InstanceId,
    ulong DefinitionId,
    int DefinitionRevision,
    ContentInstanceKind Kind,
    ContentOwnerKind OwnerKind,
    long OwnerId,
    ContentHolderKind HolderKind,
    long HolderId,
    ushort LocationId,
    long Quantity,
    ContentQualityAvailability QualityAvailability,
    ushort QualityHundredths,
    ushort ConditionHundredths,
    ContentInstanceStatus Status,
    long AuthorityRevision,
    long LatestProvenanceId);

public readonly record struct ContentProvenanceCheckpoint(
    long ProvenanceId,
    long Sequence,
    long Tick,
    ulong SourceResultId,
    ContentProvenanceKind Kind,
    long InstanceId,
    ContentOwnerKind PreviousOwnerKind,
    long PreviousOwnerId,
    ContentOwnerKind CurrentOwnerKind,
    long CurrentOwnerId,
    long Quantity,
    long ParentProvenanceId);

public readonly record struct ContentDiscoveryCheckpoint(
    ContentDiscovererKind DiscovererKind,
    long DiscovererId,
    long SubjectInstanceId,
    ContentDiscoveryState State,
    long SourceProvenanceId,
    int Revision,
    long UpdatedTick);

public sealed record ContentAuthorityCheckpoint(
    int SchemaRevision,
    long NextInstanceId,
    long NextProvenanceId,
    long ProvenanceSequence,
    ContentInstanceCheckpoint[] Instances,
    ContentProvenanceCheckpoint[] Provenance,
    ContentDiscoveryCheckpoint[] Discoveries);

public readonly record struct CreateContentInstanceCommand(
    ContentDefinitionId DefinitionId,
    ContentOwnerRef Owner,
    ContentHolderRef Holder,
    ushort LocationId,
    long Quantity,
    ContentQualityAvailability QualityAvailability,
    ushort QualityHundredths,
    ushort ConditionHundredths,
    ContentSourceResultId SourceResultId,
    long Tick);

public readonly record struct TransferContentCommand(
    ContentInstanceId InstanceId,
    ContentOwnerRef ExpectedOwner,
    ContentOwnerRef NewOwner,
    ContentHolderRef NewHolder,
    ushort NewLocationId,
    ContentSourceResultId SourceResultId,
    long Tick);

public readonly record struct ConsumeContentCommand(
    ContentInstanceId InstanceId,
    ContentOwnerRef ExpectedOwner,
    long Quantity,
    ContentSourceResultId SourceResultId,
    long Tick);

public readonly record struct DiscoverContentCommand(
    ContentInstanceId SubjectId,
    ContentDiscovererRef Discoverer,
    ContentDiscoveryState State,
    ContentSourceResultId SourceResultId,
    long Tick);

public readonly record struct ChangeContentStatusCommand(
    ContentInstanceId InstanceId,
    ContentOwnerRef ExpectedOwner,
    ContentSourceResultId SourceResultId,
    long Tick);

public readonly record struct ContentCommandResult(
    ContentCommandStatus Status,
    ContentInstanceId? InstanceId = null,
    ContentProvenanceId? ProvenanceId = null,
    long Quantity = 0)
{
    public bool Applied => Status == ContentCommandStatus.Applied;
}

public sealed record ContentInstanceSnapshot(
    ContentInstanceId Id,
    ContentDefinitionId DefinitionId,
    string DefinitionName,
    ContentInstanceKind Kind,
    SecretRealmNature SecretRealmNature,
    ContentOwnerRef Owner,
    ContentHolderRef Holder,
    ushort LocationId,
    long Quantity,
    ContentQualityAvailability QualityAvailability,
    ushort QualityHundredths,
    ushort ConditionHundredths,
    ContentInstanceStatus Status,
    ContentDiscoveryState ViewerDiscovery,
    long AuthorityRevision,
    ContentProvenanceId LatestProvenanceId);

public sealed record WorldContentSnapshot(
    long AuthorityRevision,
    int DefinitionCount,
    int TotalInstances,
    int ActiveInstances,
    int NaturalSecretRealms,
    int ArtificialSecretRealms,
    int PubliclyLocatedSecretRealms,
    int ViewerKnownSecretRealms,
    int MaterialStackCount,
    int ItemInstanceCount,
    int CreatedWorkCount,
    int PhysicalRecordCount,
    IReadOnlyList<ContentInstanceSnapshot> SecretRealms,
    int SecretRealmResultOffset,
    int SecretRealmResultTotal,
    IReadOnlyList<ContentInstanceSnapshot> ItemsAndMaterials,
    int ItemResultOffset,
    int ItemResultTotal,
    bool IncludesHidden,
    WorldKnowledgeSnapshot? Knowledge = null,
    WorldLawSnapshot? Laws = null)
{
    public static WorldContentSnapshot Empty { get; } = new(
        AuthorityRevision: 0,
        DefinitionCount: 0,
        TotalInstances: 0,
        ActiveInstances: 0,
        NaturalSecretRealms: 0,
        ArtificialSecretRealms: 0,
        PubliclyLocatedSecretRealms: 0,
        ViewerKnownSecretRealms: 0,
        MaterialStackCount: 0,
        ItemInstanceCount: 0,
        CreatedWorkCount: 0,
        PhysicalRecordCount: 0,
        SecretRealms: Array.Empty<ContentInstanceSnapshot>(),
        SecretRealmResultOffset: 0,
        SecretRealmResultTotal: 0,
        ItemsAndMaterials: Array.Empty<ContentInstanceSnapshot>(),
        ItemResultOffset: 0,
        ItemResultTotal: 0,
        IncludesHidden: false,
        Knowledge: WorldKnowledgeSnapshot.Empty,
        Laws: WorldLawSnapshot.Empty);
}

public static class ContentPopulationRules
{
    public const int InitialNaturalSecretRealmCount = 240;
    public const int InitialArtificialSecretRealmCount = 60;
    public const int InitialSecretRealmCount =
        InitialNaturalSecretRealmCount + InitialArtificialSecretRealmCount;
    public const int ApprovedItemDefinitionCount = 120;
    public const int ApprovedMaterialDefinitionCount = 90;
    public const int ApprovedIndependentInheritanceCount = 30;
    public const int ApprovedBookContentCount = 60;
    public const int ApprovedEditionCount = 90;
    public const int ApprovedPhysicalBookCount = 600;
    public const int ApprovedMaterialStackCount = 9_000;
    public const int ApprovedItemInstanceCount = 2_400;
    public const int InitialAuthorityInstanceCount =
        InitialSecretRealmCount
        + ApprovedMaterialStackCount
        + ApprovedItemInstanceCount
        + ApprovedIndependentInheritanceCount;

    public const string PlacementReviewId = "PC-TEMP-CONTENT-PLACEMENT-021";
    public const string PcDefinitionAdaptationId = "PC-TEMP-CONTENT-PC-DEFINITIONS-021";
}

public static class ContentDiscoveryRules
{
    public const int TravelDiscoveryChanceBasisPoints = 500;
    public const int HuntDiscoveryChanceBasisPoints = 1_000;
    public const ulong SourceResultPrefix = 0xD021_0000_0000_0000UL;
    public const string ChanceReviewId = "PC-TEMP-CONTENT-DISCOVERY-CHANCE-021";

    public static ulong CreateDeterministicRoll(
        ulong worldSeed,
        long tick,
        int agentIndex,
        ushort locationId,
        byte trigger)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        ArgumentOutOfRangeException.ThrowIfNegative(agentIndex);

        // SplitMix 계열 혼합은 64비트 래핑 산술 자체가 공식의 일부다. 프로젝트의
        // overflow 검사를 유지하면서도 이 좁은 해시 구간만 명시적으로 허용한다.
        unchecked
        {
            var value = worldSeed
                ^ ((ulong)tick * 0x9E37_79B9_7F4A_7C15UL)
                ^ ((ulong)(agentIndex + 1) * 0xBF58_476D_1CE4_E5B9UL)
                ^ ((ulong)(locationId + 1) * 0x94D0_49BB_1331_11EBUL)
                ^ trigger;
            value ^= value >> 30;
            value *= 0xBF58_476D_1CE4_E5B9UL;
            value ^= value >> 27;
            value *= 0x94D0_49BB_1331_11EBUL;
            return value ^ (value >> 31);
        }
    }
}

public static class ContentDefinitionRegistry
{
    public const int CurrentRevision = 1;

    private static readonly ContentDefinition[] Definitions =
    [
        Create("secret-realm.natural", "자연 비경", ContentDefinitionKind.SecretRealm,
            ContentInstanceKind.SecretRealm, SecretRealmNature.Natural, false),
        Create("secret-realm.artificial", "인공 비경", ContentDefinitionKind.SecretRealm,
            ContentInstanceKind.SecretRealm, SecretRealmNature.Artificial, false),
        Create("material.artifact", "법보 재료", ContentDefinitionKind.Material,
            ContentInstanceKind.MaterialStack, SecretRealmNature.None, true),
        Create("material.pill", "단약 재료", ContentDefinitionKind.Material,
            ContentInstanceKind.MaterialStack, SecretRealmNature.None, true),
        Create("item.artifact.offense", "공격 법보", ContentDefinitionKind.Item,
            ContentInstanceKind.Item, SecretRealmNature.None, false),
        Create("item.artifact.defense", "방어 법보", ContentDefinitionKind.Item,
            ContentInstanceKind.Item, SecretRealmNature.None, false),
        Create("item.pill.growth", "성장 단약", ContentDefinitionKind.Item,
            ContentInstanceKind.Item, SecretRealmNature.None, false),
        Create("item.pill.breakthrough", "돌파 단약", ContentDefinitionKind.Item,
            ContentInstanceKind.Item, SecretRealmNature.None, false),
        Create("book.technique", "공법 서적", ContentDefinitionKind.Book,
            ContentInstanceKind.PhysicalRecord, SecretRealmNature.None, false),
        Create("book.spell", "법술 서적", ContentDefinitionKind.Book,
            ContentInstanceKind.PhysicalRecord, SecretRealmNature.None, false),
        Create("created-work.independent-inheritance", "독립 전승", ContentDefinitionKind.CreatedWork,
            ContentInstanceKind.CreatedWork, SecretRealmNature.None, false),
    ];

    private static readonly Dictionary<ulong, ContentDefinition> ById =
        Definitions.ToDictionary(definition => definition.Id.Value);
    private static readonly IReadOnlyList<ContentDefinition> ReadOnlyDefinitions =
        Array.AsReadOnly(Definitions);

    public static IReadOnlyList<ContentDefinition> All => ReadOnlyDefinitions;

    public static bool TryGet(ContentDefinitionId id, out ContentDefinition definition) =>
        ById.TryGetValue(id.Value, out definition);

    public static ContentDefinition Get(string stableRef) => Definitions.Single(definition =>
        string.Equals(definition.StableRef, stableRef, StringComparison.Ordinal));

    private static ContentDefinition Create(
        string stableRef,
        string koreanName,
        ContentDefinitionKind kind,
        ContentInstanceKind instanceKind,
        SecretRealmNature secretRealmNature,
        bool stackable) => new(
            new ContentDefinitionId(
                AuthorityRegistry.CreateStableId("cmip.content-definition", stableRef).Value),
            stableRef,
            CurrentRevision,
            koreanName,
            kind,
            instanceKind,
            secretRealmNature,
            stackable);
}
