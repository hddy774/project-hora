using Android.Graphics;
using AlphaExchange.Core;

namespace AlphaExchange.App;
public sealed partial class GameView
{
    int statsTab;
    bool retailAverage;
    int statsChart;
    float DrawStatistics(float y)
    {
        var stats = game!.Statistics();
        Text("시장의 전체 그림", 20, y + 23, 24, Ink, true);
        Text($"100 투자사 · 개미 {S.Retail.Count:N0}명 · 업종별 3개", 21, y + 47, 11, Muted); y += 66;
        string[] tabs = ["전체 시장", "업종별", "개미 재무", "기관 재무", "정책·금융", "호가·수급", "위험·전략", "투자 성과"];
        for (int i = 0; i < tabs.Length; i++)
        {
            int tab = i; bool active = statsTab == i;
            Box(20 + i % 4 * 92, y + i / 4 * 41, 84, 34, active ? Lime : Card2, 10);
            Text(tabs[i], 62 + i % 4 * 92, y + i / 4 * 41 + 23, 10, active ? Bg : Muted, active, Paint.Align.Center);
            Hit(20 + i % 4 * 92, y + i / 4 * 41, 84, 34, () => { statsTab = tab; scroll = 0; });
        }
        y += 89;
        y = NumberFormatPicker(y);
        Text($"S{S.Season} D{S.Day} {S.Hour:00}:{S.Minute:00} · 현황 → 분포 → 상세", 21, y + 12, 10, Muted); y += 30;
        if (statsTab == 7) return DrawInvestmentResults(y);
        if (statsTab == 5) return DrawMicrostructure(y);
        if (statsTab == 6) return DrawMarketRisk(y);
        if (statsTab == 4) return DrawEconomy(y);
        if (statsTab == 3)
        {
            y = Graph("100 투자사의 누적 순자산", S.DailyHistory.Select(d => (double)d.InstitutionEquity), y, "투자 자산·부채와 실제 거래 손익");
            y = Pie("기관 법인 자산 구성", [("현금", (double)stats.Institutions.Cash), ("주식 평가액", (double)stats.Institutions.Holdings)], y);
            return DrawFinancials(stats.Institutions, y, 1);
        }
        y = PeriodPicker(y);
        var period = game.PeriodSummary(comparisonPeriod);
        if (statsTab == 2)
        {
            Box(20, y, 360, 83, Card, 16);
            Text($"개미 {S.Retail.Count:N0}명", 36, y + 27, 17, Ink, true);
            Text($"이번 시간 참여 {S.ActiveRetailLastHour:N0}명 · 누적 체결 {stats.Retail.Trades:N0}회", 36, y + 51, 10, Muted);
            Text("시장·주가·공개 뉴스의 방향을 따라 거래", 36, y + 70, 10, Teal); y += 96;
            Button(retailAverage ? "표시: 1인 평균  ⇄  전체로 변경" : "표시: 개인 전체  ⇄  1인 평균으로 변경", 20, y, 360, 40, () => retailAverage = !retailAverage, false); y += 59;
            Text(retailAverage ? "개미 1인 평균 · 원" : "개미 전체 합계 · 원", 21, y, 11, Muted);
            return DrawFinancials(stats.Retail, y + 15, retailAverage ? S.Retail.Count : 1);
        }
        if (statsTab == 1)
        {
            y = HorizontalBars("업종별 기간 등락률", stats.Sectors.Select(s => (s.Name, SectorChange(s.Name, period))).ToArray(), y, "%", true);
            y = HorizontalBars("업종별 시가총액 · 현재", stats.Sectors.Select(s => (s.Name, (double)s.Capitalization)).ToArray(), y, "원");
            foreach (var sector in stats.Sectors)
            {
                Box(20, y, 360, 177, Card, 17);
                Text(sector.Name, 36, y + 29, 18, Ink, true);
                Text(Percent(SectorChange(sector.Name, period)), 364, y + 29, 15, Direction(sector.Change), true, Paint.Align.Right);
                Text($"{sector.Companies}개 기업 · 시가총액 {ShortMoney(sector.Capitalization)}원", 36, y + 55, 11, Muted);
                Text("기간 거래량 / 거래대금", 36, y + 86, 11, Muted);
                Text($"{Money(SectorActivity(sector.Name, period, false))}주 / {ShortMoney(SectorActivity(sector.Name, period, true))}원", 364, y + 109, 15, Ink, true, Paint.Align.Right);
                Text($"보유 주식: 기관 {Money(sector.InstitutionShares)} / 개인 {Money(sector.RetailShares)}", 36, y + 139, 10, Muted);
                float ratio = (float)((double)sector.InstitutionShares / Math.Max(1, sector.InstitutionShares + sector.RetailShares));
                Box(36, y + 152, 328, 5, Hex("#C2AFFA"), 2); Box(36, y + 152, 328 * ratio, 5, Teal, 2); y += 191;
            }
            Text("등락·거래량은 기간 기준 · 구성·보유는 현재 기준입니다.", 21, y + 8, 10, Muted); return y + 30;
        }
        Box(20, y, 360, 117, Card, 18, Stroke);
        Text("전체 시가총액", 36, y + 27, 12, Muted);
        Text($"₩{ShortMoney(stats.Capitalization)}", 36, y + 66, 30, Ink, true);
        Text(Percent(period.Change), 364, y + 65, 17, Direction(period.Change), true, Paint.Align.Right);
        Text($"상승 {stats.Rising}  ·  하락 {stats.Falling}  ·  보합 {stats.Unchanged}", 36, y + 96, 11, Muted); y += 132;
        Metric("기간 거래량", $"{Money(period.Volume)}주", 20, y, 174);
        Metric("기간 거래대금", $"{ShortMoney(period.Turnover)}원", 206, y, 174); y += 84;
        bool cohortPeriod = period.Start.CohortTradingBasis == 6 && period.End.CohortTradingBasis == 6;
        string Income(double end, double start) => ShortMoney((long)(end - (cohortPeriod ? start : 0))) + "원";
        y = Statement(cohortPeriod ? "기간 투자 손익 · 임금/소비/재출자 제외" : "누적 투자 손익 · 이전 기간 기준 없음", y,
            [("기관",Income(period.End.InstitutionTradingIncome,period.Start.InstitutionTradingIncome)),
             ("개인",Income(period.End.RetailTradingIncome,period.Start.RetailTradingIncome))]);
        string[] charts = ["총수익 지수", "시가총액", "기관 투자 손익", "개인 투자 손익"];
        Button("그래프: " + charts[statsChart] + "  ⇄", 20, y, 360, 38, () => statsChart = (statsChart + 1) % charts.Length, false); y += 53;
        Func<DailySnapshot, double> metric = statsChart switch { 1 => d => d.Capitalization, 2 => d => d.InstitutionTradingIncome, 3 => d => d.RetailTradingIncome, _ => d => d.TotalReturnIndex };
        var chartPoints = ChartPeriodPoints();
        if (statsChart >= 2) chartPoints = chartPoints.Where(d => d.CohortTradingBasis == 6);
        y = TimeGraph(charts[statsChart], chartPoints, metric, y, statsChart >= 2 ? "임금·소비 제외 · 이전 세대 손실 포함 · 실제 기록부터 표시" : "기간 선택 · 실제 시간 축 · 파일의 원기록 보존");
        y = Pie("분야별 시가총액 구성", stats.Sectors.Select(s => (s.Name, (double)s.Capitalization)).ToArray(), y);
        y = Statement("기간 비교 · 시작 → 종료", y, [("시가총액", ShortMoney(period.Start.Capitalization) + " → " + ShortMoney(period.End.Capitalization)),
            ("기관 순자산", ShortMoney(period.Start.InstitutionEquity) + " → " + ShortMoney(period.End.InstitutionEquity)),
            ("개인 순자산", ShortMoney(period.Start.RetailEquity) + " → " + ShortMoney(period.End.RetailEquity)),
            ("기관 현금", ShortMoney(period.Start.InstitutionCash) + " → " + ShortMoney(period.End.InstitutionCash)),
            ("개인 현금", ShortMoney(period.Start.RetailCash) + " → " + ShortMoney(period.End.RetailCash))]);
        Button($"파산 기록 →  기업 {S.BankruptcyTotals.Companies} · 기관 {S.BankruptcyTotals.Institutions} · 개인 {S.BankruptcyTotals.Retail}", 20, y, 360, 42, () => SetPage(ScreenId.Bankruptcies), false); y += 58;
        return DrawStorage(y);
    }
}
