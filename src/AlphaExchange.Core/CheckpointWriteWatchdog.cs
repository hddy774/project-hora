namespace AlphaExchange.Core;

// Measures an active checkpoint write, not time since the previous successful
// save. A long user pause must not look like a stalled write on resume.
public sealed class CheckpointWriteWatchdog
{
    long started = -1;

    public void Begin(long now) => Volatile.Write(ref started, now);
    public void Finish() => Volatile.Write(ref started, -1);
    public bool IsStalled(long now, long timeoutMilliseconds)
    {
        long activeStart = Volatile.Read(ref started);
        return activeStart >= 0 && now - activeStart > timeoutMilliseconds;
    }
}
