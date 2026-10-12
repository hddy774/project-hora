namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    static void ValidateMarket(GameState s)
    {
        var environment = s.Environment ?? throw new InvalidDataException("시장 환경 없음");
        if (environment.Rules is null) throw new InvalidDataException("시장 규칙 없음");
        environment.Rules.Validate();
        if (!double.IsFinite(environment.EconomicActivity) || environment.EconomicActivity is < .5 or > 2 ||
            !double.IsFinite(environment.Inflation) || environment.Inflation is < -.1 or > .5 || environment.NumberFormat is < 0 or > 1 ||
            environment.NextEventMinute < 0 || environment.EventsPublished < 0 ||
            s.CompletedMinutes < 0 || s.CompletedMinutes / 60 != s.CompletedHours || !double.IsFinite(s.MinuteProgress) ||
            s.MinuteProgress is < 0 or >= 1 || s.PendingClockMinutes < 0 ||
            s.MinuteActors.Count > 900 || s.MinuteActors.Any(id => id < 1 || id > AiCount + s.Retail.Count) ||
            s.Minute == 0 && s.MinuteActors.Count != 0 || s.Minute != 0 && s.MinuteActors.Count < AiCount ||
            s.Stocks.Count != StockCount || s.Stocks.GroupBy(x => x.Sector).Count() != MarketDefaults.SectorCount ||
            s.Stocks.GroupBy(x => x.Sector).Any(g => g.Count() != 3) ||
            !s.Stocks.Select(x => x.Symbol).Order().SequenceEqual(CompanyCatalog.Companies.Select(x => x.Symbol).Order()))
            throw new InvalidDataException("시간·시장 환경·15종목 구성 손상");
        var activity = s.Activity ?? throw new InvalidDataException("거래 활동 기록 없음");
        if (activity.SubmittedOrders < 0 || activity.AggressiveOrders < 0 || activity.AggressiveOrders > activity.SubmittedOrders ||
            activity.CancelledOrders < 0 || activity.CancelledOrders > activity.SubmittedOrders ||
            activity.InstitutionMatches < 0 || activity.RetailMatches < 0 || activity.CrossGroupMatches < 0 ||
            (decimal)activity.InstitutionMatches + activity.RetailMatches + activity.CrossGroupMatches > s.TotalMatches ||
            activity.CashToInstitutions < 0 || activity.CashToRetail < 0 ||
            (decimal)activity.CashToInstitutions + activity.CashToRetail != activity.CrossGroupTurnover ||
            activity.CrossGroupTurnover > s.MatchedTurnover || s.News.Count > 80 ||
            s.News.Any(n => !Enum.IsDefined(n.Kind) || n.AbsoluteMinute < 0 || n.AbsoluteMinute > s.CompletedMinutes ||
                n.StockIndex < 0 || n.StockIndex >= StockCount || !double.IsFinite(n.Impact) || Math.Abs(n.Impact) > .2))
            throw new InvalidDataException("거래 수급·이벤트 기록 손상");
        foreach (var t in s.Bots)
        {
            var d = t.Development!;
            if (d.AnalystReports is null || d.Plans is null || d.AnalystReports.Count > MarketDefaults.SectorCount || d.Plans.Count > StockCount ||
                d.NextExecutionMinute < 0 || d.PlanRevision < 0 || d.CampaignIndex < -1 || d.CampaignIndex >= StockCount ||
                !Enum.IsDefined(d.CampaignPhase) || d.CampaignSinceMinute < 0 || d.CampaignSinceMinute > s.CompletedMinutes ||
                d.CampaignIndex >= 0 && d.CampaignReferencePrice < 1 || d.PeakEquity < 0 || d.AggressiveOrders < 0 ||
                !double.IsFinite(d.MaximumDrawdown) || d.MaximumDrawdown < 0 ||
                d.LastDecisionMinute < -1 || d.LastDecisionMinute > s.CompletedMinutes ||
                d.AnalystReports.Any(r => r.OwnerId != t.Id || r.WrittenMinute < 0 || r.WrittenMinute > s.CompletedMinutes || r.ExpiresMinute < r.WrittenMinute ||
                    !double.IsFinite(r.Confidence) || r.Confidence is < 0 or > 1 || !double.IsFinite(r.ExpectedReturn) || r.ExpectedReturn is < -1 or > 2) ||
                d.Plans.Select(p => p.SecurityId).Distinct().Count() != d.Plans.Count || d.Plans.Any(p => p.CreatedMinute < 0 || p.CreatedMinute > s.CompletedMinutes ||
                    p.DurationMinutes < 60 || p.TargetMinute != p.CreatedMinute + p.DurationMinutes || p.LowerPrice < 1 || p.UpperPrice < p.LowerPrice || p.UpperPrice > 10_000_000 ||
                    p.ReviewMinute < p.CreatedMinute || p.ReviewMinute > s.CompletedMinutes + 30 || p.StopPrice < 1 || p.TakeProfitPrice < p.StopPrice || p.LossBudget < 0 ||
                    !double.IsFinite(p.MaximumWeight) || p.MaximumWeight is < 0 or > 1 ||
                    !double.IsFinite(p.Confidence) || p.Confidence is < 0 or > 1 || !double.IsFinite(p.ExpectedReturn) ||
                    !double.IsFinite(p.EstimatedCost) || p.EstimatedCost < 0 ||
                    !double.IsFinite(p.TargetWeight) || p.TargetWeight < 0 || p.TargetWeight > p.MaximumWeight + 1e-9 || !Enum.IsDefined(p.Horizon) || !Enum.IsDefined(p.Style)))
                throw new InvalidDataException("투자 계획 데이터 손상");
        }
    }
}
