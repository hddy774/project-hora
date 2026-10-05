using System.Text.Json;

namespace AlphaExchange.Core;

// Embedded scripts are copied into saves. Balance changes do not rewrite a running economy.
public sealed class WorldRules
{
    public string Version { get; set; } = "world-1.7";
    public long MarketCapitalization { get; set; }
    public long InitialFloatShares { get; set; }
    public int LiquidityMinimumQuantity { get; set; }
    public int LiquidityMaximumQuantity { get; set; }
    public int StaffCostScale { get; set; }
    public int RetailTradeDivisor { get; set; }
    public double RetailHerdWeight { get; set; } = .25;
    public double RetailProfitTakingWeight { get; set; } = .55;
    public double RetailProfitTakingReturn { get; set; } = .20;
    public double RetailProfitTakingAggression { get; set; } = .04;
    public double RetailTakeLiquidityProbability { get; set; } = .70;
    public long InvestorCapital { get; set; }
    public long RetailCapital { get; set; }
    public long BankCapital { get; set; }
    public long GovernmentCapital { get; set; }
    public long RealEconomyCapital { get; set; }
    public long PersonalCapital { get; set; }
    public int InitialRetailUnits { get; set; }
    public int MaximumRetailUnits { get; set; }
    public int PeoplePerRetailUnit { get; set; }
    public int DailyRetailGrowth { get; set; }
    public int MinimumElectionDays { get; set; }
    public int MaximumElectionDays { get; set; }
    public int ReportIntervalHours { get; set; }
    public int ReportLifetimeHours { get; set; }
    public double AnalystWeight { get; set; }
    public double PlanBandMinimum { get; set; }
    public double PlanBandMaximum { get; set; }
    public double BusinessBudgetRatio { get; set; }
    public int[] BusinessDurationDays { get; set; } = [];
    public string[] BusinessStages { get; set; } = [];
    public double SalaryRatio { get; set; }
    public double DonationRatio { get; set; }
    public double FundingFineRatio { get; set; }
    public double[] CompositeWeights { get; set; } = [];
    public int[] RewardRanks { get; set; } = [];
    public int[] RewardPoints { get; set; } = [];
    public StaffJobRule[] Jobs { get; set; } = [];
    public PolicyScript[] Policies { get; set; } = [];
    public RoleScript[] Roles { get; set; } = [];
    public string[] Surnames { get; set; } = [];
    public string[] GivenNames { get; set; } = [];
    public static WorldRules Default()
    {
        using var stream=typeof(WorldRules).Assembly.GetManifestResourceStream("AlphaExchange.Core.Rules.world-v1.json")!;
        var rules=JsonSerializer.Deserialize<WorldRules>(stream,new JsonSerializerOptions { PropertyNameCaseInsensitive=true })!;
        rules.Validate(); return rules;
    }
    public WorldRules Copy() => JsonSerializer.Deserialize<WorldRules>(JsonSerializer.Serialize(this))!;
    public void Validate()
    {
        if(!double.IsFinite(RetailHerdWeight) || RetailHerdWeight is <0 or >.4 ||
            !double.IsFinite(RetailProfitTakingWeight) || RetailProfitTakingWeight is <0 or >.8 ||
            !double.IsFinite(RetailProfitTakingReturn) || RetailProfitTakingReturn is <=0 or >1 ||
            !double.IsFinite(RetailProfitTakingAggression) || RetailProfitTakingAggression is <0 or >.1 ||
            !double.IsFinite(RetailTakeLiquidityProbability) || RetailTakeLiquidityProbability is <0 or >1)
            throw new InvalidDataException("개미 시장 반응 규칙 손상");
        if(LiquidityMinimumQuantity<1 || LiquidityMaximumQuantity>1000 || LiquidityMaximumQuantity<LiquidityMinimumQuantity || StaffCostScale is <1 or >100 || RetailTradeDivisor<1 || Version!="world-1.7" || MarketCapitalization is <100000000 or >100000000000000 || InvestorCapital<=0 || RetailCapital<=0 ||
            BankCapital<0 || GovernmentCapital<0 || RealEconomyCapital<0 || PersonalCapital<0 || InitialRetailUnits<1 ||
            MaximumRetailUnits>GameEngine.RetailCount || InitialRetailUnits>MaximumRetailUnits || PeoplePerRetailUnit<1 || DailyRetailGrowth<0 ||
            MinimumElectionDays<10 || MaximumElectionDays>30 || MinimumElectionDays>MaximumElectionDays || ReportIntervalHours<1 ||
            ReportLifetimeHours<ReportIntervalHours || !double.IsFinite(AnalystWeight) || AnalystWeight is <0 or >1 ||
            PlanBandMinimum<=0 || PlanBandMaximum>=1 || PlanBandMaximum<PlanBandMinimum || BusinessBudgetRatio is <0 or >.1 ||
            BusinessDurationDays.Length!=2 || BusinessDurationDays[0]<1 || BusinessDurationDays[1]>30 || BusinessDurationDays[0]>BusinessDurationDays[1] ||
            BusinessStages.Length!=3 || SalaryRatio is <0 or >.01 || DonationRatio is <=0 or >.1 || FundingFineRatio is <=0 or >1 ||
            CompositeWeights.Length!=4 || CompositeWeights.Any(x=>!double.IsFinite(x)||x<0) || Math.Abs(CompositeWeights.Sum()-1)>1e-9 ||
            RewardRanks.Length!=10 || !RewardRanks.SequenceEqual(new[]{1,2,3,5,10,20,30,40,50,100}) || !RewardPoints.SequenceEqual(Enumerable.Range(1,10).Reverse()) ||
            Jobs.Length!=8 || Jobs.Any(j=>j.Weights.Length!=10 || j.Weights.Any(x=>!double.IsFinite(x)||x<0||x>1)) ||
            Policies.Length!=3 || Policies.Any(p=>p.TaxRate is <0 or >.5 || p.BaseRate is <0 or >.5 || p.FeeBasisPoints is <0 or >100 || p.SubsidyRate is <0 or >.01) ||
            Roles.Length!=4 || Roles.Any(r=>r.Skills.Length!=10) || Surnames.Length<10 || GivenNames.Length<10)
            throw new InvalidDataException("경제 스크립트 손상");
    }
}
public sealed class StaffJobRule
{
    public string Name { get; set; } = "";
    public string Duty { get; set; } = "";
    public double[] Weights { get; set; } = [];
}
public sealed class PolicyScript
{
    public string Name { get; set; } = "";
    public double TaxRate { get; set; }
    public int FeeBasisPoints { get; set; }
    public double BaseRate { get; set; }
    public double SubsidyRate { get; set; }
    public double SpendingRatio { get; set; }
    public double Enforcement { get; set; }
    public bool ShortSellingAllowed { get; set; }
}
public sealed class RoleScript
{
    public string Name { get; set; } = "";
    public string[] Skills { get; set; } = [];
}
