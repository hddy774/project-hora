namespace Chronicles.Simulation;

public enum LawCandidateKind : byte
{
    Base,
    Derived,
}

public readonly record struct LawCandidateRef(byte Ordinal, LawCandidateKind Kind)
{
    public bool IsValid => Ordinal < LawAuthorityRules.CandidateCount
        && Kind == LawAuthorityRules.GetKind(Ordinal);

    public override string ToString() => Kind == LawCandidateKind.Base
        ? $"LAW-CANDIDATE-BASE-{Ordinal + 1:00}"
        : $"LAW-CANDIDATE-DERIVED-{Ordinal - LawAuthorityRules.BaseCandidateCount + 1:00}";
}

public enum LawContractAvailability : byte
{
    DataUnavailable,
    Specified,
}

[Flags]
public enum LawEvidenceFacet : byte
{
    None = 0,
    Adapter = 1 << 0,
    ResultContract = 1 << 1,
    Metric = 1 << 2,
    StateInput = 1 << 3,
    StateOutput = 1 << 4,
    SqliteRoundTrip = 1 << 5,
    SaveReplay = 1 << 6,
    All = Adapter | ResultContract | Metric | StateInput | StateOutput
        | SqliteRoundTrip | SaveReplay,
}

[Flags]
public enum LawResearchSourceKind : byte
{
    None = 0,
    ObservedPhenomenon = 1 << 0,
    ApplicationEvidence = 1 << 1,
    EditionOrInheritance = 1 << 2,
    Teaching = 1 << 3,
    CultivationInsight = 1 << 4,
    ExperimentOrContent = 1 << 5,
    PastLifeTrace = 1 << 6,
}

public enum LawKnowledgeClaimState : byte
{
    Unknown,
    Correct,
    Incomplete,
    Misunderstood,
    Corrected,
}

public enum LawWorldActor : byte
{
    WorldAi,
    Tiandao,
}

public enum LawPreflightStatus : byte
{
    ReviewRequired,
    Ready,
}

public enum LawApplicationStatus : byte
{
    NotApplied,
    Applied,
}

public enum LawCommandStatus : byte
{
    Applied,
    InvalidCandidate,
    InvalidAgent,
    InvalidActor,
    InvalidSource,
    InvalidValue,
    InvalidTransition,
    DuplicateSource,
}

public readonly record struct LawReadinessCheckpoint(
    ulong AdapterEvidence,
    ulong ResultContractEvidence,
    ulong MetricEvidence,
    ulong StateInputEvidence,
    ulong StateOutputEvidence,
    ulong SqliteRoundTripEvidence,
    ulong SaveReplayEvidence)
{
    public LawEvidenceFacet PresentFacets
    {
        get
        {
            var value = LawEvidenceFacet.None;
            if (AdapterEvidence != 0) value |= LawEvidenceFacet.Adapter;
            if (ResultContractEvidence != 0) value |= LawEvidenceFacet.ResultContract;
            if (MetricEvidence != 0) value |= LawEvidenceFacet.Metric;
            if (StateInputEvidence != 0) value |= LawEvidenceFacet.StateInput;
            if (StateOutputEvidence != 0) value |= LawEvidenceFacet.StateOutput;
            if (SqliteRoundTripEvidence != 0) value |= LawEvidenceFacet.SqliteRoundTrip;
            if (SaveReplayEvidence != 0) value |= LawEvidenceFacet.SaveReplay;
            return value;
        }
    }

    public LawEvidenceFacet MissingFacets => LawEvidenceFacet.All & ~PresentFacets;

    public bool IsReady => MissingFacets == LawEvidenceFacet.None;
}

public readonly record struct LawCandidateCheckpoint(
    byte Ordinal,
    LawCandidateKind Kind,
    LawContractAvailability ScopeAvailability,
    LawContractAvailability RelationAvailability,
    LawReadinessCheckpoint Readiness);

public readonly record struct LawApplicationCheckpoint(
    long Sequence,
    byte CandidateOrdinal,
    long Tick,
    ulong SourceResultId,
    ulong InputStateHash,
    ulong OutputStateHash,
    long MetricValue,
    LawApplicationStatus Status);

