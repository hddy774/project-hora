
namespace AlphaExchange.Core;

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

public sealed record RankHistory(long Season, int Rank, double Return, long Equity, int Generation = 1);

public sealed record SeasonStanding(int TraderId, int Rank, long OpeningEquity, long Equity, double Return, int Generation = 1, string InstitutionName = "");
