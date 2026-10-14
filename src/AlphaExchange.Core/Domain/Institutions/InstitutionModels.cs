using System.Text.Json.Serialization;

namespace AlphaExchange.Core;

public enum AbilityKind { Valuation, Technical, News, RiskManagement, Diversification, Execution, SwingTrading, Scalping, Macro, Negotiation }
public enum InvestmentHorizon { UltraShort, Short, Long }
public sealed class InstitutionDevelopment
{
    public List<AnalystReport> AnalystReports { get; set; } = [];
    public List<InvestmentPlan> Plans { get; set; } = [];
    public long NextExecutionMinute { get; set; }
    public long LastReportMinute { get; set; } = -1;
    public long PlanRevision { get; set; }
    public long PointsEarned { get; set; }
    public long PointsSpent { get; set; }
    public int AvailablePoints { get; set; }
    public int[] AllocatedPoints { get; set; } = new int[10];
    public int CreditBonus { get; set; }
    public long LastRewardSeason { get; set; }
    public int LastRewardRank { get; set; }
    public int LastRewardPoints { get; set; }
    public long NextDecisionHour { get; set; }
    public long LastDecisionHour { get; set; } = -1;
    public double TargetCashRatio { get; set; }
    public long[] PositionOpenedHours { get; set; } = [];
    public long[] ShortOpenedHours { get; set; } = [];
    public long[] NextReviewHours { get; set; } = [];
    public InvestmentHorizon[] PositionHorizons { get; set; } = [];
    public double[] LastNewsSignals { get; set; } = [];
    public int CampaignIndex { get; set; } = -1;
    public CampaignPhase CampaignPhase { get; set; }
    public long CampaignSinceMinute { get; set; }
    public int CampaignReferencePrice { get; set; }
    public long LastDecisionMinute { get; set; } = -1;
    public long PeakEquity { get; set; }
    public double MaximumDrawdown { get; set; }
    public long AggressiveOrders { get; set; }
    public long PortfolioReviews { get; set; }
    public long SuccessfulInvestments { get; set; }
    public long CompletedInvestments { get; set; }
    public long OutcomesSinceMinute { get; set; }
    public long[] LastExitMinutes { get; set; } = Enumerable.Repeat(-1L, MarketDefaults.StockCount).ToArray();
    public decimal[] InvestmentNet { get; set; } = new decimal[MarketDefaults.StockCount];
    public decimal[] ShortInvestmentNet { get; set; } = new decimal[MarketDefaults.StockCount];
    public bool[] TrackedInvestments { get; set; } = new bool[MarketDefaults.StockCount];
    public bool[] TrackedShortInvestments { get; set; } = new bool[MarketDefaults.StockCount];
    public long TurnoverDay { get; set; } = -1;
    public long DayTurnover { get; set; }
}
public sealed record SeasonGrowthReward(int TraderId, int Generation, int Rank, int AbilityPoints, int CreditBonus, int[] AllocatedPoints);
public sealed record InstitutionSkills(Abilities Representative, Abilities Effective);
public readonly record struct PositionTiming(long? AgeHours, InvestmentHorizon Planned, InvestmentHorizon? Actual, long NextReviewHour);
