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
    static readonly AsyncCheckpointBarrier sessionStorage = new();
    readonly GameStore store;
    readonly Action tickCallback;
    readonly ForegroundTickScheduler lifecycle;
    readonly ModalScrollState modalScroll = new();
    readonly object simulationGate=new();
    bool simulating,saveRequested;
    long lastSave;
    string preparedRun="";
    long preparedHour=-1,preparedTransaction=-1,preparedEvent=-1;
    readonly object saveGate = new();
    SaveSnapshot? pendingSave;
    bool saving;
    Task saveTask = Task.CompletedTask;
    bool fileBusy,loading=true,loadFailed;
    readonly CheckpointWriteWatchdog writeWatchdog=new();
    int portfolioTab;
    long historyPage, selectedSeason;
    Canvas c = null!;
    GameEngine? game;
    bool lobby = true, showResult, help, confirmNew;
    volatile bool auto;
    int page, selectedStock = -1, selectedTrader = -1, speed = 1;
    RankingMetric rankingMetric;
    ComparisonPeriod comparisonPeriod;
    int companyStock, companyTab;
    long companySeason;
    bool operationsTab;
    float scale = 1, h = 800, scroll, maxScroll, downY, lastY, downX;
    bool dragging, gestureActive, modalGesture;
    string gestureModal = "";
    string toast = "";
    long toastUntil,lastFrame;
    float clipTop = 0, clipBottom = 100000;
    static readonly AColor Bg = Hex("#0B111B"), Card = Hex("#141E2B"), Card2 = Hex("#1C2939"), Stroke = Hex("#273446"), Ink = Hex("#F4F7F4"), Muted = Hex("#8493A7"), Lime = Hex("#DCFF7D"), Teal = Hex("#65DDC1"), Red = Hex("#FF8996");
    static readonly string[] Palette = ["#DCFF7D", "#C2AFFA", "#65DDC1", "#FFA97D", "#91BCFF", "#FF95BA", "#ADE7AD", "#E5CD84", "#A0D9FF", "#D6B0FF"];
    static readonly AColor[] PaletteColors=Palette.Select(Hex).ToArray();
    static readonly string BuildVersion=typeof(GameView).Assembly.GetName().Version!.ToString(3);
    AlphaExchange.Core.GameState S => game!.State;
    Trader Focus => game!.FocusTrader;

    public GameView(Context context) : base(context)
    {
        tickCallback = Tick;
        lifecycle = new ForegroundTickScheduler(
            () => PostDelayed(tickCallback, game?.Rules.Performance.TickMilliseconds ?? defaultPerformance.TickMilliseconds),
            () => RemoveCallbacks(tickCallback));
        SetLayerType(LayerType.Hardware, null);
        Focusable = true;
        ContentDescription = "알파 익스체인지, 오프라인 주식 전략 게임";
        store = new GameStore(context.FilesDir!.AbsolutePath);
        try { using var stream = context.Assets!.Open("arena.png"); arena = BitmapFactory.DecodeStream(stream); } catch { }
        LoadPortraitManifest();
        fileBusy=true;
        // Serialize initial load/migration too: a recreated Activity must not
        // overlap an older Activity that was paused while it was still loading.
        _=sessionStorage.Enqueue(()=>
        {
            GameEngine? loaded=null; string message=""; bool failed=false;
            try { loaded=store.Load(out message); }
            catch(Exception e) when(GameStore.StorageException(e)) { failed=true; message="기록 불러오기 실패 · 앱을 다시 열어주세요. 기존 파일은 보존했습니다."; }
            Post(()=> { lock(simulationGate) { game=loaded; loading=false; loadFailed=failed; fileBusy=false; lifecycle.ResetClock(Now); if(message.Length>0) Notify(message); Invalidate(); } });
            return Task.CompletedTask;
        });
    }

    static long Now => System.Environment.TickCount64;
    static AColor Hex(string value) => AColor.ParseColor(value);
    static string Money(long amount) => amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
    string ShortMoney(long amount) => game?.State.World?.NumberFormat==1 ? KoreanNumber.Full(amount) : KoreanNumber.Compact(amount);
    static string Percent(double value) => $"{(value >= 0 ? "+" : "")}{value * 100:0.00}%";
    static AColor Direction(double value) => value >= 0 ? Teal : Red;

    void Tick()
    {
        if (!lifecycle.TryBeginTick(Now, out double elapsed)) return;
        try { lock(simulationGate) TickLocked(elapsed); }
        finally { lifecycle.ScheduleNext(); }
    }
    void TickLocked(double elapsed)
    {
        long now = Now;
        if (lifecycle.IsRunning && auto && !lobby && game is not null && !fileBusy)
        {
            try { game.QueueTime(Math.Clamp(elapsed,0,3600), speed); StartSimulationWorker(); }
            catch (Exception e) when (GameStore.StorageException(e)) { auto = false; Notify("시장 진행을 멈췄습니다. 최근 정상 저장을 보존합니다."); }
            if (store.PendingCount > 512 || writeWatchdog.IsStalled(now,15000))
            { auto = false; Notify("기록 저장을 기다리며 일시정지했습니다."); }
            if (now - lastSave >= 1000) { saveRequested=true; lastSave=now; StartSimulationWorker(); }
            if(now-lastFrame>=game.Rules.Performance.FrameMilliseconds) Invalidate();
        }
        if (toastUntil > 0 && now > toastUntil) { toastUntil = 0; Invalidate(); }
    }
    void StartSimulationWorker()
    {
        if(simulating) return;
        simulating=true;
        _=Task.Run(()=>
        {
            try
            {
                while(true)
                {
                    lock(simulationGate)
                    {
                        if(!lifecycle.IsRunning || game is null || fileBusy) { simulating=false; return; }
                        if(saveRequested) { saveRequested=false; Save(false); }
                        if(!auto || lobby || !game.AdvanceQueuedMinute()) { simulating=false; return; }
                    }
                    // Give drawing/input a chance between minutes, including at 100x.
                    Thread.Yield();
                }
            }
            catch(Exception e) when(GameStore.StorageException(e))
            {
                lock(simulationGate) { auto=false; simulating=false; }
                Post(()=>Notify("시장 진행을 멈췄습니다. 최근 정상 저장을 보존합니다."));
            }
        });
    }

    protected override void OnAttachedToWindow()
    {
        base.OnAttachedToWindow();
        lifecycle.SetAttached(true, Now);
    }

    protected override void OnDetachedFromWindow()
    {
        bool wasRunning = lifecycle.IsRunning;
        lifecycle.SetAttached(false, Now);
        StopForLifecycle(wasRunning);
        base.OnDetachedFromWindow();
    }

    public void Resume()
    {
        ResetGesture();
        lifecycle.SetResumed(true, Now);
        Invalidate();
    }

    public void Suspend()
    {
        // Android lifecycle callbacks must not wait for a minute, checkpoint copy,
        // serialization, or disk I/O. An in-flight minute may finish; the worker
        // checks this gate before starting another. Foreground queued time remains.
        bool wasRunning = lifecycle.IsRunning;
        lifecycle.SetResumed(false, Now);
        StopForLifecycle(wasRunning);
    }

    void StopForLifecycle(bool wasRunning)
    {
        auto = false;
        ResetGesture();
        targets.Clear();
        if (!wasRunning) return;
        _ = sessionStorage.Enqueue(async () =>
        {
            Task write;
            lock (simulationGate)
            {
                saveRequested = false;
                Save();
                write = saveTask;
            }
            // The next Activity must wait for disk completion, not just snapshot
            // preparation, before it loads and starts its own writer.
            await write.ConfigureAwait(false);
        });
    }

    bool PauseForNewMarket()
    {
        // Switching runs still needs a save barrier; lifecycle suspension does not.
        auto = false;
        if (fileBusy || !Save()) return false;
        try { saveTask.GetAwaiter().GetResult(); }
        catch { return false; }
        lock (saveGate) return pendingSave is null;
    }
    bool Save(bool force=true)
    {
        if (game is null || fileBusy) return false;
        lastSave=Now;
        if(!force && preparedRun==S.RunId && preparedHour==S.CompletedMinutes && preparedTransaction==S.NextTransactionId && preparedEvent==S.NextCorporateEventId) return true;
        try
        {
            var snapshot = store.PrepareSave(game);
            preparedRun=S.RunId; preparedHour=S.CompletedMinutes; preparedTransaction=S.NextTransactionId; preparedEvent=S.NextCorporateEventId;
            lock (saveGate)
            {
                pendingSave = snapshot;
                if (saving) return true;
                saving = true;
                writeWatchdog.Begin(Now);
            }
            saveTask = Task.Run(SaveWorker);
            return true;
        }
        catch { Post(() => Notify("저장 데이터를 준비하지 못했습니다.")); return false; }
    }
    void SaveWorker()
    {
        while (true)
        {
            SaveSnapshot snapshot;
            lock (saveGate)
            {
                if (pendingSave is null) { saving = false; writeWatchdog.Finish(); return; }
                snapshot = pendingSave; pendingSave = null;
                writeWatchdog.Begin(Now);
            }
            try
            {
                store.WriteSnapshot(snapshot);
                Post(() => { lock(simulationGate) { if (game is not null) GameStore.Acknowledge(game, snapshot); } });
            }
            catch
            {
                lock (saveGate) { pendingSave ??= snapshot; saving = false; writeWatchdog.Finish(); }
                Post(() => { lock(simulationGate) { auto = false; Notify("저장 실패 · 진행을 멈췄습니다. 공간 확보 후 재개하세요."); } });
                return;
            }
        }
    }

    void Start()
    {
        if(fileBusy || loading || loadFailed) return;
        if (game is not null)
        {
            if (!PauseForNewMarket()) { Notify("기존 기록을 저장한 뒤 새 시장을 시작하세요."); return; }
        }
        game = new GameEngine((uint)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        store.Attach(game);
        lobby = false; auto = lifecycle.IsRunning; lifecycle.ResetClock(Now); page = 0; scroll = 0; rankingMetric = RankingMetric.Return; comparisonPeriod = ComparisonPeriod.Season;
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
        lifecycle.ResetClock(Now);
        auto = lifecycle.IsRunning && !auto;
        Save(); Invalidate();
    }

    public bool GoBack()
    {
        lock(simulationGate) return GoBackLocked();
    }
    bool GoBackLocked()
    {
        if(fileBusy) return true;
        if (confirmNew) confirmNew = false;
        else if (help) help = false;
        else if (portraitZoom >= 0) portraitZoom = -1;
        else if (selectedStock >= 0) selectedStock = -1;
        else if (selectedTrader >= 0) selectedTrader = -1;
        else if (showResult) showResult = false;
        else if (!lobby && page is 11 or 12) SetPage(10);
        else if (!lobby && page == 13) { companyStock=businessStock; SetPage(6); }
        else if (!lobby && page is 6 or 7 or 8) SetPage(0);
        else if (!lobby && page == 9) SetPage(3);
        else if (!lobby) { lobby = true; auto = false; Save(); }
        else return false;
        ResetGesture(); targets.Clear(); modalScroll.Reset();
        Invalidate(); return true;
    }

    void Notify(string value) { toast = value; toastUntil = Now + 3000; Invalidate(); }
    void SetPage(int value) { page = value; scroll = 0; Invalidate(); }

    protected override void OnDraw(Canvas canvas)
    {
        lock(simulationGate) DrawLocked(canvas);
    }
    void DrawLocked(Canvas canvas)
    {
        base.OnDraw(canvas);
        lastFrame=Now;
        c = canvas; scale = Width / 400f; h = Height / scale;
        DescribeScreen();
        c.Save(); c.Scale(scale, scale); c.DrawColor(Bg);
        targets.Clear(); clipTop = 0; clipBottom = h;
        if (fileBusy && !loading)
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
        }
        // Draw only the top overlay: its scroll state and hit targets must not
        // be replaced by a covered modal, or leak through to the page below.
        if (confirmNew) DrawConfirmation();
        else if (help) DrawHelp();
        else if (!lobby && game is not null && portraitZoom >= 0) DrawPortraitZoom();
        else if (!lobby && game is not null && showResult) DrawResults();
        else if (!lobby && game is not null && selectedTrader >= 0) DrawTraderSheet();
        else if (!lobby && game is not null && selectedStock >= 0) DrawStockSheet();
        else modalScroll.Reset();
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
        var clipped = ModalViewport.ClipTarget(y, y + height, clipTop, clipBottom);
        if (clipped is { } bounds) targets.Add((x,bounds.Top,x+w,bounds.Bottom,()=> { lock(simulationGate) action(); }));
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
        else if (type == 3) { Circle(x+11,y+6,5,color); Line(x+3,y+22,x+3,y+16,color,2); Line(x+3,y+16,x+11,y+12,color,2); Line(x+11,y+12,x+19,y+16,color,2); Line(x+19,y+16,x+19,y+22,color,2); }
        else if (type == 4) { Box(x, y + 12, 5, 10, color, 1); Box(x + 8, y + 5, 5, 17, color, 1); Box(x + 16, y, 5, 22, color, 1); }
        else if (type == 5) { Box(x, y + 3, 22, 20, Bg, 3, color); Line(x, y + 10, x + 22, y + 10, color); Line(x + 6, y, x + 6, y + 6, color, 2); Line(x + 16, y, x + 16, y + 6, color, 2); Circle(x + 7, y + 16, 1.5f, color); Circle(x + 15, y + 16, 1.5f, color); }
        else { Box(x, y + 1, 22, 21, Bg, 4, color); Line(x + 5, y + 7, x + 17, y + 7, color, 1.5f); Line(x + 5, y + 12, x + 17, y + 12, color, 1.5f); Line(x + 5, y + 17, x + 12, y + 17, color, 1.5f); }
    }

    bool HasModal => help || confirmNew || portraitZoom >= 0 || selectedStock >= 0 || selectedTrader >= 0 || showResult;

    void ResetGesture()
    {
        dragging = gestureActive = modalGesture = false;
        gestureModal = "";
        downX = downY = lastY = 0;
    }

    public override bool OnTouchEvent(MotionEvent? e)
    {
        if (!lifecycle.IsRunning) return false;
        lock(simulationGate) return TouchLocked(e);
    }
    bool TouchLocked(MotionEvent? e)
    {
        if (e is null) return false;
        float x = e.GetX() / scale, y = e.GetY() / scale;
        switch (e.ActionMasked)
        {
            case MotionEventActions.Down:
                downX = x; downY = lastY = y; dragging = false; gestureActive = true;
                modalGesture = HasModal;
                gestureModal = modalScroll.Key;
                return true;
            case MotionEventActions.Move:
                if (!gestureActive) return true;
                if (Math.Abs(y - downY) > 7 || Math.Abs(x - downX) > 7) dragging = true;
                if (dragging)
                {
                    if (modalGesture && HasModal && gestureModal.Length > 0 && gestureModal == modalScroll.Key && modalScroll.Viewport.ContainsBody(downX, downY))
                    { modalScroll.Drag(lastY - y); Invalidate(); }
                    else if (!modalGesture && !HasModal && !lobby && downY > 102 && downY < h - 145)
                    { scroll = Math.Clamp(scroll + lastY - y, 0, maxScroll); Invalidate(); }
                }
                lastY = y; return true;
            case MotionEventActions.Up:
                bool click = gestureActive && !dragging && modalGesture == HasModal && Math.Abs(x - downX) < 18 && Math.Abs(y - downY) < 18;
                ResetGesture();
                if (click)
                {
                    for (int i = targets.Count - 1; i >= 0; i--)
                        if (x>=targets[i].Left && x<targets[i].Right && y>=targets[i].Top && y<targets[i].Bottom)
                        {
                            var action = targets[i].Action;
                            // Do not let a second tap reuse a dismissed modal's targets
                            // before Android draws the next frame.
                            targets.Clear();
                            PerformHapticFeedback(FeedbackConstants.VirtualKey); action(); Invalidate(); break;
                        }
                    PerformClick();
                }
                return true;
            case MotionEventActions.PointerDown:
            case MotionEventActions.Cancel: ResetGesture(); return true;
        }
        return true;
    }
    public override bool PerformClick() { base.PerformClick(); return true; }
}
