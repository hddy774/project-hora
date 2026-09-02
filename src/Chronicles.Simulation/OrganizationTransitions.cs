using Chronicles.Core;

namespace Chronicles.Simulation;

internal sealed partial class OrganizationStore
{
    public bool TryGetActiveMembershipOrganization(
        int agent,
        OrganizationKind kind,
        out OrganizationId organizationId)
    {
        var index = GetMembershipIndex(agent, kind);
        if (index < 0)
        {
            organizationId = default;
            return false;
        }

        organizationId = _organizations[index].Id;
        return true;
    }

    public int GetLeaderAgentIndex(OrganizationId organizationId)
    {
        var index = FindIndex(organizationId);
        return index < 0 ? -1 : _organizations[index].LeaderAgentIndex;
    }

    public int GetMembershipLoyalty(int agent, OrganizationKind kind)
    {
        var index = GetMembershipIndex(agent, kind);
        if (index < 0)
        {
            return 0;
        }

        var member = _organizations[index].Members.First(candidate =>
            candidate.AgentIndex == agent && candidate.Active);
        return member.LoyaltyHundredths;
    }

    public OrganizationCommandResult TryJoin(
        OrganizationId organizationId,
        int authorizedByAgent,
        int joiningAgent,
        PersonIdentityId personId,
        SoulIdentityId soulId,
        OrganizationRole role,
        int loyaltyHundredths,
        long tick)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        ValidateAgent(authorizedByAgent);
        ValidateAgent(joiningAgent);
        var index = FindIndex(organizationId);
        if (index < 0)
        {
            return Failure(OrganizationCommandStatus.OrganizationNotFound, "가입 대상 조직이 없습니다.");
        }

        var state = _organizations[index];
        if (state.Status != OrganizationStatus.Active)
        {
            return Failure(OrganizationCommandStatus.OrganizationInactive, "활성 조직에만 가입할 수 있습니다.");
        }
        if (state.LeaderAgentIndex != authorizedByAgent)
        {
            return Failure(OrganizationCommandStatus.PermissionDenied, "조직 지도자의 승인이 필요합니다.");
        }
        if (personId.Value == 0
            || soulId.Value == 0
            || role is OrganizationRole.Leader or OrganizationRole.Founder
            || loyaltyHundredths is < 0 or > OrganizationRules.LoyaltyMaximumHundredths)
        {
            return Failure(OrganizationCommandStatus.InvalidEvidence, "가입 권위 입력이 잘못되었습니다.");
        }
        if (GetMembershipIndex(joiningAgent, state.Kind) >= 0)
        {
            return Failure(OrganizationCommandStatus.MembershipConflict, "같은 종류의 활성 조직에 이미 소속되어 있습니다.");
        }
        if (state.Members.Count(member => member.Active)
            >= OrganizationRules.GetMemberCapacity(state.Kind))
        {
            return Failure(OrganizationCommandStatus.MemberCapacityReached, "조직 구성원 상한에 도달했습니다.");
        }

