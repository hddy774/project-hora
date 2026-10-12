using System.Text.Json.Serialization;

namespace AlphaExchange.Core;

public sealed class GameState : ICashAccount
{
    public int Version { get; set; } = 9;
    public MarketEnvironment Environment { get; set; } = new();
    public long CompletedMinutes { get; set; }
    public double MinuteProgress { get; set; }
    public int PendingClockMinutes { get; set; }
    public List<int> MinuteActors { get; set; } = [];
    public SimulationRules? Rules { get; set; }
    public long NextBankruptcyId { get; set; } = 1;
    public List<BankruptcyRecord> Bankruptcies { get; set; } = [];
    public BankruptcyTotals BankruptcyTotals { get; set; } = new();
    public string RunId { get; set; } = Guid.NewGuid().ToString("N");
    public List<string> SecurityIds { get; set; } = [];
    public RealEconomyState RealEconomy { get; set; } = new();
    public long ExchangeRevenue { get; set; }
    public long ExchangeSpending { get; set; }
    public long MarketDividends { get; set; }
    public long NextCorporateEventId { get; set; } = 1;
    public List<CorporateEvent> CorporateEvents { get; set; } = [];
    public double IndexDivisor { get; set; } = 1;
    public double PriceIndex { get; set; } = 1000;
    public double TotalReturnIndex { get; set; } = 1000;
    public double LastPriceIndex { get; set; } = 1000;
    public double DividendIndexPoints { get; set; }
    public double LastDividendIndexPoints { get; set; }
    public long LastMarketDividends { get; set; }
    public Dictionary<string, long> CashFlows { get; set; } = [];
    public List<LedgerEntry> Journal { get; set; } = [];
    public long NextTransactionId { get; set; } = 1;
    [JsonIgnore] public long Cash { get => FeePool; set => FeePool = value; }
    public uint RandomState { get; set; }
    public uint Seed { get; set; }
    public long CompletedHours { get; set; }
    public double HourProgress { get; set; }
    public int PendingClockHours { get; set; }
    public long ComparisonFrom { get; set; }
    public long ComparisonTo { get; set; }
    public int FollowedId { get; set; } = 1;
    public TradingActivity Activity { get; set; } = new();
    public long TotalAiTrades { get; set; }
    public long TotalMatches { get; set; }
    public long MatchedVolume { get; set; }
    public long MatchedTurnover { get; set; }
    public Dictionary<string, long> SectorVolumes { get; set; } = [];
    public Dictionary<string, long> SectorTurnovers { get; set; } = [];
    public long SeasonStartMatches { get; set; }
    public long FeePool { get; set; }
    public long InitialSystemCash { get; set; }
    public long NextOrderId { get; set; } = 1;
    public int RetailCursor { get; set; }
    public int ActiveRetailLastHour { get; set; }
    public long LastArchivedSeason { get; set; }
    public List<Stock> Stocks { get; set; } = [];
    public List<Trader> Bots { get; set; } = [];
    public List<Trader> Retail { get; set; } = [];
    public List<LimitOrder> Orders { get; set; } = [];
    public List<MarketEvent> News { get; set; } = [];
    public List<TradeRecord> Tape { get; set; } = [];
    public List<SeasonResult> PendingSeasons { get; set; } = [];
    public BankState Bank { get; set; } = new();
    public GovernmentState Government { get; set; } = new();
    public List<DailySnapshot> DailyHistory { get; set; } = [];
    public DailySnapshot? OpeningSnapshot { get; set; }
    public DailySnapshot? SeasonSnapshot { get; set; }
    [JsonIgnore] public long Season => CompletedHours / (MarketDefaults.SeasonDays * 24) + 1;
    [JsonIgnore] public int Day => (int)(CompletedHours % (MarketDefaults.SeasonDays * 24) / 24) + 1;
    [JsonIgnore] public int Hour => (int)(CompletedHours % 24);
    [JsonIgnore] public int Minute => (int)(CompletedMinutes % 60);
    [JsonIgnore] public bool Finished => false;
}
