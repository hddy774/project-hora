using Chronicles.Core;

namespace Chronicles.Simulation;

internal sealed partial class OrganizationStore
{
    private readonly int _population;
    private readonly List<OrganizationAuthorityCheckpoint> _organizations = [];
    private readonly int[] _activeMembershipByKind;
    private readonly long[] _foundingReserves;
    private readonly int[] _preparationSponsors;
    private readonly List<int> _preparationFounders = new(
        OrganizationRules.MaximumSectPreparationPlans);
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
        _foundingReserves = new long[population];
        _preparationSponsors = new int[population];
        Array.Fill(_activeMembershipByKind, -1);
        Array.Fill(_preparationSponsors, -1);
    }

    public int Count => _organizations.Count;
    public long AuthorityRevision => _authorityRevision;
    public int EvaluationCursor => _evaluationCursor;
    public long LastMaintenanceMonth => _lastMaintenanceMonth;
    public long TotalFoundingReserves => _foundingReserves.Sum();
    public int PreparationPlanCount => _preparationFounders.Count;

    public int CopyPreparationFounders(Span<int> destination)
    {
        if (destination.Length < _preparationFounders.Count)
        {
            throw new ArgumentException("창설 준비자 출력 공간이 부족합니다.", nameof(destination));
        }
        for (var index = 0; index < _preparationFounders.Count; index++)
        {
            destination[index] = _preparationFounders[index];
        }
        return _preparationFounders.Count;
    }

    public int CopyActiveSectIds(Span<OrganizationId> destination)
    {
        var count = 0;
        foreach (var organization in _organizations)
        {
            if (organization.Kind != OrganizationKind.Sect
                || organization.Status != OrganizationStatus.Active)
            {
                continue;
            }
            if (count >= destination.Length)
            {
                throw new ArgumentException("활성 종문 출력 공간이 부족합니다.", nameof(destination));
            }
            destination[count++] = organization.Id;
        }
        return count;
    }

    public int CopyActiveMemberAgents(OrganizationId organizationId, Span<int> destination)
    {
        var index = FindIndex(organizationId);
        if (index < 0)
        {
            return 0;
        }
        var count = 0;
        foreach (var member in _organizations[index].Members)
        {
            if (!member.Active)
            {
                continue;
            }
            if (count >= destination.Length)
            {
                throw new ArgumentException("활성 구성원 출력 공간이 부족합니다.", nameof(destination));
            }
            destination[count++] = member.AgentIndex;
        }
        return count;
    }

    public bool CanPledgeForPreparation(int agent, int founder)
    {
        ValidateAgent(agent);
        ValidateAgent(founder);
        return _preparationSponsors[agent] is -1 || _preparationSponsors[agent] == founder;
    }

    public bool TryRegisterPreparationPlan(int founder, int[] pledgedAgents)
    {
        ValidateAgent(founder);
        ArgumentNullException.ThrowIfNull(pledgedAgents);
        if (pledgedAgents.Length != OrganizationRules.SectFounderMinimumPledgedMembers
            || pledgedAgents.Distinct().Count() != pledgedAgents.Length
            || pledgedAgents.Contains(founder)
            || pledgedAgents.Any(agent => (uint)agent >= (uint)_population)
            || pledgedAgents.Any(agent => !CanPledgeForPreparation(agent, founder))
            || (_preparationSponsors[founder] >= 0
                && _preparationSponsors[founder] != founder)
            || (_preparationSponsors[founder] == -1
                && PreparationPlanCount >= OrganizationRules.GetSectPreparationCapacity(_population)))
        {
            return false;
        }

        for (var agent = 0; agent < _population; agent++)
        {
            if (agent != founder && _preparationSponsors[agent] == founder)
            {
                _preparationSponsors[agent] = -1;
            }
        }
        if (_preparationSponsors[founder] == -1)
        {
            _preparationFounders.Add(founder);
            _preparationFounders.Sort();
        }
        _preparationSponsors[founder] = founder;
        foreach (var pledged in pledgedAgents)
        {
            _preparationSponsors[pledged] = founder;
        }
        _authorityRevision++;
        return true;
    }

    public bool TryGetPreparationSponsor(int agent, out int founder)
    {
        ValidateAgent(agent);
        founder = _preparationSponsors[agent];
        return founder >= 0
            && _foundingReserves[founder] < OrganizationRules.SectFoundingSpiritStoneCost;
    }

    public bool HasPreparationPlan(int founder)
    {
        ValidateAgent(founder);
        return _preparationSponsors[founder] == founder;
    }

    public long CancelPreparationParticipation(int agent)
    {
        ValidateAgent(agent);
        var founder = _preparationSponsors[agent];
        if (founder < 0)
        {
            return 0;
        }
        if (founder != agent)
        {
            _preparationSponsors[agent] = -1;
            _authorityRevision++;
            return 0;
        }

        return CompletePreparationPlan(founder);
    }

    public long CompletePreparationPlan(int founder)
    {
        ValidateAgent(founder);
        if (_preparationSponsors[founder] != founder)
        {
            return 0;
        }
        for (var agent = 0; agent < _population; agent++)
        {
            if (_preparationSponsors[agent] == founder)
            {
                _preparationSponsors[agent] = -1;
            }
        }
        var remainder = _foundingReserves[founder];
        _foundingReserves[founder] = 0;
        _preparationFounders.Remove(founder);
        _authorityRevision++;
        return remainder;
    }

    public void HandlePreparationLifeEnded(int agent, Func<int, bool> isLiving)
    {
        ValidateAgent(agent);
        ArgumentNullException.ThrowIfNull(isLiving);
        var founder = _preparationSponsors[agent];
        if (founder < 0)
        {
            return;
        }
        if (founder != agent)
        {
            _preparationSponsors[agent] = -1;
            _authorityRevision++;
            return;
        }

        var replacement = -1;
        for (var candidate = 0; candidate < _population; candidate++)
        {
            if (candidate != agent
                && _preparationSponsors[candidate] == founder
                && isLiving(candidate)
                && GetMembershipIndex(candidate, OrganizationKind.Sect) < 0)
            {
                replacement = candidate;
                break;
            }
        }

        for (var candidate = 0; candidate < _population; candidate++)
        {
            if (_preparationSponsors[candidate] == founder)
            {
                _preparationSponsors[candidate] = replacement;
            }
        }
        if (replacement >= 0)
        {
            _preparationSponsors[replacement] = replacement;
            _foundingReserves[replacement] = _foundingReserves[founder];
            var founderIndex = _preparationFounders.IndexOf(founder);
            if (founderIndex >= 0)
            {
                _preparationFounders[founderIndex] = replacement;
                _preparationFounders.Sort();
            }
        }
        else
        {
            _preparationFounders.Remove(founder);
        }
        _foundingReserves[founder] = 0;
        _authorityRevision++;
    }

    public long GetFoundingReserve(int agent)
    {
        ValidateAgent(agent);
        return _foundingReserves[agent];
    }

    public bool TryDepositFoundingReserve(int agent, long amount)
    {
        ValidateAgent(agent);
        if (_preparationSponsors[agent] != agent
            || amount <= 0
            || _foundingReserves[agent] > long.MaxValue - amount)
        {
            return false;
        }

        _foundingReserves[agent] += amount;
        _authorityRevision++;
        return true;
    }

    public long WithdrawFoundingReserve(int agent)
    {
        ValidateAgent(agent);
        var amount = _foundingReserves[agent];
        if (amount == 0)
        {
            return 0;
        }

        _foundingReserves[agent] = 0;
        _authorityRevision++;
        return amount;
    }

    public bool TryConsumeFoundingReserve(int agent, long amount)
    {
        ValidateAgent(agent);
        if (amount < 0 || amount > _foundingReserves[agent])
        {
            return false;
        }
        if (amount == 0)
        {
            return true;
        }

        _foundingReserves[agent] -= amount;
        _authorityRevision++;
        return true;
    }

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
                                relation.Kind == OrganizationDiplomacyKind.Overlord);
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
            history,
            CreateFoundingStrategy(evidence),
            CreateFoundingProfile(evidence));

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
                            OrganizationDiplomacyKind.Overlord,
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
            var memberIndex = Array.FindIndex(members, member =>
                member.AgentIndex == agent && member.Active);
            if (memberIndex < 0)
            {
                throw new InvalidDataException("조직 소속 index와 권위 상태가 일치하지 않습니다.");
            }

            members[memberIndex] = members[memberIndex] with { Active = false };
            SetMembershipIndex(agent, state.Kind, -1);
            var history = AppendHistory(state.History, NewHistory(
                tick,
                OrganizationHistoryKind.MemberDeparted,
                agent,
                null,
                0));
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
        var mergedDiplomacy = primary.Diplomacy
            .Concat(secondary.Diplomacy)
            .Where(relation => relation.OtherOrganizationId != primary.Id
                && relation.OtherOrganizationId != secondary.Id)
            .GroupBy(relation => relation.OtherOrganizationId)
            .Select(group => group.FirstOrDefault(relation =>
                    primary.Diplomacy.Contains(relation)) is var preferred
                && preferred.OtherOrganizationId.Value != 0
                    ? preferred
                    : group.First())
            .OrderBy(relation => relation.OtherOrganizationId.Value)
            .ToArray();
        long mergedTreasury;
        try
        {
            mergedTreasury = checked(
                primary.TreasurySpiritStones + secondary.TreasurySpiritStones);
        }
        catch (OverflowException)
        {
            return Failure(OrganizationCommandStatus.Overflow, "병합 재정이 정수 범위를 넘습니다.");
        }
        var secondaryExternalDiplomacy = secondary.Diplomacy.Where(relation =>
            relation.OtherOrganizationId != primary.Id).ToArray();
        if (secondaryExternalDiplomacy.Any(relation =>
            FindIndex(relation.OtherOrganizationId) < 0))
        {
            return Failure(OrganizationCommandStatus.InvalidTransition, "병합 외교 참조가 끊어져 있습니다.");
        }
        var counterpartUpdates = new List<(int Index, OrganizationAuthorityCheckpoint State)>();
        foreach (var relation in secondaryExternalDiplomacy)
        {
            var counterpartIndex = FindIndex(relation.OtherOrganizationId);
            var counterpart = _organizations[counterpartIndex];
            var withoutSecondary = counterpart.Diplomacy
                .Where(item => item.OtherOrganizationId != secondary.Id)
                .ToArray();
            var hasPrimary = withoutSecondary.Any(item =>
                item.OtherOrganizationId == primary.Id);
            var replacement = hasPrimary
                ? withoutSecondary
                : withoutSecondary.Append(relation with
                {
                    OtherOrganizationId = primary.Id,
                    ChangedTick = tick,
                }).ToArray();
            counterpartUpdates.Add((
                counterpartIndex,
                counterpart with
                {
                    Diplomacy = replacement,
                    Profile = counterpart.Profile with
                    {
                        VassalOrganizationCount = replacement.Count(item =>
                            item.Kind == OrganizationDiplomacyKind.Vassal),
                    },
                    History = AppendHistory(counterpart.History, NewHistory(
                        tick,
                        OrganizationHistoryKind.DiplomacyChanged,
                        counterpart.LeaderAgentIndex,
                        primary.Id,
                        (long)relation.Kind)),
                }));
        }
        var history = AppendHistory(primary.History, NewHistory(
            tick,
            OrganizationHistoryKind.Merged,
            primary.LeaderAgentIndex,
            secondary.Id,
            secondary.TreasurySpiritStones));
        primary = primary with
        {
            TreasurySpiritStones = mergedTreasury,
            Members = mergedMembers,
            Rights = mergedRights,
            Diplomacy = mergedDiplomacy,
            Profile = primary.Profile with
            {
                VassalOrganizationCount = mergedDiplomacy.Count(relation =>
                    relation.Kind == OrganizationDiplomacyKind.Vassal),
            },
            History = history,
        };
        secondary = ChangeStatus(
            secondary with
            {
                LeaderAgentIndex = -1,
                LeaderPersonId = default,
                Members = secondary.Members.Select(member => member with { Active = false }).ToArray(),
                Diplomacy = Array.Empty<OrganizationDiplomacyState>(),
            },
            OrganizationStatus.Merged,
            tick,
            OrganizationHistoryKind.Merged,
            primary.Id);

        _organizations[primaryIndex] = primary;
        _organizations[secondaryIndex] = secondary;
        foreach (var update in counterpartUpdates)
        {
            _organizations[update.Index] = update.State;
        }
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
                state.History,
                state.Strategy,
                state.Profile);
        }

        return new WorldOrganizationSnapshot(
            _authorityRevision,
            organizations,
            CountActive(OrganizationKind.Sect),
            CountActive(OrganizationKind.Clan),
            CountActive(OrganizationKind.Dynasty),
            false,
            "DataUnavailable · 미승인 사회 자질과 종합 세력 순위 공식 없음",
            PreparationPlanCount,
            TotalFoundingReserves);
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
            member => member.AgentIndex == successor.AgentIndex && member.Active);
        members[successorIndex] = successor with { Role = OrganizationRole.Leader };
        return state with
        {
            Status = OrganizationStatus.Active,
            StatusChangedTick = tick,
            LeaderAgentIndex = successor.AgentIndex,
            LeaderPersonId = successor.PersonId,
            Members = members,
            History = AppendHistory(state.History, NewHistory(
                tick,
                OrganizationHistoryKind.LeaderSucceeded,
                successor.AgentIndex,
                null,
                0)),
        };
    }

    private void Dissolve(int index, long tick)
    {
        var state = _organizations[index];
        foreach (var relation in state.Diplomacy)
        {
            var counterpartIndex = FindIndex(relation.OtherOrganizationId);
            if (counterpartIndex < 0 || counterpartIndex == index)
            {
                continue;
            }
            var counterpart = _organizations[counterpartIndex];
            var diplomacy = counterpart.Diplomacy
                .Where(item => item.OtherOrganizationId != state.Id)
                .ToArray();
            _organizations[counterpartIndex] = counterpart with
            {
                Diplomacy = diplomacy,
                Profile = counterpart.Profile with
                {
                    VassalOrganizationCount = diplomacy.Count(item =>
                        item.Kind == OrganizationDiplomacyKind.Vassal),
                },
                History = AppendHistory(counterpart.History, NewHistory(
                    tick,
                    OrganizationHistoryKind.DiplomacyChanged,
                    counterpart.LeaderAgentIndex,
                    state.Id,
                    (long)OrganizationDiplomacyKind.Neutral)),
            };
        }
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
                Diplomacy = Array.Empty<OrganizationDiplomacyState>(),
                Profile = state.Profile with { VassalOrganizationCount = 0 },
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
        History = AppendHistory(state.History, NewHistory(
            tick,
            historyKind,
            state.LeaderAgentIndex,
            other,
            0)),
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

    private static OrganizationStrategyState CreateFoundingStrategy(
        OrganizationFoundingEvidence evidence) => new(
        Math.Clamp(
            evidence.FoundingAptitudeHundredths - evidence.OrganizationOperationAptitudeHundredths,
            -10_000,
            10_000),
        Math.Clamp(evidence.AverageTrustHundredths - 5_000, -10_000, 10_000),
        Math.Clamp(evidence.FoundingAptitudeHundredths, 0, 10_000));

    private static OrganizationKindProfile CreateFoundingProfile(
        OrganizationFoundingEvidence evidence) => new(
        evidence.LivingGenealogyCount,
        evidence.ControlledProvinceCount,
        evidence.VassalOrganizationIds.Length,
        Math.Clamp(evidence.AverageSupportHundredths, 0, 10_000),
        evidence.HasFoundingClan,
        evidence.HasValidRuler,
        IsFormalized: true);

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

    private static OrganizationHistoryEntry[] AppendHistory(
        OrganizationHistoryEntry[] history,
        OrganizationHistoryEntry entry)
    {
        var count = Math.Min(history.Length + 1, OrganizationRules.HistoryRetentionLimit);
        var retained = count - 1;
        var result = new OrganizationHistoryEntry[count];
        if (retained > 0)
        {
            Array.Copy(history, history.Length - retained, result, 0, retained);
        }
        result[^1] = entry;
        return result;
    }

    private static OrganizationAuthorityCheckpoint TrimHistory(
        OrganizationAuthorityCheckpoint organization) =>
        organization.History.Length <= OrganizationRules.HistoryRetentionLimit
            ? organization
            : organization with
            {
                History = organization.History[^OrganizationRules.HistoryRetentionLimit..],
            };

    public void TrimHistoriesToLimit()
    {
        for (var index = 0; index < _organizations.Count; index++)
        {
            _organizations[index] = TrimHistory(_organizations[index]);
        }
    }

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

        if (kind == OrganizationKind.Sect)
        {
            return WorldGeographyRules.GetPoints(2).Any(point =>
                point.NodeId == locationNodeId && point.Resources.HasSpiritVein);
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
        Add(ref hash, checked((ulong)value.Length));
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
