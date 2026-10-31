using Chronicles.Game.Knowledge;

namespace Chronicles.Game.Tests.Features;

public sealed class KnowledgeGenesisTransmissionContractScenario : IFeatureScenario
{
    public string Name => "INV-024 knowledge bridge contract: explicit genesis transmission licence and existing comprehension baseline";
    public void Run()
    {
        var ordinary = KnowledgeFixture.Fresh();
        KnowledgeFixture.Ok(ordinary.SeedInitialKnowledge(2, 1, [1], 100, "ordinary-initial"));
        KnowledgeFixture.Rejected(ordinary, KnowledgeStatus.Forbidden, () => ordinary.StartTeaching(new("unlicensed",
            KnowledgeFixture.Two, KnowledgeFixture.One, KnowledgeFixture.Work, [1], KnowledgeTransmission.Parent, 0, 2)));
        var licensed = KnowledgeFixture.Fresh();
        KnowledgeFixture.Ok(licensed.SeedInitialKnowledge(2, 1, [1], 100, "explicit-initial", canTransmit: true));
        var lesson = KnowledgeFixture.Ok(licensed.StartTeaching(new("licensed", KnowledgeFixture.Two, KnowledgeFixture.One,
            KnowledgeFixture.Work, [1], KnowledgeTransmission.Parent, 0, 2)));
        licensed = KnowledgeFixture.RoundTrip(licensed);
        KnowledgeFixture.Ok(licensed.Complete(new("licensed.done", lesson, 2, [KnowledgeFixture.Two, KnowledgeFixture.One])));
        KnowledgeFixture.Check(licensed.ViewPersonal(1).Rows.Single().Mastery == 0, "Genesis licence granted practical skill.");

        var baseline = new KnowledgeAuthority();
        KnowledgeFixture.Ok(baseline.DefineWork(new("entry", "입문", "entry", KnowledgeKind.Cultivation,
            [new(1, "원리", 100, 100)], 0, "entry-definition")));
        var edition = KnowledgeFixture.Ok(baseline.CreateEdition("entry", 0, EditionRelation.Original, 1, [new(1, "입문 순서")], 0, "entry-edition"));
        KnowledgeFixture.Ok(baseline.BindMedium(10, edition, 1, 0, KnowledgeMediumKind.Book, 0, KnowledgeRights.Read, [], false, 0, "entry-medium"));
        KnowledgeFixture.Ok(baseline.SeedInitialKnowledge(1, edition, [1], 30, "entry-genesis"));
        var study = KnowledgeFixture.Ok(baseline.StartStudy(new("entry-study", KnowledgeFixture.One, 10, [1], 0, 1)));
        KnowledgeFixture.Ok(baseline.Complete(new("entry-study.done", study, 1, [KnowledgeFixture.One])));
        KnowledgeFixture.Check(baseline.FindKnowledge(1, "entry", 1)?.Comprehension == 31 && baseline.Checkpoint().Mastery.Count == 0,
            "Actual new study failed to advance above an explicit initial comprehension baseline.");
        KnowledgeFixture.RoundTrip(baseline);
    }
}

public sealed class KnowledgeSubstrateAndRevocationContractScenario : IFeatureScenario
{
    public string Name => "INV-004/024 knowledge bridge contract: a real reserved paper substrate may be the paid blank and reading rights revoke";
    public void Run()
    {
        var a = KnowledgeFixture.Fresh();
        var request = KnowledgeFixture.Copy() with { Chapters = [1], Payment = new("physical-substrate", 500, 500, 1, 202, 1) };
        var copy = KnowledgeFixture.Ok(a.StartTranscription(request));
        a = KnowledgeFixture.RoundTrip(a);
        KnowledgeFixture.Ok(a.Complete(new("physical-copy.done", copy, 1, [KnowledgeFixture.One])));
        KnowledgeFixture.Check(a.Checkpoint().Media.Any(m => m.ItemId == 500), "A reserved paper substrate could not become the bound output.");
        KnowledgeFixture.Ok(a.GrantAccess(101, 1, 2, KnowledgeRights.Read, 10, 1, "reading-right"));
        KnowledgeFixture.Check(a.ViewAccessible(2, 0, 1).Total == 1, "Explicit reading grant was ignored.");
        var study = KnowledgeFixture.Ok(a.StartStudy(new("borrowed-study", KnowledgeFixture.Two, 101, [1], 1, 1)));
        KnowledgeFixture.Ok(a.RevokeAccess(101, 1, 2, 1, "reading-revoked"));
        a = KnowledgeFixture.RoundTrip(a);
        KnowledgeFixture.Check(a.ViewAccessible(2, 0, 1).Total == 0, "A revoked right remained visible.");
        KnowledgeFixture.Rejected(a, KnowledgeStatus.Unavailable, () => a.Complete(new("revoked-completion", study, 2, [KnowledgeFixture.Two])));
        KnowledgeFixture.Check(a.ViewPersonal(2).Total == 0, "Revocation completed an inaccessible learning action.");
        KnowledgeFixture.RoundTrip(a);
    }
}

