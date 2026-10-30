namespace Chronicles.Game.Knowledge;

public sealed partial class KnowledgeAuthority
{
    public KnowledgeResult StartStudy(StudyKnowledgeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var error = SourceError(request.SourceId, request.Month);
        if (error is not null) return error;
        if (!media.TryGetValue(request.ItemId, out var medium)) return Reject(KnowledgeStatus.NotFound, "Unknown medium.");
        var edition = editions[medium.EditionId];
        error = LearningWindow(request.Learner, request.Month, request.Months, request.Chapters);
        if (error is not null) return error;
        if (!CanRead(request.Learner, medium, request.Chapters, request.Month, KnowledgeRights.Read))
            return Reject(KnowledgeStatus.Forbidden, "Reading needs present, readable chapters and current physical access.");
        var selected = request.Chapters.Order().ToArray();
        var claims = selected.Select(c => edition.Chapters.Single(p => p.Number == c)).ToArray();
        return Reserve(new(0, KnowledgeActivityKind.Study, KnowledgeActivityStatus.Active, request.Learner.PersonId,
            0, request.Learner.Place, edition.WorkId, edition.Id, medium.ItemId, selected,
            request.Month, request.Month + request.Months, 0, KnowledgeTransmission.Peer, [], claims,
            StudyCredits(request.Learner.PersonId, edition.WorkId, claims), state.Rules with { },
            0, KnowledgeMediumKind.Book, 0, null, request.SourceId, 0, ""));
    }

    public KnowledgeResult StartTeaching(TeachKnowledgeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var error = SourceError(request.SourceId, request.Month);
        if (error is not null) return error;
        if (!ValidText(request.WorkId) || !works.TryGetValue(request.WorkId, out var work)) return Reject(KnowledgeStatus.NotFound, "Unknown work.");
        error = LearningWindow(request.Pupil, request.Month, request.Months, request.Chapters);
        if (error is not null) return error;
        if (!ValidParticipant(request.Teacher) || request.Teacher.PersonId == request.Pupil.PersonId ||
            request.Teacher.Place != request.Pupil.Place || !Enum.IsDefined(request.Transmission))
            return Reject(KnowledgeStatus.Unavailable, "Teaching needs two able participants at one place.");
        if (!CanReserve(request.Teacher.PersonId, request.Month, request.Month + request.Months))
            return Reject(KnowledgeStatus.Busy, "The teacher's time is already reserved.");
        if (request.Chapters.Any(c => !work.Chapters.Any(p => p.Number == c)))
            return Reject(KnowledgeStatus.Invalid, "Unknown chapter.");
        var adopted = request.Chapters.Order().Select(c => Adopt(request.Teacher.PersonId, request.WorkId, c)).ToArray();
        if (adopted.Any(a => a is null || a.Comprehension < state.Rules.TeachingMinimumComprehension ||
            !CanTransmit(request.Teacher.PersonId, a.EditionId, request.Month)))
            return Reject(KnowledgeStatus.Forbidden, "Teaching requires understood personal knowledge and transmission rights.");
        var adoption = adopted.Select(a => a!).ToArray();
        var claims = adoption.Select(a => new EditionChapter(a.Chapter, a.Claim)).ToArray();
        return Reserve(new(0, KnowledgeActivityKind.Teaching, KnowledgeActivityStatus.Active, request.Pupil.PersonId,
            request.Teacher.PersonId, request.Pupil.Place, request.WorkId, adoption[0].EditionId, 0,
            request.Chapters.Order().ToArray(), request.Month, request.Month + request.Months, 0, request.Transmission,
            adoption, claims, StudyCredits(request.Pupil.PersonId, request.WorkId, claims), state.Rules with { },
            0, KnowledgeMediumKind.Book, 0, null, request.SourceId, 0, ""));
    }

    public KnowledgeWritingCost QuoteTranscription(KnowledgeMediumKind kind, int chapterCount)
    {
        if (!Enum.IsDefined(kind) || chapterCount is < 1 or > 256) throw new ArgumentOutOfRangeException(nameof(chapterCount));
        return new(kind == KnowledgeMediumKind.Book ? state.Rules.PaperPerChapter * chapterCount : 0,
            kind == KnowledgeMediumKind.JadeSlip ? 0 : state.Rules.InkPerChapter * chapterCount,
            state.Rules.TranscriptionMonthsPerChapter * chapterCount);
    }

