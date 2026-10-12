using System.Text.Json.Serialization;
using System.Runtime.InteropServices;

namespace AlphaExchange.Core;

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
    public long[] Shares { get; set; } = new long[MarketDefaults.StockCount];
    public double[] AverageCost { get; set; } = new double[MarketDefaults.StockCount];
    public long ReservedCash { get; set; }
    public long[] ReservedShares { get; set; } = new long[MarketDefaults.StockCount];
    public long[] ShortShares { get; set; } = new long[MarketDefaults.StockCount];
    public double[] ShortAveragePrice { get; set; } = new double[MarketDefaults.StockCount];
    public long[] ReservedCovers { get; set; } = new long[MarketDefaults.StockCount];
    public Abilities Abilities { get; set; } = new();
    public InstitutionDevelopment? Development { get; set; }
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
    [JsonIgnore] public int PortraitId => IsRetail || Id == 0 ? 0 : (Id - 1 + (Generation - 1) * 17) % MarketDefaults.InstitutionCount + 1;
    [JsonIgnore] public string Gender => PortraitId <= 70 ? "여성" : "남성";
    public long Equity(IReadOnlyList<Stock> stocks)
    {
        if (stocks is List<Stock> list) return Equity(CollectionsMarshal.AsSpan(list));
        return GrossAssets(stocks) - ShortLiability(stocks) - LoanDebt - FineDebt - ShortDividendDebt - TaxDebt;
    }
    long Equity(ReadOnlySpan<Stock> stocks)
    {
        long gross = Cash, shorts = 0;
        if (Shares.AsSpan().ContainsAnyExcept(0L) || ShortShares.AsSpan().ContainsAnyExcept(0L)) for (int i = 0; i < Shares.Length; i++)
            {
                if (Shares[i] != 0) gross = checked(gross + stocks[i].Value(Shares[i]));
                if (ShortShares[i] != 0) shorts = checked(shorts + stocks[i].Value(ShortShares[i]));
            }
        return gross - shorts - LoanDebt - FineDebt - ShortDividendDebt - TaxDebt;
    }
    public long GrossAssets(IReadOnlyList<Stock> stocks)
    { long value = Cash; for (int i = 0; i < Shares.Length; i++) if (Shares[i] != 0) value = checked(value + stocks[i].Value(Shares[i])); return value; }
    public long ShortLiability(IReadOnlyList<Stock> stocks)
    { long value = 0; for (int i = 0; i < ShortShares.Length; i++) if (ShortShares[i] != 0) value = checked(value + stocks[i].Value(ShortShares[i])); return value; }
    public long ShortCollateral(IReadOnlyList<Stock> stocks) => (long)Math.Ceiling(ShortLiability(stocks) * 1.5);
    public double Return(IReadOnlyList<Stock> stocks) => ReturnIndex / Math.Max(1e-12, IsRetail ? SeasonReturnBasis : SeasonSnapshot.ReturnIndex) - 1;
    public FinancialStatement Financials(IReadOnlyList<Stock> stocks)
    {
        var result = new FinancialStatement(); AccumulateFinancials(result, stocks); return result;
    }
    public long AccumulateFinancials(FinancialStatement result, IReadOnlyList<Stock> stocks)
    {
        long gross = Cash, shortDebt = 0; double cost = 0, shortEntry = 0;
        if (Shares.AsSpan().ContainsAnyExcept(0L) || ShortShares.AsSpan().ContainsAnyExcept(0L)) for (int i = 0; i < Shares.Length; i++)
            {
                long shares = Shares[i], shorts = ShortShares[i];
                if (shares != 0) { gross = checked(gross + stocks[i].Value(shares)); cost += shares * AverageCost[i]; }
                if (shorts != 0) { shortDebt = checked(shortDebt + stocks[i].Value(shorts)); shortEntry += shorts * ShortAveragePrice[i]; }
            }
        return AccumulateFinancialTotals(result, gross, shortDebt, cost, shortEntry);
    }

    internal long AccumulateFinancials(FinancialStatement result, ReadOnlySpan<ShareValuation> prices)
    {
        long gross = Cash, shortDebt = 0;
        double cost = 0, shortEntry = 0;
        if (Shares.AsSpan().ContainsAnyExcept(0L) || ShortShares.AsSpan().ContainsAnyExcept(0L))
            for (int i = 0; i < Shares.Length; i++)
            {
                long shares = Shares[i], shorts = ShortShares[i];
                if (shares != 0) { gross = checked(gross + prices[i].Value(shares)); cost += shares * AverageCost[i]; }
                if (shorts != 0) { shortDebt = checked(shortDebt + prices[i].Value(shorts)); shortEntry += shorts * ShortAveragePrice[i]; }
            }
        return AccumulateFinancialTotals(result, gross, shortDebt, cost, shortEntry);
    }

    long AccumulateFinancialTotals(FinancialStatement result, long gross, long shortDebt, double cost, double shortEntry)
    {
        long holdings = gross - Cash;
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
        result.TaxesPaid += TaxesPaid; result.TaxDebt += TaxDebt;
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
        return gross - shortDebt - LoanDebt - FineDebt - ShortDividendDebt - TaxDebt;
    }
}
