using System.Text.Json;

namespace AlphaExchange.Core;
public sealed partial class GameEngine
{
    public string Serialize()
    {
        State.Orders.RemoveAll(o => o.Remaining <= 0);
        return JsonSerializer.Serialize(State,State.Version>=6 ? StateJson : null);
    }
    public static GameEngine Deserialize(string json)
    {
        var s = JsonSerializer.Deserialize<GameState>(json,StateJson) ?? throw new InvalidDataException("비어 있는 저장 데이터");
        int source = s.Version, count = source >= 5 ? s.Stocks.Count : source == 4 ? 10 : 8;
        if (source is not (2 or 3 or 4 or 5 or 6) || count < 8 || count > 2048 || s.Stocks.Count != count ||
            s.RandomState == 0 || s.CompletedHours < 0 || !double.IsFinite(s.HourProgress) || s.HourProgress is < 0 or >= 1 ||
            s.Bots.Count != AiCount || s.FollowedId is < 1 or > AiCount || !Guid.TryParseExact(s.RunId, "N", out _))
            throw new InvalidDataException("지원하지 않는 저장 데이터");
        s.Bots.Sort((a,b) => a.Id.CompareTo(b.Id));
        if (s.Bots.Where((t,i) => t.Id != i+1 || t.IsRetail).Any() || s.Stocks.Any(x => x.Price is < 1 or > 10_000_000 ||
            x.FractionalMark is < 0 or >= 1 || x.PreviousPrice < 1 || x.DayOpenPrice < 1 || x.StartPrice < 1 ||
            !double.IsFinite(x.FairValue) || x.FairValue <= 0 || !double.IsFinite(x.Sentiment) || x.History.Count is < 1 or > HistoryLimit ||
            x.History.Any(v => !double.IsFinite(v) || v <= 0) || x.TotalVolume < 0 || x.TotalTurnover < 0))
            throw new InvalidDataException("시장 데이터 손상");
        foreach (var t in s.Bots.Concat(s.Retail))
        {
            if(source==6) NormalizeCompactTrader(t,count);
            if (source < 4)
            { t.ReservedShares = new long[count]; t.ShortShares = new long[count]; t.ShortAveragePrice = new double[count]; t.ReservedCovers = new long[count]; }
            ValidateTrader(t, count, source >= 4);
        }
        if (source == 2)
        {
            if (s.CompletedHours > 720) throw new InvalidDataException("이전 시즌 시간 손상");
            s.Orders.Clear(); s.Retail.Clear(); s.Tape.Clear(); s.PendingSeasons.Clear();
            var legacy = new GameEngine(s); s.MigratedFromV1 = true;
            foreach (var t in s.Bots)
            {
                t.LegacyFees = t.Fees; t.LegacyRealizedProfit = t.RealizedProfit; t.Fees = 0; t.RealizedProfit = 0;
                t.OpeningCash = t.Cash; t.OpeningEquity = t.Equity(s.Stocks); t.SeasonOpeningEquity = InitialCash;
                t.OpeningUnrealized = t.Financials(s.Stocks).UnrealizedProfit; t.BuyCashFlow = t.SellCashFlow = 0;
                t.EquityHistory = t.EquityHistory.TakeLast(HistoryLimit).ToList();
            }
            for (int i = 0; i < 8; i++) s.Stocks[i].Sector = CompanyCatalog.Companies[i].Sector;
            legacy.CreateRetail();
            s.InitialSystemCash = legacy.Participants.Sum(t => t.Cash) + s.FeePool;
            for (int i = 0; i < 8; i++) s.Stocks[i].TotalShares = legacy.Participants.Sum(t => t.Shares[i]);
        }
        if (s.Retail.Count != RetailCount || s.Retail.Where((t,i) => t.Id != AiCount+i+1 || !t.IsRetail).Any() ||
            s.RetailCursor is < 0 or >= RetailCount || s.FeePool < 0 || s.NextOrderId < 1 || s.LastArchivedSeason < 0 || s.LastArchivedSeason >= s.Season)
            throw new InvalidDataException("참가자 데이터 손상");
        if (source < 5)
        {
            long oldCash = s.Bots.Concat(s.Retail).Sum(t => t.Cash) + s.FeePool + (source == 4 ? s.Bank.Cash + s.Government.Cash : 0);
            if (oldCash != s.InitialSystemCash || Enumerable.Range(0,count).Any(i =>
                s.Bots.Concat(s.Retail).Sum(t => t.Shares[i]) + (source == 4 ? s.Bank.ShareInventory[i] : 0) != s.Stocks[i].TotalShares))
                throw new InvalidDataException("기존 장부 손상");
            UpgradeMarket(s, source);
            count = s.Stocks.Count;
        }
        ValidateEconomy(s, count);
        if(source<6)
        {
            foreach(var t in s.Bots.Concat(s.Retail)) t.TaxesPaid=t.Taxes;
            foreach(var t in s.Retail) t.SeasonReturnBasis=t.SeasonSnapshot.ReturnIndex;
            s.Version=6;
        }
        ValidateLifecycle(s);
        if (s.SecurityIds.Count != count || !s.SecurityIds.SequenceEqual(s.Stocks.Select(x => x.SecurityId)) ||
            s.SecurityIds.Any(string.IsNullOrWhiteSpace) || s.SecurityIds.Distinct().Count() != count)
            throw new InvalidDataException("종목 ID 손상");
        if (s.Orders.Select(o => o.Id).Distinct().Count() != s.Orders.Count || s.Orders.Any(o => o.Id < 1 || o.Id >= s.NextOrderId ||
            o.OwnerId is < 0 or > AiCount+RetailCount || o.StockIndex < 0 || o.StockIndex >= count ||
            o.SecurityId != s.Stocks[o.StockIndex].SecurityId || !s.Stocks[o.StockIndex].Active || o.Price is < 1 or > 10_000_000 ||
            o.Remaining is <= 0 or > 1_000_000 || o.ExpiresAt < s.CompletedHours || (o.Short && o.Buy) || (o.Cover && !o.Buy) ||
            (o.Short && o.Cover) || ((o.Short || o.Cover) && (o.OwnerId==0 || o.OwnerId > AiCount)))) throw new InvalidDataException("호가 데이터 손상");
        if (s.Orders.Any(o => o.Short && o.CollateralPrice is < 1 or > 10_000_000) ||
            s.Orders.Where(o => o.Short || o.Cover).GroupBy(o => (o.OwnerId,o.StockIndex)).Any(g => g.Any(o => o.Short) && g.Any(o => o.Cover)))
            throw new InvalidDataException("공매도 담보 데이터 손상");
        var engine = new GameEngine(s);
        if (engine.Participants.Any(t => t.ReservedCash > t.Cash || t.ReservedShares.Where((q,i) => q > t.Shares[i]).Any() ||
            t.ReservedCovers.Where((q,i) => q > t.ShortShares[i]).Any()) || s.Bank.MarketAccount.ReservedCash>s.Bank.Cash ||
            s.Bank.ReservedLending.Where((q,i) => q+s.Bank.MarketAccount.ReservedShares[i] > s.Bank.ShareInventory[i]).Any())
            throw new InvalidDataException("주문 담보 부족");
        for (int i = 0; i < count; i++)
        {
            var stock = s.Stocks[i];
            if (engine.Participants.Sum(t => t.Shares[i]) + s.Bank.ShareInventory[i] + stock.FounderShares + stock.TreasuryShares != stock.TotalShares)
                throw new InvalidDataException("주식 수 불일치");
            if (engine.bids[i].Count > 0 && engine.asks[i].Count > 0 && engine.bids[i][0].Price >= engine.asks[i][0].Price)
                throw new InvalidDataException("교차 호가 손상");
        }
        if (engine.SystemCash() != s.InitialSystemCash) throw new InvalidDataException("현금 장부 불일치");
        if (s.PendingSeasons.Any(r => r.Season <= s.LastArchivedSeason || r.Season >= s.Season || r.Standings.Count != AiCount ||
            r.Standings.Select(x => x.TraderId).Distinct().Count() != AiCount)) throw new InvalidDataException("시즌 기록 손상");
        if (s.DailyHistory.Count is < 1 or > 121 || s.OpeningSnapshot is null || s.SeasonSnapshot is null ||
            s.Stocks.Any(stock => stock.Reports.Count is < 1 or > 12) || s.Operations.Count > 80 || s.Operations.Any(o =>
            o.LeaderId is < 1 or > AiCount || o.PartnerId is < 1 or > AiCount || o.LeaderId == o.PartnerId ||
            o.StockIndex < 0 || o.StockIndex >= count || !Enum.IsDefined(o.Status))) throw new InvalidDataException("기록 데이터 손상");
        return engine;
    }
    static void UpgradeMarket(GameState s, int source)
    {
        s.LegacyShares = s.Stocks.Select(stock => stock.TotalShares).ToArray();
        var engine = new GameEngine(s);
        if (source < 4)
        {
            s.Bank = new BankState { ShareInventory = new long[s.Stocks.Count], ReservedLending = new long[s.Stocks.Count] };
            s.Government = new GovernmentState(); s.Government.Policy.Season = s.Season;
            s.Government.History = [s.Government.Policy];
            foreach (var t in engine.Participants) if (!t.IsRetail) engine.InitializeRepresentative(t);
            for (int i = 0; i < s.Stocks.Count; i++) { s.Bank.ShareInventory[i] = 500; s.Stocks[i].TotalShares += 500; }
        }
        s.RealEconomy = new RealEconomyState();
        for (int i = 0; i < s.Stocks.Count; i++)
        {
            var stock = s.Stocks[i]; var definition = CompanyCatalog.Companies.First(c => c.Symbol == stock.Symbol);
            var profile = CompanyCatalog.Create(definition);
            stock.CostRatio = profile.CostRatio; stock.DemandSensitivity = profile.DemandSensitivity; stock.DividendPayout = profile.DividendPayout;
            stock.SecurityId = definition.SecurityId; stock.Business = definition.Business; stock.Driver = definition.Driver;
            stock.NewsExposure = definition.NewsExposure; stock.CapitalPolicy = definition.CapitalPolicy; stock.LastTradePrice = stock.Price;
            var oldReports = stock.Reports.Select(CopyReport).ToList();
            foreach (var report in oldReports) { report.AccountingBasis = source; report.SecurityId = stock.SecurityId; report.AverageShares = report.OutstandingShares = stock.OutstandingShares; report.SplitFactor = 1; }
            engine.InitializeBusiness(stock);
            stock.Reports = oldReports.TakeLast(11).Append(CopyReport(stock.Report)).ToList();
        }
        s.SecurityIds = s.Stocks.Select(stock => stock.SecurityId).ToList();
        foreach (var order in s.Orders) if (order.StockIndex >= 0 && order.StockIndex < s.Stocks.Count) order.SecurityId = s.Stocks[order.StockIndex].SecurityId;
        engine.ResizeSecurities();
        double legacyIndex = s.OpeningSnapshot is { Capitalization: > 0 } open ? 1000.0 * s.Stocks.Select((stock,i)=>(double)stock.MarkPrice*s.LegacyShares[i]).Sum() / open.Capitalization : 1000;
        s.PriceIndex = s.TotalReturnIndex = s.LastPriceIndex = legacyIndex;
        s.IndexDivisor = s.Stocks.Sum(x => x.MarketCap) / legacyIndex;
        engine.RestoreSectorListings();
        if (s.DailyHistory.Count == 0) engine.InitializeHistory();
        foreach (var t in engine.Participants)
        {
            t.ReturnIndex = (double)t.Equity(s.Stocks) / Math.Max(1, t.OpeningEquity);
            t.LastReturnEquity = t.Equity(s.Stocks); t.LastNetContribution = t.NetContribution;
            if (source < 4) { t.OpeningSnapshot = engine.CaptureTrader(t); t.OpeningSnapshot.Equity = t.OpeningEquity; t.OpeningSnapshot.Cash = t.OpeningCash; t.OpeningSnapshot.NetIncome = 0; }
            t.OpeningSnapshot.ReturnIndex = 1;
            t.SeasonSnapshot = engine.CaptureTrader(t); t.SeasonSnapshot.Equity = Math.Max(1,t.SeasonOpeningEquity);
            t.SeasonSnapshot.ReturnIndex = (double)Math.Max(1,t.SeasonOpeningEquity) / Math.Max(1,t.OpeningEquity);
        }
        s.OpeningSnapshot = GameStore.NormalizeLegacy(s.OpeningSnapshot ?? engine.CaptureSnapshot(),engine);
        s.SeasonSnapshot = GameStore.NormalizeLegacy(s.SeasonSnapshot ?? engine.CaptureSnapshot(),engine);
        s.DailyHistory = s.DailyHistory.Select(day => GameStore.NormalizeLegacy(day,engine)).ToList();
        s.InitialSystemCash = engine.SystemCash(); s.Version = 5;
        s.MigratedFromV3 = source == 3; s.MigratedFromV4 = source == 4;
        if (source == 2 && s.CompletedHours == 720) engine.FinishSeason(1);
        if (s.News.Count == 0) engine.PublishNews();
    }
    static void ValidateEconomy(GameState s, int count)
    {
        foreach (var t in s.Bots.Concat(s.Retail)) ValidateTrader(t,count,true);
        var p = s.Government.Policy;
        if (s.Bank.Cash < 0 || s.Government.Cash < 0 || s.RealEconomy.Cash < 0 || s.Bank.ShareInventory.Length != count ||
            s.Bank.ReservedLending.Length != count || s.Bank.ShareInventory.Any(q => q < 0) ||
            s.Stocks.Any(x => x.Report.Cash < 0 || x.Report.Debt < 0 || x.Report.TradePayables<0 || x.TotalShares < 0 || x.TotalShares>10_000_000_000L || x.TreasuryShares < 0 || x.FounderShares < 0 ||
                x.OutstandingShares < x.FounderShares || !double.IsFinite(x.SplitFactor) || x.SplitFactor <= 0) ||
            s.Government.TaxEscrow < 0 || s.Government.TaxEscrow > s.Government.Cash ||
            !double.IsFinite(s.IndexDivisor) || s.IndexDivisor <= 0 || !double.IsFinite(s.TotalReturnIndex) || s.TotalReturnIndex < 0 ||
            p.FeeBasisPoints is < 0 or > 100 || !double.IsFinite(p.TaxRate) || p.TaxRate is < 0 or > .5 ||
            !double.IsFinite(p.BaseRate) || p.BaseRate is < 0 or > .5 || !double.IsFinite(p.LoanLimitMultiplier) || p.LoanLimitMultiplier is < 0 or > 1 ||
            !double.IsFinite(p.ShortExposureLimit) || p.ShortExposureLimit is < 0 or > 1 || !double.IsFinite(p.SubsidyRate) || p.SubsidyRate is < 0 or > .01 ||
            !double.IsFinite(p.Enforcement) || p.Enforcement is < 0 or > 1 || !double.IsFinite(p.SpendingRatio) || p.SpendingRatio is < 0 or > 1 ||
            !double.IsFinite(p.DividendTaxRate) || p.DividendTaxRate is < 0 or > .5)
            throw new InvalidDataException("경제 데이터 손상");
    }
    static void ValidateTrader(Trader t, int count, bool modern)
    {
        if (t.Cash < 0 || t.Shares.Length != count || t.AverageCost.Length != count || t.ReservedShares.Length != count ||
            t.Shares.Any(q => q < 0) || t.AverageCost.Any(c => c < 0 || !double.IsFinite(c)) || !double.IsFinite(t.RealizedProfit) ||
            !double.IsFinite(t.OpeningUnrealized) || !double.IsFinite(t.Risk) || !double.IsFinite(t.Patience) || !Enum.IsDefined(t.Strategy) || t.Fees < 0)
            throw new InvalidDataException("포트폴리오 데이터 손상");
        if (modern && (t.ShortShares.Length != count || t.ShortAveragePrice.Length != count || t.ReservedCovers.Length != count ||
            t.ShortShares.Any(q => q < 0) || t.ShortAveragePrice.Any(p => p < 0 || !double.IsFinite(p)) || t.LoanDebt < 0 || t.FineDebt < 0 ||
            t.ShortDividendDebt < 0 || t.TaxDebt<0 || t.TaxesPaid<0 || t.Taxes < 0 || t.Fines < 0 || t.Subsidies < 0 || t.TradedVolume < 0 || t.TradedTurnover < 0 ||
            t.CreditScore is < 0 or > 99 || !Enum.IsDefined(t.Disposition) || !double.IsFinite(t.Integrity) || t.Integrity is < 0 or > 1 ||
            !double.IsFinite(t.ReturnIndex) || t.ReturnIndex < 0 || (!t.IsRetail && t.Abilities.Values().Any(a => a is < 1 or > 100)) ||
            (t.IsRetail && t.ShortShares.Any(q => q != 0)))) throw new InvalidDataException("신용·능력 데이터 손상");
    }
    static void ValidateLifecycle(GameState s)
    {
        if(s.NextVoteId<1 || s.NextBankruptcyId<1 || s.CompanyVotes.Count>Math.Max(180,s.Stocks.Count*6) || s.Bankruptcies.Count>64 ||
            s.Bots.Concat(s.Retail).Any(t=>t.Generation<1 || t.BirthHour<0 || t.BirthHour>s.CompletedHours || !double.IsFinite(t.SeasonReturnBasis) || t.SeasonReturnBasis<0) ||
            s.Stocks.Any(x=>x.Generation<1 || x.InsolventMonths<0 || !Enum.IsDefined(x.CompanyStrategy) || !Enum.IsDefined(x.Management) ||
                !Enum.IsDefined(x.Allocation) || !double.IsFinite(x.TreasuryTarget) || x.TreasuryTarget is <0 or >1 || x.WaitingForCapital && x.Active) ||
            s.CompanyVotes.Select(v=>v.Id).Distinct().Count()!=s.CompanyVotes.Count || s.CompanyVotes.Any(v=>v.Id<1 || v.Id>=s.NextVoteId ||
                v.OpenHour<0 || v.OpenHour>s.CompletedHours || v.CloseHour<v.OpenHour || !Enum.IsDefined(v.Kind) || !Enum.IsDefined(v.Status) || v.Option is <0 or >2 ||
                v.EligibleShares<0 || v.ForShares<0 || v.AgainstShares<0 || v.AbstainShares<0 ||
                v.Status!=VoteStatus.Open && (v.CloseHour>s.CompletedHours || v.ForShares+v.AgainstShares+v.AbstainShares!=v.EligibleShares) ||
                v.Ballots.Any(b=>b.Shares<=0 || !Enum.IsDefined(b.Choice)) || v.Ballots.Sum(b=>b.Shares)!=v.ForShares+v.AgainstShares+v.AbstainShares) ||
            s.Bankruptcies.Select(b=>b.Id).Distinct().Count()!=s.Bankruptcies.Count || s.Bankruptcies.Any(b=>b.Id<1 || b.Id>=s.NextBankruptcyId ||
                b.Hour<0 || b.Hour>s.CompletedHours || b.Generation<1 || !Enum.IsDefined(b.Kind) || b.Assets<0 || b.Liabilities<0 || b.Equity!=b.Assets-b.Liabilities ||
                b.LoanWriteOff<0 || b.ShortWriteOff<0 || b.TaxWriteOff<0 || b.FineWriteOff<0 || b.ReplacementCapital<0 || !double.IsFinite(b.TradingIncome)) ||
            s.BankruptcyTotals.Companies<0 || s.BankruptcyTotals.Institutions<0 || s.BankruptcyTotals.Retail<0 ||
            s.BankruptcyTotals.Total!=s.NextBankruptcyId-1 || s.Bank.LoanWriteOffs<0 || s.Bank.ShortWriteOffs<0 || s.Government.TaxWriteOffs<0 || s.Government.FineWriteOffs<0)
            throw new InvalidDataException("주총·파산·세대 데이터 손상");
        var dealer=s.Bank.MarketAccount;
        if(dealer.Id!=0 || dealer.IsRetail || dealer.LoanDebt!=0 || dealer.FineDebt!=0 || dealer.ShortShares.Any(q=>q!=0) ||
            dealer.AverageCost.Any(p=>p<0 || !double.IsFinite(p))) throw new InvalidDataException("은행 시장 계정 손상");
    }
}
