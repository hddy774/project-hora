using Android.Content;
using Android.Graphics;
using Android.Views;
using AlphaExchange.Core;
using AColor = Android.Graphics.Color;
using APath = Android.Graphics.Path;

namespace AlphaExchange.App;

public sealed partial class GameView : View
{
    readonly Paint paint = new(PaintFlags.AntiAlias);
    readonly Typeface normal = Typeface.Create("sans-serif", TypefaceStyle.Normal)!;
    readonly Typeface bold = Typeface.Create("sans-serif-medium", TypefaceStyle.Normal)!;
    readonly Typeface display = Typeface.Create("sans-serif-black", TypefaceStyle.Normal)!;
    readonly Bitmap? arena;
    readonly List<(RectF Rect, Action Action)> targets = [];
    readonly GameStore store;
    long lastSave;
    int portfolioTab;
    long historyPage, selectedSeason;
    Canvas c = null!;
    GameEngine? game;
    bool lobby = true, auto, showResult, help, confirmNew;
    int page, selectedStock = -1, selectedTrader = -1, leagueFilter = -1, speed = 1;
    float scale = 1, h = 800, scroll, maxScroll, downY, lastY, downX;
    bool dragging;
    string toast = "";
    long toastUntil, lastTick;
    float clipTop = 0, clipBottom = 100000;
    static readonly AColor Bg = Hex("#0B111B"), Card = Hex("#141E2B"), Card2 = Hex("#1C2939"), Stroke = Hex("#273446"), Ink = Hex("#F4F7F4"), Muted = Hex("#8493A7"), Lime = Hex("#DCFF7D"), Teal = Hex("#65DDC1"), Red = Hex("#FF8996");
    static readonly string[] Palette = ["#DCFF7D", "#C2AFFA", "#65DDC1", "#FFA97D", "#91BCFF", "#FF95BA", "#ADE7AD", "#E5CD84"];
    AlphaExchange.Core.GameState S => game!.State;
    Trader Focus => game!.FocusTrader;

    public GameView(Context context) : base(context)
    {
        SetLayerType(LayerType.Hardware, null);
        Focusable = true;
        ContentDescription = "알파 익스체인지, 오프라인 주식 전략 게임";
        store = new GameStore(context.FilesDir!.AbsolutePath);
        try { using var stream = context.Assets!.Open("arena.png"); arena = BitmapFactory.DecodeStream(stream); } catch { }
        LoadPortraitManifest();
        game = store.Load(out string message);
        if (message.Length > 0) { toast = message; toastUntil = Now + 7000; }
        lastTick = Now;
        PostDelayed(Tick, 100);
    }

    static long Now => System.Environment.TickCount64;
    static AColor Hex(string value) => AColor.ParseColor(value);
    static string Money(long amount) => amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
    static string ShortMoney(long amount) => Math.Abs(amount) >= 100_000_000 ? $"{amount / 100_000_000.0:0.00}억" : Math.Abs(amount) >= 10000 ? $"{amount / 10000.0:0.0}만" : Money(amount);
    static string Percent(double value) => $"{(value >= 0 ? "+" : "")}{value * 100:0.00}%";
    static AColor Direction(double value) => value >= 0 ? Teal : Red;

    void Tick()
    {
        long now = Now;
        double elapsed = (now - lastTick) / 1000.0;
        lastTick = now;
        if (auto && !lobby && game is not null)
        {
            long previousSeason = S.Season;
            game.AdvanceTime(Math.Min(elapsed, 3600), speed);
            if (now - lastSave >= 15000 || S.Season != previousSeason) Save();
            if (S.Season != previousSeason) Notify($"시즌 {previousSeason} 기록 완료 · 시즌 {S.Season} 시작");
            Invalidate();
        }
        if (toastUntil > 0 && now > toastUntil) { toastUntil = 0; Invalidate(); }
        PostDelayed(Tick, 100);
    }

    protected override void OnDetachedFromWindow()
    {
        RemoveCallbacks(Tick);
        auto = false;
        base.OnDetachedFromWindow();
    }

    public void Pause() { auto = false; Save(); Invalidate(); }
    void Save()
    {
        if (game is null) return;
        try { store.Save(game); lastSave = Now; }
        catch { Notify("기기 저장 공간을 확인하세요. 저장하지 못했습니다."); }
    }

