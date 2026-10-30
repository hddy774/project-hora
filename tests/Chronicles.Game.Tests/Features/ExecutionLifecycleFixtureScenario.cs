using Chronicles.Game.Tests.Experiments;
using Chronicles.Game.Execution;

namespace Chronicles.Game.Tests.Features;

public sealed class ExecutionLifecycleFixtureScenario : IFeatureScenario
{
    public string Name => "INV-001/004/006/011/022 EX02 functional input: actual frozen conception, father death, reused slot and birth without measurement";
    public void Run()
    {
        var fixture = ExecutionMeasurements.LifecycleFixture(2);
        var compared = ExecutionComparisonHarness.Compare(fixture.Initial, fixture.Script, 12, [1, 8]);
        var father = fixture.Initial.People[0]; var final = compared.Final;
        var child = final.People.Single(p => p.ParentTwo == father.Id);
        var arrival = final.People.Single(p => p.Name == "EX02 새 육신");
        if (final.BirthCount != 1 || final.DeathCount != 1 || final.People.Count != 4 ||
            final.People.Single(p => p.Id == father.Id).Status != LifeStatus.Dead || arrival.Slot != father.Slot ||
            arrival.SlotGeneration != father.SlotGeneration + 1 || child.ParentOne != fixture.Initial.People[1].Id ||
            final.Events.Count(e => e.Kind == "사망" && e.Actor == father.Id) != 1 || final.Events.Count(e => e.Kind == "출생" && e.Actor == child.Id) != 1)
            throw new InvalidOperationException("Declared EX02 lifecycle must execute its genuine producers and stable ancestry exactly once.");
    }
}

public sealed class ExecutionBackendFixtureScenario : IFeatureScenario
{
    private sealed class PartialBackend : ITalentBatchBackend
    {
        public string Id => "functional-partial-actual-assess";
        public bool Calculate(TalentBatchInput input, Span<int> output)
        { output[0] = 10001; return false; }
    }
    public string Name => "INV-004/006/022 EX04 functional input: actual scalar vector partial backend selection preserves full aptitude and world hash";
    public void Run()
    {
        var world = GameWorld.Create(); var state = world.Checkpoint(); var p = state.People[0];
        var reference = GameWorld.Restore(state); reference.UseScalarExecutionReferenceForVerification();
        var expected = reference.PeopleAptitude(p); var hash = world.StateHash();
        foreach (var backend in new ITalentBatchBackend[] { new ScalarTalentBackend(), new VectorTalentBackend(), new PartialBackend() })
        {
            world.UseTalentBackendForVerification(backend);
            var actual = world.PeopleAptitude(p); var hit = world.PeopleAptitude(p);
            if (!actual.Talents.SequenceEqual(expected.Talents) || !actual.Cognition.SequenceEqual(expected.Cognition) ||
                !hit.Talents.SequenceEqual(actual.Talents) || !hit.Cognition.SequenceEqual(actual.Cognition) || world.StateHash() != hash)
                throw new InvalidOperationException("Actual backend selection must preserve canonical current aptitude, cache output and authority.");
        }
    }
}
