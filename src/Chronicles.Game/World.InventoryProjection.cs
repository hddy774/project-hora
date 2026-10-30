using Chronicles.Game.Inventory;

namespace Chronicles.Game;

public sealed partial class GameWorld
{
    // This diagnostic scope is not authority, a command option, or saved state. Every fresh/restore instance uses person15.
    private sealed class InventoryProjectionScope { internal bool StrictReference; internal long Snapshots; }
    private InventoryProjectionScope inventoryProjection = new();
    internal bool InventoryProjectionEnabled => !inventoryProjection.StrictReference;
    internal long InventoryProjectionSnapshots => inventoryProjection.Snapshots;
    internal void UseStrictInventoryForVerification() => inventoryProjection = new() { StrictReference = true };
    internal void EnableInventoryProjectionForExperiment() => inventoryProjection = new();

    private InventoryEngine.OwnedTransaction CreateOwnedInventory()
    {
        if (inventoryProjection.StrictReference) return InventoryEngine.Own(state);
        var transaction = InventoryEngine.OwnProjected(state); inventoryProjection.Snapshots++; return transaction;
    }
}
