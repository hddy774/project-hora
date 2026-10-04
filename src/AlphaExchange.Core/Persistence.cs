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
        bool legacy = s.Version == 2;
        if (s.Version is not (2 or 3) || s.RandomState == 0 || s.CompletedHours < 0 || !double.IsFinite(s.HourProgress) || s.HourProgress is < 0 or >= 1 ||
            s.Stocks.Count != 8 || s.Bots.Count != AiCount || s.FollowedId is < 1 or > AiCount || !Guid.TryParseExact(s.RunId, "N", out _))
            throw new InvalidDataException("지원하지 않는 저장 데이터");
        s.Bots.Sort((a, b) => a.Id.CompareTo(b.Id));
        if (s.Bots.Where((t, i) => t.Id != i + 1 || t.IsRetail).Any() ||
            s.Stocks.Any(x => x.Price is < 100 or > 10_000_000 || x.PreviousPrice <= 0 || x.DayOpenPrice <= 0 || x.StartPrice <= 0 ||
                !double.IsFinite(x.FairValue) || x.FairValue <= 0 || x.History.Count < 1)) throw new InvalidDataException("시장 데이터 손상");
        foreach (var t in s.Bots.Concat(s.Retail)) ValidateTrader(t);
        if (legacy)
        {
            if (s.CompletedHours > 720) throw new InvalidDataException("이전 시즌 시간 손상");
            s.Orders.Clear(); s.Retail.Clear(); s.Tape.Clear(); s.PendingSeasons.Clear();
            var migrated = new GameEngine(s); s.Version = 3; s.MigratedFromV1 = true;
            foreach (var t in s.Bots)
            {
                t.LegacyFees = t.Fees; t.LegacyRealizedProfit = t.RealizedProfit; t.Fees = 0; t.RealizedProfit = 0;
                t.OpeningCash = t.Cash; t.OpeningEquity = t.Equity(s.Stocks); t.SeasonOpeningEquity = InitialCash;
                t.OpeningUnrealized = t.Financials(s.Stocks).UnrealizedProfit; t.BuyCashFlow = t.SellCashFlow = 0;
                if (t.EquityHistory.Count > HistoryLimit) t.EquityHistory = t.EquityHistory.TakeLast(HistoryLimit).ToList();
            }
            string[] sectors = ["기술", "산업·에너지", "바이오·헬스", "산업·에너지", "금융", "미디어", "소비재", "기술"];
            for (int i = 0; i < 8; i++) { s.Stocks[i].Sector = sectors[i]; s.Stocks[i].History = s.Stocks[i].History.TakeLast(HistoryLimit).ToList(); }
            foreach (var news in s.News) news.Season = 1;
            s.News = s.News.Take(40).ToList(); migrated.CreateRetail(); migrated.InitializeTotals();
            if (s.CompletedHours == 720) migrated.FinishSeason(1);
            if (s.News.Count == 0) migrated.PublishNews();
            return migrated;
        }
        if (s.Retail.Count != RetailCount || s.Retail.Where((t, i) => t.Id != AiCount + i + 1 || !t.IsRetail).Any() ||
            s.RetailCursor is < 0 or >= RetailCount || s.FeePool < 0 || s.NextOrderId < 1 || s.LastArchivedSeason < 0 || s.LastArchivedSeason >= s.Season)
            throw new InvalidDataException("참가자 데이터 손상");
        if (s.Orders.Select(o => o.Id).Distinct().Count() != s.Orders.Count || s.Orders.Any(o => o.Id < 1 || o.Id >= s.NextOrderId ||
            o.OwnerId is < 1 or > AiCount + RetailCount || o.StockIndex is < 0 or >= 8 || o.Price is < 100 or > 10_000_000 || o.Remaining is <= 0 or > 1_000_000 || o.ExpiresAt < s.CompletedHours))
            throw new InvalidDataException("호가 데이터 손상");
        var engine = new GameEngine(s);
        if (engine.Participants.Any(t => t.ReservedCash > t.Cash || t.ReservedShares.Where((q, i) => q > t.Shares[i]).Any()))
            throw new InvalidDataException("주문 담보 부족");
        for (int i = 0; i < 8; i++)
        {
            if (engine.Participants.Sum(t => (long)t.Shares[i]) != s.Stocks[i].TotalShares) throw new InvalidDataException("주식 수 불일치");
            if (engine.bids[i].Count > 0 && engine.asks[i].Count > 0 && engine.bids[i][0].Price >= engine.asks[i][0].Price) throw new InvalidDataException("교차 호가 손상");
        }
        if (engine.Participants.Sum(t => t.Cash) + s.FeePool != s.InitialSystemCash) throw new InvalidDataException("현금 장부 불일치");
        foreach (var r in s.PendingSeasons)
            if (r.Season <= s.LastArchivedSeason || r.Season >= s.Season || r.Standings.Count != AiCount || r.Standings.Select(x => x.TraderId).Distinct().Count() != AiCount)
                throw new InvalidDataException("시즌 기록 손상");
        return engine;
    }
    static void ValidateTrader(Trader t)
    {
        if (t.Cash < 0 || t.Shares.Length != 8 || t.AverageCost.Length != 8 || t.ReservedShares.Length != 8 || t.Shares.Any(q => q < 0) ||
            t.AverageCost.Any(c => c < 0 || !double.IsFinite(c)) || !double.IsFinite(t.RealizedProfit) || !double.IsFinite(t.OpeningUnrealized) ||
            !double.IsFinite(t.Risk) || !double.IsFinite(t.Patience) || !Enum.IsDefined(t.Strategy) || t.Fees < 0)
            throw new InvalidDataException("포트폴리오 데이터 손상");
    }
}

// Completed seasons live in separate files, independent of the rolling chart/tape
// buffers. Reading an old season never loads the whole history into memory.
public sealed class GameStore(string directory)
{
    public string SavePath => Path.Combine(directory, "market-v3.json");
    public string SeasonPath(GameState state, long season) => Path.Combine(directory, "seasons", state.RunId, $"season-{season:D8}.json");
    public GameEngine? Load(out string message)
    {
        Directory.CreateDirectory(directory); message = "";
        foreach (string path in new[] { SavePath, SavePath + ".bak", Path.Combine(directory, "season-v2.json") })
        {
            if (!File.Exists(path)) continue;
            try
            {
                var result = GameEngine.Deserialize(File.ReadAllText(path));
                if (path.EndsWith(".bak")) message = "이전 자동 저장본에서 복구했습니다.";
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
