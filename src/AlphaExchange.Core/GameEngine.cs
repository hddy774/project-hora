using System.Text.Json;
using System.Text.Json.Serialization;

namespace AlphaExchange.Core;

public enum Strategy { Momentum, Value, Contrarian, News, Balanced, Explorer }

public sealed class Stock
{
    public string Symbol { get; set; } = "";
    public string Name { get; set; } = "";
    public string Sector { get; set; } = "";
    public int Price { get; set; }
    public int PreviousPrice { get; set; }
    public int DayOpenPrice { get; set; }
    public int StartPrice { get; set; }
    public double FairValue { get; set; }
    public double Volatility { get; set; }
    public double Sentiment { get; set; }
    public long Volume { get; set; }
    public List<int> History { get; set; } = [];
    [JsonIgnore] public double Change => (double)Price / DayOpenPrice - 1;
    [JsonIgnore] public double HourlyChange => (double)Price / PreviousPrice - 1;
}

public sealed class Trader
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public Strategy Strategy { get; set; }
    public double Risk { get; set; }
    public double Patience { get; set; }
    public long Cash { get; set; } = GameEngine.InitialCash;
    public int[] Shares { get; set; } = new int[8];
    public double[] AverageCost { get; set; } = new double[8];
    public long Fees { get; set; }
    public double RealizedProfit { get; set; }
    public int Trades { get; set; }
    public string LastAction { get; set; } = "시장 분석 중";
    public List<long> EquityHistory { get; set; } = [GameEngine.InitialCash];
    public long Equity(IReadOnlyList<Stock> stocks) => Cash + stocks.Select((s, i) => (long)s.Price * Shares[i]).Sum();
    public double Return(IReadOnlyList<Stock> stocks) => (double)Equity(stocks) / GameEngine.InitialCash - 1;
}

public sealed class MarketEvent
{
    public int Day { get; set; }
    public int Hour { get; set; }
    public int StockIndex { get; set; }
    public string Headline { get; set; } = "";
    public string Detail { get; set; } = "";
    public double Impact { get; set; }
}

public sealed class TradeRecord
{
    public int Day { get; set; }
    public int Hour { get; set; }
    public int TraderId { get; set; }
    public int StockIndex { get; set; }
    public bool Buy { get; set; }
    public int Quantity { get; set; }
    public int Price { get; set; }
}

public sealed class GameState
{
    public int Version { get; set; } = 2;
    public uint RandomState { get; set; }
    public uint Seed { get; set; }
    public int CompletedHours { get; set; }
    public double HourProgress { get; set; }
    [JsonIgnore] public int CompletedDays => CompletedHours / 24;
    [JsonIgnore] public int Hour => CompletedHours % 24;
    public int FollowedId { get; set; } = 1;
    public int TotalAiTrades { get; set; }
    public long[] PendingFlow { get; set; } = new long[8];
    public long[] PendingVolume { get; set; } = new long[8];
    public List<Stock> Stocks { get; set; } = [];
    public List<Trader> Bots { get; set; } = [];
    public List<MarketEvent> News { get; set; } = [];
    public List<TradeRecord> Tape { get; set; } = [];
    [JsonIgnore] public int Day => Math.Min(CompletedDays + 1, GameEngine.SeasonLength);
    [JsonIgnore] public bool Finished => CompletedHours >= GameEngine.SeasonLength * 24;
}

public sealed class GameEngine
{
    public const int AiCount = 100;
    public const int SeasonLength = 30;
    public const long InitialCash = 10_000_000;
    public const double FeeRate = .0015;
    public const double SecondsPerGameHour = 5;
    public GameState State { get; private set; }
    public static readonly string[] StrategyNames = ["모멘텀", "가치 투자", "역추세", "뉴스 분석", "분산 투자", "탐험가"];
    public static readonly string[] StrategyDescriptions = [
        "상승 흐름과 추세를 따라가며 기회를 포착합니다.",
        "추정 기업 가치보다 저렴한 종목을 오래 보유합니다.",
        "급락한 종목을 매수하고 반등하면 차익을 실현합니다.",
        "공개된 뉴스의 방향과 강도를 분석해 거래합니다.",
        "변동성과 현금 비중을 관리하며 분산 투자합니다.",
        "다양한 종목에서 높은 위험의 기회를 탐색합니다."
    ];
    static readonly string[] Names = ["노바", "볼트", "루멘", "아틀라스", "픽셀", "오닉스", "테라", "제니스", "코멧", "에코", "벡터", "오리온", "네온", "루나", "제로", "시그마", "펄스", "퀀트", "코어", "아스트로"];