    /// <summary>Pure preflight. The world then pays/reserves inventory and starts the action in one transaction.</summary>
    public KnowledgeResult PreflightTranscription(TranscribeKnowledgeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return TranscriptionError(request) ?? Applied();
    }

    public KnowledgeResult StartTranscription(TranscribeKnowledgeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var error = TranscriptionError(request);
        if (error is not null) return error;
        var source = media[request.SourceItemId];
        var edition = editions[source.EditionId];
        var cost = QuoteTranscription(request.TargetKind, request.Chapters.Length);
        var selected = request.Chapters.Order().ToArray();
        var result = Reserve(new(0, KnowledgeActivityKind.Transcription, KnowledgeActivityStatus.Active,
            request.Scribe.PersonId, 0, request.Scribe.Place, edition.WorkId, edition.Id, source.ItemId,
            selected, request.Month, request.Month + cost.Months, 0, KnowledgeTransmission.Peer, [],
            selected.Select(c => edition.Chapters.Single(p => p.Number == c)).ToArray(), [], state.Rules with { },
            request.TargetItemId, request.TargetKind, request.TargetQuality, request.Payment with { }, request.SourceId, 0, ""));
        RecordReceipt(request.Payment.ReceiptId, "writing-payment", request.Scribe.PersonId, edition.WorkId,
            edition.Id, request.TargetItemId, selected, request.Month,
            $"paper:{request.Payment.PaperItemId}:{cost.Paper};ink:{request.Payment.InkItemId}:{cost.Ink}");
        return result;
    }

    public KnowledgeResult StartPractice(PracticeKnowledgeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var error = SourceError(request.SourceId, request.Month);
        if (error is not null) return error;
        error = LearningWindow(request.Practitioner, request.Month, request.Months, [request.Chapter]);
        if (error is not null) return error;
        var adopted = Adopt(request.Practitioner.PersonId, request.WorkId, request.Chapter, request.ExpectedPersonalRevision);
        if (adopted is null) return Reject(KnowledgeStatus.StaleRevision, "Practice must adopt an existing current personal revision.");
        if (request.ExpectedPersonalRevision <= 0 || adopted.Comprehension < state.Rules.PracticeMinimumComprehension)
            return Reject(KnowledgeStatus.Forbidden, "Practice requires sufficient personal comprehension.");
        return Reserve(new(0, KnowledgeActivityKind.Practice, KnowledgeActivityStatus.Active, request.Practitioner.PersonId,
            0, request.Practitioner.Place, request.WorkId, adopted.EditionId, 0, [request.Chapter], request.Month,
            request.Month + request.Months, 0, KnowledgeTransmission.Peer, [adopted], [], [], state.Rules with { },
            0, KnowledgeMediumKind.Book, 0, null, request.SourceId, 0, ""));
    }

