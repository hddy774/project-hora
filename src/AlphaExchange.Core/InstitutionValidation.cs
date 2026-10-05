namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    static void ValidateDevelopment(GameState s)
    {
        if(s.Version is not (7 or 8) || s.Rules is null) throw new InvalidDataException("기관 실행 규칙이 없습니다.");
        s.Rules.Validate();
        if(s.PendingClockHours<0 || s.Version<8 && s.PendingClockHours>s.Rules.Performance.MaximumCatchupHours) throw new InvalidDataException("시계 대기 시간 손상");
        if(s.Retail.Any(t=>t.Development is not null || t.StaffCosts!=0)) throw new InvalidDataException("개인 기관 능력 손상");
        int count=s.Stocks.Count;
        foreach(var t in s.Bots)
        {
            var d=t.Development;
            if(d is null || d.PointsEarned<0 || d.PointsSpent<0 || d.AvailablePoints<0 || d.PointsEarned-d.PointsSpent!=d.AvailablePoints ||
                d.AllocatedPoints is null || d.AllocatedPoints.Length!=10 || d.AllocatedPoints.Any(x=>x is <0 or >100) || d.AllocatedPoints.Sum()!=(long)d.PointsSpent ||
                d.LegacyAbilities is null || d.LegacyAbilities.Length is not (0 or 10) || d.LegacyAbilities.Any(x=>x is <0 or >100) ||
                d.CreditBonus<0 || d.CreditBonus>s.Rules.Representative.MaximumCreditBonus || d.LastRewardSeason<0 || d.LastRewardSeason>=s.Season ||
                d.LastRewardRank is <0 or >AiCount || d.LastRewardPoints is <0 or >100 || d.LastHireHour is <-1 || d.LastHireHour>s.CompletedHours ||
                d.LastPayrollHour<0 || d.LastPayrollHour>s.CompletedHours || d.LastDecisionHour is <-1 || d.LastDecisionHour>s.CompletedHours ||
                d.NextDecisionHour<0 || d.NextDecisionHour>s.CompletedHours+720 || d.PortfolioReviews<0 ||
                !double.IsFinite(d.TargetCashRatio) || d.TargetCashRatio<s.Rules.Investment.MinimumCash || d.TargetCashRatio>s.Rules.Investment.MaximumCash ||
                d.Employees is null || d.Employees.Count>50 || d.Employees.Any(c=>c is null || !Enum.IsDefined(c.Grade) || !Enum.IsDefined(c.Role) || c.Count<=0 || c.Count>s.Rules.Employees.MaximumCount) ||
                d.EmployeeCount>s.Rules.Employees.MaximumCount || d.Employees.Select(c=>(c.Grade,c.Role,c.Job,c.SectorSpecialty)).Distinct().Count()!=d.Employees.Count ||
                d.PositionOpenedHours is null || d.ShortOpenedHours is null || d.NextReviewHours is null || d.PositionHorizons is null || d.LastNewsSignals is null ||
                d.PositionOpenedHours.Length!=count || d.ShortOpenedHours.Length!=count || d.NextReviewHours.Length!=count || d.PositionHorizons.Length!=count || d.LastNewsSignals.Length!=count ||
                d.LastNewsSignals.Any(x=>!double.IsFinite(x)) ||
                d.PositionOpenedHours.Any(x=>x is <-1 || x>s.CompletedHours) || d.ShortOpenedHours.Any(x=>x is <-1 || x>s.CompletedHours) ||
                d.NextReviewHours.Any(x=>x<0 || x>s.CompletedHours+720) || d.PositionHorizons.Any(x=>!Enum.IsDefined(x)))
                throw new InvalidDataException("대표 성장·직원·투자 기간 손상");
        }
        foreach(var season in s.PendingSeasons)
        {
            if(season.GrowthRewards is null || season.GrowthRewards.Count>AiCount || season.GrowthRewards.Select(x=>x.TraderId).Distinct().Count()!=season.GrowthRewards.Count ||
                season.GrowthRewards.Any(x=>x.TraderId is <1 or >AiCount || x.Generation<1 || x.Rank is <1 or >AiCount || x.AbilityPoints is <0 or >100 ||
                    x.CreditBonus is <0 or >30 || x.AllocatedPoints is null || x.AllocatedPoints.Length!=10 || x.AllocatedPoints.Any(p=>p<0)))
                throw new InvalidDataException("시즌 성장 보상 기록 손상");
        }
    }
}
