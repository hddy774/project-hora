using System.Text.Json.Serialization;

namespace AlphaExchange.Core;

public enum PersonRole { Investor, Executive, Politician, Financier }
public enum StaffJob { Analyst, ExecutionTrader, PortfolioManager, RiskManager, ComplianceOfficer, TreasuryAccountant, Economist, QuantResearcher }
public enum PortfolioStyle { Defensive, Balanced, Growth, Opportunistic }
public enum WorldVoteKind { GovernmentElection, GovernorElection, EconomicPolicy, FundingLaw }
public sealed class Person : ICashAccount
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public PersonRole Role { get; set; }
    public string Office { get; set; } = "";
    public int OrganizationId { get; set; }
    public int Party { get; set; } = -1;
    public int PortraitId { get; set; }
    public long Cash { get; set; }
    public long OpeningCash { get; set; }
    public long Earnings { get; set; }
    public long DonationsReceived { get; set; }
    public long DonationsPaid { get; set; }
    public long FinesPaid { get; set; }
    public int[] Skills { get; set; } = new int[10];
    public double Reputation { get; set; } = 50;
    public double Performance { get; set; } = 50;
    public int VotingPower { get; set; } = 1;
    public int LastOverallRank { get; set; }
    public int Generation { get; set; } = 1;
}
public sealed class AnalystReport
{
    public int OwnerId { get; set; }
    public string Sector { get; set; } = "";
    public long WrittenMinute { get; set; }
    public long ExpiresMinute { get; set; }
    public double ExpectedReturn { get; set; }
    public double Confidence { get; set; }
    public string Thesis { get; set; } = "";
    public int SupportingAnalysts { get; set; }
}
public sealed class InvestmentPlan
{
    public string SecurityId { get; set; } = "";
    public long CreatedMinute { get; set; }
    public long ReviewMinute { get; set; }
    public long TargetMinute { get; set; }
    public long DurationMinutes { get; set; }
    public int LowerPrice { get; set; }
    public int UpperPrice { get; set; }
    public double TargetWeight { get; set; }
    public PortfolioStyle Style { get; set; }
    public InvestmentHorizon Horizon { get; set; }
    public string Thesis { get; set; } = "";
}
public sealed class BusinessProject
{
    public string SecurityId { get; set; } = "";
    public long StartHour { get; set; }
    public int DurationDays { get; set; }
    public int Stage { get; set; }
    public long Budget { get; set; }
    public long Paid { get; set; }
    public long SalesTarget { get; set; }
    public double Quality { get; set; }
    public bool Completed { get; set; }
    public string Detail { get; set; } = "";
}
public sealed record PersonStanding(int PersonId,PersonRole Role,int Rank,double Score,int VotingPower);
public sealed record WorldBallot(int PersonId,int Weight,int CandidateId);
public sealed class WorldVote
{
    public long Id { get; set; }
    public long Minute { get; set; }
    public WorldVoteKind Kind { get; set; }
    public string Title { get; set; } = "";
    public int ProposerId { get; set; }
    public int WinnerId { get; set; }
    public List<WorldBallot> Ballots { get; set; } = [];
}
public sealed record PersonActivity(long Id,long Minute,int ActorId,int RelatedPersonId,string SecurityId,string Kind,string Detail,long Amount=0);
public sealed class EconomyWorld
{
    public WorldRules? Rules { get; set; }
    public List<Person> People { get; set; } = [];
    public List<WorldVote> Votes { get; set; } = [];
    public List<PersonActivity> Activities { get; set; } = [];
    public List<BusinessProject> Projects { get; set; } = [];
    public long NextActivityId { get; set; } = 1;
    public long NextVoteId { get; set; } = 1;
    public long NextGovernmentElectionHour { get; set; }
    public long NextGovernorElectionHour { get; set; }
    public int LeaderId { get; set; } = 131;
    public int GovernorId { get; set; } = 181;
    public bool PoliticalFundingAllowed { get; set; } = true;
    public int PolicyMode { get; set; } = 1;
    public int RetailUnitPeople { get; set; } = 1000;
    public int PopulationAdded { get; set; }
    public double EconomicActivity { get; set; } = 1;
    public double Inflation { get; set; } = .02;
    public int NumberFormat { get; set; }
    public long LastRankingSeason { get; set; }
    [JsonIgnore] public long Revision => NextActivityId + NextVoteId;
}
