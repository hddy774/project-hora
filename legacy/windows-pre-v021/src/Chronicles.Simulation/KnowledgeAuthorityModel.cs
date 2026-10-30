using Chronicles.Core;

namespace Chronicles.Simulation;

public enum KnowledgeContentKind : byte
{
    Technique,
    Spell,
    CraftingMethod,
    HistoricalRecord,
}

public enum KnowledgeEditionRelation : byte
{
    Original,
    Copy,
    Revision,
    Summary,
    Translation,
    Tampered,
    Restored,
    Reconstructed,
}

public enum PhysicalRecordFormat : byte
{
    Book,
    JadeSlip,
    Inscription,
}

public enum KnowledgeAccessorKind : byte
{
    Agent,
    Organization,
}

public readonly record struct KnowledgeAccessorRef(KnowledgeAccessorKind Kind, long Id)
{
    public bool IsValid => Kind switch
    {
        KnowledgeAccessorKind.Agent => Id >= 0,
        KnowledgeAccessorKind.Organization => Id > 0,
        _ => false,
    };
}

public enum KnowledgeAccessState : byte
{
    None,
    CanRead,
    Studied,
    Understood,
}

public enum KnowledgeBeliefState : byte
{
    Unknown,
    Correct,
    Incomplete,
    Misunderstood,
    Corrected,
}

public enum KnowledgeValueAvailability : byte
{
    DataUnavailable,
    Specified,
}

public enum KnowledgeProvenanceKind : byte
{
    ContentAuthored,
    EditionCreated,
    PhysicalRecordBound,
    AccessGranted,
    ClaimCorrected,
}

public enum KnowledgeCommandStatus : byte
{
    Applied,
    InvalidContent,
    InvalidEdition,
    InvalidPhysicalRecord,
    InvalidAccessor,
    InvalidRelation,
    InvalidConfidence,
    InvalidTransition,
    InvalidSource,
    DuplicateSource,
    DuplicateBinding,
}

public readonly record struct KnowledgeContentCheckpoint(
    ulong ContentId,
    int Revision,
    KnowledgeContentKind Kind,
    int CatalogOrdinal,
    int OriginatorAgent,
    long CreatedTick,
    long LatestProvenanceId);

public readonly record struct KnowledgeEditionCheckpoint(
    long EditionId,
    ulong ContentId,
    long ParentEditionId,
    KnowledgeEditionRelation Relation,
    int ContributorAgent,
    KnowledgeValueAvailability ConfidenceAvailability,
    ushort ConfidenceHundredths,
    long CreatedTick,
    long LatestProvenanceId);

public readonly record struct KnowledgeRecordBindingCheckpoint(
    long PhysicalRecordInstanceId,
    long EditionId,
    PhysicalRecordFormat Format,
    long BoundTick,
    long SourceProvenanceId);

public readonly record struct KnowledgeAccessCheckpoint(
    KnowledgeAccessorKind AccessorKind,
    long AccessorId,
    ulong ContentId,
    long EditionId,
    KnowledgeAccessState AccessState,
    KnowledgeBeliefState ClaimState,
    KnowledgeValueAvailability UnderstandingAvailability,
    ushort UnderstandingHundredths,
    KnowledgeValueAvailability ConfidenceAvailability,
    ushort ConfidenceHundredths,
    long UpdatedTick,
    long SourceProvenanceId,
    int Revision);

public readonly record struct KnowledgeProvenanceCheckpoint(
    long ProvenanceId,
    long Sequence,
    long Tick,
    ulong SourceResultId,
    KnowledgeProvenanceKind Kind,
    ulong ContentId,
    long EditionId,
    long PhysicalRecordInstanceId,
    KnowledgeAccessorKind AccessorKind,
    long AccessorId,
    long ParentProvenanceId);