internal static class KnowledgeWorldFixture
{
    internal static GameWorld Fresh()
    {
        var world = GameWorld.Create(42, new Dictionary<string, long>(StringComparer.Ordinal)
        {
            ["birth.enabled"] = 0, ["ecology.foodUse"] = 0, ["ecology.disasterChance"] = 0,
            ["knowledge.misreadChance"] = 0
        });
        var state = world.Checkpoint();
        foreach (var person in state.People) { person.Action = ActionKind.Guard; person.Due = 100; }
        state.People[0].Action = ActionKind.Idle; state.People[0].Due = 0;
        return GameWorld.Restore(state);
    }
    internal static void Accept(GameWorld world, string id, string kind, long actor = 0, long target = 0, long value = 0, string data = "")
    {
        var result = world.Execute(new(id, actor, kind, target, value, data));
        KnowledgeFixture.Check(result.Accepted, result.Message);
    }
    internal static void AddStock(GameState state, long owner, string definition, int quantity)
    {
        state.Items.Add(new() { Id = state.NextId++, Owner = owner, Definition = definition, Quantity = quantity,
            Quality = 5000, Place = -1, Source = "기능 검사에 명시한 실제 원재료" });
        if (!state.Materials.TryGetValue(definition, out var flow)) state.Materials.Add(definition, flow = new());
        flow.Opening += quantity;
    }
    internal static (GameWorld World, long Pupil, long Book) ReaderFixture()
    {
        var world = Fresh();
        Accept(world, "actual-arrival", "create", data: "학습자");
        var state = world.Checkpoint();
        var pupil = state.People[^1];
        pupil.Action = ActionKind.Idle; pupil.Due = 0;
        var teacher = state.People[0];
        if (!teacher.KnownPeople.Contains(pupil.Id)) teacher.KnownPeople.Add(pupil.Id);
        if (!pupil.KnownPeople.Contains(teacher.Id)) pupil.KnownPeople.Add(teacher.Id);
        var book = state.Items.First(i => i.Owner == teacher.Id && i.Definition == "path.1.0");
        book.Owner = pupil.Id;
        var metadata = state.Inventory.Metadata.Find(m => m.Item == book.Id);
        if (metadata is not null) metadata.Custodian = pupil.Id;
        var knowledge = KnowledgeAuthority.Restore(state.Knowledge);
        KnowledgeFixture.Ok(knowledge.SynchronizeMedium(book.Id, pupil.Id, pupil.Place, 3, [], false, false, state.Month, "fixture:actual-book-transfer"));
        state.Knowledge = knowledge.Checkpoint();
        foreach (var person in state.People.Where(p => p.Id != pupil.Id)) { person.Action = ActionKind.Guard; person.Due = 100; }
        AddStock(state, pupil.Id, "paper", 4); AddStock(state, pupil.Id, "ink", 4);
        state.Controlled = pupil.Id;
        return (GameWorld.Restore(state), pupil.Id, book.Id);
    }
}

public sealed class KnowledgeWorldGenesisScenario : IFeatureScenario
{
    public string Name => "INV-004/024 knowledge public world: real genesis books and explicit personal sources with zero mastery";
    public void Run()
    {
        var world = KnowledgeWorldFixture.Fresh();
        var state = world.Checkpoint();
        KnowledgeFixture.Check(state.Knowledge.Works.Count > 0 && state.People.Count == 2 &&
            state.Knowledge.Media.Count == state.Items.Count(i => i.Definition.StartsWith("path.", StringComparison.Ordinal)),
            "World creation did not bind its real initial books.");
        KnowledgeFixture.Check(state.Knowledge.Media.All(m => state.Items.Any(i => i.Id == m.ItemId && i.Owner == m.OwnerId && i.Quantity == 1)) &&
            state.People.All(p => p.Skills.Count == 0) && state.Knowledge.Mastery.Count == 0,
            "Genesis generated a free medium or practical mastery.");
        KnowledgeFixture.Check(state.Knowledge.Memories.All(m => m.SourceId.StartsWith("genesis:person:", StringComparison.Ordinal)) &&
            state.People.All(p => p.Understanding["entry"] == 10000 && p.Understanding["path.1.0"] == 3000),
            "Initial personal comprehension lacked explicit genesis evidence.");
        var before = world.StateHash();
        world.View(); world.View(state.People[0].Id);
        KnowledgeFixture.Check(before == world.StateHash(), "Opening world views mutated knowledge or RNG.");
        KnowledgeFixture.Check(GameCodec.Decode(GameCodec.Encode(world)).StateHash() == before, "Real world knowledge save roundtrip failed.");
    }
}

