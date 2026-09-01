using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed partial class World
{
    private enum ContentDiscoveryTrigger : byte
    {
        Travel = 1,
        Hunt = 2,
    }

    private void TryDiscoverSecretRealm(int agentIndex, ContentDiscoveryTrigger trigger)
    {
        var chance = trigger == ContentDiscoveryTrigger.Travel
            ? ContentDiscoveryRules.TravelDiscoveryChanceBasisPoints
            : ContentDiscoveryRules.HuntDiscoveryChanceBasisPoints;
        var locationId = _agents.LocationIds[agentIndex];
        var roll = ContentDiscoveryRules.CreateDeterministicRoll(
            Seed,
            Tick.Value,
            agentIndex,
            locationId,
            (byte)trigger);
        if (roll % 10_000UL >= checked((ulong)chance)
            || !_content.TryFindUndiscoveredSecretRealm(
                ContentDiscovererRef.Observer,
                locationId,
                roll,
                out var secretRealmId))
        {
            return;
        }

        var agentResult = _content.TryDiscover(new DiscoverContentCommand(
            secretRealmId,
            new ContentDiscovererRef(ContentDiscovererKind.Agent, agentIndex),
            ContentDiscoveryState.Located,
            NextContentSourceResultId(),
            Tick.Value));
        if (!agentResult.Applied)
        {
            return;
        }

        var publicResult = _content.TryDiscover(new DiscoverContentCommand(
            secretRealmId,
            ContentDiscovererRef.Observer,
            ContentDiscoveryState.Located,
            NextContentSourceResultId(),
            Tick.Value));
        if (publicResult.Applied)
        {
            AddEvent(WorldEventKind.SecretRealmDiscovered, agentIndex, secretRealmId.Value);
        }
    }

    private ContentSourceResultId NextContentSourceResultId() => new(
        ContentDiscoveryRules.SourceResultPrefix
        + checked((ulong)_content.ProvenanceCount + 1UL));

    private void RecordPurchasedKnowledgeAccess(int agentIndex, bool technique)
    {
        var accessor = new KnowledgeAccessorRef(KnowledgeAccessorKind.Agent, agentIndex);
        var kind = technique
            ? KnowledgeContentKind.Technique
            : KnowledgeContentKind.Spell;
        var selector = ContentDiscoveryRules.CreateDeterministicRoll(
            Seed,
            Tick.Value,
            agentIndex,
            _agents.LocationIds[agentIndex],
            technique ? (byte)0xB1 : (byte)0xB2);
        if (!_knowledge.TrySelectUnaccessedEdition(accessor, kind, selector, out var editionId))
        {
            return;
        }

        var result = _knowledge.TryGrantAccess(new GrantKnowledgeAccessCommand(
            accessor,
            editionId,
            KnowledgeAccessState.CanRead,
            KnowledgeBeliefState.Unknown,
            KnowledgeValueAvailability.DataUnavailable,
            UnderstandingHundredths: 0,
            KnowledgeValueAvailability.DataUnavailable,
            ConfidenceHundredths: 0,
            CreateKnowledgeActionSourceResultId(
                agentIndex,
                technique ? (byte)1 : (byte)2),
            Tick.Value));
        if (!result.Applied)
        {
            throw new InvalidOperationException(
                $"서적 구매 지식 접근 기록에 실패했습니다: {agentIndex} · {result.Status}");
        }
    }

    private ContentSourceResultId CreateKnowledgeActionSourceResultId(
        int agentIndex,
        byte phase)
    {
        if ((uint)agentIndex >= (uint)Population || phase is 0 or >= 16)
        {
            throw new ArgumentOutOfRangeException(nameof(agentIndex));
        }
        const ulong prefix = 0xE021_0000_0000_0000UL;
        const ulong maximumPayload = 0x0000_FFFF_FFFF_FFFFUL;
        var payload = checked(
            (((ulong)Tick.Value * (ulong)Population) + (ulong)agentIndex) * 16UL
            + phase);
        if (payload > maximumPayload)
        {
            throw new InvalidOperationException("지식 Action source_result 범위를 초과했습니다.");
        }
        return new ContentSourceResultId(prefix + payload);
    }
}
