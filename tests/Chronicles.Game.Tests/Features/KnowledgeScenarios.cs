using System.Globalization;
using System.Text.Json;
using Chronicles.Game.Knowledge;

namespace Chronicles.Game.Tests.Features;

public sealed class KnowledgeAccessibleSpatialFilterScenario : IFeatureScenario
{
    public string Name => "INV-009/024 knowledge: 허가한 실제 매체를 전체수·정렬·페이지 전에 필터하여 바깥 도서를 인지하지 않는다";
    public void Run()
    {
        var authority = KnowledgeFixture.Fresh();
        for (var id = 1000; id < 1129; id++)
            KnowledgeFixture.Ok(authority.BindMedium(id, 1, 1, 0, KnowledgeMediumKind.Book, 0,
                KnowledgeRights.Read, [], false, 0, "외부 실물 검사 " + id));
        KnowledgeFixture.Ok(authority.BindMedium(9999, 1, 1, 0, KnowledgeMediumKind.Book, 0,
            KnowledgeRights.Read, [], false, 0, "실제 허가한 휴대 매체"));
        var before = KnowledgeFixture.Json(authority); var allowed = new HashSet<long> { 9999 };
        var page = authority.ViewAccessible(1, 0, 0, limit: 1, allowedItems: allowed);
        KnowledgeFixture.Check(page.Total == 1 && page.Rows is [{ ItemId: 9999 }], "바깥 도서가 첫 페이지나 전체수에 영향을 주었다.");
        KnowledgeFixture.Check(authority.ViewAccessible(1, 0, 0, offset: 1, allowedItems: allowed).Rows.Count == 0 &&
            authority.ViewAccessible(1, 0, 0, allowedItems: new HashSet<long>()).Total == 0 &&
            authority.ViewAccessible(1, 0, 0).Total == 131, "순수 허가 매체 필터나 기존 호출의 권한 계약이 달라졌다.");
        KnowledgeFixture.Check(KnowledgeFixture.Json(authority) == before, "허가 매체 조회가 권위나 개인 학습을 만들었다.");
    }
}

internal static class KnowledgeFixture
{
    internal const string Work = "craft.solvent";
    internal static readonly KnowledgeParticipant One = new(1, 0, true);
    internal static readonly KnowledgeParticipant Two = new(2, 0, true);
    internal static KnowledgeAuthority Fresh()
    {
        var authority = new KnowledgeAuthority();
        Ok(authority.DefineWork(new(Work, "용매의 기록", "alchemy", KnowledgeKind.Craft,
            [new(1, "정제", 2, 4), new(2, "보관", 2, 4), new(3, "용기", 2, 4)], 0, "work")));
        var edition = Ok(authority.CreateEdition(Work, 0, EditionRelation.Original, 1,
            [new(1, "먼저 가열한다"), new(2, "밀봉한다"), new(3, "유리 용기를 쓴다")], 0, "edition"));
        Ok(authority.BindMedium(101, edition, 1, 0, KnowledgeMediumKind.Book, 0,
            KnowledgeRights.Read | KnowledgeRights.Copy | KnowledgeRights.Teach, [], false, 0, "book"));
        return authority;
    }
    internal static long Ok(KnowledgeResult result)
    {
        Check(result.Applied, result.Status + ": " + result.Message);
        return result.Id;
    }
    internal static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    internal static string Json(KnowledgeAuthority authority) => JsonSerializer.Serialize(authority.Checkpoint());
    internal static KnowledgeAuthority RoundTrip(KnowledgeAuthority authority)
    {
        var before = Json(authority);
        var restored = KnowledgeAuthority.Restore(JsonSerializer.Deserialize<KnowledgeState>(before)!);
        Check(before == Json(restored), "Knowledge JSON round trip changed authority state.");
        return restored;
    }
    internal static void Rejected(KnowledgeAuthority authority, KnowledgeStatus status, Func<KnowledgeResult> action)
    {
        var before = Json(authority);
        var result = action();
        Check(result.Status == status, $"Expected {status}, received {result.Status}: {result.Message}");
        Check(before == Json(authority), "Rejected transition mutated knowledge state.");
    }
    internal static long Study(KnowledgeAuthority authority, long item, int[] chapters, long month, int months, string source)
    {
        var id = Ok(authority.StartStudy(new(source, One, item, chapters, month, months)));
        Ok(authority.Complete(new(source + ".done", id, month + months, [One])));
        return id;
    }
    internal static TranscribeKnowledgeRequest Copy(string source = "copy", long month = 0, long target = 500,
        string receipt = "paper-ink", long sourceItem = 101) => new(source, One, sourceItem, [1, 3], target,
            KnowledgeMediumKind.Book, 0, new(receipt, target, 201, 2, 202, 2), month);
}

