namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    void PublishNews()
    {
        if (State.World is not null && World.People.Count == 200)
        {
            if (State.CompletedHours == 0) Activity(World.GovernorId, World.LeaderId, "", "market-open", "시장안정은행 시장 개장 보고 · 실제 보유 주식으로 양방향 호가 공급");
            else if (State.Hour % 24 == 6)
            {
                int companyIndex = (int)(Next() * Math.Min(30, State.Stocks.Count)); var company = State.Stocks[companyIndex]; if (!company.Active) return;
                Activity(101 + companyIndex, 0, company.SecurityId, "management-briefing", $"{company.Name} 대표 공개 브리핑 · {CompanyStrategyNames[(int)company.CompanyStrategy]} 경영 방향 확인");
            }
            return;
        }
        int i = (int)(Next() * State.Stocks.Count); var s = State.Stocks[i]; bool up = Next() >= .5;
        double impact = (up ? 1 : -1) * (.02 + Next() * .05); s.Sentiment = Math.Clamp(s.Sentiment + impact, -.12, .12);
        if (!s.Active) return;
        string headline = up ? $"{s.Business} 수요·계약 개선" : $"{s.Business} 수요 둔화·비용 상승";
        ApplyNewsImpact(i, impact);
        State.News.Insert(0, new MarketEvent
        {
            Season = State.Season,
            Day = State.Day,
            Hour = State.Hour,
            StockIndex = i,
            SecurityId = s.SecurityId,
            Headline = $"{s.Name}, {headline}",
            Impact = impact,
            Detail = "기관 판단과 이번 시즌 기업 매출·비용에 반영됩니다. 월말 결산에서 새 재무제표가 발표됩니다."
        });
        if (State.News.Count > 40) State.News.RemoveRange(40, State.News.Count - 40);
    }
}
