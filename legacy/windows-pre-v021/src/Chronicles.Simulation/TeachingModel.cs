using Chronicles.Core;

namespace Chronicles.Simulation;

public enum TeachingTargetKind : byte
{
    Technique,
    Spell,
}

public enum TeachingResultKind : byte
{
    None,
    LearningProgress,
    Misunderstanding,
    Correction,
    TransmissionEstablished,
    TransmissionUpdated,
    Interrupted,
    Refused,
}

public readonly record struct TeachingKnowledgeRef(ulong Value)
{
    public override string ToString() => $"K{Value:X16}";
}

public readonly record struct TeachingProvenanceId(ulong Value)
{
    public override string ToString() => $"T{Value:X16}";
}

public readonly record struct TeachingTraceState(
    long AuthorityRevision,
    bool IsActive,
    int LearnerAgent,
    PersonIdentityId TeacherPersonId,
    PersonIdentityId LearnerPersonId,
    RelationId RelationId,
    TeachingTargetKind TargetKind,
    ushort SourceQuality,
    TeachingKnowledgeRef TargetKnowledgeRef,
    TeachingProvenanceId ProvenanceId,
    long StartedTick,
    long LastResultTick,
    TeachingResultKind LastResult,
    int LastGain,
    int SessionsCompleted,
    ushort MisunderstandingPoints,
    bool TransmissionEstablished)
{
    public static TeachingTraceState Empty => new(
        AuthorityRevision: 0,
        IsActive: false,
        LearnerAgent: -1,
        TeacherPersonId: default,
        LearnerPersonId: default,
        RelationId: default,
        TargetKind: default,
        SourceQuality: 0,
        TargetKnowledgeRef: default,
        ProvenanceId: default,
        StartedTick: 0,
        LastResultTick: 0,
        LastResult: TeachingResultKind.None,
        LastGain: 0,
        SessionsCompleted: 0,
        MisunderstandingPoints: 0,
        TransmissionEstablished: false);
}

public readonly record struct TeachingCheckpoint(
    long AuthorityRevision,
    TeachingTraceState Latest,
    long LearningAuthorityRevision,
    TeachingTraceState LatestLearning,
    ushort TechniqueMisunderstandingPoints,
    ushort SpellMisunderstandingPoints);

public sealed record TeachingSnapshot(
    string ContractId,
    int ContractRevision,
    bool RequiresReview,
    int DirectCultivationSpeedBonusBasisPoints,
    TeachingTraceState Latest,
    TeachingTraceState LatestLearning,
    ushort TechniqueMisunderstandingPoints,
    ushort SpellMisunderstandingPoints,
    TeachingParticipantSnapshot? LatestParticipants = null,
    TeachingParticipantSnapshot? LatestLearningParticipants = null);

public sealed record TeachingParticipantSnapshot(
    AgentId TeacherId,
    string TeacherName,
    AgentId LearnerId,
    string LearnerName);

public static class TeachingRules
{
    public const string ContractId = "PC-TEACHING-001";
    public const int ContractRevision = 1;
    public const string ProvisionalReviewId = "PC-TEMP-TEACHING-BALANCE-113";
    public const int SelectionActionInterval = 12;
    public const int SelectionActionPhase = 1;
    public const int MinimumKnowledgeGap = 1_000;
    public const int BaseTrainingExperienceGain = 500;
    public const int BaseUnderstandingGainHundredths = 25;
    public const int CorrectionAmountHundredths = 250;
    public const int MisunderstandingAmountHundredths = 100;
    public const int TransmissionUpdateInterval = 5;

    public static TeachingKnowledgeRef CreateKnowledgeRef(
        PersonIdentityId teacherPersonId,
        TeachingTargetKind targetKind,
        ushort quality)
    {
        if (teacherPersonId.Value == 0 || !Enum.IsDefined(targetKind))
        {
            throw new ArgumentOutOfRangeException(nameof(teacherPersonId));
        }

        var value = teacherPersonId.Value * 0x9E3779B185EBCA87UL;
        value ^= ((ulong)targetKind + 1UL) * 0xC2B2AE3D27D4EB4FUL;
        value ^= (ulong)quality * 0x165667B19E3779F9UL;
        return new TeachingKnowledgeRef(Mix(value));
    }