public sealed record KnowledgeAuthorityCheckpoint(
    int SchemaRevision,
    long NextEditionId,
    long NextProvenanceId,
    long ProvenanceSequence,
    KnowledgeContentCheckpoint[] Contents,
    KnowledgeEditionCheckpoint[] Editions,
    KnowledgeRecordBindingCheckpoint[] RecordBindings,
    KnowledgeAccessCheckpoint[] Access,
    KnowledgeProvenanceCheckpoint[] Provenance);

public readonly record struct CreateKnowledgeEditionCommand(
    KnowledgeContentId ContentId,
    KnowledgeEditionId? ParentEditionId,
    KnowledgeEditionRelation Relation,
    int ContributorAgent,
    KnowledgeValueAvailability ConfidenceAvailability,
    ushort ConfidenceHundredths,
    ContentSourceResultId SourceResultId,
    long Tick);

public readonly record struct BindKnowledgeRecordCommand(
    ContentInstanceId PhysicalRecordInstanceId,
    KnowledgeEditionId EditionId,
    PhysicalRecordFormat Format,
    ContentSourceResultId SourceResultId,
    long Tick);

public readonly record struct GrantKnowledgeAccessCommand(
    KnowledgeAccessorRef Accessor,
    KnowledgeEditionId EditionId,
    KnowledgeAccessState AccessState,
    KnowledgeBeliefState ClaimState,
    KnowledgeValueAvailability UnderstandingAvailability,
    ushort UnderstandingHundredths,
    KnowledgeValueAvailability ConfidenceAvailability,
    ushort ConfidenceHundredths,
    ContentSourceResultId SourceResultId,
    long Tick);

public readonly record struct KnowledgeCommandResult(
    KnowledgeCommandStatus Status,
    KnowledgeContentId? ContentId = null,
    KnowledgeEditionId? EditionId = null,
    KnowledgeProvenanceId? ProvenanceId = null)
{
    public bool Applied => Status == KnowledgeCommandStatus.Applied;
}

public readonly record struct KnowledgeEditionSnapshot(
    KnowledgeEditionId EditionId,
    KnowledgeContentId ContentId,
    KnowledgeContentKind ContentKind,
    int CatalogOrdinal,
    KnowledgeEditionRelation Relation,
    KnowledgeEditionId? ParentEditionId,
    KnowledgeValueAvailability ConfidenceAvailability,
    ushort ConfidenceHundredths,
    int PhysicalRecordCount,
    int AccessorCount,
    GeneratedBookDefinitionSnapshot? GeneratedDefinition = null);

public sealed record WorldKnowledgeSnapshot(
    long AuthorityRevision,
    int ContentCount,
    int EditionCount,
    int PhysicalRecordCount,
    int AccessCount,
    int MisunderstoodAccessCount,
    int RestoredEditionCount,
    IReadOnlyList<KnowledgeEditionSnapshot> Editions,
    int EditionResultOffset,
    int EditionResultTotal)
{
    public static WorldKnowledgeSnapshot Empty { get; } = new(
        0, 0, 0, 0, 0, 0, 0,
        Array.Empty<KnowledgeEditionSnapshot>(), 0, 0);
}

public static class KnowledgePopulationRules
{
    public const int InitialContentCount = ContentPopulationRules.ApprovedBookContentCount;
    public const int InitialEditionCount = ContentPopulationRules.ApprovedEditionCount;
    public const int InitialPhysicalRecordCount = ContentPopulationRules.ApprovedPhysicalBookCount;
    public const int TechniqueContentCount = InitialContentCount / 2;
    public const ulong InitialContentSourcePrefix = 0xC012_0000_0000_0000UL;
    public const ulong InitialRecordSourcePrefix = 0xC013_0000_0000_0000UL;
    public const string InitialDistributionReviewId =
        "PC-TEMP-KNOWLEDGE-INITIAL-DISTRIBUTION-021";
    public const string RestorationFormulaReviewId =
        "PC-TEMP-KNOWLEDGE-RESTORATION-FORMULA-021";
}
