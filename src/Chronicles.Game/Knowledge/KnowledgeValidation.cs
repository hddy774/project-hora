using System.Diagnostics.CodeAnalysis;

namespace Chronicles.Game.Knowledge;

public sealed partial class KnowledgeAuthority
{
    /// <summary>Validates the domain graph. The world must additionally validate PersonId/ItemId against its own registries.</summary>
    public static void Validate(KnowledgeState snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Require(snapshot.Schema == 1 && snapshot.Revision >= 0 && snapshot.Month >= 0 && snapshot.NextId > 0 &&
            snapshot.Rules is not null && ValidRules(snapshot.Rules), "Invalid knowledge schema, cursor or rules.");
        Require(snapshot.Works is not null && snapshot.Editions is not null && snapshot.Media is not null &&
            snapshot.Access is not null && snapshot.Memories is not null && snapshot.Mastery is not null &&
            snapshot.Activities is not null && snapshot.TimeUses is not null && snapshot.Evidence is not null,
            "Knowledge collections cannot be null.");
        var evidence = new Dictionary<string, KnowledgeEvidence>(StringComparer.Ordinal);
        long revision = 0;
        long month = 0;
        foreach (var row in snapshot.Evidence)
        {
            Require(row is not null && ValidText(row.SourceId) && ValidText(row.Kind) && row.ActorId >= 0 && row.OtherPersonId >= 0 &&
                row.StartedMonth >= 0 && row.EndedMonth >= row.StartedMonth && row.EndedMonth <= snapshot.Month &&
                row.EndedMonth >= month && row.Revision > 0 && row.Revision <= snapshot.Revision &&
                (row.Revision == revision || revision < long.MaxValue && row.Revision == revision + 1) &&
                row.Chapters is not null && row.Chapters.All(c => c > 0) && row.Chapters.Distinct().Count() == row.Chapters.Length &&
                row.PersonalRevision >= 0 && row.PersonalRevision < row.Revision && evidence.TryAdd(row.SourceId, row),
                "Invalid or duplicate knowledge provenance.");
            revision = row.Revision; month = row.EndedMonth;
        }
        Require(revision == snapshot.Revision && month == snapshot.Month, "Knowledge revision/month is not supported by provenance.");
        var worksById = new Dictionary<string, KnowledgeWork>(StringComparer.Ordinal);
        foreach (var work in snapshot.Works)
        {
            Require(work is not null && ValidWork(work) && worksById.TryAdd(work.Id, work), "Invalid or duplicate knowledge work.");
            var source = Evidence(evidence, work.SourceId);
            Require(source.Kind == "work" && source.WorkId == work.Id && source.EndedMonth == work.CreatedMonth,
                "Work has no matching authorship evidence.");
        }
        var editionsById = new Dictionary<long, KnowledgeEdition>();
        var allocated = new HashSet<long>();
        foreach (var edition in snapshot.Editions)
        {
            Require(edition is not null && edition.Id > 0 && edition.Id < snapshot.NextId && allocated.Add(edition.Id) &&
                editionsById.TryAdd(edition.Id, edition) && worksById.ContainsKey(edition.WorkId) && edition.ContributorId > 0 &&
                Enum.IsDefined(edition.Relation) && edition.CreatedMonth >= worksById[edition.WorkId].CreatedMonth &&
                edition.CreatedMonth <= snapshot.Month && ValidEditionChapters(worksById[edition.WorkId], edition.Chapters),
                "Invalid knowledge edition.");
            var source = Evidence(evidence, edition.SourceId);
            Require(source.Revision == edition.Revision && source.EndedMonth == edition.CreatedMonth && source.WorkId == edition.WorkId &&
                (source.Kind == "edition" && source.EditionId == edition.Id || source.Kind == nameof(KnowledgeActivityKind.Transcription)),
                "Edition has no matching creation evidence.");
        }
        foreach (var edition in snapshot.Editions)
        {
            if (edition.Relation == EditionRelation.Original)
            {
                Require(edition.ParentEditionId == 0, "An original cannot have a parent edition.");
                continue;
            }
            Require(editionsById.TryGetValue(edition.ParentEditionId, out var parent) && parent.Id < edition.Id &&
                parent.WorkId == edition.WorkId && parent.CreatedMonth <= edition.CreatedMonth, "Invalid edition lineage.");
            if (edition.Relation == EditionRelation.Copy)
                Require(edition.Chapters.All(c => parent!.Chapters.Any(p => p.Number == c.Number && p.Claim == c.Claim)),
                    "Copy claims do not match their parent edition.");
        }
        var mediaById = new Dictionary<long, KnowledgeMedium>();
        foreach (var medium in snapshot.Media)
        {
            Require(medium is not null && medium.ItemId > 0 && mediaById.TryAdd(medium.ItemId, medium) &&
                editionsById.ContainsKey(medium.EditionId) && medium.OwnerId >= 0 && medium.Place >= 0 &&
                medium.Quality is >= 0 and <= 6 && Enum.IsDefined(medium.Kind) && ValidRights(medium.OwnerRights) &&
                ValidMissing(editionsById[medium.EditionId], medium.MissingChapters), "Invalid physical knowledge binding.");
            var source = Evidence(evidence, medium.SourceId);
            Require(source.Revision == medium.Revision && source.EndedMonth >= editionsById[medium.EditionId].CreatedMonth &&
                (source.ItemId == medium.ItemId || source.Kind == nameof(KnowledgeActivityKind.Transcription)),
                "Medium has no matching physical evidence.");
        }
        foreach (var access in snapshot.Access)
        {
            Require(access is not null && access.PersonId > 0 && access.GrantorId > 0 && mediaById.ContainsKey(access.ItemId) &&
                ValidRights(access.Rights) && access.Rights != KnowledgeRights.None && access.GrantedMonth >= 0 &&
                access.ExpiresMonth > access.GrantedMonth && access.GrantedMonth <= snapshot.Month &&
                (access.Rights & mediaById[access.ItemId].OwnerRights) == access.Rights, "Invalid knowledge access grant.");
            var source = Evidence(evidence, access.SourceId);
            Require(source.Kind == "access" && source.Revision == access.Revision && source.ItemId == access.ItemId &&
                source.ActorId == access.GrantorId && source.OtherPersonId == access.PersonId && source.EndedMonth == access.GrantedMonth,
                "Access grant has no matching authority evidence.");
        }
        var memoryByRevision = new Dictionary<(long Person, string Work, int Chapter, long Revision), PersonalKnowledge>();
        var current = new Dictionary<(long Person, string Work, int Chapter), PersonalKnowledge>();
        foreach (var memory in snapshot.Memories)
        {
            Require(memory is not null && memory.PersonId > 0 && worksById.ContainsKey(memory.WorkId) &&
                editionsById.TryGetValue(memory.EditionId, out var edition) && edition.WorkId == memory.WorkId &&
                edition.Chapters.Any(c => c.Number == memory.Chapter) && ValidText(memory.Claim) && memory.StudyMonths >= 0 &&
                memory.Decoding is >= 0 and <= 100 && memory.Comprehension is >= 0 and <= 100 && memory.Confidence is >= 0 and <= 100 &&
                (memory.Comprehension == 0 || memory.Decoding > 0) && Enum.IsDefined(memory.Belief) &&
                memory.LearnedMonth >= edition.CreatedMonth && memory.LearnedMonth <= snapshot.Month &&
                memoryByRevision.TryAdd((memory.PersonId, memory.WorkId, memory.Chapter, memory.Revision), memory),
                "Invalid personal knowledge revision.");
            var key = (memory.PersonId, memory.WorkId, memory.Chapter);
            var previous = current.GetValueOrDefault(key);
            Require(memory.PreviousRevision == (previous?.Revision ?? 0) && memory.Revision > memory.PreviousRevision,
                "Personal knowledge history cannot be rewritten or branched.");
            var source = Evidence(evidence, memory.SourceId);
            Require(source.Revision == memory.Revision && source.ActorId == memory.PersonId && source.WorkId == memory.WorkId &&
                source.Chapters.Contains(memory.Chapter) && source.EndedMonth == memory.LearnedMonth &&
                (source.Kind is "genesis-knowledge" or nameof(KnowledgeActivityKind.Study) or nameof(KnowledgeActivityKind.Teaching) or nameof(KnowledgeActivityKind.Practice)) &&
                (source.Kind != "genesis-knowledge" || memory.LearnedMonth == 0 && memory.StudyMonths == 0),
                "Personal knowledge has no real learning or testing source.");
            current[key] = memory;
        }
        var activitiesById = new Dictionary<long, KnowledgeActivity>();
        foreach (var activity in snapshot.Activities)
        {
            Require(activity is not null && activity.Id > 0 && activity.Id < snapshot.NextId && allocated.Add(activity.Id) &&
                activitiesById.TryAdd(activity.Id, activity) && Enum.IsDefined(activity.Kind) && Enum.IsDefined(activity.Status) &&
                activity.ActorId > 0 && activity.Place >= 0 && worksById.ContainsKey(activity.WorkId) &&
                editionsById.TryGetValue(activity.EditionId, out var edition) && edition.WorkId == activity.WorkId &&
                ValidSelection(edition, activity.Chapters) && activity.StartedMonth >= edition.CreatedMonth && activity.StartedMonth <= snapshot.Month &&
                activity.DueMonth > activity.StartedMonth && activity.Rules is not null && ValidRules(activity.Rules) &&
                activity.DueMonth - activity.StartedMonth <= activity.Rules.MaximumActionMonths &&
                activity.DueMonth - activity.StartedMonth >= activity.Chapters.Length && Enum.IsDefined(activity.Transmission) &&
                activity.Adopted is not null && activity.Claims is not null && activity.StudyCredits is not null,
                "Invalid knowledge activity reservation.");
            var source = Evidence(evidence, activity.SourceId);
            Require(source.Kind == "begin-" + activity.Kind && source.ActorId == activity.ActorId && source.WorkId == activity.WorkId &&
                source.OtherPersonId == activity.TeacherId && source.EndedMonth == activity.StartedMonth &&
                source.Chapters.SequenceEqual(activity.Chapters), "Activity has no matching start evidence.");
            if (activity.Status == KnowledgeActivityStatus.Active)
                Require(activity.EndedMonth == 0 && activity.CompletionSourceId == "" && activity.PracticeOutcome is null &&
                    activity.Revision == source.Revision, "An active activity cannot have completion evidence.");
            else
            {
                var end = Evidence(evidence, activity.CompletionSourceId);
                Require(activity.EndedMonth >= activity.StartedMonth && activity.EndedMonth <= snapshot.Month &&
                    end.EndedMonth == activity.EndedMonth && end.StartedMonth == activity.StartedMonth &&
                    end.ActorId == activity.ActorId && end.Revision == activity.Revision &&
                    (activity.Status == KnowledgeActivityStatus.Cancelled ? end.Kind == "interrupted" && activity.PracticeOutcome is null :
                        end.Kind == activity.Kind.ToString() && activity.EndedMonth >= activity.DueMonth), "Invalid activity completion evidence.");
            }
            Require(activity.TeacherId == 0 || activity.Kind == KnowledgeActivityKind.Teaching && activity.TeacherId > 0 &&
                activity.TeacherId != activity.ActorId, "Invalid teaching participant.");
            if (activity.Kind is KnowledgeActivityKind.Study or KnowledgeActivityKind.Transcription)
                Require(mediaById.TryGetValue(activity.MediumItemId, out var medium) && medium.EditionId == activity.EditionId,
                    "Physical learning activity has no matching medium.");
            else Require(activity.MediumItemId == 0, "Oral teaching/practice cannot invent a physical source.");
            if (activity.Kind is KnowledgeActivityKind.Study or KnowledgeActivityKind.Teaching or KnowledgeActivityKind.Transcription)
                Require(activity.Claims.Length == activity.Chapters.Length && activity.Claims.Select(c => c.Number).SequenceEqual(activity.Chapters) &&
                    activity.Claims.All(c => c is not null && ValidText(c.Claim)), "Activity claims do not match selected chapters.");
            else Require(activity.Claims.Length == 0, "Practice cannot create new textual claims.");
            if (activity.Kind is KnowledgeActivityKind.Study or KnowledgeActivityKind.Teaching)
                Require(activity.StudyCredits.Length == activity.Chapters.Length && activity.StudyCredits.All(c => c >= 0), "Invalid study credit snapshot.");
            else Require(activity.StudyCredits.Length == 0, "Non-learning action cannot carry study credits.");
            if (activity.Kind == KnowledgeActivityKind.Teaching)
                Require(activity.TeacherId > 0 && activity.Adopted.Length == activity.Chapters.Length, "Teaching needs the teacher's personal revisions.");
            else Require(activity.Adopted.Length == (activity.Kind == KnowledgeActivityKind.Practice ? 1 : 0), "Invalid adopted personal revision count.");
            foreach (var adoption in activity.Adopted)
            {
                Require(adoption is not null && adoption.PersonId == (activity.Kind == KnowledgeActivityKind.Teaching ? activity.TeacherId : activity.ActorId) &&
                    adoption.WorkId == activity.WorkId && activity.Chapters.Contains(adoption.Chapter) &&
                    memoryByRevision.TryGetValue((adoption.PersonId, adoption.WorkId, adoption.Chapter, adoption.PersonalRevision), out var memory) &&
                    memory.Claim == adoption.Claim && memory.EditionId == adoption.EditionId && memory.Comprehension == adoption.Comprehension &&
                    memory.Belief == adoption.Belief && memory.LearnedMonth <= activity.StartedMonth && memory.Revision < source.Revision &&
                    adoption.Mastery is >= 0 and <= 100 && adoption.Comprehension >= (activity.Kind == KnowledgeActivityKind.Teaching ?
                        activity.Rules.TeachingMinimumComprehension : activity.Rules.PracticeMinimumComprehension), "Invalid personal revision adoption.");
            }
            if (activity.Kind == KnowledgeActivityKind.Transcription)
            {
                var payment = activity.Payment;
                var count = activity.Chapters.Length;
                var paper = activity.TargetKind == KnowledgeMediumKind.Book ? activity.Rules.PaperPerChapter * count : 0;
                var ink = activity.TargetKind == KnowledgeMediumKind.JadeSlip ? 0 : activity.Rules.InkPerChapter * count;
                Require(activity.TargetItemId > 0 && activity.TargetItemId != activity.MediumItemId && Enum.IsDefined(activity.TargetKind) &&
                    activity.TargetQuality is >= 0 and <= 6 && payment is not null && payment.BlankMediumItemId == activity.TargetItemId &&
                    payment.Paper == paper && payment.Ink == ink && ValidMaterialItem(payment.PaperItemId, paper) &&
                    ValidMaterialItem(payment.InkItemId, ink) && (paper == 0 || ink == 0 || payment.PaperItemId != payment.InkItemId) &&
                    payment.PaperItemId != activity.TargetItemId && payment.InkItemId != activity.TargetItemId &&
                    payment.PaperItemId != activity.MediumItemId && payment.InkItemId != activity.MediumItemId &&
                    activity.DueMonth - activity.StartedMonth == (long)activity.Rules.TranscriptionMonthsPerChapter * count &&
                    activity.Claims.All(c => editionsById[activity.EditionId].Chapters.Any(p => p.Number == c.Number && p.Claim == c.Claim)),
                    "Invalid transcription input reservation.");
                var receipt = Evidence(evidence, payment!.ReceiptId);
                Require(receipt.Kind == "writing-payment" && receipt.Revision == source.Revision && receipt.ActorId == activity.ActorId &&
                    receipt.ItemId == activity.TargetItemId, "Writing receipt does not belong to this action.");
                if (activity.Status == KnowledgeActivityStatus.Completed)
                    Require(mediaById.TryGetValue(activity.TargetItemId, out var copied) &&
                        editionsById[copied.EditionId].ParentEditionId == activity.EditionId &&
                        editionsById[copied.EditionId].SourceId == activity.CompletionSourceId,
                        "Completed transcription has no real output medium and copy lineage.");
                else Require(!mediaById.ContainsKey(activity.TargetItemId), "Unfinished transcription has already minted its physical output.");
            }
            else Require(activity.Payment is null && activity.TargetItemId == 0, "Only transcription can spend writing inputs.");
            if (activity.Kind == KnowledgeActivityKind.Practice && activity.Status == KnowledgeActivityStatus.Completed)
            {
                var outcome = activity.PracticeOutcome;
                Require(outcome is not null && outcome.Performed && outcome.PersonId == activity.ActorId &&
                    outcome.AdoptedRevision == activity.Adopted[0].PersonalRevision && Enum.IsDefined(outcome.TestResult) &&
                    (outcome.TestResult == KnowledgeTestResult.None || ValidText(outcome.Observation)), "Invalid practice process outcome.");
                var receipt = Evidence(evidence, outcome!.ProcessReceiptId);
                Require(receipt.Kind == "practice-process" && receipt.Revision == activity.Revision && receipt.ActorId == activity.ActorId,
                    "Practice requires one real process receipt.");
            }
            else Require(activity.PracticeOutcome is null, "An unperformed activity cannot retain a practice outcome.");
        }
        Require(allocated.Count == snapshot.NextId - 1, "Knowledge identity cursor cannot have holes or reuse.");
        var activeTargets = snapshot.Activities.Where(a => a.Status == KnowledgeActivityStatus.Active && a.TargetItemId > 0).Select(a => a.TargetItemId).ToArray();
        Require(activeTargets.Distinct().Count() == activeTargets.Length, "A target medium is reserved twice.");
        var activePeople = snapshot.Activities.Where(a => a.Status == KnowledgeActivityStatus.Active).SelectMany(Participants).ToArray();
        Require(activePeople.Distinct().Count() == activePeople.Length, "A person cannot perform concurrent knowledge activities.");
        var timeKeys = new HashSet<(long Person, long Activity)>();
        foreach (var use in snapshot.TimeUses)
        {
            Require(use is not null && activitiesById.TryGetValue(use.ActivityId, out var activity) &&
                Participants(activity).Contains(use.PersonId) && timeKeys.Add((use.PersonId, use.ActivityId)) &&
                use.StartedMonth == activity.StartedMonth && use.EndedMonth == (activity.Status == KnowledgeActivityStatus.Cancelled ?
                    Math.Min(activity.DueMonth, activity.EndedMonth) : activity.DueMonth), "Invalid participant time ledger.");
        }
        Require(timeKeys.Count == snapshot.Activities.Sum(a => a.TeacherId > 0 ? 2 : 1), "A knowledge activity is missing real participant time.");
        foreach (var group in snapshot.TimeUses.Where(t => t.EndedMonth > t.StartedMonth).GroupBy(t => t.PersonId))
        {
            long end = 0;
            foreach (var use in group.OrderBy(t => t.StartedMonth))
            {
                Require(use.StartedMonth >= end, "Knowledge activities cannot reuse the same person's time.");
                end = use.EndedMonth;
            }
        }
        var previousSkills = new Dictionary<(long Person, string Work, int Chapter), PracticalKnowledge>();
        foreach (var skill in snapshot.Mastery)
        {
            Require(skill is not null && skill.PersonId > 0 && worksById.ContainsKey(skill.WorkId) &&
                worksById[skill.WorkId].Chapters.Any(c => c.Number == skill.Chapter) && skill.PracticeMonths > 0 && skill.Mastery is >= 0 and <= 100,
                "Invalid practical mastery record.");
            var source = Evidence(evidence, skill.SourceId);
            var practice = snapshot.Activities.Find(a => a.Kind == KnowledgeActivityKind.Practice && a.CompletionSourceId == skill.SourceId);
            var key = (skill.PersonId, skill.WorkId, skill.Chapter);
            var previous = previousSkills.GetValueOrDefault(key);
            Require(practice?.PracticeOutcome?.Successful == true && practice.ActorId == skill.PersonId &&
                practice.WorkId == skill.WorkId && practice.Chapters.Contains(skill.Chapter) && source.Revision == skill.Revision &&
                skill.Revision > (previous?.Revision ?? 0) && skill.PracticeMonths == Math.Min(int.MaxValue,
                    (long)(previous?.PracticeMonths ?? 0) + practice.DueMonth - practice.StartedMonth) &&
                skill.Mastery == Math.Min(100, (long)skill.PracticeMonths * 100 /
                    worksById[skill.WorkId].Chapters.Single(c => c.Number == skill.Chapter).PracticeMonths),
                "Practical mastery is not backed by successful, timed practice.");
            previousSkills[key] = skill;
        }
    }

