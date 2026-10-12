using Android.Content;
using Android.Graphics;
using Android.Views;
using AlphaExchange.Core;

namespace AlphaExchange.App;

public sealed partial class GameView
{
    void Start()
    {
        if (fileBusy || loading || loadFailed) return;
        auto = false;
        fileBusy = true;
        Invalidate();
        // Register the transition immediately. A paused/recreated Activity must
        // wait for both the old run's save and the new run's initial checkpoint.
        _ = sessionStorage.Enqueue(async () =>
        {
            try
            {
                lock (simulationGate)
                {
                    saveRequested = forcedSaveRequested = false;
                    if (game is not null && !Save(duringTransition: true))
                        throw new IOException("Cannot capture the previous market.");
                }
                await checkpointWriter.Completion.ConfigureAwait(false);
                if (checkpointWriter.HasPending) throw new IOException("Previous checkpoint failed.");

                var created = new GameEngine((uint)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                store.Attach(created);
                lock (simulationGate)
                {
                    game = created;
                    ResetNavigation();
                    if (!Save(duringTransition: true)) throw new IOException("Cannot capture the new market.");
                }
                await checkpointWriter.Completion.ConfigureAwait(false);
                if (checkpointWriter.HasPending) throw new IOException("Initial checkpoint failed.");
                Post(() =>
                {
                    lock (simulationGate)
                    {
                        fileBusy = false;
                        auto = lifecycle.IsRunning;
                        lifecycle.ResetClock(Now);
                        Invalidate();
                    }
                });
            }
            catch (Exception error) when (GameStore.StorageException(error))
            {
                Post(() =>
                {
                    lock (simulationGate)
                    {
                        fileBusy = false;
                        auto = false;
                        Notify("기록을 저장하지 못했습니다. 공간 확보 후 다시 시도하세요.");
                    }
                });
            }
        });
    }

    void ResetNavigation()
    {
        lobby = false;
        page = ScreenId.Market;
        scroll = 0;
        rankingMetric = RankingMetric.Return;
        comparisonPeriod = ComparisonPeriod.Season;
        companyStock = companyTab = 0;
        companySeason = 0;
        operationsTab = false;
        ownershipStock = ownershipPage = 0;
        selectedStock = selectedTrader = -1;
        showResult = confirmNew = false;
        portfolioTab = 0;
        historyPage = selectedSeason = 0;
        seasonCache.Clear();
        companyReportCache.Clear();
        frameData.Clear();
        modalScroll.Reset();
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
        RequestSave(); Invalidate();
    }

    public bool GoBack()
    {
        lock (simulationGate) return GoBackLocked();
    }
    bool GoBackLocked()
    {
        if (fileBusy) return true;
        if (confirmNew) confirmNew = false;
        else if (help) help = false;
        else if (portraitZoom >= 0) portraitZoom = -1;
        else if (selectedStock >= 0) selectedStock = -1;
        else if (selectedTrader >= 0) selectedTrader = -1;
        else if (showResult) showResult = false;
        else if (!lobby && page is ScreenId.PersonProfile or ScreenId.Politics) SetPage(ScreenId.People);
        else if (!lobby && page == ScreenId.Business) { companyStock = businessStock; SetPage(ScreenId.CompanyFinancials); }
        else if (!lobby && page is ScreenId.CompanyFinancials or ScreenId.Ownership or ScreenId.Governance) SetPage(ScreenId.Market);
        else if (!lobby && page == ScreenId.Bankruptcies) SetPage(ScreenId.Statistics);
        else if (!lobby) { lobby = true; auto = false; RequestSave(); }
        else return false;
        ResetGesture(); targets.Clear(); modalScroll.Reset();
        Invalidate(); return true;
    }

    void Notify(string value) { toast = value; toastUntil = Now + 3000; Invalidate(); }
    void SetPage(ScreenId value) { page = value; scroll = 0; Invalidate(); }
}
