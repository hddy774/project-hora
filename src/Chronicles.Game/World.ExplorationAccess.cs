namespace Chronicles.Game;

public sealed partial class GameWorld
{
    // A strategic map point includes the entrance and its interior. Ground stock at the
    // entrance is outside; carried stock follows the actual holder's current subspace.
    private bool ExplorationCarriedItemAccessible(Person person, Item item)
    {
        var visited = new HashSet<long>();
        while (item.Container != 0)
        {
            if (!visited.Add(item.Id) || !items.TryGetValue(item.Container, out item!)) return false;
        }
        if (item.Place >= 0) return OccupiedExploration(person) is null;
        var metadata = state.Inventory.Metadata.FirstOrDefault(m => m.Item == item.Id);
        var holder = metadata?.Carrier > 0 ? metadata.Carrier : metadata?.Custodian > 0 ? metadata.Custodian : item.Owner;
        return people.TryGetValue(holder, out var custodian) && ShareExplorationSpace(person, custodian);
    }
}