public sealed class KnowledgeOwnershipScenario : IFeatureScenario
{
    public string Name => "INV-024 knowledge: physical ownership grants neither comprehension nor mastery";
    public void Run()
    {
        var a = KnowledgeFixture.Fresh();
        KnowledgeFixture.Check(a.ViewPersonal(1).Total == 0 && a.Adopt(1, KnowledgeFixture.Work, 1) is null, "Owning a book learned its contents.");
        KnowledgeFixture.Check(a.ViewAccessible(1, 0, 0).Rows.Single().Quality == 0, "Defect-free lowest-quality book disappeared.");
        KnowledgeFixture.Rejected(a, KnowledgeStatus.Forbidden, () => a.StartStudy(new("steal", KnowledgeFixture.Two, 101, [1], 0, 1)));
        var id = KnowledgeFixture.Ok(a.StartStudy(new("study", KnowledgeFixture.One, 101, [1], 0, 1)));
        KnowledgeFixture.Rejected(a, KnowledgeStatus.TooEarly, () => a.Complete(new("early", id, 0, [KnowledgeFixture.One])));
        a = KnowledgeFixture.RoundTrip(a);
        KnowledgeFixture.Ok(a.Complete(new("study.done", id, 1, [KnowledgeFixture.One])));
        var known = a.ViewPersonal(1).Rows.Single();
        KnowledgeFixture.Check(known.Comprehension == 50 && known.Decoding == 100 && known.Mastery == 0, "Reading incorrectly awarded practical mastery.");
        KnowledgeFixture.Check(a.ViewPersonal(2).Total == 0 && a.Checkpoint().Mastery.Count == 0, "Knowledge leaked to another person.");
        KnowledgeFixture.RoundTrip(a);
    }
}

public sealed class KnowledgeDefectsScenario : IFeatureScenario
{
    public string Name => "INV-015/024 knowledge: physical missing pages and book loss preserve learned memory";
    public void Run()
    {
        var a = KnowledgeFixture.Fresh();
        KnowledgeFixture.Ok(a.SynchronizeMedium(101, 1, 0, 0, [2], false, false, 0, "missing-page"));
        KnowledgeFixture.Rejected(a, KnowledgeStatus.Forbidden, () => a.StartStudy(new("missing", KnowledgeFixture.One, 101, [2], 0, 1)));
        KnowledgeFixture.Study(a, 101, [1], 0, 2, "read-present");
        var memory = a.FindKnowledge(1, KnowledgeFixture.Work, 1);
        KnowledgeFixture.Ok(a.SynchronizeMedium(101, 1, 0, 6, [2], true, false, 2, "lost-core"));
        KnowledgeFixture.Rejected(a, KnowledgeStatus.Forbidden, () => a.StartStudy(new("unreadable", KnowledgeFixture.One, 101, [1], 2, 1)));
        KnowledgeFixture.Ok(a.SynchronizeMedium(101, 1, 0, 6, [2], true, true, 2, "destroyed"));
        KnowledgeFixture.Check(a.ViewAccessible(1, 0, 2).Total == 0 && a.FindKnowledge(1, KnowledgeFixture.Work, 1) == memory,
            "Physical destruction deleted or changed personal knowledge.");
        KnowledgeFixture.Rejected(a, KnowledgeStatus.Invalid, () => a.SynchronizeMedium(101, 1, 0, 6, [], false, false, 2, "resurrect"));
        KnowledgeFixture.Check(a.FindKnowledge(1, KnowledgeFixture.Work, 2) is null, "Unread missing chapter was awarded.");
        KnowledgeFixture.RoundTrip(a);
    }
}

