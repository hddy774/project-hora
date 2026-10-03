using Android.Graphics;
using AlphaExchange.Core;
using AColor = Android.Graphics.Color;

namespace AlphaExchange.App;

public sealed partial class GameView
{
    void DrawLobby()
    {
        if (arena is not null)
        {
            paint.Color = AColor.White; paint.SetShader(null);
            float artSize = Math.Min(440, h - 200);
            c.DrawBitmap(arena, null, new RectF((400 - artSize) / 2, 66, (400 + artSize) / 2, 66 + artSize), paint);
            using var fade = new LinearGradient(0, 66 + artSize * .66f, 0, 66 + artSize, AColor.Transparent, Bg, Shader.TileMode.Clamp!);
            paint.SetShader(fade); c.DrawRect(0, 66 + artSize * .66f, 400, 68 + artSize, paint); paint.SetShader(null);
        }
        Mark(25, 24, 22, Lime); Text("ALPHA EXCHANGE", 60, 42, 14, Ink, true);
        Pill("OFFLINE", 299, 23, Teal, 77);
        Text("100개의 AI.", 24, 104, 34, Ink, true, headline: true);
        Text("하나의 시장.", 24, 146, 34, Lime, true, headline: true);
        Text("THE AUTONOMOUS MARKET SIMULATOR", 26, 174, 10, Muted, true);
        float y = h - 294;
        Box(20, y, 360, 83, Card, 20, Stroke);
        string[] values = ["100", "120초", "1,000만"], labels = ["AI 투자자", "1일 · 1배속", "AI별 시작 자금"];
        for (int i = 0; i < 3; i++)
        {
            Text(values[i], 80 + i * 120, y + 35, i == 2 ? 21 : 26, i == 0 ? Lime : Ink, true, Paint.Align.Center);
            Text(labels[i], 80 + i * 120, y + 59, 11, Muted, false, Paint.Align.Center);
            if (i < 2) Line(140 + i * 120, y + 20, 140 + i * 120, y + 62, Stroke);
        }
        Text("매시간 거래하는 AI, 스스로 움직이는 시장.", 200, h - 188, 12, Muted, false, Paint.Align.Center);
        if (game is not null)
        {
            Button(S.Finished ? "지난 시즌 결과 보기  →" : $"DAY {S.Day:00}  ·  관찰 이어하기  →", 20, h - 166, 360, 52, () => { lobby = false; auto = !S.Finished; lastTick = Now; if (S.Finished) showResult = true; });
            Button("새 시뮬레이션 시작", 20, h - 103, 360, 45, RequestNew, false);
        }
        else
        {
            Button("시뮬레이션 시작  →", 20, h - 166, 360, 52, Start);
            Button("100 AI · 6가지 투자 전략", 20, h - 103, 360, 45, () => help = true, false);
        }
        Text("시뮬레이션 안내", 200, h - 28, 12, Muted, false, Paint.Align.Center);
        Hit(110, h - 53, 180, 47, () => help = true);
    }

    void DrawHeader()
    {
        Mark(23, 20, 20, Lime);
        Text("ALPHA", 57, 29, 15, Ink, true, headline: true);
        Text("EXCHANGE", 58, 44, 9, Muted, true);
        Hit(14, 7, 151, 45, () => { lobby = true; auto = false; Save(); });
        Pill("100 AI · OFFLINE", 207, 20, Teal, 128);
        Circle(360, 32, 15, Card2); Text("?", 360, 37, 15, Ink, true, Paint.Align.Center);
        Hit(338, 9, 43, 45, () => { help = true; });
        Text(S.Finished ? "시즌 종료" : $"DAY {S.Day:00}  /  30", 21, 77, 18, Ink, true);
        Text(S.Finished ? "COMPLETE" : $"{S.Hour:00}:{Math.Min(59, (int)(S.HourProgress * 60)):00}", 379, 77, S.Finished ? 15 : 20, Lime, true, Paint.Align.Right);
        Text(auto ? "진행 중" : "일시정지", 234, 76, 11, auto ? Teal : Muted);
        Box(20, 90, 360, 3, Card2, 2);
        Box(20, 90, S.Finished ? 360 : 360f * (float)((S.Hour + S.HourProgress) / 24), 3, Lime, 2);
    }

