using System.Numerics;
using Chronicles.Game.Execution;
using Chronicles.Game.People;
using Chronicles.Game.Tests.Experiments;

namespace Chronicles.Game.Tests.Features;

internal static class ExecutionFixture
{
    internal static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    internal static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }
    internal static Person Person(long id, int slot) => new() { Id = id, BodyId = id + 100, SoulId = id + 200,
        Slot = slot, SlotGeneration = 1, Health = 75, Action = ActionKind.Wait, Due = 2 };
    internal static ActionSchedule Schedule(long id, long due) => new(new(id, id + 100, id + 200, (int)id, 1),
        ActionKind.Wait, 0, "", 1, due, true);
}

public sealed class ExecutionPopulationLeaseScenario : IFeatureScenario
{
    public string Name => "INV-001/004/006/021/022 H04 dynamic hot columns: original person order, slot generations and lease relocation lifetime";
    public void Run()
    {
        var people = new List<Person> { ExecutionFixture.Person(40, 0), ExecutionFixture.Person(2, 1), ExecutionFixture.Person(9, 2) };
        var slots = people.Select(p => new PopulationSlot { Person = p.Id }).ToList();
        var columns = new PopulationColumns(2); columns.Refresh(people, slots);
        var lease = columns.Borrow(); var oldRow = lease.At(0);
        ExecutionFixture.Check(Enumerable.Range(0, lease.Count).Select(i => lease.At(i).Identity.Person).SequenceEqual([40L, 2, 9]) &&
            columns.ChunkCount == 2, "Chunking must preserve current list order, not sort permanent IDs.");
        people[0].Status = LifeStatus.Dead; people[0].Place = 7;
        ExecutionFixture.Check(ReferenceEquals(columns.Original(oldRow), people[0]) && columns.Original(oldRow).Status == LifeStatus.Dead &&
            oldRow.Place == 0 && columns.Original(oldRow).Place == 7, "The original snapshot reference observes same-month death/movement.");
        ExecutionFixture.Throws<InvalidOperationException>(() => columns.Refresh(people, slots));
        lease.Dispose(); lease.Dispose();
        ExecutionFixture.Throws<ObjectDisposedException>(() => lease.At(0));
        ExecutionFixture.Throws<InvalidOperationException>(() => columns.Original(oldRow));
        using (var anotherLease = columns.Borrow())
        {
            ExecutionFixture.Check(anotherLease.Count == 3, "A new read lease remains valid before structural refresh.");
            ExecutionFixture.Throws<InvalidOperationException>(() => columns.Original(oldRow));
        }
        slots[0].Person = 77; slots[0].Generation = 2;
        var next = ExecutionFixture.Person(77, 0); next.SlotGeneration = 2; next.ParentOne = 40; people.Add(next);
        for (var n = 3; n < 5; n++) { var p = ExecutionFixture.Person(n + 500, n); people.Add(p); slots.Add(new() { Person = p.Id }); }
        columns.Refresh(people, slots);
        using (var expanded = columns.Borrow())
        {
            ExecutionFixture.Check(expanded.Count == 5 && columns.ChunkCount == 3 && expanded.At(2).Identity.Person == 77 &&
                expanded.At(2).Identity.Generation == 2 && next.ParentOne == 40, "Slot reuse never rewrites an old identity or family edge.");
            ExecutionFixture.Throws<InvalidOperationException>(() => columns.Original(oldRow));
        }
        foreach (var p in people.Where(p => p.Id != 77)) p.Status = LifeStatus.Dead;
        columns.Refresh(people, slots);
        using var reduced = columns.Borrow();
        ExecutionFixture.Check(reduced.Count == 1 && columns.ChunkCount == 1 && reduced.At(0).Identity.Person == 77,
            "Dead rows and unused chunks are released only after the old borrow ends.");
    }
}

