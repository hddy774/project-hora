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
}