    void DrawFooter()
    {
        Box(0, h - 146, 400, 146, Bg, 0);
        Line(20, h - 146, 380, h - 146, Stroke);
        Text("1시간 = 5초", 21, h - 116, 10, Muted);
        Text("1일 = 120초 · 1×", 21, h - 96, 10, Muted);
        Button($"{speed}×", 122, h - 132, 46, 49, () => { lastTick = Now; speed = speed == 1 ? 2 : speed == 2 ? 5 : 1; }, false, !S.Finished);
        Button(S.Finished ? "시즌 결과 보기  →" : auto ? "Ⅱ   일시정지" : "▷   시뮬레이션 재개", 176, h - 132, 204, 49, ToggleSimulation);
        string[] labels = ["시장", "관찰 자산", "AI 리그", "뉴스"];
        for (int i = 0; i < 4; i++)
        {
            int p = i; AColor color = page == i ? Lime : Muted;
            if (page == i) Box(i * 100 + 29, h - 73, 42, 3, Lime, 2);
            NavIcon(i, i * 100 + 39, h - 59, color);
            Text(labels[i], i * 100 + 50, h - 17, 10, color, page == i, Paint.Align.Center);
            Hit(i * 100, h - 73, 100, 73, () => SetPage(p));
        }
    }

    float DrawMarket(float y)
    {
        var focus = Focus; long equity = focus.Equity(S.Stocks); double ret = focus.Return(S.Stocks);
        Box(20, y, 360, 166, Card, 22, Stroke);
        Text($"{focus.Name}의 총 자산", 38, y + 30, 12, Muted);
        Pill($"{game!.RankOf(focus.Id)}위 / 100", 278, y + 15, Lime, 84);
        Text($"₩{Money(equity)}", 37, y + 72, 32, Ink, true, headline: true);
        Text(Percent(ret), 39, y + 99, 14, Direction(ret), true);
        Text("시즌 누적 수익률", 122, y + 99, 10, Muted);
        Chart(focus.EquityHistory.Select(v => (double)v), 39, y + 114, 205, 28, Teal);
        Text("보유 현금", 358, y + 124, 10, Muted, false, Paint.Align.Right);
        Text($"₩{ShortMoney(focus.Cash)}", 358, y + 145, 15, Ink, true, Paint.Align.Right);
        y += 183;
        Text("MARKET PULSE", 22, y + 11, 10, Lime, true);
        Text("8개 가상 종목", 379, y + 11, 10, Muted, false, Paint.Align.Right);
        y += 27;
        var news = S.News.First();
        Box(20, y, 360, 52, Card2, 13);
        Circle(38, y + 26, 3, news.Impact > 0 ? Teal : Red);
        TextFit(news.Headline, 50, y + 23, 11, Ink, 298);
        Text($"DAY {news.Day:00}  ·  뉴스 보기  →", 50, y + 40, 9, Muted);
        Hit(20, y, 360, 52, () => SetPage(3));
        y += 76;
        Text("오늘의 시장", 21, y, 19, Ink, true);
        Text("종목별 흐름 관찰", 379, y, 11, Muted, false, Paint.Align.Right);
        y += 16;
        for (int i = 0; i < S.Stocks.Count; i++)
        {
            int index = i; var s = S.Stocks[i];
            Box(20, y, 360, 80, Card, 16);
            AColor accent = Hex(Palette[i]);
            Box(32, y + 17, 43, 43, new AColor((int)accent.R, accent.G, accent.B, 24), 13);
            Text(s.Symbol[..1], 53.5f, y + 46, 22, accent, true, Paint.Align.Center);
            Text(s.Name, 87, y + 30, 13, Ink, true);
            Text(s.Symbol, 87, y + 50, 10, Muted);
            if (focus.Shares[i] > 0) Text($"{focus.Shares[i]}주 보유", 87, y + 67, 9, Lime);
            Chart(s.History.TakeLast(16).Select(v => (double)v), 207, y + 23, 57, 31, Direction(s.Change));
            Text(Money(s.Price), 365, y + 34, 16, Ink, true, Paint.Align.Right);
            Text(Percent(s.Change), 365, y + 56, 11, Direction(s.Change), true, Paint.Align.Right);
            Hit(20, y, 360, 80, () => { selectedStock = index; });
            y += 89;
        }
        Text("등락률은 당일 기준 · 뉴스와 AI 수급이 가격에 반영됩니다.", 200, y + 17, 10, Muted, false, Paint.Align.Center);
        return y + 35;
    }

