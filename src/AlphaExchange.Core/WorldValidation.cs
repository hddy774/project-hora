namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    static void ValidateWorld(GameState s)
    {
        var w=s.World ?? throw new InvalidDataException("인물 경제 없음"); w.Rules?.Validate();
        if(w.Rules is null || w.People.Count!=200 || w.People.Where((p,i)=>p.Id!=i+1).Any() || w.People.GroupBy(p=>p.Role).Select(g=>g.Count()).Order().SequenceEqual(new[]{20,30,50,100})==false ||
            w.People.Any(p=>!Enum.IsDefined(p.Role) || p.Role!=(p.Id<=100 ? PersonRole.Investor : p.Id<=130 ? PersonRole.Executive : p.Id<=180 ? PersonRole.Politician : PersonRole.Financier) ||
                string.IsNullOrWhiteSpace(p.Name) || p.Cash<0 || p.OpeningCash<0 || p.Earnings<0 || p.DonationsPaid<0 || p.DonationsReceived<0 || p.FinesPaid<0 ||
                p.Cash!=p.OpeningCash+p.Earnings+p.DonationsReceived-p.DonationsPaid-p.FinesPaid || p.Skills.Length!=10 || p.Skills.Any(x=>x is <1 or >100) ||
                !double.IsFinite(p.Performance) || p.Performance is <0 or >100 || !double.IsFinite(p.Reputation) || p.Reputation is <0 or >100 ||
                p.VotingPower is <1 or >10 || p.PortraitId is <1 or >200 || p.Role==PersonRole.Politician && p.Party is <0 or >1) ||
            w.People.Where(p=>p.Role==PersonRole.Politician).GroupBy(p=>p.Party).Any(g=>g.Count()!=25) ||
            w.LeaderId is <131 or >180 || w.GovernorId is <181 or >200 || w.NextGovernmentElectionHour<s.CompletedHours || w.NextGovernorElectionHour<s.CompletedHours ||
            !double.IsFinite(w.EconomicActivity) || w.EconomicActivity is <.5 or >2 || !double.IsFinite(w.Inflation) || w.Inflation is <-.1 or >.5 ||
            w.RetailUnitPeople<1 || w.NumberFormat is <0 or >1 || w.PolicyMode is <0 or >2 || w.NextActivityId<1 || w.NextVoteId<1 || w.Activities.Count>240 || w.Votes.Count>32 ||
            s.CompletedMinutes<0 || s.CompletedMinutes/60!=s.CompletedHours || !double.IsFinite(s.MinuteProgress) || s.MinuteProgress is <0 or >=1 || s.PendingClockMinutes<0 ||
            s.MinuteActors.Count>900 || s.MinuteActors.Any(id=>id<1 || id>AiCount+s.Retail.Count) || s.Minute==0 && s.MinuteActors.Count!=0 || s.Minute!=0 && s.MinuteActors.Count<AiCount)
            throw new InvalidDataException("인물·시간·경제 데이터 손상");
        if(w.Activities.Any(a=>a.Id<1 || a.Id>=w.NextActivityId || a.Minute<0 || a.Minute>s.CompletedMinutes || a.ActorId is <1 or >200 || a.RelatedPersonId is <0 or >200 || a.Amount<0) ||
            w.Votes.Any(v=>v.Id<1 || v.Id>=w.NextVoteId || v.Minute<0 || v.Minute>s.CompletedMinutes || !Enum.IsDefined(v.Kind) || v.ProposerId is <1 or >200 ||
                v.Ballots.Count!=200 || v.Ballots.Select(b=>b.PersonId).Distinct().Count()!=200 || v.Ballots.Any(b=>b.PersonId is <1 or >200 || b.Weight is <1 or >10) ||
                WinningCandidate(v)!=v.WinnerId) || w.Projects.Any(p=>p.StartHour<0 || p.StartHour>s.CompletedHours || p.DurationDays is <1 or >30 || p.Stage is <0 or >2 ||
                    p.Paid<0 || p.Paid>p.Budget || p.Budget<0 || !double.IsFinite(p.Quality) || p.Quality is <0 or >2))
            throw new InvalidDataException("활동·투표·사업 데이터 손상");
        foreach(var t in s.Bots)
        {
            var d=t.Development!;
            if(d.AnalystReports.Count>30 || d.Plans.Count>s.Stocks.Count || d.NextExecutionMinute<0 || d.PlanRevision<0 ||
                d.Employees.Any(c=>c.Job is {} j && !Enum.IsDefined(j)) ||
                d.AnalystReports.Any(r=>r.OwnerId!=t.Id || r.WrittenMinute<0 || r.WrittenMinute>s.CompletedMinutes || r.ExpiresMinute<r.WrittenMinute ||
                    !double.IsFinite(r.Confidence) || r.Confidence is <0 or >1 || !double.IsFinite(r.ExpectedReturn) || r.ExpectedReturn is <-1 or >2) ||
                d.Plans.Select(p=>p.SecurityId).Distinct().Count()!=d.Plans.Count || d.Plans.Any(p=>p.CreatedMinute<0 || p.CreatedMinute>s.CompletedMinutes ||
                    p.DurationMinutes<60 || p.TargetMinute!=p.CreatedMinute+p.DurationMinutes || p.LowerPrice<1 || p.UpperPrice<p.LowerPrice || p.UpperPrice>10_000_000 ||
                    !double.IsFinite(p.TargetWeight) || p.TargetWeight is <0 or >1 || !Enum.IsDefined(p.Horizon) || !Enum.IsDefined(p.Style)))
                throw new InvalidDataException("기밀 투자 계획 데이터 손상");
        }
    }
}
