using System.Text.Json;

namespace AlphaExchange.Core;
public sealed partial class GameEngine
{
    public string Serialize()
    {
        State.Orders.RemoveAll(o => o.Remaining <= 0);
        return JsonSerializer.Serialize(State);
    }
    public static GameEngine Deserialize(string json)
    {
        var s = JsonSerializer.Deserialize<GameState>(json) ?? throw new InvalidDataException("비어 있는 저장 데이터");
        int sourceVersion = s.Version, count = sourceVersion == 4 ? StockCount : 8;
        if (sourceVersion is not (2 or 3 or 4) || s.RandomState == 0 || s.CompletedHours < 0 || !double.IsFinite(s.HourProgress) || s.HourProgress is < 0 or >= 1 ||
            s.Stocks.Count != count || s.Bots.Count != AiCount || s.FollowedId is < 1 or > AiCount || !Guid.TryParseExact(s.RunId, "N", out _))
            throw new InvalidDataException("지원하지 않는 저장 데이터");
        s.Bots.Sort((a, b) => a.Id.CompareTo(b.Id));
        if (s.Bots.Where((t, i) => t.Id != i + 1 || t.IsRetail).Any() ||
            s.Stocks.Any(x => x.Price is < 100 or > 10_000_000 || x.PreviousPrice <= 0 || x.DayOpenPrice <= 0 || x.StartPrice <= 0 ||
                !double.IsFinite(x.FairValue) || x.FairValue <= 0 || !double.IsFinite(x.Sentiment) || x.History.Count < 1 || x.TotalVolume < 0 || x.TotalTurnover < 0))
            throw new InvalidDataException("시장 데이터 손상");
        foreach (var t in s.Bots.Concat(s.Retail))
        {
            if (sourceVersion < 4)
            { t.ReservedShares = new int[count]; t.ShortShares = new int[count]; t.ShortAveragePrice = new double[count]; t.ReservedCovers = new int[count]; }
            ValidateTrader(t, count, sourceVersion == 4);
        }
        if (sourceVersion == 2)
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
            string[] sectors = ["기술", "산업·에너지", "바이오·헬스", "산업·에너지", "금융", "미디어", "소비재", "기술"];
            for (int i = 0; i < 8; i++) { s.Stocks[i].Sector = sectors[i]; s.Stocks[i].History = s.Stocks[i].History.TakeLast(HistoryLimit).ToList(); }
            foreach (var news in s.News) news.Season = 1;
            s.News = s.News.Take(40).ToList(); legacy.CreateRetail();
            // The old format had no closed-system baseline for retail.
            s.InitialSystemCash = legacy.Participants.Sum(t => t.Cash) + s.FeePool;
            for (int i = 0; i < 8; i++) s.Stocks[i].TotalShares = legacy.Participants.Sum(t => (long)t.Shares[i]);
        }
        if (s.Retail.Count != RetailCount || s.Retail.Where((t, i) => t.Id != AiCount + i + 1 || !t.IsRetail).Any() ||
            s.RetailCursor is < 0 or >= RetailCount || s.FeePool < 0 || s.NextOrderId < 1 || s.LastArchivedSeason < 0 || s.LastArchivedSeason >= s.Season)
            throw new InvalidDataException("참가자 데이터 손상");
        if (sourceVersion < 4)
        {
            if (s.Bots.Concat(s.Retail).Sum(t => t.Cash) + s.FeePool != s.InitialSystemCash ||
                Enumerable.Range(0, 8).Any(i => s.Bots.Concat(s.Retail).Sum(t => (long)t.Shares[i]) != s.Stocks[i].TotalShares))
                throw new InvalidDataException("기존 장부 손상");
            UpgradeEconomy(s, sourceVersion);
        }
        if (s.Bank.Cash < 0 || s.Government.Cash < 0 || s.Bank.ShareInventory.Length != StockCount || s.Bank.ReservedLending.Length != StockCount ||
            s.Bank.ShareInventory.Any(q => q < 0) || s.Government.Policy.FeeBasisPoints is < 0 or > 100 ||
            !double.IsFinite(s.Government.Policy.TaxRate) || s.Government.Policy.TaxRate is < 0 or > .5 ||
            !double.IsFinite(s.Government.Policy.BaseRate) || s.Government.Policy.BaseRate is < 0 or > .5 ||
            !double.IsFinite(s.Government.Policy.LoanLimitMultiplier) || s.Government.Policy.LoanLimitMultiplier is < 0 or > 1 ||
            !double.IsFinite(s.Government.Policy.ShortExposureLimit) || s.Government.Policy.ShortExposureLimit is < 0 or > 1 ||
            !double.IsFinite(s.Government.Policy.SubsidyRate) || s.Government.Policy.SubsidyRate is < 0 or > .01 ||
            !double.IsFinite(s.Government.Policy.Enforcement) || s.Government.Policy.Enforcement is < 0 or > 1)
            throw new InvalidDataException("경제 데이터 손상");
        if (s.Orders.Select(o => o.Id).Distinct().Count() != s.Orders.Count || s.Orders.Any(o => o.Id < 1 || o.Id >= s.NextOrderId ||
            o.OwnerId is < 1 or > AiCount + RetailCount || o.StockIndex is < 0 or >= StockCount || o.Price is < 100 or > 10_000_000 || o.Remaining is <= 0 or > 1_000_000 || o.ExpiresAt < s.CompletedHours ||
            (o.Short && o.Buy) || (o.Cover && !o.Buy) || (o.Short && o.Cover) || ((o.Short || o.Cover) && o.OwnerId > AiCount)))
            throw new InvalidDataException("호가 데이터 손상");
        if (s.Orders.Any(o => o.Short && o.CollateralPrice is < 100 or > 10_000_000) ||
            s.Orders.Where(o => o.Short || o.Cover).GroupBy(o => (o.OwnerId, o.StockIndex)).Any(g => g.Any(o => o.Short) && g.Any(o => o.Cover)))
            throw new InvalidDataException("공매도 담보 데이터 손상");
        var engine = new GameEngine(s);
        if (engine.Participants.Any(t => t.ReservedCash > t.Cash || t.ReservedShares.Where((q, i) => q > t.Shares[i]).Any() ||
            t.ReservedCovers.Where((q, i) => q > t.ShortShares[i]).Any()) || s.Bank.ReservedLending.Where((q, i) => q > s.Bank.ShareInventory[i]).Any())
            throw new InvalidDataException("주문 담보 부족");
        for (int i = 0; i < StockCount; i++)
        {
            if (engine.Participants.Sum(t => (long)t.Shares[i]) + s.Bank.ShareInventory[i] != s.Stocks[i].TotalShares) throw new InvalidDataException("주식 수 불일치");
            if (engine.bids[i].Count > 0 && engine.asks[i].Count > 0 && engine.bids[i][0].Price >= engine.asks[i][0].Price) throw new InvalidDataException("교차 호가 손상");
        }
        if (engine.Participants.Sum(t => t.Cash) + s.FeePool + s.Bank.Cash + s.Government.Cash != s.InitialSystemCash) throw new InvalidDataException("현금 장부 불일치");
        foreach (var r in s.PendingSeasons)
            if (r.Season <= s.LastArchivedSeason || r.Season >= s.Season || r.Standings.Count != AiCount || r.Standings.Select(x => x.TraderId).Distinct().Count() != AiCount)
                throw new InvalidDataException("시즌 기록 손상");
        if (s.DailyHistory.Count is < 1 or > 121 || s.OpeningSnapshot is null || s.SeasonSnapshot is null ||
            s.Stocks.Any(stock => stock.Reports.Count is < 1 or > 12) || s.Operations.Count > 80 || s.Operations.Any(o => o.LeaderId is < 1 or > AiCount || o.PartnerId is < 1 or > AiCount ||
            o.LeaderId == o.PartnerId || o.StockIndex is < 0 or >= StockCount || !Enum.IsDefined(o.Status))) throw new InvalidDataException("기록 데이터 손상");
        return engine;
    }
    static void UpgradeEconomy(GameState s, int sourceVersion)
    {
        var engine = new GameEngine(s);
        string[] symbols = ["CLUD", "CARE"], names = ["클라우드 네트웍스", "케어 메디컬"], sectors = ["기술", "바이오·헬스"];
        int[] prices = [14600, 28100];
        for (int i = 0; i < 2; i++) s.Stocks.Add(new Stock { Symbol = symbols[i], Name = names[i], Sector = sectors[i], Price = prices[i],
            PreviousPrice = prices[i], DayOpenPrice = prices[i], StartPrice = prices[i], FairValue = prices[i], History = [prices[i], prices[i]] });
        foreach (var t in engine.Participants)
        {
            var shares = t.Shares; Array.Resize(ref shares, StockCount); t.Shares = shares;
            var costs = t.AverageCost; Array.Resize(ref costs, StockCount); t.AverageCost = costs;
            t.ReservedShares = new int[StockCount]; t.ShortShares = new int[StockCount]; t.ShortAveragePrice = new double[StockCount]; t.ReservedCovers = new int[StockCount];
            if (!t.IsRetail) engine.InitializeRepresentative(t);
        }
        s.Bank = new BankState(); s.Government = new GovernmentState(); s.Government.Policy.Season = s.Season;
        engine.InitializeEconomy(); engine.InitializeTotals(); engine.InitializeHistory();
        foreach (var t in engine.Participants)
        {
            t.OpeningSnapshot.Equity = t.OpeningEquity; t.OpeningSnapshot.Cash = t.OpeningCash; t.OpeningSnapshot.NetIncome = 0;
            t.SeasonSnapshot = engine.CaptureTrader(t); t.SeasonSnapshot.Equity = t.SeasonOpeningEquity;
            t.SeasonSnapshot.NetIncome = t.SeasonOpeningEquity - t.OpeningEquity;
        }
        if (sourceVersion == 2 && s.CompletedHours == 720) engine.FinishSeason(1);
        s.Version = 4; s.MigratedFromV3 = sourceVersion == 3;
        if (s.News.Count == 0) engine.PublishNews();
    }
    static void ValidateTrader(Trader t, int count, bool modern)
    {
        if (t.Cash < 0 || t.Shares.Length != count || t.AverageCost.Length != count || t.ReservedShares.Length != count || t.Shares.Any(q => q < 0) ||
            t.AverageCost.Any(c => c < 0 || !double.IsFinite(c)) || !double.IsFinite(t.RealizedProfit) || !double.IsFinite(t.OpeningUnrealized) ||
            !double.IsFinite(t.Risk) || !double.IsFinite(t.Patience) || !Enum.IsDefined(t.Strategy) || t.Fees < 0)
            throw new InvalidDataException("포트폴리오 데이터 손상");
        if (modern && (t.ShortShares.Length != count || t.ShortAveragePrice.Length != count || t.ReservedCovers.Length != count || t.ShortShares.Any(q => q < 0) ||
            t.ShortAveragePrice.Any(p => p < 0 || !double.IsFinite(p)) || t.LoanDebt < 0 || t.FineDebt < 0 || t.Taxes < 0 || t.Fines < 0 || t.Subsidies < 0 ||
            t.TradedVolume < 0 || t.TradedTurnover < 0 || t.CreditScore is < 0 or > 99 || !Enum.IsDefined(t.Disposition) ||
            !double.IsFinite(t.Integrity) || t.Integrity is < 0 or > 1 || (!t.IsRetail && t.Abilities.Values().Any(a => a is < 1 or > 100)) ||
            (t.IsRetail && t.ShortShares.Any(q => q != 0)))) throw new InvalidDataException("신용·능력 데이터 손상");
    }
}

