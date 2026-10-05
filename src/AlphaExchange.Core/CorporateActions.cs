using System.Text.Json;

namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    public event Action<CorporateEvent>? CorporateEventRecorded;
    public int SecurityIndex(string id) => State.Stocks.FindIndex(s => s.SecurityId == id);
    static CompanyReport CopyReport(CompanyReport report) => CheckpointCopy.Scalar(report);
    void CancelSecurity(int index)
    {
        foreach (var order in State.Orders.Where(o => o.StockIndex == index && o.Remaining > 0).ToArray()) CancelOrder(order.Id);
        State.Orders.RemoveAll(o => o.Remaining == 0);
        foreach (var op in State.Operations.Where(o => o.StockIndex == index && o.Status == OperationStatus.Active))
        { op.Status = OperationStatus.Completed; op.Detail = "기업행동으로 공동 매집 종료"; }
    }
    void LogCorporate(Stock stock, CorporateEventKind kind, string detail, long amount = 0,
        long numerator = 1, long denominator = 1, string other = "", long shares = 0)
    {
        var corporateEvent = new CorporateEvent { Id = State.NextCorporateEventId++, Hour = State.CompletedHours,
            SecurityId = stock.SecurityId, OtherSecurityId = other, Kind = kind, Amount = amount, Shares = shares,
            Numerator = numerator, Denominator = denominator, Detail = detail };
        Append(State.CorporateEvents,corporateEvent,120); CorporateEventRecorded?.Invoke(corporateEvent);
        cachedStats = null;
    }

    public long PayDividend(string securityId, long budget)
    {
        int i = SecurityIndex(securityId);
        if (i < 0 || budget <= 0) return 0;
        var s = State.Stocks[i]; if (!s.Active || s.OutstandingShares <= 0) return 0;
        long perShare = Math.Min(Math.Min(budget, s.Report.Cash) / s.OutstandingShares, Math.Max(0, s.Price - 1));
        if (perShare == 0) return 0;
        CancelSecurity(i);
        // Historical reports are immutable even when an action changes the live balance.
        if (s.Reports.Contains(s.Report)) s.Report = CopyReport(s.Report);
        var report = s.Report;
        long total = checked(perShare * s.OutstandingShares);
        foreach (var t in Participants)
        {
            long gross = checked(t.Shares[i] * perShare);
            long tax = (long)(gross * State.Government.Policy.DividendTaxRate);
            TransferCash(report, t, gross - tax, "dividend");
            TransferCash(report, State.Government, tax, "dividend-tax");
            t.DividendIncome += gross; t.DividendTax += tax; State.Government.DividendTaxes += tax;
            long compensation = checked(t.ShortShares[i] * perShare);
            long paid = Math.Min(compensation, AvailableCash(t));
            TransferCash(t, State.Bank, paid, "short-dividend");
            t.ShortDividendExpense += compensation; t.ShortDividendPaid += paid; t.ShortDividendDebt += compensation - paid;
            State.Bank.DividendIncome += paid;
        }
        long bankDividend = checked(State.Bank.ShareInventory[i] * perShare);
        TransferCash(report, State.Bank, bankDividend, "bank-dividend"); State.Bank.DividendIncome += bankDividend;
        TransferCash(report, State.RealEconomy, checked(s.FounderShares * perShare), "founder-dividend");
        report.Dividends += total; report.DividendsPerShare += perShare; report.FinancingCashFlow -= total;
        if (!closingBooks) { s.PendingDividends += total; s.PendingDividendPerShare += perShare; s.PendingFinancingFlow -= total; }
        s.LastDividendPerShare = perShare; s.TotalDividends += total; State.MarketDividends += total; State.DividendIndexPoints += total / State.IndexDivisor;
        s.Price -= (int)perShare;
        LogCorporate(s, CorporateEventKind.Dividend, $"주당 {perShare:N0}원 현금 배당 · 배당락 기준 조정", total);
        return total;
    }

    string? AtomicAction(Func<string?> operation)
    {
        var before = CheckpointCopy.Freeze(State);
        try
        {
            string? error = operation();
            if (error is not null) { State = before; RebuildBooks(); cachedStats=null; cachedPeriod=null; }
            return error;
        }
        catch (Exception e) when (e is OverflowException or InvalidOperationException or ArgumentException)
        { State = before; RebuildBooks(); cachedStats=null; cachedPeriod=null; return "기업행동을 적용하지 못했습니다: " + e.Message; }
    }

    public string? SplitShares(string id, int numerator, int denominator = 1)
    {
        int i = SecurityIndex(id);
        if (i < 0 || !State.Stocks[i].Active || numerator < 1 || denominator < 1 || numerator > 20 || denominator > 20 || numerator == denominator)
            return "분할/병합 비율이 올바르지 않습니다.";
        return AtomicAction(() => TransformShares(i, numerator, denominator));
    }
    string? TransformShares(int i, int numerator, int denominator)
    {
        var s = State.Stocks[i]; decimal mark = s.MarkPrice;
        decimal nextPrice = mark * denominator / numerator;
        if (nextPrice < 1 || nextPrice > 10_000_000 || s.TotalShares > 10_000_000_000L / numerator)
            return "가격/수량 범위를 초과하여 사건을 연기합니다.";
        long Remainder(long quantity) => quantity * numerator % denominator;
        long Convert(long quantity) => checked(quantity * numerator / denominator);
        long CashFraction(long quantity) => (long)decimal.Round(Remainder(quantity) * mark / numerator);
        long payouts = Participants.Sum(t => CashFraction(t.Shares[i])) + CashFraction(State.Bank.ShareInventory[i]) + CashFraction(s.FounderShares);
        if (payouts > s.Report.Cash || Participants.Any(t => CashFraction(t.ShortShares[i]) > AvailableCash(t)))
            return "단주 현금 정산 재원이 부족하여 사건을 연기합니다.";
        long beforeCap = State.Stocks.Sum(x => x.MarketCap);
        CancelSecurity(i);
        if (s.Reports.Contains(s.Report)) s.Report = CopyReport(s.Report);
        foreach (var t in Participants)
        {
            long oldLong = t.Shares[i], oldShort = t.ShortShares[i]; double beforeProfit=t.RealizedProfit;
            long fractionCash = CashFraction(oldLong);
            double removedQuantity = (double)Remainder(oldLong) / numerator;
            if (fractionCash > 0)
            {
                TransferCash(s.Report, t, fractionCash, "fractional-share");
                t.SellCashFlow += fractionCash; t.RealizedProfit += fractionCash - removedQuantity * t.AverageCost[i];
            }
            long shortCash = CashFraction(oldShort);
            if (shortCash > 0)
            {
                TransferCash(t, State.Bank, shortCash, "fractional-short");
                t.BuyCashFlow += shortCash;
                t.RealizedProfit += (double)Remainder(oldShort) / numerator * t.ShortAveragePrice[i] - shortCash;
            }
            t.Shares[i] = Convert(oldLong); t.ShortShares[i] = Convert(oldShort);
            t.AverageCost[i] = t.Shares[i] == 0 ? 0 : t.AverageCost[i] * denominator / numerator;
            t.ShortAveragePrice[i] = t.ShortShares[i] == 0 ? 0 : t.ShortAveragePrice[i] * denominator / numerator;
            RefreshPositionTiming(t,i);
            PayTradeTax(t,t.RealizedProfit-beforeProfit);
        }
        TransferCash(s.Report, State.Bank, CashFraction(State.Bank.ShareInventory[i]), "bank-fractional-share");
        TransferCash(s.Report, State.RealEconomy, CashFraction(s.FounderShares), "founder-fractional-share");
        State.Bank.ShareInventory[i] = Convert(State.Bank.ShareInventory[i]);
        s.FounderShares = Convert(s.FounderShares); s.TreasuryShares = Convert(s.TreasuryShares);
        s.TotalShares = Participants.Sum(t => t.Shares[i]) + State.Bank.ShareInventory[i] + s.FounderShares + s.TreasuryShares;
        s.Report.CapitalChange -= payouts; s.Report.FinancingCashFlow -= payouts;
        if (!closingBooks) { s.PendingCapitalChange -= payouts; s.PendingFinancingFlow -= payouts; }
        s.Price = (int)decimal.Floor(nextPrice); s.FractionalMark = nextPrice - s.Price;
        s.PreviousPrice = Math.Max(1, (int)(s.PreviousPrice * (double)denominator / numerator));
        s.DayOpenPrice = Math.Max(1, (int)(s.DayOpenPrice * (double)denominator / numerator));
        s.StartPrice = Math.Max(1, (int)(s.StartPrice * (double)denominator / numerator));
        s.FairValue *= (double)denominator / numerator; s.SplitFactor *= (double)numerator / denominator;
        s.History = s.History.Select(p => p * denominator / numerator).ToList();
        s.ParValue = Math.Max(1, s.ParValue * denominator / numerator);
        s.ShareHourSum = checked(s.ShareHourSum * numerator / denominator);
        s.PendingDividendPerShare = s.PendingDividendPerShare * denominator / numerator;
        long afterCap = State.Stocks.Sum(x => x.MarketCap);
        if (beforeCap > 0 && afterCap > 0) State.IndexDivisor *= (double)afterCap / beforeCap;
        LogCorporate(s, numerator > denominator ? CorporateEventKind.Split : CorporateEventKind.ReverseSplit,
            $"{numerator}:{denominator} {(numerator > denominator ? "액면분할" : "주식병합")} · 단주 정산 {payouts:N0}원", payouts, numerator, denominator);
        return null;
    }

    public string? SubscribeIssue(int ownerId, string id, long quantity)
    {
        int i = SecurityIndex(id);
        if (i < 0 || !State.Stocks[i].Active || ownerId < 1 || ownerId > AiCount + RetailCount || quantity <= 0 || quantity > 1_000_000)
            return "공모/증자 값이 올바르지 않습니다.";
        var s = State.Stocks[i]; var t = Owner(ownerId); long amount = checked(quantity * s.Price);
        if (amount > AvailableCash(t) || s.TotalShares + quantity > 10_000_000_000L) return "청약 가능한 현금/수량이 부족합니다.";
        long before = State.Stocks.Sum(x => x.MarketCap);
        if (s.Reports.Contains(s.Report)) s.Report = CopyReport(s.Report);
        TransferCash(t, s.Report, amount, "subscription");
        t.AverageCost[i] = (t.Shares[i] * t.AverageCost[i] + amount) / (t.Shares[i] + quantity);
        RecordPositionOpen(t,i,false,t.Shares[i]==0);
        t.Shares[i] += quantity; t.BuyCashFlow += amount;
        s.TotalShares += quantity; s.Report.CapitalChange += amount; s.Report.FinancingCashFlow += amount;
        if (!closingBooks) { s.PendingCapitalChange += amount; s.PendingFinancingFlow += amount; }
        AdjustDivisor(before);
        LogCorporate(s, CorporateEventKind.Issue, $"공모/유상증자 {quantity:N0}주 · 실제 납입 {amount:N0}원", amount, shares: quantity);
        return null;
    }
    public string? Buyback(string id, int ownerId, long quantity, bool retire = false)
    {
        int i = SecurityIndex(id);
        if (i < 0 || !State.Stocks[i].Active || quantity <= 0 || ownerId < 0 || ownerId > AiCount + RetailCount) return "자사주 값이 올바르지 않습니다.";
        var s = State.Stocks[i]; var t = Owner(ownerId);
        long amount = checked(quantity * s.Price);
        if (t.Shares[i] - t.ReservedShares[i]-(ownerId==0 ? State.Bank.ReservedLending[i] : 0) < quantity || s.Report.Cash < amount) return "자사주 매입 재원/주식이 부족합니다.";
        long before = State.Stocks.Sum(x => x.MarketCap);
        if (s.Reports.Contains(s.Report)) s.Report = CopyReport(s.Report);
        TransferCash(s.Report, t, amount, "buyback");
        double profit=amount-quantity*t.AverageCost[i]; t.RealizedProfit += profit; t.SellCashFlow += amount; PayTradeTax(t,profit);
        t.Shares[i] -= quantity; if (t.Shares[i] == 0) t.AverageCost[i] = 0;
        s.TreasuryShares += quantity; s.Report.CapitalChange -= amount; s.Report.FinancingCashFlow -= amount;
        if (!closingBooks) { s.PendingCapitalChange -= amount; s.PendingFinancingFlow -= amount; }
        if (retire) { s.TreasuryShares -= quantity; s.TotalShares -= quantity; }
        AdjustDivisor(before);
        LogCorporate(s, retire ? CorporateEventKind.Retirement : CorporateEventKind.Buyback,
            $"자사주 {quantity:N0}주 {(retire ? "매입·소각" : "매입")} · {amount:N0}원", amount, shares: quantity);
        return null;
    }
    void AdjustDivisor(long before)
    {
        long after = State.Stocks.Sum(x => x.MarketCap);
        if (before > 0 && after > 0) State.IndexDivisor *= (double)after / before;
    }

    public string? MergeCompanies(string sourceId, string targetId)
    {
        int a = SecurityIndex(sourceId), b = SecurityIndex(targetId);
        if (a < 0 || b < 0 || a == b || !State.Stocks[a].Active || !State.Stocks[b].Active || State.Stocks[a].Sector != State.Stocks[b].Sector)
            return "같은 분야의 서로 다른 상장 회사가 필요합니다.";
        return AtomicAction(() => MergeInternal(a, b));
    }
    string? MergeInternal(int a, int b)
    {
        var source = State.Stocks[a]; var target = State.Stocks[b];
        // Exchange ratio uses public mark prices at the agreed boundary; no arbitrary wealth revaluation.
        long numerator = (long)decimal.Round(source.MarkPrice * 10000), denominator = (long)decimal.Round(target.MarkPrice * 10000);
        long gcd = Gcd(numerator, denominator); numerator /= gcd; denominator /= gcd;
        if (source.TotalShares > long.MaxValue / numerator) return "합병 수량 범위를 초과합니다.";
        long Convert(long q) => checked(q * numerator / denominator);
        long CashFraction(long q) => (long)decimal.Round((q * numerator % denominator) * target.MarkPrice / denominator);
        long payouts = Participants.Sum(t => CashFraction(t.Shares[a])) + CashFraction(State.Bank.ShareInventory[a]) + CashFraction(source.FounderShares);
        if (payouts > source.Report.Cash || Participants.Any(t => CashFraction(t.ShortShares[a]) > AvailableCash(t))) return "합병 단주 정산 재원이 부족합니다.";
        if (source.TotalShares * numerator / denominator + target.TotalShares > 10_000_000_000L) return "합병 주식 수 한도 초과";
        long beforeCap = State.Stocks.Sum(s => s.MarketCap);
        CancelSecurity(a); CancelSecurity(b);
        source.Report = CopyReport(source.Report); target.Report = CopyReport(target.Report);
        foreach (var t in Participants)
        {
            long longs = Convert(t.Shares[a]), shorts = Convert(t.ShortShares[a]); double beforeProfit=t.RealizedProfit;
            long longCash = CashFraction(t.Shares[a]), shortCash = CashFraction(t.ShortShares[a]);
            double removed = (double)(t.Shares[a] * numerator % denominator) / numerator;
            if (longCash > 0)
            { TransferCash(source.Report, t, longCash, "merger-fractional"); t.SellCashFlow += longCash; t.RealizedProfit += longCash - removed * t.AverageCost[a]; }
            if (shortCash > 0)
            { TransferCash(t, State.Bank, shortCash, "merger-short-fractional"); t.BuyCashFlow += shortCash; t.RealizedProfit += (double)(t.ShortShares[a] * numerator % denominator) / numerator * t.ShortAveragePrice[a] - shortCash; }
            t.AverageCost[b] = t.Shares[b] + longs == 0 ? 0 : (t.Shares[b] * t.AverageCost[b] + longs * t.AverageCost[a] * denominator / numerator) / (t.Shares[b] + longs);
            t.ShortAveragePrice[b] = t.ShortShares[b] + shorts == 0 ? 0 : (t.ShortShares[b] * t.ShortAveragePrice[b] + shorts * t.ShortAveragePrice[a] * denominator / numerator) / (t.ShortShares[b] + shorts);
            if(t.Development is {} timing)
            {
                if(longs>0 && t.Shares[b]==0) { timing.PositionOpenedHours[b]=timing.PositionOpenedHours[a]; timing.PositionHorizons[b]=timing.PositionHorizons[a]; }
                if(shorts>0 && t.ShortShares[b]==0) timing.ShortOpenedHours[b]=timing.ShortOpenedHours[a];
            }
            t.Shares[b] += longs; t.ShortShares[b] += shorts; t.Shares[a] = t.ShortShares[a] = 0;
            t.AverageCost[a] = t.ShortAveragePrice[a] = 0;
            RefreshPositionTiming(t,a); RefreshPositionTiming(t,b);
            PayTradeTax(t,t.RealizedProfit-beforeProfit);
        }
        TransferCash(source.Report, State.Bank, CashFraction(State.Bank.ShareInventory[a]), "merger-bank-fractional");
        TransferCash(source.Report, State.RealEconomy, CashFraction(source.FounderShares), "merger-founder-fractional");
        State.Bank.ShareInventory[b] += Convert(State.Bank.ShareInventory[a]); State.Bank.ShareInventory[a] = 0;
        target.FounderShares += Convert(source.FounderShares); target.TreasuryShares += Convert(source.TreasuryShares);
        target.TotalShares = Participants.Sum(t => t.Shares[b]) + State.Bank.ShareInventory[b] + target.FounderShares + target.TreasuryShares;
        source.Report.CapitalChange -= payouts; source.Report.FinancingCashFlow -= payouts;
        if (!closingBooks) { target.PendingCapitalChange += source.PendingCapitalChange - payouts; target.PendingFinancingFlow += source.PendingFinancingFlow - payouts; target.PendingInvestingFlow += source.PendingInvestingFlow; }
        var sr = source.Report; var tr = target.Report;
        TransferCash(sr, tr, sr.Cash, "merger-cash");
        foreach (var property in typeof(CompanyReport).GetProperties().Where(p => p.CanWrite && p.PropertyType == typeof(long)
            && p.Name is not ("Cash" or "Season" or "AverageShares" or "OutstandingShares" or "DividendsPerShare")))
            property.SetValue(tr, checked((long)property.GetValue(tr)! + (long)property.GetValue(sr)!));
        tr.OutstandingShares = target.OutstandingShares; tr.AverageShares = target.OutstandingShares;
        target.BaseRevenue += source.BaseRevenue; target.InitialFixedAssets += source.InitialFixedAssets;
        target.FairValue = Math.Max(1, ((double)tr.Equity / Math.Max(1, target.OutstandingShares)));
        source.TotalShares = source.FounderShares = source.TreasuryShares = 0; source.Active = false;
        source.Report = new CompanyReport { SecurityId = source.SecurityId, Season = State.Season - 1 };
        if (!closingBooks)
        {
            tr.IsOpening = true; Append(target.Reports,CopyReport(tr),12);
            target.PendingCapitalChange = target.PendingFinancingFlow = target.PendingInvestingFlow = target.PendingDividends = target.PendingDividendPerShare = 0;
        }
        AdjustDivisor(beforeCap);
        LogCorporate(target, CorporateEventKind.Merger, $"{source.Symbol} → {target.Symbol} 주식 교환 합병 · {numerator}:{denominator}", payouts, numerator, denominator, source.SecurityId);
        return null;
    }
    static long Gcd(long a, long b) { while (b != 0) (a, b) = (b, a % b); return a; }

    void ResizeSecurities()
    {
        int count = State.Stocks.Count;
        foreach (var t in Participants)
        {
            t.Shares = Resize(t.Shares, count); t.AverageCost = Resize(t.AverageCost, count);
            t.ShortShares = Resize(t.ShortShares, count); t.ShortAveragePrice = Resize(t.ShortAveragePrice, count);
            t.ReservedShares = Resize(t.ReservedShares, count); t.ReservedCovers = Resize(t.ReservedCovers, count);
            if(t.Development is {} d)
            {
                int old=d.PositionOpenedHours.Length;
                d.PositionOpenedHours=Resize(d.PositionOpenedHours,count); d.ShortOpenedHours=Resize(d.ShortOpenedHours,count);
                for(int i=old;i<count;i++) d.PositionOpenedHours[i]=d.ShortOpenedHours[i]=-1;
                d.NextReviewHours=Resize(d.NextReviewHours,count); d.PositionHorizons=Resize(d.PositionHorizons,count);
                d.LastNewsSignals=Resize(d.LastNewsSignals,count); d.NextDecisionHour=State.CompletedHours;
            }
        }
        State.Bank.ShareInventory = Resize(State.Bank.ShareInventory, count); State.Bank.ReservedLending = Resize(State.Bank.ReservedLending, count);
        State.SecurityIds = State.Stocks.Select(s => s.SecurityId).ToList(); RebuildBooks();
    }
    static T[] Resize<T>(T[] values, int count) { Array.Resize(ref values, count); return values; }

    long ListCompany(CompanyDefinition definition,int slot=-1,int generation=1)
    {
        long capital = ListingCapital(definition.InitialPrice);
        if(slot<0) slot=State.Stocks.FindIndex(s=>!s.Active && !s.WaitingForCapital && s.Symbol==definition.Symbol);
        if(slot>=0) generation=Math.Max(generation,State.Stocks[slot].Generation+1);
        long before = State.Stocks.Sum(s => s.MarketCap);
        string id = slot>=0 || State.Stocks.Any(s=>s.SecurityId==definition.SecurityId)
            ? definition.SecurityId+"-g"+generation+"-"+State.NextCorporateEventId : definition.SecurityId;
        var stock = CompanyCatalog.Create(definition, id);
        stock.Generation=generation; stock.Name+=generation>1 ? $" {generation}기" : "";
        stock.FundedIpo = true; stock.ListedSeason = State.Season;
        stock.Report = new CompanyReport { IsOpening = true, SecurityId = id, Season = Math.Max(0, State.Season - 1),
            OutstandingShares = 0, AverageShares = 0 };
        stock.Reports=[CopyReport(stock.Report)];
        if(slot<0) { slot=State.Stocks.Count; State.Stocks.Add(stock); ResizeSecurities(); }
        else { State.Stocks[slot]=stock; State.SecurityIds[slot]=id; }
        if(capital>State.Bank.Cash-State.Bank.MarketAccount.ReservedCash && capital-State.Bank.Cash>State.RealEconomy.Cash/2)
        { stock.Active=false; stock.WaitingForCapital=true; return 0; }
        return FundListing(stock,slot,capital,before);
    }
    long FundListing(Stock stock,int slot,long capital,long before)
    {
        CancelOrders(0);
        long support=Math.Max(0,capital-State.Bank.Cash);
        if(support>State.RealEconomy.Cash/2) return 0;
        TransferCash(State.RealEconomy,State.Bank,support,"bank-recapitalization");
        long floatShares=capital/stock.Price;
        stock.Active=true; stock.WaitingForCapital=false; stock.TotalShares=checked(floatShares*2); stock.TreasuryShares=floatShares;
        State.Bank.ShareInventory[slot]=floatShares; BankAccount().AverageCost[slot]=stock.Price;
        TransferCash(State.Bank, stock.Report, capital, "ipo-bank-subscription");
        stock.Report.CapitalChange += capital; stock.Report.FinancingCashFlow += capital;
        long equipment = capital * 3 / 5;
        TransferCash(stock.Report, State.RealEconomy, equipment, "ipo-equipment");
        stock.Report.FixedAssets += equipment; stock.Report.InvestingCashFlow -= equipment;
        stock.BaseRevenue = capital / 20; stock.InitialFixedAssets = equipment;
        stock.Report.OutstandingShares = stock.Report.AverageShares = stock.OutstandingShares;
        stock.Reports = [CopyReport(stock.Report)]; AdjustDivisor(before);
        LogCorporate(stock, CorporateEventKind.Ipo, $"{stock.Name} 신규 상장 · 자사주 50%/은행 50% · 실제 납입", capital, shares: stock.TotalShares);
        OpenCompanyVotes(stock);
        return capital;
    }
    long ListingCapital(int price)
    {
        long shares=State.World is null ? 1000 : WorldRules.InitialFloatShares>0 ? WorldRules.InitialFloatShares :
            Math.Max(1,WorldRules.MarketCapitalization/CompanyCatalog.Companies.Count()/price);
        return checked(shares*price);
    }
    void RestoreSectorListings()
    {
        foreach (var definition in CompanyCatalog.Companies)
            if (!State.Stocks.Any(s => (s.Active || s.WaitingForCapital) && s.Symbol == definition.Symbol)) ListCompany(definition);
    }
    void RunCorporatePolicy()
    {
        long completedSeason = State.Season - 1;
        if (completedSeason % 4 == 0)
        {
            var candidate = State.Stocks.Where(s => s.Active && s.Price > 60000).OrderByDescending(s => s.Price).FirstOrDefault();
            if (candidate is not null) SplitShares(candidate.SecurityId, 2);
            var low = State.Stocks.Where(s => s.Active && s.Price < 300).OrderBy(s => s.Price).FirstOrDefault();
            if (low is not null) SplitShares(low.SecurityId, 1, 5);
        }
        if (completedSeason > 0 && completedSeason % 12 == 0)
        {
            var sector = State.Stocks.Where(s => s.Active).GroupBy(s => s.Sector).OrderBy(g => g.Key).ElementAt((int)(completedSeason / 12 - 1) % 6).ToArray();
            var a = sector.OrderByDescending(s => s.Price).First(); var b = sector.OrderBy(s => s.Price).First();
            if (a != b) MergeCompanies(a.SecurityId, b.SecurityId);
        }
        foreach(var company in State.Stocks) SeekTreasuryOwnership(company);
    }
}
