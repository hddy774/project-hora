namespace Chronicles.Game.Knowledge;

public enum KnowledgeKind { Cultivation, Spell, Craft, Martial, History }
public enum EditionRelation { Original, Copy, Revision, Summary, Translation, Tampered, Restored, Reconstructed }
public enum KnowledgeMediumKind { Book, JadeSlip, Inscription }
[Flags]
public enum KnowledgeRights { None = 0, Read = 1, Copy = 2, Teach = 4 }
public enum KnowledgeBelief { Unverified, Supported, Contradicted, Corrected }
public enum KnowledgeActivityKind { Study, Teaching, Transcription, Practice }
public enum KnowledgeActivityStatus { Active, Completed, Cancelled }
public enum KnowledgeTransmission { Parent, Mentor, Apprenticeship, Peer }
public enum KnowledgeTestResult { None, SupportsClaim, RefutesClaim }
public enum KnowledgeStatus { Applied, Invalid, NotFound, Forbidden, Unavailable, Busy, DuplicateSource, StaleRevision, TooEarly, InsufficientMaterials }

/// <summary>Shared definitions contain no personal progress and do not prescribe whether an edition's claim is true.</summary>
public sealed record KnowledgeChapter(int Number, string Title, int StudyMonths, int PracticeMonths);
public sealed record KnowledgeWork(string Id, string Title, string SkillId, KnowledgeKind Kind,
    KnowledgeChapter[] Chapters, long CreatedMonth, string SourceId);
public sealed record EditionChapter(int Number, string Claim);
public sealed record KnowledgeEdition(long Id, string WorkId, long ParentEditionId, EditionRelation Relation,
    long ContributorId, EditionChapter[] Chapters, long CreatedMonth, long Revision, string SourceId);

/// <summary>ItemId is the existing inventory identity. Quality never substitutes for actual missing or unreadable content.</summary>
public sealed record KnowledgeMedium(long ItemId, long EditionId, long OwnerId, int Place,
    KnowledgeMediumKind Kind, int Quality, KnowledgeRights OwnerRights, int[] MissingChapters,
    bool Unreadable, bool Destroyed, long Revision, string SourceId);
public sealed record KnowledgeAccess(long PersonId, long ItemId, KnowledgeRights Rights, long ExpiresMonth,
    long GrantorId, long GrantedMonth, long Revision, string SourceId);

/// <summary>Every semantic change appends a revision. Old revisions remain valid execution evidence.</summary>
public sealed record PersonalKnowledge(long PersonId, string WorkId, int Chapter, long EditionId,
    string Claim, int StudyMonths, int Decoding, int Comprehension, int Confidence, KnowledgeBelief Belief,
    long PreviousRevision, long Revision, long LearnedMonth, string SourceId);
public sealed record PracticalKnowledge(long PersonId, string WorkId, int Chapter, int PracticeMonths,
    int Mastery, long Revision, string SourceId);
public sealed record KnowledgeAdoption(long PersonId, string WorkId, int Chapter, long EditionId,
    long PersonalRevision, string Claim, int Comprehension, int Mastery, KnowledgeBelief Belief);

/// <summary>Developer defaults. The world-law adapter may change future actions; each action stores its adopted rules.</summary>
public sealed record KnowledgeRules
{
    public long Revision { get; init; } = 1;
    public int TranscriptionMonthsPerChapter { get; init; } = 1;
    public int PaperPerChapter { get; init; } = 1;
    public int InkPerChapter { get; init; } = 1;
    public int TeachingMinimumComprehension { get; init; } = 25;
    public int PracticeMinimumComprehension { get; init; } = 25;
    public int MaximumActionMonths { get; init; } = 120;
}

/// <summary>The world supplies current physical/lifecycle facts. CanParticipate means alive and able to perform this action.</summary>
public readonly record struct KnowledgeParticipant(long PersonId, int Place, bool CanParticipate);

/// <summary>
/// A receipt issued only after the world has reserved/consumed these real inventory inputs. The knowledge authority
/// consumes a receipt once; it does not create inventory stock. Preflight and external payment must share a world transaction.
/// </summary>
public sealed record KnowledgeWritingPayment(string ReceiptId, long BlankMediumItemId, long PaperItemId, int Paper,
    long InkItemId, int Ink);
public sealed record KnowledgeWritingCost(int Paper, int Ink, int Months);

