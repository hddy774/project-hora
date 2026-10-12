using AlphaExchange.Client;
using AlphaExchange.Core;

static class CheckpointWriterChecks
{
    public static void Run(Action<bool, string> check)
    {
        string folder = Path.Combine(Path.GetTempPath(), "hora-writer-" + Guid.NewGuid().ToString("N"));
        using var firstWritten = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        try
        {
            var game = new GameEngine(123);
            var store = new GameStore(folder);
            int commits = 0, failures = 0;
            var writer = new CheckpointWriter(store, snapshot =>
            {
                GameStore.Acknowledge(game, snapshot);
                if (Interlocked.Increment(ref commits) != 1) return;
                firstWritten.Set();
                if (!release.Wait(TimeSpan.FromSeconds(15))) throw new IOException("Test checkpoint stalled.");
            }, () => Interlocked.Increment(ref failures));
            writer.Request(store.PrepareSave(game));
            check(firstWritten.Wait(TimeSpan.FromSeconds(15)), "Initial checkpoint commits off-thread");
            game.AdvanceMinute();
            writer.Request(store.PrepareSave(game));
            game.AdvanceMinute();
            writer.Request(store.PrepareSave(game));
            release.Set();
            writer.Completion.GetAwaiter().GetResult();
            check(commits == 2 && failures == 0 && !writer.HasPending, "Superseded pending checkpoint coalesces");
            check(store.Load(out _)!.State.CompletedMinutes == 2, "Newest immutable checkpoint is durable");

            writer.Request(new SaveSnapshot("wrong-run", 0, game.Serialize(), [], []));
            writer.Completion.GetAwaiter().GetResult();
            check(failures == 1 && writer.HasPending, "Failed capture stays available for retry");
            writer.Request(store.PrepareSave(game));
            writer.Completion.GetAwaiter().GetResult();
            check(!writer.HasPending && store.Load(out _)!.State.CompletedMinutes == 2,
                "A valid retry recovers without reverting committed time");
        }
        finally
        {
            release.Set();
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }
}
