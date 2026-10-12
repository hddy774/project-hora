using Android.Content;
using Android.Graphics;
using Android.Views;
using AlphaExchange.Core;

namespace AlphaExchange.App;

public sealed partial class GameView
{
    void Tick()
    {
        if (!lifecycle.TryBeginTick(Now, out double elapsed)) return;
        try { lock (simulationGate) TickLocked(elapsed); }
        finally { lifecycle.ScheduleNext(); }
    }
    void TickLocked(double elapsed)
    {
        long now = Now;
        if (lifecycle.IsRunning && auto && !lobby && game is not null && !fileBusy)
        {
            try { game.QueueTime(Math.Clamp(elapsed, 0, 3600), speed); StartSimulationWorker(); }
            catch (Exception e) when (GameStore.StorageException(e)) { auto = false; Notify("시장 진행을 멈췄습니다. 최근 정상 저장을 보존합니다."); }
            if (store.PendingCount > 512 || checkpointWriter.IsStalled(now, 15000))
            { auto = false; Notify("기록 저장을 기다리며 일시정지했습니다."); }
            if (now - lastSave >= 1000) { saveRequested = true; lastSave = now; StartSimulationWorker(); }
            if (now - lastFrame >= game.Rules.Performance.FrameMilliseconds) Invalidate();
        }
        if (toastUntil > 0 && now > toastUntil) { toastUntil = 0; Invalidate(); }
    }
    void StartSimulationWorker()
    {
        if (simulating) return;
        simulating = true;
        _ = Task.Run(() =>
        {
            try
            {
                while (true)
                {
                    lock (simulationGate)
                    {
                        if (!lifecycle.IsRunning || game is null || fileBusy) { simulating = false; return; }
                        if (saveRequested)
                        {
                            bool force = forcedSaveRequested;
                            saveRequested = forcedSaveRequested = false;
                            Save(force);
                        }
                        if (!auto || lobby || !game.AdvanceQueuedMinute()) { simulating = false; return; }
                    }
                    // Give drawing/input a chance between minutes, including at 100x.
                    Thread.Yield();
                }
            }
            catch (Exception e) when (GameStore.StorageException(e))
            {
                lock (simulationGate) { auto = false; simulating = false; }
                Post(() => Notify("시장 진행을 멈췄습니다. 최근 정상 저장을 보존합니다."));
            }
        });
    }
}
