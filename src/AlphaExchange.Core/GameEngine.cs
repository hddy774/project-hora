namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    public const int AiCount = 100, RetailCount = 10_000, SeasonLength = 30, StockCount = 30;
    public const long InitialCash = 10_000_000, RetailInitialCapital = InitialCash / 100;
    public const double FeeRate = .0015, SecondsPerGameHour = 5;
    public const int HistoryLimit = 120, TapeLimit = 160;
    public GameState State { get; private set; }
    public static readonly string[] StrategyNames = ["모멘텀", "가치 투자", "역추세", "뉴스 분석", "분산 투자", "탐험가"];
    public static readonly string[] StrategyDescriptions = ["가격의 흐름을 따라 호가를 조절합니다.", "추정 가치와 가격의 차이로 주문합니다.", "과열을 매도하고 하락에 매수 호가를 냅니다.", "공개된 뉴스에 따라 호가를 수정합니다.", "현금과 보유 종목의 비중을 관리합니다.", "여러 종목의 가격 차이와 기회를 탐색합니다."];
    public static readonly string[] AbilityNames = ["가치 분석", "기술 분석", "뉴스 해석", "위험 관리", "분산 설계", "주문 집행", "단타", "초단타", "거시 판단", "협상"];
    public static readonly string[] DispositionNames = ["신중형", "분석형", "기회형", "공격형", "교류형"];
    public static readonly string[] MetricNames = ["수익률", "순수익", "순자산", "현금", "거래량", "거래금액"];
    public static readonly string[] PeriodNames = ["현재 시즌", "전체", "10일 전 대비", "5일 전 대비", "전일 대비", "30일", "90일", "1년", "기간 지정"];
    public static readonly int[] Speeds = [1, 2, 5, 20, 50, 100];
    static readonly string[] Names = ["노바", "볼트", "루멘", "아틀라스", "픽셀", "오닉스", "테라", "제니스", "코멧", "에코", "벡터", "오리온", "네온", "루나", "제로", "시그마", "펄스", "퀀트", "코어", "아스트로"];
    public GameEngine(uint seed)
    {
        seed = seed == 0 ? 20261004 : seed;
        State = new GameState { Seed = seed, RandomState = seed };
        State.Stocks.AddRange(CompanyCatalog.Companies.Select(c => CompanyCatalog.Create(c)));
        State.SecurityIds = State.Stocks.Select(s => s.SecurityId).ToList();
        for (int i = 1; i <= AiCount; i++)
        {
            var t = new Trader { Id = i, Name = $"{Names[(i - 1) % Names.Length]} {i:000}", Strategy = (Strategy)((i - 1) % 6), Risk = .25 + Next() * .7, Patience = .1 + Next() * .7 };
            InitializeRepresentative(t);
            Endow(t, InitialCash); State.Bots.Add(t);
        }
        CreateRetail(); InitializeEconomy(); InitializeTotals(); InitializeHistory(); PublishNews(); RebuildBooks(); OpenSeasonVotes();
    }
    GameEngine(GameState state) { State = state; RebuildBooks(); }
    void Endow(Trader t, long capital)
    {
        t.Shares = new long[State.Stocks.Count]; t.AverageCost = new double[State.Stocks.Count]; t.ReservedShares = new long[State.Stocks.Count];
        t.ShortShares = new long[State.Stocks.Count]; t.ShortAveragePrice = new double[State.Stocks.Count]; t.ReservedCovers = new long[State.Stocks.Count];
        t.Cash = capital;
        t.LastReturnEquity = capital;
        t.OpeningCash = t.Cash; t.OpeningEquity = t.SeasonOpeningEquity = capital;
        if (!t.IsRetail) t.EquityHistory = [capital];
    }
    void CreateRetail()
    {
        for (int i = 0; i < RetailCount; i++)
        {
            var t = new Trader { Id = AiCount + i + 1, IsRetail = true, Risk = .15 + Next() * .8, Patience = .2 + Next() * .6 };
            Endow(t, RetailInitialCapital); State.Retail.Add(t);
        }
    }
    void InitializeTotals()
    {
        State.InitialSystemCash = SystemCash();
        State.IndexDivisor = State.Stocks.Sum(s => s.MarketCap) / 1000.0;
        for (int i = 0; i < State.Stocks.Count; i++) State.Stocks[i].TotalShares = Participants.Sum(t => t.Shares[i]) + State.Bank.ShareInventory[i] + State.Stocks[i].FounderShares + State.Stocks[i].TreasuryShares;
    }
    public IEnumerable<Trader> Participants => State.Bots.Concat(State.Retail);
    public Trader Owner(int id) => id==0 ? BankAccount() : id is >= 1 and <= AiCount ? State.Bots[id - 1] : State.Retail[id - AiCount - 1];
    public Trader FocusTrader => State.Bots[State.FollowedId - 1];
    public List<Trader> Ranking(RankingMetric metric = RankingMetric.Return, ComparisonPeriod period = ComparisonPeriod.Season)
    { var range = period is ComparisonPeriod.All or ComparisonPeriod.Season ? null : PeriodSummary(period); return State.Bots.OrderByDescending(t => RankingValue(t, metric, period, range?.Start, period == ComparisonPeriod.Custom ? range?.End : null)).ThenBy(t => t.Id).ToList(); }
    public int RankOf(int id) => Ranking().FindIndex(t => t.Id == id) + 1;
    double Next()
    {
        uint x = State.RandomState; x ^= x << 13; x ^= x >> 17; x ^= x << 5; State.RandomState = x;
        return x / 4294967296.0;
    }
    public void AdvanceHour()
    {
        ExpireOrders();
        foreach (var s in State.Stocks)
        {
            s.PreviousPrice = s.Price; s.Volume = 0;
            if (State.Hour == 0) { s.DayOpenPrice = s.Price; s.DayVolume = s.DayTurnover = 0; }
        }
        PrepareMarketSignals();
        MakeBankMarket();
        int count = 300 + (int)(Next() * 501); State.ActiveRetailLastHour = count;
        var actors = new int[AiCount + count];
        for (int i = 0; i < AiCount; i++) actors[i] = i + 1;
        for (int i = 0; i < count; i++)
        { State.RetailCursor = (State.RetailCursor + 7919) % RetailCount; actors[AiCount + i] = AiCount + State.RetailCursor + 1; }
        for (int i = actors.Length - 1; i > 0; i--)
        { int j = (int)(Next() * (i + 1)); (actors[i], actors[j]) = (actors[j], actors[i]); }
        foreach (int id in actors) { var t = Owner(id); if (t.IsRetail) DecideRetail(t); else DecideInstitution(t); }
        State.Orders.RemoveAll(o => o.Remaining == 0);
        State.CompletedHours++;
        if (State.Hour == 0)
            foreach (var s in State.Stocks) { s.DayOpenPrice = s.Price; s.DayVolume = s.DayTurnover = 0; }
        foreach (var s in State.Stocks)
        {
            s.Sentiment *= .94; Append(s.History, (double)s.MarkPrice, HistoryLimit);
        }
        foreach (var t in State.Bots) Append(t.EquityHistory, t.Equity(State.Stocks), HistoryLimit);
        foreach (var stock in State.Stocks.Where(s => s.Active)) { stock.ShareHourSum = checked(stock.ShareHourSum + stock.OutstandingShares); stock.ShareHours++; }
        UpdateOperations();
        if (State.Hour == 0) ServiceFinance();
        bool monthEnd = State.CompletedHours % (SeasonLength * 24) == 0;
        if (monthEnd)
        {
            CancelAllOrders(); closingBooks = true;
            try { CloseBusinesses(); } finally { closingBooks = false; }
        }
        UpdateGovernance();
        ProcessBankruptcies();
        UpdatePerformance();
        if (State.Hour == 0) Append(State.DailyHistory, CaptureSnapshot(), 121);
        if (monthEnd) { FinishSeason(State.Season - 1); RefreshEconomy(); }
        RecordHour();
        if (State.Hour % 6 == 0) PublishNews();
    }
    public int AdvanceTime(double seconds, int speed = 1)
    {
        if (!double.IsFinite(seconds) || seconds < 0 || seconds > 3600 || !Speeds.Contains(speed)) throw new ArgumentOutOfRangeException(nameof(seconds));
        double hours = State.HourProgress + seconds * speed / SecondsPerGameHour;
        int count = (int)Math.Floor(hours + 1e-9);
        State.HourProgress = Math.Clamp(hours - count, 0, .999999999999);
        for (int i = 0; i < count; i++) AdvanceHour();
        return count;
    }
    void DecideRetail(Trader t)
    {
        CancelOrders(t.Id);
        if(t.WaitingForCapital) return;
        if (Next() < t.Patience * .15) return;
        int index=(int)(Next()*State.Stocks.Count);
        // Follow whichever of two randomly observed quotes has attracted volume.
        int other=(int)(Next()*State.Stocks.Count);
        if(State.Stocks[other].DayVolume>State.Stocks[index].DayVolume && Next()<.65) index=other;
        if(t.Shares[index]==0 && Next()<.35)
            for(int i=0;i<t.Shares.Length;i++) if(t.Shares[i]>0) { index=i; break; }
        var stock=State.Stocks[index]; if(!stock.Active) return;
        var reaction=RetailReaction(t,index);
        bool buy=Next()<reaction.BuyProbability;
        int limit=Quote(stock.PreviousPrice*(1+(buy ? reaction.Aggression : -reaction.Aggression)),buy);
        long available=buy ? MaxBuy(t,index,limit) : t.Shares[index]-t.ReservedShares[index];
        int quantity=(int)Math.Min(available,1+(Next()<t.Risk*.25 ? 1 : 0));
        if(quantity>0) SubmitOrder(t.Id,index,buy,limit,quantity,1);
    }
    void FinishSeason(long season)
    {
        var result = new SeasonResult { Season = season, Matches = State.TotalMatches - State.SeasonStartMatches,
            RetailEquity = State.Retail.Sum(t => t.Equity(State.Stocks)), Start = State.SeasonSnapshot, End = CaptureSnapshot(),
            Days = State.DailyHistory.Where(d => d.Hour > (season - 1) * 720 && d.Hour <= season * 720).ToList(),
            Policy = State.Government.Policy, CompanyReports = State.Stocks.Select(s => s.Active ? CopyReport(s.Report) : CopyReport(s.Reports.LastOrDefault() ?? s.Report)).ToList() };
        foreach (var (t, index) in Ranking().Select((t, i) => (t, i)))
        {
            var row = new SeasonStanding(t.Id, index + 1, t.SeasonOpeningEquity, t.Equity(State.Stocks), t.Return(State.Stocks),t.Generation,t.Name);
            result.Standings.Add(row);
            Append(t.SeasonRanks, new RankHistory(season, row.Rank, row.Return, row.Equity,t.Generation), 12);
        }
        State.PendingSeasons.Add(result);
        foreach (var t in Participants)
        { t.SeasonOpeningEquity = t.Equity(State.Stocks); if(t.IsRetail) t.SeasonReturnBasis=t.ReturnIndex; else t.SeasonSnapshot = CaptureTrader(t); }
        State.SeasonSnapshot = result.End;
        State.SeasonStartMatches = State.TotalMatches;
    }
    static void Append<T>(List<T> values, T value, int maximum)
    { values.Add(value); if (values.Count > maximum) values.RemoveRange(0, values.Count - maximum); }
    void PublishNews()
    {
        int i = (int)(Next() * State.Stocks.Count); var s = State.Stocks[i]; bool up = Next() >= .5;
        double impact = (up ? 1 : -1) * (.02 + Next() * .05); s.Sentiment = Math.Clamp(s.Sentiment + impact, -.12, .12);
        if (!s.Active) return;
        string headline = up ? $"{s.Business} 수요·계약 개선" : $"{s.Business} 수요 둔화·비용 상승";
        ApplyNewsImpact(i, impact);
        State.News.Insert(0, new MarketEvent { Season = State.Season, Day = State.Day, Hour = State.Hour, StockIndex = i, SecurityId = s.SecurityId,
            Headline = $"{s.Name}, {headline}", Impact = impact, Detail = "기관 판단과 이번 시즌 기업 매출·비용에 반영됩니다. 월말 결산에서 새 재무제표가 발표됩니다." });
        if (State.News.Count > 40) State.News.RemoveRange(40, State.News.Count - 40);
    }
}
