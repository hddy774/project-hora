using Chronicles.Persistence;

namespace Chronicles.Application;

public readonly record struct SaveOperationProgress(int Percent, string Stage);

internal static class SaveOperationProgressProjection
{
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