// Completed seasons live in separate files, independent of the rolling chart/tape
// buffers. Reading an old season never loads the whole history into memory.
public sealed class GameStore(string directory)
{
    public string SavePath => Path.Combine(directory, "market-v4.json");
    public string SeasonPath(GameState state, long season) => Path.Combine(directory, "seasons", state.RunId, $"season-{season:D8}.json");
    public GameEngine? Load(out string message)
    {
        Directory.CreateDirectory(directory); message = "";
        foreach (string path in new[] { SavePath, SavePath + ".bak", Path.Combine(directory, "market-v3.json"), Path.Combine(directory, "market-v3.json.bak"), Path.Combine(directory, "season-v2.json") })
        {
            if (!File.Exists(path)) continue;
            try
            {
                var result = GameEngine.Deserialize(File.ReadAllText(path));
                if (path.EndsWith(".bak")) message = "이전 자동 저장본에서 복구했습니다.";
                else if (result.State.MigratedFromV3 && path.EndsWith("market-v3.json")) message = "v1.2 경제 시스템으로 이전했습니다. 기존 저장 원본은 보존됩니다.";
                else if (result.State.MigratedFromV1 && path.EndsWith("season-v2.json")) message = "기관 자산을 이전했습니다. 기존 저장 원본은 보존됩니다.";
                return result;
            }
            catch (Exception e) when (e is IOException or InvalidDataException or JsonException or InvalidOperationException or ArgumentException or NullReferenceException or OverflowException)
            { message = "저장을 읽지 못했습니다. 원본은 보존되어 있습니다."; }
        }
        return null;
    }
    public SaveSnapshot PrepareSave(GameEngine engine)
    {
        var state = engine.State;
        var seasons = state.PendingSeasons.OrderBy(r => r.Season)
            .Select(r => new ArchivedSnapshot(r.Season, SeasonPath(state, r.Season), JsonSerializer.Serialize(r))).ToArray();
        // Capture on the simulation thread; all subsequent file I/O uses only
        // these immutable strings, never the live mutable market.
        return new SaveSnapshot(state.RunId, engine.Serialize(), seasons);
    }
    public void WriteSnapshot(SaveSnapshot snapshot)
    {
        Directory.CreateDirectory(directory);
        foreach (var season in snapshot.Seasons)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(season.Path)!);
            AtomicWrite(season.Path, season.Json);
        }
        if (File.Exists(SavePath))
        {
            try { GameEngine.Deserialize(File.ReadAllText(SavePath)); File.Copy(SavePath, SavePath + ".bak", true); }
            catch (Exception e) when (e is IOException or InvalidDataException or JsonException or InvalidOperationException or ArgumentException or NullReferenceException or OverflowException) { }
        }
        // The checkpoint retains pending records until a later checkpoint. This
        // makes a crash between archive write and acknowledgement recoverable.
        AtomicWrite(SavePath, snapshot.Json);
    }
    public static void Acknowledge(GameEngine engine, SaveSnapshot snapshot)
    {
        if (engine.State.RunId != snapshot.RunId || snapshot.Seasons.Length == 0) return;
        long last = snapshot.Seasons.Max(s => s.Season);
        engine.State.LastArchivedSeason = Math.Max(engine.State.LastArchivedSeason, last);
        engine.State.PendingSeasons.RemoveAll(s => s.Season <= last);
    }
    public void Save(GameEngine engine)
    {
        var snapshot = PrepareSave(engine); WriteSnapshot(snapshot); Acknowledge(engine, snapshot);
    }
    public SeasonResult? ReadSeason(GameState state, long season)
    {
        if (season < 1 || season >= state.Season) return null;
        var pending = state.PendingSeasons.FirstOrDefault(r => r.Season == season); if (pending is not null) return pending;
        string path = SeasonPath(state, season);
        return File.Exists(path) ? JsonSerializer.Deserialize<SeasonResult>(File.ReadAllText(path)) : null;
    }
    static void AtomicWrite(string path, string content)
    {
        using (var file = new FileStream(path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
        { using var writer = new StreamWriter(file, leaveOpen: true); writer.Write(content); writer.Flush(); file.Flush(true); }
        File.Move(path + ".tmp", path, true);
    }
}

public sealed record ArchivedSnapshot(long Season, string Path, string Json);
public sealed record SaveSnapshot(string RunId, string Json, ArchivedSnapshot[] Seasons);
