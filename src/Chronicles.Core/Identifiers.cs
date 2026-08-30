namespace Chronicles.Core;

public readonly record struct AgentId
{
    public AgentId(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    public int Value { get; }

    public override string ToString() => $"A{Value + 1:00000}";
}

public readonly record struct SimulationTick
{
    public SimulationTick(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    public long Value { get; }

    public static implicit operator long(SimulationTick tick) => tick.Value;
}
