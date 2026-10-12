namespace AlphaExchange.Core;

public sealed class DailySnapshot
{
    public Dictionary<string, long> SectorVolumes { get; set; } = [];
    public Dictionary<string, long> SectorTurnovers { get; set; } = [];
    public int CohortTradingBasis { get; set; }
    public double InstitutionTradingIncome { get; set; }
    public double RetailTradingIncome { get; set; }
    public long CompanyBankruptcies { get; set; }
    public long InstitutionBankruptcies { get; set; }
    public long RetailBankruptcies { get; set; }
    public int Resolution { get; set; } = 1;
    public bool LegacyNoFundamentals { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public bool GapBefore { get; set; }
    public long Hour { get; set; }
    public Dictionary<string, long> CashFlows { get; set; } = [];
    public double PriceIndex { get; set; }
    public double TotalReturnIndex { get; set; }
    public long CompanyEquity { get; set; }
    public long CompanyProfit { get; set; }
    public long Dividends { get; set; }
    public long GovernmentCash { get; set; }
    public long BankCash { get; set; }
    public long ExchangeCash { get; set; }
    public long RealEconomyCash { get; set; }
    public string[] SecurityIds { get; set; } = [];
    public string[] StockSectors { get; set; } = [];
    public long[] StockShares { get; set; } = [];
    public long[] FloatShares { get; set; } = [];
    public decimal[] MarkPrices { get; set; } = [];
    public double[] SplitFactors { get; set; } = [];
    public long Capitalization { get; set; }
    public long Volume { get; set; }
    public long Turnover { get; set; }
    public long InstitutionEquity { get; set; }
    public long RetailEquity { get; set; }
    public long InstitutionCash { get; set; }
    public long RetailCash { get; set; }
    public List<TraderSnapshot> Institutions { get; set; } = [];
    public long[] StockVolumes { get; set; } = [];
    public long[] StockTurnovers { get; set; } = [];
    public int[] StockPrices { get; set; } = [];
}
