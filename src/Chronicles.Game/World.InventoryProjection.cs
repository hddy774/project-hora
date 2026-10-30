using Chronicles.Game.Inventory;

namespace Chronicles.Game;

public sealed partial class GameWorld
{
    // This diagnostic scope is not authority, a command option, or saved state. Every fresh/restore instance uses person15.
    private sealed class InventoryProjectionScope { internal bool StrictReference; internal bool TypedCopy; internal long Snapshots; internal long TypedSnapshots; }
    private InventoryProjectionScope inventoryProjection = new();
    internal bool InventoryProjectionEnabled => !inventoryProjection.StrictReference;
    internal long InventoryProjectionSnapshots => inventoryProjection.Snapshots;
    internal bool InventoryTypedCopyExperimentEnabled => inventoryProjection.TypedCopy;
    internal long InventoryTypedCopySnapshots => inventoryProjection.TypedSnapshots;
    internal void UseStrictInventoryForVerification() => inventoryProjection = new() { StrictReference = true };
    internal void EnableInventoryProjectionForExperiment() => inventoryProjection = new();
    internal void EnableInventoryTypedCopyForExperiment() => inventoryProjection = new() { TypedCopy = true };

    private InventoryEngine.OwnedTransaction CreateOwnedInventory()
    {
        if (inventoryProjection.StrictReference) return InventoryEngine.Own(state);
        var transaction = inventoryProjection.TypedCopy ? InventoryEngine.OwnTypedProjectedForExperiment(state) : InventoryEngine.OwnProjected(state);
        inventoryProjection.Snapshots++; if (inventoryProjection.TypedCopy) inventoryProjection.TypedSnapshots++; return transaction;
    }
}
