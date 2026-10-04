namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    public const int AiCount = 100, RetailCount = 10_000, SeasonLength = 30, StockCount = 10;
    public const long InitialCash = 10_000_000, RetailInitialCapital = InitialCash / 100;
    public const double FeeRate = .0015, SecondsPerGameHour = 5;
    public const int HistoryLimit = 120, TapeLimit = 160;
    public GameState State { get; private set; }
    public static readonly string[] StrategyNames = ["모멘텀", "가치 투자", "역추세", "뉴스 분석", "분산 투자", "탐험가"];
    public static readonly string[] StrategyDescriptions = ["가격의 흐름을 따라 호가를 조절합니다.", "추정 가치와 가격의 차이로 주문합니다.", "과열을 매도하고 하락에 매수 호가를 냅니다.", "공개된 뉴스에 따라 호가를 수정합니다.", "현금과 보유 종목의 비중을 관리합니다.", "여러 종목의 가격 차이와 기회를 탐색합니다."];
    public static readonly string[] AbilityNames = ["가치 분석", "기술 분석", "뉴스 해석", "위험 관리", "분산 설계", "주문 집행", "단타", "초단타", "거시 판단", "협상"];
    public static readonly string[] DispositionNames = ["신중형", "분석형", "기회형", "공격형", "교류형"];
    public static readonly string[] MetricNames = ["수익률", "순수익", "순자산", "현금", "거래량", "거래금액"];
    public static readonly string[] PeriodNames = ["현재 시즌", "전체", "10일 전 대비", "5일 전 대비", "전일 대비"];
    public static readonly int[] Speeds = [1, 2, 5, 20, 50, 100];
    static readonly string[] Names = ["노바", "볼트", "루멘", "아틀라스", "픽셀", "오닉스", "테라", "제니스", "코멧", "에코", "벡터", "오리온", "네온", "루나", "제로", "시그마", "펄스", "퀀트", "코어", "아스트로"];
    public GameEngine(uint seed)
    {
        seed = seed == 0 ? 20261004 : seed;
        State = new GameState { Seed = seed, RandomState = seed };
        string[] symbols = ["NOVA", "VOLT", "HELX", "ORBT", "MINT", "WAVE", "FOOD", "AURA", "CLUD", "CARE"];
        string[] names = ["노바 테크", "볼트 에너지", "헬릭스 바이오", "오비트 우주", "민트 파이낸스", "웨이브 미디어", "그린 푸드", "아우라 로보틱스", "클라우드 네트웍스", "케어 메디컬"];
        string[] sectors = ["기술", "산업·에너지", "바이오·헬스", "산업·에너지", "금융", "미디어", "소비재", "기술", "기술", "바이오·헬스"];
        int[] prices = [52400, 31200, 78600, 45300, 22800, 18700, 35600, 64200, 14600, 28100];
        for (int i = 0; i < StockCount; i++)
            State.Stocks.Add(new Stock { Symbol = symbols[i], Name = names[i], Sector = sectors[i], Price = prices[i], PreviousPrice = prices[i],
                DayOpenPrice = prices[i], StartPrice = prices[i], FairValue = prices[i], History = [prices[i], prices[i]] });
        for (int i = 1; i <= AiCount; i++)
        {
            var t = new Trader { Id = i, Name = $"{Names[(i - 1) % Names.Length]} {i:000}", Strategy = (Strategy)((i - 1) % 6), Risk = .25 + Next() * .7, Patience = .1 + Next() * .7 };
            InitializeRepresentative(t);
            Endow(t, InitialCash); State.Bots.Add(t);
        }
        CreateRetail(); InitializeEconomy(); InitializeTotals(); InitializeHistory(); PublishNews(); RebuildBooks();
    }
    GameEngine(GameState state) { State = state; RebuildBooks(); }
    void Endow(Trader t, long capital)
    {
        t.Shares = new int[State.Stocks.Count]; t.AverageCost = new double[State.Stocks.Count]; t.ReservedShares = new int[State.Stocks.Count];
        t.ShortShares = new int[State.Stocks.Count]; t.ShortAveragePrice = new double[State.Stocks.Count]; t.ReservedCovers = new int[State.Stocks.Count];
        t.Cash = capital;
        if (!t.IsRetail)
        {
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                int q = (int)(capital * (.025 + Next() * .045) / State.Stocks[i].Price);
                t.Shares[i] = q; t.AverageCost[i] = State.Stocks[i].Price; t.Cash -= (long)q * State.Stocks[i].Price;
            }
        }
        else
        {
            long budget = (long)(capital * (.40 + Next() * .35));
            for (int j = 0; j < 8; j++)
            {
                int i = (int)(Next() * State.Stocks.Count), price = State.Stocks[i].Price;
                if (budget < price) continue;
                t.Shares[i]++; t.AverageCost[i] = price; t.Cash -= price; budget -= price;
            }
        }
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
        State.InitialSystemCash = Participants.Sum(t => t.Cash) + State.FeePool + State.Bank.Cash + State.Government.Cash;
        for (int i = 0; i < State.Stocks.Count; i++) State.Stocks[i].TotalShares = Participants.Sum(t => (long)t.Shares[i]) + State.Bank.ShareInventory[i];
    }
    public IEnumerable<Trader> Participants => State.Bots.Concat(State.Retail);
    public Trader Owner(int id) => id is >= 1 and <= AiCount ? State.Bots[id - 1] : State.Retail[id - AiCount - 1];
    public Trader FocusTrader => State.Bots[State.FollowedId - 1];
    public List<Trader> Ranking(RankingMetric metric = RankingMetric.Return, ComparisonPeriod period = ComparisonPeriod.Season)
    { var basis = period is ComparisonPeriod.All or ComparisonPeriod.Season ? null : Period(period).Start; return State.Bots.OrderByDescending(t => RankingValue(t, metric, period, basis)).ThenBy(t => t.Id).ToList(); }
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
            s.Sentiment *= .94; Append(s.History, s.Price, HistoryLimit);
        }
        foreach (var t in State.Bots) Append(t.EquityHistory, t.Equity(State.Stocks), HistoryLimit);
        UpdateOperations();
        if (State.Hour == 0) { ServiceFinance(); Append(State.DailyHistory, CaptureSnapshot(), 121); }
        if (State.CompletedHours % (SeasonLength * 24) == 0) { FinishSeason(State.Season - 1); RefreshEconomy(); }
        if (State.Hour % 6 == 0) PublishNews();
        cachedStats = null;
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
        if (Next() < t.Patience * .15) return;
        long equity = Math.Max(1, t.Equity(State.Stocks));
        double exposure = 1 - (double)t.Cash / equity;
        int attempts = 1;
        for (int j = 0; j < attempts; j++)
        {
            int index = (int)(Next() * State.Stocks.Count); var s = State.Stocks[index];
            double trend = (double)s.PreviousPrice / s.History[Math.Max(0, s.History.Count - 6)] - 1;
            double value = s.FairValue / s.PreviousPrice - 1;
            double score = trend * .30 + s.Sentiment * .25;
            score += (.48 + t.Risk * .15 - exposure) * .04;
            bool buy = Next() < Math.Clamp(.5 + score * 5, .15, .85);
            double offset = Math.Clamp(score * .18 + (Next() - .5) * (.014 + t.Risk * .01), -.025, .025);
            int limit = Math.Clamp((int)Math.Round(s.PreviousPrice * (1 + offset) / 10) * 10, 100, 10_000_000);
            int available = buy ? MaxBuy(t, index, limit) : t.Shares[index] - t.ReservedShares[index];
            int desired = t.IsRetail ? 1 : Math.Max(1, (int)(equity * (.004 + Next() * .013) / limit));
            int quantity = Math.Min(available, desired);
            if (quantity > 0) SubmitOrder(t.Id, index, buy, limit, quantity);
        }
    }
    void FinishSeason(long season)
    {
        var result = new SeasonResult { Season = season, Matches = State.TotalMatches - State.SeasonStartMatches,
            RetailEquity = State.Retail.Sum(t => t.Equity(State.Stocks)), Start = State.SeasonSnapshot, End = CaptureSnapshot(),
            Days = State.DailyHistory.Where(d => d.Hour > (season - 1) * 720 && d.Hour <= season * 720).ToList(),
            Policy = State.Government.Policy, CompanyReports = State.Stocks.Select(s => s.Report).ToList() };
        foreach (var (t, index) in Ranking().Select((t, i) => (t, i)))
        {
            var row = new SeasonStanding(t.Id, index + 1, t.SeasonOpeningEquity, t.Equity(State.Stocks), t.Return(State.Stocks));
            result.Standings.Add(row);
            Append(t.SeasonRanks, new RankHistory(season, row.Rank, row.Return, row.Equity), 12);
        }
        State.PendingSeasons.Add(result);
        foreach (var t in Participants) { t.SeasonOpeningEquity = t.Equity(State.Stocks); t.SeasonSnapshot = CaptureTrader(t); }
        State.SeasonSnapshot = result.End;
        State.SeasonStartMatches = State.TotalMatches;
    }
    static void Append<T>(List<T> values, T value, int maximum)
    { values.Add(value); if (values.Count > maximum) values.RemoveRange(0, values.Count - maximum); }
    void PublishNews()
    {
        int i = (int)(Next() * State.Stocks.Count); var s = State.Stocks[i]; bool up = Next() >= .5;
        double impact = (up ? 1 : -1) * (.02 + Next() * .05); s.Sentiment = Math.Clamp(s.Sentiment + impact, -.12, .12);
        string[] good = ["첨단 칩 신규 공급 계약", "친환경 전력 수주 확대", "신약 임상 결과 호조", "위성 발사 시험 성공", "결제 서비스 이용자 증가", "신작 콘텐츠 흥행", "해외 유통 채널 확대", "산업용 로봇 수주 확대", "클라우드 장기 계약 확대", "의료 서비스 신규 공급 계약"];
        string[] bad = ["칩 공급망 비용 상승", "원자재 조달 지연", "신약 심사 일정 지연", "발사 프로젝트 비용 증가", "보안 투자 비용 상승", "광고 시장 성장 둔화", "원재료 가격 상승", "로봇 부품 공급 차질", "데이터센터 운영 비용 상승", "의료 장비 원가 상승"];
        ApplyNewsImpact(i, impact);
        State.News.Insert(0, new MarketEvent { Season = State.Season, Day = State.Day, Hour = State.Hour, StockIndex = i,
            Headline = $"{s.Name}, {(up ? good[i] : bad[i])}", Impact = impact, Detail = "기관 판단과 이번 시즌 기업 매출·비용에 반영됩니다. 월말 결산에서 새 재무제표가 발표됩니다." });
        if (State.News.Count > 40) State.News.RemoveRange(40, State.News.Count - 40);
    }
}
