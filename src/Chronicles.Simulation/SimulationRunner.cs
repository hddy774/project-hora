using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed class SimulationRunner : IDisposable
{
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

    public event Action? ObservationInvalidated;
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

    public WorldSnapshot GetSnapshot(
        int rankingCount = 1_000,
        int eventCount = 150,
        ObservationSections sections = ObservationSections.All,
        ushort? detailedMapLocationId = null,
        int detailedMapAgentOffset = 0)
    {
        lock (_worldGate)
        {
            return _world.CreateSnapshot(
                rankingCount,
                eventCount,
                sections,
                detailedMapLocationId,
                detailedMapAgentOffset);
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
        int rankingCount = 1_000,
        int eventCount = 150,
        ObservationSections sections = ObservationSections.All,
        ushort? detailedMapLocationId = null,
        int detailedMapAgentOffset = 0)
    {
        lock (_worldGate)
        {
            var world = _world.CreateSnapshot(
                rankingCount,
                eventCount,
                sections,
                detailedMapLocationId,
                detailedMapAgentOffset);
            var agentId = selectedAgent ?? world.LeaderId;
            var agent = _world.GetAgentSnapshot(agentId);
            return new WorldObservation(
                world,
                agent,
                (sections & ObservationSections.PersonalLog) != 0
                    ? _world.GetAgentPersonalLog(agentId)
                    : Array.Empty<PersonalLogEntry>(),
                (sections & ObservationSections.Chronicle) != 0
                    ? _world.GetAgentChronicle(agentId)
                    : Array.Empty<LifeChronicleEntry>());
        }
    }

    public IReadOnlyList<AgentSnapshot> BrowseAgents(int offset, int limit = 500)
    {
        lock (_worldGate)
        {
            return _world.BrowseAgents(offset, limit);
        }
    }

    public IReadOnlyList<AgentSnapshot> SearchAgents(string? query, int limit = 1_000)
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
            PublishObservationInvalidated();
            while (!_stop.Wait(SimulationPacing.PulseMilliseconds))
            {
                lock (_worldGate)
                {
                    if (!IsPaused)
                    {
                        var ticks = SimulationPacing.GetTicksPerPulse(Speed);
                        _world.AdvanceTo(new SimulationTick(checked(_world.Tick.Value + ticks)));
                    }
                }

                PublishObservationInvalidated();
            }
        }
        catch (Exception exception)
        {
            Faulted?.Invoke(exception);
        }
    }

    private void PublishObservationInvalidated()
    {
        ObservationInvalidated?.Invoke();
    }
}

public static class SimulationPacing
{
    public const int PulseMilliseconds = 80;
    public const int BaseTicksPerPulse = 16;

    public static int GetTicksPerPulse(int speed)
    {
        if (speed is not (1 or 10 or 25 or 50 or 100))
        {
            throw new ArgumentOutOfRangeException(nameof(speed));
        }

        return checked(BaseTicksPerPulse * speed);
    }
}
