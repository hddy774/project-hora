using Android.Graphics;
using AlphaExchange.Core;

namespace AlphaExchange.App;
public sealed partial class GameView
{
    float DrawPortfolio(float y)
    {
        var t = Focus;
        Box(20, y, 360, 176, Card, 18, Stroke); Portrait(t.PortraitId, 30, y + 10, 99, 156); Hit(30, y + 10, 99, 156, () => portraitZoom = t.PortraitId);
        Text("INSTITUTION " + t.Id.ToString("000"), 146, y + 28, 10, Lime, true);
        Text(Representatives.Name(t.PortraitId), 146, y + 57, 24, Ink, true);
        Text(t.Name + " · " + GameEngine.DispositionNames[(int)t.Disposition], 146, y + 81, 11, Muted);
        Text($"₩{Money(t.Equity(S.Stocks))}", 146, y + 116, 23, Ink, true);
        Text(Percent(t.Return(S.Stocks)), 147, y + 144, 15, Direction(t.Return(S.Stocks)), true);
        Text($"시즌 {S.Season} · {FrameRankOf(t.Id)}위", 364, y + 165, 10, Muted, false, Paint.Align.Right);
        y += 188;
        y = NumberFormatPicker(y);
        string[] tabs = ["보유 자산", "재무제표", "시즌 성적", "계획·전략"];
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
            Text("시뮬레이션 시작부터의 누적 재무제표 · 원", 21, y + 2, 10, Muted);
            y = DrawFinancials(t.Financials(S.Stocks), y + 16, 1);
            if (t.LegacyFees > 0) { Text($"이전 버전 수수료 기록  ₩{Money(t.LegacyFees)}", 22, y + 16, 11, Muted); y += 40; }
            return DrawPortfolioRisk(t, y);
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
}
