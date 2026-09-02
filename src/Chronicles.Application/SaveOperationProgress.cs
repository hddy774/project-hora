using Chronicles.Persistence;

namespace Chronicles.Application;

public readonly record struct SaveOperationProgress(int Percent, string Stage);

/// <summary>
/// Lightweight result returned after the candidate checkpoint has been swapped
/// into the runtime. It deliberately excludes the large authority checkpoint so
/// a completed UI Task cannot retain a second world graph.
/// </summary>
public readonly record struct SlotLoadCompletion(
    SlotCandidateKind? CandidateKind,
    string? CandidatePath,
    int WorldSchema,
    int CheckpointSchema,
    long CreatedSequence,
    ulong StateHash);

internal static class SaveOperationProgressProjection
{
    public static SaveOperationPhase GetPhase(SlotPersistencePhase phase) => phase switch
    {
        SlotPersistencePhase.Preparing => SaveOperationPhase.Preparing,
        SlotPersistencePhase.EncodingAuthority => SaveOperationPhase.EncodingAuthority,
        SlotPersistencePhase.WritingCandidate => SaveOperationPhase.WritingCandidate,
        SlotPersistencePhase.VerifyingCandidate => SaveOperationPhase.VerifyingCandidate,
        SlotPersistencePhase.ReplacingPrimary => SaveOperationPhase.ReplacingPrimary,
        SlotPersistencePhase.WritingManifest => SaveOperationPhase.WritingManifest,
        SlotPersistencePhase.InspectingCandidates => SaveOperationPhase.InspectingCandidates,
        SlotPersistencePhase.SelectingCandidate => SaveOperationPhase.SelectingCandidate,
        SlotPersistencePhase.DecodingCandidate => SaveOperationPhase.DecodingCandidate,
        SlotPersistencePhase.ValidatingWorld => SaveOperationPhase.ValidatingAuthority,
        SlotPersistencePhase.Complete => SaveOperationPhase.Complete,
        _ => SaveOperationPhase.Preparing,
    };

    public static SaveOperationProgress FromPersistence(SlotPersistenceProgress progress)
    {
        var candidate = progress.CandidateKind switch
        {
            SlotCandidateKind.Primary => "primary",
            SlotCandidateKind.Temporary => "temporary",
            SlotCandidateKind.Backup => "backup",
            _ => null,
        };
        var stage = progress.Phase switch
        {
            SlotPersistencePhase.Preparing => "저장 트랜잭션 준비",
            SlotPersistencePhase.EncodingAuthority => "권위 상태 직렬화",
            SlotPersistencePhase.WritingCandidate => "임시 SQLite 후보 기록",
            SlotPersistencePhase.VerifyingCandidate => "기록 후보 전체 검증",
            SlotPersistencePhase.ReplacingPrimary => "primary 원자 교체",
            SlotPersistencePhase.WritingManifest => "슬롯 요약 기록",
            SlotPersistencePhase.InspectingCandidates => "저장 후보 검사",
            SlotPersistencePhase.SelectingCandidate => "최신 복구 후보 선택",
            SlotPersistencePhase.DecodingCandidate => $"{candidate ?? "선택"} 후보 해제",
            SlotPersistencePhase.ValidatingWorld => $"{candidate ?? "선택"} 세계 hash 검증",
            SlotPersistencePhase.Complete => candidate is null
                ? "저장 완료"
                : $"{candidate} 후보 검증 완료",
            _ => progress.Phase.ToString(),
        };
        return new SaveOperationProgress(progress.Percent, stage);
    }
}
