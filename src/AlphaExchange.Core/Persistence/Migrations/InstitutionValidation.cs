namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    static void ValidateDevelopment(GameState s)
    {
        if (s.Version is not (9 or 10) || s.Rules is null) throw new InvalidDataException("기관 실행 규칙이 없습니다.");
        s.Rules.Validate();
        if (s.PendingClockHours != s.PendingClockMinutes / 60) throw new InvalidDataException("시계 대기 시간 손상");
        if (s.Retail.Any(t => t.Development is not null)) throw new InvalidDataException("개인 기관 능력 손상");
        int count = s.Stocks.Count;
        foreach (var t in s.Bots)
        {
            var d = t.Development;
            if (d is not null && (d.SuccessfulInvestments < 0 || d.CompletedInvestments < d.SuccessfulInvestments ||
                d.OutcomesSinceMinute < 0 || d.OutcomesSinceMinute > s.CompletedMinutes || d.TurnoverDay < -1 ||
                d.TurnoverDay > s.CompletedHours / 24 || d.DayTurnover < 0 ||
                d.LastExitMinutes is null || d.LastExitMinutes.Length != count || d.LastExitMinutes.Any(x => x < -1 || x > s.CompletedMinutes) ||
                d.InvestmentNet is null || d.ShortInvestmentNet is null || d.TrackedInvestments is null || d.TrackedShortInvestments is null ||
                d.InvestmentNet.Length != count || d.ShortInvestmentNet.Length != count || d.TrackedInvestments.Length != count || d.TrackedShortInvestments.Length != count))
                throw new InvalidDataException("투자 성과 기록 손상");
            if (d is null || d.PointsEarned < 0 || d.PointsSpent < 0 || d.AvailablePoints < 0 || d.PointsEarned - d.PointsSpent != d.AvailablePoints ||
                d.AllocatedPoints is null || d.AllocatedPoints.Length != 10 || d.AllocatedPoints.Any(x => x is < 0 or > 100) || d.AllocatedPoints.Sum() != (long)d.PointsSpent ||
                d.CreditBonus < 0 || d.CreditBonus > s.Rules.Representative.MaximumCreditBonus || d.LastRewardSeason < 0 || d.LastRewardSeason >= s.Season ||
                d.LastRewardRank is < 0 or > AiCount || d.LastRewardPoints is < 0 or > 100 ||
                d.LastDecisionHour is < -1 || d.LastDecisionHour > s.CompletedHours ||
                d.NextDecisionHour < 0 || d.NextDecisionHour > s.CompletedHours + 720 || d.PortfolioReviews < 0 ||
                !double.IsFinite(d.TargetCashRatio) || d.TargetCashRatio < s.Rules.Investment.MinimumCash || d.TargetCashRatio > s.Rules.Investment.MaximumCash ||
                d.PositionOpenedHours is null || d.ShortOpenedHours is null || d.NextReviewHours is null || d.PositionHorizons is null || d.LastNewsSignals is null ||
                d.PositionOpenedHours.Length != count || d.ShortOpenedHours.Length != count || d.NextReviewHours.Length != count || d.PositionHorizons.Length != count || d.LastNewsSignals.Length != count ||
                d.LastNewsSignals.Any(x => !double.IsFinite(x)) ||
                d.PositionOpenedHours.Any(x => x is < -1 || x > s.CompletedHours) || d.ShortOpenedHours.Any(x => x is < -1 || x > s.CompletedHours) ||
                d.NextReviewHours.Any(x => x < 0 || x > s.CompletedHours + 720) || d.PositionHorizons.Any(x => !Enum.IsDefined(x)))
                throw new InvalidDataException("투자자 성장·투자 기간 손상");
        }
        foreach (var season in s.PendingSeasons)
        {
            if (season.GrowthRewards is null || season.GrowthRewards.Count > AiCount || season.GrowthRewards.Select(x => x.TraderId).Distinct().Count() != season.GrowthRewards.Count ||
                season.GrowthRewards.Any(x => x.TraderId is < 1 or > AiCount || x.Generation < 1 || x.Rank is < 1 or > AiCount || x.AbilityPoints is < 0 or > 100 ||
                    x.CreditBonus is < 0 or > 30 || x.AllocatedPoints is null || x.AllocatedPoints.Length != 10 || x.AllocatedPoints.Any(p => p < 0)))
                throw new InvalidDataException("시즌 성장 보상 기록 손상");
        }
    }
}
