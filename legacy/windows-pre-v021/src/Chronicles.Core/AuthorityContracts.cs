namespace Chronicles.Core;

public static class RuntimeSpeedRules
{
    public static IReadOnlyList<int> Supported { get; } = [1, 10, 25, 50, 100];

    public static bool IsSupported(int speed) => speed is 1 or 10 or 25 or 50 or 100;
}

public readonly record struct AuthoritySessionId
{
    public AuthoritySessionId(ulong value)
    {
        ArgumentOutOfRangeException.ThrowIfZero(value);
        Value = value;
    }

    public ulong Value { get; }
    public override string ToString() => $"AS-{Value:X16}";
}

public readonly record struct AuthorityCommandId
{
    public AuthorityCommandId(AuthoritySessionId sessionId, long sequence)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
        SessionId = sessionId;
        Sequence = sequence;
    }

    public AuthoritySessionId SessionId { get; }
    public long Sequence { get; }
    public override string ToString() => $"{SessionId}/C{Sequence:D12}";
}

public readonly record struct AuthorityEventId
{
    public AuthorityEventId(AuthoritySessionId sessionId, long sequence)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
        SessionId = sessionId;
        Sequence = sequence;
    }

    public AuthoritySessionId SessionId { get; }
    public long Sequence { get; }
    public override string ToString() => $"{SessionId}/E{Sequence:D12}";
}

public readonly record struct CausalThreadId
{
    public CausalThreadId(AuthoritySessionId sessionId, long sequence)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
        SessionId = sessionId;
        Sequence = sequence;
    }

    public AuthoritySessionId SessionId { get; }
    public long Sequence { get; }
    public override string ToString() => $"{SessionId}/T{Sequence:D12}";
}

public enum RuntimeControlEffectKind : byte
{
    None,
    PauseChanged,
    SpeedChanged,
}

public readonly record struct RuntimeControlEffect(
    RuntimeControlEffectKind Kind,
    long Before,
    long After);

public readonly record struct RuntimeControlCommand(
    AuthorityCommandId Id,
    long ExpectedRevision,
    bool? Paused,
    int? Speed,
    CausalThreadId ThreadId,
    AuthorityEventId? ParentEventId = null);

public enum AuthorityCommandStatus : byte
{
    Applied,
    Rejected,
    RevisionConflict,
    SequenceConflict,
    InvalidCausalReference,
}

public readonly record struct AuthorityCommandResult(
    AuthorityCommandId CommandId,
    AuthorityCommandStatus Status,
    long RevisionBefore,
    long RevisionAfter,
    IReadOnlyList<RuntimeControlEffect> Effects,
    AuthorityEventId? EventId,
    string ErrorCode)
{
    public bool Applied => Status == AuthorityCommandStatus.Applied;
}

public readonly record struct AuthorityEventRecord(
    AuthorityEventId Id,
    AuthorityCommandId CommandId,
    CausalThreadId ThreadId,
    AuthorityEventId RootEventId,
    AuthorityEventId? ParentEventId,
    long AuthorityRevision,
    IReadOnlyList<RuntimeControlEffect> Effects);
