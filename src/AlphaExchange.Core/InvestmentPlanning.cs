namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    public IReadOnlyList<AnalystReport> ConfidentialReports(int requesterId,int ownerId)
        => requesterId==ownerId && ownerId is >=1 and <=100 ? Owner(ownerId).Development!.AnalystReports.AsReadOnly() : Array.Empty<AnalystReport>();
    public IReadOnlyList<InvestmentPlan> ConfidentialPlans(int requesterId,int ownerId)
        => requesterId==ownerId && ownerId is >=1 and <=100 ? Owner(ownerId).Development!.Plans.AsReadOnly() : Array.Empty<InvestmentPlan>();
    void WriteAnalystReports(Trader t,Abilities chief)
    {
        var d=t.Development!;
        if(d.LastReportMinute>=0 && State.CompletedMinutes-d.LastReportMinute<WorldRules.ReportIntervalHours*60 &&
            !d.AnalystReports.Any(r=>r.ExpiresMinute<=State.CompletedMinutes)) return;
        d.AnalystReports.Clear();
        foreach(var sector in State.Stocks.Select((s,i)=>(s,i)).Where(x=>x.s.Active).GroupBy(x=>x.s.Sector))
        {
            int analysts=d.Employees.Where(c=>c.Job==StaffJob.Analyst && (c.SectorSpecialty==sector.Key || c.SectorSpecialty=="전체")).Sum(c=>c.Count);
            double skill=(chief.Valuation+chief.News+chief.Macro)/300.0;
            double raw=sector.Average(x=>Math.Clamp(x.s.FairValue/Math.Max(1,x.s.Price)-1,-.5,.8)*.45+trends[x.i]*.20+
                x.s.Sentiment*.25+(World.EconomicActivity-1)*.1-State.Government.Policy.BaseRate*x.s.Report.DebtRatio*.1);
            double noise=(Next()-.5)*.06*(1-skill)/(1+analysts*.2);
            double expected=Math.Clamp(raw*(.5+skill*.5)+noise,-.5,.8),confidence=Math.Clamp(.35+skill*.35+analysts*.025,.1,.95);
            d.AnalystReports.Add(new AnalystReport { OwnerId=t.Id,Sector=sector.Key,WrittenMinute=State.CompletedMinutes,
                ExpiresMinute=State.CompletedMinutes+WorldRules.ReportLifetimeHours*60,ExpectedReturn=expected,Confidence=confidence,SupportingAnalysts=analysts,
                Thesis=$"{sector.Key} {(expected>=0 ? "상승" : "하락")} 전망 · 가치·공개 공시·거시 수요 검토 · 대표 책임 분석" });
        }
        d.LastReportMinute=State.CompletedMinutes;
    }
    InvestmentPlan PlanInvestment(Trader t,int index,InvestmentSignal signal)
    {
        var d=t.Development!; var stock=State.Stocks[index]; var report=d.AnalystReports.Find(r=>r.Sector==stock.Sector);
        var previous=d.Plans.Find(p=>p.SecurityId==stock.SecurityId);
        bool breakBand=previous is not null && (stock.Price<previous.LowerPrice || stock.Price>previous.UpperPrice);
        if(previous is not null && previous.ReviewMinute>State.CompletedMinutes && previous.Horizon==signal.Horizon && !breakBand && !signal.Defensive) return previous;
        PortfolioStyle style=signal.Defensive || t.Disposition==Disposition.Cautious ? PortfolioStyle.Defensive :
            t.Disposition==Disposition.Aggressive ? PortfolioStyle.Opportunistic : signal.Horizon==InvestmentHorizon.Long ? PortfolioStyle.Growth : PortfolioStyle.Balanced;
        long min=Rules.Horizons[(int)signal.Horizon].MinimumHours*60L;
        long max=signal.Horizon==InvestmentHorizon.Long ? min*3 : Rules.Horizons[(int)signal.Horizon].MaximumHours!.Value*60L-1;
        long duration=min+(long)((max-min)*Math.Clamp(t.Patience*.65+(report?.Confidence ?? .5)*.35,0,1));
        double confidence=report?.Confidence ?? .5;
        double band=Math.Clamp(WorldRules.PlanBandMinimum+signal.Volatility*Math.Sqrt(Math.Max(1,duration/60.0))*(style==PortfolioStyle.Opportunistic ? 3 : 1.5),WorldRules.PlanBandMinimum,WorldRules.PlanBandMaximum);
        double estimate=Math.Clamp(signal.Score+(report?.ExpectedReturn ?? 0)*confidence,-band,band);
        int lower=Math.Clamp((int)Math.Floor(stock.Price*(1-band+Math.Min(0,estimate))),1,10_000_000);
        int upper=Math.Clamp((int)Math.Ceiling(stock.Price*(1+band+Math.Max(0,estimate))),lower,10_000_000);
        var plan=new InvestmentPlan { SecurityId=stock.SecurityId,CreatedMinute=State.CompletedMinutes,ReviewMinute=State.CompletedMinutes+Rules.Horizons[(int)signal.Horizon].ReviewHours*60,
            TargetMinute=State.CompletedMinutes+duration,DurationMinutes=duration,LowerPrice=lower,UpperPrice=upper,TargetWeight=signal.StockLimit*(style==PortfolioStyle.Defensive ? .65 : .9),
            Style=style,Horizon=signal.Horizon,Thesis=$"{report?.Thesis ?? "가격·가치 검토"} · {duration/1440.0:0.##}일 계획 · 범위 이탈 시 재검토" };
        if(previous is not null) d.Plans.Remove(previous); d.Plans.Add(plan); d.PlanRevision++;
        return plan;
    }
    public string? HireSpecialist(int traderId,EmployeeGrade grade,StaffJob job,string sector="전체",int count=1)
    {
        if(traderId is <1 or >100 || !Enum.IsDefined(job) || sector!="전체" && !State.Stocks.Any(s=>s.Sector==sector)) return "직무·업종 값이 올바르지 않습니다.";
        var weights=WorldRules.Jobs[(int)job].Weights;
        var role=(AbilityKind)Array.IndexOf(weights,weights.Max());
        var t=Owner(traderId); var d=t.Development!; int before=d.EmployeeCount;
        string? error=HireEmployee(traderId,grade,role,count); if(error is not null) return error;
        // Separate contracts even when two professions share the same dominant ability.
        var legacy=d.Employees.First(c=>c.Grade==grade && c.Role==role && c.Job is null);
        legacy.Count-=count; if(legacy.Count==0) d.Employees.Remove(legacy);
        var cohort=d.Employees.Find(c=>c.Grade==grade && c.Job==job && c.SectorSpecialty==sector);
        if(cohort is null) d.Employees.Add(new EmployeeCohort { Grade=grade,Role=role,Job=job,SectorSpecialty=sector,Count=count }); else cohort.Count+=count;
        d.LastReportMinute=-1; d.NextExecutionMinute=State.CompletedMinutes;
        if(d.EmployeeCount!=before+count) throw new InvalidOperationException("고용 수량 불일치");
        return null;
    }
    int ExecutionDelay(Trader t,Abilities skills)
    {
        int execution=t.Development!.Employees.Where(c=>c.Job==StaffJob.ExecutionTrader).Sum(c=>c.Count);
        return Math.Clamp((int)Math.Ceiling(60/(1+execution*.3+skills.Execution/100.0)),1,60);
    }
}
