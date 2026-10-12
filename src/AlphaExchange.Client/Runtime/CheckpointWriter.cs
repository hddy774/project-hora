using AlphaExchange.Core;

namespace AlphaExchange.Client;

// The caller freezes state under its simulation gate. This worker sees only
// immutable checkpoints, serializes writes and coalesces superseded captures.
internal sealed class CheckpointWriter(GameStore store, Action<SaveSnapshot> committed, Action failed)
{
    readonly object gate = new();
    readonly CheckpointWriteWatchdog watchdog = new();
    SaveSnapshot? pending;
    bool writing;
    Task completion = Task.CompletedTask;

    public bool HasPending { get { lock (gate) return pending is not null; } }
    public Task Completion { get { lock (gate) return completion; } }
    public bool IsStalled(long now, long timeout) => watchdog.IsStalled(now, timeout);

    public void Request(SaveSnapshot snapshot)
    {
        lock (gate)
        {
            pending = snapshot;
            if (writing) return;
            writing = true;
            watchdog.Begin(Environment.TickCount64);
            completion = Task.Run(WritePending);
        }
    }

    void WritePending()
    {
        while (true)
        {
            SaveSnapshot snapshot;
            lock (gate)
            {
                if (pending is null) { writing = false; watchdog.Finish(); return; }
                snapshot = pending;
                pending = null;
                watchdog.Begin(Environment.TickCount64);
            }
            try
            {
                store.WriteSnapshot(snapshot);
                committed(snapshot);
            }
            catch (Exception error) when (GameStore.StorageException(error))
            {
                lock (gate)
                {
                    pending ??= snapshot;
                    writing = false;
                    watchdog.Finish();
                }
                failed();
                return;
            }
        }
    }
}
