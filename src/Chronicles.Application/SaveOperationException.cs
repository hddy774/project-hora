using System.IO;
using Chronicles.Persistence;

namespace Chronicles.Application;

public enum SaveOperationKind : byte
{
    Capacity,
    Memory,
    Storage,
    Authority,
    Cancelled,
    Unexpected,
}

public enum SaveOperationPhase : byte
{
    Preparing,
    CapturingAuthority,
    EncodingAuthority,
    WritingCandidate,
    VerifyingCandidate,
    ReplacingPrimary,
    WritingManifest,
    InspectingCandidates,
    SelectingCandidate,
    DecodingCandidate,
    ValidatingAuthority,
    SwappingAuthority,
    Complete,
}

/// <summary>
/// Stable application-boundary failure for save/load work. Expected capacity,
/// storage and authority validation failures are recoverable by the desktop shell;
/// an unexpected failure is deliberately left for the global fatal boundary.
/// </summary>
public sealed class SaveOperationException : Exception
{
    public SaveOperationException(
        string operation,
        SaveOperationKind kind,
        SaveOperationPhase phase,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        Operation = operation;
        Kind = kind;
        Phase = phase;
    }

    public string Operation { get; }

    public SaveOperationKind Kind { get; }

    public SaveOperationPhase Phase { get; }

    public bool IsRecoverable => Kind != SaveOperationKind.Unexpected;

    internal static SaveOperationException Normalize(
        string operation,
        SaveOperationPhase phase,
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (exception is SaveOperationException normalized)
        {
            return normalized;
        }

        var root = Unwrap(exception);
        var kind = Classify(root, phase);
        var message = kind == SaveOperationKind.Cancelled
            ? $"{operation} 작업이 취소되었습니다."
            : $"{operation} 처리 중 오류가 발생했습니다 ({FormatPhase(phase)}). {root.Message}";
        return new SaveOperationException(operation, kind, phase, message, root);
    }

    internal static SaveOperationException FromLoadResult(SlotLoadResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var kind = result.Status switch
        {
            SlotLoadStatus.IncompatibleWorldSchema
                or SlotLoadStatus.IncompatibleCheckpointSchema => SaveOperationKind.Authority,
            SlotLoadStatus.SlotNotFound
                or SlotLoadStatus.NoValidCandidate
                or SlotLoadStatus.IncompatibleLegacySave => SaveOperationKind.Storage,
            _ => SaveOperationKind.Unexpected,
        };
        return new SaveOperationException(
            "불러오기",
            kind,
            SaveOperationPhase.SelectingCandidate,
            $"불러오기 후보를 선택하지 못했습니다. {result.Message}");
    }

    private static SaveOperationKind Classify(
        Exception exception,
        SaveOperationPhase phase)
    {
        if (exception is OperationCanceledException)
        {
            return SaveOperationKind.Cancelled;
        }

        if (exception is OutOfMemoryException)
        {
            return SaveOperationKind.Memory;
        }

        if (exception is OverflowException || IsCapacityMessage(exception.Message))
        {
            return SaveOperationKind.Capacity;
        }

        if (exception is InvalidDataException
            && phase is SaveOperationPhase.CapturingAuthority
                or SaveOperationPhase.ValidatingAuthority
                or SaveOperationPhase.SwappingAuthority)
        {
            return SaveOperationKind.Authority;
        }

        if (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return SaveOperationKind.Storage;
        }

        return SaveOperationKind.Unexpected;
    }

    private static Exception Unwrap(Exception exception)
    {
        while (exception is AggregateException aggregate
               && aggregate.InnerExceptions.Count == 1)
        {
            exception = aggregate.InnerExceptions[0];
        }

        return exception;
    }

    private static bool IsCapacityMessage(string message) =>
        message.Contains("허용 크기", StringComparison.OrdinalIgnoreCase)
        || message.Contains("용량", StringComparison.OrdinalIgnoreCase)
        || message.Contains("크기를 초과", StringComparison.OrdinalIgnoreCase)
        || message.Contains("too large", StringComparison.OrdinalIgnoreCase)
        || message.Contains("maximum", StringComparison.OrdinalIgnoreCase)
        || message.Contains("capacity", StringComparison.OrdinalIgnoreCase);

    private static string FormatPhase(SaveOperationPhase phase) => phase switch
    {
        SaveOperationPhase.Preparing => "준비",
        SaveOperationPhase.CapturingAuthority => "권위 상태 고정",
        SaveOperationPhase.EncodingAuthority => "권위 상태 직렬화",
        SaveOperationPhase.WritingCandidate => "저장 후보 기록",
        SaveOperationPhase.VerifyingCandidate => "저장 후보 검증",
        SaveOperationPhase.ReplacingPrimary => "기본 저장 교체",
        SaveOperationPhase.WritingManifest => "슬롯 정보 기록",
        SaveOperationPhase.InspectingCandidates => "저장 후보 검사",
        SaveOperationPhase.SelectingCandidate => "복구 후보 선택",
        SaveOperationPhase.DecodingCandidate => "저장 후보 해제",
        SaveOperationPhase.ValidatingAuthority => "권위 상태 검증",
        SaveOperationPhase.SwappingAuthority => "권위 세계 교체",
        SaveOperationPhase.Complete => "완료",
        _ => phase.ToString(),
    };
}