public sealed class KnowledgeWorldStudyScenario : IFeatureScenario
{
    public string Name => "INV-004/005/024 knowledge public world: study resumes against its actual body book and clock without skill gain";
    public void Run()
    {
        var world = KnowledgeWorldFixture.Fresh();
        var state = world.Checkpoint(); var actor = state.People[0].Id;
        var book = state.Items.First(i => i.Owner == actor && i.Definition == "path.1.0");
        KnowledgeWorldFixture.Accept(world, "control-reader", "possess", actor);
        KnowledgeWorldFixture.Accept(world, "real-study", "Study", actor, book.Id);
        var pending = world.Checkpoint();
        KnowledgeFixture.Check(pending.People[0].Action == ActionKind.Study && pending.People[0].Due == 1 &&
            pending.Knowledge.Activities.Single(a => a.Status == KnowledgeActivityStatus.Active).MediumItemId == book.Id,
            "Study bypassed actual world action/body/media state.");
        var beforeReplay = world.StateHash();
        KnowledgeWorldFixture.Accept(world, "real-study", "Study", actor, book.Id);
        KnowledgeFixture.Check(beforeReplay == world.StateHash(), "Replayed world study duplicated an activity.");
        world = GameCodec.Decode(GameCodec.Encode(world)); world.AdvanceTo(1);
        var completed = world.Checkpoint();
        KnowledgeFixture.Check(completed.People[0].Understanding[book.Definition] == 3100 && completed.People[0].Skills.Count == 0 &&
            completed.Knowledge.Mastery.Count == 0 && completed.Knowledge.Activities.Single().Status == KnowledgeActivityStatus.Completed,
            "Elapsed physical study did not update only personal comprehension.");
        var learning = completed.PeopleDomain.Profiles.Single(p => p.PersonId == actor).Learning;
        KnowledgeFixture.Check(learning.Length == 1 && learning[0].UnderstandingChange == 100 && learning[0].LearnedChange == 0 &&
            learning[0].CompletedMonths == 1, "One real study completion must produce exactly one sourced personal expression change.");
    }
}

public sealed class KnowledgeWorldTranscriptionScenario : IFeatureScenario
{
    public string Name => "INV-004/005/017/018/024 knowledge public world: a paid reserved paper item becomes exactly one copied book";
    public void Run()
    {
        var world = KnowledgeWorldFixture.Fresh();
        var state = world.Checkpoint(); var actor = state.People[0].Id;
        var source = state.Items.First(i => i.Owner == actor && i.Definition == "path.1.0");
        KnowledgeWorldFixture.AddStock(state, actor, "paper", 2); KnowledgeWorldFixture.AddStock(state, actor, "ink", 2);
        world = GameWorld.Restore(state);
        KnowledgeWorldFixture.Accept(world, "control-scribe", "possess", actor);
        KnowledgeWorldFixture.Accept(world, "real-copy", "transcribe", actor, source.Id);
        var pending = world.Checkpoint(); var activity = pending.Knowledge.Activities.Single();
        var blank = pending.Items.Single(i => i.Id == activity.TargetItemId);
        KnowledgeFixture.Check(blank.Definition == "paper" && blank.Quantity == 1 && blank.Reserved > 0 &&
            pending.Inventory.Reservations.Any(r => r.Id == blank.Reserved && r.Active && r.Items.Contains(blank.Id)) &&
            pending.Knowledge.Editions.Count == state.Knowledge.Editions.Count,
            "Transcription created a premature book or an unbacked physical reservation.");
        var replayHash = world.StateHash();
        KnowledgeWorldFixture.Accept(world, "real-copy", "transcribe", actor, source.Id);
        KnowledgeFixture.Check(replayHash == world.StateHash(), "Replayed transcription spent inputs twice.");
        world = GameCodec.Decode(GameCodec.Encode(world)); world.AdvanceTo(1);
        var completed = world.Checkpoint(); var output = completed.Items.Single(i => i.Id == blank.Id);
        KnowledgeFixture.Check(output.Definition == source.Definition && output.Reserved == 0 &&
            completed.Materials["paper"].Consumption == 1 && completed.Materials["ink"].Consumption == 1 &&
            completed.Materials[source.Definition].Production == 1,
            "Copying did not conserve the real paper/ink-to-book transformation.");
        var copyEdition = completed.Knowledge.Editions.Single(e => e.Id == completed.Knowledge.Media.Single(m => m.ItemId == output.Id).EditionId);
        KnowledgeFixture.Check(copyEdition.Relation == EditionRelation.Copy && copyEdition.Chapters.Length == 1 &&
            copyEdition.ParentEditionId == state.Knowledge.Media.Single(m => m.ItemId == source.Id).EditionId && completed.People[0].Skills.Count == 0,
            "Copy output lost its source, awarded unread sections, or granted mastery.");
        Invariants.Validate(completed);
    }
}

