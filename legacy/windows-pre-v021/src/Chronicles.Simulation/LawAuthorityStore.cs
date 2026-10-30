namespace Chronicles.Simulation;

/// <summary>
/// 객관 법칙 후보, 실제 적용, 개인 지식 상태와 세계 AI/천도 판단을 서로 다른
/// 권위 열로 보존한다. 실행 증거가 없는 현재 revision에서는 Stable LawId를 만들지 않는다.
/// </summary>
public sealed class LawAuthorityStore
{
    private readonly int _population;
    private readonly LawCandidateCheckpoint[] _candidates;
    private readonly List<LawApplicationCheckpoint> _applications = [];
    private readonly List<PersonalLawStateCheckpoint> _personalStates = [];
    private readonly List<LawWorldJudgmentCheckpoint> _judgments = [];
    private readonly Dictionary<(int Agent, byte Candidate), int> _personalStateIndex = [];
    private readonly HashSet<ulong> _appliedSourceResults = [];
    private long _authorityRevision;
    private long _nextApplicationSequence = 1;
    private long _nextJudgmentSequence = 1;

    private LawAuthorityStore(int population, LawCandidateCheckpoint[] candidates)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(population);
        _population = population;
        _candidates = candidates;
    }

    public long AuthorityRevision => _authorityRevision;
    public int CandidateCount => _candidates.Length;
    public int ApplicationCount => _applications.Count;
    public int PersonalStateCount => _personalStates.Count;
    public int JudgmentCount => _judgments.Count;

    public static LawAuthorityStore CreateReviewCatalog(int population)
    {
        var candidates = new LawCandidateCheckpoint[LawAuthorityRules.CandidateCount];
        for (var ordinal = 0; ordinal < candidates.Length; ordinal++)
        {
            candidates[ordinal] = new LawCandidateCheckpoint(
                checked((byte)ordinal),
                LawAuthorityRules.GetKind(ordinal),
                LawContractAvailability.DataUnavailable,
                LawContractAvailability.DataUnavailable,
                default);
        }
        return new LawAuthorityStore(population, candidates);
    }

    public static LawAuthorityStore Restore(LawAuthorityCheckpoint checkpoint, int population)
    {
        ValidateCheckpoint(checkpoint, population);
        var store = new LawAuthorityStore(population, checkpoint.Candidates.ToArray())
        {
            _authorityRevision = checkpoint.AuthorityRevision,
            _nextApplicationSequence = checkpoint.NextApplicationSequence,
            _nextJudgmentSequence = checkpoint.NextJudgmentSequence,
        };
        store._applications.AddRange(checkpoint.Applications);
        foreach (var state in checkpoint.PersonalStates)
        {
            store._personalStateIndex.Add(
                (state.AgentIndex, state.CandidateOrdinal),
                store._personalStates.Count);
            store._personalStates.Add(state);
            store._appliedSourceResults.Add(state.SourceResultId);
        }
        foreach (var judgment in checkpoint.Judgments)
        {
            store._judgments.Add(judgment);
            store._appliedSourceResults.Add(judgment.SourceResultId);
        }
        return store;
    }

    public LawCommandResult TryObservePersonalLaw(ObservePersonalLawCommand command)
    {
        if (!command.Candidate.IsValid)
        {
            return new LawCommandResult(LawCommandStatus.InvalidCandidate);
        }
        if ((uint)command.AgentIndex >= (uint)_population)
        {
            return new LawCommandResult(LawCommandStatus.InvalidAgent);
        }
        if (!IsValidResearchSources(command.ResearchSources)
            || command.SourceResultId == 0
            || command.Tick < 0
            || !HasRequiredSourceReferences(command))
        {
            return new LawCommandResult(LawCommandStatus.InvalidSource);
        }
        if (!Enum.IsDefined(command.ClaimState)
            || !IsValidValue(
                command.UnderstandingAvailability,
                command.UnderstandingHundredths)
            || !IsValidValue(
                command.EmbodimentAvailability,
                command.EmbodimentHundredths)
            || !IsValidValue(
                command.OperationAvailability,
                command.OperationHundredths)
            || !IsValidValue(
                command.DaoDirectionAvailability,
                command.DaoDirectionHundredths)
            || !IsValidValue(
                command.ConfidenceAvailability,
                command.ConfidenceHundredths))
        {
            return new LawCommandResult(LawCommandStatus.InvalidValue);
        }
        if (!_appliedSourceResults.Add(command.SourceResultId))
        {
            return new LawCommandResult(LawCommandStatus.DuplicateSource);
        }

        var key = (command.AgentIndex, command.Candidate.Ordinal);
        _personalStateIndex.TryGetValue(key, out var existingIndex);
        var previous = _personalStateIndex.ContainsKey(key)
            ? _personalStates[existingIndex]
            : (PersonalLawStateCheckpoint?)null;
        if (!IsValidClaimTransition(previous?.ClaimState, command.ClaimState))
        {
            _appliedSourceResults.Remove(command.SourceResultId);
            return new LawCommandResult(LawCommandStatus.InvalidTransition);
        }

        var state = new PersonalLawStateCheckpoint(
            command.AgentIndex,
            command.Candidate.Ordinal,
            command.ResearchSources,
            command.ClaimState,
            command.UnderstandingAvailability,
            command.UnderstandingHundredths,
            command.EmbodimentAvailability,
            command.EmbodimentHundredths,
            command.OperationAvailability,
            command.OperationHundredths,
            command.DaoDirectionAvailability,
            command.DaoDirectionHundredths,
            command.ConfidenceAvailability,
            command.ConfidenceHundredths,
            command.KnowledgeProvenanceId,
            command.ApplicationSequence,
            command.SourceResultId,
            command.Tick,
            checked((previous?.Revision ?? 0) + 1));
        if (previous is null)
        {
            _personalStateIndex.Add(key, _personalStates.Count);
            _personalStates.Add(state);
        }
        else
        {
            _personalStates[existingIndex] = state;
        }
        _authorityRevision = checked(_authorityRevision + 1);
        return new LawCommandResult(LawCommandStatus.Applied);
    }

    public LawCommandResult TryRecordWorldJudgment(RecordLawWorldJudgmentCommand command)
    {
        if (!command.Candidate.IsValid)
        {
            return new LawCommandResult(LawCommandStatus.InvalidCandidate);
        }
        if (!Enum.IsDefined(command.Actor))
        {
            return new LawCommandResult(LawCommandStatus.InvalidActor);
        }
        if (command.SourceResultId == 0 || command.Tick < 0)
        {
            return new LawCommandResult(LawCommandStatus.InvalidSource);
        }
        if (!_appliedSourceResults.Add(command.SourceResultId))
        {
            return new LawCommandResult(LawCommandStatus.DuplicateSource);
        }

        var preflight = _candidates[command.Candidate.Ordinal].Readiness.IsReady
            ? LawPreflightStatus.Ready
            : LawPreflightStatus.ReviewRequired;
        var sequence = _nextJudgmentSequence++;
        _judgments.Add(new LawWorldJudgmentCheckpoint(
            sequence,
            command.Actor,
            command.Candidate.Ordinal,
            command.Tick,
            command.SourceResultId,
            preflight,
            LawApplicationStatus.NotApplied,
            ApplicationSequence: 0));
        _authorityRevision = checked(_authorityRevision + 1);
        return new LawCommandResult(LawCommandStatus.Applied, preflight, sequence);
    }

    public WorldLawSnapshot CreateSnapshot(int candidateOffset = 0, int candidateLimit = 54)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(candidateOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(candidateLimit);
        var rows = candidateLimit == 0
            ? Array.Empty<LawCandidateSnapshot>()
            : CreateCandidateRows(candidateOffset, candidateLimit);
        return new WorldLawSnapshot(
            _authorityRevision,
            _candidates.Length,
            LawAuthorityRules.BaseCandidateCount,
            LawAuthorityRules.DerivedCandidateCount,
            _candidates.Count(value => value.Readiness.IsReady),
            StableLawCount: 0,
            _applications.Count,
            _personalStates.Count,
            _personalStates.Count(value =>
                value.ClaimState == LawKnowledgeClaimState.Misunderstood),
            _judgments.Count(value => value.Actor == LawWorldActor.WorldAi),
            _judgments.Count(value => value.Actor == LawWorldActor.Tiandao),
            rows,
            Math.Min(candidateOffset, _candidates.Length),
            _candidates.Length);
    }

    public LawAuthorityCheckpoint CreateCheckpoint() => new(
        LawAuthorityRules.CurrentSchemaRevision,
        _authorityRevision,
        _nextApplicationSequence,
        _nextJudgmentSequence,
        _candidates.ToArray(),
        _applications.ToArray(),
        _personalStates.ToArray(),
        _judgments.ToArray());

    public ulong ComputeStateHash()
    {
        var hash = 14695981039346656037UL;
        Add(ref hash, checked((ulong)_population));
        Add(ref hash, checked((ulong)_authorityRevision));
        Add(ref hash, checked((ulong)_nextApplicationSequence));
        Add(ref hash, checked((ulong)_nextJudgmentSequence));
        foreach (var candidate in _candidates)
        {
            Add(ref hash, candidate.Ordinal);
            Add(ref hash, (byte)candidate.Kind);
            Add(ref hash, (byte)candidate.ScopeAvailability);
            Add(ref hash, (byte)candidate.RelationAvailability);
            AddReadinessHash(ref hash, candidate.Readiness);
        }
        foreach (var application in _applications)
        {
            Add(ref hash, checked((ulong)application.Sequence));
            Add(ref hash, application.CandidateOrdinal);
            Add(ref hash, checked((ulong)application.Tick));
            Add(ref hash, application.SourceResultId);
            Add(ref hash, application.InputStateHash);
            Add(ref hash, application.OutputStateHash);
            Add(ref hash, unchecked((ulong)application.MetricValue));
            Add(ref hash, (byte)application.Status);
        }
        foreach (var state in _personalStates)
        {
            Add(ref hash, checked((ulong)state.AgentIndex));
            Add(ref hash, state.CandidateOrdinal);
            Add(ref hash, (byte)state.ResearchSources);
            Add(ref hash, (byte)state.ClaimState);
            AddValueHash(ref hash, state.UnderstandingAvailability, state.UnderstandingHundredths);
            AddValueHash(ref hash, state.EmbodimentAvailability, state.EmbodimentHundredths);
            AddValueHash(ref hash, state.OperationAvailability, state.OperationHundredths);
            AddValueHash(ref hash, state.DaoDirectionAvailability, state.DaoDirectionHundredths);
            AddValueHash(ref hash, state.ConfidenceAvailability, state.ConfidenceHundredths);
            Add(ref hash, checked((ulong)state.KnowledgeProvenanceId));
            Add(ref hash, checked((ulong)state.ApplicationSequence));
            Add(ref hash, state.SourceResultId);
            Add(ref hash, checked((ulong)state.UpdatedTick));
            Add(ref hash, checked((ulong)state.Revision));
        }
        foreach (var judgment in _judgments)
        {
            Add(ref hash, checked((ulong)judgment.Sequence));
            Add(ref hash, (byte)judgment.Actor);
            Add(ref hash, judgment.CandidateOrdinal);
            Add(ref hash, checked((ulong)judgment.Tick));
            Add(ref hash, judgment.SourceResultId);
            Add(ref hash, (byte)judgment.Preflight);
            Add(ref hash, (byte)judgment.Result);
            Add(ref hash, checked((ulong)judgment.ApplicationSequence));
        }
        return hash;
    }

    public static void ValidateCheckpoint(LawAuthorityCheckpoint checkpoint, int population)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(population);
        if (checkpoint.SchemaRevision != LawAuthorityRules.CurrentSchemaRevision
            || checkpoint.AuthorityRevision < 0
            || checkpoint.NextApplicationSequence <= 0
            || checkpoint.NextJudgmentSequence <= 0
            || checkpoint.Candidates is not { Length: LawAuthorityRules.CandidateCount }
            || checkpoint.Applications is null
            || checkpoint.PersonalStates is null
            || checkpoint.Judgments is null)
        {
            throw new InvalidDataException("법칙 권위 checkpoint header가 올바르지 않습니다.");
        }

        for (var index = 0; index < checkpoint.Candidates.Length; index++)
        {
            var candidate = checkpoint.Candidates[index];
            if (candidate.Ordinal != index
                || candidate.Kind != LawAuthorityRules.GetKind(index)
                || !Enum.IsDefined(candidate.ScopeAvailability)
                || !Enum.IsDefined(candidate.RelationAvailability)
                || candidate.ScopeAvailability != LawContractAvailability.DataUnavailable
                || candidate.RelationAvailability != LawContractAvailability.DataUnavailable
                || candidate.Readiness.PresentFacets != LawEvidenceFacet.None)
            {
                throw new InvalidDataException("검토 중인 법칙 후보 계약이 올바르지 않습니다.");
            }
        }
        if (checkpoint.Applications.Length != 0
            || checkpoint.NextApplicationSequence != 1)
        {
            throw new InvalidDataException("READY 전 법칙 적용 결과가 저장되었습니다.");
        }

        var personalKeys = new HashSet<(int Agent, byte Candidate)>();
        var sourceResults = new HashSet<ulong>();
        foreach (var state in checkpoint.PersonalStates)
        {
            if ((uint)state.AgentIndex >= (uint)population
                || state.CandidateOrdinal >= LawAuthorityRules.CandidateCount
                || !IsValidResearchSources(state.ResearchSources)
                || !Enum.IsDefined(state.ClaimState)
                || !IsValidValue(
                    state.UnderstandingAvailability,
                    state.UnderstandingHundredths)
                || !IsValidValue(
                    state.EmbodimentAvailability,
                    state.EmbodimentHundredths)
                || !IsValidValue(
                    state.OperationAvailability,
                    state.OperationHundredths)
                || !IsValidValue(
                    state.DaoDirectionAvailability,
                    state.DaoDirectionHundredths)
                || !IsValidValue(state.ConfidenceAvailability, state.ConfidenceHundredths)
                || state.SourceResultId == 0
                || state.UpdatedTick < 0
                || state.Revision <= 0
                || !HasRequiredSourceReferences(state)
                || !personalKeys.Add((state.AgentIndex, state.CandidateOrdinal))
                || !sourceResults.Add(state.SourceResultId))
            {
                throw new InvalidDataException("개인 법칙 이해 상태가 올바르지 않습니다.");
            }
        }

        long expectedJudgmentSequence = 1;
        foreach (var judgment in checkpoint.Judgments)
        {
            if (judgment.Sequence != expectedJudgmentSequence
                || !Enum.IsDefined(judgment.Actor)
                || judgment.CandidateOrdinal >= LawAuthorityRules.CandidateCount
                || judgment.Tick < 0
                || judgment.SourceResultId == 0
                || judgment.Preflight != LawPreflightStatus.ReviewRequired
                || judgment.Result != LawApplicationStatus.NotApplied
                || judgment.ApplicationSequence != 0
                || !sourceResults.Add(judgment.SourceResultId))
            {
                throw new InvalidDataException("세계 AI·천도 법칙 판단 상태가 올바르지 않습니다.");
            }
            expectedJudgmentSequence++;
        }
        if (checkpoint.NextJudgmentSequence != expectedJudgmentSequence
            || checkpoint.AuthorityRevision
                < checkpoint.PersonalStates.Length + checkpoint.Judgments.Length)
        {
            throw new InvalidDataException("법칙 권위 sequence가 올바르지 않습니다.");
        }
    }

    private LawCandidateSnapshot[] CreateCandidateRows(int offset, int limit)
    {
        var actualOffset = Math.Min(offset, _candidates.Length);
        var count = Math.Min(limit, _candidates.Length - actualOffset);
        var applicationCounts = new int[_candidates.Length];
        var personalCounts = new int[_candidates.Length];
        var judgmentCounts = new int[_candidates.Length];
        foreach (var value in _applications) applicationCounts[value.CandidateOrdinal]++;
        foreach (var value in _personalStates) personalCounts[value.CandidateOrdinal]++;
        foreach (var value in _judgments) judgmentCounts[value.CandidateOrdinal]++;
        var rows = new LawCandidateSnapshot[count];
        for (var index = 0; index < count; index++)
        {
            var candidate = _candidates[actualOffset + index];
            rows[index] = new LawCandidateSnapshot(
                new LawCandidateRef(candidate.Ordinal, candidate.Kind),
                candidate.ScopeAvailability,
                candidate.RelationAvailability,
                candidate.Readiness.MissingFacets,
                applicationCounts[candidate.Ordinal],
                personalCounts[candidate.Ordinal],
                judgmentCounts[candidate.Ordinal]);
        }
        return rows;
    }

    private bool HasRequiredSourceReferences(ObservePersonalLawCommand command) =>
        ((command.ResearchSources & LawResearchSourceKind.EditionOrInheritance) == 0
            ? command.KnowledgeProvenanceId == 0
            : command.KnowledgeProvenanceId > 0)
        && ((command.ResearchSources & LawResearchSourceKind.ApplicationEvidence) == 0
            ? command.ApplicationSequence == 0
            : command.ApplicationSequence > 0
                && _applications.Any(value => value.Sequence == command.ApplicationSequence));

    private static bool HasRequiredSourceReferences(PersonalLawStateCheckpoint state) =>
        ((state.ResearchSources & LawResearchSourceKind.EditionOrInheritance) == 0
            ? state.KnowledgeProvenanceId == 0
            : state.KnowledgeProvenanceId > 0)
        && (state.ResearchSources & LawResearchSourceKind.ApplicationEvidence) == 0
        && state.ApplicationSequence == 0;

    private static bool IsValidClaimTransition(
        LawKnowledgeClaimState? previous,
        LawKnowledgeClaimState next) => next != LawKnowledgeClaimState.Corrected
            ? previous != LawKnowledgeClaimState.Corrected
                || next != LawKnowledgeClaimState.Misunderstood
            : previous == LawKnowledgeClaimState.Misunderstood;

    private static bool IsValidResearchSources(LawResearchSourceKind value) =>
        value != LawResearchSourceKind.None
        && (value & ~(LawResearchSourceKind.ObservedPhenomenon
            | LawResearchSourceKind.ApplicationEvidence
            | LawResearchSourceKind.EditionOrInheritance
            | LawResearchSourceKind.Teaching
            | LawResearchSourceKind.CultivationInsight
            | LawResearchSourceKind.ExperimentOrContent
            | LawResearchSourceKind.PastLifeTrace)) == 0;

    private static bool IsValidValue(
        LawContractAvailability availability,
        ushort hundredths) => availability switch
        {
            LawContractAvailability.DataUnavailable => hundredths == 0,
            LawContractAvailability.Specified => hundredths <= 10_000,
            _ => false,
        };

    private static void AddReadinessHash(ref ulong hash, LawReadinessCheckpoint value)
    {
        Add(ref hash, value.AdapterEvidence);
        Add(ref hash, value.ResultContractEvidence);
        Add(ref hash, value.MetricEvidence);
        Add(ref hash, value.StateInputEvidence);
        Add(ref hash, value.StateOutputEvidence);
        Add(ref hash, value.SqliteRoundTripEvidence);
        Add(ref hash, value.SaveReplayEvidence);
    }

    private static void AddValueHash(
        ref ulong hash,
        LawContractAvailability availability,
        ushort hundredths)
    {
        Add(ref hash, (byte)availability);
        Add(ref hash, hundredths);
    }

    private static void Add(ref ulong hash, ulong value)
    {
        unchecked
        {
            hash ^= value;
            hash *= 1099511628211UL;
        }
    }

    private static void Add(ref ulong hash, ushort value) => Add(ref hash, (ulong)value);
    private static void Add(ref ulong hash, byte value) => Add(ref hash, (ulong)value);
}