    void Start()
    {
        game = new GameEngine((uint)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        lobby = false; auto = true; lastTick = Now; page = 0; scroll = 0; leagueFilter = -1;
        selectedStock = selectedTrader = -1; showResult = confirmNew = false;
        portfolioTab = 0; historyPage = selectedSeason = 0; seasonCache.Clear();
        Save(); Invalidate();
    }

    void RequestNew()
    {
        if (game is not null) { confirmNew = true; Invalidate(); }
        else Start();
    }

    void ToggleSimulation()
    {
        if (game is null) return;
        lastTick = Now;
        auto = !auto;
        Save(); Invalidate();
    }

    public bool GoBack()
    {
        if (help) help = false;
        else if (confirmNew) confirmNew = false;
        else if (selectedStock >= 0) selectedStock = -1;
        else if (selectedTrader >= 0) selectedTrader = -1;
        else if (showResult) showResult = false;
        else if (!lobby) { lobby = true; auto = false; Save(); }
        else return false;
        Invalidate(); return true;
    }

    void Notify(string value) { toast = value; toastUntil = Now + 3000; Invalidate(); }
    void SetPage(int value) { page = value; scroll = 0; Invalidate(); }

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        c = canvas; scale = Width / 400f; h = Height / scale;
        c.Save(); c.Scale(scale, scale); c.DrawColor(Bg);
        targets.Clear(); clipTop = 0; clipBottom = h;
        if (lobby) DrawLobby();
        else if (game is not null)
        {
            DrawHeader();
            c.Save(); c.ClipRect(0, 101, 400, h - 145);
            clipTop = 101; clipBottom = h - 145;
            float end = page switch { 0 => DrawMarket(112 - scroll), 1 => DrawPortfolio(112 - scroll), 2 => DrawLeague(112 - scroll), 3 => DrawStatistics(112 - scroll), 4 => DrawSeasons(112 - scroll), _ => DrawNews(112 - scroll) };
            maxScroll = Math.Max(0, end + scroll - (h - 160));
            c.Restore(); clipTop = 0; clipBottom = h;
            DrawFooter();
            if (scroll > maxScroll + 1) { scroll = maxScroll; PostInvalidate(); }
            if (selectedStock >= 0) DrawStockSheet();
            if (selectedTrader >= 0) DrawTraderSheet();
            if (showResult) DrawResults();
        }
        if (help) DrawHelp();
        if (confirmNew) DrawConfirmation();
        if (toastUntil > Now)
        {
            Box(20, h - 194, 360, 46, Ink, 14);
            TextFit(toast, 34, h - 166, 13, Bg, 332, true);
        }
        c.Restore();
    }

    void Box(float x, float y, float w, float height, AColor color, float radius = 18, AColor? border = null)
    {
        paint.SetShader(null); paint.Color = color; paint.SetStyle(Paint.Style.Fill);
        c.DrawRoundRect(x, y, x + w, y + height, radius, radius, paint);
        if (border.HasValue)
        { paint.Color = border.Value; paint.StrokeWidth = 1; paint.SetStyle(Paint.Style.Stroke); c.DrawRoundRect(x, y, x + w, y + height, radius, radius, paint); paint.SetStyle(Paint.Style.Fill); }
    }

    void Text(string value, float x, float y, float size, AColor color, bool heavy = false, Paint.Align? align = null, bool headline = false)
    {
        paint.SetShader(null); paint.Color = color; paint.TextSize = size; paint.SetStyle(Paint.Style.Fill);
        paint.SetTypeface(headline ? display : heavy ? bold : normal); paint.TextAlign = align ?? Paint.Align.Left;
        c.DrawText(value, x, y, paint);
    }

    void TextFit(string value, float x, float y, float size, AColor color, float width, bool heavy = false)
    {
        paint.TextSize = size; paint.SetTypeface(heavy ? bold : normal);
        if (paint.MeasureText(value) > width)
        { while (value.Length > 0 && paint.MeasureText(value + "…") > width) value = value[..^1]; value += "…"; }
        Text(value, x, y, size, color, heavy);
    }

    float Wrap(string value, float x, float y, float width, float size, AColor color, float spacing = 21)
    {
        string row = ""; paint.TextSize = size; paint.SetTypeface(normal);
        foreach (char ch in value)
        {
            if (ch == '\n' || paint.MeasureText(row + ch) > width) { Text(row, x, y, size, color); y += spacing; row = ""; }
            if (ch != '\n') row += ch;
        }
        if (row.Length > 0) { Text(row, x, y, size, color); y += spacing; }
        return y;
    }

    void Line(float x1, float y1, float x2, float y2, AColor color, float width = 1)
    { paint.SetShader(null); paint.Color = color; paint.StrokeWidth = width; paint.StrokeCap = Paint.Cap.Round; c.DrawLine(x1, y1, x2, y2, paint); }

    void Circle(float x, float y, float radius, AColor color)
    { paint.SetShader(null); paint.SetStyle(Paint.Style.Fill); paint.Color = color; c.DrawCircle(x, y, radius, paint); }

    void Hit(float x, float y, float w, float height, Action action)
    {
        float top = Math.Max(y, clipTop), bottom = Math.Min(y + height, clipBottom);
        if (bottom > top) targets.Add((new RectF(x, top, x + w, bottom), action));
    }

    void Button(string label, float x, float y, float w, float height, Action action, bool primary = true, bool enabled = true)
    {
        Box(x, y, w, height, !enabled ? Card2 : primary ? Lime : Card2, 15, primary ? null : Stroke);
        Text(label, x + w / 2, y + height / 2 + 5, 14, !enabled ? Muted : primary ? Bg : Ink, true, Paint.Align.Center);
        if (enabled) Hit(x, y, w, height, action);
    }

    void Pill(string label, float x, float y, AColor color, float w)
    { Box(x, y, w, 24, new AColor((int)color.R, color.G, color.B, 24), 8); Text(label, x + w / 2, y + 16, 10, color, true, Paint.Align.Center); }