    public GameEngine(uint seed)
    {
        if (seed == 0) seed = 20261003;
        State = new GameState { Seed = seed, RandomState = seed };
        string[] symbols = ["NOVA", "VOLT", "HELX", "ORBT", "MINT", "WAVE", "FOOD", "AURA"];
        string[] names = ["노바 테크", "볼트 에너지", "헬릭스 바이오", "오비트 우주", "민트 파이낸스", "웨이브 미디어", "그린 푸드", "아우라 로보틱스"];
        string[] sectors = ["반도체 · AI", "신재생 에너지", "바이오 · 헬스", "항공 · 우주", "금융 · 핀테크", "콘텐츠 · 미디어", "식품 · 소비재", "로봇 · 자동화"];
        int[] prices = [52400, 31200, 78600, 45300, 22800, 18700, 35600, 64200];
        for (int i = 0; i < 8; i++)
        {
            var history = new List<int>();
            double p = prices[i] * (.92 + Next() * .16);
            for (int h = 0; h < 15; h++) { p += (prices[i] - p) * .2 + (Next() - .5) * prices[i] * .03; history.Add((int)Math.Round(p)); }
            history.Add(prices[i]);
            State.Stocks.Add(new Stock { Symbol = symbols[i], Name = names[i], Sector = sectors[i], Price = prices[i], PreviousPrice = history[^2], DayOpenPrice = prices[i], StartPrice = prices[i], FairValue = prices[i] * (.9 + Next() * .25), Volatility = i is 2 or 3 or 7 ? .052 : .03, History = history });
        }
        for (int i = 1; i <= AiCount; i++)
            State.Bots.Add(new Trader { Id = i, Name = $"{Names[(i - 1) % Names.Length]} {i:000}", Strategy = (Strategy)((i - 1) % 6), Risk = .25 + Next() * .7, Patience = .1 + Next() * .7 });
        PublishNews();
    }

    private GameEngine(GameState state) { State = state; }

    double Next()
    {
        uint x = State.RandomState;
        x ^= x << 13; x ^= x >> 17; x ^= x << 5;
        State.RandomState = x;
        return x / 4294967296.0;
    }

    public List<Trader> Ranking() => State.Bots.OrderByDescending(t => t.Equity(State.Stocks)).ThenBy(t => t.Id).ToList();
    public int RankOf(int id) => Ranking().FindIndex(t => t.Id == id) + 1;
    public Trader FocusTrader => State.Bots.First(t => t.Id == State.FollowedId);
    public static long Fee(long notional) => (long)Math.Ceiling(notional * FeeRate);
    public int MaxBuy(Trader trader, int stock)
    {
        if (stock < 0 || stock >= State.Stocks.Count) return 0;
        int price = State.Stocks[stock].Price;
        int q = (int)(trader.Cash / (price * (1 + FeeRate)));
        while (q > 0 && (long)q * price + Fee((long)q * price) > trader.Cash) q--;
        return q;
    }

    internal string? Trade(Trader trader, int index, int quantity, bool buy)
    {
        if (State.Finished) return "시즌이 종료되었습니다.";
        if (index < 0 || index >= State.Stocks.Count || quantity <= 0) return "거래 수량을 확인하세요.";
        var stock = State.Stocks[index];
        long cost = (long)stock.Price * quantity;
        long fee = Fee(cost);
        if (buy && cost + fee > trader.Cash) return "매수 가능 금액이 부족합니다.";
        if (!buy && trader.Shares[index] < quantity) return "보유 수량이 부족합니다.";
        if (buy)
        {
            trader.AverageCost[index] = (trader.AverageCost[index] * trader.Shares[index] + cost + fee) / (trader.Shares[index] + quantity);
            trader.Cash -= cost + fee;
            trader.Shares[index] += quantity;
        }
        else
        {
            trader.Cash += cost - fee;
            trader.RealizedProfit += cost - fee - trader.AverageCost[index] * quantity;
            trader.Shares[index] -= quantity;
            if (trader.Shares[index] == 0) trader.AverageCost[index] = 0;
        }
        trader.Trades++;
        trader.Fees += fee;
        trader.LastAction = $"{stock.Symbol} {quantity}주 {(buy ? "매수" : "매도")}";
        State.PendingFlow[index] += buy ? cost : -cost;
        State.PendingVolume[index] += quantity;
        var trade = new TradeRecord { Day = State.Day, Hour = State.Hour, TraderId = trader.Id, StockIndex = index, Quantity = quantity, Buy = buy, Price = stock.Price };
        State.Tape.Insert(0, trade);
        State.TotalAiTrades++;
        if (State.Tape.Count > 1200) State.Tape.RemoveRange(1200, State.Tape.Count - 1200);
        return null;
    }

