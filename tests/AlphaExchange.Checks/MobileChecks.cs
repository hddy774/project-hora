using AlphaExchange.App;
using AlphaExchange.Core;

internal static class MobileChecks
{
    public static void Run(Action<bool, string> check)
    {
        CheckLifecycle(check);
        CheckCheckpointOrdering(check);
        CheckModals(check);
    }

    static void CheckLifecycle(Action<bool, string> check)
    {
        int queued = 0, posted = 0, removed = 0;
        var ticks = new ForegroundTickScheduler(
            () => { queued++; posted++; check(queued == 1, "Mobile ticker has exactly one queued callback"); },
            () => { queued = 0; removed++; });
        check(!ticks.IsRunning && !ticks.HasPendingTick, "Ticker is idle before resume/attachment");
        ticks.SetResumed(true, 100);
        check(posted == 0, "Resume alone does not tick a detached view");
        ticks.SetAttached(true, 200);
        check(ticks.IsRunning && posted == 1, "Attached resumed view schedules first tick");
        ticks.SetAttached(true, 210);
        ticks.SetResumed(true, 220);
        ticks.ScheduleNext();
        check(posted == 1, "Repeated resume/attach/schedule does not duplicate callbacks");
        queued--;
        check(ticks.TryBeginTick(250, out double elapsed) && elapsed == .05, "Foreground tick measures time since attachment");
        check(!ticks.TryBeginTick(251, out _), "Already consumed callback cannot tick twice");
        ticks.ScheduleNext();
        ticks.SetResumed(false, 300);
        ticks.ScheduleNext();
        check(!ticks.IsRunning && !ticks.HasPendingTick && queued == 0 && removed == 1, "Suspension removes callbacks and prevents rescheduling");
        check(!ticks.TryBeginTick(100_000, out _), "Late callback cannot simulate in the background");
        ticks.SetResumed(false, 100_001);
        check(removed == 1, "Repeated suspension is idempotent");
        ticks.SetResumed(true, 100_100);
        queued--;
        check(ticks.TryBeginTick(100_150, out elapsed) && elapsed == .05, "Resume excludes all elapsed background time");
        ticks.SetResumed(false, 100_151);
        ticks.ScheduleNext();
        check(queued == 0, "Suspension during a tick prevents its next callback");
        ticks.SetResumed(true, 200_000);
        ticks.SetAttached(false, 200_001);
        check(!ticks.IsRunning && queued == 0, "Detach suspends a resumed activity");
        ticks.SetAttached(true, 300_000);
        queued--;
        check(ticks.TryBeginTick(300_050, out elapsed) && elapsed == .05, "Reattachment starts a fresh clock");
        ticks.ResetClock(300_055);
        ticks.ScheduleNext();
        queued--;
        check(ticks.TryBeginTick(300_060, out elapsed) && elapsed == .005, "Play/reset only queues newly elapsed time");
        ticks.SetResumed(false, 300_061);

        // The scheduler never mutates the engine on suspension. In particular,
        // completed minutes and foreground work already accepted by QueueTime
        // remain intact; a later explicit Play may continue that queued work.
        var game = new GameEngine(20261010);
        game.QueueTime(.25, 1);
        check(game.AdvanceQueuedMinute(), "Lifecycle fixture has a completed minute");
        long completed = game.State.CompletedMinutes;
        int pending = game.State.PendingClockMinutes;
        for (int cycle = 0; cycle < 50; cycle++)
        {
            long now = 1_000_000 + cycle * 100_000;
            ticks.SetResumed(true, now);
            ticks.SetResumed(true, now + 1);
            queued--;
            check(ticks.TryBeginTick(now + 50, out elapsed) && elapsed == .05, "Repeated foreground cycles never catch up background time");
            ticks.SetResumed(false, now + 51);
            ticks.ScheduleNext();
            check(queued == 0 && !ticks.IsRunning, "Repeated background cycles leave no callback");
        }
        check(game.State.CompletedMinutes == completed && game.State.PendingClockMinutes == pending,
            "Lifecycle scheduling preserves completed minutes and foreground queue");
    }

