using Chronicles.Core;

namespace Chronicles.Simulation;

/// <summary>
/// 조직 권위의 checkpoint, 상태 hash, 복원 및 손상 검증 작업집합이다.
/// 실시간 조직 전이와 저장 경계를 분리하되 동일 partial store의 불변식을 공유한다.
/// </summary>
internal sealed partial class OrganizationStore
{
    public OrganizationStoreCheckpoint CreateCheckpoint() => new(
        _nextOrganizationId,
        _historySequence,
        _authorityRevision,
        _organizations.ToArray(),
        _lastMaintenanceMonth,
        _evaluationCursor,
        _foundingReserves.ToArray(),
        _preparationSponsors.ToArray());

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
        Add(ref hash, checked((ulong)_foundingReserves.Length));
        foreach (var reserve in _foundingReserves)
        {
            Add(ref hash, unchecked((ulong)reserve));
        }
        Add(ref hash, checked((ulong)_preparationSponsors.Length));
        foreach (var sponsor in _preparationSponsors)
        {
            Add(ref hash, unchecked((ulong)sponsor));
        }
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
            Add(ref hash, checked((ulong)organization.Members.Length));
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
            Add(ref hash, checked((ulong)organization.Rights.Length));
            foreach (var right in organization.Rights)
            {
                Add(ref hash, (ulong)right.Kind);
                Add(ref hash, checked((ulong)right.LocationNodeId.Value));
                Add(ref hash, unchecked((ulong)right.GrantedTick));
            }
            Add(ref hash, checked((ulong)organization.Diplomacy.Length));
            foreach (var diplomacy in organization.Diplomacy)
            {
                Add(ref hash, checked((ulong)diplomacy.OtherOrganizationId.Value));
                Add(ref hash, (ulong)diplomacy.Kind);
                Add(ref hash, unchecked((ulong)diplomacy.ChangedTick));
            }
            Add(ref hash, checked((ulong)organization.History.Length));
            foreach (var history in organization.History)
            {
                Add(ref hash, unchecked((ulong)history.Sequence));
                Add(ref hash, unchecked((ulong)history.Tick));
                Add(ref hash, (ulong)history.Kind);
                Add(ref hash, unchecked((ulong)history.PrimaryAgentIndex));
                Add(ref hash, checked((ulong)(history.OtherOrganizationId?.Value ?? 0)));
                Add(ref hash, unchecked((ulong)history.Value));
            }
            Add(ref hash, unchecked((ulong)organization.Strategy.GrowthVsStabilityHundredths));
            Add(ref hash, unchecked((ulong)organization.Strategy.CooperationVsDominanceHundredths));
            Add(ref hash, unchecked((ulong)organization.Strategy.RiskToleranceHundredths));
            Add(ref hash, unchecked((ulong)organization.Profile.LivingGenealogyCount));
            Add(ref hash, unchecked((ulong)organization.Profile.ControlledProvinceCount));
            Add(ref hash, unchecked((ulong)organization.Profile.VassalOrganizationCount));
            Add(ref hash, unchecked((ulong)organization.Profile.AverageSupportHundredths));
            Add(ref hash, organization.Profile.HasFoundingClan ? 1UL : 0UL);
            Add(ref hash, organization.Profile.HasValidRuler ? 1UL : 0UL);
            Add(ref hash, organization.Profile.IsFormalized ? 1UL : 0UL);
        }
    }

    public static OrganizationStore Restore(
        int population,
        OrganizationStoreCheckpoint checkpoint,
        bool trimHistory = false)
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
        store._organizations.AddRange(trimHistory
            ? checkpoint.Organizations.Select(TrimHistory)
            : checkpoint.Organizations);
        if (checkpoint.FoundingReserves is { } reserves)
        {
            Array.Copy(reserves, store._foundingReserves, reserves.Length);
        }
        if (checkpoint.PreparationSponsors is { } sponsors)
        {
            Array.Copy(sponsors, store._preparationSponsors, sponsors.Length);
            for (var agent = 0; agent < sponsors.Length; agent++)
            {
                if (sponsors[agent] == agent)
                {
                    store._preparationFounders.Add(agent);
                }
            }
        }
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
            || checkpoint.Organizations is null
            || (checkpoint.FoundingReserves is { } reserves
                && (reserves.Length != population || reserves.Any(amount => amount < 0)))
            || (checkpoint.PreparationSponsors is { } sponsors
                && (sponsors.Length != population
                    || sponsors.Any(sponsor => sponsor < -1 || sponsor >= population))))
        {
            return false;
        }

        if (checkpoint.PreparationSponsors is { } preparationSponsors)
        {
            var planCount = 0;
            for (var agent = 0; agent < population; agent++)
            {
                var sponsor = preparationSponsors[agent];
                if (sponsor < 0)
                {
                    continue;
                }
                if (preparationSponsors[sponsor] != sponsor)
                {
                    return false;
                }
                if (sponsor == agent)
                {
                    planCount++;
                }
            }
            if (planCount > OrganizationRules.GetSectPreparationCapacity(population))
            {
                return false;
            }
        }

        var ids = new HashSet<long>();
        var memberships = new HashSet<(int Agent, OrganizationKind Kind)>();
        var historySequences = new HashSet<long>();
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
                || organization.History is null
                || organization.Strategy.GrowthVsStabilityHundredths is < -10_000 or > 10_000
                || organization.Strategy.CooperationVsDominanceHundredths is < -10_000 or > 10_000
                || organization.Strategy.RiskToleranceHundredths is < 0 or > 10_000
                || organization.Profile.LivingGenealogyCount < 0
                || organization.Profile.ControlledProvinceCount < 0
                || organization.Profile.VassalOrganizationCount < 0
                || organization.Profile.AverageSupportHundredths is < 0 or > 10_000
                || (organization.Status == OrganizationStatus.Active
                    && organization.LeaderAgentIndex < 0)
                || (organization.Status is OrganizationStatus.Vacant
                        or OrganizationStatus.Declining
                        or OrganizationStatus.Dissolved
                        or OrganizationStatus.Merged
                    && organization.LeaderAgentIndex >= 0)
                || (organization.Status is OrganizationStatus.Dissolved
                        or OrganizationStatus.Merged
                    && organization.Members.Any(member => member.Active)))
            {
                return false;
            }

            if (organization.LeaderAgentIndex >= 0)
            {
                var leaders = organization.Members.Where(member =>
                    member.Active
                    && member.AgentIndex == organization.LeaderAgentIndex
                    && member.PersonId == organization.LeaderPersonId
                    && member.Role == OrganizationRole.Leader).Count();
                if (leaders != 1)
                {
                    return false;
                }
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
            if (organization.Rights.Distinct().Count() != organization.Rights.Length
                || organization.Diplomacy
                    .Select(item => item.OtherOrganizationId)
                    .Distinct()
                    .Count() != organization.Diplomacy.Length)
            {
                return false;
            }
            foreach (var diplomacy in organization.Diplomacy)
            {
                if (!Enum.IsDefined(diplomacy.Kind)
                    || diplomacy.OtherOrganizationId == organization.Id
                    || diplomacy.ChangedTick < organization.FoundedTick)
                {
                    return false;
                }
            }
            foreach (var history in organization.History)
            {
                maximumHistory = Math.Max(maximumHistory, history.Sequence);
                if (history.Sequence <= 0
                    || !historySequences.Add(history.Sequence)
                    || history.Tick < organization.FoundedTick
                    || !Enum.IsDefined(history.Kind)
                    || history.PrimaryAgentIndex < -1
                    || history.PrimaryAgentIndex >= population)
                {
                    return false;
                }
            }
        }

        foreach (var organization in checkpoint.Organizations)
        {
            foreach (var diplomacy in organization.Diplomacy)
            {
                var counterpart = checkpoint.Organizations.FirstOrDefault(candidate =>
                    candidate.Id == diplomacy.OtherOrganizationId);
                if (counterpart is null)
                {
                    return false;
                }
                var expectedReciprocal = diplomacy.Kind switch
                {
                    OrganizationDiplomacyKind.Vassal => OrganizationDiplomacyKind.Overlord,
                    OrganizationDiplomacyKind.Overlord => OrganizationDiplomacyKind.Vassal,
                    _ => diplomacy.Kind,
                };
                if (!counterpart.Diplomacy.Any(item =>
                    item.OtherOrganizationId == organization.Id
                    && item.Kind == expectedReciprocal))
                {
                    return false;
                }
            }
        }

        return checkpoint.NextOrganizationId > maximumId
            && checkpoint.HistorySequence >= maximumHistory;
    }

}
