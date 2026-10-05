using Android.Graphics;
using AlphaExchange.Core;

namespace AlphaExchange.App;
public sealed partial class GameView
{
    float DrawPortfolio(float y)
    {
        var t = Focus;
        Box(20, y, 360, 176, Card, 18, Stroke); Portrait(t.PortraitId, 30, y + 10, 99, 156); Hit(30,y+10,99,156,()=>portraitZoom=t.PortraitId);
        Text("INSTITUTION " + t.Id.ToString("000"), 146, y + 28, 10, Lime, true);
        Text(Representatives.Name(t.PortraitId), 146, y + 57, 24, Ink, true);
        Text(t.Name + " · " + GameEngine.DispositionNames[(int)t.Disposition], 146, y + 81, 11, Muted);
        Text($"₩{Money(t.Equity(S.Stocks))}", 146, y + 116, 23, Ink, true);
        Text(Percent(t.Return(S.Stocks)), 147, y + 144, 15, Direction(t.Return(S.Stocks)), true);
        Text($"시즌 {S.Season} · {FrameRankOf(t.Id)}위", 364, y + 165, 10, Muted, false, Paint.Align.Right);
        y += 188;
        string[] tabs = ["보유 자산", "재무제표", "시즌 성적", "대표·은행"];
        for (int i = 0; i < tabs.Length; i++)
        {
            int tab = i; bool active = portfolioTab == i;
            Box(20 + i * 92, y, 84, 34, active ? Lime : Card2, 10);
            Text(tabs[i], 62 + i * 92, y + 23, 12, active ? Bg : Muted, active, Paint.Align.Center);
            Hit(20 + i * 92, y, 84, 34, () => { portfolioTab = tab; scroll = 0; });
        }
        y += 55;
        if (portfolioTab == 1)
        {
            Text(S.MigratedFromV1 ? "v1.1 이전 시점부터의 누적 재무제표 · 원" : "시뮬레이션 시작부터의 누적 재무제표 · 원", 21, y + 2, 10, Muted);
            y = DrawFinancials(t.Financials(S.Stocks), y + 16, 1);
            if (t.LegacyFees > 0) { Text($"이전 버전 수수료 기록  ₩{Money(t.LegacyFees)}", 22, y + 16, 11, Muted); y += 40; }
            return y;
        }
        if (portfolioTab == 2) return DrawPersonalHistory(y, t);
        if (portfolioTab == 3) return DrawRepresentative(y, t);
        Metric("현금 / 주문 예약", $"{ShortMoney(t.Cash)} / {ShortMoney(t.ReservedCash)}", 20, y, 174);
        Metric("누적 체결 참여", $"{Money(t.Trades)}회", 206, y, 174); y += 89;
        Box(20, y, 360, 106, Card, 16);
        Text("최근 120시간 자산 추이", 36, y + 24, 11, Muted);
        Chart(t.EquityHistory.Select(x => (double)x), 36, y + 40, 328, 48, Teal, true); y += 130;
        y = Pie("현금과 종목별 자산 구성", S.Stocks.Select((stock, i) => (stock.Symbol, (double)stock.Value(t.Shares[i]))).Prepend(("현금", (double)t.Cash)).ToArray(), y);
        if (t.ShortShares.Any(q => q > 0))
        {
            y = Statement("공매도 · 담보 / 부채", y, [("필요 담보", Money(t.ShortCollateral(S.Stocks))), ("공매도 부채", Money(t.ShortLiability(S.Stocks))), ("누적 대여료", Money(t.BorrowFees))]);
            foreach (var (stock, i) in S.Stocks.Select((stock, i) => (stock, i)).Where(x => t.ShortShares[x.i] > 0))
            { Text($"{stock.Symbol} 공매도 {t.ShortShares[i]}주 · 평균 {Money((long)t.ShortAveragePrice[i])}원", 22, y + 14, 12, Red); y += 35; }
        }
        Text("보유 종목", 21, y, 18, Ink, true); y += 18;
        for (int i = 0; i < S.Stocks.Count; i++)
        {
            if (t.Shares[i] == 0) continue;
            int index = i; var s = S.Stocks[i]; double gain = s.Price / Math.Max(1, t.AverageCost[i]) - 1;
            Box(20, y, 360, 77, Card, 14);
            Text(s.Name, 35, y + 26, 13, Ink, true);
            Text($"{t.Shares[i]}주 · 매도 예약 {t.ReservedShares[i]}주", 35, y + 47, 10, Muted);
            Text($"평균 {Money((long)t.AverageCost[i])}원", 35, y + 64, 10, Muted);
            Text($"₩{Money(s.Value(t.Shares[i]))}", 364, y + 29, 15, Ink, true, Paint.Align.Right);
            Text(Percent(gain), 364, y + 53, 11, Direction(gain), true, Paint.Align.Right);
            Hit(20, y, 360, 77, () => selectedStock = index); y += 86;
        }
        Text("최근 체결", 21, y + 19, 18, Ink, true); y += 40;
        var tape = S.Tape.Where(x => x.BuyerId == t.Id || x.SellerId == t.Id).Take(12).ToArray();
        if (tape.Length == 0) { Text("최근 시장 체결 160건 중 참여 내역이 없습니다.", 22, y + 20, 11, Muted); y += 50; }
        foreach (var trade in tape)
        {
            bool buy = trade.BuyerId == t.Id;
            string action = buy ? trade.ShortCover ? "상환" : "매수" : trade.ShortSale ? "공매도" : "매도";
            Pill(action, 22, y, buy ? Teal : Red, 44);
            Text($"{S.Stocks[trade.StockIndex].Symbol} · {trade.Quantity}주", 81, y + 17, 12, Ink);
            Text($"{Money(trade.Price)}원", 375, y + 17, 12, Ink, true, Paint.Align.Right);
            Text($"S{trade.Season} D{trade.Day:00} {trade.Hour:00}:{trade.Minute:00} · 상대 {((buy ? trade.SellerId : trade.BuyerId) <= 100 ? "기관" : "개인")}", 81, y + 35, 10, Muted);
            y += 51;
        }
        return y + 10;
    }
    float DrawFinancials(FinancialStatement f, float y, int divisor)
    {
        string Amount(double amount) => divisor == 1 ? ShortMoney((long)Math.Round(amount)) : (amount / divisor).ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
        y = Statement("재무상태표", y, [ ("현금 및 예금", Amount(f.Cash)), ("주식 평가액", Amount(f.Holdings)), ("자산 총계", Amount(f.Assets)), ("공매도 부채", Amount(f.ShortDebt)), ("대출 잔액", Amount(f.LoanDebt)), ("미납 벌금", Amount(f.FineDebt)), ("공매도 미납 배당",Amount(f.ShortDividendDebt)), ("부채 총계", Amount(f.Liabilities)), ("자본 총계", Amount(f.Equity)) ]);
        y = Statement("손익계산서", y, [ ("실현 매매손익", Amount(f.RealizedProfit)), ("평가손익 변동", Amount(f.ValuationChange)), ("거래 수수료", "−" + Amount(f.Fees)), ("세금", "−" + Amount(f.Taxes)), ("이자", "−" + Amount(f.InterestExpense)), ("공매도 대여료", "−" + Amount(f.BorrowFees)), ("벌금", "−" + Amount(f.Fines)), ("주식 보조금", Amount(f.Subsidies)), ("배당 / 배당세",$"{Amount(f.DividendIncome)} / {Amount(f.DividendTax)}"), ("공매도 배당 비용",Amount(f.ShortDividendExpense)), ("임금 / 소비",$"{Amount(f.WageIncome)} / {Amount(f.Consumption)}"), ("직원 급여·채용 비용", "−"+Amount(f.StaffCosts)), ("누적 순손익", Amount(f.NetIncome)) ]);
        y = Statement("현금흐름표", y, [ ("기초 현금", Amount(f.OpeningCash)), ("주식 매도 유입", Amount(f.Sales)), ("주식 매수 유출", "−" + Amount(f.Purchases)), ("수수료 유출", "−" + Amount(f.Fees)), ("차입 / 상환", $"{Amount(f.Borrowed)} / {Amount(f.Repaid)}"), ("세금·이자·대여료·벌금 납부", "−" + Amount(f.Taxes + f.InterestPaid + f.BorrowFees + f.FinesPaid)), ("보조금", Amount(f.Subsidies)), ("배당 유입 (세후)",Amount(f.DividendIncome-f.DividendTax)), ("공매도 배당 납부",Amount(f.ShortDividendPaid)), ("임금 / 소비",$"{Amount(f.WageIncome)} / {Amount(f.Consumption)}"), ("직원 급여·채용 유출", "−"+Amount(f.StaffCosts)), ("순현금흐름", Amount(f.NetCashFlow)), ("기말 현금", Amount(f.Cash)) ]);
        Text("자산 = 부채 + 자본 · 수수료는 손익에 한 번 반영", 21, y + 10, 10, Muted);
        Text("수익률은 임금·소비 유출입을 제외한 투자 성과입니다.", 21, y + 30, 10, Muted);
        return y + 50;
    }
    float Statement(string title, float y, (string label, string value)[] rows)
    {
        if(!Visible(y,58+rows.Length*32)) return y+74+rows.Length*32;
        Box(20, y, 360, 58 + rows.Length * 32, Card, 17);
        Text(title, 36, y + 31, 16, Ink, true); Line(36, y + 44, 364, y + 44, Stroke);
        for (int i = 0; i < rows.Length; i++)
        {
            bool last = i == rows.Length - 1; float baseline = y + 69 + i * 32;
            Text(rows[i].label, 36, baseline, 12, last ? Ink : Muted, last);
            Text(rows[i].value, 364, baseline, 13, last ? Lime : Ink, last, Paint.Align.Right);
        }
        return y + 74 + rows.Length * 32;
    }
    int statsTab;
    bool retailAverage;
    int statsChart;
    float DrawStatistics(float y)
    {
        var stats = game!.Statistics();
        Text("시장의 전체 그림", 20, y + 23, 24, Ink, true);
        Text($"100 투자사 · 개미 {S.Retail.Count:N0}집단 · 분야별 5개", 21, y + 47, 11, Muted); y += 66;
        string[] tabs = ["전체 시장", "분야별", "개미 재무", "기관 재무", "정부·은행"];
        for (int i = 0; i < tabs.Length; i++)
        {
            int tab = i; bool active = statsTab == i;
            Box(20 + i * 73, y, 68, 34, active ? Lime : Card2, 10);
            Text(tabs[i], 54 + i * 73, y + 23, 10, active ? Bg : Muted, active, Paint.Align.Center);
            Hit(20 + i * 73, y, 68, 34, () => { statsTab = tab; scroll = 0; });
        }
        y += 49;
        if (statsTab == 4) return DrawEconomy(y);
        if (statsTab == 3)
        {
            y=Graph("100 투자사의 누적 순자산",S.DailyHistory.Select(d=>(double)d.InstitutionEquity),y,"임금·개인 지갑과 분리된 투자 법인 재무");
            y=Pie("기관 법인 자산 구성",[("현금",(double)stats.Institutions.Cash),("주식 평가액",(double)stats.Institutions.Holdings)],y);
            return DrawFinancials(stats.Institutions,y,1);
        }
        y = PeriodPicker(y);
        var period = game.PeriodSummary(comparisonPeriod);
        if (statsTab == 2)
        {
            Box(20, y, 360, 83, Card, 16);
            Text($"개미 {S.Retail.Count:N0}집단 · {S.Retail.Count*(long)S.World!.RetailUnitPeople:N0}명", 36, y + 27, 17, Ink, true);
            Text($"이번 시간 참여 {S.ActiveRetailLastHour:N0}명 · 누적 체결 {stats.Retail.Trades:N0}회", 36, y + 51, 10, Muted);
            Text($"집단 1개 = {S.World!.RetailUnitPeople:N0}명 · 시장 반응을 따라 거래", 36, y + 70, 10, Teal); y += 96;
            Button(retailAverage ? "표시: 1인 평균  ⇄  전체로 변경" : "표시: 개인 전체  ⇄  1인 평균으로 변경", 20, y, 360, 40, () => retailAverage = !retailAverage, false); y += 59;
            Text(retailAverage ? "개미 집단당 평균 · 원" : "개미 전체 합계 · 원", 21, y, 11, Muted);
            return DrawFinancials(stats.Retail, y + 15, retailAverage ? S.Retail.Count : 1);
        }
        if (statsTab == 1)
        {
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
        bool cohortPeriod=period.Start.CohortTradingBasis==6 && period.End.CohortTradingBasis==6;
        string Income(double end,double start)=>ShortMoney((long)(end-(cohortPeriod ? start : 0)))+"원";
        y=Statement(cohortPeriod ? "기간 투자 손익 · 임금/소비/재출자 제외" : "누적 투자 손익 · 이전 기간 기준 없음",y,
            [("기관",Income(period.End.InstitutionTradingIncome,period.Start.InstitutionTradingIncome)),
             ("개인",Income(period.End.RetailTradingIncome,period.Start.RetailTradingIncome))]);
        string[] charts=["총수익 지수", "시가총액", "기관 투자 손익", "개인 투자 손익"];
        Button("그래프: "+charts[statsChart]+"  ⇄",20,y,360,38,()=>statsChart=(statsChart+1)%charts.Length,false); y+=53;
        Func<DailySnapshot,double> metric=statsChart switch { 1=>d=>d.Capitalization,2=>d=>d.InstitutionTradingIncome,3=>d=>d.RetailTradingIncome,_=>d=>d.TotalReturnIndex };
        var chartPoints=ChartPeriodPoints();
        if(statsChart>=2) chartPoints=chartPoints.Where(d=>d.CohortTradingBasis==6);
        y=TimeGraph(charts[statsChart],chartPoints,metric,y,statsChart>=2 ? "임금·소비 제외 · 이전 세대 손실 포함 · 실제 기록부터 표시" : "기간 선택 · 실제 시간 축 · 파일의 원기록 보존");
        y = Pie("분야별 시가총액 구성", stats.Sectors.Select(s => (s.Name, (double)s.Capitalization)).ToArray(), y);
        y = Statement("기간 비교 · 시작 → 종료", y, [("시가총액", ShortMoney(period.Start.Capitalization) + " → " + ShortMoney(period.End.Capitalization)),
            ("기관 순자산", ShortMoney(period.Start.InstitutionEquity) + " → " + ShortMoney(period.End.InstitutionEquity)),
            ("개인 순자산", ShortMoney(period.Start.RetailEquity) + " → " + ShortMoney(period.End.RetailEquity)),
            ("기관 현금", ShortMoney(period.Start.InstitutionCash) + " → " + ShortMoney(period.End.InstitutionCash)),
            ("개인 현금", ShortMoney(period.Start.RetailCash) + " → " + ShortMoney(period.End.RetailCash))]);
        Button($"파산 기록 →  기업 {S.BankruptcyTotals.Companies} · 기관 {S.BankruptcyTotals.Institutions} · 개인 {S.BankruptcyTotals.Retail}",20,y,360,42,()=>SetPage(9),false); y+=58;
        return DrawStorage(y);
    }
}