    public void AdvanceHour()
    {
        if (State.Finished) return;
        // All 100 AIs observe the same public information before the hourly market clears.
        if (State.CompletedHours % 24 == 0) foreach (var stock in State.Stocks) stock.DayOpenPrice = stock.Price;
        foreach (var bot in State.Bots) Decide(bot);
        double market = (Next() + Next() + Next() - 1.5) * .0041;
        foreach (var (s, i) in State.Stocks.Select((s, i) => (s, i)))
        {
            double noise = (Next() + Next() + Next() - 1.5) * s.Volatility / Math.Sqrt(24);
            double fundamentalPull = (s.FairValue / s.Price - 1) * .0015;
            double flow = Math.Tanh(State.PendingFlow[i] / 70_000_000.0) * .0045;
            double change = Math.Clamp(market + noise + s.Sentiment * .025 + fundamentalPull + flow, -.04, .04);
            s.PreviousPrice = s.Price;
            s.Price = Math.Max(100, (int)Math.Round(s.Price * (1 + change)));
            s.FairValue = Math.Max(100, s.FairValue * (1 + (Next() - .48) * .0035));
            s.Sentiment *= .94;
            s.Volume = State.PendingVolume[i];
            s.History.Add(s.Price);
            State.PendingFlow[i] = 0;
            State.PendingVolume[i] = 0;
        }
        State.CompletedHours++;
        foreach (var trader in State.Bots) trader.EquityHistory.Add(trader.Equity(State.Stocks));
        if (!State.Finished && State.Hour % 6 == 0) PublishNews();
    }

    public int AdvanceTime(double realSeconds, int speed = 1)
    {
        if (!double.IsFinite(realSeconds) || realSeconds < 0 || speed is not (1 or 2 or 5))
            throw new ArgumentOutOfRangeException(nameof(realSeconds));
        if (State.Finished) return 0;
        double hours = State.HourProgress + realSeconds * speed / SecondsPerGameHour;
        int count = (int)Math.Min(SeasonLength * 24 - State.CompletedHours, Math.Floor(hours + 1e-9));
        State.HourProgress = Math.Clamp(hours - count, 0, .999999999999);
        for (int i = 0; i < count; i++) AdvanceHour();
        if (State.Finished) State.HourProgress = 0;
        return count;
    }

    void Decide(Trader bot)
    {
        bot.LastAction = "현금 보유 · 관망";
        double[] scores = State.Stocks.Select(s => Score(bot, s)).ToArray();
        long equity = bot.Equity(State.Stocks);
        double exposure = 1 - (double)bot.Cash / equity;
        double target = .26 + bot.Risk * .63;
        int sales = 0;
        foreach (int i in Enumerable.Range(0, 8).OrderBy(i => scores[i]))
        {
            if (bot.Shares[i] <= 0 || sales >= 2) continue;
            bool sell = scores[i] < -.006 - bot.Patience * .012 || exposure > target + .12;
            if (!sell) continue;
            int q = Math.Max(1, (int)(bot.Shares[i] * (.25 + bot.Risk * .5)));
            Trade(bot, i, q, false);
            sales++;
            exposure = 1 - (double)bot.Cash / Math.Max(1, bot.Equity(State.Stocks));
        }
        if (exposure > target || Next() < bot.Patience * .17) return;
        foreach (int i in Enumerable.Range(0, 8).OrderByDescending(i => scores[i]).Take(2))
        {
            if (scores[i] < -.003 || (double)bot.Shares[i] * State.Stocks[i].Price / equity > .30) continue;
            long budget = Math.Min((long)(equity * (.035 + bot.Risk * .065)), (long)(bot.Cash * .40));
            int q = Math.Min(MaxBuy(bot, i), (int)(budget / (State.Stocks[i].Price * (1 + FeeRate))));
            if (q > 0) Trade(bot, i, q, true);
        }
    }

