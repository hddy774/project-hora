namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    List<LimitOrder>[] bids = [];
    List<LimitOrder>[] asks = [];
    readonly Dictionary<int, List<LimitOrder>> ownedOrders = [];
    readonly Dictionary<long,LimitOrder> orderById=[];
    readonly PriorityQueue<LimitOrder,(long Minute,long Id)> expirations=new();
    public long BookRevision { get; private set; }
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
    public List<BookLevel> Depth(int index,bool buy,int levels=5)
    {
        if(index<0 || index>=State.Stocks.Count || levels is <1 or >100) throw new ArgumentOutOfRangeException(nameof(index));
        var result=new List<BookLevel>(levels); var book=buy ? bids[index] : asks[index];
        int price=0; long institutions=0,retail=0,bank=0;
        foreach(var order in book)
        {
            if(order.Remaining<=0) continue;
            if(order.Price!=price && price!=0)
            {
                result.Add(new BookLevel(price,institutions,retail,bank));
                if(result.Count==levels) return result;
                institutions=retail=bank=0;
            }
            price=order.Price;
            if(order.OwnerId==0) bank+=order.Remaining; else if(order.OwnerId<=AiCount) institutions+=order.Remaining; else retail+=order.Remaining;
        }
        if(price!=0) result.Add(new BookLevel(price,institutions,retail,bank));
        return result;
    }
    void RebuildBooks()
    {
        BookRevision++;
        State.Orders.RemoveAll(o=>o.Remaining<=0);
        bids = Enumerable.Range(0, State.Stocks.Count).Select(_ => new List<LimitOrder>()).ToArray();
        asks = Enumerable.Range(0, State.Stocks.Count).Select(_ => new List<LimitOrder>()).ToArray();
        ownedOrders.Clear(); orderById.Clear(); expirations.Clear();
        Array.Clear(State.Bank.ReservedLending);
        var bank=BankAccount(); bank.ReservedCash=0; Array.Clear(bank.ReservedShares);
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
        if (ownerId < 0 || ownerId > AiCount + State.Retail.Count || stockIndex < 0 || stockIndex >= State.Stocks.Count || price is < 1 or > 10_000_000 || quantity is <= 0 or > 1_000_000 || lifetime is < 1 or > 24 ||
            (shortSale && buy) || (cover && !buy) || (shortSale && cover))
            return "주문 값이 올바르지 않습니다.";
        if (!State.Stocks[stockIndex].Active) return "상장 종료 종목입니다.";
        var t = Owner(ownerId);
        if(t.WaitingForCapital) return "재등장 자금 대기 중입니다.";
        var incoming = new LimitOrder { OwnerId = ownerId, StockIndex = stockIndex, Buy = buy, Short = shortSale, Cover = cover,
            SecurityId = State.Stocks[stockIndex].SecurityId, Price = price, Remaining = quantity, ExpiresAt = State.CompletedHours + lifetime,
            CreatedMinute=State.CompletedMinutes,ExpiresMinute=State.CompletedMinutes+lifetime*60 };
        if ((shortSale || cover) && (t.IsRetail || ownerId==0)) return "공매도는 기관만 가능합니다.";
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
            if (quantity > State.Bank.ShareInventory[stockIndex] - State.Bank.ReservedLending[stockIndex]-State.Bank.MarketAccount.ReservedShares[stockIndex]) return "대여 가능 주식이 부족합니다.";
        }
        if ((buy || shortSale) && Reserve(t, incoming, quantity) > (cover ? t.Cash - t.ReservedCash : AvailableCash(t))) return "주문 가능 현금/담보가 부족합니다.";
        if (!buy && !shortSale && quantity > t.Shares[stockIndex] - t.ReservedShares[stockIndex]-(ownerId==0 ? State.Bank.ReservedLending[stockIndex] : 0)) return "주문 가능 주식이 부족합니다.";
        incoming.Id = State.NextOrderId++;
        SetReservation(t, incoming, quantity);
        var opposite = buy ? asks[stockIndex] : bids[stockIndex];
        while (incoming.Remaining > 0 && opposite.Count > 0)
        {
            var resting = opposite[0];
            if (buy ? resting.Price > price : resting.Price < price) break;
            if (resting.OwnerId == ownerId) { CancelOrder(resting.Id); continue; }
            int q = Math.Min(incoming.Remaining, resting.Remaining);
            if(q<=0) throw new InvalidDataException("체결 호가의 수량이 0입니다.");
            SetReservation(t, incoming, -q); SetReservation(Owner(resting.OwnerId), resting, -q);
            Execute(buy ? incoming : resting, buy ? resting : incoming, resting.Price, q);
            incoming.Remaining -= q; resting.Remaining -= q;
            if (resting.Remaining == 0) { opposite.RemoveAt(0); ownedOrders[resting.OwnerId].Remove(resting); }
        }
        if (incoming.Remaining > 0) { State.Orders.Add(incoming); AddResting(incoming); }
        if (!t.IsRetail && ownerId!=0) t.LastAction = $"{State.Stocks[stockIndex].Symbol} {quantity}주 {(shortSale ? "공매도" : cover ? "공매도 상환" : buy ? "매수" : "매도")} 호가";
        cachedStats = null;
        BookRevision++;
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
        orderById[o.Id]=o; expirations.Enqueue(o,(o.ExpiresMinute,o.Id));
    }
    public bool CancelOrder(long id)
    {
        if(!orderById.TryGetValue(id,out var o) || o.Remaining<=0) return false;
        SetReservation(Owner(o.OwnerId), o, -o.Remaining);
        (o.Buy ? bids[o.StockIndex] : asks[o.StockIndex]).Remove(o);
        ownedOrders[o.OwnerId].Remove(o); o.Remaining = 0; cachedStats = null; BookRevision++;
        return true;
    }
    public void CancelOrders(int id)
    {
        if (!ownedOrders.TryGetValue(id, out var owned) || owned.Count==0) return;
        foreach (var o in owned)
        {
            SetReservation(Owner(id), o, -o.Remaining);
            (o.Buy ? bids[o.StockIndex] : asks[o.StockIndex]).Remove(o); o.Remaining = 0;
        }
        owned.Clear(); cachedStats = null; BookRevision++;
    }
    public void CancelAllOrders()
    {
        foreach(var order in State.Orders)
            if(order.Remaining>0) { SetReservation(Owner(order.OwnerId),order,-order.Remaining); order.Remaining=0; }
        State.Orders.Clear(); ownedOrders.Clear(); orderById.Clear(); expirations.Clear();
        foreach(var book in bids) book.Clear(); foreach(var book in asks) book.Clear(); cachedStats=null; BookRevision++;
    }
    void ExpireOrders()
    {
        while(expirations.TryPeek(out var o,out var due) && due.Minute<=State.CompletedMinutes)
        {
            expirations.Dequeue(); orderById.Remove(o.Id);
            if (o.Remaining == 0) continue;
            SetReservation(Owner(o.OwnerId), o, -o.Remaining);
            (o.Buy ? bids[o.StockIndex] : asks[o.StockIndex]).Remove(o); ownedOrders[o.OwnerId].Remove(o); o.Remaining = 0; BookRevision++;
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
            if(buyer.ShortShares[i]==0 && buyer.Development is {} coverTiming) coverTiming.ShortOpenedHours[i]=-1;
            PayTradeTax(buyer, profit);
        }
        else
        { RecordPositionOpen(buyer,i,false,buyer.Shares[i]==0); buyer.AverageCost[i] = (buyer.Shares[i] * buyer.AverageCost[i] + amount) / (buyer.Shares[i] + quantity); buyer.Shares[i] += quantity; }
        TransferCash(buyer, seller, amount, "stock-trade"); TransferCash(buyer, State, fee, "exchange-fee");
        buyer.BuyCashFlow += amount; buyer.Fees += fee; buyer.Trades++;
        TransferCash(seller, State, fee, "exchange-fee"); seller.SellCashFlow += amount;
        if (ask.Short)
        {
            RecordPositionOpen(seller,i,true,seller.ShortShares[i]==0);
            seller.ShortAveragePrice[i] = (seller.ShortShares[i] * seller.ShortAveragePrice[i] + amount) / (seller.ShortShares[i] + quantity);
            seller.ShortShares[i] += quantity; State.Bank.ShareInventory[i] -= quantity;
        }
        else
        {
            double profit = amount - quantity * seller.AverageCost[i]; seller.RealizedProfit += profit;
            seller.Shares[i] -= quantity; if (seller.Shares[i] == 0) seller.AverageCost[i] = 0; PayTradeTax(seller, profit);
            if(seller.Shares[i]==0 && seller.Development is {} soldTiming) soldTiming.PositionOpenedHours[i]=-1;
        }
        seller.Fees += fee; seller.Trades++;
        buyer.TradedVolume += quantity; seller.TradedVolume += quantity; buyer.TradedTurnover += amount; seller.TradedTurnover += amount;
        State.ExchangeRevenue += fee * 2; State.TotalMatches++;
        State.MatchedVolume=checked(State.MatchedVolume+quantity); State.MatchedTurnover=checked(State.MatchedTurnover+amount);
        State.TotalAiTrades += (buyer.Id is >0 and <=AiCount ? 1 : 0) + (seller.Id is >0 and <=AiCount ? 1 : 0);
        if(buyer.Id==0) State.Bank.InterventionPurchases+=amount;
        if(seller.Id==0) State.Bank.InterventionSales+=amount;
        var s = State.Stocks[i]; s.Price = s.LastTradePrice = price; s.FractionalMark = 0; s.Volume += quantity; s.DayVolume += quantity; s.TotalVolume += quantity;
        State.SectorVolumes[s.Sector]=checked(State.SectorVolumes.GetValueOrDefault(s.Sector)+quantity);
        State.SectorTurnovers[s.Sector]=checked(State.SectorTurnovers.GetValueOrDefault(s.Sector)+amount);
        s.DayTurnover += amount; s.TotalTurnover += amount;
        State.Tape.Insert(0, new TradeRecord { Minute=State.Minute,AbsoluteMinute=State.CompletedMinutes, Season = State.Season, Day = State.Day, Hour = State.Hour, BuyerId = buyer.Id,
            SellerId = seller.Id, StockIndex = i, SecurityId = s.SecurityId, Quantity = quantity, Price = price, ShortSale = ask.Short, ShortCover = bid.Cover });
        if (State.Tape.Count > TapeLimit) State.Tape.RemoveAt(State.Tape.Count - 1);
    }
    void PayTradeTax(Trader t, double profit)
    {
        if(t.Id==0) return;
        t.SeasonRealizedProfit += profit;
        long target = (long)Math.Ceiling(Math.Max(0, t.SeasonRealizedProfit - t.LossCarryForward) * State.Government.Policy.TaxRate);
        long delta = target - t.SeasonTaxPaid;
        if (delta > 0)
        {
            // Delisting can realize short gains without creating cash. Recognize
            // the bill and any arrears; ordinary covered trades still pay in full.
            long paid=Math.Min(delta,Math.Max(0,t.Cash-t.ReservedCash));
            TransferCash(t, State.Government, paid, "trade-tax");
            t.TaxesPaid+=paid; t.TaxDebt+=delta-paid;
            t.Taxes += delta; t.SeasonTaxPaid += delta; State.Government.Taxes += delta; State.Government.TaxEscrow += paid;
        }
        else if (delta < 0)
        {
            long waived=Math.Min(t.TaxDebt,-delta),refund=-delta-waived; t.TaxDebt-=waived;
            TransferCash(State.Government, t, refund, "tax-refund"); t.TaxesPaid-=refund;
            t.Taxes += delta; t.SeasonTaxPaid += delta; State.Government.Taxes += delta; State.Government.TaxEscrow -= refund;
            State.Government.TaxRefunds +=refund;
        }
    }
}