    public static TeachingProvenanceId CreateProvenanceId(
        RelationId relationId,
        TeachingKnowledgeRef knowledgeRef,
        long startedTick)
    {
        if (relationId.Value == 0 || knowledgeRef.Value == 0 || startedTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startedTick));
        }

        var tickSalt = unchecked((ulong)startedTick * 0xD6E8FEB86659FD93UL);
        return new TeachingProvenanceId(Mix(
            relationId.Value
            ^ knowledgeRef.Value
            ^ tickSalt));
    }

    public static int GetDedicatedRollBasisPoints(
        ulong worldSeed,
        TeachingProvenanceId provenanceId,
        long tick,
        int sessionNumber)
    {
        var tickSalt = unchecked((ulong)tick * 0x9E3779B185EBCA87UL);
        var sessionSalt = unchecked(
            (ulong)sessionNumber * 0xC2B2AE3D27D4EB4FUL);
        var value = worldSeed
            ^ provenanceId.Value
            ^ tickSalt
            ^ sessionSalt;
        return checked((int)(Mix(value) % 10_000UL));
    }

    public static int GetMisunderstandingChanceBasisPoints(
        int teacherUnderstanding,
        int learnerComprehension) => Math.Clamp(
            1_200 - (teacherUnderstanding / 10) - (learnerComprehension / 20),
            100,
            1_200);

    public static int GetTrainingExperienceGain(int knowledgeGap) => checked(
        BaseTrainingExperienceGain + Math.Clamp(knowledgeGap / 10, 0, 2_000));

    public static int GetUnderstandingGainHundredths(
        int teacherUnderstanding,
        int learnerComprehension) => Math.Clamp(
            BaseUnderstandingGainHundredths
            + (teacherUnderstanding / 50)
            + (learnerComprehension / 100),
            1,
            500);

    public static bool IsTransmissionProgress(TeachingResultKind result) => result is
        TeachingResultKind.LearningProgress
        or TeachingResultKind.TransmissionEstablished
        or TeachingResultKind.TransmissionUpdated;

    public static bool IsValidTrace(
        TeachingTraceState trace,
        int population,
        long maximumTick)
    {
        if (trace.AuthorityRevision < 0
            || trace.LearnerAgent < -1
            || trace.LearnerAgent >= population
            || trace.StartedTick < 0
            || trace.StartedTick > maximumTick
            || trace.LastResultTick < 0
            || trace.LastResultTick > maximumTick
            || trace.LastGain < 0
            || trace.SessionsCompleted < 0
            || trace.SessionsCompleted == int.MaxValue
            || trace.MisunderstandingPoints > QualityValue.MaximumHundredths
            || trace.SourceQuality > QualityValue.MaximumHundredths
            || !Enum.IsDefined(trace.TargetKind)
            || !Enum.IsDefined(trace.LastResult))
        {
            return false;
        }

        if (trace.AuthorityRevision == 0)
        {
            return trace == TeachingTraceState.Empty;
        }

        if (trace.IsActive
            ? trace.LastResult != TeachingResultKind.None
                || trace.LastResultTick != 0
                || trace.LastGain != 0
            : trace.LastResult == TeachingResultKind.None
                || trace.LastResultTick < trace.StartedTick)
        {
            return false;
        }

        return trace.LearnerAgent >= 0
            && trace.TeacherPersonId.Value != 0
            && trace.LearnerPersonId.Value != 0
            && trace.RelationId.Value != 0
            && trace.TargetKnowledgeRef.Value != 0
            && trace.ProvenanceId.Value != 0;
    }

    private static ulong Mix(ulong value)
    {
        value ^= value >> 30;
        value *= 0xBF58476D1CE4E5B9UL;
        value ^= value >> 27;
        value *= 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}
