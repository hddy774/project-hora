namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    readonly List<LimitOrder>[] bids = Enumerable.Range(0, 8).Select(_ => new List<LimitOrder>()).ToArray();
    readonly List<LimitOrder>[] asks = Enumerable.Range(0, 8).Select(_ => new List<LimitOrder>()).ToArray();
    readonly Dictionary<int, List<LimitOrder>> ownedOrders = [];
    public static long Fee(long notional) => checked((notional * 15 + 9999) / 10000);
    static long Reserve(int price, int quantity) => checked((price + Fee(price)) * quantity);
    public int MaxBuy(Trader t, int index, int? limit = null)
    {
        if (index is < 0 or >= 8) return 0;
        int price = limit ?? State.Stocks[index].Price;
        return (int)Math.Min(1_000_000, Math.Max(0, t.Cash - t.ReservedCash) / (price + Fee(price)));
    }
    public IReadOnlyList<LimitOrder> Orders(int index, bool buy) => buy ? bids[index] : asks[index];
    public List<BookLevel> Depth(int index, bool buy, int levels = 5) => Orders(index, buy).GroupBy(o => o.Price).Take(levels)
        .Select(g => new BookLevel(g.Key, g.Where(o => o.OwnerId <= AiCount).Sum(o => (long)o.Remaining),
            g.Where(o => o.OwnerId > AiCount).Sum(o => (long)o.Remaining))).ToList();
    void RebuildBooks()
    {
        foreach (var b in bids.Concat(asks)) b.Clear(); ownedOrders.Clear();
        foreach (var t in Participants) { t.ReservedCash = 0; Array.Clear(t.ReservedShares); }
        foreach (var o in State.Orders)
        {
            var owner = Owner(o.OwnerId); SetReservation(owner, o, o.Remaining); AddResting(o);
        }
    }
    static void SetReservation(Trader owner, LimitOrder o, int delta)
    {
        if (o.Buy) owner.ReservedCash += Reserve(o.Price, delta);
        else owner.ReservedShares[o.StockIndex] += delta;
    }
    // Incoming orders reserve their full liability before matching. A buy reserves
    // the per-share rounded fee, which is safe even if it fills one share at a time.
    public string? SubmitOrder(int ownerId, int stockIndex, bool buy, int price, int quantity, int lifetime = 3)
    {
        if (ownerId < 1 || ownerId > AiCount + RetailCount || stockIndex is < 0 or >= 8 || price is < 100 or > 10_000_000 || quantity is <= 0 or > 1_000_000 || lifetime is < 1 or > 24)
            return "주문 값이 올바르지 않습니다.";
        var t = Owner(ownerId);
        if (buy && Reserve(price, quantity) > t.Cash - t.ReservedCash) return "주문 가능 현금이 부족합니다.";
        if (!buy && quantity > t.Shares[stockIndex] - t.ReservedShares[stockIndex]) return "주문 가능 주식이 부족합니다.";
        var incoming = new LimitOrder { Id = State.NextOrderId++, OwnerId = ownerId, StockIndex = stockIndex, Buy = buy,
            Price = price, Remaining = quantity, ExpiresAt = State.CompletedHours + lifetime };
        SetReservation(t, incoming, quantity);
        var opposite = buy ? asks[stockIndex] : bids[stockIndex];
        while (incoming.Remaining > 0 && opposite.Count > 0)
        {
            var resting = opposite[0];
            if (buy ? resting.Price > price : resting.Price < price) break;
            if (resting.OwnerId == ownerId) { CancelOrder(resting.Id); continue; }
            int q = Math.Min(incoming.Remaining, resting.Remaining);
            SetReservation(t, incoming, -q); SetReservation(Owner(resting.OwnerId), resting, -q);
            Execute(buy ? incoming : resting, buy ? resting : incoming, resting.Price, q);
            incoming.Remaining -= q; resting.Remaining -= q;
            if (resting.Remaining == 0) { opposite.RemoveAt(0); ownedOrders[resting.OwnerId].Remove(resting); }
        }
        if (incoming.Remaining > 0) { State.Orders.Add(incoming); AddResting(incoming); }
        if (!t.IsRetail) t.LastAction = $"{State.Stocks[stockIndex].Symbol} {quantity}주 {(buy ? "매수" : "매도")} 호가";
        cachedStats = null;
        return null;
    }
    void AddResting(LimitOrder o)
    {
        var book = o.Buy ? bids[o.StockIndex] : asks[o.StockIndex];
        int lo = 0, hi = book.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2; var other = book[mid];
            bool before = o.Price == other.Price ? o.Id < other.Id : o.Buy ? o.Price > other.Price : o.Price < other.Price;
            if (before) hi = mid; else lo = mid + 1;
        }
        book.Insert(lo, o);
        if (!ownedOrders.TryGetValue(o.OwnerId, out var own)) ownedOrders[o.OwnerId] = own = [];
        own.Add(o);
    }
    public bool CancelOrder(long id)
    {
        var o = State.Orders.Find(o => o.Id == id && o.Remaining > 0);
        if (o is null) return false;
        SetReservation(Owner(o.OwnerId), o, -o.Remaining);
        (o.Buy ? bids[o.StockIndex] : asks[o.StockIndex]).Remove(o);
        ownedOrders[o.OwnerId].Remove(o); o.Remaining = 0; cachedStats = null;
        return true;
    }
    public void CancelOrders(int id)
    {
        if (!ownedOrders.TryGetValue(id, out var owned)) return;
        foreach (var o in owned)
        {
            SetReservation(Owner(id), o, -o.Remaining);
            (o.Buy ? bids[o.StockIndex] : asks[o.StockIndex]).Remove(o); o.Remaining = 0;
        }
        owned.Clear(); cachedStats = null;
    }
    void ExpireOrders()
    {
        foreach (var o in State.Orders)
        {
            if (o.Remaining == 0 || o.ExpiresAt > State.CompletedHours) continue;
            SetReservation(Owner(o.OwnerId), o, -o.Remaining);
            (o.Buy ? bids[o.StockIndex] : asks[o.StockIndex]).Remove(o); ownedOrders[o.OwnerId].Remove(o); o.Remaining = 0;
        }
        State.Orders.RemoveAll(o => o.Remaining == 0);
    }
    void Execute(LimitOrder bid, LimitOrder ask, int price, int quantity)
    {
        var buyer = Owner(bid.OwnerId); var seller = Owner(ask.OwnerId); int i = bid.StockIndex;
        long amount = (long)price * quantity, fee = Fee(amount);
        buyer.AverageCost[i] = (buyer.Shares[i] * buyer.AverageCost[i] + amount) / (buyer.Shares[i] + quantity);
        buyer.Cash -= amount + fee; buyer.Shares[i] += quantity; buyer.BuyCashFlow += amount; buyer.Fees += fee; buyer.Trades++;
        seller.Cash += amount - fee; seller.SellCashFlow += amount; seller.RealizedProfit += amount - quantity * seller.AverageCost[i];
        seller.Shares[i] -= quantity; seller.Fees += fee; seller.Trades++;
        if (seller.Shares[i] == 0) seller.AverageCost[i] = 0;
        State.FeePool += fee * 2; State.TotalMatches++;
        State.TotalAiTrades += (buyer.IsRetail ? 0 : 1) + (seller.IsRetail ? 0 : 1);
        var s = State.Stocks[i]; s.Price = price; s.Volume += quantity; s.DayVolume += quantity; s.TotalVolume += quantity;
        s.DayTurnover += amount; s.TotalTurnover += amount;
        State.Tape.Insert(0, new TradeRecord { Season = State.Season, Day = State.Day, Hour = State.Hour, BuyerId = buyer.Id,
            SellerId = seller.Id, StockIndex = i, Quantity = quantity, Price = price });
        if (State.Tape.Count > TapeLimit) State.Tape.RemoveAt(State.Tape.Count - 1);
    }
}
