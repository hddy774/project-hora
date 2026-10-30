using Chronicles.Game.Cultivation;

namespace Chronicles.Game.Tests.Features;

public sealed class CultivationMartialTenStagesWorldScenario : IFeatureScenario
{
    public string Name => "INV-004/006/011/015 W01 public martial: ten real stages, root-free labor, saved continuation, combat and 600-year boundary";
    public void Run()
    {
        var world = GameWorld.Create(42, new Dictionary<string, long> { ["birth.enabled"] = 0, ["ecology.foodUse"] = 0,
            ["ecology.regrowth"] = 0, ["ecology.disasterChance"] = 0, ["cultivation.breakthroughRisk"] = 0, ["martial.period"] = 1 });
        var state = world.Checkpoint(); var person = state.People[0]; var id = person.Id;
        WorldScenarioFixtures.SetCultivation(state, person, Realm.Mortal);
        person.Roots = new int[5]; person.Born = -state.Laws["birth.maturity"]; person.Lifespan = state.Laws["life.mortal"];
        state.PeopleDomain = state.PeopleDomain with
        {
            Profiles = state.PeopleDomain.Profiles.Select(p => p.PersonId == id ? p with
                { Innate = p.Innate with { ElementRoots = new int[5] } } : p).ToArray(),
            Genomes = state.PeopleDomain.Genomes.Select(g => g.PersonId == id ? g with
                { Genes = g.Genes.Select(pair => pair.GeneId.StartsWith("element.", StringComparison.Ordinal)
                    ? pair with { FirstAllele = 0, SecondAllele = 0 } : pair).ToArray() } : g).ToArray()
        };
        state.People[1].Action = ActionKind.Wait; state.People[1].Due = 101;
        world = GameWorld.Restore(state);
        CultivationAdapterFixture.Accepted(world, id, "possess");
        var initialPower = world.View(id).Detail!.CombatProjection!.Axes.OutputPenetration;
        var initial = world.Checkpoint(); var qi = initial.Places[person.Place].Qi;
        var money = initial.CreatedMoney; var stages = new List<int>(); var saved = false;
        for (var month = 1; month <= 100 && CultivationAdapterFixture.Person(world, id).Martial < 10; month++)
        {
            var before = world.Checkpoint(); var actor = before.People.Single(p => p.Id == id);
            var work = before.Cultivation[actor.BodyId].Martial.Work;
            CultivationAdapterFixture.Accepted(world, id, "martial");
            var scheduled = world.Checkpoint();
            CultivationFixture.Check(scheduled.People.Single(p => p.Id == id).Due == month &&
                scheduled.Cultivation[actor.BodyId].Martial.Work == work, "selection reserves one actual month without banking idle work");
            var pendingHash = world.StateHash();
            CultivationFixture.Check(!world.Execute(CultivationAdapterFixture.Command(id, "martial")).Accepted && world.StateHash() == pendingHash,
                "the occupied real actor cannot train twice in the same month");
            var resumed = actor.Martial >= 4 && !saved ? GameCodec.Decode(GameCodec.Encode(world)) : null;
            world.AdvanceTo(month);
            if (resumed is not null)
            { resumed.AdvanceTo(month); CultivationFixture.Check(world.StateHash() == resumed.StateHash(), "pending mid-route martial work resumes on the same real clock"); saved = true; }
            state = world.Checkpoint(); person = state.People.Single(p => p.Id == id);
            if (person.Martial != actor.Martial)
            {
                stages.Add(person.Martial); Invariants.Validate(state);
                CultivationFixture.Check(person.Martial == actor.Martial + 1 && person.Lifespan <= 600 * 12,
                    "each paid stage is singular and does not exceed the independent life cap");
            }
            CultivationFixture.Check(person.Status == LifeStatus.Alive && person.Realm == Realm.Mortal && person.Roots.All(v => v == 0) &&
                state.Cultivation[person.BodyId].RootGrowth.Total == 0 && state.Cultivation[person.BodyId].Structure is null &&
                state.Places[person.Place].Qi == qi && state.CultivationSupply.GetValueOrDefault(person.Place).Total == 0 &&
                state.CreatedMoney == money && person.Action == ActionKind.Idle, "martial work consumes labor without roots, spiritual source, free wealth or automatic repetition");
        }
        state = world.Checkpoint(); person = state.People.Single(p => p.Id == id);
        var combat = world.View(id).Detail!.CombatProjection!;
        CultivationFixture.Check(saved && stages.SequenceEqual(Enumerable.Range(1, 10)) && person.Martial == 10 &&
            person.Lifespan == 600 * 12 && combat.Axes.OutputPenetration > initialPower &&
            combat.Contributions.Count(c => c.SourceId == "martial-body") == 1, "all ten actual stages provide one canonical martial combat contribution and exactly 600 years");
        var reference = world.Checkpoint(); reference.Controlled = 0;
        var core = reference.People.Single(p => p.Id != id);
        WorldScenarioFixtures.SetCultivation(reference, core, Realm.GoldenCore, 9, true);
        var comparison = GameWorld.Restore(reference);
        var martialPower = comparison.View(id).Detail!.CombatProjection!.DisplayPower;
        var corePower = comparison.View(core.Id).Detail!.CombatProjection!.DisplayPower;
        CultivationFixture.Check(martialPower <= corePower * 2 && corePower <= martialPower * 2,
            "actual shared-world neutral projections place the tenth martial stage in the Golden Core power range");
        var completed = world.StateHash();
        CultivationFixture.Check(!world.Execute(CultivationAdapterFixture.Command(id, "martial")).Accepted && world.StateHash() == completed,
            "the completed tenth stage cannot repeat for extra life or power");
        CultivationAdapterFixture.Accepted(world, id, "observe");
        var enabled = world.Execute(new("martial.birth-enabled", 0, "law", Value: 1, Data: "birth.enabled"));
        CultivationFixture.Check(enabled.Accepted, enabled.Message);
        var partner = world.Checkpoint().People.Single(p => p.Id != id); var beforeBirth = world.StateHash();
        CultivationFixture.Check(!world.Execute(CultivationAdapterFixture.Command(id, "conceive", target: partner.Id)).Accepted &&
            world.StateHash() == beforeBirth && world.Checkpoint().Pregnancies.Count == 0 && world.Checkpoint().BirthCount == 0 &&
            world.Checkpoint().Laws["birth.martialEquivalent"] == 0, "actual tenth-stage martial power grants no Golden Core or automatic fertility qualification");
        var eligibleState = world.Checkpoint();
        WorldScenarioFixtures.SetCultivation(eligibleState, eligibleState.People.Single(p => p.Id == id), Realm.GoldenCore);
        var eligible = GameWorld.Restore(eligibleState);
        CultivationAdapterFixture.Accepted(eligible, id, "conceive", target: partner.Id);
        CultivationFixture.Check(eligible.Checkpoint().Pregnancies.Count == 1,
            "the same actual pair qualifies only when the explicit counterfactual also supplies Golden Core");
        CultivationFixture.Check(GameCodec.Decode(GameCodec.Encode(world)).StateHash() == world.StateHash(), "completed martial life and rejected birth save exactly");
    }
}
