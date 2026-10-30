namespace Chronicles.Game.Inventory;

public static partial class InventoryEngine
{
    private static InventoryContract Contract(InventoryState state, long id)
    {
        var contract = state.Ledger.Contracts.SingleOrDefault(c => c.Id == id);
        Require(contract is { Status: "open" }, "Contract is absent or already terminal."); return contract!;
    }
    private static void ReserveTree(InventoryState state, long actor, Item item, AssetReservation reservation)
    {
        foreach (var entry in Tree(state, item))
        {
            Require(entry.Owner == actor && entry.BoundSoul == 0, "Contract requires unbound property owned by its sender.");
            entry.Reserved = reservation.Id; reservation.Items.Add(entry.Id);
        }
    }
    private static long Offer(InventoryState state, OfferItem command)
    {
        var item = Accessible(state, command.Actor, command.Item); var actor = PersonAt(state, command.Actor);
        Require(command.Price > 0 && command.Due > state.Month && item.Container == 0, "Offer needs a positive price, future deadline, and root item.");
        var reservation = Reservation(state, actor.Id, command.Auction ? "auction" : "sale");
        ReserveTree(state, actor.Id, item, reservation);
        foreach (var entry in Tree(state, item)) entry.Place = actor.Place;
        var contract = new InventoryContract { Id = Id(state), Kind = command.Auction ? "auction" : "sale", Seller = actor.Id,
            Item = item.Id, Reservation = reservation.Id, Price = command.Price, Due = command.Due, Origin = actor.Place, Destination = actor.Place };
        state.Ledger.Contracts.Add(contract); return contract.Id;
    }
    private static long Withdraw(InventoryState state, WithdrawOffer command)
    {
        var contract = Contract(state, command.Contract);
        Require(contract.Seller == command.Actor && contract.Kind is "sale" or "auction" && contract.Buyer == 0, "A funded bid or cargo commitment cannot be withdrawn as an offer.");
        contract.Status = "cancelled"; FreeReservation(state, contract.Reservation); return contract.Id;
    }
    private static void Pay(Person payer, Person payee, long amount)
    {
        Require(amount >= 0 && payer.Money >= amount, "Insufficient available money.");
        payer.Money -= amount; payee.Money = checked(payee.Money + amount);
    }
    private static void HandOver(InventoryState state, InventoryContract contract, long recipient)
    {
        var item = ItemAt(state, contract.Item);
        Require(item.Reserved == contract.Reservation, "Contract lost physical reservation.");
        foreach (var entry in Tree(state, item)) { entry.Owner = recipient; entry.Place = contract.Destination; }
        var metadata = Detail(state, item); metadata.Custodian = recipient; metadata.Carrier = 0;
        metadata.Provenance.Add($"contract:{contract.Id}");
        FreeReservation(state, contract.Reservation); contract.Status = "completed";
    }
    private static long Buy(InventoryState state, BuyItem command)
    {
        var contract = Contract(state, command.Contract); var buyer = PersonAt(state, command.Actor); var seller = PersonAt(state, contract.Seller);
        Require(contract.Kind == "sale" && contract.Due > state.Month && buyer.Id != seller.Id && buyer.Place == contract.Destination &&
            Location(state, contract.Item) == buyer.Place, "Sale requires live local counterparties before its deadline.");
        Pay(buyer, seller, contract.Price); contract.Buyer = buyer.Id; HandOver(state, contract, buyer.Id);
        Event(state, "sale", buyer.Id, contract.Id, "Money transferred between actual parties; physical item received at market storage."); return contract.Id;
    }
    private static long Bid(InventoryState state, PlaceBid command)
    {
        var contract = Contract(state, command.Contract); var buyer = PersonAt(state, command.Actor); PersonAt(state, contract.Seller);
        Require(contract.Kind == "auction" && contract.Due > state.Month && buyer.Id != contract.Seller &&
            buyer.Place == contract.Destination && Location(state, contract.Item) == buyer.Place && command.Amount >= contract.Price &&
            (contract.Buyer == 0 || command.Amount > contract.Escrow), "Bid is not valid for this auction.");
        var available = checked(buyer.Money + (contract.Buyer == buyer.Id ? contract.Escrow : 0));
        Require(command.Amount <= available, "Bid exceeds available money and this bidder's own deposit.");
        if (contract.Buyer != 0)
        {
            var former = PersonAt(state, contract.Buyer, false); former.Money = checked(former.Money + contract.Escrow);
        }
        buyer.Money -= command.Amount; contract.Buyer = buyer.Id; contract.Escrow = command.Amount; return contract.Id;
    }
    private static long Transport(InventoryState state, BeginTransport command)
    {
        var item = Accessible(state, command.Actor, command.Item); var sender = PersonAt(state, command.Actor);
        var carrier = PersonAt(state, command.Carrier); PersonAt(state, command.Recipient);
        Require(command.RouteAllowed && command.Destination is >= 0 and < 100 && command.Destination != sender.Place &&
            carrier.Place == sender.Place && item.Container == 0 && command.Fee >= 0 && sender.Money >= command.Fee &&
            (carrier.Id == sender.Id || command.CarrierConsented), "Transport requires a real route, carrier acceptance, local cargo, and funded fee.");
        var labor = AddLabor(state, carrier.Id, "transport", state.Month, command.Due, command.LaborUnits);
        var reservation = Reservation(state, sender.Id, "transport"); ReserveTree(state, sender.Id, item, reservation);
        sender.Money -= command.Fee;
        foreach (var entry in Tree(state, item)) entry.Place = -1;
        var metadata = Detail(state, item); metadata.Custodian = carrier.Id; metadata.Carrier = carrier.Id;
        var contract = new InventoryContract { Id = Id(state), Kind = "transport", Seller = sender.Id, Buyer = command.Recipient,
            Carrier = carrier.Id, Item = item.Id, Reservation = reservation.Id, Labor = labor, Fee = command.Fee, Escrow = command.Fee,
            Origin = sender.Place, Destination = command.Destination, Due = command.Due };
        state.Ledger.Contracts.Add(contract); return contract.Id;
    }
    private static void FailContract(InventoryState state, InventoryContract contract)
    {
        var refund = PersonAt(state, contract.Kind == "transport" ? contract.Seller : contract.Buyer, false);
        refund.Money = checked(refund.Money + contract.Escrow); contract.Escrow = 0;
        var item = ItemAt(state, contract.Item); var place = Location(state, item.Id);
        foreach (var entry in Tree(state, item)) entry.Place = place;
        Detail(state, item).Carrier = 0;
        FreeReservation(state, contract.Reservation); FreeLabor(state, contract.Labor); contract.Status = "failed";
        Event(state, "contract-failed", contract.Seller, contract.Id, $"Cargo remains at actual location {place}; ownership is retained and escrow refunded.");
    }
    private static long Delivery(InventoryState state, FinishTransport command)
    {
        var contract = Contract(state, command.Contract);
        Require(contract.Kind == "transport" && (command.Actor == contract.Seller || command.Actor == contract.Carrier), "Only a cargo party can report delivery.");
        if (command.Delivered)
        {
            var carrier = PersonAt(state, contract.Carrier); PersonAt(state, contract.Seller); PersonAt(state, contract.Buyer);
            Require(state.Month >= contract.Due && carrier.Place == contract.Destination &&
                Location(state, contract.Item) == contract.Destination, "Delivery requires actual arrival and completed travel time.");
            carrier.Money = checked(carrier.Money + contract.Escrow); contract.Escrow = 0;
            HandOver(state, contract, contract.Buyer); FreeLabor(state, contract.Labor);
            Event(state, "delivered", contract.Carrier, contract.Id, "Ownership and paid carrier fee settled once at the physical destination.");
        }
        else FailContract(state, contract);
        return contract.Id;
    }
    private static long Settle(InventoryState state)
    {
        foreach (var contract in state.Ledger.Contracts.Where(c => c.Status == "open").OrderBy(c => c.Due).ThenBy(c => c.Id))
        {
            var sellerAlive = PersonAt(state, contract.Seller, false).Status == LifeStatus.Alive;
            var buyerAlive = contract.Buyer == 0 || PersonAt(state, contract.Buyer, false).Status == LifeStatus.Alive;
            if (contract.Kind == "transport")
            {
                var carrier = PersonAt(state, contract.Carrier, false);
                if (!sellerAlive || !buyerAlive || carrier.Status != LifeStatus.Alive) FailContract(state, contract);
                else if (state.Month >= contract.Due)
                {
                    if (carrier.Place == contract.Destination && Location(state, contract.Item) == contract.Destination)
                        Delivery(state, new("system:" + Number(contract.Id), contract.Carrier, contract.Id, true));
                    else FailContract(state, contract);
                }
                continue;
            }
            if (!sellerAlive || !buyerAlive)
            {
                if (contract.Buyer != 0)
                {
                    var buyer = PersonAt(state, contract.Buyer, false); buyer.Money = checked(buyer.Money + contract.Escrow);
                }
                contract.Escrow = 0; FreeReservation(state, contract.Reservation); contract.Status = "cancelled"; continue;
            }
            if (state.Month < contract.Due) continue;
            if (contract.Kind == "auction" && contract.Buyer != 0)
            {
                var seller = PersonAt(state, contract.Seller); seller.Money = checked(seller.Money + contract.Escrow); contract.Escrow = 0;
                HandOver(state, contract, contract.Buyer);
            }
            else { FreeReservation(state, contract.Reservation); contract.Status = "expired"; }
        }
        foreach (var job in state.Ledger.Jobs.Where(j => j.Status is "reserved" or "processing").ToArray())
            if (PersonAt(state, job.Actor, false).Status != LifeStatus.Alive)
                CancelJob(state, new("system:" + Number(job.Id), job.Actor, job.Id));
        foreach (var source in state.Ledger.Sources)
        {
            Require(source.LastMonth <= state.Month, "Source regeneration clock is in the future.");
            var growth = checked((state.Month - source.LastMonth) * source.Regeneration);
            var remaining = source.Remaining;
            source.Remaining = Math.Min(source.Capacity, checked(source.Remaining + growth)); source.LastMonth = state.Month;
            if (source.Remaining > remaining) Event(state, "source-regeneration", 0, source.Id, Number(source.Remaining - remaining));
        }
        foreach (var array in state.Ledger.Arrays)
        {
            var item = ItemAt(state, array.Item); array.Condition = Math.Min(array.Condition, 100 - item.Damage);
            array.Active = array.Condition > 0 && array.PaidUntil > state.Month;
            if (!array.Active) state.Ledger.Effects.RemoveAll(e => e.Cause.StartsWith($"array:{array.Id}:", StringComparison.Ordinal));
        }
        foreach (var labor in state.Ledger.Labor.Where(l => l.Active && l.End <= state.Month &&
            !state.Ledger.Jobs.Any(j => j.Labor == l.Id && j.Status is "reserved" or "processing") &&
            !state.Ledger.Contracts.Any(c => c.Labor == l.Id && c.Status == "open"))) labor.Active = false;
        return 0;
    }
}