    private static bool ValidText(string? text) => !string.IsNullOrWhiteSpace(text) && text.Length <= 4096;
    private static bool ValidParticipant(KnowledgeParticipant person) => person.PersonId > 0 && person.Place >= 0 && person.CanParticipate;
    private static bool ValidRights(KnowledgeRights rights) => (rights & ~(KnowledgeRights.Read | KnowledgeRights.Copy | KnowledgeRights.Teach)) == 0;
    private static bool ValidMaterialItem(long itemId, int count) => count == 0 ? itemId == 0 : count > 0 && itemId > 0;
    private static bool ValidRules(KnowledgeRules rules) => rules.Revision > 0 && rules.TranscriptionMonthsPerChapter is >= 1 and <= 120 &&
        rules.PaperPerChapter is >= 1 and <= 10000 && rules.InkPerChapter is >= 1 and <= 10000 &&
        rules.TeachingMinimumComprehension is >= 1 and <= 100 && rules.PracticeMinimumComprehension is >= 1 and <= 100 &&
        rules.MaximumActionMonths is >= 1 and <= 1200;
    private static bool ValidWork(KnowledgeWork work) => ValidText(work.Id) && ValidText(work.Title) && ValidText(work.SkillId) &&
        Enum.IsDefined(work.Kind) && work.CreatedMonth >= 0 && ValidText(work.SourceId) && work.Chapters is not null &&
        work.Chapters.Length is >= 1 and <= 256 && work.Chapters.All(c => c is not null && c.Number > 0 && ValidText(c.Title) &&
            c.StudyMonths is >= 1 and <= 120000 && c.PracticeMonths is >= 1 and <= 120000) &&
        work.Chapters.Select(c => c.Number).Distinct().Count() == work.Chapters.Length;
    private static bool ValidEditionChapters(KnowledgeWork work, EditionChapter[]? chapters) => chapters is not null &&
        chapters.Length is >= 1 and <= 256 && chapters.All(c => c is not null && ValidText(c.Claim) && work.Chapters.Any(p => p.Number == c.Number)) &&
        chapters.Select(c => c.Number).Distinct().Count() == chapters.Length;
    private static bool ValidSelection(KnowledgeEdition edition, int[]? chapters) => chapters is not null && chapters.Length is >= 1 and <= 256 &&
        chapters.Distinct().Count() == chapters.Length && chapters.All(c => edition.Chapters.Any(p => p.Number == c));
    private static bool ValidMissing(KnowledgeEdition edition, int[]? chapters) => chapters is not null &&
        chapters.Distinct().Count() == chapters.Length && chapters.All(c => edition.Chapters.Any(p => p.Number == c));
    private static KnowledgeEvidence Evidence(Dictionary<string, KnowledgeEvidence> evidence, string sourceId) =>
        sourceId is not null && evidence.TryGetValue(sourceId, out var source) ? source : throw new InvalidDataException("Missing knowledge provenance.");
    private static void Require([DoesNotReturnIf(false)] bool valid, string message)
    {
        if (!valid) throw new InvalidDataException(message);
    }