    public KnowledgeResult Complete(CompleteKnowledgeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var error = SourceError(request.SourceId, request.Month);
        if (error is not null) return error;
        if (!activities.TryGetValue(request.ActivityId, out var activity)) return Reject(KnowledgeStatus.NotFound, "Unknown activity.");
        if (activity.Status != KnowledgeActivityStatus.Active) return Reject(KnowledgeStatus.Invalid, "Activity has already ended.");
        if (request.Month < activity.DueMonth) return Reject(KnowledgeStatus.TooEarly, "Actual action time has not elapsed.");
        if (request.Participants is null || request.Participants.Length != (activity.TeacherId > 0 ? 2 : 1) ||
            request.Participants.Select(p => p.PersonId).Distinct().Count() != request.Participants.Length ||
            !Participants(activity).All(id => request.Participants.Any(p => p.PersonId == id && ValidParticipant(p) && p.Place == activity.Place)))
            return Reject(KnowledgeStatus.Unavailable, "Every participant must still be able to finish at the action's place.");
        if (activity.Kind is KnowledgeActivityKind.Study or KnowledgeActivityKind.Transcription &&
            !CanRead(request.Participants.Single(p => p.PersonId == activity.ActorId), media[activity.MediumItemId],
                activity.Chapters, request.Month, activity.Kind == KnowledgeActivityKind.Study ? KnowledgeRights.Read : KnowledgeRights.Read | KnowledgeRights.Copy))
            return Reject(KnowledgeStatus.Unavailable, "The adopted physical source is no longer accessible.");
        if (activity.Kind == KnowledgeActivityKind.Teaching && activity.Adopted.Any(a => !CanTransmit(activity.TeacherId, a.EditionId, request.Month)))
            return Reject(KnowledgeStatus.Forbidden, "The teacher's transmission right has expired.");
        if (activity.Kind == KnowledgeActivityKind.Practice)
        {
            error = PracticeOutcomeError(activity, request);
            if (error is not null) return error;
        }
        else if (request.Practice is not null) return Reject(KnowledgeStatus.Invalid, "Only practice accepts a process outcome.");
        if (request.Interpretations is not null &&
            (activity.Kind is not (KnowledgeActivityKind.Study or KnowledgeActivityKind.Teaching) ||
            request.Interpretations.Any(i => i is null || !activity.Chapters.Contains(i.Chapter) ||
                !ValidText(i.Claim) || i.Confidence is < 0 or > 100) ||
            request.Interpretations.Select(i => i.Chapter).Distinct().Count() != request.Interpretations.Length))
            return Reject(KnowledgeStatus.Invalid, "Interpretations must refer only to chapters actually learned in this action.");

        if (activity.Kind is KnowledgeActivityKind.Study or KnowledgeActivityKind.Teaching) CompleteLearning(activity, request);
        else if (activity.Kind == KnowledgeActivityKind.Transcription) CompleteTranscription(activity, request);
        else CompletePractice(activity, request);
        ReplaceActivity(activity with { Status = KnowledgeActivityStatus.Completed, EndedMonth = request.Month,
            Revision = state.Revision + 1, CompletionSourceId = request.SourceId, PracticeOutcome = request.Practice,
            Interpretations = request.Interpretations?.Select(i => i with { }).ToArray() });
        Commit(request.SourceId, activity.Kind.ToString(), activity.ActorId, activity.TeacherId, activity.WorkId,
            activity.EditionId, activity.MediumItemId, activity.Chapters, activity.StartedMonth, request.Month,
            activity.Adopted.Length == 1 ? activity.Adopted[0].PersonalRevision : 0,
            activity.Kind == KnowledgeActivityKind.Teaching ? activity.Transmission.ToString() : request.Practice?.Observation ?? "");
        if (request.Practice is not null)
            RecordReceipt(request.Practice.ProcessReceiptId, "practice-process", activity.ActorId, activity.WorkId,
                activity.EditionId, 0, activity.Chapters, request.Month, request.Practice.Observation);
        return Applied(activity.Id);
    }

    /// <summary>Combat, death, travel or other interruption releases unused time. Spent writing inputs are not refunded.</summary>
    public KnowledgeResult Cancel(long activityId, long month, string sourceId)
    {
        var error = SourceError(sourceId, month);
        if (error is not null) return error;
        if (!activities.TryGetValue(activityId, out var activity)) return Reject(KnowledgeStatus.NotFound, "Unknown activity.");
        if (activity.Status != KnowledgeActivityStatus.Active || month < activity.StartedMonth)
            return Reject(KnowledgeStatus.Invalid, "Activity is not active.");
        for (var i = 0; i < state.TimeUses.Count; i++)
            if (state.TimeUses[i].ActivityId == activityId)
                state.TimeUses[i] = state.TimeUses[i] with { EndedMonth = Math.Min(month, activity.DueMonth) };
        ReplaceActivity(activity with { Status = KnowledgeActivityStatus.Cancelled, EndedMonth = month,
            CompletionSourceId = sourceId, Revision = state.Revision + 1 });
        Commit(sourceId, "interrupted", activity.ActorId, activity.TeacherId, activity.WorkId, activity.EditionId,
            activity.MediumItemId, activity.Chapters, activity.StartedMonth, month);
        return Applied(activityId);
    }

