namespace Chronicles.Game.Tests.Features;

public sealed class PeoplePublicTeachingRequestAdapterScenario : IFeatureScenario
{
    public string Name => "INV-004/008/024 people teaching public: AI missing-method requests use the same actual knowledge adapter and source as player requests";
    public void Run()
    {
        var f = new PeopleSocialPublicFixture { World = PeoplePublicFixture.Fresh(new() { ["birth.enabled"] = 0, ["ecology.foodUse"] = 0 }) };
        var pupil = f.Create("입문을 모르는 실제 학습자"); var teacher = f.First;
        f.World = PeoplePublicFixture.Configure(f.World, state =>
        {
            foreach (var other in state.People.Where(p => p.Id != pupil)) { other.Action = ActionKind.Guard; other.Due = 100; }
            // Declare the current custody of one existing finite opening stack; no material is minted.
            var food = state.Items.First(i => i.Owner == teacher && i.Definition == "food"); food.Owner = pupil;
            var metadata = state.Inventory.Metadata.FirstOrDefault(m => m.Item == food.Id);
            if (metadata is not null) metadata.Custodian = pupil;
        });
        var publicWorld = GameCodec.Decode(GameCodec.Encode(f.World));
        PeoplePublicFixture.Accept(publicWorld, new("people-public-learning-request", pupil, "requestTeaching", teacher));
        var publicRequest = publicWorld.Checkpoint().People.Single(p => p.Id == teacher).LessonRequests.Single(r => r.Pupil == pupil);
        var randomBefore = f.World.Checkpoint().RandomState;
        f.World.AdvanceTo(1); var requested = f.World.Checkpoint();
        var actualRequest = requested.People.Single(p => p.Id == teacher).LessonRequests.Single(r => r.Pupil == pupil);
        var learner = requested.People.Single(p => p.Id == pupil);
        var source = learner.Journal.Single(e => e.Kind == "배움 요청");
        var receipt = learner.Beliefs["plan:request:entry"];
        var decision = learner.Decisions.Single(d => d.Month == requested.Month);
        var expectedSource = $"plan:{learner.BodyId}:{decision.Month}:{learner.Decisions.Count - 1}:{decision.Expansions}:requestTeaching";
        PeopleFixture.Check(actualRequest.Knowledge == publicRequest.Knowledge && actualRequest.Knowledge == "entry" && actualRequest.Requested == 1 &&
            source.Actor == pupil && source.Target == teacher && source.Source == expectedSource && source.Source == receipt.Source &&
            learner.Plan.Any(s => s.Action == "awaitKnowledge" && s.Target == teacher && s.Data == actualRequest.Knowledge) &&
            requested.People.Single(p => p.Id == pupil) is { Action: ActionKind.Wait, Due: 2 } &&
            requested.People.Single(p => p.Id == teacher).Action == ActionKind.Guard,
            "Actual AI fallback must delegate the unresolved method to the same canonical adapter, reserve its own waiting month and deliver its real source to the busy teacher.");
        PeopleFixture.Check(requested.Knowledge.Memories.All(m => m.PersonId != pupil) &&
            requested.PeopleDomain.Profiles.Single(p => p.PersonId == pupil).Learning.Length == 0,
            "A delivered request cannot grant the unknown method or any completed-learning credit.");
        PeopleFixture.Check(GameCodec.Decode(GameCodec.Encode(f.World)).StateHash() == f.World.StateHash() && requested.RandomState == randomBefore,
            "The actual source-backed AI request survives production save continuation without consuming RNG.");
        Invariants.Validate(requested);
    }
}
