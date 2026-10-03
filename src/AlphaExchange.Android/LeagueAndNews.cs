using Android.Graphics;
using AlphaExchange.Core;
using AColor = Android.Graphics.Color;

namespace AlphaExchange.App;

public sealed partial class GameView
{
    float DrawLeague(float y)
    {
        Text("100개의 투자 지능", 20, y + 23, 24, Ink, true);
        Text($"6가지 전략  ·  누적 {Money(S.TotalAiTrades)}건의 AI 거래", 21, y + 47, 12, Muted);
        y += 66;
        var ranking = game!.Ranking();
        var leaders = ranking.Where(t => t.Id > 0).Take(3).ToArray();
        for (int i = 0; i < leaders.Length; i++)
        {
            var bot = leaders[i]; float x = 20 + i * 123;
            Box(x, y + (i == 0 ? 0 : 9), 114, 145 - (i == 0 ? 0 : 9), Card, 18, i == 0 ? Lime : Stroke);
            Text($"0{i + 1}", x + 13, y + 25, 10, i == 0 ? Lime : Muted, true);
            Robot(bot.Id, x + 34, y + 29, 46);
            Text(bot.Name, x + 57, y + 96, 12, Ink, true, Paint.Align.Center);
            Text(Percent(bot.Return(S.Stocks)), x + 57, y + 121, 13, Direction(bot.Return(S.Stocks)), true, Paint.Align.Center);
            int id = bot.Id; Hit(x, y, 114, 145, () => { selectedTrader = id; });
        }
        y += 161;
        var focus = Focus;
        Box(20, y, 360, 67, Card2, 16, Stroke);
        Text($"#{game.RankOf(focus.Id):00}", 36, y + 39, 20, Lime, true);
        Text($"관찰 중 · {focus.Name}", 103, y + 27, 13, Ink, true);
        Text($"₩{Money(focus.Equity(S.Stocks))}", 103, y + 47, 11, Muted);
        Text(Percent(focus.Return(S.Stocks)), 362, y + 39, 13, Direction(focus.Return(S.Stocks)), true, Paint.Align.Right);
        Hit(20, y, 360, 67, () => SetPage(1));
        y += 85;
        string[] filters = ["전체 100", "모멘텀", "가치", "역추세", "뉴스", "분산", "탐험가"];
        for (int i = 0; i < 7; i++)
        {
            int filter = i - 1; float x = 20 + i % 4 * 92, fy = y + i / 4 * 37;
            bool active = leagueFilter == filter;
            Box(x, fy, 84, 30, active ? Lime : Card, 9);
            Text(filters[i], x + 42, fy + 20, 11, active ? Bg : Muted, active, Paint.Align.Center);
            Hit(x, fy, 84, 31, () => { leagueFilter = filter; });
        }
        y += 85;
        Text("RANK / TRADER", 22, y + 8, 9, Muted, true);
        Text("TOTAL ASSETS / RETURN", 378, y + 8, 9, Muted, true, Paint.Align.Right);
        y += 19;
        foreach (var (bot, rank) in ranking.Select((bot, i) => (bot, i + 1)))
        {
            if (bot.Id == 0 || (leagueFilter >= 0 && (int)bot.Strategy != leagueFilter)) continue;
            if (y + 82 >= clipTop && y <= clipBottom)
            {
                bool follow = S.FollowedId == bot.Id;
                Box(20, y, 360, 77, follow ? Card2 : Card, 14, follow ? Teal : null);
                Text($"{rank:00}", 34, y + 43, 12, rank < 4 ? Lime : Muted, true);
                Robot(bot.Id, 59, y + 17, 41);
                Text(bot.Name, 111, y + 29, 12, Ink, true);
                Text(GameEngine.StrategyNames[(int)bot.Strategy], 111, y + 48, 10, Hex(Palette[(int)bot.Strategy]));
                TextFit(bot.LastAction, 111, y + 65, 9, Muted, 136);
                Text($"₩{ShortMoney(bot.Equity(S.Stocks))}", 363, y + 29, 13, Ink, true, Paint.Align.Right);
                Text(Percent(bot.Return(S.Stocks)), 363, y + 50, 11, Direction(bot.Return(S.Stocks)), true, Paint.Align.Right);
                int id = bot.Id; Hit(20, y, 360, 77, () => { selectedTrader = id; });
            }
            y += 85;
        }
        Text("모든 AI는 같은 초기 자금과 공개 정보로 경쟁합니다.", 200, y + 16, 10, Muted, false, Paint.Align.Center);
        return y + 34;
    }

