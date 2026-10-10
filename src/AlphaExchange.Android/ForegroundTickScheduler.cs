namespace AlphaExchange.App;

// The Android view drives this on its UI thread. Only IsRunning is read by the
// simulation worker, so suspension never needs to wait for the simulation lock.
internal sealed class ForegroundTickScheduler(Action schedule, Action cancel)
{
    bool attached, resumed, pending;
    volatile bool running;
    long lastTick;

    public bool IsRunning => running;
    public bool HasPendingTick => pending;

    public void SetAttached(bool value, long now)
    {
        attached = value;
        Update(now);
    }

    public void SetResumed(bool value, long now)
    {
        resumed = value;
        Update(now);
    }

    void Update(long now)
    {
        bool next = attached && resumed;
        if (next == running) return;
        running = next;
        lastTick = now;
        if (running) ScheduleNext();
        else
        {
            pending = false;
            cancel();
        }
    }

    public bool TryBeginTick(long now, out double elapsedSeconds)
    {
        elapsedSeconds = 0;
        if (!running || !pending) return false;
        pending = false;
        elapsedSeconds = Math.Max(0, now - lastTick) / 1000.0;
        lastTick = now;
        return true;
    }

    public void ResetClock(long now) => lastTick = now;

    public void ScheduleNext()
    {
        if (!running || pending) return;
        pending = true;
        schedule();
    }
}
