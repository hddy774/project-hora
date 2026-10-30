using Chronicles.Game.Tests.Experiments;

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
