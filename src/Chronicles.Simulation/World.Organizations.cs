using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed partial class World
{
    private void AdvanceOrganizationSystem(long currentMonth)
    {
        _organizations.AdvanceMaintenance(currentMonth);
        var candidate = _organizations.EvaluationCursor;
        TryFoundSectFromLocalRelations(candidate);
    }

    private void TryFoundSectFromLocalRelations(int founder)
    {
        if (_agents.LifeStates[founder] != PersonLifeState.Living
            || _agents.RealmStages[founder] < RealmStage.GoldenCore
            || _agents.SpiritStones[founder] < OrganizationRules.SectFoundingSpiritStoneCost
            || _organizations.HasActiveMembership(founder, OrganizationKind.Sect)
            || _atlas.GetRemainingSpiritStoneSupply(_agents.LocationIds[founder]) <= 0)
        {
            return;
        }

        var pledged = _relations.GetForAgent(founder)
            .Where(relation =>
                relation.TowardOther.TrustDistrust
                    >= OrganizationRules.SectMinimumAverageTrustHundredths
                && relation.OtherTowardSelf.TrustDistrust
                    >= OrganizationRules.SectMinimumAverageTrustHundredths
                && _agents.LifeStates[relation.OtherAgent] == PersonLifeState.Living
                && _agents.LocationIds[relation.OtherAgent] == _agents.LocationIds[founder]
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
            return;
        }

        var averageTrust = checked((int)(pledged.Sum(relation =>
            (long)relation.TowardOther.TrustDistrust
                + relation.OtherTowardSelf.TrustDistrust)
            / (pledged.Length * 2L)));
        var location = WorldMapRules.GetLocation(_agents.LocationIds[founder]);
        var pointX = Math.Clamp(1 + (location.X * 3), 0, 9);
        var pointY = Math.Clamp(1 + (location.Y * 3), 0, 9);
        var physicalPointId = checked((ushort)((pointY * 10) + pointX));
        var point = WorldGeographyRules.GetPoint(location.HeavenlyLayer, physicalPointId);

        // PC_TEMP_REVIEW ORG-020-01: 전용 사회 자질이 승인되기 전에는 정신 종합을
        // 조직운영, 오성 종합을 창설 자질로 임시 매핑한다.
        var evidence = new OrganizationFoundingEvidence(
            OrganizationKind.Sect,
            $"{_agents.Names[founder]}문",
            founder,
            _agents.PersonIdentityIds[founder],
            _agents.SoulIdentityIds[founder],
            _agents.RealmStages[founder],
            _agents.SpiritStones[founder],
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
            pledged.Select(relation => relation.OtherAgent).ToArray(),
            pledged.Select(relation => _agents.PersonIdentityIds[relation.OtherAgent]).ToArray(),
            pledged.Select(relation => _agents.SoulIdentityIds[relation.OtherAgent]).ToArray(),
            LivingGenealogyCount: 0,
            Array.Empty<OrganizationId>());
        var result = _organizations.TryFound(evidence, Tick.Value);
        if (!result.IsApplied)
        {
            return;
        }

        _agents.SpiritStones[founder] = checked(
            _agents.SpiritStones[founder] - result.SpiritStoneCost);
        MarkWealthRankingDirty();
    }
}
