using Android.Content;
using Android.Graphics;
using Android.Views;
using AlphaExchange.Core;

namespace AlphaExchange.App;

public sealed partial class GameView
{
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
                saveRequested = forcedSaveRequested = false;
                Save();
                write = checkpointWriter.Completion;
            }
            // The next Activity must wait for disk completion, not just snapshot
            // preparation, before it loads and starts its own writer.
            await write.ConfigureAwait(false);
        });
    }
}
