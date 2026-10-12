using AlphaExchange.Client;
using Android.Content;
using Android.Graphics;
using Android.Views;
using AlphaExchange.Core;
using AColor = Android.Graphics.Color;

namespace AlphaExchange.App;

public sealed partial class GameView : View
{
    readonly Paint paint = new(PaintFlags.AntiAlias);
    readonly Typeface normal = Typeface.Create("sans-serif", TypefaceStyle.Normal)!;
    readonly Typeface bold = Typeface.Create("sans-serif-medium", TypefaceStyle.Normal)!;
    readonly Typeface display = Typeface.Create("sans-serif-black", TypefaceStyle.Normal)!;
    Bitmap? arena;
    readonly List<(float Left, float Top, float Right, float Bottom, Action Action)> targets = [];
    readonly PerformanceRules defaultPerformance = SimulationRules.Default().Performance;
    static readonly AsyncCheckpointBarrier sessionStorage = new();
    readonly GameStore store;
    readonly CheckpointWriter checkpointWriter;
    readonly CorporateHistoryCache corporateHistory;
    readonly Action tickCallback;
    readonly ForegroundTickScheduler lifecycle;
    readonly ModalScrollState modalScroll = new();
    readonly object simulationGate = new();
    bool simulating, saveRequested, forcedSaveRequested;
    long lastSave;
    string preparedRun = "";
    long preparedMinute = -1, preparedTransaction = -1, preparedEvent = -1;
    bool fileBusy, loading = true, loadFailed;
    int portfolioTab;
    long historyPage, selectedSeason;
    Canvas c = null!;
    GameEngine? game;
    bool lobby = true, showResult, help, confirmNew;
    volatile bool auto;
    ScreenId page;
    int selectedStock = -1, selectedTrader = -1, speed = 1;
    RankingMetric rankingMetric;
    ComparisonPeriod comparisonPeriod;
    int companyStock, companyTab;
    long companySeason;
    bool operationsTab;
    float scale = 1, h = 800, scroll, maxScroll, downY, lastY, downX;
    bool dragging, gestureActive, modalGesture;
    string gestureModal = "";
    string toast = "";
    long toastUntil, lastFrame;
    float clipTop = 0, clipBottom = 100000;
    static readonly AColor Bg = Hex("#0B111B"), Card = Hex("#141E2B"), Card2 = Hex("#1C2939"), Stroke = Hex("#273446"), Ink = Hex("#F4F7F4"), Muted = Hex("#8493A7"), Lime = Hex("#DCFF7D"), Teal = Hex("#65DDC1"), Red = Hex("#FF8996");
    static readonly string[] Palette = ["#DCFF7D", "#C2AFFA", "#65DDC1", "#FFA97D", "#91BCFF", "#FF95BA", "#ADE7AD", "#E5CD84", "#A0D9FF", "#D6B0FF"];
    static readonly AColor[] PaletteColors = Palette.Select(Hex).ToArray();
    static readonly string BuildVersion = typeof(GameView).Assembly.GetName().Version!.ToString(3);
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
        corporateHistory = new CorporateHistoryCache(store);
        checkpointWriter = new CheckpointWriter(store,
            snapshot => Post(() => { lock (simulationGate) { if (game is not null) GameStore.Acknowledge(game, snapshot); } }),
            () => Post(() => { lock (simulationGate) { auto = false; Notify("저장 실패 · 진행을 멈췄습니다. 공간 확보 후 재개하세요."); } }));
        LoadPortraitManifest();
        fileBusy = true;
        // Serialize initial load/migration too: a recreated Activity must not
        // overlap an older Activity that was paused while it was still loading.
        _ = sessionStorage.Enqueue(() =>
        {
            var artwork = DecodeArena();
            GameEngine? loaded = null; string message = ""; bool failed = false;
            try { loaded = store.Load(out message); }
            catch (Exception e) when (GameStore.StorageException(e)) { failed = true; message = "기록 불러오기 실패 · 앱을 다시 열어주세요. 기존 파일은 보존했습니다."; }
            Post(() =>
            {
                lock (simulationGate)
                {
                    if (graphicsReleased) return;
                    arena = artwork;
                    game = loaded;
                    loading = false;
                    loadFailed = failed;
                    fileBusy = false;
                    lifecycle.ResetClock(Now);
                    if (message.Length > 0) Notify(message);
                    Invalidate();
                }
            });
            return Task.CompletedTask;
        });
    }

    static long Now => System.Environment.TickCount64;
    static AColor Hex(string value) => AColor.ParseColor(value);
    static string Money(long amount) => amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
    string ShortMoney(long amount) => game?.State.Environment.NumberFormat == 1 ? KoreanNumber.Full(amount) : KoreanNumber.Compact(amount);
    static string Percent(double value) => $"{(value >= 0 ? "+" : "")}{value * 100:0.00}%";
    static AColor Direction(double value) => value >= 0 ? Teal : Red;
}
