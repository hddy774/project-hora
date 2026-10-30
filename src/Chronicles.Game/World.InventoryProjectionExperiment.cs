using Chronicles.Game.Inventory;

namespace Chronicles.Game;

public sealed partial class GameWorld
{
    // This scope is not authority, a command option, or saved state. Each ordinary instance defaults to strict.
    private sealed class InventoryProjectionScope { internal long Snapshots; }
    private InventoryProjectionScope? inventoryProjectionExperiment;
    internal bool InventoryProjectionExperimentEnabled => inventoryProjectionExperiment is not null;
    internal long InventoryProjectionExperimentSnapshots => inventoryProjectionExperiment?.Snapshots ?? 0;
    internal void EnableInventoryProjectionForExperiment() => inventoryProjectionExperiment = new();

    private InventoryEngine.OwnedTransaction CreateOwnedInventory()
    {
        if (inventoryProjectionExperiment is null) return InventoryEngine.Own(state);
        var transaction = InventoryEngine.OwnProjectedForExperiment(state); inventoryProjectionExperiment.Snapshots++; return transaction;
    }
}
