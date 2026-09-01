using Chronicles.Core;

namespace Chronicles.Simulation;

public enum OrganizationKind : byte
{
    Sect,
    Clan,
    Dynasty,
}

public enum OrganizationStatus : byte
{
    Active,
    Vacant,
    Declining,
    Dissolved,
    Merged,
}

public enum OrganizationRole : byte
{
    Member,
    Founder,
    Leader,
    Elder,
    VassalRepresentative,
}

public enum OrganizationRightKind : byte
{
    Headquarters,
    Residence,
    Territory,
    SpiritVein,
}

public enum OrganizationDiplomacyKind : byte
{
    Neutral,
    Allied,
    Hostile,
    Vassal,
}

public enum OrganizationHistoryKind : byte
{
    Founded,
    MemberJoined,
    MemberDeparted,
    LeaderSucceeded,
    LeadershipVacant,
    DeclineStarted,
    Dissolved,
    Split,
    Merged,
    AssetTransferred,
    DiplomacyChanged,
    SoulReconnected,
}

public enum OrganizationCommandStatus : byte
{
    Applied,
    InvalidEvidence,
    GateNotMet,
    KindCapacityReached,
    MembershipConflict,
    MemberCapacityReached,
    OrganizationNotFound,
    OrganizationInactive,
    InvalidTransition,
    InsufficientAssets,
}

public readonly record struct OrganizationMembershipState(
    int AgentIndex,
    PersonIdentityId PersonId,
    SoulIdentityId SoulId,
    OrganizationRole Role,
    long JoinedTick,
    int LoyaltyHundredths,
    bool Active);

public readonly record struct OrganizationRightState(
    OrganizationRightKind Kind,
    AtlasNodeId LocationNodeId,
    long GrantedTick);

public readonly record struct OrganizationDiplomacyState(
    OrganizationId OtherOrganizationId,
    OrganizationDiplomacyKind Kind,
    long ChangedTick);

public readonly record struct OrganizationHistoryEntry(
    long Sequence,
    long Tick,
    OrganizationHistoryKind Kind,
    int PrimaryAgentIndex,
    OrganizationId? OtherOrganizationId,
    long Value);

public sealed record OrganizationAuthorityCheckpoint(
    OrganizationId Id,
    OrganizationKind Kind,
    OrganizationStatus Status,
    string Name,
    int FounderAgentIndex,
    PersonIdentityId FounderPersonId,
    int LeaderAgentIndex,
    PersonIdentityId LeaderPersonId,
    long FoundedTick,
    long StatusChangedTick,
    long TreasurySpiritStones,
    long ReputationHundredths,
    OrganizationMembershipState[] Members,
    OrganizationRightState[] Rights,
    OrganizationDiplomacyState[] Diplomacy,
    OrganizationHistoryEntry[] History);

public sealed record OrganizationStoreCheckpoint(
    long NextOrganizationId,
    long HistorySequence,
    long AuthorityRevision,
    OrganizationAuthorityCheckpoint[] Organizations,
    long LastMaintenanceMonth = -1,
    int EvaluationCursor = 0);

public sealed record OrganizationMemberSnapshot(
    AgentId AgentId,
    string Name,
    OrganizationRole Role,
    int LoyaltyHundredths,
    bool Active);

public sealed record OrganizationSnapshot(
    OrganizationId Id,
    OrganizationKind Kind,
    OrganizationStatus Status,
    string Name,
    AgentId FounderAgentId,
    string FounderName,
    AgentId? LeaderAgentId,
    string LeaderName,
    SimulationTick FoundedTick,
    long TreasurySpiritStones,
    long ReputationHundredths,
    IReadOnlyList<OrganizationMemberSnapshot> Members,
    IReadOnlyList<OrganizationRightState> Rights,
    IReadOnlyList<OrganizationDiplomacyState> Diplomacy,
    IReadOnlyList<OrganizationHistoryEntry> History);

public sealed record WorldOrganizationSnapshot(
    long AuthorityRevision,
    IReadOnlyList<OrganizationSnapshot> Organizations,
    int ActiveSects,
    int ActiveClans,
    int ActiveDynasties,
    bool CompositePowerRankingAvailable,
    string CompositePowerDataState)
{
    public static WorldOrganizationSnapshot Empty { get; } = new(
        0,
        Array.Empty<OrganizationSnapshot>(),
        0,
        0,
        0,
        false,
        "DataUnavailable");
}