public sealed class ExecutionEventQueueScenario : IFeatureScenario
{
    public string Name => "INV-001/004/006/022 H04 actual due queue contracts: same-month changes, cancellation, generation and stable rebuild";
    public void Run()
    {
        var queue = new StableEventQueue(); var one = ExecutionFixture.Schedule(1, 1); var two = ExecutionFixture.Schedule(2, 1);
        queue.Reconcile(one); queue.Reconcile(two); queue.Promote(1);
        ExecutionFixture.Check(queue.TakeDue(one, 1), "First current participant was not due.");
        var changed = two with { Due = 3, Data = "knowledge:changed", OwnerRevision = 2 };
        ExecutionFixture.Check(!queue.TakeDue(changed, 1) && !queue.TakeDue(changed, 2) && queue.TakeDue(changed, 3),
            "Earlier same-month work must invalidate a later participant's old ready token.");
        for (var n = 0; n < 70; n++) queue.Reconcile(two with { Due = 5 + n, OwnerRevision = n + 1 });
        var before = queue.Next(); queue.Rebuild();
        ExecutionFixture.Check(queue.Next() == before && queue.Discarded > 0 && queue.QueuedCount == 1, "Rebuild changed the stable next key.");
        var reused = two with { Identity = two.Identity with { Body = 999, Generation = 2 }, Due = 4 };
        queue.Reconcile(reused); queue.Promote(4); var ready = queue.ReadyOrder(); queue.Rebuild();
        ExecutionFixture.Check(queue.ReadyOrder().SequenceEqual(ready) && queue.TakeDue(reused, 4) && !queue.TakeDue(reused with { Alive = false }, 4),
            "Ready entries, slot/body generations and cancellation must survive compaction without old output.");
        queue.Reconcile(one); queue.Cancel(one.Identity.Person);
        ExecutionFixture.Check(!queue.TakeDue(one with { Kind = ActionKind.Idle }, 100) && queue.ActiveCount == 0,
            "Cancelled work was resurrected from an invalid heap entry.");
    }
}

