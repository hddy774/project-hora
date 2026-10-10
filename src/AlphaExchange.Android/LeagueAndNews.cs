using Android.Graphics;
using AlphaExchange.Core;
using AColor = Android.Graphics.Color;

namespace AlphaExchange.App;

public sealed partial class GameView
{
    float DrawLeague(float y)
    {
        Text("분야별 랭킹과 투자 성과", 20, y + 23, 24, Ink, true);
        Text("10개 능력과 성향 · 다양한 투자 기법을 혼합", 21, y + 47, 12, Muted); y += 64;
        y=PeopleRoleTabs(y,true);
        if(rankingRole!=1) return DrawPeopleRanking(y,rankingRole==0 ? null : (PersonRole)(rankingRole-1));
        y=DrawInvestorPodium(y);
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
        var period = game!.PeriodSummary(comparisonPeriod);
        var ranking = FrameRanking(rankingMetric, comparisonPeriod);
        Text(rankingMetric is RankingMetric.Cash or RankingMetric.Assets ? "자산·현금은 현재 잔액으로 정렬합니다." : "선택한 기간의 누적 성과로 정렬합니다.", 21, y, 10, Muted); y += 22;
        string Value(RankRow row)
        {
            double value = row.Value;
            return rankingMetric == RankingMetric.Return ? Percent(value) : rankingMetric == RankingMetric.Volume ? Money((long)value) + "주" : "₩" + ShortMoney((long)value);
        }
        foreach (var row in ranking)
        {
            var bot=row.Trader; int rank=row.Rank;
            if (y + 82 >= clipTop && y <= clipBottom)
            {
                bool follow = S.FollowedId == bot.Id;
                Box(20, y, 360, 77, follow ? Card2 : Card, 14, follow ? Teal : null);
                Text($"{rank:00}", 34, y + 43, 12, rank < 4 ? Lime : Muted, true);
                Robot(bot.Id, 59, y + 17, 41);
                Text(Representatives.Name(bot.PortraitId), 111, y + 29, 12, Ink, true);
                Text(GameEngine.DispositionNames[(int)bot.Disposition] + " · " + bot.CreditRating, 111, y + 48, 10, Teal);
                TextFit(bot.Decision, 111, y + 65, 9, Muted, 137);
                Text(Value(row), 363, y + 29, 14, Lime, true, Paint.Align.Right);
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
        Button(corporateTab ? "기업행동 · 배당/분할/합병  ⇄  시장 보기" : "기업행동 · 배당/분할/합병 기록  →",20,y,360,39,()=>{ corporateTab=!corporateTab; scroll=0; },false); y+=56;
        if(corporateTab) return DrawCorporateEvents(y);
        if (operationsTab) return DrawOperations(y);
        int up = S.Stocks.Count(s => s.Active && s.Change >= 0);
        Metric("오늘의 상승 / 하락", $"{up}  /  {S.Stocks.Count(s=>s.Active) - up}", 20, y, 174);
        Metric("AI 누적 체결", $"{Money(S.TotalAiTrades)}건", 206, y, 174); y += 88;
        Box(20, y, 360, 56, Card2, 13);
        Text("뉴스가 호가 판단에 반영되는 가상 시장", 36, y + 23, 12, Lime, true);
        Text("뉴스는 게임 내 사건이며 실제 기업과 관련이 없습니다.", 36, y + 42, 10, Muted); y += 82;
        Text("MARKET WIRE", 21, y, 10, Muted, true); y += 18;
        foreach (var news in S.News)
        {
            if (y + 152 < clipTop || y > clipBottom) { y += 163; continue; }
            Box(20, y, 360, 152, Card, 17);
            Pill(news.Impact==0 ? "인물 활동" : news.Impact > 0 ? "긍정 신호" : "주의 신호", 35, y + 14, news.Impact > 0 ? Teal : Red, 70);
            Text($"D{news.Day:00} {news.Hour:00}:{news.Minute:00} · {(news.StockIndex>=0 ? S.Stocks[news.StockIndex].Symbol : "경제")}", 363, y + 30, 10, Muted, false, Paint.Align.Right);
            float after = Wrap(news.Headline, 36, y + 62, 328, 14, Ink, 22);
            TextFit(news.ActorId>0 ? $"활동 #{news.ActivityId} · {game!.Person(news.ActorId).Name}" : "이전 버전 뉴스 기록", 36, Math.Max(after + 4, y + 112), 11, Muted,328);
            Text("종목 살펴보기  ↗", 362, y + 136, 10, news.Impact > 0 ? Teal : Red, true, Paint.Align.Right);
            int index = news.StockIndex;
            Hit(20,y,360,152,()=> { if(index>=0) selectedStock=index; else if(news.ActorId>0) OpenPerson(news.ActorId); });
            y += 163;
        }
        return y + 10;
    }

    float Modal(float desiredHeight, Action close)
    {
        modalScroll.Reset();
        float top = Math.Max(12, h - desiredHeight);
        DrawModalChrome(top, close);
        return top;
    }

    ModalViewport ScrollModal(string key, float desiredHeight, float footerHeight, Action close)
    {
        var viewport = ModalViewport.Create(h, desiredHeight, footerHeight);
        modalScroll.Show(key, viewport);
        DrawModalChrome(viewport.Top, close);
        return viewport;
    }

    void DrawModalChrome(float top, Action close)
    {
        targets.Clear(); clipTop = 0; clipBottom = h;
        void Dismiss() { modalScroll.Reset(); close(); }
        Box(0, 0, 400, h, new AColor(0, 0, 0, 190), 0);
        Hit(0, 0, 400, h, Dismiss);
        Box(8, top, 384, h - top + 24, Bg, 26, Stroke);
        Hit(8, top, 384, h - top, () => { });
        Box(179, top + 9, 42, 4, Stroke, 2);
        Circle(360, top + 34, 15, Card2);
        Text("×", 360, top + 40, 22, Muted, false, Paint.Align.Center);
        Hit(336, top + 15, 46, 42, Dismiss);
    }

    float BeginModalBody()
    {
        var viewport = modalScroll.Viewport;
        clipTop = viewport.BodyTop; clipBottom = viewport.BodyBottom;
        c.Save(); c.ClipRect(8, clipTop, 392, clipBottom);
        return viewport.BodyTop - modalScroll.Offset;
    }

    void EndModalBody(float bottom)
    {
        bool clamped = modalScroll.SetContentHeight(bottom + modalScroll.Offset - modalScroll.Viewport.BodyTop);
        c.Restore(); clipTop = 0; clipBottom = h;
        if (modalScroll.Maximum > 0)
        {
            var viewport = modalScroll.Viewport;
            float track = viewport.BodyHeight;
            float thumb = Math.Max(24, track * track / (track + modalScroll.Maximum));
            float top = viewport.BodyTop + (track - thumb) * modalScroll.Offset / modalScroll.Maximum;
            Box(385, top, 3, thumb, Stroke, 2);
        }
        if (clamped) PostInvalidate();
    }

    void DrawStockSheet()
    {
        int index = selectedStock; var s = S.Stocks[index];
        var viewport = ScrollModal($"stock:{index}", Math.Min(664, h - 16), 45, () => selectedStock = -1);
        TextFit(s.Symbol + "  /  " + s.Sector, 27, viewport.Top + 37, 11, PaletteColors[index % Palette.Length], 298, true);
        float y = BeginModalBody();
        Text(s.Name, 27, y + 28, 22, Ink, true);
        Text($"₩{Money(s.Price)}", 27, y + 67, 32, Ink, true);
        Text(Percent(s.Change), 373, y + 65, 14, Direction(s.Change), true, Paint.Align.Right);
        const int levels = 5;
        Chart(s.History.Select(v => (double)v), 28, y + 85, 344, 64, PaletteColors[index % Palette.Length], true);
        Text($"분할 조정 120시간 · 시총 {ShortMoney(s.MarketCap)}원", 28, y + 170, 10, Muted);
        y += 193;
        Text("실시간 호가", 28, y, 16, Ink, true);
        Text("기=기관 · 개=개인 · 은=은행", 373, y, 9, Muted, false, Paint.Align.Right);
        y += 23;
        Text("매수 잔량", 30, y, 10, Teal); Text("매수가", 179, y, 10, Teal, false, Paint.Align.Right);
        Text("매도가", 216, y, 10, Red); Text("매도 잔량", 372, y, 10, Red, false, Paint.Align.Right);
        y += 12;
        var bids = FrameDepth(index,true,levels); var asks=FrameDepth(index,false,levels);
        long max=1; foreach(var level in bids) max=Math.Max(max,level.Quantity); foreach(var level in asks) max=Math.Max(max,level.Quantity);
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
                    if(side==0)
                    { Text(Money(level.Quantity),left+6,y+15,11,color,true); Text(Money(level.Price),left+159,y+15,12,Ink,true,Paint.Align.Right); }
                    else
                    { Text(Money(level.Price),left+6,y+15,12,Ink,true); Text(Money(level.Quantity),left+159,y+15,11,color,true,Paint.Align.Right); }
                    Text($"기 {level.InstitutionQuantity} · 개 {level.RetailQuantity} · 은 {level.BankQuantity}", left + 6, y + 30, 9, Muted);
                }
                else Text("—", left + 83, y + 23, 12, Muted, false, Paint.Align.Center);
            }
            y += 40;
        }
        string status=bids.Count==0 || asks.Count==0 ?
            string.Join(" · ",new[]{bids.Count==0 ? "매수: "+game!.EmptyBookReason(index,true) : "",asks.Count==0 ? "매도: "+game!.EmptyBookReason(index,false) : ""}.Where(s=>s.Length>0)) :
            "가격·시간 우선 · 은행이 실제 잔량을 보충합니다";
        TextFit(status,28,y+14,10,Muted,344); y+=35;
        Text("최근 매칭 체결", 28, y, 14, Ink, true); y += 24;
        foreach (var t in S.Tape.Where(t => t.StockIndex == index).Take(2))
        {
            string buyer = t.BuyerId==0 ? "은행" : t.BuyerId <= 100 ? "기관" : "개인", seller = t.SellerId==0 ? "은행" : t.SellerId <= 100 ? "기관" : "개인";
            Text($"{buyer} 매수 / {seller} 매도", 28, y, 11, Muted);
            Text($"{t.Quantity}주 · {Money(t.Price)}원", 373, y, 11, Ink, true, Paint.Align.Right); y += 23;
        }
        EndModalBody(y + 12);
        Button("기업 재무제표 →",27,viewport.FooterTop,170,45,()=>{ companyStock=index; companyTab=0; companySeason=0; selectedStock=-1; SetPage(6); });
        Button("지분 구조 →",207,viewport.FooterTop,166,45,()=>OpenOwnership(index),false);
    }

    void DrawTraderSheet()
    {
        var bot = S.Bots[selectedTrader - 1];
        var viewport = ScrollModal($"trader:{bot.Id}", Math.Min(630, h - 16), 52, () => selectedTrader = -1);
        Text("INSTITUTION " + bot.Id.ToString("000"), 27, viewport.Top + 38, 11, Lime, true);
        float y = BeginModalBody();
        Portrait(bot.PortraitId, 28, y + 8, 120, 195); Hit(28,y+8,120,195,()=>portraitZoom=bot.PortraitId);
        TextFit(Representatives.Name(bot.PortraitId), 166, y + 47, 25, Ink, 207, true);
        TextFit(bot.Name, 166, y + 73, 14, Muted, 207);
        Text(GameEngine.DispositionNames[(int)bot.Disposition] + " · 신용 " + bot.CreditRating, 166, y + 104, 13, Teal, true);
        Text($"시즌 {S.Season} · {FrameRankOf(bot.Id)}위", 166, y + 139, 15, Ink, true);
        Hit(166,y+16,207,71,()=>OpenPerson(bot.Id));
        float afterDecision = Wrap(bot.Decision, 29, y + 232, 342, 12, Muted, 20);
        y = Math.Max(y + 255, afterDecision + 12);
        Box(27, y, 346, 105, Card, 18);
        Text("총 평가 순자산", 44, y + 29, 12, Muted);
        Text($"₩{Money(bot.Equity(S.Stocks))}", 44, y + 67, 28, Ink, true);
        Text(Percent(bot.Return(S.Stocks)), 355, y + 95, 14, Direction(bot.Return(S.Stocks)), true, Paint.Align.Right);
        Text($"위험 관리 {bot.Abilities.RiskManagement} · 분산 {bot.Abilities.Diversification} · 대출 {ShortMoney(bot.LoanDebt)}원", 29, y + 135, 11, Muted);
        Text($"누적 거래량 {Money(bot.TradedVolume)}주 · 거래금액 {ShortMoney(bot.TradedTurnover)}원",29,y+161,10,Ink);
        Text($"전체 수익률 {Percent(bot.ReturnIndex/Math.Max(1e-12,bot.OpeningSnapshot.ReturnIndex)-1)} · 직원 {bot.Development!.EmployeeCount}명",29,y+184,11,Teal);
        Text($"시즌 보상 {bot.Development.LastRewardPoints}P · 성장 {bot.Development.PointsSpent}P · 현금 {game!.CashRatio(bot):P1}",29,y+207,10,Muted);
        EndModalBody(y + 225);
        Button("대표·기관 능력 / 직원·성장 / 신용  →", 27, viewport.FooterTop, 346, 52, () =>
        { S.FollowedId = bot.Id; portfolioTab = 3; historyPage = 0; selectedTrader = -1; SetPage(1); Save(); });
    }

    void DrawResults() { showResult = false; SetPage(4); }

    void DrawHelp()
    {
        var viewport = ScrollModal("help", 654, 50, () => help = false);
        Text("HOW IT WORKS", 28, viewport.Top + 38, 11, Lime, true);
        float y = BeginModalBody();
        Text("스스로 움직이는 주식 시장", 27, y + 29, 24, Ink, true);
        string[] titles = ["01  인물 200명, 개미 집단과 1조 시장", "02  호가 경쟁으로 결정되는 주가", "03  현실 120초 = 게임 1일", "04  끝없이 이어지는 30일 시즌"];
        string[] body = [
            "대표 능력은 10개 모두 50점으로 시작합니다. 시즌 1~100위 모두 1~10점의 성장·신용 보상을 받습니다. 직원 등급·역할·인원이 대표와 함께 기관의 판단 능력을 만듭니다.",
            "실제 양방향 주문이 가격·시간 순서로 체결됩니다. 기관의 현금 목표는 5~30%이며 가치·뉴스·비용을 봅니다. 개인은 시장 반응을 따라갑니다.",
            "1배속에서 실제 5초마다 게임 시간 1시간이 지납니다. 24시간, 즉 실제 120초가 게임의 하루입니다.",
            "초단기는 시간, 단기는 1~10일 미만, 장기는 10일 이상을 봅니다. 중요한 뉴스·위험 때 계획이 바뀝니다. 매월 결산·정책·보상과 통계가 파일에 보존됩니다."
        ];
        float row = y + 66;
        for (int i = 0; i < titles.Length; i++)
        { Text(titles[i], 28, row, 15, Ink, true); row = Wrap(body[i], 28, row + 27, 340, 12, Muted, 20) + 22; }
        Box(27, row, 346, 66, Card, 14);
        Text("자유로운 관찰 · 자동 저장", 41, row + 23, 12, Teal, true);
        Text("1×·2×·5×·20×·50×·100× · 앱을 벗어나면 일시정지", 41, row + 49, 10, Muted);
        EndModalBody(row + 78);
        Button("준비됐어요", 27, viewport.FooterTop, 346, 50, () => { help = false; modalScroll.Reset(); });
    }

    void DrawConfirmation()
    {
        float y = Modal(286, () => confirmNew = false);
        Text("시장을 새로 시작할까요?", 27, y + 62, 23, Ink, true);
        Wrap("새 시장을 시작합니다. 기존 기록은 파일에 보존됩니다. 현재 시장을 따로 다시 열려면 먼저 통계에서 기록을 내보내세요.", 28, y + 99, 339, 13, Muted, 23);
        Button("계속 관찰", 27, y + 204, 166, 51, () => confirmNew = false, false);
        Button("새 시장 시작", 207, y + 204, 166, 51, () => Start());
    }
}