    double Score(Trader bot, Stock s)
    {
        double change = s.HourlyChange;
        double trend = (double)s.Price / s.History[Math.Max(0, s.History.Count - 6)] - 1;
        double value = s.FairValue / s.Price - 1;
        double personality = (Next() - .5) * (.009 + bot.Risk * .014);
        return bot.Strategy switch
        {
            Strategy.Momentum => change * .65 + trend * .35 + s.Sentiment * .25 + personality,
            Strategy.Value => value * .25 + s.Sentiment * .08 + personality,
            Strategy.Contrarian => -change * .8 - trend * .2 + value * .09 + personality,
            Strategy.News => s.Sentiment * .9 + trend * .1 + personality,
            Strategy.Balanced => value * .13 + trend * .17 + s.Sentiment * .22 - s.Volatility * .08 + .004 + personality,
            _ => (Next() - .47) * .085 + s.Sentiment * .3 + personality
        };
    }

    void PublishNews()
    {
        foreach (var s in State.Stocks) s.Sentiment *= .38;
        int first = (int)(Next() * 8);
        int second = (first + 1 + (int)(Next() * 7)) % 8;
        foreach (int i in new[] { first, second })
        {
            bool positive = Next() > .48;
            var s = State.Stocks[i];
            string[] ups = ["차세대 칩 공급 계약 체결", "대규모 친환경 전력 수주", "신약 임상 중간 결과 호조", "위성 발사 시험 성공", "신규 결제 서비스 이용자 증가", "신작 콘텐츠 글로벌 흥행", "해외 유통 채널 확대", "산업용 로봇 신규 수주"];
            string[] downs = ["반도체 공급망 비용 상승", "핵심 원자재 조달 지연", "신약 심사 일정 지연", "발사 프로젝트 비용 증가", "금융 보안 투자 비용 증가", "광고 시장 성장 둔화", "원재료 가격 급등", "로봇 부품 공급 차질"];
            double impact = (positive ? 1 : -1) * (.025 + Next() * .055);
            s.Sentiment = Math.Clamp(s.Sentiment + impact, -.12, .12);
            State.News.Insert(0, new MarketEvent { Day = State.Day, Hour = State.Hour, StockIndex = i, Headline = $"{s.Name}, {(positive ? ups[i] : downs[i])}", Detail = positive ? "긍정적인 기대가 유입됩니다. 실제 주가는 수급과 시장 변동에 따라 달라집니다." : "불확실성이 커지고 있습니다. 실제 주가는 수급과 시장 변동에 따라 달라집니다.", Impact = impact });
        }
        if (State.News.Count > 80) State.News.RemoveRange(80, State.News.Count - 80);
    }

    public string Serialize() => JsonSerializer.Serialize(State);

    public static GameEngine Deserialize(string json)
    {
        var state = JsonSerializer.Deserialize<GameState>(json) ?? throw new InvalidDataException("저장 데이터가 없습니다.");
        if (state.Version != 2 || state.RandomState == 0 || state.CompletedHours is < 0 or > SeasonLength * 24 || !double.IsFinite(state.HourProgress) || state.HourProgress is < 0 or >= 1 || state.Stocks.Count != 8 || state.Bots.Count != AiCount || state.PendingFlow.Length != 8 || state.PendingVolume.Length != 8 || state.FollowedId is < 1 or > AiCount)
            throw new InvalidDataException("지원하지 않는 저장 데이터입니다.");
        if (state.Bots.Select(t => t.Id).Order().Where((id, i) => id != i + 1).Any() || state.Stocks.Any(s => s.DayOpenPrice < 100 || s.Price < 100 || s.PreviousPrice < 100 || s.History.Count < 2 || s.FairValue <= 0 || !double.IsFinite(s.FairValue)))
            throw new InvalidDataException("저장 데이터가 손상되었습니다.");
        foreach (var t in state.Bots)
            if (t.Cash < 0 || t.Shares.Length != 8 || t.AverageCost.Length != 8 || t.Shares.Any(x => x < 0) || t.AverageCost.Any(x => !double.IsFinite(x) || x < 0) || !Enum.IsDefined(t.Strategy))
                throw new InvalidDataException("포트폴리오 데이터가 손상되었습니다.");
        return new GameEngine(state);
    }
}