public sealed class ExecutionTalentBatchScenario : IFeatureScenario
{
    private sealed class FaultBackend(bool complete, bool unsupported = false) : ITalentBatchBackend
    {
        public string Id => "test-only-partial-or-invalid";
        public bool Calculate(TalentBatchInput input, Span<int> output)
        {
            if (unsupported) return false;
            if (!output.IsEmpty) output[0] = 10001;
            return complete;
        }
    }
    public string Name => "INV-004/006/022 H09 real talent kernel: scalar/vector boundary, whole output, partial failure, stale and duplicate apply";
    public void Run()
    {
        var stamp = new TalentInputStamp(new(1, 2, 3, 0, 1), 4, 5, "actual-source-fixture");
        int[] foundations = [0, 10000, 3333, 5678, 100, 9999], roots = [12, 3456, 5000, 7890, 10000];
        foreach (var size in new[] { 0, 1, Vector<int>.Count - 1, Vector<int>.Count, Vector<int>.Count + 1, 24 }.Distinct())
        {
            var input = new TalentBatchInput(stamp, foundations, roots, Enumerable.Range(0, size).Select(i => i % 24).ToArray());
            foreach (var backend in new ITalentBatchBackend[] { new ScalarTalentBackend(), new VectorTalentBackend(),
                new FaultBackend(false), new FaultBackend(true), new FaultBackend(false, true) })
            {
                var pending = InnateTalentBatch.Calculate(input, backend);
                ExecutionFixture.Check(InnateTalentBatch.Commit(pending, stamp, out var result) == TalentBatchStatus.Applied &&
                    result.SequenceEqual(Enumerable.Range(0, size).Select(input.CanonicalAt)), "Batch must publish the complete canonical integer result.");
                ExecutionFixture.Check(InnateTalentBatch.Commit(pending, stamp, out var duplicate) == TalentBatchStatus.AlreadyApplied &&
                    duplicate.Length == 0, "An applied result was published twice.");
            }
        }
        var captured = new TalentBatchInput(stamp, foundations, roots);
        var expected = captured.CanonicalAt(0); foundations[0] = 9999;
        ExecutionFixture.Check(captured.CanonicalAt(0) == expected, "Asynchronous input must own immutable values, not the caller's array.");
        foreach (var stale in new[] { stamp with { WorldRevision = 6 }, stamp with { LawRevision = 7 },
            stamp with { Identity = stamp.Identity with { Body = 20 } }, stamp with { Identity = stamp.Identity with { Generation = 2 } },
            stamp with { Life = LifeStatus.Dead }, stamp with { CurrentSlotPerson = 9, CurrentSlotGeneration = 2 },
            stamp with { SourceFingerprint = "changed-genetics-or-source" } })
        {
            var pending = InnateTalentBatch.Calculate(captured, new FaultBackend(false));
            ExecutionFixture.Check(InnateTalentBatch.Commit(pending, stale, out var discarded) == TalentBatchStatus.Stale && discarded.Length == 0,
                "A failed candidate cannot use fallback or publish after input identity/revision/source changed.");
        }
        var innate = new InnateProfile((int[])foundations.Clone(), (int[])roots.Clone(),
            [new("execution.source", PeopleSourceKind.Development, "explicit functional starting profile", 0)]);
        var complete = Enumerable.Range(0, 24).Select(i => Aptitudes.InnateTalent(innate.Foundations, innate.ElementRoots, i)).ToArray();
        var publication = new InnateTalentSnapshot(innate, complete);
        complete[0] = -1;
        ExecutionFixture.Check(publication.Read(innate)[0] >= 0, "Published values must own their complete result.");
        ExecutionFixture.Throws<InvalidDataException>(() => { _ = new InnateTalentSnapshot(innate, new int[23]); });
        ExecutionFixture.Throws<InvalidDataException>(() => publication.Read(innate with
            { Sources = [innate.Sources[0] with { Revision = 2 }] }));
        ExecutionFixture.Throws<InvalidDataException>(() => publication.Read(innate with { Foundations = new int[6] }));
        var development = new TalentDevelopment(Enumerable.Repeat(4200, 24).ToArray(), Enumerable.Repeat(2700, 24).ToArray());
        var context = new AptitudeContext(innate, development, 7300, 4100, [], new());
        var canonical = Aptitudes.Assess(context); var batched = Aptitudes.Assess(context, publication);
        ExecutionFixture.Check(canonical.Talents.SequenceEqual(batched.Talents) && canonical.Cognition.SequenceEqual(batched.Cognition),
            "Cached innate values cannot cache learned skill, understanding, health, rounding or current expression.");
        ExecutionFixture.Check(ExecutionExperimentPlan.Definitions.Select(e => e.Id).SequenceEqual(
            ["EX-01", "EX-02", "EX-03", "EX-04", "EX-05", "EX-06"]), "All canon experiments need distinct evidence scopes.");
        ExecutionFixture.Throws<InvalidDataException>(() => ExecutionExperimentPlan.ValidateMeasuredEvidence(
            new("", "fixture", "command", "kernel", 24, "scalar", "runtime", "device", "before", "before", [], "adopt", "functional only")));
    }
}

public sealed class ExecutionWorldReferenceScenario : IFeatureScenario
{
    public string Name => "INV-001/004/006/021/022 H04 world reference: actual commands, birth, cancellation, martial and save boundaries match every month";
    public void Run()
    {
        var world = GameWorld.Create(42, new Dictionary<string, long> { ["economy.travelDuration"] = 3,
            ["ecology.disasterChance"] = 0, ["hazard.sealChance"] = 0, ["hazard.portalChance"] = 0 });
        var initial = world.Checkpoint(); var one = initial.People[0].Id; var two = initial.People[1].Id;
        var result = ExecutionComparisonHarness.Compare(initial,
        [new(0, new("execution.pregnancy", two, "conceive", one)), new(0, new("execution.control", one, "possess")),
            new(0, new("execution.explore", one, "explore")), new(1, new("execution.travel", one, "travel", 1)),
            new(2, new("execution.cancel", one, "cancel")), new(2, new("execution.martial", one, "martial")),
            new(3, new("execution.wait", one, "wait")), new(4, new("execution.martial2", one, "martial"))],
            10, [2, 8]);
        ExecutionFixture.Check(result.Final.BirthCount == 1 && result.Final.People.Count == 3 && result.IndexedFrames == 10 &&
            result.IndexedDueChecks > 0 && result.IndexedTalentCommits > 0 && result.Final.Cultivation[initial.People[0].BodyId].Martial.Work > 0,
            $"Actual bridge use: births={result.Final.BirthCount}, people={result.Final.People.Count}, frames={result.IndexedFrames}, due={result.IndexedDueChecks}, talents={result.IndexedTalentCommits}, martial={result.Final.Cultivation[initial.People[0].BodyId].Martial.Work}.");
    }
}

