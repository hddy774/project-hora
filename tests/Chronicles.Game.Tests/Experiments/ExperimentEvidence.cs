namespace Chronicles.Game.Tests.Experiments;

// Preparation schema only. A source implementation/functional pass cannot fill the measured decision fields.
internal sealed record ExperimentDefinition(string Id, string Baseline, string Candidate, string Scope, string[] RequiredEvidence);
internal sealed record BackendSample(long Operation, long Nanoseconds);
internal sealed record ExperimentMeasurement(string ProductSourceFingerprint, string FixtureHash, string CommandHash,
    string KernelVersion, int BatchLength, string SupportedFeatures, string Runtime, string Device, string WarmupOpeningHash,
    string MeasuredOpeningHash, BackendSample[] RawSamples, string Decision, string Rationale);

internal static class ExecutionExperimentPlan
{
    internal static IReadOnlyList<ExperimentDefinition> Definitions { get; } =
    [
        new("EX-01", "원래 월별 Person 순회", "hot columns + 실제 행동 기한 힙", "명령·취소·재예약·생사·월별 경계 전체",
            ["초기/중간/final hash", "사건열/RNG", "큐·무효 토큰·재구축", "실제 작업별 원시 지연 표본"]),
        new("EX-02", "Person/슬롯 목록", "확장 구획 hot columns", "명시 합성 초기조건과 별도 출생·사망·계보 기능 fixture",
            ["2/100/1000/10000 단계", "활성/사망/재사용 세대", "메모리", "대여/반환/재배치"]),
        new("EX-03", "현재 실제 AI/시장 후보", "명시 후보/색인 변경분", "권한·선택 누락·실제 노동·정산까지 포함",
            ["후보 규칙", "명령/결정 분포", "자산 보존", "같은 입력·사건 경계"]),
        new("EX-04", "Aptitudes.InnateTalent 정답", "C# 배치/Vector 후보", "입력 캡처·원천 fingerprint·대여·계산·검증·게시 전체",
            ["빈/소량/벡터 경계/대량", "부분 실패/미지원/낡은 입력/중복", "배치 길이/커널/지원 기능", "p50/p95/p99 원시 표본"]),
        new("EX-05", "현재 GameCodec/GameSaveStore", "선정 serializer/저장 후보", "실제 schema·중단·손상·복구·원본 보존",
            ["H01 기준 소스/fixture", "크기·실제 읽기/쓰기 표본", "고정 버전/라이선스", "호스트/기기 분리"]),
        new("EX-06", "현재 공개 조회/인벤토리", "권한 보존 색인/캐시 후보", "열기/닫기·검색·장비·원시 관측 누출·입력/응답 전체",
            ["권한별 동일 의미", "닫힌 화면 요청/행 생성", "캐시 무효화", "실제 요청별 지연/할당"])
    ];

    internal static void ValidateMeasuredEvidence(ExperimentMeasurement measurement)
    {
        if (string.IsNullOrWhiteSpace(measurement.ProductSourceFingerprint) || string.IsNullOrWhiteSpace(measurement.FixtureHash) ||
            string.IsNullOrWhiteSpace(measurement.CommandHash) || string.IsNullOrWhiteSpace(measurement.KernelVersion) ||
            string.IsNullOrWhiteSpace(measurement.SupportedFeatures) || string.IsNullOrWhiteSpace(measurement.Runtime) || string.IsNullOrWhiteSpace(measurement.Device) ||
            string.IsNullOrWhiteSpace(measurement.WarmupOpeningHash) ||
            measurement.WarmupOpeningHash != measurement.MeasuredOpeningHash || measurement.BatchLength < 0 ||
            measurement.RawSamples.Length == 0 || measurement.RawSamples.Any(s => s.Operation < 0 || s.Nanoseconds < 0) ||
            measurement.RawSamples.Select(s => s.Operation).Distinct().Count() != measurement.RawSamples.Length ||
            measurement.Decision is not ("adopt" or "reject" or "revisit") || string.IsNullOrWhiteSpace(measurement.Rationale))
            throw new InvalidDataException("A prepared source/functional result is not a measured EX decision.");
    }
}
