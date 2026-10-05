namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    public SimulationRules Rules => State.Rules ??= SimulationRules.Default();
    void InitializeDevelopment(Trader t)
    {
        if(t.IsRetail || t.Id==0) return;
        int count=State.Stocks.Count;
        t.Development=new InstitutionDevelopment {
            TargetCashRatio=Rules.Investment.BaseCash,
            PositionOpenedHours=Enumerable.Repeat(-1L,count).ToArray(),
            ShortOpenedHours=Enumerable.Repeat(-1L,count).ToArray(),
            NextReviewHours=new long[count], PositionHorizons=new InvestmentHorizon[count],LastNewsSignals=new double[count] };
    }
    public InstitutionSkills Capabilities(Trader t)
    {
        if(t.IsRetail || t.Id is <1 or >AiCount) throw new ArgumentException("기관 성장 정보가 없습니다.",nameof(t));
        if(t.Development is null) InitializeDevelopment(t);
        var e=Rules.Employees; var team=new Abilities(); var effective=new Abilities();
        Span<double> labor=stackalloc double[10];
        foreach(var cohort in t.Development!.Employees)
        {
            double skill=e.Grades[(int)cohort.Grade].Skill*cohort.Count;
            for(int i=0;i<labor.Length;i++) labor[i]+=skill*(i==(int)cohort.Role ? 1 : e.OtherRoleWeight);
        }
        for(int i=0;i<10;i++)
        {
            var kind=(AbilityKind)i;
            double organization=e.TeamBaseline+(e.TeamMaximum-e.TeamBaseline)*labor[i]/(labor[i]+e.TeamSaturation);
            team.Set(kind,(int)Math.Round(organization));
            effective.Set(kind,(int)Math.Round(t.Abilities.Get(kind)*Rules.Representative.Weight+organization*(1-Rules.Representative.Weight)));
        }
        return new(t.Abilities,team,effective);
    }
    public InvestmentHorizon HoldingHorizon(long hours)
    {
        if(hours<0) throw new ArgumentOutOfRangeException(nameof(hours));
        for(int i=0;i<Rules.Horizons.Length;i++)
            if(Rules.Horizons[i].MaximumHours is not {} maximum || hours<maximum) return (InvestmentHorizon)i;
        return InvestmentHorizon.Long;
    }
    public PositionTiming PositionTiming(Trader t,int index)
    {
        if(t.Development is not {} d || index<0 || index>=State.Stocks.Count) throw new ArgumentException("기관 보유 기간이 없습니다.");
        long opened=t.Shares[index]>0 ? d.PositionOpenedHours[index] : t.ShortShares[index]>0 ? d.ShortOpenedHours[index] : -1;
        long? age=opened>=0 ? State.CompletedHours-opened : null;
        return new(age,d.PositionHorizons[index],age.HasValue ? HoldingHorizon(age.Value) : null,d.NextReviewHours[index]);
    }
    public double CashRatio(Trader t) => (double)t.Cash/Math.Max(1,t.GrossAssets(State.Stocks));
    public long MonthlyPayroll(Trader t) => t.Development?.Employees.Sum(c=>Rules.Employees.Grades[(int)c.Grade].MonthlySalary*c.Count) ?? 0;
    public string? HireEmployee(int traderId,EmployeeGrade grade,AbilityKind role,int count=1)
    {
        if(traderId is <1 or >AiCount || !Enum.IsDefined(grade) || !Enum.IsDefined(role) || count<1 || count>Rules.Employees.MaximumCount)
            return "고용 값이 올바르지 않습니다.";
        var t=Owner(traderId); var d=t.Development!; var rule=Rules.Employees.Grades[(int)grade];
        if(t.WaitingForCapital || d.EmployeeCount+count>Rules.Employees.MaximumCount) return "직원 수 또는 운영 상태가 제한됩니다.";
        long cost=checked(rule.HiringCost*count),gross=t.GrossAssets(State.Stocks);
        long budget=Math.Max(0,AvailableCash(t)-(long)Math.Ceiling(gross*Rules.Investment.MinimumCash));
        if(cost>budget || MonthlyPayroll(t)+rule.MonthlySalary*count>gross*Rules.Employees.PayrollAssetRatio)
            return "현금 완충 또는 급여 예산이 부족합니다.";
        TransferCash(t,State.RealEconomy,cost,"staff-hiring"); t.StaffCosts=checked(t.StaffCosts+cost);
        var cohort=d.Employees.Find(c=>c.Grade==grade && c.Role==role);
        if(cohort is null) d.Employees.Add(new EmployeeCohort { Grade=grade,Role=role,Count=count }); else cohort.Count+=count;
        d.NextDecisionHour=State.CompletedHours; cachedStats=null; return null;
    }
    void ServiceStaff()
    {
        foreach(var t in State.Bots)
        {
            var d=t.Development!;
            if(d.LastPayrollHour>=State.CompletedHours) continue;
            d.LastPayrollHour=State.CompletedHours;
            long daily=DailyPayroll(d);
            // Pay actual temporary service contracts. When funding runs out,
            // reduce headcount before committing an unaffordable new day's work.
            while(daily>AvailableCash(t) && d.Employees.Count>0)
            {
                var largest=d.Employees.OrderByDescending(c=>Rules.Employees.Grades[(int)c.Grade].MonthlySalary).First();
                if(--largest.Count==0) d.Employees.Remove(largest);
                daily=DailyPayroll(d); d.NextDecisionHour=State.CompletedHours;
            }
            if(daily>0) { TransferCash(t,State.RealEconomy,daily,"staff-payroll"); t.StaffCosts=checked(t.StaffCosts+daily); }
            if(t.WaitingForCapital || State.CompletedHours<Rules.Employees.FirstHireHour ||
                d.LastHireHour>=0 && State.CompletedHours-d.LastHireHour<Rules.Employees.HireIntervalHours) continue;
            d.LastHireHour=State.CompletedHours;
            for(int n=0;n<Rules.Employees.HireLimit && d.EmployeeCount<Rules.Employees.MaximumCount;n++)
            {
                var skills=Capabilities(t).Team;
                int first=(int)(Next()*10),role=first;
                for(int j=0;j<10;j++) { int index=(first+j)%10; if(skills.Get((AbilityKind)index)<skills.Get((AbilityKind)role)) role=index; }
                bool hired=false;
                for(int g=0;g<Rules.Employees.Grades.Length;g++)
                    if(HireEmployee(t.Id,(EmployeeGrade)g,(AbilityKind)role) is null) { hired=true; break; }
                if(!hired) break;
            }
        }
        cachedStats=null;
    }
    long DailyPayroll(InstitutionDevelopment d) => d.Employees.Sum(c=>(Rules.Employees.Grades[(int)c.Grade].MonthlySalary+SeasonLength-1)/SeasonLength*c.Count);
    internal SeasonGrowthReward? AwardSeasonGrowth(Trader t,long season,int rank)
    {
        if(season<1 || season>=State.Season || rank is <1 or >AiCount) throw new ArgumentOutOfRangeException(nameof(season));
        var d=t.Development!;
        if(season<=d.LastRewardSeason) return null;
        d.LastRewardSeason=season; d.LastRewardRank=rank;
        var reward=!t.WaitingForCapital && t.BirthHour<=(season-1)*SeasonLength*24 ? Rules.Rewards.FirstOrDefault(r=>rank<=r.MaximumRank) : null;
        int points=reward?.AbilityPoints ?? 0,credit=reward?.CreditBonus ?? 0;
        d.LastRewardPoints=points; d.PointsEarned=checked(d.PointsEarned+points); d.AvailablePoints=checked(d.AvailablePoints+points);
        d.CreditBonus=Math.Min(Rules.Representative.MaximumCreditBonus,(int)Math.Round(d.CreditBonus*Rules.Representative.CreditRetention)+credit);
        t.CreditScore=Math.Min(99,t.CreditScore+credit);
        var allocation=new int[10];
        while(d.AvailablePoints>0)
        {
            int best=-1; double value=double.MinValue;
            for(int i=0;i<10;i++)
            {
                int current=t.Abilities.Get((AbilityKind)i); if(current>=Rules.Representative.AbilityMaximum) continue;
                double priority=Rules.Representative.PointPriorities[i]*(Rules.Representative.AbilityMaximum-current);
                if(d.TargetCashRatio>=Rules.Investment.MaximumCash-Rules.Investment.CashTolerance && i==(int)AbilityKind.RiskManagement) priority*=1+Rules.Representative.Weight;
                if(priority>value) { best=i; value=priority; }
            }
            if(best<0) break;
            t.Abilities.Set((AbilityKind)best,t.Abilities.Get((AbilityKind)best)+1);
            d.AllocatedPoints[best]++; allocation[best]++; d.PointsSpent++; d.AvailablePoints--;
        }
        d.NextDecisionHour=State.CompletedHours;
        return new(t.Id,t.Generation,rank,points,credit,allocation);
    }
    void RecordPositionOpen(Trader t,int index,bool shortPosition,bool wasEmpty)
    {
        if(!wasEmpty || t.Development is not {} d) return;
        (shortPosition ? d.ShortOpenedHours : d.PositionOpenedHours)[index]=State.CompletedHours;
        d.NextReviewHours[index]=State.CompletedHours+Rules.Horizons[(int)d.PositionHorizons[index]].ReviewHours;
    }
    void RefreshPositionTiming(Trader t,int index)
    {
        if(t.Development is not {} d) return;
        if(t.Shares[index]==0) d.PositionOpenedHours[index]=-1;
        if(t.ShortShares[index]==0) d.ShortOpenedHours[index]=-1;
        d.NextReviewHours[index]=State.CompletedHours;
        d.NextDecisionHour=State.CompletedHours;
    }
}
