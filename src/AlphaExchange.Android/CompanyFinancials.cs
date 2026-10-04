using Android.Graphics;
using AlphaExchange.Core;

namespace AlphaExchange.App;
public sealed partial class GameView
{
    long SectorActivity(string name, PeriodStatistics period, bool turnover)
    {
        long value = 0;
        var start = turnover ? period.Start.StockTurnovers : period.Start.StockVolumes;
        var end = turnover ? period.End.StockTurnovers : period.End.StockVolumes;
        for (int i = 0; i < S.Stocks.Count; i++) if (S.Stocks[i].Sector == name && i < start.Length && i < end.Length) value += end[i] - start[i];
        return value;
    }
    double SectorChange(string name, PeriodStatistics period)
    {
        long begin = 0, end = 0;
        for (int i = 0; i < S.Stocks.Count; i++)
            if (S.Stocks[i].Sector == name && i < period.Start.StockPrices.Length)
            { begin += (long)period.Start.StockPrices[i] * S.Stocks[i].TotalShares; end += (long)S.Stocks[i].Price * S.Stocks[i].TotalShares; }
        return (double)end / Math.Max(1, begin) - 1;
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
        y += 79;
        var reports = stock.Reports;
        CompanyReport? report = companySeason == 0 ? stock.Report : reports.LastOrDefault(r => r.Season == companySeason) ?? Season(companySeason)?.CompanyReports.ElementAtOrDefault(companyStock);
        Button("← 이전 결산", 20, y, 105, 36, () => { companySeason = Math.Max(1, (companySeason == 0 ? stock.Report.Season : companySeason) - 1); scroll = 0; }, false, (companySeason == 0 ? stock.Report.Season : companySeason) > 1);
        Text(companySeason == 0 ? "최근 발표" : $"시즌 {companySeason}", 200, y + 24, 13, Lime, true, Paint.Align.Center);
        Button("최신 결산 →", 275, y, 105, 36, () => { companySeason = 0; scroll = 0; }, false, companySeason != 0); y += 55;
        if (report is null) { Text("해당 시즌의 기업 결산 기록이 없습니다.", 21, y + 20, 12, Muted); return y + 70; }
        var r = report; var previous = reports.LastOrDefault(x => x.Season < r.Season)
            ?? (r.Season > 1 ? Season(r.Season - 1)?.CompanyReports.ElementAtOrDefault(companyStock) : null);
        var graphReports = reports.Where(x => x.Season <= r.Season).ToArray();
        if (graphReports.Length == 0) graphReports = [r];
        Text(stock.Name, 21, y + 15, 20, Ink, true);
        Text($"결산 S{r.Season} · 반영 뉴스 {r.NewsCount}건 · 다음 갱신 S{S.Season + 1} 시작", 21, y + 40, 10, Muted); y += 58;
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
            y = Graph("시즌별 자산", graphReports.Select(x => (double)x.Assets), y, "선택 결산까지 최대 12회 · 원");
        }
        else if (companyTab == 1)
        {
            y = Statement("02 · 손익계산서", y, [("매출", Amount(r.Revenue)), ("영업 비용", Amount(r.OperatingCosts)), ("영업 이익", Amount(r.OperatingProfit)),
                ("이자", Amount(r.Interest)), ("법인세", Amount(r.Tax)), ("당기 순이익", Amount(r.NetIncome))]);
            y = Pie("매출 배분 · 비용과 이익", [("영업 비용", r.OperatingCosts), ("이자", r.Interest), ("세금", r.Tax), ("순이익", r.NetIncome)], y);
            y = Graph("시즌별 순이익", graphReports.Select(x => (double)x.NetIncome), y, "선택 결산까지 최대 12회 · 원", true);
        }
        else if (companyTab == 2)
        {
            y = Statement("03 · 현금흐름표", y, [("기초 현금", Amount(r.OpeningCash)), ("영업 현금흐름", Amount(r.OperatingCashFlow)),
                ("투자 현금흐름", Amount(r.InvestingCashFlow)), ("재무 현금흐름", Amount(r.FinancingCashFlow)), ("순현금흐름", Amount(r.Cash - r.OpeningCash)), ("기말 현금", Amount(r.Cash))]);
            y = Pie("현금흐름 규모 · 절댓값", [("영업", Math.Abs(r.OperatingCashFlow)), ("투자 유출", Math.Abs(r.InvestingCashFlow)), ("재무 유출", Math.Abs(r.FinancingCashFlow))], y);
            y = Graph("시즌별 영업 현금흐름", graphReports.Select(x => (double)x.OperatingCashFlow), y, "선택 결산까지 최대 12회 · 원", true);
        }
        else if (companyTab == 3)
        {
            y = Statement("04 · 자본변동표", y, [("기초 자본", Amount(r.OpeningEquity)), ("당기 순이익", Amount(r.NetIncome)),
                ("배당 지급", Amount(-r.Dividends)), ("자본 거래", Amount(r.CapitalChange)), ("기말 자본", Amount(r.Equity))]);
            y = Pie("자본 변화 규모 · 절댓값", [("순이익", Math.Abs(r.NetIncome)), ("배당", r.Dividends), ("자본 거래", Math.Abs(r.CapitalChange))], y);
            y = Graph("시즌별 자본", graphReports.Select(x => (double)x.Equity), y, "선택 결산까지 최대 12회 · 원");
        }
        else
        {
            double eps = (double)r.NetIncome / Math.Max(1, stock.TotalShares);
            y = Statement("05 · 주요 재무비율", y, [("순이익률", r.Margin.ToString("P2")), ("부채비율 (자산 대비)", r.DebtRatio.ToString("P2")),
                ("자기자본이익률", r.ReturnOnEquity.ToString("P2")), ("월 주당 순이익", eps.ToString("N2") + "원"),
                ("연 환산 PER", eps > 0 ? (stock.Price / (eps * 12)).ToString("N2") + "배" : "적자 / 계산 불가"),
                ("PBR", ((double)stock.Price * stock.TotalShares / Math.Max(1, r.Equity)).ToString("N2") + "배")]);
            y = Pie("자본과 부채 비중", [("자본", r.Equity), ("부채", r.Debt)], y);
            y = Graph("시즌별 순이익률", graphReports.Select(x => x.Margin * 100), y, "선택 결산까지 최대 12회 · %");
        }
        if (previous is not null)
            y = Statement("직전 결산 대비", y, [("매출", Percent((double)r.Revenue / Math.Max(1, previous.Revenue) - 1)),
                ("순이익 증감", Amount(r.NetIncome - previous.NetIncome)), ("자산 증감", Amount(r.Assets - previous.Assets)), ("자본 증감", Amount(r.Equity - previous.Equity))]);
        Button("시장으로 돌아가기", 20, y, 360, 42, () => SetPage(0), false);
        return y + 65;
    }
}