    public KnowledgeActivity? FindActivity(long activityId) => activities.TryGetValue(activityId, out var activity) ? Clone(activity) : null;

    public IReadOnlyList<long> ActiveActivities(long personId) => Array.AsReadOnly(activities.Values
        .Where(a => a.Status == KnowledgeActivityStatus.Active && Participants(a).Contains(personId))
        .OrderBy(a => a.Id).Select(a => a.Id).ToArray());

    private KnowledgeResult? TranscriptionError(TranscribeKnowledgeRequest request)
    {
        var error = SourceError(request.SourceId, request.Month);
        if (error is not null) return error;
        if (!media.TryGetValue(request.SourceItemId, out var source)) return Reject(KnowledgeStatus.NotFound, "Unknown source medium.");
        if (!Enum.IsDefined(request.TargetKind) || request.Chapters is null || request.Chapters.Length is < 1 or > 256)
            return Reject(KnowledgeStatus.Invalid, "A transcription needs valid physical media and selected chapters.");
        var cost = QuoteTranscription(request.TargetKind, request.Chapters.Length);
        error = LearningWindow(request.Scribe, request.Month, cost.Months, request.Chapters);
        if (error is not null) return error;
        if (!CanRead(request.Scribe, source, request.Chapters, request.Month, KnowledgeRights.Read | KnowledgeRights.Copy))
            return Reject(KnowledgeStatus.Forbidden, "Copying needs physical access and explicit copy rights.");
        if (request.TargetItemId <= 0 || request.TargetItemId == request.SourceItemId || request.TargetQuality is < 0 or > 6 ||
            media.ContainsKey(request.TargetItemId) || activities.Values.Any(a => a.Status == KnowledgeActivityStatus.Active && a.TargetItemId == request.TargetItemId))
            return Reject(KnowledgeStatus.Invalid, "The target must be an unbound, reserved physical item.");
        var payment = request.Payment;
        if (payment is null || !ValidText(payment.ReceiptId) || payment.ReceiptId == request.SourceId ||
            payment.BlankMediumItemId != request.TargetItemId || payment.Paper != cost.Paper || payment.Ink != cost.Ink ||
            !ValidMaterialItem(payment.PaperItemId, payment.Paper) || !ValidMaterialItem(payment.InkItemId, payment.Ink) ||
            payment.Paper > 0 && payment.Ink > 0 && payment.PaperItemId == payment.InkItemId ||
            new[] { request.SourceItemId, request.TargetItemId }.Any(id => id == payment.PaperItemId || id == payment.InkItemId))
            return Reject(KnowledgeStatus.InsufficientMaterials, "Payment must identify the reserved blank medium and exact real paper/ink inputs.");
        return sources.Contains(payment.ReceiptId) ? Reject(KnowledgeStatus.DuplicateSource, "Writing receipt already consumed.") : null;
    }

    private KnowledgeResult? LearningWindow(KnowledgeParticipant actor, long month, int months, int[] chapters)
    {
        if (!ValidParticipant(actor) || chapters is null || chapters.Length is < 1 or > 256 ||
            chapters.Any(c => c <= 0) || chapters.Distinct().Count() != chapters.Length || months < chapters.Length ||
            months > state.Rules.MaximumActionMonths || month > long.MaxValue - months)
            return Reject(KnowledgeStatus.Invalid, "An able actor, distinct chapters and a bounded positive action duration are required.");
        return CanReserve(actor.PersonId, month, month + months) ? null : Reject(KnowledgeStatus.Busy, "The actor's time is already reserved.");
    }

    private KnowledgeResult Reserve(KnowledgeActivity activity)
    {
        var id = state.NextId++;
        activity = activity with { Id = id, Revision = state.Revision + 1 };
        state.Activities.Add(activity); activities.Add(id, activity);
        foreach (var person in Participants(activity)) state.TimeUses.Add(new(person, activity.StartedMonth, activity.DueMonth, id));
        Commit(activity.SourceId, "begin-" + activity.Kind, activity.ActorId, activity.TeacherId, activity.WorkId,
            activity.EditionId, activity.MediumItemId, activity.Chapters, activity.StartedMonth, activity.StartedMonth);
        return Applied(id);
    }