    float DrawNews(float y)
    {
        Text("시장의 시그널", 20, y + 23, 24, Ink, true);
        Text("6시간마다 도착하는 뉴스와 AI의 반응.", 21, y + 47, 12, Muted); y += 67;
        int up = S.Stocks.Count(s => s.Change >= 0);
        Metric("오늘의 상승 / 하락", $"{up}  /  {8 - up}", 20, y, 174);
        Metric("AI 누적 체결", $"{Money(S.TotalAiTrades)}건", 206, y, 174); y += 88;
        Box(20, y, 360, 56, Card2, 13);
        Text("100% 오프라인 가상 시장", 36, y + 23, 12, Lime, true);
        Text("뉴스는 게임 내 사건이며 실제 기업과 관련이 없습니다.", 36, y + 42, 10, Muted); y += 82;
        Text("MARKET WIRE", 21, y, 10, Muted, true); y += 18;
        foreach (var news in S.News)
        {
            if (y + 152 < clipTop || y > clipBottom) { y += 163; continue; }
            Box(20, y, 360, 152, Card, 17);
            Pill(news.Impact > 0 ? "긍정 신호" : "주의 신호", 35, y + 14, news.Impact > 0 ? Teal : Red, 70);
            Text($"D{news.Day:00} {news.Hour:00}:00 · {S.Stocks[news.StockIndex].Symbol}", 363, y + 30, 10, Muted, false, Paint.Align.Right);
            float after = Wrap(news.Headline, 36, y + 62, 328, 14, Ink, 22);
            Text("투자 심리에 영향을 주는 새로운 소식입니다.", 36, Math.Max(after + 4, y + 112), 11, Muted);
            Text("종목 살펴보기  ↗", 362, y + 136, 10, news.Impact > 0 ? Teal : Red, true, Paint.Align.Right);
            int index = news.StockIndex;
            Hit(20, y, 360, 152, () => { selectedStock = index; });
            y += 163;
        }
        return y + 10;
    }

    float Modal(float desiredHeight, Action close)
    {
        targets.Clear(); clipTop = 0; clipBottom = h;
        Box(0, 0, 400, h, new AColor(0, 0, 0, 190), 0);
        Hit(0, 0, 400, h, close);
        float top = Math.Max(12, h - desiredHeight);
        Box(8, top, 384, h - top + 24, Bg, 26, Stroke);
        Hit(8, top, 384, h - top, () => { });
        Box(179, top + 9, 42, 4, Stroke, 2);
        Circle(360, top + 34, 15, Card2);
        Text("×", 360, top + 40, 22, Muted, false, Paint.Align.Center);
        Hit(336, top + 15, 46, 42, close);
        return top;
    }

