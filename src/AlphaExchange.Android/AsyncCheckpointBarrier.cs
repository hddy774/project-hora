namespace AlphaExchange.App;

// One instance per app process. Initial loads/migrations and final checkpoints
// register immediately and execute in order, always off the UI thread.
internal sealed class AsyncCheckpointBarrier
{
    readonly object gate = new();
    Task pending = Task.CompletedTask;

    public Task Enqueue(Func<Task> checkpoint)
    {
        lock (gate)
        {
            Task previous = pending;
            pending = Task.Run(async () =>
            {
                await ObserveCompletion(previous).ConfigureAwait(false);
                await checkpoint().ConfigureAwait(false);
            });
            return pending;
        }
    }

    public Task WaitForPendingAsync()
    {
        lock (gate) return ObserveCompletion(pending);
    }

    static async Task ObserveCompletion(Task checkpoint)
    {
        // A failed best-effort checkpoint must not strand the next Activity.
        // GameStore.Load will use its last valid transactional save/backup.
        try { await checkpoint.ConfigureAwait(false); }
        catch { }
    }
}