    private void CompleteLearning(KnowledgeActivity activity, CompleteKnowledgeRequest request)
    {
        var work = works[activity.WorkId];
        var interpreted = activity with { Interpretations = request.Interpretations };
        for (var i = 0; i < activity.Chapters.Length; i++)
            AppendMemory(LearningResult(interpreted, work, i, FindKnowledge(activity.ActorId, activity.WorkId, activity.Chapters[i]),
                request.Month, state.Revision + 1, request.SourceId));
    }

    private static PersonalKnowledge LearningResult(KnowledgeActivity activity, KnowledgeWork work, int index,
        PersonalKnowledge? previous, long month, long revision, string sourceId)
    {
        var elapsed = (int)(activity.DueMonth - activity.StartedMonth);
        var chapter = activity.Chapters[index];
        var interpretation = activity.Interpretations?.SingleOrDefault(p => p.Chapter == chapter);
        var claim = interpretation?.Claim ?? activity.Claims[index].Claim;
        var credits = claim == activity.Claims[index].Claim ? activity.StudyCredits[index] : previous?.Claim == claim ? previous.StudyMonths : 0;
        var months = (int)Math.Min(int.MaxValue, (long)credits + elapsed / activity.Chapters.Length +
            (index < elapsed % activity.Chapters.Length ? 1 : 0));
        var cap = activity.Kind == KnowledgeActivityKind.Teaching ? activity.Adopted[index].Comprehension : 100;
        var comprehension = (int)Math.Min(cap, (long)months * 100 / work.Chapters.Single(c => c.Number == chapter).StudyMonths);
        if (previous?.Claim == claim) comprehension = Math.Max(previous.Comprehension, comprehension);
        var belief = previous?.Claim == claim ? previous.Belief :
            previous?.Belief == KnowledgeBelief.Contradicted ? KnowledgeBelief.Corrected : KnowledgeBelief.Unverified;
        return new(activity.ActorId, activity.WorkId, chapter,
            activity.Kind == KnowledgeActivityKind.Teaching ? activity.Adopted[index].EditionId : activity.EditionId,
            claim, months, 100, comprehension, interpretation?.Confidence ?? (previous?.Claim == claim ? previous.Confidence : 50), belief,
            previous?.Revision ?? 0, revision, month, sourceId);
    }

    private void CompleteTranscription(KnowledgeActivity activity, CompleteKnowledgeRequest request)
    {
        var id = state.NextId++;
        var edition = new KnowledgeEdition(id, activity.WorkId, activity.EditionId, EditionRelation.Copy,
            activity.ActorId, activity.Claims.Select(c => c with { }).ToArray(), request.Month, state.Revision + 1, request.SourceId);
        state.Editions.Add(edition); editions.Add(id, edition);
        var rights = RightsFor(activity.ActorId, media[activity.MediumItemId], request.Month);
        var medium = new KnowledgeMedium(activity.TargetItemId, id, activity.ActorId, activity.Place,
            activity.TargetKind, activity.TargetQuality, rights, [], false, false, state.Revision + 1, request.SourceId);
        state.Media.Add(medium); media.Add(medium.ItemId, medium);
    }

    private KnowledgeResult? PracticeOutcomeError(KnowledgeActivity activity, CompleteKnowledgeRequest request)
    {
        var outcome = request.Practice;
        var adopted = activity.Adopted[0];
        if (outcome is null || !outcome.Performed || outcome.PersonId != activity.ActorId ||
            outcome.AdoptedRevision != adopted.PersonalRevision || !ValidText(outcome.ProcessReceiptId) ||
            outcome.ProcessReceiptId == request.SourceId || !Enum.IsDefined(outcome.TestResult) ||
            outcome.Observation is null || outcome.Observation.Length > 4096 ||
            outcome.TestResult != KnowledgeTestResult.None && !ValidText(outcome.Observation))
            return Reject(KnowledgeStatus.Invalid, "Practice needs a performed world process and evidence for the exact adopted revision.");
        if (sources.Contains(outcome.ProcessReceiptId)) return Reject(KnowledgeStatus.DuplicateSource, "Process receipt already consumed.");
        return FindKnowledge(activity.ActorId, activity.WorkId, adopted.Chapter)?.Revision == adopted.PersonalRevision ?
            null : Reject(KnowledgeStatus.StaleRevision, "Process evidence cannot silently rewrite a newer personal revision.");
    }