public sealed class KnowledgeCorrectionScenario : IFeatureScenario
{
    public string Name => "INV-024 knowledge: experiments refute adopted claims and corrections never rewrite history";
    public void Run()
    {
        var a = KnowledgeFixture.Fresh();
        KnowledgeFixture.Study(a, 101, [1], 0, 2, "first-study");
        var frozen = a.Adopt(1, KnowledgeFixture.Work, 1)!;
        var practice = KnowledgeFixture.Ok(a.StartPractice(new("experiment", KnowledgeFixture.One, KnowledgeFixture.Work, 1, frozen.PersonalRevision, 2, 1)));
        var corrected = KnowledgeFixture.Ok(a.CreateEdition(KnowledgeFixture.Work, 1, EditionRelation.Revision, 1,
            [new(1, "먼저 여과한다"), new(2, "차광한다")], 2, "corrected-edition"));
        KnowledgeFixture.Ok(a.BindMedium(102, corrected, 1, 0, KnowledgeMediumKind.Book, 6, KnowledgeRights.Read,
            [], false, 2, "corrected-book"));
        KnowledgeFixture.Check(a.Adopt(1, KnowledgeFixture.Work, 1) == frozen, "Publishing a correction changed a reader retroactively.");
        a = KnowledgeFixture.RoundTrip(a);
        KnowledgeFixture.Ok(a.Complete(new("experiment.done", practice, 3, [KnowledgeFixture.One],
            new("actual-failed-process", 1, frozen.PersonalRevision, true, false, KnowledgeTestResult.RefutesClaim, "가열 후 침전 발생"))));
        var contradicted = a.FindKnowledge(1, KnowledgeFixture.Work, 1)!;
        KnowledgeFixture.Check(contradicted.Belief == KnowledgeBelief.Contradicted && contradicted.Claim == frozen.Claim,
            "An observation supplied an unlearned correct answer.");
        KnowledgeFixture.Study(a, 102, [1], 3, 2, "read-correction");
        var revised = a.FindKnowledge(1, KnowledgeFixture.Work, 1)!;
        KnowledgeFixture.Check(revised.Belief == KnowledgeBelief.Corrected && revised.Claim == "먼저 여과한다" && revised.PreviousRevision == contradicted.Revision,
            "Correction did not preserve the personal revision chain.");
        KnowledgeFixture.Check(frozen.Claim == "먼저 가열한다" && a.FindPersonalRevision(1, KnowledgeFixture.Work, 1, frozen.PersonalRevision)?.Claim == frozen.Claim,
            "A previously adopted execution source was rewritten.");
        KnowledgeFixture.Check(a.FindKnowledge(1, KnowledgeFixture.Work, 2) is null && a.ViewPersonal(1).Rows.Single().Mastery == 0,
            "Correction awarded unread chapters or practical mastery.");
        KnowledgeFixture.RoundTrip(a);
    }
}

public sealed class KnowledgeInterpretationScenario : IFeatureScenario
{
    public string Name => "INV-024 knowledge: personal misinterpretation and oral transmission do not mutate editions";
    public void Run()
    {
        var a = KnowledgeFixture.Fresh();
        var study = KnowledgeFixture.Ok(a.StartStudy(new("misread", KnowledgeFixture.One, 101, [1], 0, 2)));
        KnowledgeFixture.Ok(a.Complete(new("misread.done", study, 2, [KnowledgeFixture.One],
            Interpretations: [new(1, "두 번 가열한다", 80)])));
        KnowledgeFixture.Check(a.Checkpoint().Editions[0].Chapters[0].Claim == "먼저 가열한다", "A reader changed the physical edition.");
        KnowledgeFixture.Check(a.FindKnowledge(1, KnowledgeFixture.Work, 1)?.Claim == "두 번 가열한다", "Personal interpretation was replaced with hidden truth.");
        var lesson = KnowledgeFixture.Ok(a.StartTeaching(new("oral", KnowledgeFixture.One, KnowledgeFixture.Two,
            KnowledgeFixture.Work, [1], KnowledgeTransmission.Mentor, 2, 2)));
        a = KnowledgeFixture.RoundTrip(a);
        KnowledgeFixture.Ok(a.Complete(new("oral.done", lesson, 4, [KnowledgeFixture.One, KnowledgeFixture.Two])));
        KnowledgeFixture.Check(a.FindKnowledge(2, KnowledgeFixture.Work, 1)?.Claim == "두 번 가열한다" && a.ViewPersonal(2).Rows.Single().Mastery == 0,
            "Teaching used the edition's answer instead of the teacher's personal interpretation.");
        KnowledgeFixture.RoundTrip(a);
    }
}

