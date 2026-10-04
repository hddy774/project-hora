using Android.Graphics;
using AlphaExchange.Core;

namespace AlphaExchange.App;
public sealed partial class GameView
{
    static long SectorTotal(string name,DailySnapshot snapshot,bool turnover)
    {
        var values=turnover ? snapshot.StockTurnovers : snapshot.StockVolumes;
        return values.Where((_,i)=>i<snapshot.StockSectors.Length && snapshot.StockSectors[i]==name).Sum();
    }
    long SectorActivity(string name,PeriodStatistics period,bool turnover) => SectorTotal(name,period.End,turnover)-SectorTotal(name,period.Start,turnover);
    static long SectorCap(string name,DailySnapshot snapshot)
    {
        decimal total=0;
        for(int i=0;i<snapshot.StockShares.Length;i++) if(i<snapshot.StockSectors.Length && snapshot.StockSectors[i]==name)
            total+=snapshot.StockShares[i]*(i<snapshot.MarkPrices.Length ? snapshot.MarkPrices[i] : snapshot.StockPrices[i]);
        return (long)decimal.Round(total);
    }
    double SectorChange(string name,PeriodStatistics period) => (double)SectorCap(name,period.End)/Math.Max(1,SectorCap(name,period.Start))-1;
    readonly Dictionary<(string Id,long Season),IReadOnlyList<CompanyReport>> companyReportCache=[];
    IReadOnlyList<CompanyReport> Reports(Stock stock,long season)
    {
        var key=(stock.SecurityId,season);
        if(companyReportCache.TryGetValue(key,out var cached)) return cached;
        if(companyReportCache.Count>=12) companyReportCache.Clear();
        var reports=store.ReadReports(S.RunId,stock.SecurityId,season).Concat(stock.Reports.Where(r=>r.Season<=season))
            .GroupBy(r=>(r.Season,r.AccountingBasis,r.IsOpening)).Select(g=>g.Last()).OrderBy(r=>r.Season).ThenBy(r=>r.IsOpening ? 0 : 1).ThenBy(r=>r.AccountingBasis).ToArray();
        companyReportCache[key]=reports; return reports;
    }
    float DrawCompanyFinancials(float y)
    {
        var stock = S.Stocks[companyStock];
        Text("기업의 재무 기록", 20, y + 23, 24, Ink, true);
        Text("월별 결산 · 뉴스와 정부 정책을 반영", 21, y + 47, 12, Muted); y += 65;
        for (int i = 0; i < S.Stocks.Count; i++)
        {
            int index = i; bool active = companyStock == i; float x = 20 + i % 5 * 73, row = y + i / 5 * 34;
            Box(x, row, 68, 28, active ? Lime : Card2, 8); Text(S.Stocks[i].Symbol, x + 34, row + 19, 10, active ? Bg : Muted, active, Paint.Align.Center);
            Hit(x, row, 68, 28, () => { companyStock = index; scroll = 0; });
        }
        y += (int)Math.Ceiling(S.Stocks.Count/5.0)*34+15;
        var reports = Reports(stock,companySeason==0 ? S.Season : companySeason);
        CompanyReport? report = reports.LastOrDefault(r=>companySeason==0 || r.Season==companySeason);
        Button("← 이전 결산", 20, y, 105, 36, () => { companySeason = Math.Max(1, (companySeason == 0 ? stock.Report.Season : companySeason) - 1); scroll = 0; }, false, (companySeason == 0 ? stock.Report.Season : companySeason) > 1);
        Text(companySeason == 0 ? "최근 발표" : $"시즌 {companySeason}", 200, y + 24, 13, Lime, true, Paint.Align.Center);
        Button("최신 결산 →", 275, y, 105, 36, () => { companySeason = 0; scroll = 0; }, false, companySeason != 0); y += 55;
        if (report is null) { Text("해당 시즌의 기업 결산 기록이 없습니다.", 21, y + 20, 12, Muted); return y + 70; }
        var r = report;
        var previous = Reports(stock,r.Season-1).LastOrDefault();
        var graphReports=reports.Where(x=>x.Season<=r.Season && x.AccountingBasis==r.AccountingBasis).ToArray();
        Text(stock.Name+(stock.Active ? "" : " · 합병 완료"),21,y+15,20,Ink,true);
        string reportLabel=r.IsOpening ? "기초·변경 재무 (수익 추정치)" : $"결산 S{r.Season}";
        Text($"{reportLabel} · 반영 뉴스 {r.NewsCount}건",21,y+40,10,Muted); y+=58;
        y=Wrap(stock.Business+" · "+stock.Driver,21,y+4,354,11,Muted,19)+8;
        y=Statement("주식과 기업 가치 · 현재 기준",y,[("시가총액",ShortMoney(stock.MarketCap)+"원"),("발행 / 자사주",$"{Money(stock.TotalShares)} / {Money(stock.TreasuryShares)}주"),
            ("상장 주식 (자사주 제외)",Money(stock.OutstandingShares)+"주"),("유동 주식 (창업자 제외)",Money(stock.FloatShares)+"주"),("액면가",Money(stock.ParValue)+"원"),
            ("최근 주당 배당",Money(stock.LastDividendPerShare)+"원"),("최근 12결산 배당수익률",stock.DividendYield.ToString("P2"))]);
        if(r.AccountingBasis<5) { y=Wrap("이전 버전 결산입니다. 배당은 실제 지급되지 않았던 보고 금액이며, v1.3 결산과 회계 기준이 다릅니다.",21,y+3,354,11,Red,20)+12; }
        string[] tabs = ["재무상태", "손익", "현금흐름", "자본변동", "재무비율"];
        for (int i = 0; i < tabs.Length; i++)
        {
            int tab = i; float x = 20 + i * 73; bool active = companyTab == i;
            Box(x, y, 68, 32, active ? Lime : Card2, 8); Text(tabs[i], x + 34, y + 21, 10, active ? Bg : Muted, active, Paint.Align.Center);
            Hit(x, y, 68, 32, () => { companyTab = tab; scroll = 0; });
        }
        y += 48;
        string Amount(long n) => Money(n) + "원";
        if (companyTab == 0)
        {
            y = Statement("01 · 재무상태표", y, [("현금", Amount(r.Cash)), ("매출채권", Amount(r.Receivables)), ("재고자산", Amount(r.Inventory)),
                ("유형자산", Amount(r.FixedAssets)), ("자산 총계", Amount(r.Assets)), ("부채", Amount(r.Debt)), ("자본", Amount(r.Equity))]);
            y = Pie("자산 구성", [("현금", r.Cash), ("매출채권", r.Receivables), ("재고", r.Inventory), ("유형자산", r.FixedAssets)], y);
            y = TimeGraph("시즌별 자산",graphReports.Select(x=>(x.Season*720,(double)((double)x.Assets),false)),y,"선택 결산까지 파일 전체 기록 · 같은 회계 기준 · 원");
        }
        else if (companyTab == 1)
        {
            y = Statement("02 · 손익계산서", y, [("매출", Amount(r.Revenue)), ("영업 비용", Amount(r.OperatingCosts)), ("감가상각",Amount(r.Depreciation)), ("영업 이익", Amount(r.OperatingProfit)),
                ("이자", Amount(r.Interest)), ("법인세", Amount(r.Tax)), ("당기 순이익", Amount(r.NetIncome))]);
            y = Pie("매출 배분 · 비용과 이익", [("영업 비용", r.OperatingCosts), ("이자", r.Interest), ("세금", r.Tax), ("순이익", r.NetIncome)], y);
            y = TimeGraph("시즌별 순이익",graphReports.Select(x=>(x.Season*720,(double)((double)x.NetIncome),false)),y,"선택 결산까지 파일 전체 기록 · 같은 회계 기준 · 원");
        }
        else if (companyTab == 2)
        {
            y = Statement("03 · 현금흐름표", y, [("기초 현금", Amount(r.OpeningCash)), ("영업 현금흐름", Amount(r.OperatingCashFlow)),
                ("투자 현금흐름", Amount(r.InvestingCashFlow)), ("재무 현금흐름", Amount(r.FinancingCashFlow)), ("순현금흐름", Amount(r.Cash - r.OpeningCash)), ("기말 현금", Amount(r.Cash))]);
            y = Pie("현금흐름 규모 · 절댓값", [("영업", Math.Abs(r.OperatingCashFlow)), ("투자 유출", Math.Abs(r.InvestingCashFlow)), ("재무 유출", Math.Abs(r.FinancingCashFlow))], y);
            y = TimeGraph("시즌별 영업 현금흐름",graphReports.Select(x=>(x.Season*720,(double)((double)x.OperatingCashFlow),false)),y,"선택 결산까지 파일 전체 기록 · 같은 회계 기준 · 원");
        }
        else if (companyTab == 3)
        {
            y = Statement("04 · 자본변동표", y, [("기초 자본", Amount(r.OpeningEquity)), ("당기 순이익", Amount(r.NetIncome)),
                ("배당 지급", Amount(-r.Dividends)), ("자본 거래", Amount(r.CapitalChange)), ("기말 자본", Amount(r.Equity))]);
            y = Pie("자본 변화 규모 · 절댓값", [("순이익", Math.Abs(r.NetIncome)), ("배당", r.Dividends), ("자본 거래", Math.Abs(r.CapitalChange))], y);
            y = TimeGraph("시즌별 자본",graphReports.Select(x=>(x.Season*720,(double)((double)x.Equity),false)),y,"선택 결산까지 파일 전체 기록 · 같은 회계 기준 · 원");
        }
        else
        {
            double eps=(double)r.NetIncome/Math.Max(1,r.AverageShares);
            double factor=stock.SplitFactor/Math.Max(1e-12,r.SplitFactor);
            var actual=reports.Where(x=>x.AccountingBasis==5 && !x.IsOpening && x.Season<=r.Season).TakeLast(12).ToArray();
            double annualEps=actual.Length==0 ? 0 : actual.Sum(x=>(double)x.NetIncome/Math.Max(1,x.AverageShares)/(stock.SplitFactor/x.SplitFactor))*12/actual.Length;
            double bps=(double)r.Equity/Math.Max(1,r.OutstandingShares)/factor;
            y = Statement("05 · 주요 재무비율",y,[("순이익률",r.Margin.ToString("P2")),("부채비율 (자산 대비)",r.DebtRatio.ToString("P2")),
                ("자기자본이익률",r.ReturnOnEquity.ToString("P2")),("결산 평균 주식 수",Money(r.AverageShares)+"주"),("월 EPS (분할 조정)",(eps/factor).ToString("N2")+"원"),
                (actual.Length<12 ? "연 환산 PER (추정)" : "12개월 PER",annualEps>0 ? ((double)stock.MarkPrice/annualEps).ToString("N2")+"배" : "적자 / 기록 부족"),
                ("BPS (분할 조정)",bps.ToString("N2")+"원"),("PBR",bps>0 ? ((double)stock.MarkPrice/bps).ToString("N2")+"배" : "자본 잠식")]);
            y = Pie("자본과 부채 비중", [("자본", r.Equity), ("부채", r.Debt)], y);
            y = TimeGraph("시즌별 순이익률",graphReports.Select(x=>(x.Season*720,(double)(x.Margin * 100),false)),y,"선택 결산까지 파일 전체 기록 · 같은 회계 기준 · %");
        }
        if (previous is not null && previous.AccountingBasis==r.AccountingBasis && !r.IsOpening)
            y = Statement("직전 결산 대비", y, [("매출", Percent((double)r.Revenue / Math.Max(1, previous.Revenue) - 1)),
                ("순이익 증감", Amount(r.NetIncome - previous.NetIncome)), ("자산 증감", Amount(r.Assets - previous.Assets)), ("자본 증감", Amount(r.Equity - previous.Equity))]);
        Text("배당·분할·합병 기록",21,y+16,18,Ink,true); y+=37;
        y=CorporateCards(y,store.ReadEvents(S,stock.SecurityId,maximum:6));
        Button("시장으로 돌아가기", 20, y, 360, 42, () => SetPage(0), false);
        return y + 65;
    }
}