        var member = new OrganizationMembershipState(
            joiningAgent,
            personId,
            soulId,
            role,
            tick,
            loyaltyHundredths,
            true);
        _organizations[index] = state with
        {
            Members = state.Members.Append(member).ToArray(),
            History = AppendHistory(state.History, NewHistory(
                tick,
                OrganizationHistoryKind.MemberJoined,
                joiningAgent,
                null,
                loyaltyHundredths)),
        };
        SetMembershipIndex(joiningAgent, state.Kind, index);
        _authorityRevision++;
        return new OrganizationCommandResult(
            OrganizationCommandStatus.Applied,
            organizationId,
            0,
            "조직 가입 완료");
    }

    public OrganizationCommandResult TryLeave(
        OrganizationId organizationId,
        int leavingAgent,
        long tick,
        Func<int, bool> isLiving,
        Func<int, long> successionScore,
        bool loyaltyBroken = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        ValidateAgent(leavingAgent);
        var index = FindIndex(organizationId);
        if (index < 0)
        {
            return Failure(OrganizationCommandStatus.OrganizationNotFound, "이탈 대상 조직이 없습니다.");
        }

        var state = _organizations[index];
        var members = state.Members.ToArray();
        var memberIndex = Array.FindIndex(members, member =>
            member.AgentIndex == leavingAgent && member.Active);
        if (memberIndex < 0 || GetMembershipIndex(leavingAgent, state.Kind) != index)
        {
            return Failure(OrganizationCommandStatus.InvalidTransition, "활성 소속을 찾을 수 없습니다.");
        }

        members[memberIndex] = members[memberIndex] with { Active = false };
        state = state with
        {
            Members = members,
            History = AppendHistory(state.History, NewHistory(
                tick,
                OrganizationHistoryKind.MemberDeparted,
                leavingAgent,
                null,
                loyaltyBroken ? 1 : 0)),
        };
        SetMembershipIndex(leavingAgent, state.Kind, -1);
        if (state.LeaderAgentIndex == leavingAgent)
        {
            state = ApplySuccession(state, tick, isLiving, successionScore);
        }
        if (!state.Members.Any(member => member.Active))
        {
            _organizations[index] = state;
            Dissolve(index, tick);
        }
        else
        {
            _organizations[index] = state;
            _authorityRevision++;
        }

        return new OrganizationCommandResult(
            OrganizationCommandStatus.Applied,
            organizationId,
            0,
            loyaltyBroken ? "충심 붕괴로 조직 이탈" : "조직 이탈 완료");
    }

    public OrganizationCommandResult TryChangeRole(
        OrganizationId organizationId,
        int authorizedByAgent,
        int memberAgent,
        OrganizationRole replacementRole,
        long tick)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        var index = FindIndex(organizationId);
        if (index < 0)
        {
            return Failure(OrganizationCommandStatus.OrganizationNotFound, "역할 변경 대상 조직이 없습니다.");
        }

        var state = _organizations[index];
        if (state.Status != OrganizationStatus.Active
            || state.LeaderAgentIndex != authorizedByAgent)
        {
            return Failure(OrganizationCommandStatus.PermissionDenied, "활성 지도자만 역할을 변경할 수 있습니다.");
        }
        if (replacementRole is OrganizationRole.Leader or OrganizationRole.Founder)
        {
            return Failure(OrganizationCommandStatus.InvalidEvidence, "지도자 승계는 별도 전이를 사용해야 합니다.");
        }

        var members = state.Members.ToArray();
        var memberIndex = Array.FindIndex(members, member =>
            member.AgentIndex == memberAgent && member.Active);
        if (memberIndex < 0)
        {
            return Failure(OrganizationCommandStatus.InvalidTransition, "활성 구성원이 아닙니다.");
        }
        if (members[memberIndex].Role == replacementRole)
        {
            return Failure(OrganizationCommandStatus.Duplicate, "이미 같은 역할입니다.");
        }

        members[memberIndex] = members[memberIndex] with { Role = replacementRole };
        _organizations[index] = state with
        {
            Members = members,
            History = AppendHistory(state.History, NewHistory(
                tick,
                OrganizationHistoryKind.RoleChanged,
                memberAgent,
                null,
                (long)replacementRole)),
        };
        _authorityRevision++;
        return new OrganizationCommandResult(
            OrganizationCommandStatus.Applied,
            organizationId,
            0,
            "조직 역할 변경 완료");
    }

    public OrganizationCommandResult TryUpdateLoyalty(
        OrganizationId organizationId,
        int authorizedByAgent,
        int memberAgent,
        int replacementLoyaltyHundredths,
        long tick,
        Func<int, bool> isLiving,
        Func<int, long> successionScore)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        if (replacementLoyaltyHundredths is < 0 or > OrganizationRules.LoyaltyMaximumHundredths)
        {
            return Failure(OrganizationCommandStatus.InvalidEvidence, "충심 범위는 0~10,000입니다.");
        }

        var index = FindIndex(organizationId);
        if (index < 0)
        {
            return Failure(OrganizationCommandStatus.OrganizationNotFound, "충심 변경 대상 조직이 없습니다.");
        }
        var state = _organizations[index];
        if (state.LeaderAgentIndex != authorizedByAgent && memberAgent != authorizedByAgent)
        {
            return Failure(OrganizationCommandStatus.PermissionDenied, "지도자 또는 본인의 충심만 변경할 수 있습니다.");
        }

        var members = state.Members.ToArray();
        var memberIndex = Array.FindIndex(members, member =>
            member.AgentIndex == memberAgent && member.Active);
        if (memberIndex < 0)
        {
            return Failure(OrganizationCommandStatus.InvalidTransition, "활성 구성원이 아닙니다.");
        }
        if (members[memberIndex].LoyaltyHundredths == replacementLoyaltyHundredths)
        {
            return Failure(OrganizationCommandStatus.Duplicate, "충심 수치가 변하지 않았습니다.");
        }

        members[memberIndex] = members[memberIndex] with
        {
            LoyaltyHundredths = replacementLoyaltyHundredths,
        };
        _organizations[index] = state with
        {
            Members = members,
            History = AppendHistory(state.History, NewHistory(
                tick,
                OrganizationHistoryKind.LoyaltyChanged,
                memberAgent,
                null,
                replacementLoyaltyHundredths)),
        };
        _authorityRevision++;
        if (replacementLoyaltyHundredths < OrganizationRules.LoyaltyStrainedMinimumHundredths)
        {
            return TryLeave(
                organizationId,
                memberAgent,
                tick,
                isLiving,
                successionScore,
                loyaltyBroken: true);
        }

        return new OrganizationCommandResult(
            OrganizationCommandStatus.Applied,
            organizationId,
            0,
            "조직 충심 변경 완료");
    }

    public OrganizationCommandResult TryAdjustTreasury(
        OrganizationId organizationId,
        int authorizedByAgent,
        long delta,
        long tick)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        var index = FindIndex(organizationId);
        if (index < 0)
        {
            return Failure(OrganizationCommandStatus.OrganizationNotFound, "재정 변경 대상 조직이 없습니다.");
        }
        var state = _organizations[index];
        if (state.Status != OrganizationStatus.Active
            || state.LeaderAgentIndex != authorizedByAgent)
        {
            return Failure(OrganizationCommandStatus.PermissionDenied, "활성 지도자만 재정을 변경할 수 있습니다.");
        }
        if (delta == 0)
        {
            return Failure(OrganizationCommandStatus.Duplicate, "재정 변동액이 0입니다.");
        }

        long replacement;
        try
        {
            replacement = checked(state.TreasurySpiritStones + delta);
        }
        catch (OverflowException)
        {
            return Failure(OrganizationCommandStatus.Overflow, "조직 재정이 정수 범위를 넘습니다.");
        }
        if (replacement < 0)
        {
            return Failure(OrganizationCommandStatus.InsufficientAssets, "조직 영석이 부족합니다.");
        }

        _organizations[index] = state with
        {
            TreasurySpiritStones = replacement,
            History = AppendHistory(state.History, NewHistory(
                tick,
                OrganizationHistoryKind.TreasuryAdjusted,
                authorizedByAgent,
                null,
                delta)),
        };
        _authorityRevision++;
        return new OrganizationCommandResult(
            OrganizationCommandStatus.Applied,
            organizationId,
            0,
            "조직 재정 변경 완료");
    }

    public OrganizationCommandResult TrySetDiplomacy(
        OrganizationId organizationId,
        int authorizedByAgent,
        OrganizationId otherOrganizationId,
        OrganizationDiplomacyKind kind,
        long tick)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        var firstIndex = FindIndex(organizationId);
        var secondIndex = FindIndex(otherOrganizationId);
        if (firstIndex < 0 || secondIndex < 0 || firstIndex == secondIndex)
        {
            return Failure(OrganizationCommandStatus.OrganizationNotFound, "외교 대상 조직이 없습니다.");
        }
        var first = _organizations[firstIndex];
        var second = _organizations[secondIndex];
        if (first.Status != OrganizationStatus.Active || second.Status != OrganizationStatus.Active)
        {
            return Failure(OrganizationCommandStatus.OrganizationInactive, "활성 조직끼리만 외교를 변경할 수 있습니다.");
        }
        if (first.LeaderAgentIndex != authorizedByAgent)
        {
            return Failure(OrganizationCommandStatus.PermissionDenied, "조직 지도자만 외교를 변경할 수 있습니다.");
        }
        if (!Enum.IsDefined(kind) || kind == OrganizationDiplomacyKind.Overlord)
        {
            return Failure(OrganizationCommandStatus.InvalidEvidence, "외교 상태가 잘못되었습니다.");
        }
        if (kind == OrganizationDiplomacyKind.Vassal
            && (WouldCreateVassalCycle(first.Id, second.Id)
                || first.Diplomacy.Count(item => item.Kind == OrganizationDiplomacyKind.Vassal)
                    >= OrganizationRules.MaximumDirectSubsects))
        {
            return Failure(OrganizationCommandStatus.InvalidTransition, "봉신 순환 또는 직속 상한을 위반합니다.");
        }

        var reciprocalKind = kind == OrganizationDiplomacyKind.Vassal
            ? OrganizationDiplomacyKind.Overlord
            : kind;
        var firstDiplomacy = ReplaceDiplomacy(first.Diplomacy, second.Id, kind, tick);
        var secondDiplomacy = ReplaceDiplomacy(second.Diplomacy, first.Id, reciprocalKind, tick);
        if (firstDiplomacy.SequenceEqual(first.Diplomacy)
            && secondDiplomacy.SequenceEqual(second.Diplomacy))
        {
            return Failure(OrganizationCommandStatus.Duplicate, "외교 상태가 변하지 않았습니다.");
        }

        _organizations[firstIndex] = first with
        {
            Diplomacy = firstDiplomacy,
            Profile = first.Profile with
            {
                VassalOrganizationCount = firstDiplomacy.Count(item =>
                    item.Kind == OrganizationDiplomacyKind.Vassal),
            },
            History = AppendHistory(first.History, NewHistory(
                tick,
                OrganizationHistoryKind.DiplomacyChanged,
                authorizedByAgent,
                second.Id,
                (long)kind)),
        };
        _organizations[secondIndex] = second with
        {
            Diplomacy = secondDiplomacy,
            Profile = second.Profile with
            {
                VassalOrganizationCount = secondDiplomacy.Count(item =>
                    item.Kind == OrganizationDiplomacyKind.Vassal),
            },
            History = AppendHistory(second.History, NewHistory(
                tick,
                OrganizationHistoryKind.DiplomacyChanged,
                second.LeaderAgentIndex,
                first.Id,
                (long)reciprocalKind)),
        };
        _authorityRevision++;
        return new OrganizationCommandResult(
            OrganizationCommandStatus.Applied,
            organizationId,
            0,
            "조직 외교 변경 완료");
    }

    public OrganizationCommandResult TrySplit(
        OrganizationId sourceOrganizationId,
        int authorizedByAgent,
        string newName,
        int newLeaderAgent,
        int[] movingAgents,
        long transferredSpiritStones,
        OrganizationRightState[] transferredRights,
        long tick)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        ArgumentNullException.ThrowIfNull(movingAgents);
        ArgumentNullException.ThrowIfNull(transferredRights);
        var sourceIndex = FindIndex(sourceOrganizationId);
        if (sourceIndex < 0)
        {
            return Failure(OrganizationCommandStatus.OrganizationNotFound, "분열 원본 조직이 없습니다.");
        }

        var source = _organizations[sourceIndex];
        if (source.Status != OrganizationStatus.Active
            || source.LeaderAgentIndex != authorizedByAgent)
        {
            return Failure(OrganizationCommandStatus.PermissionDenied, "활성 지도자만 조직 분열을 승인할 수 있습니다.");
        }
        if (string.IsNullOrWhiteSpace(newName)
            || movingAgents.Length < 2
            || movingAgents.Distinct().Count() != movingAgents.Length
            || !movingAgents.Contains(newLeaderAgent)
            || movingAgents.Contains(source.LeaderAgentIndex)
            || transferredSpiritStones < 0
            || transferredSpiritStones > source.TreasurySpiritStones)
        {
            return Failure(OrganizationCommandStatus.InvalidEvidence, "분열 입력이 잘못되었습니다.");
        }

        var movingMembers = source.Members
            .Where(member => member.Active && movingAgents.Contains(member.AgentIndex))
            .ToArray();
        if (movingMembers.Length != movingAgents.Length
            || source.Members.Count(member => member.Active) - movingMembers.Length < 1
            || movingMembers.Length > OrganizationRules.GetMemberCapacity(source.Kind)
            || transferredRights.Any(right => !source.Rights.Contains(right))
            || transferredRights.Distinct().Count() != transferredRights.Length)
        {
            return Failure(OrganizationCommandStatus.InvalidEvidence, "구성원·권리·상한 근거가 일치하지 않습니다.");
        }

        var newId = new OrganizationId(_nextOrganizationId);
        var sourceMembers = source.Members.ToArray();
        foreach (var moving in movingMembers)
        {
            var memberIndex = Array.FindIndex(sourceMembers, member =>
                member.AgentIndex == moving.AgentIndex && member.Active);
            sourceMembers[memberIndex] = sourceMembers[memberIndex] with { Active = false };
        }
        var newMembers = movingMembers.Select(member => member with
        {
            Role = member.AgentIndex == newLeaderAgent
                ? OrganizationRole.Leader
                : OrganizationRole.Member,
            JoinedTick = tick,
        }).ToArray();
        var splitRights = transferredRights.Select(right => right with
        {
            GrantedTick = tick,
        }).ToArray();
        var newLeader = newMembers.First(member => member.AgentIndex == newLeaderAgent);
        var sourceHistory = AppendHistory(source.History, NewHistory(
            tick,
            OrganizationHistoryKind.Split,
            authorizedByAgent,
            newId,
            transferredSpiritStones));
        var splitHistory = new[]
        {
            NewHistory(
                tick,
                OrganizationHistoryKind.Split,
                newLeaderAgent,
                source.Id,
                transferredSpiritStones),
        };
        var splitState = new OrganizationAuthorityCheckpoint(
            newId,
            source.Kind,
            OrganizationStatus.Active,
            newName.Trim(),
            newLeaderAgent,
            newLeader.PersonId,
            newLeaderAgent,
            newLeader.PersonId,
            tick,
            tick,
            transferredSpiritStones,
            source.ReputationHundredths / 2,
            newMembers,
            splitRights,
            Array.Empty<OrganizationDiplomacyState>(),
            splitHistory,
            source.Strategy,
            source.Profile);

        source = source with
        {
            TreasurySpiritStones = source.TreasurySpiritStones - transferredSpiritStones,
            ReputationHundredths = source.ReputationHundredths - (source.ReputationHundredths / 2),
            Members = sourceMembers,
            Rights = source.Rights.Except(transferredRights).ToArray(),
            History = sourceHistory,
        };

        _organizations[sourceIndex] = source;
        var splitIndex = _organizations.Count;
        _organizations.Add(splitState);
        foreach (var member in newMembers)
        {
            SetMembershipIndex(member.AgentIndex, source.Kind, splitIndex);
        }
        _nextOrganizationId++;
        _authorityRevision++;
        return new OrganizationCommandResult(
            OrganizationCommandStatus.Applied,
            newId,
            0,
            "조직 분열 완료");
    }

    public OrganizationCommandResult TryTransferRight(
        OrganizationId sourceOrganizationId,
        int authorizedByAgent,
        OrganizationId targetOrganizationId,
        OrganizationRightState right,
        long tick)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        var sourceIndex = FindIndex(sourceOrganizationId);
        var targetIndex = FindIndex(targetOrganizationId);
        if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex)
        {
            return Failure(OrganizationCommandStatus.OrganizationNotFound, "권리 이전 조직이 없습니다.");
        }
        var source = _organizations[sourceIndex];
        var target = _organizations[targetIndex];
        if (source.Status != OrganizationStatus.Active
            || target.Status != OrganizationStatus.Active
            || source.LeaderAgentIndex != authorizedByAgent)
        {
            return Failure(OrganizationCommandStatus.PermissionDenied, "활성 원본 조직 지도자의 승인이 필요합니다.");
        }
        if (!source.Rights.Contains(right)
            || target.Rights.Any(item => item.Kind == right.Kind
                && item.LocationNodeId == right.LocationNodeId))
        {
            return Failure(OrganizationCommandStatus.InvalidEvidence, "이전할 실제 권리를 찾을 수 없거나 대상에 중복됩니다.");
        }

        var transferred = right with { GrantedTick = tick };
        _organizations[sourceIndex] = source with
        {
            Rights = source.Rights.Where(item => item != right).ToArray(),
            History = AppendHistory(source.History, NewHistory(
                tick,
                OrganizationHistoryKind.RightTransferred,
                authorizedByAgent,
                target.Id,
                right.LocationNodeId.Value)),
        };
        _organizations[targetIndex] = target with
        {
            Rights = target.Rights.Append(transferred).ToArray(),
            History = AppendHistory(target.History, NewHistory(
                tick,
                OrganizationHistoryKind.RightTransferred,
                target.LeaderAgentIndex,
                source.Id,
                right.LocationNodeId.Value)),
        };
        _authorityRevision++;
        return new OrganizationCommandResult(
            OrganizationCommandStatus.Applied,
            target.Id,
            0,
            "조직 권리 이전 완료");
    }

    public OrganizationCommandResult TryReconnectSoul(
        OrganizationId organizationId,
        int agent,
        PersonIdentityId previousPersonId,
        PersonIdentityId reincarnatedPersonId,
        SoulIdentityId soulId,
        long tick)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        ValidateAgent(agent);
        var index = FindIndex(organizationId);
        if (index < 0)
        {
            return Failure(OrganizationCommandStatus.OrganizationNotFound, "재연결 대상 조직이 없습니다.");
        }
        var state = _organizations[index];
        if (state.Status != OrganizationStatus.Active
            || previousPersonId.Value == 0
            || reincarnatedPersonId.Value == 0
            || previousPersonId == reincarnatedPersonId
            || soulId.Value == 0
            || GetMembershipIndex(agent, state.Kind) >= 0)
        {
            return Failure(OrganizationCommandStatus.InvalidEvidence, "동일 Soul 재연결 근거가 잘못되었습니다.");
        }

        var members = state.Members.ToArray();
        var memberIndex = Array.FindLastIndex(members, member =>
            !member.Active
            && member.AgentIndex == agent
            && member.PersonId == previousPersonId
            && member.SoulId == soulId);
        if (memberIndex < 0)
        {
            return Failure(OrganizationCommandStatus.InvalidTransition, "이전 생의 비활성 소속을 찾을 수 없습니다.");
        }

        var previousMembership = members[memberIndex];
        var reconnectedMembership = previousMembership with
        {
            PersonId = reincarnatedPersonId,
            Role = OrganizationRole.Member,
            JoinedTick = tick,
            LoyaltyHundredths = Math.Max(
                previousMembership.LoyaltyHundredths,
                OrganizationRules.LoyaltyActiveMinimumHundredths),
            Active = true,
        };
        _organizations[index] = state with
        {
            Members = members.Append(reconnectedMembership).ToArray(),
            History = AppendHistory(state.History, NewHistory(
                tick,
                OrganizationHistoryKind.SoulReconnected,
                agent,
                null,
                unchecked((long)soulId.Value))),
        };
        SetMembershipIndex(agent, state.Kind, index);
        _authorityRevision++;
        return new OrganizationCommandResult(
            OrganizationCommandStatus.Applied,
            organizationId,
            0,
            "동일 Soul 조직 재연결 완료");
    }

    private bool WouldCreateVassalCycle(OrganizationId parentId, OrganizationId childId)
    {
        var pending = new Stack<OrganizationId>();
        var visited = new HashSet<OrganizationId>();
        pending.Push(childId);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!visited.Add(current))
            {
                continue;
            }
            if (current == parentId)
            {
                return true;
            }

            var index = FindIndex(current);
            if (index < 0)
            {
                continue;
            }
            foreach (var relation in _organizations[index].Diplomacy.Where(item =>
                item.Kind == OrganizationDiplomacyKind.Vassal))
            {
                pending.Push(relation.OtherOrganizationId);
            }
        }

        return false;
    }

    private static OrganizationDiplomacyState[] ReplaceDiplomacy(
        OrganizationDiplomacyState[] source,
        OrganizationId otherOrganizationId,
        OrganizationDiplomacyKind kind,
        long tick)
    {
        var index = Array.FindIndex(source, item =>
            item.OtherOrganizationId == otherOrganizationId);
        if (index >= 0 && source[index].Kind == kind)
        {
            return source;
        }
        if (index < 0)
        {
            return source.Append(new OrganizationDiplomacyState(
                otherOrganizationId,
                kind,
                tick)).ToArray();
        }

        var replacement = source.ToArray();
        replacement[index] = replacement[index] with { Kind = kind, ChangedTick = tick };
        return replacement;
    }
}