public sealed class KnowledgeTeachingScenario : IFeatureScenario
{
    public string Name => "INV-006/012/024 knowledge: parents mentors and apprentices spend both participants' time";
    public void Run()
    {
        foreach (var transmission in Enum.GetValues<KnowledgeTransmission>())
        {
            var a = KnowledgeFixture.Fresh();
            KnowledgeFixture.Ok(a.SeedInitialKnowledge(1, 1, [1], 100, "genesis"));
            KnowledgeFixture.Check(a.ViewPersonal(2).Total == 0, "A child inherited a parent's skill without learning.");
            KnowledgeFixture.Rejected(a, KnowledgeStatus.Forbidden, () => a.StartTeaching(new("unknown", KnowledgeFixture.One,
                KnowledgeFixture.Two, KnowledgeFixture.Work, [2], transmission, 0, 2)));
            var lesson = KnowledgeFixture.Ok(a.StartTeaching(new("lesson", KnowledgeFixture.One, KnowledgeFixture.Two,
                KnowledgeFixture.Work, [1], transmission, 0, 2)));
            KnowledgeFixture.Rejected(a, KnowledgeStatus.Busy, () => a.StartStudy(new("double-time", KnowledgeFixture.One, 101, [1], 0, 1)));
            a = KnowledgeFixture.RoundTrip(a);
            KnowledgeFixture.Rejected(a, KnowledgeStatus.Unavailable, () => a.Complete(new("absent", lesson, 2,
                [KnowledgeFixture.One with { CanParticipate = false }, KnowledgeFixture.Two])));
            KnowledgeFixture.Ok(a.Cancel(lesson, 1, "interruption"));
            KnowledgeFixture.Check(a.ViewPersonal(2).Total == 0 && a.ActiveActivities(1).Count == 0 && a.ActiveActivities(2).Count == 0,
                "An interrupted lesson awarded progress or kept one participant reserved.");
            var retry = KnowledgeFixture.Ok(a.StartTeaching(new("retry", KnowledgeFixture.One, KnowledgeFixture.Two,
                KnowledgeFixture.Work, [1], transmission, 1, 2)));
            KnowledgeFixture.Ok(a.Complete(new("retry.done", retry, 3, [KnowledgeFixture.One, KnowledgeFixture.Two])));
            KnowledgeFixture.Check(a.FindKnowledge(2, KnowledgeFixture.Work, 1)?.Comprehension == 100 &&
                a.ViewPersonal(2).Rows.Single().Mastery == 0 && a.Checkpoint().Evidence.Any(e => e.Detail == transmission.ToString()),
                "Teaching omitted provenance or awarded free practical experience.");
            KnowledgeFixture.RoundTrip(a);
        }
    }
}

public sealed class KnowledgeTranscriptionScenario : IFeatureScenario
{
    public string Name => "INV-004/024 knowledge: transcription reserves a physical target time and exact one-use inputs";
    public void Run()
    {
        var a = KnowledgeFixture.Fresh();
        KnowledgeFixture.Ok(a.SynchronizeMedium(101, 1, 0, 0, [2], false, false, 0, "missing"));
        var request = KnowledgeFixture.Copy();
        var before = KnowledgeFixture.Json(a);
        KnowledgeFixture.Ok(a.PreflightTranscription(request));
        KnowledgeFixture.Check(before == KnowledgeFixture.Json(a), "Transcription preflight mutated state.");
        KnowledgeFixture.Rejected(a, KnowledgeStatus.InsufficientMaterials, () => a.StartTranscription(request with { Payment = request.Payment with { Ink = 1 } }));
        KnowledgeFixture.Rejected(a, KnowledgeStatus.Forbidden, () => a.StartTranscription(request with { Chapters = [1, 2] }));
        var copy = KnowledgeFixture.Ok(a.StartTranscription(request));
        KnowledgeFixture.Check(a.Checkpoint().Editions.Count == 1 && a.ViewPersonal(1).Total == 0, "Copying minted an early edition or free knowledge.");
        KnowledgeFixture.Rejected(a, KnowledgeStatus.Invalid, () => a.BindMedium(500, 1, 1, 0, KnowledgeMediumKind.Book,
            0, KnowledgeRights.Read, [], false, 0, "reserved-target"));
        a = KnowledgeFixture.RoundTrip(a);
        KnowledgeFixture.Ok(a.Complete(new("copy.done", copy, 2, [KnowledgeFixture.One])));
        var state = a.Checkpoint();
        var output = state.Media.Single(m => m.ItemId == 500);
        var edition = state.Editions.Single(e => e.Id == output.EditionId);
        KnowledgeFixture.Check(edition.ParentEditionId == 1 && edition.Relation == EditionRelation.Copy &&
            edition.Chapters.Length == 2 && edition.Chapters[0].Number == 1 && edition.Chapters[1].Number == 3 && output.Quality == 0,
            "Copy lost lineage, restored unread missing pages, or confused quality with a defect.");
        KnowledgeFixture.Rejected(a, KnowledgeStatus.DuplicateSource, () => a.StartTranscription(KnowledgeFixture.Copy("copy-again", 2, 501)));
        KnowledgeFixture.Check(a.ViewPersonal(1).Total == 0 && state.Mastery.Count == 0, "Transcription awarded learning or mastery.");
        KnowledgeFixture.RoundTrip(a);
    }
}

