namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    readonly List<LimitOrder>[] bids = Enumerable.Range(0, StockCount).Select(_ => new List<LimitOrder>()).ToArray();
    readonly List<LimitOrder>[] asks = Enumerable.Range(0, StockCount).Select(_ => new List<LimitOrder>()).ToArray();
    readonly Dictionary<int, List<LimitOrder>> ownedOrders = [];
    public static long Fee(long notional) => checked((notional * 15 + 9999) / 10000);
    public long ExchangeFee(long notional) => checked((notional * State.Government.Policy.FeeBasisPoints + 9999) / 10000);
    long Reserve(Trader t, LimitOrder o, int quantity)
    {
        long unit = o.Short ? (o.CollateralPrice + 1L) / 2 + ExchangeFee(o.CollateralPrice) : o.Price + ExchangeFee(o.Price);
        if (o.Cover) unit += (long)Math.Ceiling(Math.Max(0, t.ShortAveragePrice[o.StockIndex] - o.Price) * State.Government.Policy.TaxRate);
        return checked(unit * quantity);
    }
    public int MaxBuy(Trader t, int index, int? limit = null)
    {
        if (index < 0 || index >= State.Stocks.Count) return 0;
        int price = limit ?? State.Stocks[index].Price;
        return (int)Math.Min(1_000_000, AvailableCash(t) / (price + ExchangeFee(price)));
    }
    public IReadOnlyList<LimitOrder> Orders(int index, bool buy) => buy ? bids[index] : asks[index];
    public List<BookLevel> Depth(int index, bool buy, int levels = 5) => Orders(index, buy).GroupBy(o => o.Price).Take(levels)
        .Select(g => new BookLevel(g.Key, g.Where(o => o.OwnerId <= AiCount).Sum(o => (long)o.Remaining),
            g.Where(o => o.OwnerId > AiCount).Sum(o => (long)o.Remaining))).ToList();
    void RebuildBooks()
    {
        foreach (var b in bids.Concat(asks)) b.Clear(); ownedOrders.Clear();
        Array.Clear(State.Bank.ReservedLending);
        foreach (var t in Participants) { t.ReservedCash = 0; Array.Clear(t.ReservedShares); Array.Clear(t.ReservedCovers); }
        foreach (var o in State.Orders)
        {
            var owner = Owner(o.OwnerId); SetReservation(owner, o, o.Remaining); AddResting(o);
        }
    }
    void SetReservation(Trader owner, LimitOrder o, int delta)
    {
        if (o.Short) { owner.ReservedCash += Reserve(owner, o, delta); State.Bank.ReservedLending[o.StockIndex] += delta; }
        else if (o.Buy) { owner.ReservedCash += Reserve(owner, o, delta); if (o.Cover) owner.ReservedCovers[o.StockIndex] += delta; }
        else owner.ReservedShares[o.StockIndex] += delta;
    }
    // Incoming orders reserve their full liability before matching. A buy reserves
    // the per-share rounded fee, which is safe even if it fills one share at a time.
    public string? SubmitOrder(int ownerId, int stockIndex, bool buy, int price, int quantity, int lifetime = 3, bool shortSale = false, bool cover = false)
    {
        if (ownerId < 1 || ownerId > AiCount + RetailCount || stockIndex < 0 || stockIndex >= State.Stocks.Count || price is < 100 or > 10_000_000 || quantity is <= 0 or > 1_000_000 || lifetime is < 1 or > 24 ||
            (shortSale && buy) || (cover && !buy) || (shortSale && cover))
            return "주문 값이 올바르지 않습니다.";
        var t = Owner(ownerId);
        var incoming = new LimitOrder { OwnerId = ownerId, StockIndex = stockIndex, Buy = buy, Short = shortSale, Cover = cover,
            Price = price, Remaining = quantity, ExpiresAt = State.CompletedHours + lifetime };
        if ((shortSale || cover) && t.IsRetail) return "공매도는 기관만 가능합니다.";
        if (cover && quantity > t.ShortShares[stockIndex] - t.ReservedCovers[stockIndex]) return "상환 가능 수량이 부족합니다.";
        if (cover && ownedOrders.TryGetValue(t.Id, out var active) && active.Any(o => o.Short && o.StockIndex == stockIndex)) return "공매도 주문을 먼저 취소해야 합니다.";
        if (shortSale)
        {
            incoming.CollateralPrice = Math.Max(price, Math.Max(State.Stocks[stockIndex].Price, bids[stockIndex].FirstOrDefault()?.Price ?? 0));
            if (!State.Government.Policy.ShortSellingAllowed) return "이번 시즌 공매도가 제한됩니다.";
            if (t.ReservedCovers[stockIndex] > 0) return "상환 주문이 진행 중입니다.";
            long pending = ownedOrders.TryGetValue(t.Id, out var orders) ? orders.Where(o => o.Short).Sum(o => (long)o.CollateralPrice * o.Remaining) : 0;
            if (t.ShortLiability(State.Stocks) + pending + (long)incoming.CollateralPrice * quantity > Math.Max(0, t.Equity(State.Stocks)) * State.Government.Policy.ShortExposureLimit)
                return "공매도 위험 한도를 초과합니다.";
            if (quantity > State.Bank.ShareInventory[stockIndex] - State.Bank.ReservedLending[stockIndex]) return "대여 가능 주식이 부족합니다.";
        }
        if ((buy || shortSale) && Reserve(t, incoming, quantity) > (cover ? t.Cash - t.ReservedCash : AvailableCash(t))) return "주문 가능 현금/담보가 부족합니다.";
        if (!buy && !shortSale && quantity > t.Shares[stockIndex] - t.ReservedShares[stockIndex]) return "주문 가능 주식이 부족합니다.";
        incoming.Id = State.NextOrderId++;
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
        if (!t.IsRetail) t.LastAction = $"{State.Stocks[stockIndex].Symbol} {quantity}주 {(shortSale ? "공매도" : cover ? "공매도 상환" : buy ? "매수" : "매도")} 호가";
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
        long amount = (long)price * quantity, fee = ExchangeFee(amount);
        if (bid.Cover)
        {
            double profit = quantity * buyer.ShortAveragePrice[i] - amount; buyer.RealizedProfit += profit;
            buyer.ShortShares[i] -= quantity; State.Bank.ShareInventory[i] += quantity;
            if (buyer.ShortShares[i] == 0) buyer.ShortAveragePrice[i] = 0;
            PayTradeTax(buyer, profit);
        }
        else
        { buyer.AverageCost[i] = (buyer.Shares[i] * buyer.AverageCost[i] + amount) / (buyer.Shares[i] + quantity); buyer.Shares[i] += quantity; }
        buyer.Cash -= amount + fee; buyer.BuyCashFlow += amount; buyer.Fees += fee; buyer.Trades++;
        seller.Cash += amount - fee; seller.SellCashFlow += amount;
        if (ask.Short)
        {
            seller.ShortAveragePrice[i] = (seller.ShortShares[i] * seller.ShortAveragePrice[i] + amount) / (seller.ShortShares[i] + quantity);
            seller.ShortShares[i] += quantity; State.Bank.ShareInventory[i] -= quantity;
        }
        else
        {
            double profit = amount - quantity * seller.AverageCost[i]; seller.RealizedProfit += profit;
            seller.Shares[i] -= quantity; if (seller.Shares[i] == 0) seller.AverageCost[i] = 0; PayTradeTax(seller, profit);
        }
        seller.Fees += fee; seller.Trades++;
        buyer.TradedVolume += quantity; seller.TradedVolume += quantity; buyer.TradedTurnover += amount; seller.TradedTurnover += amount;
        State.FeePool += fee * 2; State.TotalMatches++;
        State.TotalAiTrades += (buyer.IsRetail ? 0 : 1) + (seller.IsRetail ? 0 : 1);
        var s = State.Stocks[i]; s.Price = price; s.Volume += quantity; s.DayVolume += quantity; s.TotalVolume += quantity;
        s.DayTurnover += amount; s.TotalTurnover += amount;
        State.Tape.Insert(0, new TradeRecord { Season = State.Season, Day = State.Day, Hour = State.Hour, BuyerId = buyer.Id,
            SellerId = seller.Id, StockIndex = i, Quantity = quantity, Price = price, ShortSale = ask.Short, ShortCover = bid.Cover });
        if (State.Tape.Count > TapeLimit) State.Tape.RemoveAt(State.Tape.Count - 1);
    }
    void PayTradeTax(Trader t, double profit)
    {
        long tax = (long)Math.Ceiling(Math.Max(0, profit) * State.Government.Policy.TaxRate);
        t.Cash -= tax; t.Taxes += tax; State.Government.Cash += tax; State.Government.Taxes += tax;
    }
}
