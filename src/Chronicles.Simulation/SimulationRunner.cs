using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed class SimulationRunner : IDisposable
{
    private const int BaseTicksPerPulse = 20;
    private const int PulseMilliseconds = 100;
    private readonly object _worldGate = new();
    private World _world;
    private readonly ManualResetEventSlim _stop = new(initialState: false);
    private readonly Thread _worker;
    private int _speed = 1;
    private int _paused;
    private int _started;
    private int _disposed;

    public SimulationRunner(World world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _worker = new Thread(Run)
        {
            IsBackground = true,
            Name = "Chronicles Simulation",
        };
    }

    public event Action<WorldSnapshot>? SnapshotPublished;
    public event Action<Exception>? Faulted;

    public int Speed => Volatile.Read(ref _speed);
    public bool IsPaused => Volatile.Read(ref _paused) != 0;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (Interlocked.Exchange(ref _started, 1) == 0)
        {
            _worker.Start();
        }
    }

    public void SetPaused(bool paused) => Volatile.Write(ref _paused, paused ? 1 : 0);

    public void SetSpeed(int speed)
    {
        if (speed is not (1 or 10 or 25 or 50 or 100))
        {
            throw new ArgumentOutOfRangeException(nameof(speed), "배속은 1, 10, 25, 50, 100 중 하나여야 합니다.");
        }

        Volatile.Write(ref _speed, speed);
    }

    public WorldSnapshot GetSnapshot(int rankingCount = 150, int eventCount = 150)
    {
        lock (_worldGate)
        {
            return _world.CreateSnapshot(rankingCount, eventCount);
        }
    }

    public AgentSnapshot GetAgent(AgentId agentId)
    {
        lock (_worldGate)
        {
            return _world.GetAgentSnapshot(agentId);
        }
    }

    public WorldObservation GetObservation(
        AgentId? selectedAgent = null,
        int rankingCount = 150,
        int eventCount = 150)
    {
        lock (_worldGate)
        {
            var world = _world.CreateSnapshot(rankingCount, eventCount);
            var agent = _world.GetAgentSnapshot(selectedAgent ?? world.LeaderId);
            return new WorldObservation(world, agent);
        }
    }

    public IReadOnlyList<AgentSnapshot> SearchAgents(string? query, int limit = 150)
    {
        lock (_worldGate)
        {
            return _world.SearchAgents(query, limit);
        }
    }

    public WorldCheckpoint CreateCheckpoint()
    {
        lock (_worldGate)
        {
            return _world.CreateCheckpoint();
        }
    }

    public void RestoreCheckpoint(WorldCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        var restored = World.Restore(checkpoint);
        lock (_worldGate)
        {
            _world = restored;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _stop.Set();
        if (_worker.IsAlive)
        {
            _worker.Join(TimeSpan.FromSeconds(5));
        }

        _stop.Dispose();
    }

    private void Run()
    {
        try
        {
            PublishSnapshot();
            while (!_stop.Wait(PulseMilliseconds))
            {
                lock (_worldGate)
                {
                    if (!IsPaused)
                    {
                        var ticks = checked(BaseTicksPerPulse * Speed);
                        _world.AdvanceTo(new SimulationTick(checked(_world.Tick.Value + ticks)));
                    }
                }

                PublishSnapshot();
            }
        }
        catch (Exception exception)
        {
            Faulted?.Invoke(exception);
        }
    }

    private void PublishSnapshot()
    {
        var handler = SnapshotPublished;
        if (handler is null)
        {
            return;
        }

        WorldSnapshot snapshot;
        lock (_worldGate)
        {
            snapshot = _world.CreateSnapshot(150, 150);
        }

        handler(snapshot);
    }
}