public sealed class KnowledgeInterruptionScenario : IFeatureScenario
{
    public string Name => "INV-004/024 knowledge: loss of an action's physical source prevents completion without refunding spent inputs";
    public void Run()
    {
        var a = KnowledgeFixture.Fresh();
        var copy = KnowledgeFixture.Ok(a.StartTranscription(KnowledgeFixture.Copy()));
        KnowledgeFixture.Ok(a.SynchronizeMedium(101, 1, 0, 0, [], false, true, 1, "source-destroyed"));
        KnowledgeFixture.Rejected(a, KnowledgeStatus.Unavailable, () => a.Complete(new("lost-source", copy, 2, [KnowledgeFixture.One])));
        KnowledgeFixture.Ok(a.Cancel(copy, 2, "copy-cancelled"));
        KnowledgeFixture.Check(a.Checkpoint().Media.Count == 1 && a.Checkpoint().Editions.Count == 1 && a.Checkpoint().Evidence.Count(e => e.Kind == "writing-payment") == 1,
            "Interrupted transcription created output or refunded its spent receipt.");
        KnowledgeFixture.Ok(a.BindMedium(102, 1, 1, 0, KnowledgeMediumKind.Book, 1, KnowledgeRights.Read | KnowledgeRights.Copy,
            [], false, 2, "other-source"));
        KnowledgeFixture.Rejected(a, KnowledgeStatus.DuplicateSource, () => a.StartTranscription(KnowledgeFixture.Copy("replay-payment", 2, 501, sourceItem: 102)));
        KnowledgeFixture.RoundTrip(a);
    }
}

public sealed class KnowledgePracticeScenario : IFeatureScenario
{
    public string Name => "INV-004/024 knowledge: only timed successful real practice earns mastery and process receipts cannot replay";
    public void Run()
    {
        var a = KnowledgeFixture.Fresh();
        KnowledgeFixture.Ok(a.SeedInitialKnowledge(1, 1, [1], 100, "initial"));
        var adopted = a.Adopt(1, KnowledgeFixture.Work, 1)!;
        KnowledgeFixture.Rejected(a, KnowledgeStatus.StaleRevision, () => a.StartPractice(new("stale", KnowledgeFixture.One,
            KnowledgeFixture.Work, 1, adopted.PersonalRevision + 1, 0, 1)));
        var failed = KnowledgeFixture.Ok(a.StartPractice(new("attempt", KnowledgeFixture.One, KnowledgeFixture.Work, 1, adopted.PersonalRevision, 0, 1)));
        KnowledgeFixture.Rejected(a, KnowledgeStatus.Invalid, () => a.Complete(new("no-process", failed, 1, [KnowledgeFixture.One])));
        KnowledgeFixture.Ok(a.Complete(new("attempt.done", failed, 1, [KnowledgeFixture.One],
            new("failed-process", 1, adopted.PersonalRevision, true, false, KnowledgeTestResult.None, "실제 실패"))));
        KnowledgeFixture.Check(a.ViewPersonal(1).Rows.Single().Mastery == 0, "An unsuccessful attempt awarded mastery.");
        var good = KnowledgeFixture.Ok(a.StartPractice(new("practice", KnowledgeFixture.One, KnowledgeFixture.Work, 1, adopted.PersonalRevision, 1, 2)));
        KnowledgeFixture.Ok(a.Complete(new("practice.done", good, 3, [KnowledgeFixture.One],
            new("successful-process", 1, adopted.PersonalRevision, true, true, KnowledgeTestResult.SupportsClaim, "실제 산출물 확인"))));
        var known = a.ViewPersonal(1).Rows.Single();
        KnowledgeFixture.Check(known.Mastery == 50 && known.Belief == KnowledgeBelief.Supported, "Mastery did not follow actual successful practice duration.");
        a = KnowledgeFixture.RoundTrip(a);
        var next = KnowledgeFixture.Ok(a.StartPractice(new("next", KnowledgeFixture.One, KnowledgeFixture.Work, 1, known.PersonalRevision, 3, 1)));
        KnowledgeFixture.Rejected(a, KnowledgeStatus.DuplicateSource, () => a.Complete(new("reused-process", next, 4, [KnowledgeFixture.One],
            new("successful-process", 1, known.PersonalRevision, true, true, KnowledgeTestResult.None, ""))));
        KnowledgeFixture.Ok(a.Complete(new("next.done", next, 4, [KnowledgeFixture.One],
            new("next-process", 1, known.PersonalRevision, true, true, KnowledgeTestResult.None, ""))));
        KnowledgeFixture.Check(a.ViewPersonal(1, changedSinceRevision: known.ChangedRevision).Rows.Single().Mastery == 75,
            "Changed personal projection missed an independent mastery revision.");
        KnowledgeFixture.Rejected(a, KnowledgeStatus.DuplicateSource, () => a.Complete(new("next.done", next, 4, [KnowledgeFixture.One])));
        KnowledgeFixture.RoundTrip(a);
    }
}