    static void CheckCheckpointOrdering(Action<bool, string> check)
    {
        var barrier = new AsyncCheckpointBarrier();
        var writing = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int savedMinute = 17;
        Task save = barrier.Enqueue(async () =>
        {
            writing.SetResult(true);
            await release.Task;
            savedMinute = 18;
        });
        check(writing.Task.Wait(TimeSpan.FromSeconds(10)), "Delayed checkpoint starts off the requesting thread");
        Task previous = barrier.WaitForPendingAsync();
        check(!save.IsCompleted && !previous.IsCompleted, "Suspension registration returns while slow storage is still pending");
        int loadedMinute = 0;
        Task load = barrier.Enqueue(() => { loadedMinute = savedMinute; return Task.CompletedTask; });
        check(!load.IsCompleted, "Replacement Activity cannot load before prior writer completes");
        release.SetResult(true);
        check(load.Wait(TimeSpan.FromSeconds(10)) && loadedMinute == 18, "Replacement Activity loads the newest completed minute");

        var failRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task failed = barrier.Enqueue(async () => { await failRelease.Task; throw new IOException("test storage failure"); });
        Task fallback = barrier.WaitForPendingAsync();
        check(!fallback.IsCompleted, "Even a failing checkpoint keeps replacement load ordered");
        failRelease.SetResult(true);
        check(fallback.Wait(TimeSpan.FromSeconds(10)) && failed.IsFaulted && savedMinute == 18,
            "Failed checkpoint releases replacement load and preserves the last valid minute");
        Task retry = barrier.Enqueue(() => { savedMinute = 19; return Task.CompletedTask; });
        check(retry.Wait(TimeSpan.FromSeconds(10)) && savedMinute == 19, "A failed checkpoint does not prevent later saves");

        var migrationRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task migration = barrier.Enqueue(async () => { await migrationRelease.Task; savedMinute = 20; });
        Task pauseDuringLoad = barrier.Enqueue(() => Task.CompletedTask);
        Task reload = barrier.Enqueue(() => { loadedMinute = savedMinute; return Task.CompletedTask; });
        check(!migration.IsCompleted && !pauseDuringLoad.IsCompleted && !reload.IsCompleted,
            "Pause and recreation wait for unfinished initial load/migration");
        migrationRelease.SetResult(true);
        check(reload.Wait(TimeSpan.FromSeconds(10)) && loadedMinute == 20, "Replacement load follows completed migration even when no engine was available to save");
    }

    static void CheckModals(Action<bool, string> check)
    {
        foreach (float height in new[] { 480f, 600f, 800f })
        foreach (var sheet in new[] { (Key: "help", Height: 654f, Footer: 50f), (Key: "stock:0", Height: 664f, Footer: 45f), (Key: "trader:1", Height: 630f, Footer: 52f) })
        {
            float desiredHeight = sheet.Key == "help" ? sheet.Height : Math.Min(sheet.Height, height - 16);
            var viewport = ModalViewport.Create(height, desiredHeight, sheet.Footer);
            check(viewport.Top >= 12 && viewport.BodyHeight > 0, $"{sheet.Key} has a visible body at {height}");
            check(viewport.BodyTop >= viewport.Top + 57, "Body cannot cover the close button");
            check(viewport.BodyBottom < viewport.FooterTop && viewport.FooterBottom <= height, "Pinned CTA stays inside the screen and outside the body");
            check(viewport.FooterBottom - viewport.FooterTop == sheet.Footer, "CTA preserves its full touch height");
            check(!viewport.ContainsBody(200, viewport.Top + 34) && !viewport.ContainsBody(200, viewport.FooterTop + 10), "Header/footer gestures cannot drag the body");
            check(viewport.ContainsBody(200, viewport.BodyTop + 10) && !viewport.ContainsBody(1, viewport.BodyTop + 10), "Only the modal body accepts its drag");
            check(viewport.ClipTarget(viewport.Top, viewport.BodyTop) is null, "Fully clipped content has no target over close");
            check(viewport.ClipTarget(viewport.BodyBottom, height) is null, "Fully clipped content has no target over CTA");
            var clipped = viewport.ClipTarget(viewport.BodyTop - 20, viewport.BodyBottom + 20);
            check(clipped is { } bounds && bounds.Top == viewport.BodyTop && bounds.Bottom == viewport.BodyBottom, "Partly visible targets match the visible body");
            var scroll = new ModalScrollState();
            for (int cycle = 0; cycle < 25; cycle++)
            {
                scroll.Show(sheet.Key, viewport);
                check(scroll.Offset == 0, "Reopened modal starts at the top");
                scroll.SetContentHeight(720);
                scroll.Drag(10_000);
                check(scroll.Offset == scroll.Maximum && scroll.Maximum > 0, "Long modal content reaches its bottom");
                check(Math.Abs(viewport.BodyTop + 720 - scroll.Offset - viewport.BodyBottom) < .01f, "Last body content is reachable above pinned CTA");
                scroll.Show(sheet.Key, viewport);
                check(scroll.Offset == scroll.Maximum, "Redraw preserves modal scroll");
                scroll.Drag(-10_000);
                check(scroll.Offset == 0, "Modal scroll clamps at top");
                scroll.Drag(50);
                scroll.Show(sheet.Key + ":next", viewport);
                check(scroll.Offset == 0 && scroll.Maximum == 0, "Changing modal identity discards stale scroll bounds");
                scroll.SetContentHeight(720);
                scroll.Drag(50);
                check(scroll.SetContentHeight(10) && scroll.Offset == 0 && scroll.Maximum == 0, "Shrinking content clamps scroll safely");
                scroll.Reset();
                check(scroll.Key == "" && scroll.Offset == 0 && scroll.Maximum == 0, "Dismissal clears modal gesture identity and bounds");
            }
        }
    }
}