public readonly record struct PersonalLawStateCheckpoint(
    int AgentIndex,
    byte CandidateOrdinal,
    LawResearchSourceKind ResearchSources,
    LawKnowledgeClaimState ClaimState,
    LawContractAvailability UnderstandingAvailability,
    ushort UnderstandingHundredths,
    LawContractAvailability EmbodimentAvailability,
    ushort EmbodimentHundredths,
    LawContractAvailability OperationAvailability,
    ushort OperationHundredths,
    LawContractAvailability DaoDirectionAvailability,
    ushort DaoDirectionHundredths,
    LawContractAvailability ConfidenceAvailability,
    ushort ConfidenceHundredths,
    long KnowledgeProvenanceId,
    long ApplicationSequence,
    ulong SourceResultId,
    long UpdatedTick,
    int Revision);

public readonly record struct LawWorldJudgmentCheckpoint(
    long Sequence,
    LawWorldActor Actor,
    byte CandidateOrdinal,
    long Tick,
    ulong SourceResultId,
    LawPreflightStatus Preflight,
    LawApplicationStatus Result,
    long ApplicationSequence);

public sealed record LawAuthorityCheckpoint(
    int SchemaRevision,
    long AuthorityRevision,
    long NextApplicationSequence,
    long NextJudgmentSequence,
    LawCandidateCheckpoint[] Candidates,
    LawApplicationCheckpoint[] Applications,
    PersonalLawStateCheckpoint[] PersonalStates,
    LawWorldJudgmentCheckpoint[] Judgments);

public readonly record struct ObservePersonalLawCommand(
    int AgentIndex,
    LawCandidateRef Candidate,
    LawResearchSourceKind ResearchSources,
    LawKnowledgeClaimState ClaimState,
    LawContractAvailability UnderstandingAvailability,
    ushort UnderstandingHundredths,
    LawContractAvailability EmbodimentAvailability,
    ushort EmbodimentHundredths,
    LawContractAvailability OperationAvailability,
    ushort OperationHundredths,
    LawContractAvailability DaoDirectionAvailability,
    ushort DaoDirectionHundredths,
    LawContractAvailability ConfidenceAvailability,
    ushort ConfidenceHundredths,
    long KnowledgeProvenanceId,
    long ApplicationSequence,
    ulong SourceResultId,
    long Tick);

public readonly record struct RecordLawWorldJudgmentCommand(
    LawWorldActor Actor,
    LawCandidateRef Candidate,
    ulong SourceResultId,
    long Tick);

public readonly record struct LawCommandResult(
    LawCommandStatus Status,
    LawPreflightStatus Preflight = LawPreflightStatus.ReviewRequired,
    long Sequence = 0)
{
    public bool Applied => Status == LawCommandStatus.Applied;
}

public readonly record struct LawCandidateSnapshot(
    LawCandidateRef Candidate,
    LawContractAvailability ScopeAvailability,
    LawContractAvailability RelationAvailability,
    LawEvidenceFacet MissingEvidence,
    int ApplicationCount,
    int PersonalStateCount,
    int WorldJudgmentCount);

public sealed record WorldLawSnapshot(
    long AuthorityRevision,
    int CandidateCount,
    int BaseCandidateCount,
    int DerivedCandidateCount,
    int ReadyCandidateCount,
    int StableLawCount,
    int ApplicationCount,
    int PersonalStateCount,
    int MisunderstoodClaimCount,
    int WorldAiJudgmentCount,
    int TiandaoJudgmentCount,
    IReadOnlyList<LawCandidateSnapshot> Candidates,
    int CandidateResultOffset,
    int CandidateResultTotal)
{
    public static WorldLawSnapshot Empty { get; } = new(
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        Array.Empty<LawCandidateSnapshot>(), 0, 0);
}

public static class LawAuthorityRules
{
    public const int CurrentSchemaRevision = 1;
    public const int BaseCandidateCount = 10;
    public const int DerivedCandidateCount = 44;
    public const int CandidateCount = BaseCandidateCount + DerivedCandidateCount;
    public const string StableIdGateReviewId = "PC-TEMP-LAW-STABLE-ID-GATE-021";
    public const string ScopeMappingReviewId = "PC-TEMP-LAW-SCOPE-MAPPING-021";

    public static LawCandidateKind GetKind(int ordinal)
    {
        if ((uint)ordinal >= CandidateCount)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }
        return ordinal < BaseCandidateCount
            ? LawCandidateKind.Base
            : LawCandidateKind.Derived;
    }
}
