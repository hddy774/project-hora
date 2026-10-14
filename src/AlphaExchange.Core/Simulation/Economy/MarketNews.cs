namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    void PublishNews()
    {
        var environment = State.Environment;
        var kind = (MarketEventKind)(1 + (int)(Next() * 3));
        int index = (int)(Next() * StockCount);
        double impact = (Next() < .5 ? -1 : 1) * (.015 + Next() * .055);
        ApplyMarketEvent(kind, index, impact);
        environment.NextEventMinute = State.CompletedMinutes + 90 + (int)(Next() * 271);
    }

    void PublishCompanyNews()
    {
        for (int index = 0; index < StockCount; index++)
        {
            var stock = State.Stocks[index];
            if (!stock.Active || stock.NextCompanyNewsMinute > State.CompletedMinutes) continue;
            double impact = (Next() < .5 ? -1 : 1) * (.01 + Next() * .055);
            ApplyMarketEvent(MarketEventKind.Company, index, impact);
            stock.CompanyNewsCount++;
            stock.NextCompanyNewsMinute = State.CompletedMinutes + 720 + (int)(Next() * 721);
        }
    }

    internal void ApplyMarketEvent(MarketEventKind kind, int index, double impact)
    {
        if (!Enum.IsDefined(kind) || index < 0 || index >= StockCount || !double.IsFinite(impact) || Math.Abs(impact) > .2)
            throw new ArgumentOutOfRangeException(nameof(impact));
        var stock = State.Stocks[index];
        var environment = State.Environment;
        bool positive = impact >= 0;
        string headline;
        string detail;
        if (kind == MarketEventKind.Company)
        {
            stock.CompanyStrategy = (CompanyStrategy)(int)(Next() * 3);
            stock.Management = (ManagementPolicy)(int)(Next() * 3);
            stock.Allocation = (CapitalAllocation)(int)(Next() * 3);
            stock.BusinessSalesFactor = Math.Clamp(stock.BusinessSalesFactor * (1 + impact), .6, 1.5);
            headline = $"{stock.Name} · {(positive ? "수주 확대·실적 기대" : "납품 차질·비용 부담")}";
            detail = $"{stock.Driver} · {stock.NewsExposure} · {CompanyStrategyNames[(int)stock.CompanyStrategy]} / {ManagementNames[(int)stock.Management]} / {AllocationNames[(int)stock.Allocation]}";
        }
        else if (kind == MarketEventKind.Policy)
        {
            RefreshEconomy();
            environment.EconomicActivity = Math.Clamp(environment.EconomicActivity * (1 + impact * .4), .7, 1.35);
            headline = $"정책 발표 · {State.Government.Policy.Name}";
            detail = $"거래세 {State.Government.Policy.TaxRate:P1} · 수수료 {State.Government.Policy.FeeBasisPoints}bp · {(positive ? "투자 심리 개선" : "투자 심리 위축")}";
        }
        else if (kind == MarketEventKind.Finance)
        {
            State.Government.Policy = CheckpointCopy.Scalar(State.Government.Policy);
            State.Government.Policy.Season = State.Season;
            Append(State.Government.History, State.Government.Policy, 12);
            State.Government.Policy.BaseRate = Math.Clamp(State.Government.Policy.BaseRate - impact * .08, .005, .09);
            environment.Inflation = Math.Clamp(environment.Inflation - impact * .1, -.02, .12);
            headline = positive ? "금융 여건 완화 · 조달 비용 하락" : "금융 여건 긴축 · 조달 비용 상승";
            detail = $"기준금리 {State.Government.Policy.BaseRate:P2} · 물가 {environment.Inflation:P2} · 대출·이자 비용에 반영";
        }
        else
        {
            headline = $"{stock.Sector} · {(positive ? "수요 확대 전망" : "수요 둔화 전망")}";
            detail = "같은 업종의 공개 뉴스와 실적 기대에 반영";
        }
        for (int i = 0; i < StockCount; i++)
        {
            var target = State.Stocks[i];
            if (kind == MarketEventKind.Company && i != index || kind == MarketEventKind.Sector && target.Sector != stock.Sector) continue;
            var exposure = CompanyEconomics.For(target.Symbol);
            double strength = impact * (kind switch
            {
                MarketEventKind.Policy => exposure.Policy * .65,
                MarketEventKind.Finance => exposure.Financing * .65,
                MarketEventKind.Sector => exposure.Demand,
                _ => 1
            });
            if (kind == MarketEventKind.Sector)
                target.BusinessSalesFactor = Math.Clamp(target.BusinessSalesFactor * (1 + strength * .25), .6, 1.5);
            target.Sentiment = Math.Clamp(target.Sentiment + strength, -.2, .2);
            ApplyNewsImpact(i, strength);
        }
        State.News.Insert(0, new MarketEvent
        {
            Kind = kind, AbsoluteMinute = State.CompletedMinutes, Season = State.Season, Day = State.Day,
            Hour = State.Hour, Minute = State.Minute, StockIndex = index, SecurityId = stock.SecurityId,
            Headline = headline, Detail = detail + " · 회사별 수요·정책·금리·원가 민감도 반영 · 주가는 주문 체결로 결정됩니다.", Impact = impact
        });
        if (State.News.Count > 80) State.News.RemoveRange(80, State.News.Count - 80);
        environment.EventsPublished++;
        cachedStats = null;
    }
}