public sealed record OrganizationFoundingEvidence(
    OrganizationKind Kind,
    string Name,
    int FounderAgentIndex,
    PersonIdentityId FounderPersonId,
    SoulIdentityId FounderSoulId,
    RealmStage FounderRealm,
    long AvailableSpiritStones,
    int OrganizationOperationAptitudeHundredths,
    int FoundingAptitudeHundredths,
    int LeadershipHundredths,
    int AverageTrustHundredths,
    int AverageSupportHundredths,
    int ControlledProvinceCount,
    bool HasActualHeadquartersSpiritVeinRight,
    bool HasActualResidenceRight,
    bool HasFoundingClan,
    bool HasValidRuler,
    AtlasNodeId PrimaryLocationNodeId,
    int[] CandidateMemberAgentIndices,
    PersonIdentityId[] CandidateMemberPersonIds,
    SoulIdentityId[] CandidateMemberSoulIds,
    int LivingGenealogyCount,
    OrganizationId[] VassalOrganizationIds);

public readonly record struct OrganizationCommandResult(
    OrganizationCommandStatus Status,
    OrganizationId? OrganizationId,
    long SpiritStoneCost,
    string Message)
{
    public bool IsApplied => Status == OrganizationCommandStatus.Applied;
}

public static class OrganizationRules
{
    public const int SectFounderMinimumPledgedMembers = 8;
    public const int SectMinimumAverageTrustHundredths = 5_500;
    public const int SectMinimumOrganizationOperationHundredths = 5_000;
    public const int SectMinimumFoundingAptitudeHundredths = 5_000;
    public const long SectFoundingSpiritStoneCost = 10_000;
    public const int MaximumActiveSects = 80;
    public const int MaximumSectMembers = 100;
    public const int MaximumDirectSubsects = 10;

    public const int ClanMinimumLivingGenealogy = 8;
    public const int ClanMinimumLeadershipHundredths = 5_000;
    public const long ClanFoundingSpiritStoneCost = 5_000;

    public const int DynastyMinimumControlledProvinces = 1;
    public const int DynastyMinimumVassalOrganizations = 3;
    public const long DynastyFoundingSpiritStoneCost = 100_000;
    public const int DynastyMinimumAverageSupportHundredths = 6_000;

    // PC_TEMP_REVIEW ORG-020-01: 평가 분산 주기와 공석 쇠퇴 경계는 다음 버전 검토안이다.
    public const int MonthlyCandidateProbeCount = 1;
    public const int VacantToDecliningYears = 10;
    public const int DecliningToDissolvedYears = 50;
    public const string TemporaryMappingReviewId = "PC-TEMP-ORG-APTITUDE-MAPPING-020";
    public const string SuccessionReviewId = "PC-TEMP-ORG-SUCCESSION-020";

    public static OrganizationCommandStatus ValidateFounding(
        OrganizationFoundingEvidence evidence,
        int activeKindCount)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (!Enum.IsDefined(evidence.Kind)
            || string.IsNullOrWhiteSpace(evidence.Name)
            || evidence.FounderAgentIndex < 0
            || evidence.FounderPersonId.Value == 0
            || evidence.FounderSoulId.Value == 0
            || evidence.PrimaryLocationNodeId.Value == 0
            || evidence.CandidateMemberAgentIndices is null
            || evidence.CandidateMemberPersonIds is null
            || evidence.CandidateMemberSoulIds is null
            || evidence.VassalOrganizationIds is null
            || evidence.CandidateMemberAgentIndices.Length != evidence.CandidateMemberPersonIds.Length
            || evidence.CandidateMemberAgentIndices.Length != evidence.CandidateMemberSoulIds.Length
            || evidence.CandidateMemberAgentIndices.Distinct().Count()
                != evidence.CandidateMemberAgentIndices.Length)
        {
            return OrganizationCommandStatus.InvalidEvidence;
        }

        if (evidence.Kind == OrganizationKind.Sect && activeKindCount >= MaximumActiveSects)
        {
            return OrganizationCommandStatus.KindCapacityReached;
        }

