using System.Text.Json.Serialization;
using System.Runtime.InteropServices;

namespace AlphaExchange.Core;

public enum Strategy { Momentum, Value, Contrarian, News, Balanced, Explorer }
public enum Disposition { Cautious, Analytical, Opportunistic, Aggressive, Sociable }
public enum RankingMetric { Return, NetIncome, Assets, Cash, Volume, Turnover }
public enum ComparisonPeriod { Season, All, TenDays, FiveDays, PreviousDay, ThirtyDays, NinetyDays, Year, Custom }
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
    public int Get(AbilityKind kind) => kind switch
    { AbilityKind.Valuation=>Valuation, AbilityKind.Technical=>Technical, AbilityKind.News=>News,
      AbilityKind.RiskManagement=>RiskManagement, AbilityKind.Diversification=>Diversification, AbilityKind.Execution=>Execution,
      AbilityKind.SwingTrading=>SwingTrading, AbilityKind.Scalping=>Scalping, AbilityKind.Macro=>Macro, AbilityKind.Negotiation=>Negotiation,
      _=>throw new ArgumentOutOfRangeException(nameof(kind)) };
    public void Set(AbilityKind kind,int value)
    {
        switch(kind)
        {
            case AbilityKind.Valuation: Valuation=value; break; case AbilityKind.Technical: Technical=value; break;
            case AbilityKind.News: News=value; break; case AbilityKind.RiskManagement: RiskManagement=value; break;
            case AbilityKind.Diversification: Diversification=value; break; case AbilityKind.Execution: Execution=value; break;
            case AbilityKind.SwingTrading: SwingTrading=value; break; case AbilityKind.Scalping: Scalping=value; break;
            case AbilityKind.Macro: Macro=value; break; case AbilityKind.Negotiation: Negotiation=value; break;
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }
    public static Abilities Uniform(int value)
    { var result=new Abilities(); for(int i=0;i<10;i++) result.Set((AbilityKind)i,value); return result; }
}
public sealed class Stock
{
    public int Generation { get; set; } = 1;
    public int InsolventMonths { get; set; }
    public bool WaitingForCapital { get; set; }
    public CompanyStrategy CompanyStrategy { get; set; } = CompanyStrategy.Value;
    public ManagementPolicy Management { get; set; } = ManagementPolicy.Efficiency;
    public CapitalAllocation Allocation { get; set; } = CapitalAllocation.Buyback;
    public double TreasuryTarget { get; set; } = .65;
    public string SecurityId { get; set; } = "";
    public bool Active { get; set; } = true;
    public string Business { get; set; } = "";
    public string Driver { get; set; } = "";
    public string NewsExposure { get; set; } = "";
    public string CapitalPolicy { get; set; } = "";
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
    public decimal FractionalMark { get; set; }
    [JsonIgnore] public decimal MarkPrice => Price + FractionalMark;
    public long TreasuryShares { get; set; }
    public long FounderShares { get; set; }
    public long ParValue { get; set; } = 100;
    public double SplitFactor { get; set; } = 1;
    public double CostRatio { get; set; } = .78;
    public double DemandSensitivity { get; set; } = 1;
    public double DividendPayout { get; set; } = .33;
    public bool FundedIpo { get; set; }
    public long ListedSeason { get; set; } = 1;
    public long BaseRevenue { get; set; }
    public long InitialFixedAssets { get; set; }
    public long PendingDividends { get; set; }
    public long PendingDividendPerShare { get; set; }
    public long PendingCapitalChange { get; set; }
    public long PendingFinancingFlow { get; set; }
    public long PendingInvestingFlow { get; set; }
    public long ShareHourSum { get; set; }
    public long ShareHours { get; set; }
    public int LastTradePrice { get; set; }
    public long LastDividendPerShare { get; set; }
    public long TotalDividends { get; set; }
    [JsonIgnore] public long OutstandingShares => TotalShares - TreasuryShares;
    [JsonIgnore] public long FloatShares => Math.Max(0, OutstandingShares - FounderShares);
    [JsonIgnore] public long MarketCap => Active ? Value(OutstandingShares) : 0;
    [JsonIgnore] public long FloatMarketCap => Active ? Value(FloatShares) : 0;
    public long Value(long quantity) => FractionalMark == 0 ? checked((long)Price * quantity)
        : checked((long)decimal.Round(MarkPrice * quantity));
    [JsonIgnore] public double DividendYield => (double)Reports.Where(r => r.AccountingBasis == 5).Sum(r => r.DividendsPerShare * r.SplitFactor / SplitFactor) / Math.Max(1, Price);
    [JsonIgnore] public double TtmEps => Reports.Where(r => r.AccountingBasis == 5 && !r.IsOpening).Sum(r => (double)r.NetIncome / Math.Max(1, r.AverageShares) * r.SplitFactor / SplitFactor);
    [JsonIgnore] public int EpsMonths => Reports.Count(r => r.AccountingBasis == 5 && !r.IsOpening);
    [JsonIgnore] public double AnnualEps => EpsMonths == 0 ? (double)Report.NetIncome * 12 / Math.Max(1, OutstandingShares)
        : EpsMonths < 12 ? TtmEps * 12 / EpsMonths : TtmEps;
    public long TotalShares { get; set; }
    public long Volume { get; set; }
    public long DayVolume { get; set; }
    public long DayTurnover { get; set; }
    public long TotalVolume { get; set; }
    public long TotalTurnover { get; set; }
    public List<double> History { get; set; } = [];
    public CompanyReport Report { get; set; } = new();
    public List<CompanyReport> Reports { get; set; } = [];
    public double SeasonNewsImpact { get; set; }
    public int SeasonNewsCount { get; set; }
    [JsonIgnore] public double Change => (double)Price / DayOpenPrice - 1;
    [JsonIgnore] public double HourlyChange => (double)Price / PreviousPrice - 1;
}
public sealed class Trader : ICashAccount
{
    public int Generation { get; set; } = 1;
    public long BirthHour { get; set; }
    public bool WaitingForCapital { get; set; }
    public double SeasonReturnBasis { get; set; } = 1;
    public int Id { get; set; }
    public bool IsRetail { get; set; }
    public string Name { get; set; } = "";
    public Strategy Strategy { get; set; }
    public double Risk { get; set; }
    public double Patience { get; set; }
    public long Cash { get; set; }
    public long[] Shares { get; set; } = new long[GameEngine.StockCount];
    public double[] AverageCost { get; set; } = new double[GameEngine.StockCount];
    public long ReservedCash { get; set; }
    public long[] ReservedShares { get; set; } = new long[GameEngine.StockCount];
    public long[] ShortShares { get; set; } = new long[GameEngine.StockCount];
    public double[] ShortAveragePrice { get; set; } = new double[GameEngine.StockCount];
    public long[] ReservedCovers { get; set; } = new long[GameEngine.StockCount];
    public Abilities Abilities { get; set; } = new();
    public InstitutionDevelopment? Development { get; set; }
    public long StaffCosts { get; set; }
    public Disposition Disposition { get; set; }
    public double Integrity { get; set; } = .8;
    public int CreditScore { get; set; } = 70;
    [JsonIgnore] public CreditRating CreditRating => (CreditRating)Math.Clamp((99 - CreditScore) / 10, 0, 8);
    public long LoanDebt { get; set; }
    public long FineDebt { get; set; }
    public long TaxDebt { get; set; }
    public long TaxesPaid { get; set; }
    public long BorrowedCash { get; set; }
    public long RepaidCash { get; set; }
    public long InterestExpense { get; set; }
    public long InterestPaid { get; set; }
    public long BorrowFees { get; set; }
    public long Taxes { get; set; }
    public long Fines { get; set; }
    public long FinesPaid { get; set; }
    public long Subsidies { get; set; }
    public long DividendIncome { get; set; }
    public long DividendTax { get; set; }
    public long ShortDividendExpense { get; set; }
    public long ShortDividendDebt { get; set; }
    public long ShortDividendPaid { get; set; }
    public long WageIncome { get; set; }
    public long Consumption { get; set; }
    public double LossCarryForward { get; set; }
    public double SeasonRealizedProfit { get; set; }
    public long SeasonTaxPaid { get; set; }
    public double ReturnIndex { get; set; } = 1;
    public long LastReturnEquity { get; set; }
    public long LastNetContribution { get; set; }
    [JsonIgnore] public long NetContribution => WageIncome - Consumption;
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
    [JsonIgnore] public int PortraitId => IsRetail || Id==0 ? 0 : (Id-1+(Generation-1)*17)%GameEngine.AiCount+1;
    [JsonIgnore] public string Gender => PortraitId <= 70 ? "여성" : "남성";
    public long Equity(IReadOnlyList<Stock> stocks)
    {
        if(stocks is List<Stock> list) return Equity(CollectionsMarshal.AsSpan(list));
        return GrossAssets(stocks)-ShortLiability(stocks)-LoanDebt-FineDebt-ShortDividendDebt-TaxDebt;
    }
    long Equity(ReadOnlySpan<Stock> stocks)
    {
        long gross=Cash,shorts=0;
        if(Shares.AsSpan().ContainsAnyExcept(0L) || ShortShares.AsSpan().ContainsAnyExcept(0L)) for(int i=0;i<Shares.Length;i++)
        {
            if(Shares[i]!=0) gross=checked(gross+stocks[i].Value(Shares[i]));
            if(ShortShares[i]!=0) shorts=checked(shorts+stocks[i].Value(ShortShares[i]));
        }
        return gross-shorts-LoanDebt-FineDebt-ShortDividendDebt-TaxDebt;
    }
    public long GrossAssets(IReadOnlyList<Stock> stocks)
    { long value = Cash; for (int i = 0; i < Shares.Length; i++) if (Shares[i] != 0) value = checked(value + stocks[i].Value(Shares[i])); return value; }
    public long ShortLiability(IReadOnlyList<Stock> stocks)
    { long value = 0; for (int i = 0; i < ShortShares.Length; i++) if (ShortShares[i] != 0) value = checked(value + stocks[i].Value(ShortShares[i])); return value; }
    public long ShortCollateral(IReadOnlyList<Stock> stocks) => (long)Math.Ceiling(ShortLiability(stocks) * 1.5);
    public double Return(IReadOnlyList<Stock> stocks) => ReturnIndex / Math.Max(1e-12, IsRetail ? SeasonReturnBasis : SeasonSnapshot.ReturnIndex) - 1;
    public FinancialStatement Financials(IReadOnlyList<Stock> stocks)
    {
        var result=new FinancialStatement(); AccumulateFinancials(result,stocks); return result;
    }
    public long AccumulateFinancials(FinancialStatement result,IReadOnlyList<Stock> stocks)
    {
        long gross=Cash,shortDebt=0; double cost=0,shortEntry=0;
        if(Shares.AsSpan().ContainsAnyExcept(0L) || ShortShares.AsSpan().ContainsAnyExcept(0L)) for(int i=0;i<Shares.Length;i++)
        {
            long shares=Shares[i],shorts=ShortShares[i];
            if(shares!=0) { gross=checked(gross+stocks[i].Value(shares)); cost+=shares*AverageCost[i]; }
            if(shorts!=0) { shortDebt=checked(shortDebt+stocks[i].Value(shorts)); shortEntry+=shorts*ShortAveragePrice[i]; }
        }
        long holdings=gross-Cash;
        result.Count += 1;
        result.Cash += Cash;
        result.Holdings += holdings;
        result.Cost += cost;
        result.OpeningCash += OpeningCash;
        result.OpeningEquity += OpeningEquity;
        result.OpeningUnrealized += OpeningUnrealized;
        result.RealizedProfit += RealizedProfit;
        result.Fees += Fees;
        result.Purchases += BuyCashFlow;
        result.Sales += SellCashFlow;
        result.ReservedCash += ReservedCash;
        result.Trades += Trades;
        result.ShortDebt += shortDebt;
        result.ShortEntry += shortEntry;
        result.LoanDebt += LoanDebt;
        result.FineDebt += FineDebt;
        result.Borrowed += BorrowedCash;
        result.Repaid += RepaidCash;
        result.InterestExpense += InterestExpense;
        result.InterestPaid += InterestPaid;
        result.BorrowFees += BorrowFees;
        result.Taxes += Taxes;
        result.TaxesPaid+=TaxesPaid; result.TaxDebt+=TaxDebt;
        result.Fines += Fines;
        result.FinesPaid += FinesPaid;
        result.Subsidies += Subsidies;
        result.DebtRelief += DebtRelief;
        result.DividendIncome += DividendIncome;
        result.DividendTax += DividendTax;
        result.ShortDividendExpense += ShortDividendExpense;
        result.ShortDividendPaid += ShortDividendPaid;
        result.ShortDividendDebt += ShortDividendDebt;
        result.WageIncome += WageIncome;
        result.Consumption += Consumption;
        result.StaffCosts += StaffCosts;
        return gross-shortDebt-LoanDebt-FineDebt-ShortDividendDebt-TaxDebt;
    }
}
public sealed class FinancialStatement
{
    public long StaffCosts { get; set; }
    public long TaxDebt { get; set; }
    public long TaxesPaid { get; set; }
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
    public long DividendIncome { get; set; }
    public long DividendTax { get; set; }
    public long ShortDividendExpense { get; set; }
    public long ShortDividendPaid { get; set; }
    public long ShortDividendDebt { get; set; }
    public long WageIncome { get; set; }
    public long Consumption { get; set; }
    public long DebtRelief { get; set; }
    public long Assets => Cash + Holdings;
    public long Liabilities => ShortDebt + LoanDebt + FineDebt + ShortDividendDebt+TaxDebt;
    public long Equity => Assets - Liabilities;
    public double UnrealizedProfit => Holdings - Cost + ShortEntry - ShortDebt;
    public double ValuationChange => UnrealizedProfit - OpeningUnrealized;
    public double NetIncome => RealizedProfit + ValuationChange - Fees - Taxes - InterestExpense - BorrowFees - Fines + Subsidies + DebtRelief + DividendIncome - DividendTax - ShortDividendExpense + WageIncome - Consumption - StaffCosts;
    public long NetCashFlow => Sales - Purchases - Fees - TaxesPaid - InterestPaid - BorrowFees - FinesPaid + Subsidies + Borrowed - Repaid + DividendIncome - DividendTax - ShortDividendPaid + WageIncome - Consumption - StaffCosts;
    public void Add(FinancialStatement s)
    {
        TaxesPaid+=s.TaxesPaid; TaxDebt+=s.TaxDebt;
        Count += s.Count; Cash += s.Cash; Holdings += s.Holdings; Cost += s.Cost; OpeningCash += s.OpeningCash;
        OpeningEquity += s.OpeningEquity; OpeningUnrealized += s.OpeningUnrealized; RealizedProfit += s.RealizedProfit;
        Fees += s.Fees; Purchases += s.Purchases; Sales += s.Sales; ReservedCash += s.ReservedCash; Trades += s.Trades;
        ShortDebt += s.ShortDebt; ShortEntry += s.ShortEntry; LoanDebt += s.LoanDebt; FineDebt += s.FineDebt;
        Borrowed += s.Borrowed; Repaid += s.Repaid; InterestExpense += s.InterestExpense; InterestPaid += s.InterestPaid;
        BorrowFees += s.BorrowFees; Taxes += s.Taxes; Fines += s.Fines; FinesPaid += s.FinesPaid; Subsidies += s.Subsidies; DebtRelief += s.DebtRelief;
        DividendIncome += s.DividendIncome; DividendTax += s.DividendTax; ShortDividendExpense += s.ShortDividendExpense;
        ShortDividendPaid += s.ShortDividendPaid; ShortDividendDebt += s.ShortDividendDebt;
        WageIncome += s.WageIncome; Consumption += s.Consumption;
        StaffCosts += s.StaffCosts;
    }
}
public sealed class LimitOrder
{
    public long Id { get; set; }
    public int OwnerId { get; set; }
    public string SecurityId { get; set; } = "";
    public int StockIndex { get; set; }
    public bool Buy { get; set; }
    public bool Short { get; set; }
    public bool Cover { get; set; }
    public int CollateralPrice { get; set; }
    public int Price { get; set; }
    public int Remaining { get; set; }
    public long ExpiresAt { get; set; }
}
public sealed record BookLevel(int Price, long InstitutionQuantity, long RetailQuantity,long BankQuantity=0)
{ public long Quantity => InstitutionQuantity + RetailQuantity+BankQuantity; }
public sealed class TradeRecord
{
    public long Season { get; set; }
    public int Day { get; set; }
    public int Hour { get; set; }
    public int BuyerId { get; set; }
    public int SellerId { get; set; }
    public string SecurityId { get; set; } = "";
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
    public string SecurityId { get; set; } = "";
    public int StockIndex { get; set; }
    public string Headline { get; set; } = "";
    public string Detail { get; set; } = "";
    public double Impact { get; set; }
}
public sealed record RankHistory(long Season, int Rank, double Return, long Equity,int Generation=1);
public sealed record SeasonStanding(int TraderId, int Rank, long OpeningEquity, long Equity, double Return,int Generation=1,string InstitutionName="");
public sealed class SeasonResult
{
    public List<SeasonGrowthReward> GrowthRewards { get; set; } = [];
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
public sealed class GameState : ICashAccount
{
    public int Version { get; set; } = 7;
    public SimulationRules? Rules { get; set; }
    public long NextVoteId { get; set; } = 1;
    public long NextBankruptcyId { get; set; } = 1;
    public List<CompanyVote> CompanyVotes { get; set; } = [];
    public List<BankruptcyRecord> Bankruptcies { get; set; } = [];
    public BankruptcyTotals BankruptcyTotals { get; set; } = new();
    public string RunId { get; set; } = Guid.NewGuid().ToString("N");
    public bool MigratedFromV1 { get; set; }
    public bool MigratedFromV4 { get; set; }
    public List<string> SecurityIds { get; set; } = [];
    public long[] LegacyShares { get; set; } = [];
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
    public long TotalAiTrades { get; set; }
    public long TotalMatches { get; set; }
    public long MatchedVolume { get; set; }
    public long MatchedTurnover { get; set; }
    public Dictionary<string,long> SectorVolumes { get; set; } = [];
    public Dictionary<string,long> SectorTurnovers { get; set; } = [];
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
