namespace Chronicles.Game.Execution;

// Only copied values escape a lease. The backing arrays never do.
internal sealed class ScopedLease<T>(int count, Func<int, T> read, Action validate, Action release) : IDisposable
{
    private bool disposed;
    public int Count { get { Check(); return count; } }
    public T At(int index)
    {
        Check();
        if ((uint)index >= (uint)count) throw new ArgumentOutOfRangeException(nameof(index));
        return read(index);
    }
    private void Check()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        validate();
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; release(); GC.SuppressFinalize(this);
    }
}