public sealed class KnowledgeSaveValidationScenario : IFeatureScenario
{
    public string Name => "INV-004/024 knowledge: corrupt lineage mastery adoption time and provenance saves are rejected";
    public void Run()
    {
        var a = KnowledgeFixture.Fresh();
        KnowledgeFixture.Ok(a.SeedInitialKnowledge(1, 1, [1], 100, "initial"));
        var adopted = a.Adopt(1, KnowledgeFixture.Work, 1)!;
        var id = KnowledgeFixture.Ok(a.StartPractice(new("practice", KnowledgeFixture.One, KnowledgeFixture.Work, 1, adopted.PersonalRevision, 0, 1)));
        KnowledgeFixture.Ok(a.Complete(new("practice.done", id, 1, [KnowledgeFixture.One],
            new("process", 1, adopted.PersonalRevision, true, true, KnowledgeTestResult.None, ""))));
        KnowledgeFixture.Ok(a.StartTeaching(new("active-lesson", KnowledgeFixture.One, KnowledgeFixture.Two, KnowledgeFixture.Work,
            [1], KnowledgeTransmission.Parent, 1, 2)));
        KnowledgeFixture.RoundTrip(a);
        Action<KnowledgeState>[] corruptions =
        [
            s => s.Editions[0] = s.Editions[0] with { Relation = EditionRelation.Revision, ParentEditionId = s.Editions[0].Id },
            s => s.Media.Add(s.Media[0]),
            s => s.Memories.Add(s.Memories[0]),
            s => s.Mastery[0] = s.Mastery[0] with { PracticeMonths = 999, Mastery = 100 },
            s => s.TimeUses.RemoveAt(0),
            s => s.Evidence.Add(s.Evidence[0]),
            s => s.Revision++,
            s => s.Memories[0] = s.Memories[0] with { SourceId = "no-real-source" },
            s => s.Activities[1].Adopted[0] = s.Activities[1].Adopted[0] with { Claim = "hidden replacement" },
            s => s.Activities[0] = s.Activities[0] with { PracticeOutcome = s.Activities[0].PracticeOutcome! with { Successful = false } }
        ];
        foreach (var corrupt in corruptions)
        {
            var state = a.Checkpoint();
            corrupt(state);
            var rejected = false;
            try { KnowledgeAuthority.Restore(state); }
            catch (InvalidDataException) { rejected = true; }
            KnowledgeFixture.Check(rejected, "Corrupted knowledge save was accepted.");
        }
        var studied = KnowledgeFixture.Fresh();
        KnowledgeFixture.Study(studied, 101, [1], 0, 1, "timed-reading");
        Action<KnowledgeState>[] readingCorruptions =
        [
            s => s.Memories[0] = s.Memories[0] with { Comprehension = 100 },
            s => s.Memories[0] = s.Memories[0] with { Claim = "unlearned replacement" },
            s => s.Memories.Clear(),
            s => s.Activities[0].StudyCredits[0] = 99
        ];
        foreach (var corrupt in readingCorruptions)
        {
            var state = studied.Checkpoint();
            corrupt(state);
            var rejected = false;
            try { KnowledgeAuthority.Restore(state); }
            catch (InvalidDataException) { rejected = true; }
            KnowledgeFixture.Check(rejected, "A save fabricated or removed the output of a real learning action.");
        }
    }
}

