using System.Text.Json.Serialization;

namespace AlphaExchange.Core;

public enum EmployeeGrade { S, A, B, C, D }
public enum AbilityKind { Valuation, Technical, News, RiskManagement, Diversification, Execution, SwingTrading, Scalping, Macro, Negotiation }
public enum InvestmentHorizon { UltraShort, Short, Long }
public sealed class EmployeeCohort
{
    public StaffJob? Job { get; set; }
    public string SectorSpecialty { get; set; } = "";
    public EmployeeGrade Grade { get; set; }
    public AbilityKind Role { get; set; }
    public int Count { get; set; }
}
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
    public int[] LegacyAbilities { get; set; } = [];
    public int CreditBonus { get; set; }
    public long LastRewardSeason { get; set; }
    public int LastRewardRank { get; set; }
    public int LastRewardPoints { get; set; }
    public List<EmployeeCohort> Employees { get; set; } = [];
    public long LastHireHour { get; set; } = -1;
    public long LastPayrollHour { get; set; }
    public long NextDecisionHour { get; set; }
    public long LastDecisionHour { get; set; } = -1;
    public double TargetCashRatio { get; set; }
    public long[] PositionOpenedHours { get; set; } = [];
    public long[] ShortOpenedHours { get; set; } = [];
    public long[] NextReviewHours { get; set; } = [];
    public InvestmentHorizon[] PositionHorizons { get; set; } = [];
    public double[] LastNewsSignals { get; set; } = [];
    public long PortfolioReviews { get; set; }
    [JsonIgnore] public int EmployeeCount => Employees.Sum(x=>x.Count);
}
public sealed record SeasonGrowthReward(int TraderId, int Generation, int Rank, int AbilityPoints, int CreditBonus, int[] AllocatedPoints);
public sealed record InstitutionSkills(Abilities Representative, Abilities Team, Abilities Effective);
public readonly record struct PositionTiming(long? AgeHours, InvestmentHorizon Planned, InvestmentHorizon? Actual, long NextReviewHour);