public sealed class ExecutionSharedWorkReferenceScenario : IFeatureScenario
{
    public string Name => "INV-004/006/017/022 H04 shared work: earlier teacher clears later pupil's due token in the actual month";
    public void Run()
    {
        var world = GameWorld.Create(42, new Dictionary<string, long> { ["birth.enabled"] = 0,
            ["ecology.foodUse"] = 0, ["knowledge.misreadChance"] = 0 });
        ExecutionFixture.Check(world.Execute(new("execution.arrival", 0, "create", Data: "실행 경계 학습자")).Accepted, "Actual explicit arrival failed.");
        var state = world.Checkpoint(); var teacher = state.People[0].Id; var pupil = state.People[^1].Id;
        foreach (var p in state.People) { p.Action = ActionKind.Wait; p.Due = 100; }
        state.People[0].Action = ActionKind.Idle; state.People[0].Due = 0;
        world = GameWorld.Restore(state);
        ExecutionFixture.Check(world.Execute(new("execution.teacher.control", teacher, "possess")).Accepted, "Teacher control failed.");
        world.AdvanceTo(1);
        var result = ExecutionComparisonHarness.Compare(world.Checkpoint(),
            [new(1, new("execution.teach", teacher, "teach", pupil, 2, "entry"))], 4, [2]);
        var actual = result.Final.People.Single(p => p.Id == pupil);
        ExecutionFixture.Check(actual.Understanding.GetValueOrDefault("entry") == 200 && result.IndexedDueChecks > 0 &&
            result.Final.Knowledge.Activities.Any(a => a.Kind == Knowledge.KnowledgeActivityKind.Teaching &&
                a.Status == Knowledge.KnowledgeActivityStatus.Completed),
            "A shared real lesson must complete once when the earlier teacher changes the later participant's action.");
    }
}

public sealed class ExecutionSlotReuseReferenceScenario : IFeatureScenario
{
    public string Name => "INV-001/004/006/021/022 H04 saved life boundary: real lifespan death and explicit new arrival reuse no old token";
    public void Run()
    {
        var world = GameWorld.Create(42, new Dictionary<string, long> { ["birth.enabled"] = 0, ["ecology.foodUse"] = 0 });
        var state = world.Checkpoint(); var old = state.People[1];
        old.Lifespan = state.Month - old.Born + 1;
        foreach (var p in state.People) { p.Action = ActionKind.Wait; p.Due = 20; }
        var result = ExecutionComparisonHarness.Compare(state,
            [new(1, new("execution.reused-slot.arrival", 0, "create", Data: "새 슬롯 인물"))], 2, [1]);
        var actualOld = result.Final.People.Single(p => p.Id == old.Id); var arrival = result.Final.People[^1];
        var restored = GameWorld.Restore(result.Final);
        ExecutionFixture.Check(result.Final.DeathCount == 1 && actualOld.Status == LifeStatus.Dead &&
            result.Final.Events.Any(e => e.Kind == "사망" && e.Actor == old.Id) && arrival.Id != old.Id && arrival.Slot == old.Slot &&
            arrival.SlotGeneration == old.SlotGeneration + 1 && restored.ResolveSlot(old.Slot, old.SlotGeneration) == 0 &&
            restored.ResolveSlot(arrival.Slot, arrival.SlotGeneration) == arrival.Id && result.IndexedFrames == 2,
            "Actual death/cancellation/source records must retain the old identity across a saved recycled slot.");
    }
}