public sealed class KnowledgeQueryScenario : IFeatureScenario
{
    public string Name => "INV-006/008/024 knowledge: scoped paged queries are pure isolated and culture independent";
    public void Run()
    {
        var a = KnowledgeFixture.Fresh();
        KnowledgeFixture.Ok(a.SeedInitialKnowledge(1, 1, [1, 2], 100, "initial"));
        foreach (var key in new[] { "I", "i", "İ" })
        {
            KnowledgeFixture.Ok(a.DefineWork(new(key, key, key, KnowledgeKind.History, [new(1, "기록", 1, 1)], 0, "work-" + key)));
            var edition = KnowledgeFixture.Ok(a.CreateEdition(key, 0, EditionRelation.Original, 1, [new(1, key)], 0, "edition-" + key));
            KnowledgeFixture.Ok(a.SeedInitialKnowledge(1, edition, [1], 0, "initial-" + key));
        }
        var before = KnowledgeFixture.Json(a);
        var oldCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var english = JsonSerializer.Serialize(a.ViewPersonal(1));
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            KnowledgeFixture.Check(english == JsonSerializer.Serialize(a.ViewPersonal(1)), "Personal ordering changed with host culture.");
            KnowledgeFixture.Check(a.ViewPersonal(1, 1, 2).Rows.Count == 2 && a.ViewPersonal(1).Total == 5,
                "Personal query ignored its requested range.");
            KnowledgeFixture.Check(a.ViewPersonal(999).Total == 0 && a.ViewAccessible(2, 0, 0).Total == 0 &&
                a.FindKnowledge(1, KnowledgeFixture.Work, 3) is null, "Queries exposed hidden or unread knowledge.");
            var snapshot = a.Checkpoint();
            snapshot.Works[0].Chapters[0] = snapshot.Works[0].Chapters[0] with { StudyMonths = 999 };
            snapshot.Media[0] = snapshot.Media[0] with { MissingChapters = [3] };
            a.ViewPersonalHistory(1, KnowledgeFixture.Work, 1);
            KnowledgeFixture.Check(before == KnowledgeFixture.Json(a), "Query or returned snapshot mutated authority state.");
        }
        finally { CultureInfo.CurrentCulture = oldCulture; }
        KnowledgeFixture.RoundTrip(a);
    }
}

public sealed class KnowledgeRuleRevisionScenario : IFeatureScenario
{
    public string Name => "INV-004/024 knowledge: changed writing laws affect future actions and preserve adopted costs";
    public void Run()
    {
        var a = KnowledgeFixture.Fresh();
        var request = KnowledgeFixture.Copy() with { Chapters = [1], Payment = new("old-payment", 500, 201, 1, 202, 1) };
        var first = KnowledgeFixture.Ok(a.StartTranscription(request));
        KnowledgeFixture.Ok(a.ChangeRules(a.Rules with { Revision = 2, PaperPerChapter = 2, InkPerChapter = 3, TranscriptionMonthsPerChapter = 2 }, 0, "new-laws"));
        a = KnowledgeFixture.RoundTrip(a);
        KnowledgeFixture.Check(a.FindActivity(first)?.Rules.Revision == 1 && a.FindActivity(first)?.DueMonth == 1,
            "A law edit rewrote a pending action's media costs or duration.");
        KnowledgeFixture.Ok(a.Complete(new("first.done", first, 1, [KnowledgeFixture.One])));
        var cost = a.QuoteTranscription(KnowledgeMediumKind.Book, 1);
        KnowledgeFixture.Check(cost == new KnowledgeWritingCost(2, 3, 2), "New transcription ignored current law revision.");
        var next = request with { SourceId = "next", TargetItemId = 501, Month = 1, Payment = new("new-payment", 501, 201, 2, 202, 3) };
        var second = KnowledgeFixture.Ok(a.StartTranscription(next));
        KnowledgeFixture.Check(a.FindActivity(second)?.DueMonth == 3, "New action used old duration.");
        KnowledgeFixture.RoundTrip(a);
    }
}

public sealed class KnowledgeMixedEditionTeachingScenario : IFeatureScenario
{
    public string Name => "INV-004/024 knowledge: mixed-edition oral teaching preserves each source and surviving transmission rights";
    public void Run()
    {
        var a = KnowledgeFixture.Fresh();
        var first = KnowledgeFixture.Ok(a.CreateEdition(KnowledgeFixture.Work, 1, EditionRelation.Summary, 1,
            [new(1, "정제에 관한 주석")], 0, "first-summary"));
        var second = KnowledgeFixture.Ok(a.CreateEdition(KnowledgeFixture.Work, 1, EditionRelation.Summary, 1,
            [new(2, "보관에 관한 주석")], 0, "second-summary"));
        KnowledgeFixture.Ok(a.BindMedium(102, first, 1, 0, KnowledgeMediumKind.JadeSlip, 1, KnowledgeRights.Teach, [], false, 0, "first-slip"));
        KnowledgeFixture.Ok(a.BindMedium(103, second, 1, 0, KnowledgeMediumKind.Inscription, 2, KnowledgeRights.Teach, [], false, 0, "second-stone"));
        KnowledgeFixture.Ok(a.SeedInitialKnowledge(1, first, [1], 100, "initial-first"));
        KnowledgeFixture.Ok(a.SeedInitialKnowledge(1, second, [2], 100, "initial-second"));
        KnowledgeFixture.Ok(a.SynchronizeMedium(102, 1, 0, 1, [], true, true, 0, "lost-slip"));
        var lesson = KnowledgeFixture.Ok(a.StartTeaching(new("mixed-lesson", KnowledgeFixture.One, KnowledgeFixture.Two,
            KnowledgeFixture.Work, [1, 2], KnowledgeTransmission.Mentor, 0, 4)));
        a = KnowledgeFixture.RoundTrip(a);
        KnowledgeFixture.Ok(a.Complete(new("mixed-lesson.done", lesson, 4, [KnowledgeFixture.One, KnowledgeFixture.Two])));
        KnowledgeFixture.Check(a.FindKnowledge(2, KnowledgeFixture.Work, 1)?.EditionId == first &&
            a.FindKnowledge(2, KnowledgeFixture.Work, 2)?.EditionId == second && a.Checkpoint().Mastery.Count == 0,
            "Mixed teaching lost individual edition sources or awarded mastery.");
        KnowledgeFixture.RoundTrip(a);
    }
}

