using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed partial class World
{
    private void AdvanceOrganizationSystem(long currentMonth)
    {
        var evaluationStart = _organizations.EvaluationCursor;
        _organizations.AdvanceMaintenance(currentMonth);
        var probeCount = Math.Min(Population, OrganizationRules.MonthlyCandidateProbeCount);
        for (var offset = 0; offset < probeCount; offset++)
        {
            var candidate = (evaluationStart + offset) % Population;
            EvaluateOrganizationDecision(candidate);
        }

        Span<int> preparationFounders =
            stackalloc int[OrganizationRules.MaximumSectPreparationPlans];
        var preparationCount = _organizations.CopyPreparationFounders(preparationFounders);
        for (var index = 0; index < preparationCount; index++)
        {
            var founder = preparationFounders[index];
            var alreadyEvaluated = false;
            for (var offset = 0; offset < probeCount; offset++)
            {
                if ((evaluationStart + offset) % Population == founder)
                {
                    alreadyEvaluated = true;
                    break;
                }
            }
            if (!alreadyEvaluated)
            {
                EvaluateOrganizationDecision(founder);
            }
        }

        RecruitSectMembers(currentMonth);
    }

    private void EvaluateOrganizationDecision(int candidate)
    {
        if (_agents.LifeStates[candidate] != PersonLifeState.Living)
        {
            return;
        }

        if (_organizations.TryGetActiveMembershipOrganization(
                candidate,
                OrganizationKind.Sect,
                out var organizationId))
        {
            EvaluateSectMembership(candidate, organizationId);
            return;
        }

        if (_organizations.HasPreparationPlan(candidate)
            && TryFoundSectFromLocalRelations(candidate))
        {
            return;
        }

        if (!TryJoinSectFromLocalRelations(candidate))
        {
            TryFoundSectFromLocalRelations(candidate);
        }
    }

    private bool TryJoinSectFromLocalRelations(int candidate)
    {
        if (_agents.RealmStages[candidate] < RealmStage.QiRefining)
        {
            return false;
        }

        OrganizationId selectedOrganization = default;
        var selectedLeader = -1;
        var selectedLoyalty = 0;
        long selectedScore = long.MinValue;
        foreach (var relation in _relations.GetForAgent(candidate))
        {
            if (_agents.LifeStates[relation.OtherAgent] != PersonLifeState.Living
                || _agents.LocationIds[relation.OtherAgent] != _agents.LocationIds[candidate]
                || relation.TowardOther.TrustDistrust
                    < OrganizationRules.SectMinimumAverageTrustHundredths
                || relation.OtherTowardSelf.TrustDistrust
                    < OrganizationRules.SectMinimumAverageTrustHundredths
                || !_organizations.TryGetActiveMembershipOrganization(
                    relation.OtherAgent,
                    OrganizationKind.Sect,
                    out var organizationId))
            {
                continue;
            }

            var leader = _organizations.GetLeaderAgentIndex(organizationId);
            if (leader < 0 || _agents.LifeStates[leader] != PersonLifeState.Living)
            {
                continue;
            }

            var score = (long)relation.TowardOther.TrustDistrust
                + relation.OtherTowardSelf.TrustDistrust;
            if (score < selectedScore
                || (score == selectedScore
                    && selectedOrganization.Value != 0
                    && organizationId.Value >= selectedOrganization.Value))
            {
                continue;
            }

            selectedOrganization = organizationId;
            selectedLeader = leader;
            selectedScore = score;
            selectedLoyalty = Math.Clamp(
                checked((int)(score / 2)),
                OrganizationRules.LoyaltyActiveMinimumHundredths,
                OrganizationRules.LoyaltyMaximumHundredths);
        }

        if (selectedOrganization.Value == 0)
        {
            return false;
        }

        var result = _organizations.TryJoin(
            selectedOrganization,
            selectedLeader,
            candidate,
            _agents.PersonIdentityIds[candidate],
            _agents.SoulIdentityIds[candidate],
            OrganizationRole.Member,
            selectedLoyalty,
            Tick.Value);
        if (!result.IsApplied)
        {
            return false;
        }

        FinalizePreparationAfterJoin(candidate, selectedOrganization, selectedLeader);
        return true;
    }

    private void FinalizePreparationAfterJoin(
        int candidate,
        OrganizationId organizationId,
        int leader)
    {
        var releasedReserve = _organizations.CancelPreparationParticipation(candidate);
        if (releasedReserve <= 0)
        {
            return;
        }
        var transfer = _organizations.TryAdjustTreasury(
            organizationId,
            leader,
            releasedReserve,
            Tick.Value);
        if (!transfer.IsApplied)
        {
            _agents.SpiritStones[candidate] = checked(
                _agents.SpiritStones[candidate] + releasedReserve);
            MarkWealthRankingDirty();
        }
    }

    private void RecruitSectMembers(long currentMonth)
    {
        Span<OrganizationId> organizationIds =
            stackalloc OrganizationId[OrganizationRules.MaximumActiveSects];
        var organizationCount = _organizations.CopyActiveSectIds(organizationIds);
        Span<int> members = stackalloc int[OrganizationRules.MaximumSectMembers];
        for (var organizationIndex = 0;
             organizationIndex < organizationCount;
             organizationIndex++)
        {
            var organizationId = organizationIds[organizationIndex];
            if ((currentMonth + organizationId.Value) % GameCalendar.MonthsPerYear != 0)
            {
                continue;
            }
            var leader = _organizations.GetLeaderAgentIndex(organizationId);
            var memberCount = _organizations.CopyActiveMemberAgents(organizationId, members);
            if (leader < 0 || memberCount == 0
                || memberCount >= OrganizationRules.MaximumSectMembers)
            {
                continue;
            }
            var recruiter = members[checked((int)(
                ((currentMonth / GameCalendar.MonthsPerYear) + organizationId.Value)
                % memberCount))];
            var candidateRelation = _relations.GetForAgent(recruiter)
                .Where(relation =>
                    relation.TowardOther.TrustDistrust
                        >= OrganizationRules.SectJoinMinimumMutualTrustHundredths
                    && relation.OtherTowardSelf.TrustDistrust
                        >= OrganizationRules.SectJoinMinimumMutualTrustHundredths
                    && _agents.LifeStates[relation.OtherAgent] == PersonLifeState.Living
                    && _agents.LocationIds[relation.OtherAgent] == _agents.LocationIds[recruiter]
                    && !_organizations.HasActiveMembership(
                        relation.OtherAgent,
                        OrganizationKind.Sect))
                .OrderByDescending(relation =>
                    (long)relation.TowardOther.TrustDistrust
                        + relation.OtherTowardSelf.TrustDistrust)
                .ThenBy(relation => relation.OtherAgent)
                .FirstOrDefault();
            if (candidateRelation.Id.Value == 0)
            {
                continue;
            }
            var candidate = candidateRelation.OtherAgent;
            var loyalty = Math.Clamp(
                (candidateRelation.TowardOther.TrustDistrust
                    + candidateRelation.OtherTowardSelf.TrustDistrust) / 2,
                OrganizationRules.LoyaltyActiveMinimumHundredths,
                OrganizationRules.LoyaltyMaximumHundredths);
            var joined = _organizations.TryJoin(
                organizationId,
                leader,
                candidate,
                _agents.PersonIdentityIds[candidate],
                _agents.SoulIdentityIds[candidate],
                OrganizationRole.Member,
                loyalty,
                Tick.Value);
            if (joined.IsApplied)
            {
                FinalizePreparationAfterJoin(candidate, organizationId, leader);
            }
        }
    }

    private void EvaluateSectMembership(int candidate, OrganizationId organizationId)
    {
        var leader = _organizations.GetLeaderAgentIndex(organizationId);
        if (leader < 0 || candidate == leader)
        {
            return;
        }

        var currentLoyalty = _organizations.GetMembershipLoyalty(
            candidate,
            OrganizationKind.Sect);
        var leaderRelation = _relations.GetForAgent(candidate)
            .FirstOrDefault(relation => relation.OtherAgent == leader);
        var targetLoyalty = leaderRelation.Id.Value == 0
            ? Math.Max(0, currentLoyalty - 100)
            : Math.Clamp(
                (leaderRelation.TowardOther.TrustDistrust
                    + leaderRelation.OtherTowardSelf.TrustDistrust) / 2,
                0,
                OrganizationRules.LoyaltyMaximumHundredths);
        var loyaltyStep = Math.Clamp(targetLoyalty - currentLoyalty, -250, 250);
        if (loyaltyStep != 0)
        {
            var loyaltyResult = _organizations.TryUpdateLoyalty(
                organizationId,
                authorizedByAgent: candidate,
                memberAgent: candidate,
                replacementLoyaltyHundredths: currentLoyalty + loyaltyStep,
                Tick.Value,
                agent => _agents.LifeStates[agent] == PersonLifeState.Living,
                GetRealmRankingScore);
            if (!loyaltyResult.IsApplied
                || !_organizations.HasActiveMembership(candidate, OrganizationKind.Sect))
            {
                return;
            }
            currentLoyalty += loyaltyStep;
        }

        if (currentLoyalty < 8_000 || _agents.SpiritStones[candidate] < 1_000)
        {
            return;
        }

        var contribution = Math.Clamp(_agents.SpiritStones[candidate] / 100, 1, 100);
        if (_organizations.TryAdjustTreasury(
                organizationId,
                leader,
                contribution,
                Tick.Value).IsApplied)
        {
            _agents.SpiritStones[candidate] -= contribution;
            MarkWealthRankingDirty();
        }
    }

    private bool TryFoundSectFromLocalRelations(int founder)
    {
        var location = _generation.Map.GetLocation(_agents.LocationIds[founder]);
        var point = GetSecondHeavenPointForLocation(location);
        if (_agents.LifeStates[founder] != PersonLifeState.Living
            || _agents.RealmStages[founder] < RealmStage.GoldenCore
            || _agents.Minds[founder] < OrganizationRules.SectMinimumOrganizationOperationHundredths
            || _agents.Comprehensions[founder] < OrganizationRules.SectMinimumFoundingAptitudeHundredths
            || _organizations.HasActiveMembership(founder, OrganizationKind.Sect)
            || location.HeavenlyLayer != 2
            || !point.Resources.HasSpiritVein)
        {
            return false;
        }

        var pledged = _relations.GetForAgent(founder)
            .Where(relation =>
                relation.TowardOther.TrustDistrust > 0
                && relation.OtherTowardSelf.TrustDistrust > 0
                && _agents.LifeStates[relation.OtherAgent] == PersonLifeState.Living
                && _organizations.CanPledgeForPreparation(relation.OtherAgent, founder)
                && !_organizations.HasActiveMembership(
                    relation.OtherAgent,
                    OrganizationKind.Sect))
            .OrderByDescending(relation =>
                (long)relation.TowardOther.TrustDistrust
                    + relation.OtherTowardSelf.TrustDistrust)
            .ThenBy(relation => relation.OtherAgent)
            .Take(OrganizationRules.SectFounderMinimumPledgedMembers)
            .ToArray();
        if (pledged.Length < OrganizationRules.SectFounderMinimumPledgedMembers)
        {
            return false;
        }

        var pledgedAgents = pledged.Select(relation => relation.OtherAgent).ToArray();
        if (!_organizations.TryRegisterPreparationPlan(founder, pledgedAgents))
        {
            return false;
        }
        var contributors = new[] { founder }.Concat(pledgedAgents).ToArray();

        var averageTrust = checked((int)(pledged.Sum(relation =>
            (long)relation.TowardOther.TrustDistrust
                + relation.OtherTowardSelf.TrustDistrust)
            / (pledged.Length * 2L)));
        if (averageTrust < OrganizationRules.SectMinimumAverageTrustHundredths)
        {
            foreach (var relation in pledged)
            {
                _relations.ApplyMutualInteraction(
                    founder,
                    relation.OtherAgent,
                    _agents.PersonIdentityIds[founder],
                    _agents.PersonIdentityIds[relation.OtherAgent],
                    RelationInteractionKind.Dialogue,
                    Tick.Value);
            }
            return false;
        }
        var availableFoundingStones = contributors.Aggregate(
            _organizations.GetFoundingReserve(founder),
            (sum, contributor) => sum > long.MaxValue - _agents.SpiritStones[contributor]
                ? long.MaxValue
                : sum + _agents.SpiritStones[contributor]);
        if (availableFoundingStones < OrganizationRules.SectFoundingSpiritStoneCost)
        {
            return false;
        }
        // PC_TEMP_REVIEW ORG-020-01: 전용 사회 자질이 승인되기 전에는 정신 종합을
        // 조직운영, 오성 종합을 창설 자질로 임시 매핑한다.
        var evidence = new OrganizationFoundingEvidence(
            OrganizationKind.Sect,
            $"{_agents.Names[founder]}문",
            founder,
            _agents.PersonIdentityIds[founder],
            _agents.SoulIdentityIds[founder],
            _agents.RealmStages[founder],
            availableFoundingStones,
            _agents.Minds[founder],
            _agents.Comprehensions[founder],
            LeadershipHundredths: _agents.Minds[founder],
            averageTrust,
            AverageSupportHundredths: 0,
            ControlledProvinceCount: 0,
            HasActualHeadquartersSpiritVeinRight: true,
            HasActualResidenceRight: false,
            HasFoundingClan: false,
            HasValidRuler: false,
            point.NodeId,
            pledgedAgents,
            pledged.Select(relation => _agents.PersonIdentityIds[relation.OtherAgent]).ToArray(),
            pledged.Select(relation => _agents.SoulIdentityIds[relation.OtherAgent]).ToArray(),
            LivingGenealogyCount: 0,
            Array.Empty<OrganizationId>());
        var result = _organizations.TryFound(evidence, Tick.Value);
        if (!result.IsApplied)
        {
            return false;
        }

        var reserveContribution = Math.Min(
            _organizations.GetFoundingReserve(founder),
            result.SpiritStoneCost);
        if (!_organizations.TryConsumeFoundingReserve(founder, reserveContribution))
        {
            throw new InvalidOperationException("검증된 종문 창설 적립금과 권위 상태가 일치하지 않습니다.");
        }
        var remainingContribution = result.SpiritStoneCost - reserveContribution;
        foreach (var contributor in contributors)
        {
            var paid = Math.Min(_agents.SpiritStones[contributor], remainingContribution);
            _agents.SpiritStones[contributor] -= paid;
            remainingContribution -= paid;
            if (remainingContribution == 0)
            {
                break;
            }
        }
        if (remainingContribution != 0)
        {
            throw new InvalidOperationException("검증된 종문 공동 출자액과 실제 지갑이 일치하지 않습니다.");
        }
        var unusedReserve = _organizations.CompletePreparationPlan(founder);
        if (unusedReserve > 0)
        {
            _agents.SpiritStones[founder] = checked(
                _agents.SpiritStones[founder] + unusedReserve);
        }
        MarkWealthRankingDirty();
        return true;
    }

    private bool TryPrepareSectSiteTravel(int agent)
    {
        if (_agents.RealmStages[agent] < RealmStage.GoldenCore
            || _agents.Minds[agent] < OrganizationRules.SectMinimumOrganizationOperationHundredths
            || _agents.Comprehensions[agent] < OrganizationRules.SectMinimumFoundingAptitudeHundredths
            || _organizations.HasActiveMembership(agent, OrganizationKind.Sect))
        {
            return false;
        }

        var current = _generation.Map.GetLocation(_agents.LocationIds[agent]);
        if (current.HeavenlyLayer != 2
            || GetSecondHeavenPointForLocation(current).Resources.HasSpiritVein)
        {
            return false;
        }

        var perceived = _atlas.GetPerceivedDestinations(
            agent,
            current.Id,
            _agents.RealmStages[agent]);
        for (var index = 0; index < perceived.Count; index++)
        {
            var destination = _generation.Map.GetLocation(perceived[index]);
            if (destination.HeavenlyLayer == 2
                && GetSecondHeavenPointForLocation(destination).Resources.HasSpiritVein
                && PrepareTravelTo(agent, destination.Id))
            {
                return true;
            }
        }
        return false;
    }

    private static PointHeavenViewDefinition GetSecondHeavenPointForLocation(
        MapLocationDefinition location)
    {
        var pointX = Math.Clamp(1 + (location.X * 3), 0, 9);
        var pointY = Math.Clamp(1 + (location.Y * 3), 0, 9);
        var physicalPointId = checked((ushort)((pointY * 10) + pointX));
        return WorldGeographyRules.GetPoint(2, physicalPointId);
    }
}