    void DrawStockSheet()
    {
        int index = selectedStock; var s = S.Stocks[index];
        float y = Modal(660, () => selectedStock = -1);
        Text(s.Symbol + "  /  " + s.Sector, 27, y + 37, 11, Hex(Palette[index]), true);
        Text(s.Name, 27, y + 67, 23, Ink, true);
        Text($"₩{Money(s.Price)}", 27, y + 108, 34, Ink, true, headline: true);
        Text(Percent(s.Change), 374, y + 106, 15, Direction(s.Change), true, Paint.Align.Right);
        for (int i = 0; i < 3; i++) Line(28, y + 137 + i * 40, 372, y + 137 + i * 40, Stroke);
        Chart(s.History.Select(v => (double)v), 29, y + 133, 341, 99, Hex(Palette[index]), true);
        Text("시간별 가격 · 이전 15시간 + 이번 시즌", 29, y + 253, 9, Muted);
        Text($"AI 보유 {S.Bots.Count(b => b.Shares[index] > 0)}명", 371, y + 253, 10, Muted, false, Paint.Align.Right);
        Line(27, y + 267, 373, y + 267, Stroke);
        var trader = Focus;
        Text($"{trader.Name}  ·  {trader.Shares[index]}주 보유", 28, y + 290, 12, Ink);
        Text($"체결 {Money(s.Volume)}주 / 시간", 373, y + 290, 11, Muted, false, Paint.Align.Right);
        Box(26, y + 310, 348, 61, Card, 15);
        Text("매수 심리", 41, y + 336, 11, Muted);
        Text(s.Sentiment > .01 ? "긍정적" : s.Sentiment < -.01 ? "신중함" : "중립적", 139, y + 336, 14, s.Sentiment >= 0 ? Teal : Red, true);
        Text("거래 수수료 0.15%", 356, y + 336, 11, Muted, false, Paint.Align.Right);
        Text("공개 정보와 투자 성향에 따라 각 AI가 직접 판단합니다.", 41, y + 356, 10, Muted);
        Text("실시간 AI 체결", 28, y + 401, 15, Ink, true);
        var recent = S.Tape.Where(t => t.StockIndex == index).Take(4).ToArray();
        float ty = y + 417;
        foreach (var trade in recent)
        {
            var bot = S.Bots[trade.TraderId - 1];
            Pill(trade.Buy ? "매수" : "매도", 28, ty, trade.Buy ? Teal : Red, 42);
            Text(bot.Name, 83, ty + 16, 12, Ink);
            Text($"{trade.Quantity}주 · {Money(trade.Price)}원", 371, ty + 16, 11, Muted, false, Paint.Align.Right);
            ty += 34;
        }
        if (recent.Length == 0) Text("첫 거래를 기다리고 있습니다.", 29, ty + 25, 12, Muted);
        Button("시장으로 돌아가기", 27, y + 575, 346, 51, () => selectedStock = -1);
        Text("플레이어 주문 없이 AI만 거래하는 가상 시장", 200, y + 646, 10, Muted, false, Paint.Align.Center);
    }

    void DrawTraderSheet()
    {
        var bot = S.Bots.First(t => t.Id == selectedTrader);
        float y = Modal(610, () => selectedTrader = -1);
        Robot(bot.Id, 28, y + 31, 65);
        Text(bot.Name, 109, y + 57, 22, Ink, true);
        Text(GameEngine.StrategyNames[(int)bot.Strategy] + $"  ·  #{game!.RankOf(bot.Id):00}", 111, y + 82, 12, Hex(Palette[(int)bot.Strategy]));
        Wrap(GameEngine.StrategyDescriptions[(int)bot.Strategy], 30, y + 129, 337, 13, Muted, 22);
        Box(27, y + 177, 346, 116, Card, 18);
        Text("총 평가 자산", 43, y + 203, 11, Muted);
        Text($"₩{Money(bot.Equity(S.Stocks))}", 43, y + 239, 28, Ink, true);
        Text(Percent(bot.Return(S.Stocks)), 43, y + 271, 15, Direction(bot.Return(S.Stocks)), true);
        Text($"거래 {bot.Trades}회", 355, y + 271, 12, Muted, false, Paint.Align.Right);
        Text("위험 선호", 28, y + 321, 11, Muted);
        Box(101, y + 312, 190, 7, Card2, 4); Box(101, y + 312, (float)bot.Risk * 190, 7, Hex(Palette[(int)bot.Strategy]), 4);
        Text(bot.Risk > .66 ? "공격적" : bot.Risk < .4 ? "보수적" : "중립적", 372, y + 321, 11, Ink, true, Paint.Align.Right);
        Text("주요 보유 종목", 28, y + 354, 14, Ink, true);
        var positions = Enumerable.Range(0, 8).Where(i => bot.Shares[i] > 0).OrderByDescending(i => (long)bot.Shares[i] * S.Stocks[i].Price).Take(3).ToArray();
        float py = y + 381;
        if (positions.Length == 0) Text("아직 거래 전입니다. AI의 첫 거래를 기다려 보세요.", 28, py + 15, 11, Muted);
        foreach (int i in positions)
        { Text(S.Stocks[i].Name, 29, py, 12, Ink); Text($"{bot.Shares[i]}주", 371, py, 12, Muted, false, Paint.Align.Right); py += 30; }
        TextFit($"최근 활동  ·  {bot.LastAction}", 29, y + 495, 12, Muted, 343);
        Button("이 AI의 포트폴리오 관찰  →", 27, y + 526, 346, 52, () =>
        {
            S.FollowedId = bot.Id; SetPage(1); Save();
            selectedTrader = -1;
        });
    }

