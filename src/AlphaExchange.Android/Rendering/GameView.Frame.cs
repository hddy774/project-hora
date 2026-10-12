using Android.Content;
using Android.Graphics;
using Android.Views;
using AlphaExchange.Core;

namespace AlphaExchange.App;

public sealed partial class GameView
{
    protected override void OnDraw(Canvas canvas)
    {
        lock (simulationGate) DrawLocked(canvas);
    }
    void DrawLocked(Canvas canvas)
    {
        base.OnDraw(canvas);
        if (graphicsReleased) return;
        lastFrame = Now;
        c = canvas; scale = Width / 400f; h = Height / scale;
        DescribeScreen();
        c.Save(); c.Scale(scale, scale); c.DrawColor(Bg);
        targets.Clear(); clipTop = 0; clipBottom = h;
        if (fileBusy && !loading)
        {
            Text("기록 파일을 처리하고 있습니다", 200, h / 2, 20, Ink, true, Paint.Align.Center);
            Text("시장은 일시정지합니다", 200, h / 2 + 35, 12, Muted, false, Paint.Align.Center);
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
                float end = DrawPage(112 - scroll);
                maxScroll = Math.Max(0, end + scroll - (h - 160));
            }
            catch (Exception e) when (GameStore.StorageException(e))
            {
                auto = false; maxScroll = scroll = 0;
                Box(20, 112, 360, 155, Card, 16);
                Text("기록을 읽지 못했습니다", 36, 146, 18, Ink, true);
                Wrap("진행을 멈췄습니다. 원본을 보존하며 정상 내보내기 파일로 복원할 수 있습니다.", 36, 174, 328, 11, Muted, 20);
                Button("기록 파일 가져오기", 36, 217, 328, 36, () => PickBackup(false), false);
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

    float DrawPage(float y) => page switch
    {
        ScreenId.Market => DrawMarket(y),
        ScreenId.Portfolio => DrawPortfolio(y),
        ScreenId.Ranking => DrawLeague(y),
        ScreenId.Statistics => DrawStatistics(y),
        ScreenId.Seasons => DrawSeasons(y),
        ScreenId.CompanyFinancials => DrawCompanyFinancials(y),
        ScreenId.Ownership => DrawOwnership(y),
        ScreenId.Governance => DrawGovernance(y),
        ScreenId.Bankruptcies => DrawBankruptcies(y),
        ScreenId.People => DrawPeople(y),
        ScreenId.PersonProfile => DrawPersonProfile(y),
        ScreenId.Politics => DrawPolitics(y),
        ScreenId.Business => DrawBusiness(y),
        _ => DrawNews(y)
    };
}