public sealed class KnowledgeWorldTeachingScenario : IFeatureScenario
{
    public string Name => "INV-004/017/024 knowledge public world: real request and teaching share both participants' labor and cancel together";
    public void Run()
    {
        var (world, pupil, _) = KnowledgeWorldFixture.ReaderFixture();
        KnowledgeWorldFixture.Accept(world, "observe-for-request", "observe");
        var state = world.Checkpoint(); var teacher = state.People[0].Id;
        state.People[0].Action = ActionKind.Idle; state.People[0].Due = 0;
        world = GameWorld.Restore(state);
        KnowledgeWorldFixture.Accept(world, "real-request", "requestTeaching", pupil, teacher, data: "entry");
        KnowledgeFixture.Check(!world.Checkpoint().Knowledge.Memories.Any(m => m.PersonId == pupil), "A teaching request granted knowledge.");
        KnowledgeWorldFixture.Accept(world, "real-lesson", "teach", teacher, pupil, 2, "entry");
        var pending = world.Checkpoint();
        KnowledgeFixture.Check(pending.People.Single(p => p.Id == teacher).Action == ActionKind.Teach &&
            pending.People.Single(p => p.Id == pupil).Action == ActionKind.Wait &&
            pending.People.Single(p => p.Id == teacher).Due == pending.People.Single(p => p.Id == pupil).Due,
            "Teaching did not reserve both actual participants.");
        var before = world.StateHash();
        KnowledgeFixture.Check(!world.Execute(new("double-labor", teacher, "gather", Data: "food")).Accepted && world.StateHash() == before,
            "Teacher performed other work during the lesson.");
        KnowledgeWorldFixture.Accept(world, "cancel-pupil", "cancel", pupil);
        KnowledgeFixture.Check(world.Checkpoint().People.Where(p => p.Id == teacher || p.Id == pupil).All(p => p.Action == ActionKind.Idle) &&
            !world.Checkpoint().Knowledge.Memories.Any(m => m.PersonId == pupil), "Lesson cancellation left one side busy or granted partial free learning.");
        KnowledgeWorldFixture.Accept(world, "resume-lesson", "teach", teacher, pupil, 2, "entry");
        KnowledgeWorldFixture.Accept(world, "control-teacher", "possess", teacher);
        world = GameCodec.Decode(GameCodec.Encode(world)); world.AdvanceTo(2);
        var complete = world.Checkpoint(); var learned = complete.People.Single(p => p.Id == pupil);
        KnowledgeFixture.Check(learned.Understanding["entry"] == 200 && learned.Skills.Count == 0 &&
            complete.Knowledge.Activities.Any(a => a.Kind == KnowledgeActivityKind.Teaching && a.Status == KnowledgeActivityStatus.Completed),
            "Saved actual teaching did not yield only the pupil's sourced comprehension.");
    }
}

