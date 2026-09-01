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

public readonly record struct AtlasNodeId
{
    public AtlasNodeId(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    public int Value { get; }

    public override string ToString() => $"ATLAS-{Value:000000}";
}

public readonly record struct OrganizationId
{
    public OrganizationId(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    public long Value { get; }

    public override string ToString() => $"ORG-{Value:000000}";
}

public readonly record struct SpiritVeinSourceId
{
    public SpiritVeinSourceId(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    public int Value { get; }

    public override string ToString() => $"VEIN-{Value + 1:0000}";
}

public readonly record struct MarketOfferId
{
    public MarketOfferId(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    public long Value { get; }

    public override string ToString() => $"OFFER-{Value:0000000000}";
}

public readonly record struct MarketReservationId
{
    public MarketReservationId(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    public long Value { get; }

    public override string ToString() => $"RSV-{Value:0000000000}";
}
