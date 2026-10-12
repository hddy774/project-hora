namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    public const int AiCount = MarketDefaults.InstitutionCount, RetailCount = 10_000,
        SeasonLength = MarketDefaults.SeasonDays, StockCount = MarketDefaults.StockCount;
    public const long InitialCash = 1_000_000_000, RetailInitialCapital = 50_000_000;
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
    readonly int[] actorBuffer = new int[AiCount + 800];
    public GameEngine(uint seed, SimulationRules? rules = null, MarketRules? marketRules = null)
    {
        seed = seed == 0 ? 20261004 : seed;
        State = new GameState { Seed = seed, RandomState = seed, Rules = rules?.Copy() ?? SimulationRules.Default(), Environment = new MarketEnvironment { Rules = marketRules?.Copy() ?? AlphaExchange.Core.MarketRules.Default() } };
        ConfigureNewCapital();
        State.Stocks.AddRange(CompanyCatalog.Companies.Select(c => CompanyCatalog.Create(c)));
        State.SecurityIds = State.Stocks.Select(s => s.SecurityId).ToList();
        for (int i = 1; i <= AiCount; i++)
        {
            var t = new Trader { Id = i, Name = $"{Names[(i - 1) % Names.Length]} {i:000}", Strategy = (Strategy)((i - 1) % 6), Risk = .25 + Next() * .7, Patience = .1 + Next() * .7 };
            InitializeRepresentative(t);
            Endow(t, MarketRules.InvestorCapital); State.Bots.Add(t);
        }
        CreateRetail();
        InitializeEconomy();
        AllocateOpeningShares();
        InitializeTotals();
        InitializeHistory();
        RebuildBooks();
        PublishNews();
        PrepareMarketSignals();
    }
    GameEngine(GameState state) { State = state; RebuildBooks(); if (state.Minute != 0) PrepareMarketSignals(); }
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
        int count = MarketRules.InitialRetailUnits;
        for (int i = 0; i < count; i++)
        {
            var t = new Trader { Id = AiCount + i + 1, IsRetail = true, Risk = .15 + Next() * .8, Patience = .2 + Next() * .6 };
            Endow(t, MarketRules.RetailCapital); State.Retail.Add(t);
        }
    }
    void InitializeTotals()
    {
        State.InitialSystemCash = SystemCash();
        State.IndexDivisor = State.Stocks.Sum(s => s.MarketCap) / 1000.0;
        for (int i = 0; i < State.Stocks.Count; i++) State.Stocks[i].TotalShares = Participants.Sum(t => t.Shares[i]) + State.Bank.ShareInventory[i] + State.Stocks[i].FounderShares + State.Stocks[i].TreasuryShares;
    }
    public IEnumerable<Trader> Participants => State.Bots.Concat(State.Retail);
    public Trader Owner(int id) => id == 0 ? BankAccount() : id is >= 1 and <= AiCount ? State.Bots[id - 1] : State.Retail[id - AiCount - 1];
    public Trader FocusTrader => State.Bots[State.FollowedId - 1];
}