    float DrawPortfolio(float y)
    {
        var trader = Focus;
        {
            Robot(trader.Id, 21, y, 44); Text(trader.Name, 78, y + 19, 17, Ink, true);
            Text("AI 리그에서 다른 투자자를 선택할 수 있어요", 78, y + 38, 10, Muted);
            y += 64;
        }
        Text("AI 포트폴리오", 20, y + 20, 24, Ink, true);
        Text("투자의 흐름을 한눈에", 21, y + 43, 12, Muted); y += 62;
        Box(20, y, 360, 189, Card, 20, Stroke);
        Text("총 평가 자산", 38, y + 28, 12, Muted);
        Text($"₩{Money(trader.Equity(S.Stocks))}", 38, y + 67, 29, Ink, true);
        Text(Percent(trader.Return(S.Stocks)), 362, y + 28, 14, Direction(trader.Return(S.Stocks)), true, Paint.Align.Right);
        Chart(trader.EquityHistory.Select(v => (double)v), 40, y + 86, 320, 69, Teal, true);
        Text("START", 40, y + 175, 9, Muted); Text($"DAY {S.Day:00}", 360, y + 175, 9, Muted, false, Paint.Align.Right);
        y += 201;
        Metric("보유 현금", $"₩{ShortMoney(trader.Cash)}", 20, y, 174);
        Metric("누적 거래 수수료", $"₩{Money(trader.Fees)}", 206, y, 174);
        y += 82;
        long equity = trader.Equity(S.Stocks);
        float cashWidth = 360f * trader.Cash / equity;
        Box(20, y, 360, 9, Card2, 4); Box(20, y, cashWidth, 9, Lime, 4);
        Text($"현금  {(double)trader.Cash / equity:P0}", 20, y + 28, 11, Lime);
        Text($"주식  {1 - (double)trader.Cash / equity:P0}", 380, y + 28, 11, Muted, false, Paint.Align.Right);
        y += 60;
        Text("보유 종목", 20, y, 18, Ink, true); y += 17;
        bool any = false;
        for (int i = 0; i < 8; i++)
        {
            if (trader.Shares[i] == 0) continue;
            any = true; int index = i; var s = S.Stocks[i];
            double profit = (s.Price / trader.AverageCost[i] - 1);
            Box(20, y, 360, 79, Card, 15);
            Text(s.Name, 36, y + 26, 14, Ink, true);
            Text($"{trader.Shares[i]}주  ·  평균 {Money((long)trader.AverageCost[i])}원", 36, y + 49, 11, Muted);
            Text($"₩{Money((long)s.Price * trader.Shares[i])}", 364, y + 28, 15, Ink, true, Paint.Align.Right);
            Text(Percent(profit), 364, y + 52, 12, Direction(profit), true, Paint.Align.Right);
            Hit(20, y, 360, 79, () => { selectedStock = index; });
            y += 89;
        }
        if (!any)
        {
            Box(20, y, 360, 118, Card, 16);
            Text("아직 보유한 주식이 없어요", 200, y + 42, 15, Ink, true, Paint.Align.Center);
            Text("시뮬레이션이 진행되면 AI가 거래합니다.", 200, y + 69, 12, Muted, false, Paint.Align.Center);
            Hit(20, y, 360, 118, () => SetPage(0)); y += 135;
        }
        y += 13; Text("최근 거래", 20, y, 18, Ink, true); y += 19;
        var trades = S.Tape.Where(t => t.TraderId == trader.Id).Take(20).ToList();
        if (trades.Count == 0) { Text("거래 내역이 여기에 기록됩니다.", 22, y + 22, 12, Muted); y += 56; }
        foreach (var t in trades)
        {
            Pill(t.Buy ? "매수" : "매도", 22, y + 8, t.Buy ? Teal : Red, 42);
            Text($"{S.Stocks[t.StockIndex].Name}  {t.Quantity}주", 77, y + 25, 12, Ink);
            Text($"DAY {t.Day:00} · {t.Hour:00}:00", 77, y + 42, 9, Muted);
            Text($"₩{Money((long)t.Quantity * t.Price)}", 377, y + 26, 12, Ink, true, Paint.Align.Right);
            Line(22, y + 57, 378, y + 57, Stroke); y += 65;
        }
        return y + 10;
    }

    void Metric(string label, string value, float x, float y, float w)
    { Box(x, y, w, 70, Card, 16); Text(label, x + 16, y + 25, 11, Muted); Text(value, x + 16, y + 51, 19, Ink, true); }
}