    private void CompletePractice(KnowledgeActivity activity, CompleteKnowledgeRequest request)
    {
        var outcome = request.Practice!;
        var adopted = activity.Adopted[0];
        if (outcome.Successful)
        {
            var key = (activity.ActorId, activity.WorkId, adopted.Chapter);
            var months = (int)Math.Min(int.MaxValue, (long)(mastery.GetValueOrDefault(key)?.PracticeMonths ?? 0) +
                activity.DueMonth - activity.StartedMonth);
            var required = works[activity.WorkId].Chapters.Single(c => c.Number == adopted.Chapter).PracticeMonths;
            var skill = new PracticalKnowledge(activity.ActorId, activity.WorkId, adopted.Chapter, months,
                (int)Math.Min(100, (long)months * 100 / required), state.Revision + 1, request.SourceId);
            state.Mastery.Add(skill); mastery[key] = skill;
        }
        if (outcome.TestResult == KnowledgeTestResult.None) return;
        var previous = FindKnowledge(activity.ActorId, activity.WorkId, adopted.Chapter)!;
        AppendMemory(previous with { Belief = outcome.TestResult == KnowledgeTestResult.SupportsClaim ? KnowledgeBelief.Supported : KnowledgeBelief.Contradicted,
            Confidence = outcome.TestResult == KnowledgeTestResult.SupportsClaim ? 90 : 10,
            PreviousRevision = previous.Revision, Revision = state.Revision + 1, LearnedMonth = request.Month, SourceId = request.SourceId });
    }

    private int[] StudyCredits(long personId, string workId, EditionChapter[] claims) => claims.Select(c =>
    {
        var previous = FindKnowledge(personId, workId, c.Number);
        return previous?.Claim == c.Claim ? previous.StudyMonths : 0;
    }).ToArray();

    private bool CanReserve(long personId, long started, long ended) => !state.TimeUses.Any(t =>
        t.PersonId == personId && t.StartedMonth < ended && started < t.EndedMonth) &&
        !activities.Values.Any(a => a.Status == KnowledgeActivityStatus.Active && Participants(a).Contains(personId));

    private static IEnumerable<long> Participants(KnowledgeActivity activity) => activity.TeacherId > 0 ?
        new[] { activity.ActorId, activity.TeacherId } : [activity.ActorId];

    private KnowledgeRights RightsFor(long person, KnowledgeMedium medium, long month)
    {
        var rights = medium.OwnerId == person ? medium.OwnerRights : KnowledgeRights.None;
        foreach (var access in state.Access)
            if (access.PersonId == person && access.ItemId == medium.ItemId && access.GrantedMonth <= month && access.ExpiresMonth > month)
                rights |= access.Rights;
        return rights;
    }

    private bool CanRead(KnowledgeParticipant person, KnowledgeMedium medium, int[] chapters, long month, KnowledgeRights rights) =>
        !medium.Destroyed && !medium.Unreadable && medium.Place == person.Place &&
        (RightsFor(person.PersonId, medium, month) & rights) == rights && ValidSelection(editions[medium.EditionId], chapters) &&
        chapters.All(c => !medium.MissingChapters.Contains(c));

    private bool CanTransmit(long person, long edition, long month) => media.Values.Any(m =>
        m.EditionId == edition && (RightsFor(person, m, month) & KnowledgeRights.Teach) != 0);

    private void RecordReceipt(string sourceId, string kind, long person, string work, long edition, long item,
        int[] chapters, long month, string detail)
    {
        sources.Add(sourceId);
        state.Evidence.Add(new(sourceId, kind, person, 0, work, edition, item, chapters.ToArray(), month, month, 0, state.Revision, detail));
    }
}