        return evidence.Kind switch
        {
            OrganizationKind.Sect when evidence.FounderRealm < RealmStage.GoldenCore
                || evidence.CandidateMemberAgentIndices.Length < SectFounderMinimumPledgedMembers
                || evidence.AverageTrustHundredths < SectMinimumAverageTrustHundredths
                || evidence.OrganizationOperationAptitudeHundredths
                    < SectMinimumOrganizationOperationHundredths
                || evidence.FoundingAptitudeHundredths < SectMinimumFoundingAptitudeHundredths
                || evidence.AvailableSpiritStones < SectFoundingSpiritStoneCost
                || !evidence.HasActualHeadquartersSpiritVeinRight =>
                OrganizationCommandStatus.GateNotMet,
            OrganizationKind.Clan when evidence.FounderRealm < RealmStage.GoldenCore
                || evidence.LivingGenealogyCount < ClanMinimumLivingGenealogy
                || evidence.LeadershipHundredths < ClanMinimumLeadershipHundredths
                || evidence.AvailableSpiritStones < ClanFoundingSpiritStoneCost
                || !evidence.HasActualResidenceRight =>
                OrganizationCommandStatus.GateNotMet,
            OrganizationKind.Dynasty when evidence.ControlledProvinceCount
                    < DynastyMinimumControlledProvinces
                || !evidence.HasFoundingClan
                || !evidence.HasValidRuler
                || evidence.VassalOrganizationIds.Distinct().Count()
                    < DynastyMinimumVassalOrganizations
                || evidence.AvailableSpiritStones < DynastyFoundingSpiritStoneCost
                || evidence.AverageSupportHundredths
                    < DynastyMinimumAverageSupportHundredths =>
                OrganizationCommandStatus.GateNotMet,
            _ => OrganizationCommandStatus.Applied,
        };
    }

    public static long GetFoundingCost(OrganizationKind kind) => kind switch
    {
        OrganizationKind.Sect => SectFoundingSpiritStoneCost,
        OrganizationKind.Clan => ClanFoundingSpiritStoneCost,
        OrganizationKind.Dynasty => DynastyFoundingSpiritStoneCost,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static int GetMemberCapacity(OrganizationKind kind) => kind switch
    {
        OrganizationKind.Sect => MaximumSectMembers,
        OrganizationKind.Clan or OrganizationKind.Dynasty => int.MaxValue,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}

internal sealed class OrganizationStore
{
    private readonly int _population;
    private readonly List<OrganizationAuthorityCheckpoint> _organizations = [];
    private readonly int[] _activeMembershipByKind;
    private long _nextOrganizationId = 1;
    private long _historySequence;
    private long _authorityRevision;
    private long _lastMaintenanceMonth = -1;
    private int _evaluationCursor;

    public OrganizationStore(int population)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(population);
        _population = population;
        _activeMembershipByKind = new int[checked(population * 3)];
        Array.Fill(_activeMembershipByKind, -1);
    }

    public int Count => _organizations.Count;
    public long AuthorityRevision => _authorityRevision;
    public int EvaluationCursor => _evaluationCursor;
    public long LastMaintenanceMonth => _lastMaintenanceMonth;

    public OrganizationCommandResult TryFound(
        OrganizationFoundingEvidence evidence,
        long tick)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        var status = OrganizationRules.ValidateFounding(
            evidence,
            CountActive(evidence.Kind));
        if (status != OrganizationCommandStatus.Applied)
        {
            return Failure(status, "조직 창설 조건을 충족하지 못했습니다.");
        }

        ValidateAgent(evidence.FounderAgentIndex);
        foreach (var member in evidence.CandidateMemberAgentIndices)
        {
            ValidateAgent(member);
        }

        if (!IsProductionGeographyRight(evidence.Kind, evidence.PrimaryLocationNodeId)
            || (evidence.Kind == OrganizationKind.Dynasty
                && (GetMembershipIndex(evidence.FounderAgentIndex, OrganizationKind.Clan) < 0
                    || evidence.VassalOrganizationIds.Any(vassal =>
                    {
                        var vassalIndex = FindIndex(vassal);
                        return vassalIndex < 0
                            || _organizations[vassalIndex].Status != OrganizationStatus.Active
                            || _organizations[vassalIndex].Kind == OrganizationKind.Dynasty
                            || _organizations[vassalIndex].Diplomacy.Any(relation =>
                                relation.Kind == OrganizationDiplomacyKind.Vassal);
                    }))))
        {
            return Failure(
                OrganizationCommandStatus.InvalidEvidence,
                "실제 위치 권리·창건 가문·봉신 조직 근거가 일치하지 않습니다.");
        }

        var members = new int[1 + evidence.CandidateMemberAgentIndices.Length];
        members[0] = evidence.FounderAgentIndex;
        Array.Copy(evidence.CandidateMemberAgentIndices, 0, members, 1,
            evidence.CandidateMemberAgentIndices.Length);
        if (members.Distinct().Count() != members.Length
            || members.Any(member => GetMembershipIndex(member, evidence.Kind) >= 0))
        {
            return Failure(
                OrganizationCommandStatus.MembershipConflict,
                "같은 종류의 활성 조직에 이미 소속된 인물이 있습니다.");
        }

        if (members.Length > OrganizationRules.GetMemberCapacity(evidence.Kind))
        {
            return Failure(
                OrganizationCommandStatus.MemberCapacityReached,
                "조직 구성원 상한을 초과했습니다.");
        }

        var id = new OrganizationId(_nextOrganizationId);
        var cost = OrganizationRules.GetFoundingCost(evidence.Kind);
        var membership = new OrganizationMembershipState[members.Length];
        membership[0] = new OrganizationMembershipState(
            evidence.FounderAgentIndex,
            evidence.FounderPersonId,
            evidence.FounderSoulId,
            OrganizationRole.Leader,
            tick,
            10_000,
            true);
        for (var index = 1; index < membership.Length; index++)
        {
            membership[index] = new OrganizationMembershipState(
                members[index],
                evidence.CandidateMemberPersonIds[index - 1],
                evidence.CandidateMemberSoulIds[index - 1],
                OrganizationRole.Member,
                tick,
                Math.Clamp(evidence.AverageTrustHundredths, 0, 10_000),
                true);
        }

        var rights = CreateFoundingRights(evidence, tick);
        var history = new[]
        {
            NewHistory(
                tick,
                OrganizationHistoryKind.Founded,
                evidence.FounderAgentIndex,
                null,
                cost),
        };
        var foundingDiplomacy = evidence.Kind == OrganizationKind.Dynasty
            ? evidence.VassalOrganizationIds.Select(vassal =>
                new OrganizationDiplomacyState(
                    vassal,
                    OrganizationDiplomacyKind.Vassal,
                    tick)).ToArray()
            : Array.Empty<OrganizationDiplomacyState>();
        var state = new OrganizationAuthorityCheckpoint(
            id,
            evidence.Kind,
            OrganizationStatus.Active,
            evidence.Name.Trim(),
            evidence.FounderAgentIndex,
            evidence.FounderPersonId,
            evidence.FounderAgentIndex,
            evidence.FounderPersonId,
            tick,
            tick,
            cost,
            0,
            membership,
            rights,
            foundingDiplomacy,
            history);

        // All checks are complete before this mutation block; no externally visible partial state.
        var organizationIndex = _organizations.Count;
        _organizations.Add(state);
        for (var member = 0; member < membership.Length; member++)
        {
            SetMembershipIndex(membership[member].AgentIndex, evidence.Kind, organizationIndex);
        }
        if (evidence.Kind == OrganizationKind.Dynasty)
        {
            foreach (var vassal in evidence.VassalOrganizationIds)
            {
                var vassalIndex = FindIndex(vassal);
                var vassalState = _organizations[vassalIndex];
                _organizations[vassalIndex] = vassalState with
                {
                    Diplomacy = vassalState.Diplomacy.Append(
                        new OrganizationDiplomacyState(
                            id,
                            OrganizationDiplomacyKind.Vassal,
                            tick)).ToArray(),
                };
            }
        }
        _nextOrganizationId++;
        _authorityRevision++;
        return new OrganizationCommandResult(
            OrganizationCommandStatus.Applied,
            id,
            cost,
            "조직 창설 완료");
    }

    public void HandleLifeEnded(
        int agent,
        long tick,
        Func<int, bool> isLiving,
        Func<int, long> successionScore)
    {
        ValidateAgent(agent);
        for (var kind = 0; kind < 3; kind++)
        {
            var organizationIndex = _activeMembershipByKind[(agent * 3) + kind];
            if (organizationIndex < 0)
            {
                continue;
            }

            var state = _organizations[organizationIndex];
            var members = state.Members.ToArray();
            var memberIndex = Array.FindIndex(members, member => member.AgentIndex == agent);
            if (memberIndex < 0 || !members[memberIndex].Active)
            {
                throw new InvalidDataException("조직 소속 index와 권위 상태가 일치하지 않습니다.");
            }

            members[memberIndex] = members[memberIndex] with { Active = false };
            SetMembershipIndex(agent, state.Kind, -1);
            var history = state.History.Append(NewHistory(
                tick,
                OrganizationHistoryKind.MemberDeparted,
                agent,
                null,
                0)).ToArray();
            state = state with { Members = members, History = history };
            if (state.LeaderAgentIndex == agent)
            {
                state = ApplySuccession(state, tick, isLiving, successionScore);
            }

            _organizations[organizationIndex] = state;
            _authorityRevision++;
        }
    }

    public void AdvanceMaintenance(long currentMonth)
    {
        if (currentMonth <= _lastMaintenanceMonth)
        {
            return;
        }

        _lastMaintenanceMonth = currentMonth;
        _evaluationCursor = (_evaluationCursor + OrganizationRules.MonthlyCandidateProbeCount)
            % _population;
        var currentTick = GameCalendar.GetMonthStart(currentMonth).Value;
        for (var index = 0; index < _organizations.Count; index++)
        {
            var state = _organizations[index];
            var elapsedYears = Math.Max(0L,
                (currentTick - state.StatusChangedTick) / GameCalendar.TicksPerYear);
            if (state.Status == OrganizationStatus.Vacant
                && elapsedYears >= OrganizationRules.VacantToDecliningYears)
            {
                _organizations[index] = ChangeStatus(
                    state,
                    OrganizationStatus.Declining,
                    currentTick,
                    OrganizationHistoryKind.DeclineStarted);
                _authorityRevision++;
            }
            else if (state.Status == OrganizationStatus.Declining
                && elapsedYears >= OrganizationRules.DecliningToDissolvedYears)
            {
                Dissolve(index, currentTick);
            }
        }
    }

    public OrganizationCommandResult TryMerge(
        OrganizationId primaryId,
        OrganizationId secondaryId,
        long tick)
    {
        var primaryIndex = FindIndex(primaryId);
        var secondaryIndex = FindIndex(secondaryId);
        if (primaryIndex < 0 || secondaryIndex < 0 || primaryIndex == secondaryIndex)
        {
            return Failure(OrganizationCommandStatus.OrganizationNotFound, "병합 대상을 찾을 수 없습니다.");
        }

        var primary = _organizations[primaryIndex];
        var secondary = _organizations[secondaryIndex];
        if (primary.Status != OrganizationStatus.Active
            || secondary.Status != OrganizationStatus.Active
            || primary.Kind != secondary.Kind)
        {
            return Failure(OrganizationCommandStatus.InvalidTransition, "병합 가능한 조직 상태가 아닙니다.");
        }

        var incoming = secondary.Members.Where(member => member.Active).ToArray();
        if (primary.Members.Count(member => member.Active) + incoming.Length
            > OrganizationRules.GetMemberCapacity(primary.Kind)
            || incoming.Any(member => GetMembershipIndex(member.AgentIndex, primary.Kind)
                is var membershipIndex && membershipIndex != secondaryIndex))
        {
            return Failure(OrganizationCommandStatus.MembershipConflict, "병합 소속 또는 상한이 충돌합니다.");
        }

        var mergedMembers = primary.Members.Concat(incoming).ToArray();
        var mergedRights = primary.Rights.Concat(secondary.Rights)
            .DistinctBy(right => (right.Kind, right.LocationNodeId))
            .ToArray();
        var history = primary.History.Append(NewHistory(
            tick,
            OrganizationHistoryKind.Merged,
            primary.LeaderAgentIndex,
            secondary.Id,
            secondary.TreasurySpiritStones)).ToArray();
        primary = primary with
        {
            TreasurySpiritStones = checked(
                primary.TreasurySpiritStones + secondary.TreasurySpiritStones),
            Members = mergedMembers,
            Rights = mergedRights,
            History = history,
        };
        secondary = ChangeStatus(
            secondary,
            OrganizationStatus.Merged,
            tick,
            OrganizationHistoryKind.Merged,
            primary.Id);

        _organizations[primaryIndex] = primary;
        _organizations[secondaryIndex] = secondary;
        foreach (var member in incoming)
        {
            SetMembershipIndex(member.AgentIndex, primary.Kind, primaryIndex);
        }
        _authorityRevision++;
        return new OrganizationCommandResult(
            OrganizationCommandStatus.Applied,
            primary.Id,
            0,
            "조직 병합 완료");
    }

    public OrganizationCommandResult Dissolve(OrganizationId organizationId, long tick)
    {
        var index = FindIndex(organizationId);
        if (index < 0)
        {
            return Failure(OrganizationCommandStatus.OrganizationNotFound, "해산 대상을 찾을 수 없습니다.");
        }

        if (_organizations[index].Status is OrganizationStatus.Dissolved or OrganizationStatus.Merged)
        {
            return Failure(OrganizationCommandStatus.OrganizationInactive, "이미 종료된 조직입니다.");
        }

        Dissolve(index, tick);
        return new OrganizationCommandResult(
            OrganizationCommandStatus.Applied,
            organizationId,
            0,
            "조직 해산 완료");
    }

    public bool HasActiveMembership(int agent, OrganizationKind kind) =>
        GetMembershipIndex(agent, kind) >= 0;

    public WorldOrganizationSnapshot CreateSnapshot(IReadOnlyList<string> agentNames)
    {
        if (agentNames.Count != _population)
        {
            throw new ArgumentException("조직 표시용 인물 이름 수가 잘못되었습니다.", nameof(agentNames));
        }

        var organizations = new OrganizationSnapshot[_organizations.Count];
        for (var index = 0; index < organizations.Length; index++)
        {
            var state = _organizations[index];
            organizations[index] = new OrganizationSnapshot(
                state.Id,
                state.Kind,
                state.Status,
                state.Name,
                new AgentId(state.FounderAgentIndex),
                agentNames[state.FounderAgentIndex],
                state.LeaderAgentIndex < 0 ? null : new AgentId(state.LeaderAgentIndex),
                state.LeaderAgentIndex < 0 ? "공석" : agentNames[state.LeaderAgentIndex],
                new SimulationTick(state.FoundedTick),
                state.TreasurySpiritStones,
                state.ReputationHundredths,
                state.Members.Select(member => new OrganizationMemberSnapshot(
                    new AgentId(member.AgentIndex),
                    agentNames[member.AgentIndex],
                    member.Role,
                    member.LoyaltyHundredths,
                    member.Active)).ToArray(),
                state.Rights,
                state.Diplomacy,
                state.History);
        }

        return new WorldOrganizationSnapshot(
            _authorityRevision,
            organizations,
            CountActive(OrganizationKind.Sect),
            CountActive(OrganizationKind.Clan),
            CountActive(OrganizationKind.Dynasty),
            false,
            "DataUnavailable · 미승인 사회 자질과 종합 세력 순위 공식 없음");
    }

    public OrganizationStoreCheckpoint CreateCheckpoint() => new(
        _nextOrganizationId,
        _historySequence,
        _authorityRevision,
        _organizations.ToArray(),
        _lastMaintenanceMonth,
        _evaluationCursor);

    public void AddStateHash(ref ulong hash)
    {
        // Schema 25 saves predate Organization Core. A pristine migrated store must add
        // no bytes so its previously persisted state hash remains verifiable.
        if (_organizations.Count == 0
            && _nextOrganizationId == 1
            && _historySequence == 0
            && _authorityRevision == 0
            && _lastMaintenanceMonth == -1
            && _evaluationCursor == 0)
        {
            return;
        }

        Add(ref hash, checked((ulong)_nextOrganizationId));
        Add(ref hash, unchecked((ulong)_historySequence));
        Add(ref hash, unchecked((ulong)_authorityRevision));
        Add(ref hash, unchecked((ulong)_lastMaintenanceMonth));
        Add(ref hash, checked((ulong)_evaluationCursor));
        foreach (var organization in _organizations)
        {
            Add(ref hash, checked((ulong)organization.Id.Value));
            Add(ref hash, (ulong)organization.Kind);
            Add(ref hash, (ulong)organization.Status);
            AddText(ref hash, organization.Name);
            Add(ref hash, checked((ulong)organization.FounderAgentIndex));
            Add(ref hash, organization.FounderPersonId.Value);
            Add(ref hash, unchecked((ulong)organization.LeaderAgentIndex));
            Add(ref hash, organization.LeaderPersonId.Value);
            Add(ref hash, unchecked((ulong)organization.FoundedTick));
            Add(ref hash, unchecked((ulong)organization.StatusChangedTick));
            Add(ref hash, unchecked((ulong)organization.TreasurySpiritStones));
            Add(ref hash, unchecked((ulong)organization.ReputationHundredths));
            foreach (var member in organization.Members)
            {
                Add(ref hash, checked((ulong)member.AgentIndex));
                Add(ref hash, member.PersonId.Value);
                Add(ref hash, member.SoulId.Value);
                Add(ref hash, (ulong)member.Role);
                Add(ref hash, unchecked((ulong)member.JoinedTick));
                Add(ref hash, unchecked((ulong)member.LoyaltyHundredths));
                Add(ref hash, member.Active ? 1UL : 0UL);
            }
            foreach (var right in organization.Rights)
            {
                Add(ref hash, (ulong)right.Kind);
                Add(ref hash, checked((ulong)right.LocationNodeId.Value));
                Add(ref hash, unchecked((ulong)right.GrantedTick));
            }
            foreach (var diplomacy in organization.Diplomacy)
            {
                Add(ref hash, checked((ulong)diplomacy.OtherOrganizationId.Value));
                Add(ref hash, (ulong)diplomacy.Kind);
                Add(ref hash, unchecked((ulong)diplomacy.ChangedTick));
            }
            foreach (var history in organization.History)
            {
                Add(ref hash, unchecked((ulong)history.Sequence));
                Add(ref hash, unchecked((ulong)history.Tick));
                Add(ref hash, (ulong)history.Kind);
                Add(ref hash, unchecked((ulong)history.PrimaryAgentIndex));
                Add(ref hash, checked((ulong)(history.OtherOrganizationId?.Value ?? 0)));
                Add(ref hash, unchecked((ulong)history.Value));
            }
        }
    }

    public static OrganizationStore Restore(int population, OrganizationStoreCheckpoint checkpoint)
    {
        if (!IsValidCheckpoint(population, checkpoint))
        {
            throw new InvalidDataException("조직 권위 체크포인트가 잘못되었습니다.");
        }

        var store = new OrganizationStore(population)
        {
            _nextOrganizationId = checkpoint.NextOrganizationId,
            _historySequence = checkpoint.HistorySequence,
            _authorityRevision = checkpoint.AuthorityRevision,
            _lastMaintenanceMonth = checkpoint.LastMaintenanceMonth,
            _evaluationCursor = checkpoint.EvaluationCursor,
        };
        store._organizations.AddRange(checkpoint.Organizations);
        for (var organizationIndex = 0;
             organizationIndex < checkpoint.Organizations.Length;
             organizationIndex++)
        {
            var organization = checkpoint.Organizations[organizationIndex];
            if (organization.Status is OrganizationStatus.Dissolved or OrganizationStatus.Merged)
            {
                continue;
            }
            foreach (var member in organization.Members.Where(member => member.Active))
            {
                store.SetMembershipIndex(member.AgentIndex, organization.Kind, organizationIndex);
            }
        }
        return store;
    }

    public static bool IsValidCheckpoint(int population, OrganizationStoreCheckpoint checkpoint)
    {
        if (checkpoint.NextOrganizationId <= 0
            || checkpoint.HistorySequence < 0
            || checkpoint.AuthorityRevision < 0
            || checkpoint.LastMaintenanceMonth < -1
            || checkpoint.EvaluationCursor < 0
            || checkpoint.EvaluationCursor >= population
            || checkpoint.Organizations is null)
        {
            return false;
        }

        var ids = new HashSet<long>();
        var memberships = new HashSet<(int Agent, OrganizationKind Kind)>();
        long maximumId = 0;
        long maximumHistory = 0;
        foreach (var organization in checkpoint.Organizations)
        {
            maximumId = Math.Max(maximumId, organization.Id.Value);
            if (!ids.Add(organization.Id.Value)
                || !Enum.IsDefined(organization.Kind)
                || !Enum.IsDefined(organization.Status)
                || string.IsNullOrWhiteSpace(organization.Name)
                || (uint)organization.FounderAgentIndex >= (uint)population
                || organization.FounderPersonId.Value == 0
                || organization.LeaderAgentIndex < -1
                || organization.LeaderAgentIndex >= population
                || organization.FoundedTick < 0
                || organization.StatusChangedTick < organization.FoundedTick
                || organization.TreasurySpiritStones < 0
                || organization.Members is null
                || organization.Rights is null
                || organization.Diplomacy is null
                || organization.History is null)
            {
                return false;
            }

            foreach (var member in organization.Members)
            {
                if ((uint)member.AgentIndex >= (uint)population
                    || member.PersonId.Value == 0
                    || member.SoulId.Value == 0
                    || !Enum.IsDefined(member.Role)
                    || member.JoinedTick < organization.FoundedTick
                    || member.LoyaltyHundredths is < 0 or > 10_000
                    || (member.Active
                        && organization.Status is not (OrganizationStatus.Dissolved
                            or OrganizationStatus.Merged)
                        && !memberships.Add((member.AgentIndex, organization.Kind))))
                {
                    return false;
                }
            }
            foreach (var right in organization.Rights)
            {
                if (!Enum.IsDefined(right.Kind)
                    || right.LocationNodeId.Value == 0
                    || right.GrantedTick < organization.FoundedTick)
                {
                    return false;
                }
            }
            foreach (var history in organization.History)
            {
                maximumHistory = Math.Max(maximumHistory, history.Sequence);
                if (history.Sequence <= 0
                    || history.Tick < organization.FoundedTick
                    || !Enum.IsDefined(history.Kind)
                    || history.PrimaryAgentIndex < -1
                    || history.PrimaryAgentIndex >= population)
                {
                    return false;
                }
            }
        }

        return checkpoint.NextOrganizationId > maximumId
            && checkpoint.HistorySequence >= maximumHistory;
    }

    private OrganizationAuthorityCheckpoint ApplySuccession(
        OrganizationAuthorityCheckpoint state,
        long tick,
        Func<int, bool> isLiving,
        Func<int, long> successionScore)
    {
        var successor = state.Members
            .Where(member => member.Active && isLiving(member.AgentIndex))
            .OrderByDescending(member => successionScore(member.AgentIndex))
            .ThenBy(member => member.AgentIndex)
            .FirstOrDefault();
        if (successor.PersonId.Value == 0)
        {
            return ChangeStatus(
                state with { LeaderAgentIndex = -1, LeaderPersonId = default },
                OrganizationStatus.Vacant,
                tick,
                OrganizationHistoryKind.LeadershipVacant);
        }

        var members = state.Members.ToArray();
        var successorIndex = Array.FindIndex(
            members,
            member => member.AgentIndex == successor.AgentIndex);
        members[successorIndex] = successor with { Role = OrganizationRole.Leader };
        return state with
        {
            Status = OrganizationStatus.Active,
            StatusChangedTick = tick,
            LeaderAgentIndex = successor.AgentIndex,
            LeaderPersonId = successor.PersonId,
            Members = members,
            History = state.History.Append(NewHistory(
                tick,
                OrganizationHistoryKind.LeaderSucceeded,
                successor.AgentIndex,
                null,
                0)).ToArray(),
        };
    }

    private void Dissolve(int index, long tick)
    {
        var state = _organizations[index];
        foreach (var member in state.Members.Where(member => member.Active))
        {
            SetMembershipIndex(member.AgentIndex, state.Kind, -1);
        }
        _organizations[index] = ChangeStatus(
            state with
            {
                LeaderAgentIndex = -1,
                LeaderPersonId = default,
                Members = state.Members.Select(member => member with { Active = false }).ToArray(),
            },
            OrganizationStatus.Dissolved,
            tick,
            OrganizationHistoryKind.Dissolved);
        _authorityRevision++;
    }

    private OrganizationAuthorityCheckpoint ChangeStatus(
        OrganizationAuthorityCheckpoint state,
        OrganizationStatus status,
        long tick,
        OrganizationHistoryKind historyKind,
        OrganizationId? other = null) => state with
    {
        Status = status,
        StatusChangedTick = tick,
        History = state.History.Append(NewHistory(
            tick,
            historyKind,
            state.LeaderAgentIndex,
            other,
            0)).ToArray(),
    };

    private static OrganizationRightState[] CreateFoundingRights(
        OrganizationFoundingEvidence evidence,
        long tick) => evidence.Kind switch
    {
        OrganizationKind.Sect =>
        [
            new(OrganizationRightKind.Headquarters, evidence.PrimaryLocationNodeId, tick),
            new(OrganizationRightKind.SpiritVein, evidence.PrimaryLocationNodeId, tick),
        ],
        OrganizationKind.Clan =>
        [new(OrganizationRightKind.Residence, evidence.PrimaryLocationNodeId, tick)],
        OrganizationKind.Dynasty =>
        [new(OrganizationRightKind.Territory, evidence.PrimaryLocationNodeId, tick)],
        _ => throw new ArgumentOutOfRangeException(nameof(evidence)),
    };

    private OrganizationHistoryEntry NewHistory(
        long tick,
        OrganizationHistoryKind kind,
        int primaryAgent,
        OrganizationId? other,
        long value) => new(
            ++_historySequence,
            tick,
            kind,
            primaryAgent,
            other,
            value);

    private int CountActive(OrganizationKind kind) => _organizations.Count(organization =>
        organization.Kind == kind
        && organization.Status is OrganizationStatus.Active
            or OrganizationStatus.Vacant
            or OrganizationStatus.Declining);

    private int FindIndex(OrganizationId id) =>
        _organizations.FindIndex(organization => organization.Id == id);

    private static bool IsProductionGeographyRight(
        OrganizationKind kind,
        AtlasNodeId locationNodeId)
    {
        if (kind == OrganizationKind.Dynasty)
        {
            return WorldGeographyRules.GetProvince(WorldGeographyRules.OcheonProvinceId).NodeId
                == locationNodeId;
        }

        return WorldGeographyRules.GetPoints(1).Any(point => point.NodeId == locationNodeId)
            || WorldGeographyRules.GetPoints(2).Any(point => point.NodeId == locationNodeId);
    }

    private int GetMembershipIndex(int agent, OrganizationKind kind)
    {
        ValidateAgent(agent);
        return _activeMembershipByKind[(agent * 3) + (int)kind];
    }

    private void SetMembershipIndex(int agent, OrganizationKind kind, int index)
    {
        ValidateAgent(agent);
        _activeMembershipByKind[(agent * 3) + (int)kind] = index;
    }

    private void ValidateAgent(int agent)
    {
        if ((uint)agent >= (uint)_population)
        {
            throw new ArgumentOutOfRangeException(nameof(agent));
        }
    }

    private static OrganizationCommandResult Failure(
        OrganizationCommandStatus status,
        string message) => new(status, null, 0, message);

    private static void AddText(ref ulong hash, string value)
    {
        foreach (var character in value)
        {
            Add(ref hash, character);
        }
    }

    private static void Add(ref ulong hash, ulong value)
    {
        hash ^= value;
        hash *= 1099511628211UL;
    }
}
