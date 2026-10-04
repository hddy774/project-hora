using Android.Graphics;
using AlphaExchange.Core;
using AColor = Android.Graphics.Color;

namespace AlphaExchange.App;

public sealed partial class GameView
{
    float DrawLeague(float y)
    {
        Text("100개 기관의 성과", 20, y + 23, 24, Ink, true);
        Text("10개 능력과 성향 · 다양한 투자 기법을 혼합", 21, y + 47, 12, Muted); y += 64;
        y = PeriodPicker(y);
        for (int i = 0; i < GameEngine.MetricNames.Length; i++)
        {
            var metric = (RankingMetric)i; bool active = rankingMetric == metric;
            float x = 20 + i % 3 * 123, top = y + i / 3 * 37;
            Box(x, top, 114, 30, active ? Lime : Card2, 9);
            Text(GameEngine.MetricNames[i], x + 57, top + 20, 11, active ? Bg : Muted, active, Paint.Align.Center);
            Hit(x, top, 114, 30, () => { rankingMetric = metric; scroll = 0; });
        }
        y += 83;
        var period = game!.Period(comparisonPeriod);
        var ranking = game.Ranking(rankingMetric, comparisonPeriod);
        Text(rankingMetric is RankingMetric.Cash or RankingMetric.Assets ? "자산·현금은 현재 잔액으로 정렬합니다." : "선택한 기간의 누적 성과로 정렬합니다.", 21, y, 10, Muted); y += 22;
        string Value(Trader bot)
        {
            double value = game.RankingValue(bot, rankingMetric, comparisonPeriod, period.Start);
            return rankingMetric == RankingMetric.Return ? Percent(value) : rankingMetric == RankingMetric.Volume ? Money((long)value) + "주" : "₩" + ShortMoney((long)value);
        }
        foreach (var (bot, rank) in ranking.Select((bot, i) => (bot, i + 1)))
        {
            if (y + 82 >= clipTop && y <= clipBottom)
            {
                bool follow = S.FollowedId == bot.Id;
                Box(20, y, 360, 77, follow ? Card2 : Card, 14, follow ? Teal : null);
                Text($"{rank:00}", 34, y + 43, 12, rank < 4 ? Lime : Muted, true);
                Robot(bot.Id, 59, y + 17, 41);
                Text(Representatives.Name(bot.Id), 111, y + 29, 12, Ink, true);
                Text(GameEngine.DispositionNames[(int)bot.Disposition] + " · " + bot.CreditRating, 111, y + 48, 10, Teal);
                TextFit(bot.Decision, 111, y + 65, 9, Muted, 137);
                Text(Value(bot), 363, y + 29, 14, Lime, true, Paint.Align.Right);
                Text($"자산 {ShortMoney(bot.Equity(S.Stocks))}", 363, y + 50, 10, Muted, false, Paint.Align.Right);
                int id = bot.Id; Hit(20, y, 360, 77, () => selectedTrader = id);
            }
            y += 85;
        }
        return y + 20;
    }

