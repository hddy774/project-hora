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
    readonly List<(float Left,float Top,float Right,float Bottom,Action Action)> targets = [];
    readonly PerformanceRules defaultPerformance=SimulationRules.Default().Performance;
    readonly GameStore store;
    long lastSave;
    string preparedRun="";
    long preparedHour=-1,preparedTransaction=-1,preparedEvent=-1;
    readonly object saveGate = new();
    SaveSnapshot? pendingSave;
    bool saving;
    Task saveTask = Task.CompletedTask;
    bool fileBusy;
    long lastCommitTime;
    int portfolioTab;
    long historyPage, selectedSeason;
    Canvas c = null!;
    GameEngine? game;
    bool lobby = true, auto, showResult, help, confirmNew;
    int page, selectedStock = -1, selectedTrader = -1, speed = 1;
    RankingMetric rankingMetric;
    ComparisonPeriod comparisonPeriod;
    int companyStock, companyTab;
    long companySeason;
    bool operationsTab;
    float scale = 1, h = 800, scroll, maxScroll, downY, lastY, downX;
    bool dragging;
    string toast = "";
    long toastUntil, lastTick,lastFrame;
    float clipTop = 0, clipBottom = 100000;
    static readonly AColor Bg = Hex("#0B111B"), Card = Hex("#141E2B"), Card2 = Hex("#1C2939"), Stroke = Hex("#273446"), Ink = Hex("#F4F7F4"), Muted = Hex("#8493A7"), Lime = Hex("#DCFF7D"), Teal = Hex("#65DDC1"), Red = Hex("#FF8996");
    static readonly string[] Palette = ["#DCFF7D", "#C2AFFA", "#65DDC1", "#FFA97D", "#91BCFF", "#FF95BA", "#ADE7AD", "#E5CD84", "#A0D9FF", "#D6B0FF"];
    static readonly AColor[] PaletteColors=Palette.Select(Hex).ToArray();
    static readonly string BuildVersion=typeof(GameView).Assembly.GetName().Version!.ToString(3);
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
        PostDelayed(Tick, defaultPerformance.TickMilliseconds);
    }

    static long Now => System.Environment.TickCount64;
    static AColor Hex(string value) => AColor.ParseColor(value);
    static string Money(long amount) => amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
    string ShortMoney(long amount) => game?.State.World?.NumberFormat==1 ? KoreanNumber.Full(amount) : KoreanNumber.Compact(amount);
    static string Percent(double value) => $"{(value >= 0 ? "+" : "")}{value * 100:0.00}%";
    static AColor Direction(double value) => value >= 0 ? Teal : Red;

    void Tick()
    {
        long now = Now;
        double elapsed = (now - lastTick) / 1000.0;
        lastTick = now;
        if (auto && !lobby && game is not null && !fileBusy)
        {
            long previousSeason = S.Season;
            try { game.AdvanceFrame(Math.Min(elapsed, .5), speed); }
            catch (Exception e) when (GameStore.StorageException(e)) { auto = false; Notify("시장 진행을 멈췄습니다. 최근 정상 저장을 보존합니다."); }
            if (store.PendingCount > 512 || lastCommitTime > 0 && now-lastCommitTime > 15000 && saving)
            { auto = false; Notify("기록 저장을 기다리며 일시정지했습니다."); }
            if (now - lastSave >= 1000 || S.Season != previousSeason) Save(false);
            if (S.Season != previousSeason) Notify($"시즌 {previousSeason} 기록 완료 · 시즌 {S.Season} 시작");
            if(now-lastFrame>=game.Rules.Performance.FrameMilliseconds || S.Season!=previousSeason) Invalidate();
        }
        if (toastUntil > 0 && now > toastUntil) { toastUntil = 0; Invalidate(); }
        PostDelayed(Tick,game?.Rules.Performance.TickMilliseconds ?? defaultPerformance.TickMilliseconds);
    }

    protected override void OnDetachedFromWindow()
    {
        RemoveCallbacks(Tick);
        auto = false;
        base.OnDetachedFromWindow();
    }

    public void Pause()
    {
        auto = false;
        if (!fileBusy)
        {
            Save();
            try { saveTask.GetAwaiter().GetResult(); } catch { Notify("최근 확정 기록을 보존했습니다. 저장을 다시 시도하세요."); }
        }
        Invalidate();
    }
    void Save(bool force=true)
    {
        if (game is null || fileBusy) return;
        lastSave=Now;
        if(!force && preparedRun==S.RunId && preparedHour==S.CompletedMinutes && preparedTransaction==S.NextTransactionId && preparedEvent==S.NextCorporateEventId) return;
        try
        {
            var snapshot = store.PrepareSave(game);
            preparedRun=S.RunId; preparedHour=S.CompletedMinutes; preparedTransaction=S.NextTransactionId; preparedEvent=S.NextCorporateEventId;
            lock (saveGate)
            {
                pendingSave = snapshot;
                if (saving) return;
                saving = true;
            }
            saveTask = Task.Run(SaveWorker);
        }
        catch { Notify("저장 데이터를 준비하지 못했습니다."); }
    }
    void SaveWorker()
    {
        while (true)
        {
            SaveSnapshot snapshot;
            lock (saveGate)
            {
                if (pendingSave is null) { saving = false; return; }
                snapshot = pendingSave; pendingSave = null;
            }
            try
            {
                store.WriteSnapshot(snapshot);
                Post(() => { if (game is not null) GameStore.Acknowledge(game, snapshot); lastCommitTime = Now; });
            }
            catch
            {
                lock (saveGate) { pendingSave ??= snapshot; saving = false; }
                Post(() => { auto = false; Notify("저장 실패 · 진행을 멈췄습니다. 공간 확보 후 재개하세요."); });
                return;
            }
        }
    }

    void Start()
    {
        if (game is not null)
        {
            Pause();
            if (pendingSave is not null) { Notify("기존 기록을 저장한 뒤 새 시장을 시작하세요."); return; }
        }
        game = new GameEngine((uint)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        store.Attach(game);
        lastCommitTime = Now;
        lobby = false; auto = true; lastTick = Now; page = 0; scroll = 0; rankingMetric = RankingMetric.Return; comparisonPeriod = ComparisonPeriod.Season;
        companyStock = companyTab = 0; companySeason = 0; operationsTab = false;
        ownershipStock=ownershipPage=0;
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
        if(fileBusy) return true;
        if (help) help = false;
        else if (confirmNew) confirmNew = false;
        else if (portraitZoom >= 0) portraitZoom = -1;
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
        lastFrame=Now;
        c = canvas; scale = Width / 400f; h = Height / scale;
        c.Save(); c.Scale(scale, scale); c.DrawColor(Bg);
        targets.Clear(); clipTop = 0; clipBottom = h;
        if (fileBusy)
        {
            Text("기록 파일을 처리하고 있습니다", 200, h/2, 20, Ink, true, Paint.Align.Center);
            Text("시장은 일시정지합니다", 200, h/2+35, 12, Muted, false, Paint.Align.Center);
            c.Restore(); return;
        }
        if (lobby) DrawLobby();
        else if (game is not null)
        {
            DrawHeader();
            c.Save(); c.ClipRect(0, 101, 400, h - 145);
            clipTop = 101; clipBottom = h - 145;
            try
            {
                float end = page switch { 0 => DrawMarket(112 - scroll), 1 => DrawPortfolio(112 - scroll), 2 => DrawLeague(112 - scroll), 3 => DrawStatistics(112 - scroll), 4 => DrawSeasons(112 - scroll), 6 => DrawCompanyFinancials(112 - scroll), 7 => DrawOwnership(112 - scroll), 8=>DrawGovernance(112-scroll),9=>DrawBankruptcies(112-scroll),10=>DrawPeople(112-scroll),11=>DrawPersonProfile(112-scroll),12=>DrawPolitics(112-scroll),13=>DrawBusiness(112-scroll), _ => DrawNews(112 - scroll) };
                maxScroll = Math.Max(0, end + scroll - (h - 160));
            }
            catch(Exception e) when(GameStore.StorageException(e))
            {
                auto=false; maxScroll=scroll=0;
                Box(20,112,360,155,Card,16);
                Text("기록을 읽지 못했습니다",36,146,18,Ink,true);
                Wrap("진행을 멈췄습니다. 원본을 보존하며 정상 내보내기 파일로 복원할 수 있습니다.",36,174,328,11,Muted,20);
                Button("기록 파일 가져오기",36,217,328,36,()=>PickBackup(false),false);
            }
            c.Restore(); clipTop = 0; clipBottom = h;
            DrawFooter();
            if (scroll > maxScroll + 1) { scroll = maxScroll; PostInvalidate(); }
            if (selectedStock >= 0) DrawStockSheet();
            if (selectedTrader >= 0) DrawTraderSheet();
            if (showResult) DrawResults();
            if (portraitZoom>=0) DrawPortraitZoom();
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

    bool Visible(float y,float height) => y+height>=clipTop && y<=clipBottom;
    void Box(float x, float y, float w, float height, AColor color, float radius = 18, AColor? border = null)
    {
        if(!Visible(y,height)) return;
        paint.SetShader(null); paint.Color = color; paint.SetStyle(Paint.Style.Fill);
        c.DrawRoundRect(x, y, x + w, y + height, radius, radius, paint);
        if (border.HasValue)
        { paint.Color = border.Value; paint.StrokeWidth = 1; paint.SetStyle(Paint.Style.Stroke); c.DrawRoundRect(x, y, x + w, y + height, radius, radius, paint); paint.SetStyle(Paint.Style.Fill); }
    }

    void Text(string value, float x, float y, float size, AColor color, bool heavy = false, Paint.Align? align = null, bool headline = false)
    {
        if(!Visible(y-size*1.5f,size*2)) return;
        paint.SetShader(null); paint.Color = color; paint.TextSize = size; paint.SetStyle(Paint.Style.Fill);
        paint.SetTypeface(headline ? display : heavy ? bold : normal); paint.TextAlign = align ?? Paint.Align.Left;
        c.DrawText(value, x, y, paint);
    }

    void TextFit(string value, float x, float y, float size, AColor color, float width, bool heavy = false)
    {
        if(!Visible(y-size*1.5f,size*2)) return;
        paint.TextSize = size; paint.SetTypeface(heavy ? bold : normal);
        if (paint.MeasureText(value) > width)
        {
            int length=paint.BreakText(value,true,Math.Max(0,width-paint.MeasureText("…")),null);
            if(length>0 && char.IsHighSurrogate(value[length-1])) length--;
            value=value[..length]+"…";
        }
        Text(value, x, y, size, color, heavy);
    }

    float Wrap(string value, float x, float y, float width, float size, AColor color, float spacing = 21)
    {
        paint.TextSize=size; paint.SetTypeface(normal); int offset=0;
        while(offset<value.Length)
        {
            int newline=value.IndexOf('\n',offset); if(newline<0) newline=value.Length;
            if(newline==offset) { offset++; y+=spacing; continue; }
            string remaining=value[offset..newline];
            int length=Math.Max(1,paint.BreakText(remaining,true,width,null));
            if(length<remaining.Length && char.IsHighSurrogate(remaining[length-1])) length=length==1 ? Math.Min(2,remaining.Length) : length-1;
            Text(remaining[..Math.Min(length,remaining.Length)],x,y,size,color); y+=spacing; offset+=length;
            if(offset==newline && offset<value.Length) offset++;
        }
        return y;
    }

    void Line(float x1, float y1, float x2, float y2, AColor color, float width = 1)
    { paint.SetShader(null); paint.Color = color; paint.StrokeWidth = width; paint.StrokeCap = Paint.Cap.Round; c.DrawLine(x1, y1, x2, y2, paint); }

    void Circle(float x, float y, float radius, AColor color)
    { paint.SetShader(null); paint.SetStyle(Paint.Style.Fill); paint.Color = color; c.DrawCircle(x, y, radius, paint); }

    void Hit(float x, float y, float w, float height, Action action)
    {
        float top = Math.Max(y, clipTop), bottom = Math.Min(y + height, clipBottom);
        if (bottom > top) targets.Add((x,top,x+w,bottom,action));
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
        if(!Visible(y,height)) return;
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

    void Robot(int id, float x, float y, float size) => Portrait(S.Bots[id-1].PortraitId, x, y, size, size);

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
        else if (type == 3) { Box(x, y + 12, 5, 10, color, 1); Box(x + 8, y + 5, 5, 17, color, 1); Box(x + 16, y, 5, 22, color, 1); }
        else if (type == 4) { Box(x, y + 3, 22, 20, Bg, 3, color); Line(x, y + 10, x + 22, y + 10, color); Line(x + 6, y, x + 6, y + 6, color, 2); Line(x + 16, y, x + 16, y + 6, color, 2); Circle(x + 7, y + 16, 1.5f, color); Circle(x + 15, y + 16, 1.5f, color); }
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
                        if (x>=targets[i].Left && x<targets[i].Right && y>=targets[i].Top && y<targets[i].Bottom)
                        { var action = targets[i].Action; PerformHapticFeedback(FeedbackConstants.VirtualKey); action(); Invalidate(); break; }
                    PerformClick();
                }
                return true;
            case MotionEventActions.Cancel: dragging = false; return true;
        }
        return true;
    }
    public override bool PerformClick() { base.PerformClick(); return true; }
}