public sealed class KnowledgeCancelledTargetReuseScenario : IFeatureScenario
{
    public string Name => "INV-004/024 knowledge: an interrupted blank medium can be reused only with new paid inputs";
    public void Run()
    {
        var a = KnowledgeFixture.Fresh();
        var first = KnowledgeFixture.Ok(a.StartTranscription(KnowledgeFixture.Copy()));
        KnowledgeFixture.Ok(a.Cancel(first, 0, "cancel-before-writing"));
        var next = KnowledgeFixture.Ok(a.StartTranscription(KnowledgeFixture.Copy("new-copy", receipt: "new-payment")));
        a = KnowledgeFixture.RoundTrip(a);
        KnowledgeFixture.Ok(a.Complete(new("new-copy.done", next, 2, [KnowledgeFixture.One])));
        KnowledgeFixture.Check(a.Checkpoint().Media.Count(m => m.ItemId == 500) == 1 && a.Checkpoint().Evidence.Count(e => e.Kind == "writing-payment") == 2,
            "Reused physical target duplicated its output or reused spent writing inputs.");
        KnowledgeFixture.RoundTrip(a);
    }
}

public sealed class KnowledgeAccessScenario : IFeatureScenario
{
    public string Name => "INV-004/024 knowledge: read copy and transmission permissions expire independently and teachers cannot exceed their understanding";
    public void Run()
    {
        var a = KnowledgeFixture.Fresh();
        KnowledgeFixture.Ok(a.GrantAccess(101, 1, 2, KnowledgeRights.Read, 1, 0, "short-reading"));
        KnowledgeFixture.Rejected(a, KnowledgeStatus.Forbidden, () => a.StartTranscription(KnowledgeFixture.Copy() with { Scribe = KnowledgeFixture.Two }));
        var expired = KnowledgeFixture.Ok(a.StartStudy(new("short-study", KnowledgeFixture.Two, 101, [1], 0, 1)));
        KnowledgeFixture.Rejected(a, KnowledgeStatus.Unavailable, () => a.Complete(new("expired", expired, 1, [KnowledgeFixture.Two])));
        KnowledgeFixture.Ok(a.Cancel(expired, 1, "expired-cancel"));
        KnowledgeFixture.Ok(a.GrantAccess(101, 1, 2, KnowledgeRights.Read | KnowledgeRights.Copy, 4, 1, "long-reading"));
        var reading = KnowledgeFixture.Ok(a.StartStudy(new("pupil-study", KnowledgeFixture.Two, 101, [1], 1, 1)));
        KnowledgeFixture.Ok(a.Complete(new("pupil-study.done", reading, 2, [KnowledgeFixture.Two])));
        KnowledgeFixture.Rejected(a, KnowledgeStatus.Forbidden, () => a.StartTeaching(new("unlicensed-teaching", KnowledgeFixture.Two,
            KnowledgeFixture.One, KnowledgeFixture.Work, [1], KnowledgeTransmission.Peer, 2, 2)));
        KnowledgeFixture.Ok(a.GrantAccess(101, 1, 2, KnowledgeRights.Teach, 5, 2, "transmission-license"));
        var lesson = KnowledgeFixture.Ok(a.StartTeaching(new("licensed-teaching", KnowledgeFixture.Two, KnowledgeFixture.One,
            KnowledgeFixture.Work, [1], KnowledgeTransmission.Peer, 2, 2)));
        a = KnowledgeFixture.RoundTrip(a);
        KnowledgeFixture.Ok(a.Complete(new("licensed-teaching.done", lesson, 4, [KnowledgeFixture.One, KnowledgeFixture.Two])));
        KnowledgeFixture.Check(a.FindKnowledge(1, KnowledgeFixture.Work, 1)?.Comprehension == 50 && a.ViewPersonal(1).Rows.Single().Mastery == 0,
            "Oral teaching exceeded the source teacher's understanding or granted practical mastery.");
        KnowledgeFixture.RoundTrip(a);
    }
}