public sealed record KnowledgeActivity(long Id, KnowledgeActivityKind Kind, KnowledgeActivityStatus Status,
    long ActorId, long TeacherId, int Place, string WorkId, long EditionId, long MediumItemId,
    int[] Chapters, long StartedMonth, long DueMonth, long EndedMonth, KnowledgeTransmission Transmission,
    KnowledgeAdoption[] Adopted, EditionChapter[] Claims, int[] StudyCredits, KnowledgeRules Rules,
    long TargetItemId, KnowledgeMediumKind TargetKind, int TargetQuality, KnowledgeWritingPayment? Payment,
    string SourceId, long Revision, string CompletionSourceId, KnowledgePracticeOutcome? PracticeOutcome = null);
public sealed record KnowledgeTimeUse(long PersonId, long StartedMonth, long EndedMonth, long ActivityId);
public sealed record KnowledgeEvidence(string SourceId, string Kind, long ActorId, long OtherPersonId,
    string WorkId, long EditionId, long ItemId, int[] Chapters, long StartedMonth, long EndedMonth,
    long PersonalRevision, long Revision, string Detail);

/// <summary>Serializable authority state. Queries and runtime indexes are deliberately absent.</summary>
public sealed class KnowledgeState
{
    public int Schema { get; set; } = 1;
    public long Revision { get; set; }
    public long Month { get; set; }
    public long NextId { get; set; } = 1;
    public KnowledgeRules Rules { get; set; } = new();
    public List<KnowledgeWork> Works { get; set; } = [];
    public List<KnowledgeEdition> Editions { get; set; } = [];
    public List<KnowledgeMedium> Media { get; set; } = [];
    public List<KnowledgeAccess> Access { get; set; } = [];
    public List<PersonalKnowledge> Memories { get; set; } = [];
    public List<PracticalKnowledge> Mastery { get; set; } = [];
    public List<KnowledgeActivity> Activities { get; set; } = [];
    public List<KnowledgeTimeUse> TimeUses { get; set; } = [];
    public List<KnowledgeEvidence> Evidence { get; set; } = [];
}

public sealed record KnowledgeResult(KnowledgeStatus Status, string Message, long Id = 0, long Revision = 0)
{
    public bool Applied => Status == KnowledgeStatus.Applied;
}
public sealed record KnowledgePage<T>(long Revision, int Offset, int Total, IReadOnlyList<T> Rows);
public sealed record PersonalKnowledgeView(string WorkId, string WorkTitle, string SkillId, int Chapter,
    string ChapterTitle, long EditionId, string Claim, int Decoding, int Comprehension, int Confidence,
    KnowledgeBelief Belief, int Mastery, long PersonalRevision, long ChangedRevision, string SourceId);
public sealed record AccessibleKnowledgeView(long ItemId, long EditionId, string WorkId, string Title,
    int Quality, KnowledgeRights Rights, int ReadableChapterCount, bool Unreadable, long Revision);

public sealed record StudyKnowledgeRequest(string SourceId, KnowledgeParticipant Learner, long ItemId,
    int[] Chapters, long Month, int Months);
public sealed record TeachKnowledgeRequest(string SourceId, KnowledgeParticipant Teacher, KnowledgeParticipant Pupil,
    string WorkId, int[] Chapters, KnowledgeTransmission Transmission, long Month, int Months);
public sealed record TranscribeKnowledgeRequest(string SourceId, KnowledgeParticipant Scribe, long SourceItemId,
    int[] Chapters, long TargetItemId, KnowledgeMediumKind TargetKind, int TargetQuality,
    KnowledgeWritingPayment Payment, long Month);
public sealed record PracticeKnowledgeRequest(string SourceId, KnowledgeParticipant Practitioner,
    string WorkId, int Chapter, long ExpectedPersonalRevision, long Month, int Months);

/// <summary>Practice outcomes must come from a real completed world process using the action's adoption.</summary>
public sealed record KnowledgePracticeOutcome(string ProcessReceiptId, long PersonId, long AdoptedRevision,
    bool Performed, bool Successful, KnowledgeTestResult TestResult, string Observation);
public sealed record KnowledgeInterpretation(int Chapter, string Claim, int Confidence);
public sealed record CompleteKnowledgeRequest(string SourceId, long ActivityId, long Month,
    KnowledgeParticipant[] Participants, KnowledgePracticeOutcome? Practice = null, KnowledgeInterpretation[]? Interpretations = null);
