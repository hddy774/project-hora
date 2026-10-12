namespace AlphaExchange.Core;

public sealed record PortfolioRisk(double CashRatio, double GrossExposure, double NetExposure,
    double LargestStockWeight, double LargestSectorWeight, double Concentration,
    long EquityAfterTenPercentFall, long EquityAfterTenPercentRise, double MaximumDrawdown);

public static class FinancialAnalysis
{
    public static PortfolioRisk Portfolio(Trader trader, IReadOnlyList<Stock> stocks)
    {
        long gross = Math.Max(1, trader.GrossAssets(stocks));
        long equity = trader.Equity(stocks), holdings = gross - trader.Cash, shorts = trader.ShortLiability(stocks);
        double largest = 0, concentration = 0;
        var sectors = new Dictionary<string, long>(MarketDefaults.SectorCount);
        for (int i = 0; i < stocks.Count; i++)
        {
            long value = stocks[i].Value(trader.Shares[i]);
            double weight = (double)value / gross;
            largest = Math.Max(largest, weight); concentration += weight * weight;
            sectors[stocks[i].Sector] = checked(sectors.GetValueOrDefault(stocks[i].Sector) + value);
        }
        long shock = (long)decimal.Round((holdings - shorts) * .1m);
        return new((double)trader.Cash / gross, (double)(holdings + shorts) / Math.Max(1, equity),
            (double)(holdings - shorts) / Math.Max(1, equity), largest,
            sectors.Count == 0 ? 0 : sectors.Values.Max() / (double)gross, concentration,
            equity - shock, equity + shock, trader.Development?.MaximumDrawdown ?? 0);
    }

    public static long Ebitda(CompanyReport report) => report.OperatingProfit + report.Depreciation;
    public static long FreeCashFlow(CompanyReport report) => report.OperatingCashFlow + report.InvestingCashFlow;
    public static double? InterestCoverage(CompanyReport report) => report.Interest > 0 ? (double)report.OperatingProfit / report.Interest : null;
    public static double? CashConversion(CompanyReport report) => report.NetIncome > 0 ? (double)report.OperatingCashFlow / report.NetIncome : null;
    public static double ReturnOnAssets(CompanyReport report) => (double)report.NetIncome / Math.Max(1, report.Assets);
    public static long IncomeAfterRateRise(CompanyReport report) => report.NetIncome - (long)decimal.Round(report.Debt * .01m / 12);
}