    private static KnowledgeActivity Clone(KnowledgeActivity activity) => activity with
    {
        Chapters = activity.Chapters.ToArray(), Adopted = activity.Adopted.Select(a => a with { }).ToArray(),
        Claims = activity.Claims.Select(c => c with { }).ToArray(), StudyCredits = activity.StudyCredits.ToArray(),
        Rules = activity.Rules with { }, Payment = activity.Payment is null ? null : activity.Payment with { },
        PracticeOutcome = activity.PracticeOutcome is null ? null : activity.PracticeOutcome with { }
    };

    private static KnowledgeState Clone(KnowledgeState snapshot) => new()
    {
        Schema = snapshot.Schema, Revision = snapshot.Revision, Month = snapshot.Month, NextId = snapshot.NextId, Rules = snapshot.Rules with { },
        Works = snapshot.Works.Select(w => w with { Chapters = w.Chapters.Select(c => c with { }).ToArray() }).ToList(),
        Editions = snapshot.Editions.Select(e => e with { Chapters = e.Chapters.Select(c => c with { }).ToArray() }).ToList(),
        Media = snapshot.Media.Select(m => m with { MissingChapters = m.MissingChapters.ToArray() }).ToList(),
        Access = snapshot.Access.Select(a => a with { }).ToList(), Memories = snapshot.Memories.Select(m => m with { }).ToList(),
        Mastery = snapshot.Mastery.Select(m => m with { }).ToList(), Activities = snapshot.Activities.Select(Clone).ToList(),
        TimeUses = snapshot.TimeUses.Select(t => t with { }).ToList(),
        Evidence = snapshot.Evidence.Select(e => e with { Chapters = e.Chapters.ToArray() }).ToList()
    };
}
