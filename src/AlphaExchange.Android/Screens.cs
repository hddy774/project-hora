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
        Text("200명의 인물.", 24, 104, 34, Ink, true, headline: true);
        Text("1조 원의 시장.", 24, 146, 34, Lime, true, headline: true);
        Text("THE AUTONOMOUS MARKET SIMULATOR", 26, 174, 10, Muted, true);
        float y = h - 294;
        Box(20, y, 360, 83, Card, 20, Stroke);
        string[] values = ["200", "1,000", "100×"], labels = ["경제 인물", "개미 집단 시작", "최대 관찰 속도"];
        for (int i = 0; i < 3; i++)
        {
            Text(values[i], 80 + i * 120, y + 35, i == 2 ? 21 : 26, i == 0 ? Lime : Ink, true, Paint.Align.Center);
            Text(labels[i], 80 + i * 120, y + 59, 11, Muted, false, Paint.Align.Center);
            if (i < 2) Line(140 + i * 120, y + 20, 140 + i * 120, y + 62, Stroke);
        }
        Text("호가로 움직이는 시장 · 끝없이 이어지는 시즌", 200, h - 188, 12, Muted, false, Paint.Align.Center);
        if(loading || loadFailed)
        {
            Button(loading ? "기록을 불러오는 중…" : "기록을 보호 중 · 앱을 다시 열어주세요",20,h-166,360,52,()=>{},false,false);
            Button("시뮬레이션 안내",20,h-103,360,45,()=>help=true,false);
        }
        else if (game is not null)
        {
            Button($"시즌 {S.Season} · DAY {S.Day:00}  이어하기  →", 20, h - 166, 360, 52, () => { lobby = false; auto = true; lastTick = Now; });
            Button("새 시뮬레이션 시작", 20, h - 103, 360, 45, RequestNew, false);
        }
        else
        {
            Button("시뮬레이션 시작  →", 20, h - 166, 360, 52, Start);
            Button("대표 능력 · 공매도 · 정부와 은행", 20, h - 103, 360, 45, () => help = true, false);
        }
        Text("파일 가져오기 · 기록 복원", 90, h - 28, 11, Teal, false, Paint.Align.Center);
        Hit(10, h-50, 165, 40, () => PickBackup(false));
        Text("시뮬레이션 안내", 280, h - 28, 12, Muted, false, Paint.Align.Center);
        Hit(190, h - 53, 190, 47, () => help = true);
    }

    void DrawHeader()
    {
        Mark(23, 20, 20, Lime);
        Text("ALPHA", 57, 29, 15, Ink, true, headline: true);
        Text("EXCHANGE", 58, 44, 9, Muted, true);
        Hit(14, 7, 151, 45, () => { lobby = true; auto = false; Save(); });
        Pill($"v{BuildVersion} · OFFLINE", 207, 20, Teal, 128);
        Circle(360, 32, 15, Card2); Text("?", 360, 37, 15, Ink, true, Paint.Align.Center);
        Hit(338, 9, 43, 45, () => { help = true; });
        Text($"S{S.Season} · DAY {S.Day:00}/30", 21, 77, 18, Ink, true);
        Text($"{S.Hour:00}:{S.Minute:00}", 379, 77, 20, Lime, true, Paint.Align.Right);
        Text(auto ? "진행 중" : "일시정지", 234, 76, 11, auto ? Teal : Muted);
        Box(20, 90, 360, 3, Card2, 2);
        Box(20, 90, 360f * (float)((S.Hour + S.Minute/60.0 + S.MinuteProgress/60.0) / 24), 3, Lime, 2);
    }

    void DrawFooter()
    {
        Box(0, h - 146, 400, 146, Bg, 0);
        Line(20, h - 146, 380, h - 146, Stroke);
        Text("1시간 = 5초", 21, h - 116, 10, Muted);
        Text("1일 = 120초 · 1×", 21, h - 96, 10, Muted);
        Button($"{speed}×", 116, h - 132, 60, 49, () => { lastTick = Now; speed = GameEngine.Speeds[(Array.IndexOf(GameEngine.Speeds, speed) + 1) % GameEngine.Speeds.Length]; }, false);
        Button(auto ? "Ⅱ   일시정지" : "▷   관찰 재개", 176, h - 132, 204, 49, ToggleSimulation);
        string[] labels = ["시장", "자산", "랭킹", "인물", "통계", "시즌", "뉴스"];
        int[] pages=[0,1,2,10,3,4,5];
        for (int i = 0; i < labels.Length; i++)
        {
            int p = pages[i]; float left = i * (400f / 7), center = left + 400f / 14; AColor color = page == p ? Lime : Muted;
            if (page == p) Box(center - 18, h - 73, 36, 3, Lime, 2);
            NavIcon(i, center - 11, h - 59, color);
            Text(labels[i], center, h - 17, 10, color, page == p, Paint.Align.Center);
            Hit(left, h - 73, 400f / 7, 73, () => SetPage(p));
        }
    }

    float DrawMarket(float y)
    {
        var focus = Focus; long equity = focus.Equity(S.Stocks); double ret = focus.Return(S.Stocks);
        Box(20, y, 360, 166, Card, 22, Stroke);
        Text($"{focus.Name}의 순자산", 38, y + 30, 12, Muted);
        Pill($"{FrameRankOf(focus.Id)}위 / 100", 278, y + 15, Lime, 84);
        Text($"₩{Money(equity)}", 37, y + 72, 32, Ink, true, headline: true);
        Text(Percent(ret), 39, y + 99, 14, Direction(ret), true);
        Text("시즌 누적 수익률", 122, y + 99, 10, Muted);
        Chart(focus.EquityHistory.Select(v => (double)v), 39, y + 114, 205, 28, Teal);
        Text("보유 현금", 358, y + 124, 10, Muted, false, Paint.Align.Right);
        Text($"₩{ShortMoney(focus.Cash)}", 358, y + 145, 15, Ink, true, Paint.Align.Right);
        y += 183;
        Text("MARKET PULSE", 22, y + 11, 10, Lime, true);
        Text($"{S.Stocks.Count(s => s.Active)}개 회사 · 6분야", 379, y + 11, 10, Muted, false, Paint.Align.Right);
        y += 27;
        var news = S.News.First();
        Box(20, y, 360, 52, Card2, 13);
        Circle(38, y + 26, 3, news.Impact > 0 ? Teal : Red);
        TextFit(news.Headline, 50, y + 23, 11, Ink, 298);
        Text($"DAY {news.Day:00}  ·  뉴스 보기  →", 50, y + 40, 9, Muted);
        Hit(20, y, 360, 52, () => SetPage(5));
        y += 76;
        Text("오늘의 시장", 21, y, 19, Ink, true);
        Text("종목별 흐름 관찰", 379, y, 11, Muted, false, Paint.Align.Right);
        y += 16;
        y=SectorTabs(y);
        foreach(int i in FilteredCompanies(true))
        {
            int index = i; var s = S.Stocks[i]; if (!s.Active) continue;
            if(!Visible(y,80)) { y+=89; continue; }
            Box(20, y, 360, 80, Card, 16);
            AColor accent = PaletteColors[i % Palette.Length];
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
        Text("등락률은 당일 기준 · 기관·개인의 호가 체결이 가격을 결정합니다.", 200, y + 17, 10, Muted, false, Paint.Align.Center);
        return y + 35;
    }

    void Metric(string label, string value, float x, float y, float w)
    { Box(x, y, w, 70, Card, 16); Text(label, x + 16, y + 25, 11, Muted); Text(value, x + 16, y + 51, 19, Ink, true); }
}
