using System.Text.Json.Serialization;

namespace AlphaExchange.Core;

public enum Strategy { Momentum, Value, Contrarian, News, Balanced, Explorer }
public enum Disposition { Cautious, Analytical, Opportunistic, Aggressive, Sociable }
public enum RankingMetric { Return, NetIncome, Assets, Cash, Volume, Turnover }
public enum ComparisonPeriod { Season, All, TenDays, FiveDays, PreviousDay }
public enum CreditRating { AAA, AA, A, BBB, BB, B, CCC, CC, C }
public sealed class Abilities
{
    public int Valuation { get; set; }
    public int Technical { get; set; }
    public int News { get; set; }
    public int RiskManagement { get; set; }
    public int Diversification { get; set; }
    public int Execution { get; set; }
    public int SwingTrading { get; set; }
    public int Scalping { get; set; }
    public int Macro { get; set; }
    public int Negotiation { get; set; }
    public int[] Values() => [Valuation, Technical, News, RiskManagement, Diversification, Execution, SwingTrading, Scalping, Macro, Negotiation];
}
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
    public long TotalShares { get; set; }
    public long Volume { get; set; }
    public long DayVolume { get; set; }
    public long DayTurnover { get; set; }
    public long TotalVolume { get; set; }
    public long TotalTurnover { get; set; }
    public List<int> History { get; set; } = [];
    public CompanyReport Report { get; set; } = new();
    public List<CompanyReport> Reports { get; set; } = [];
    public double SeasonNewsImpact { get; set; }
    public int SeasonNewsCount { get; set; }
    [JsonIgnore] public double Change => (double)Price / DayOpenPrice - 1;
    [JsonIgnore] public double HourlyChange => (double)Price / PreviousPrice - 1;
}
public sealed class Trader
{
    public int Id { get; set; }
    public bool IsRetail { get; set; }
    public string Name { get; set; } = "";
    public Strategy Strategy { get; set; }
    public double Risk { get; set; }
    public double Patience { get; set; }
    public long Cash { get; set; }
    public int[] Shares { get; set; } = new int[GameEngine.StockCount];
    public double[] AverageCost { get; set; } = new double[GameEngine.StockCount];
    public long ReservedCash { get; set; }
    public int[] ReservedShares { get; set; } = new int[GameEngine.StockCount];
    public int[] ShortShares { get; set; } = new int[GameEngine.StockCount];
    public double[] ShortAveragePrice { get; set; } = new double[GameEngine.StockCount];
    public int[] ReservedCovers { get; set; } = new int[GameEngine.StockCount];
    public Abilities Abilities { get; set; } = new();
    public Disposition Disposition { get; set; }
    public double Integrity { get; set; } = .8;
    public int CreditScore { get; set; } = 70;
    [JsonIgnore] public CreditRating CreditRating => (CreditRating)Math.Clamp((99 - CreditScore) / 10, 0, 8);
    public long LoanDebt { get; set; }
    public long FineDebt { get; set; }
    public long BorrowedCash { get; set; }
    public long RepaidCash { get; set; }
    public long InterestExpense { get; set; }
    public long InterestPaid { get; set; }
    public long BorrowFees { get; set; }
    public long Taxes { get; set; }
    public long Fines { get; set; }
    public long FinesPaid { get; set; }
    public long Subsidies { get; set; }
    public long DebtRelief { get; set; }
    public long TradedVolume { get; set; }
    public long TradedTurnover { get; set; }
    public TraderSnapshot OpeningSnapshot { get; set; } = new();
    public TraderSnapshot SeasonSnapshot { get; set; } = new();
    public string Decision { get; set; } = "분산 포트폴리오 분석";
    public string ActiveMethods { get; set; } = "가치·추세·뉴스·단타·초단타";
    public int MarginCalls { get; set; }
    public long Fees { get; set; }
    public double RealizedProfit { get; set; }
    public long BuyCashFlow { get; set; }
    public long SellCashFlow { get; set; }
    public long OpeningCash { get; set; }
    public long OpeningEquity { get; set; }
    public double OpeningUnrealized { get; set; }
    public long SeasonOpeningEquity { get; set; }
    public long LegacyFees { get; set; }
    public double LegacyRealizedProfit { get; set; }
    public long Trades { get; set; }
    public string LastAction { get; set; } = "호가 탐색 중";
    public List<long> EquityHistory { get; set; } = [];
    public List<RankHistory> SeasonRanks { get; set; } = [];
    [JsonIgnore] public int PortraitId => IsRetail ? 0 : Id;
    [JsonIgnore] public string Gender => Id <= 70 ? "여성" : "남성";
    public long Equity(IReadOnlyList<Stock> stocks)
    { return GrossAssets(stocks) - ShortLiability(stocks) - LoanDebt - FineDebt; }
    public long GrossAssets(IReadOnlyList<Stock> stocks)
    { long value = Cash; for (int i = 0; i < Shares.Length; i++) value += (long)stocks[i].Price * Shares[i]; return value; }
    public long ShortLiability(IReadOnlyList<Stock> stocks)
    { long value = 0; for (int i = 0; i < ShortShares.Length; i++) value += (long)stocks[i].Price * ShortShares[i]; return value; }
    public long ShortCollateral(IReadOnlyList<Stock> stocks) => (long)Math.Ceiling(ShortLiability(stocks) * 1.5);
    public double Return(IReadOnlyList<Stock> stocks) => (double)(Equity(stocks) - SeasonOpeningEquity) / Math.Max(1, SeasonOpeningEquity);
    public FinancialStatement Financials(IReadOnlyList<Stock> stocks)
    {
        double cost = 0; for (int i = 0; i < Shares.Length; i++) cost += Shares[i] * AverageCost[i];
        long holdings = GrossAssets(stocks) - Cash;
        double shortEntry = 0; for (int i = 0; i < ShortShares.Length; i++) shortEntry += ShortShares[i] * ShortAveragePrice[i];
        return new FinancialStatement { Count = 1, Cash = Cash, Holdings = holdings, Cost = cost, OpeningCash = OpeningCash,
            OpeningEquity = OpeningEquity, OpeningUnrealized = OpeningUnrealized, RealizedProfit = RealizedProfit,
            Fees = Fees, Purchases = BuyCashFlow, Sales = SellCashFlow, ReservedCash = ReservedCash, Trades = Trades,
            ShortDebt = ShortLiability(stocks), ShortEntry = shortEntry, LoanDebt = LoanDebt, FineDebt = FineDebt,
            Borrowed = BorrowedCash, Repaid = RepaidCash, InterestExpense = InterestExpense, InterestPaid = InterestPaid,
            BorrowFees = BorrowFees, Taxes = Taxes, Fines = Fines, FinesPaid = FinesPaid, Subsidies = Subsidies, DebtRelief = DebtRelief };
    }
}
public sealed class FinancialStatement
{
    public int Count { get; set; }
    public long Cash { get; set; }
    public long Holdings { get; set; }
    public double Cost { get; set; }
    public long OpeningCash { get; set; }
    public long OpeningEquity { get; set; }
    public double OpeningUnrealized { get; set; }
    public double RealizedProfit { get; set; }
    public long Fees { get; set; }
    public long Purchases { get; set; }
    public long Sales { get; set; }
    public long ReservedCash { get; set; }
    public long Trades { get; set; }
    public long ShortDebt { get; set; }
    public double ShortEntry { get; set; }
    public long LoanDebt { get; set; }
    public long FineDebt { get; set; }
    public long Borrowed { get; set; }
    public long Repaid { get; set; }
    public long InterestExpense { get; set; }
    public long InterestPaid { get; set; }
    public long BorrowFees { get; set; }
    public long Taxes { get; set; }
    public long Fines { get; set; }
    public long FinesPaid { get; set; }
    public long Subsidies { get; set; }
    public long DebtRelief { get; set; }
    public long Assets => Cash + Holdings;
    public long Liabilities => ShortDebt + LoanDebt + FineDebt;
    public long Equity => Assets - Liabilities;
    public double UnrealizedProfit => Holdings - Cost + ShortEntry - ShortDebt;
    public double ValuationChange => UnrealizedProfit - OpeningUnrealized;
    public double NetIncome => RealizedProfit + ValuationChange - Fees - Taxes - InterestExpense - BorrowFees - Fines + Subsidies + DebtRelief;
    public long NetCashFlow => Sales - Purchases - Fees - Taxes - InterestPaid - BorrowFees - FinesPaid + Subsidies + Borrowed - Repaid;
    public void Add(FinancialStatement s)
    {
        Count += s.Count; Cash += s.Cash; Holdings += s.Holdings; Cost += s.Cost; OpeningCash += s.OpeningCash;
        OpeningEquity += s.OpeningEquity; OpeningUnrealized += s.OpeningUnrealized; RealizedProfit += s.RealizedProfit;
        Fees += s.Fees; Purchases += s.Purchases; Sales += s.Sales; ReservedCash += s.ReservedCash; Trades += s.Trades;
        ShortDebt += s.ShortDebt; ShortEntry += s.ShortEntry; LoanDebt += s.LoanDebt; FineDebt += s.FineDebt;
        Borrowed += s.Borrowed; Repaid += s.Repaid; InterestExpense += s.InterestExpense; InterestPaid += s.InterestPaid;
        BorrowFees += s.BorrowFees; Taxes += s.Taxes; Fines += s.Fines; FinesPaid += s.FinesPaid; Subsidies += s.Subsidies; DebtRelief += s.DebtRelief;
    }
}
public sealed class LimitOrder
{
    public long Id { get; set; }
    public int OwnerId { get; set; }
    public int StockIndex { get; set; }
    public bool Buy { get; set; }
    public bool Short { get; set; }
    public bool Cover { get; set; }
    public int CollateralPrice { get; set; }
    public int Price { get; set; }
    public int Remaining { get; set; }
    public long ExpiresAt { get; set; }
}
public sealed record BookLevel(int Price, long InstitutionQuantity, long RetailQuantity)
{ public long Quantity => InstitutionQuantity + RetailQuantity; }
public sealed class TradeRecord
{
    public long Season { get; set; }
    public int Day { get; set; }
    public int Hour { get; set; }
    public int BuyerId { get; set; }
    public int SellerId { get; set; }
    public int StockIndex { get; set; }
    public int Quantity { get; set; }
    public int Price { get; set; }
    public bool ShortSale { get; set; }
    public bool ShortCover { get; set; }
}
public sealed class MarketEvent
{
    public long Season { get; set; }
    public int Day { get; set; }
    public int Hour { get; set; }
    public int StockIndex { get; set; }
    public string Headline { get; set; } = "";
    public string Detail { get; set; } = "";
    public double Impact { get; set; }
}
public sealed record RankHistory(long Season, int Rank, double Return, long Equity);
public sealed record SeasonStanding(int TraderId, int Rank, long OpeningEquity, long Equity, double Return);
public sealed class SeasonResult
{
    public long Season { get; set; }
    public List<SeasonStanding> Standings { get; set; } = [];
    public long Matches { get; set; }
    public long RetailEquity { get; set; }
    public DailySnapshot? Start { get; set; }
    public DailySnapshot? End { get; set; }
    public List<DailySnapshot> Days { get; set; } = [];
    public GovernmentPolicy? Policy { get; set; }
    public List<CompanyReport> CompanyReports { get; set; } = [];
}
public sealed class GameState
{
    public int Version { get; set; } = 4;
    public string RunId { get; set; } = Guid.NewGuid().ToString("N");
    public bool MigratedFromV1 { get; set; }
    public uint RandomState { get; set; }
    public uint Seed { get; set; }
    public long CompletedHours { get; set; }
    public double HourProgress { get; set; }
    public int FollowedId { get; set; } = 1;
    public long TotalAiTrades { get; set; }
    public long TotalMatches { get; set; }
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
    public List<InstitutionOperation> Operations { get; set; } = [];
    public long NextOperationId { get; set; } = 1;
    public bool MigratedFromV3 { get; set; }
    [JsonIgnore] public long Season => CompletedHours / (GameEngine.SeasonLength * 24) + 1;
    [JsonIgnore] public int Day => (int)(CompletedHours % (GameEngine.SeasonLength * 24) / 24) + 1;
    [JsonIgnore] public int Hour => (int)(CompletedHours % 24);
    [JsonIgnore] public bool Finished => false;
}