    void DrawResults()
    {
        float y = Modal(640, () => showResult = false);
        var ranking = game!.Ranking(); var winner = ranking[0]; var focus = Focus;
        Text("SEASON COMPLETE", 200, y + 47, 11, Lime, true, Paint.Align.Center);
        Text("100 AI, 하나의 우승자", 200, y + 83, 23, Ink, true, Paint.Align.Center);
        Robot(winner.Id, 160, y + 107, 80);
        Text(winner.Name + " · 리그 우승", 200, y + 218, 18, Ink, true, Paint.Align.Center);
        Text(Percent(winner.Return(S.Stocks)), 200, y + 252, 29, Lime, true, Paint.Align.Center);
        Box(27, y + 278, 346, 114, Card, 18);
        Text($"{focus.Name} 최종 순위", 44, y + 308, 12, Muted);
        Text($"{game.RankOf(focus.Id)} / {ranking.Count}", 355, y + 308, 20, Ink, true, Paint.Align.Right);
        Text("최종 자산", 44, y + 339, 12, Muted);
        Text($"₩{Money(focus.Equity(S.Stocks))}", 355, y + 339, 18, Ink, true, Paint.Align.Right);
        Text("관찰 AI 수익률", 44, y + 372, 12, Muted);
        Text(Percent(focus.Return(S.Stocks)), 355, y + 372, 17, Direction(focus.Return(S.Stocks)), true, Paint.Align.Right);
        Text($"AI 100명  ·  {Money(S.TotalAiTrades)}건의 거래  ·  30일", 200, y + 425, 12, Muted, false, Paint.Align.Center);
        Button("최종 순위 살펴보기", 27, y + 457, 346, 50, () => { showResult = false; SetPage(2); }, false);
        Button("새 시즌 시작하기  →", 27, y + 521, 346, 52, () => Start());
        Text("매 시즌 새로운 뉴스와 시장이 기다립니다.", 200, y + 609, 11, Muted, false, Paint.Align.Center);
    }

    void DrawHelp()
    {
        float y = Modal(654, () => help = false);
        Text("스스로 움직이는 주식 시장", 27, y + 60, 24, Ink, true);
        Text("HOW IT WORKS", 28, y + 86, 11, Lime, true);
        string[] titles = ["01  100명의 독립적인 투자자", "02  매시간 이뤄지는 거래", "03  현실 120초 = 게임 1일", "04  30일 후 최고의 수익률"];
        string[] body = [
            "100명의 AI가 각각 1,000만 원으로 시작합니다. 플레이어는 참여하지 않고 시장을 관찰합니다.",
            "AI들은 6가지 전략으로 8개 종목을 매매합니다. 뉴스와 수급이 주가를 움직이고 수수료는 0.15%입니다.",
            "1배속에서 실제 5초마다 게임 시간 1시간이 지납니다. 24시간, 즉 실제 120초가 게임의 하루입니다.",
            "현금과 주식 평가액으로 순위를 정합니다. 30일 시즌은 1배속 기준 60분이며 결과를 확인할 수 있습니다."
        ];
        float row = y + 123;
        for (int i = 0; i < titles.Length; i++)
        { Text(titles[i], 28, row, 15, Ink, true); row = Wrap(body[i], 28, row + 27, 340, 12, Muted, 20) + 22; }
        Box(27, y + 493, 346, 66, Card, 14);
        Text("자유로운 관찰 · 자동 저장", 41, y + 516, 12, Teal, true);
        Text("1×·2×·5× 배속 지원 · 앱을 벗어나면 자동 일시정지", 41, y + 542, 10, Muted);
        Button("준비됐어요", 27, y + 579, 346, 50, () => help = false);
    }

    void DrawConfirmation()
    {
        float y = Modal(286, () => confirmNew = false);
        Text("새 시즌을 시작할까요?", 27, y + 62, 23, Ink, true);
        Wrap("진행 중인 시즌이 새 시즌으로 교체됩니다. 이어하기를 선택하면 기존 경기를 계속할 수 있어요.", 28, y + 99, 339, 13, Muted, 23);
        Button("계속 관찰", 27, y + 204, 166, 51, () => confirmNew = false, false);
        Button("새 시즌 시작", 207, y + 204, 166, 51, () => Start());
    }
}