public sealed class KnowledgeWorldCorrectionScenario : IFeatureScenario
{
    public string Name => "INV-004/018/024 knowledge public world: actual misreading paid experiment and rereading preserve adopted history";
    public void Run()
    {
        var (world, actor, book) = KnowledgeWorldFixture.ReaderFixture();
        KnowledgeWorldFixture.Accept(world, "allow-law-edit", "observe");
        KnowledgeWorldFixture.Accept(world, "force-misread-law", "law", value: 10000, data: "knowledge.misreadChance");
        KnowledgeWorldFixture.Accept(world, "control-new-reader", "possess", actor);
        KnowledgeWorldFixture.Accept(world, "first-reading", "study", actor, book);
        world.AdvanceTo(1);
        var state = world.Checkpoint();
        var misread = state.Knowledge.Memories.Last(m => m.PersonId == actor && m.WorkId == "path.1.0");
        var physicalClaim = state.Knowledge.Editions.Single(e => e.Id == misread.EditionId).Chapters.Single(c => c.Number == 1).Claim;
        KnowledgeFixture.Check(misread.Claim != physicalClaim && misread.Belief == KnowledgeBelief.Unverified,
            "First reading used hidden truth or labelled an untested belief as false.");
        KnowledgeWorldFixture.Accept(world, "test-wrong-claim", "experiment", actor, book);
        world = GameCodec.Decode(GameCodec.Encode(world)); world.AdvanceTo(2);
        state = world.Checkpoint();
        var refuted = state.Knowledge.Memories.Last(m => m.PersonId == actor && m.WorkId == "path.1.0");
        KnowledgeFixture.Check(refuted.Belief == KnowledgeBelief.Contradicted && refuted.Claim == misread.Claim &&
            state.People.Single(p => p.Id == actor).Skills.Count == 0 && state.Materials["paper"].Consumption == 1,
            "Failed real experiment supplied an unlearned answer or free practical skill.");
        KnowledgeWorldFixture.Accept(world, "read-real-correction", "correctKnowledge", actor, book);
        world.AdvanceTo(3);
        state = world.Checkpoint();
        var corrected = state.Knowledge.Memories.Last(m => m.PersonId == actor && m.WorkId == "path.1.0");
        KnowledgeFixture.Check(corrected.Claim == physicalClaim && corrected.Belief == KnowledgeBelief.Corrected &&
            state.Knowledge.Memories.Any(m => m.PersonId == actor && m.Revision == misread.Revision && m.Claim == misread.Claim) &&
            !state.Knowledge.Memories.Any(m => m.PersonId == actor && m.Chapter != 1), "Rereading rewrote frozen history or granted unread sections.");
        KnowledgeWorldFixture.Accept(world, "test-corrected-claim", "experiment", actor, book);
        world.AdvanceTo(4);
        var completed = world.Checkpoint();
        KnowledgeFixture.Check(completed.People.Single(p => p.Id == actor).Skills["path.1.0"] == 100 &&
            completed.Materials["paper"].Consumption == 2 && completed.Materials["ink"].Consumption == 2 &&
            completed.Knowledge.Activities.Any(a => a.PracticeOutcome is { } outcome && outcome.AdoptedRevision == misread.Revision && !outcome.Successful),
            "Only the real corrected successful process should earn mastery; old failed adoption must remain unchanged.");
        Invariants.Validate(completed);
    }
}

public sealed class KnowledgeWorldBlankBookScenario : IFeatureScenario
{
    public string Name => "INV-017/018/024 knowledge public world: an actual blank book carries no text until paid transcription";
    public void Run()
    {
        var world = KnowledgeWorldFixture.Fresh();
        var state = world.Checkpoint(); var actor = state.People[0].Id;
        var source = state.Items.First(i => i.Owner == actor && i.Definition == "path.1.0");
        KnowledgeWorldFixture.AddStock(state, actor, "paper", 1); KnowledgeWorldFixture.AddStock(state, actor, "ink", 1);
        KnowledgeWorldFixture.AddStock(state, actor, "blankBook", 1);
        var blank = state.Items.Single(i => i.Definition == "blankBook");
        world = GameWorld.Restore(state);
        KnowledgeWorldFixture.Accept(world, "control-blank-scribe", "possess", actor);
        var before = world.StateHash();
        KnowledgeFixture.Check(!world.Execute(new("read-empty", actor, "study", blank.Id)).Accepted && before == world.StateHash(),
            "Manufacturing an empty book fabricated a readable canonical edition.");
        KnowledgeWorldFixture.Accept(world, "write-real-blank", "transcribe", actor, source.Id,
            data: blank.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var pending = world.Checkpoint();
        KnowledgeFixture.Check(pending.Items.Single(i => i.Id == blank.Id).Definition == "blankBook" &&
            !pending.Knowledge.Media.Any(m => m.ItemId == blank.Id), "Pending blank already advertised copied text.");
        world = GameCodec.Decode(GameCodec.Encode(world)); world.AdvanceTo(1);
        var completed = world.Checkpoint();
        KnowledgeFixture.Check(completed.Items.Single(i => i.Id == blank.Id).Definition == source.Definition &&
            completed.Materials["blankBook"].Consumption == 1 && completed.Materials["paper"].Consumption == 1 &&
            completed.Materials["ink"].Consumption == 1 && completed.Knowledge.Media.Any(m => m.ItemId == blank.Id),
            "The actual blank substrate, paper and ink did not become one sourced physical book.");
        Invariants.Validate(completed);
    }
}
