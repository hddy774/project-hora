using Chronicles.Core;

namespace Chronicles.Application;

public interface IRuntimeControlPort
{
    bool IsPaused { get; }
    int Speed { get; }
    void SetPaused(bool paused);
    void SetSpeed(int speed);
}

public sealed class RuntimeControlAuthoritySession
{
    private readonly object _gate = new();
    private readonly IRuntimeControlPort _runtime;
    private readonly List<AuthorityEventRecord> _events = [];
    private readonly Dictionary<AuthorityEventId, AuthorityEventRecord> _eventsById = [];
    private readonly Dictionary<CausalThreadId, AuthorityEventId> _rootByThread = [];
    private long _lastCommandSequence;
    private long _nextEventSequence = 1;

    public RuntimeControlAuthoritySession(
        AuthoritySessionId sessionId,
        IRuntimeControlPort runtime)
    {
        SessionId = sessionId;
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        if (!RuntimeSpeedRules.IsSupported(runtime.Speed))
        {
            throw new ArgumentOutOfRangeException(nameof(runtime), "지원하지 않는 초기 배속입니다.");
        }
    }

    public RuntimeControlAuthoritySession(
        AuthoritySessionId sessionId,
        GameApplicationSession application)
        : this(sessionId, new GameApplicationRuntimeControlPort(application))
    {
    }

    public AuthoritySessionId SessionId { get; }
    public long Revision { get; private set; }

    public IReadOnlyList<AuthorityEventRecord> GetJournal()
    {
        lock (_gate)
        {
            return _events.ToArray();
        }
    }

    public AuthorityCommandResult Execute(RuntimeControlCommand command)
    {
        lock (_gate)
        {
            var revisionBefore = Revision;
            var validation = Validate(command);
            if (validation is not null)
            {
                return Reject(command.Id, validation.Value.Status, revisionBefore, validation.Value.ErrorCode);
            }

            var effects = BuildEffects(command);
            if (effects.Length == 0)
            {
                return Reject(command.Id, AuthorityCommandStatus.Rejected, revisionBefore, "NO_STATE_CHANGE");
            }

            var pausedBefore = _runtime.IsPaused;
            var speedBefore = _runtime.Speed;
            try
            {
                foreach (var effect in effects)
                {
                    Apply(effect);
                }
            }
            catch (Exception applyError)
            {
                try
                {
                    _runtime.SetSpeed(speedBefore);
                    _runtime.SetPaused(pausedBefore);
                }
                catch (Exception rollbackError)
                {
                    throw new InvalidOperationException(
                        "Runtime control transaction과 rollback이 모두 실패했습니다.",
                        new AggregateException(applyError, rollbackError));
                }

                return Reject(command.Id, AuthorityCommandStatus.Rejected, revisionBefore, "APPLY_FAILED_ROLLED_BACK");
            }

            Revision = checked(Revision + 1);
            _lastCommandSequence = command.Id.Sequence;
            var eventId = new AuthorityEventId(SessionId, _nextEventSequence++);
            var rootEventId = command.ParentEventId is { } parent
                ? _eventsById[parent].RootEventId
                : eventId;
            var record = new AuthorityEventRecord(
                eventId,
                command.Id,
                command.ThreadId,
                rootEventId,
                command.ParentEventId,
                Revision,
                effects);
            _events.Add(record);
            _eventsById.Add(eventId, record);
            _rootByThread.TryAdd(command.ThreadId, rootEventId);

            return new AuthorityCommandResult(
                command.Id,
                AuthorityCommandStatus.Applied,
                revisionBefore,
                Revision,
                effects,
                eventId,
                string.Empty);
        }
    }

    private (AuthorityCommandStatus Status, string ErrorCode)? Validate(RuntimeControlCommand command)
    {
        if (command.Id.SessionId != SessionId || command.ThreadId.SessionId != SessionId)
        {
            return (AuthorityCommandStatus.Rejected, "SESSION_MISMATCH");
        }
        if (command.Id.Sequence != _lastCommandSequence + 1)
        {
            return (AuthorityCommandStatus.SequenceConflict, "COMMAND_SEQUENCE_CONFLICT");
        }
        if (command.ExpectedRevision != Revision)
        {
            return (AuthorityCommandStatus.RevisionConflict, "AUTHORITY_REVISION_CONFLICT");
        }
        if (command.Paused is null && command.Speed is null)
        {
            return (AuthorityCommandStatus.Rejected, "EMPTY_COMMAND");
        }
        if (command.Speed is { } speed && !RuntimeSpeedRules.IsSupported(speed))
        {
            return (AuthorityCommandStatus.Rejected, "UNSUPPORTED_SPEED");
        }

        if (command.ParentEventId is { } parentEventId)
        {
            if (parentEventId.SessionId != SessionId
                || !_eventsById.TryGetValue(parentEventId, out var parent)
                || parent.ThreadId != command.ThreadId)
            {
                return (AuthorityCommandStatus.InvalidCausalReference, "INVALID_CAUSAL_PARENT");
            }
        }
        else if (_rootByThread.ContainsKey(command.ThreadId))
        {
            return (AuthorityCommandStatus.InvalidCausalReference, "THREAD_REQUIRES_PARENT");
        }

        return null;
    }

    private RuntimeControlEffect[] BuildEffects(RuntimeControlCommand command)
    {
        var effects = new List<RuntimeControlEffect>(2);
        if (command.Paused is { } paused && paused != _runtime.IsPaused)
        {
            effects.Add(new RuntimeControlEffect(
                RuntimeControlEffectKind.PauseChanged,
                _runtime.IsPaused ? 1 : 0,
                paused ? 1 : 0));
        }
        if (command.Speed is { } speed && speed != _runtime.Speed)
        {
            effects.Add(new RuntimeControlEffect(
                RuntimeControlEffectKind.SpeedChanged,
                _runtime.Speed,
                speed));
        }
        return effects.ToArray();
    }

    private void Apply(RuntimeControlEffect effect)
    {
        switch (effect.Kind)
        {
            case RuntimeControlEffectKind.PauseChanged:
                _runtime.SetPaused(effect.After != 0);
                break;
            case RuntimeControlEffectKind.SpeedChanged:
                _runtime.SetSpeed(checked((int)effect.After));
                break;
            default:
                throw new InvalidOperationException($"지원하지 않는 effect입니다: {effect.Kind}");
        }
    }

    private static AuthorityCommandResult Reject(
        AuthorityCommandId commandId,
        AuthorityCommandStatus status,
        long revision,
        string errorCode) => new(
            commandId,
            status,
            revision,
            revision,
            Array.Empty<RuntimeControlEffect>(),
            null,
            errorCode);

    private sealed class GameApplicationRuntimeControlPort : IRuntimeControlPort
    {
        private readonly GameApplicationSession _application;

        public GameApplicationRuntimeControlPort(GameApplicationSession application)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
        }

        public bool IsPaused => _application.IsPaused;
        public int Speed => _application.Speed;
        public void SetPaused(bool paused) => _application.SetPaused(paused);
        public void SetSpeed(int speed) => _application.SetSpeed(speed);
    }
}