    float DrawNews(float y)
    {
        Text("시장의 시그널", 20, y + 23, 24, Ink, true);
        Text("시즌 실적에 반영되는 뉴스와 대표들의 상호작용", 21, y + 47, 11, Muted); y += 65;
        Button(operationsTab ? "대표 상호작용 · 작전  ⇄  뉴스 보기" : "시장 뉴스  ⇄  대표 상호작용 · 작전", 20, y, 360, 39, () => { operationsTab = !operationsTab; scroll = 0; }, false); y += 56;
        if (operationsTab) return DrawOperations(y);
        int up = S.Stocks.Count(s => s.Change >= 0);
        Metric("오늘의 상승 / 하락", $"{up}  /  {S.Stocks.Count - up}", 20, y, 174);
        Metric("AI 누적 체결", $"{Money(S.TotalAiTrades)}건", 206, y, 174); y += 88;
        Box(20, y, 360, 56, Card2, 13);
        Text("뉴스가 호가 판단에 반영되는 가상 시장", 36, y + 23, 12, Lime, true);
        Text("뉴스는 게임 내 사건이며 실제 기업과 관련이 없습니다.", 36, y + 42, 10, Muted); y += 82;
        Text("MARKET WIRE", 21, y, 10, Muted, true); y += 18;
        foreach (var news in S.News)
        {
            if (y + 152 < clipTop || y > clipBottom) { y += 163; continue; }
            Box(20, y, 360, 152, Card, 17);
            Pill(news.Impact > 0 ? "긍정 신호" : "주의 신호", 35, y + 14, news.Impact > 0 ? Teal : Red, 70);
            Text($"D{news.Day:00} {news.Hour:00}:00 · {S.Stocks[news.StockIndex].Symbol}", 363, y + 30, 10, Muted, false, Paint.Align.Right);
            float after = Wrap(news.Headline, 36, y + 62, 328, 14, Ink, 22);
            Text("월말 기업 실적과 기관 판단에 반영됩니다.", 36, Math.Max(after + 4, y + 112), 11, Muted);
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
        float y = Modal(Math.Min(664, h - 16), () => selectedStock = -1);
        Text(s.Symbol + "  /  " + s.Sector, 27, y + 37, 11, Hex(Palette[index]), true);
        Text(s.Name, 27, y + 67, 22, Ink, true);
        Text($"₩{Money(s.Price)}", 27, y + 106, 32, Ink, true);
        Text(Percent(s.Change), 373, y + 104, 14, Direction(s.Change), true, Paint.Align.Right);
        int levels = h - y < 620 ? 3 : 5;
        Chart(s.History.Select(v => (double)v), 28, y + 124, 344, 64, Hex(Palette[index]), true);
        Text($"최근 120시간 · 오늘 {Money(s.DayVolume)}주 체결", 28, y + 209, 10, Muted);
        y += 232;
        Text("실시간 호가", 28, y, 16, Ink, true);
        Text("기 = 기관 · 개 = 개인", 373, y, 10, Muted, false, Paint.Align.Right);
        y += 23;
        Text("매수 잔량", 30, y, 10, Teal); Text("매수가", 179, y, 10, Teal, false, Paint.Align.Right);
        Text("매도가", 216, y, 10, Red); Text("매도 잔량", 372, y, 10, Red, false, Paint.Align.Right);
        y += 12;
        var bids = game!.Depth(index, true, levels); var asks = game.Depth(index, false, levels);
        long max = Math.Max(1, bids.Concat(asks).Select(l => l.Quantity).DefaultIfEmpty(1).Max());
        for (int i = 0; i < levels; i++)
        {
            Box(27, y, 166, 36, Card, 3); Box(207, y, 166, 36, Card, 3);
            for (int side = 0; side < 2; side++)
            {
                var book = side == 0 ? bids : asks; float left = side == 0 ? 27 : 207; var color = side == 0 ? Teal : Red;
                if (i < book.Count)
                {
                    var level = book[i];
                    Box(left, y, 166f * level.Quantity / max, 36, new AColor((int)color.R, color.G, color.B, 25), 2);
                    Text(Money(level.Quantity), left + 6, y + 15, 11, color, true);
                    Text(Money(level.Price), left + 159, y + 15, 12, Ink, true, Paint.Align.Right);
                    Text($"기 {level.InstitutionQuantity} · 개 {level.RetailQuantity}", left + 6, y + 30, 9, Muted);
                }
                else Text("—", left + 83, y + 23, 12, Muted, false, Paint.Align.Center);
            }
            y += 40;
        }
        Text("가격·시간 우선 · 미체결 주문은 최대 3시간 유지", 28, y + 14, 10, Muted); y += 35;
        Text("최근 매칭 체결", 28, y, 14, Ink, true); y += 24;
        foreach (var t in S.Tape.Where(t => t.StockIndex == index).Take(2))
        {
            string buyer = t.BuyerId <= 100 ? "기관" : "개인", seller = t.SellerId <= 100 ? "기관" : "개인";
            Text($"{buyer} 매수 / {seller} 매도", 28, y, 11, Muted);
            Text($"{t.Quantity}주 · {Money(t.Price)}원", 373, y, 11, Ink, true, Paint.Align.Right); y += 23;
        }
        Button("기업 재무제표 5종 · 시즌 실적  →", 27, h - 67, 346, 45, () => { companyStock = index; companyTab = 0; companySeason = 0; selectedStock = -1; SetPage(6); });
    }

    void DrawTraderSheet()
    {
        var bot = S.Bots[selectedTrader - 1];
        float y = Modal(Math.Min(630, h - 16), () => selectedTrader = -1);
        Portrait(bot.Id, 28, y + 29, 120, 195);
        Text("INSTITUTION " + bot.Id.ToString("000"), 166, y + 62, 10, Lime, true);
        Text(Representatives.Name(bot.Id), 166, y + 98, 25, Ink, true);
        Text(bot.Name, 166, y + 124, 14, Muted);
        Text(GameEngine.DispositionNames[(int)bot.Disposition] + " · 신용 " + bot.CreditRating, 166, y + 155, 13, Teal, true);
        Text($"시즌 {S.Season} · {game!.RankOf(bot.Id)}위", 166, y + 190, 15, Ink, true);
        Wrap(bot.Decision, 29, y + 250, 342, 12, Muted, 20);
        Box(27, y + 286, 346, 105, Card, 18);
        Text("총 평가 자산", 44, y + 315, 12, Muted);
        Text($"₩{Money(bot.Equity(S.Stocks))}", 44, y + 353, 28, Ink, true);
        Text(Percent(bot.Return(S.Stocks)), 355, y + 381, 14, Direction(bot.Return(S.Stocks)), true, Paint.Align.Right);
        Text($"위험 관리 {bot.Abilities.RiskManagement} · 분산 {bot.Abilities.Diversification} · 대출 {ShortMoney(bot.LoanDebt)}원", 29, y + 421, 11, Muted);
        TextFit(bot.LastAction, 29, y + 447, 12, Muted, 341);
        Button("포트폴리오 · 대표 능력 · 신용/은행  →", 27, h - 80, 346, 52, () =>
        { S.FollowedId = bot.Id; portfolioTab = 3; historyPage = 0; selectedTrader = -1; SetPage(1); Save(); });
    }

    void DrawResults() { showResult = false; SetPage(4); }

    void DrawHelp()
    {
        float y = Modal(654, () => help = false);
        Text("스스로 움직이는 주식 시장", 27, y + 60, 24, Ink, true);
        Text("HOW IT WORKS", 28, y + 86, 11, Lime, true);
        string[] titles = ["01  기관 100개, 개인 10,000명", "02  호가 경쟁으로 결정되는 주가", "03  현실 120초 = 게임 1일", "04  끝없이 이어지는 30일 시즌"];
        string[] body = [
            "기관은 1,000만 원, 개인은 10만 원의 현금과 주식으로 시작합니다. 플레이어는 시장을 관찰합니다.",
            "서로의 매수·매도 호가가 가격과 시간 순서로 체결됩니다. 체결 때만 주가가 변합니다. 수수료·세금·금리·공매도 규칙은 정부 정책에 따라 바뀝니다.",
            "1배속에서 실제 5초마다 게임 시간 1시간이 지납니다. 24시간, 즉 실제 120초가 게임의 하루입니다.",
            "시즌 시작 자산 대비 수익률로 기관 순위를 기록합니다. 시즌마다 기업 결산과 정부 정책이 바뀝니다. 자산과 시장은 이어집니다."
        ];
        float row = y + 123;
        for (int i = 0; i < titles.Length; i++)
        { Text(titles[i], 28, row, 15, Ink, true); row = Wrap(body[i], 28, row + 27, 340, 12, Muted, 20) + 22; }
        Box(27, y + 493, 346, 66, Card, 14);
        Text("자유로운 관찰 · 자동 저장", 41, y + 516, 12, Teal, true);
        Text("1×·2×·5×·20×·50×·100× · 앱을 벗어나면 일시정지", 41, y + 542, 10, Muted);
        Button("준비됐어요", 27, y + 579, 346, 50, () => help = false);
    }

    void DrawConfirmation()
    {
        float y = Modal(286, () => confirmNew = false);
        Text("시장을 새로 시작할까요?", 27, y + 62, 23, Ink, true);
        Wrap("현재 시장을 새 시뮬레이션으로 교체합니다. 시즌은 자동 갱신되므로 계속 관찰하면 자산과 성적이 이어집니다.", 28, y + 99, 339, 13, Muted, 23);
        Button("계속 관찰", 27, y + 204, 166, 51, () => confirmNew = false, false);
        Button("새 시장 시작", 207, y + 204, 166, 51, () => Start());
    }
}
