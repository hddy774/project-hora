namespace Chronicles.Game.Inventory;

public static partial class InventoryEngine
{
    private static long ReserveExternal(InventoryState state, ReserveInventoryMaterials command)
    {
        Require(!string.IsNullOrWhiteSpace(command.Purpose) && command.Materials.Count > 0 && command.Materials.Values.All(q => q > 0), "Material reservation needs a purpose and positive quantities.");
        var reservation = Reservation(state, command.Actor, command.Purpose);
        ReserveInputs(state, command.Actor, command.Materials, reservation); return reservation.Id;
    }
    private static long ReserveExternalPhysical(InventoryState state, ReserveInventoryPhysical command)
    {
        var item = Accessible(state, command.Actor, command.Item, false);
        Require((item.BoundSoul == 0 || item.BoundSoul == PersonAt(state, command.Actor).SoulId) &&
            !string.IsNullOrWhiteSpace(command.Purpose) && command.Quantity > 0 && command.Quantity <= item.Quantity &&
            state.Items.All(i => i.Container != item.Id), "Physical reservation needs a valid exact quantity and empty storage.");
        var reservation = Reservation(state, command.Actor, command.Purpose);
        var selected = command.Quantity == item.Quantity ? item : SplitItem(state, item, command.Quantity);
        selected.Reserved = reservation.Id; reservation.Items.Add(selected.Id); return reservation.Id;
    }
    private static AssetReservation ExternalReservation(InventoryState state, long actor, long id)
    {
        var reservation = state.Ledger.Reservations.SingleOrDefault(r => r.Id == id);
        Require(reservation is { Active: true } && reservation.Actor == actor &&
            !state.Ledger.Jobs.Any(j => j.Reservation == id) && !state.Ledger.Contracts.Any(c => c.Reservation == id),
            "This reservation belongs to an active process/contract or another actor.");
        return reservation!;
    }
    private static long ConsumeExternal(InventoryState state, ConsumeInventoryAssets command)
    {
        var reservation = ExternalReservation(state, command.Actor, command.Reservation); var actor = PersonAt(state, command.Actor);
        foreach (var id in reservation.Items)
        {
            var item = ItemAt(state, id);
            Require(item.Reserved == reservation.Id && Location(state, item.Id) == actor.Place, "Reserved physical input is no longer locally accessible.");
            ConsumePhysical(state, item, item.Quantity);
        }
        reservation.Active = false; return reservation.Id;
    }
    private static long ReleaseExternal(InventoryState state, ReleaseInventoryAssets command)
    {
        var reservation = ExternalReservation(state, command.Actor, command.Reservation);
        FreeReservation(state, reservation.Id); return reservation.Id;
    }
}