    void Chart(IEnumerable<double> values, float x, float y, float width, float height, AColor color, bool fill = false)
    {
        var data = values.ToArray(); if (data.Length < 2) data = [data.FirstOrDefault(), data.FirstOrDefault()];
        double low = data.Min(), high = data.Max();
        if (Math.Abs(high - low) < 1) { low -= 1; high += 1; }
        double pad = (high - low) * .15; low -= pad; high += pad;
        using var path = new APath(); float lastX = x, lastY = y;
        for (int i = 0; i < data.Length; i++)
        {
            float px = x + i * width / (data.Length - 1), py = y + height - (float)((data[i] - low) / (high - low)) * height;
            if (i == 0) path.MoveTo(px, py); else path.LineTo(px, py);
            lastX = px; lastY = py;
        }
        if (fill)
        {
            using var area = new APath(path); area.LineTo(x + width, y + height); area.LineTo(x, y + height); area.Close();
            using var gradient = new LinearGradient(x, y, x, y + height, new AColor((int)color.R, color.G, color.B, 65), new AColor((int)color.R, color.G, color.B, 0), Shader.TileMode.Clamp!);
            paint.SetShader(gradient); paint.SetStyle(Paint.Style.Fill); c.DrawPath(area, paint); paint.SetShader(null);
        }
        paint.Color = color; paint.StrokeWidth = fill ? 2.5f : 1.8f; paint.SetStyle(Paint.Style.Stroke); paint.StrokeJoin = Paint.Join.Round;
        c.DrawPath(path, paint); paint.SetStyle(Paint.Style.Fill);
        if (fill) { Circle(lastX, lastY, 4, Bg); Circle(lastX, lastY, 3, color); }
    }

    void Robot(int id, float x, float y, float size) => Portrait(id, x, y, size, size);

    void Mark(float x, float y, float size, AColor color)
    {
        using var p = new APath(); p.MoveTo(x, y + size); p.LineTo(x + size / 2, y); p.LineTo(x + size, y + size);
        paint.Color = color; paint.StrokeWidth = size * .18f; paint.SetStyle(Paint.Style.Stroke); paint.StrokeJoin = Paint.Join.Round; c.DrawPath(p, paint); paint.SetStyle(Paint.Style.Fill);
        Line(x + size * .38f, y + size * .72f, x + size * .62f, y + size * .72f, color, size * .12f);
    }

    void NavIcon(int type, float x, float y, AColor color)
    {
        if (type == 0) { Line(x, y + 17, x + 5, y + 10, color, 2); Line(x + 5, y + 10, x + 11, y + 13, color, 2); Line(x + 11, y + 13, x + 20, y + 2, color, 2); Line(x + 14, y + 2, x + 20, y + 2, color, 2); }
        else if (type == 1) { Box(x, y + 4, 22, 15, Bg, 4, color); Box(x + 13, y + 8, 11, 7, Bg, 2, color); Circle(x + 17, y + 11.5f, 1, color); }
        else if (type == 2) { Line(x + 3, y + 3, x + 19, y + 3, color, 2); Line(x + 3, y + 3, x + 6, y + 13, color, 2); Line(x + 19, y + 3, x + 16, y + 13, color, 2); Line(x + 6, y + 13, x + 16, y + 13, color, 2); Line(x + 11, y + 13, x + 11, y + 19, color, 2); Line(x + 6, y + 21, x + 16, y + 21, color, 2); }
        else { Box(x, y + 1, 22, 21, Bg, 4, color); Line(x + 5, y + 7, x + 17, y + 7, color, 1.5f); Line(x + 5, y + 12, x + 17, y + 12, color, 1.5f); Line(x + 5, y + 17, x + 12, y + 17, color, 1.5f); }
    }

    public override bool OnTouchEvent(MotionEvent? e)
    {
        if (e is null) return false;
        float x = e.GetX() / scale, y = e.GetY() / scale;
        switch (e.Action)
        {
            case MotionEventActions.Down: downX = x; downY = lastY = y; dragging = false; return true;
            case MotionEventActions.Move:
                if (!lobby && selectedStock < 0 && selectedTrader < 0 && !showResult && !help && !confirmNew && downY > 102 && downY < h - 145)
                {
                    if (Math.Abs(y - downY) > 7) dragging = true;
                    if (dragging) { scroll = Math.Clamp(scroll + lastY - y, 0, maxScroll); Invalidate(); }
                }
                lastY = y; return true;
            case MotionEventActions.Up:
                if (!dragging && Math.Abs(x - downX) < 18 && Math.Abs(y - downY) < 18)
                {
                    for (int i = targets.Count - 1; i >= 0; i--)
                        if (targets[i].Rect.Contains(x, y)) { var action = targets[i].Action; PerformHapticFeedback(FeedbackConstants.VirtualKey); action(); Invalidate(); break; }
                    PerformClick();
                }
                return true;
            case MotionEventActions.Cancel: dragging = false; return true;
        }
        return true;
    }
    public override bool PerformClick() { base.PerformClick(); return true; }
}
