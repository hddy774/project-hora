using System.Text.Json;
using Chronicles.Game.People;

namespace Chronicles.Game.Tests.Features;

internal sealed class PeopleSocialPublicFixture
{
    private int sequence;
    internal GameWorld World { get; set; } = PeoplePublicFixture.Fresh(new() { ["birth.enabled"] = 0 });
    internal long First => World.Checkpoint().People[0].Id;
    internal long Second => World.Checkpoint().People[1].Id;
    internal GameCommand Command(string kind, long actor = 0, long target = 0, long value = 0, string data = "") =>
        new($"people-social:{++sequence}", actor, kind, target, value, data);
    internal void Send(string kind, long actor = 0, long target = 0, long value = 0, string data = "") =>
        PeoplePublicFixture.Accept(World, Command(kind, actor, target, value, data));
    internal CommandResult Reject(string kind, long actor, long target = 0, long value = 0, string data = "")
    {
        var command = Command(kind, actor, target, value, data); var hash = World.StateHash();
        var result = World.Execute(command);
        PeopleFixture.Check(!result.Accepted && World.StateHash() == hash, $"Rejected {kind} must preserve all world state/RNG/IDs.");
        return result;
    }
    internal void Control(long person)
    { Send("observe"); Send("possess", person); }
    internal void Cancel(long person)
    {
        Control(person);
        if (World.Checkpoint().People.Single(p => p.Id == person).Action != ActionKind.Idle) Send("cancel", person);
    }
    internal long Request(string kind, long proposer, long recipient, long value = 0, string data = "")
    {
        Control(proposer); Send(kind, proposer, recipient, value, data);
        return World.PeopleRequests(proposer).Last().Id;
    }
    internal void Consent(long recipient, long request)
    { Control(recipient); Send("acceptPeopleRequest", recipient, request); }
    internal long Create(string name, string species = "human", int place = 0, bool gestates = false)
    {
        Send("observe"); Send("create", target: place, data: JsonSerializer.Serialize(new { name, species, gestates }));
        return World.Checkpoint().People.Last().Id;
    }
}

public sealed class PeoplePublicAssessmentAndRelayScenario : IFeatureScenario
{
    public string Name => "INV-008/015/022 people social public: bilateral timed assessment, unseen fame and consented claim relay";
    public void Run()
    {
        var f = new PeopleSocialPublicFixture(); var third = f.Create("평가를 듣지 않은 사람");
        var request = f.Request("requestAssessment", f.First, f.Second, 21);
        var proposed = f.World.Checkpoint();
        PeopleFixture.Check(proposed.PeopleDomain.Reputation.Claims.Length == 0 && proposed.People.All(p => p.Action == ActionKind.Idle),
            "A request cannot reveal an assessment or reserve another person's labor before consent.");
        f.Reject("acceptPeopleRequest", f.First, request);
        f.Consent(f.Second, request);
        var active = f.World.Checkpoint();
        PeopleFixture.Check(active.People.Where(p => p.Id == f.First || p.Id == f.Second).All(p => p.ActionData.StartsWith("people:", StringComparison.Ordinal) && p.Due == 1),
            "Both actual body actions must reserve the same assessment month.");
        f.Reject("gather", f.Second, data: "food");
        var save = GameCodec.Decode(GameCodec.Encode(f.World));
        f.World.AdvanceTo(1); save.AdvanceTo(1);
        PeopleFixture.Check(f.World.StateHash() == save.StateHash(), "Consented assessment must survive a real world save without rerolling.");
        var state = f.World.Checkpoint();
        var estimate = state.PeopleDomain.Reputation.Claims.Single(c => c.ObserverId == f.First && c.SubjectId == f.Second && c.Topic == ReputationTopic.Talent);
        PeopleFixture.Check(estimate.MinimumEstimate < estimate.MaximumEstimate && estimate.MaximumEstimate - estimate.MinimumEstimate >= 1000 &&
            estimate.Confidence < 10000 && estimate.Provenance.Single().Origin == ClaimOrigin.DirectObservation &&
            state.PeopleDomain.Reputation.Claims.All(c => c.ObserverId != third && c.Topic != ReputationTopic.AbsoluteAptitude) &&
            state.PeopleDomain.Social.Sessions.Single().Status == PeopleSessionStatus.Completed,
            "Actual participants receive a coarse performed estimate; an unseen person receives neither global fame nor innate truth.");
        f.Control(third);
        PeopleFixture.Check(f.World.PeopleDetail(f.Second)!.KnownReputation.Length == 0, "Being present in the world cannot confer an unseen evaluation.");
        f.Cancel(f.First); f.Send("explore", f.First); f.World.AdvanceTo(2);
        f.Cancel(f.First); f.Cancel(third);
        var relay = f.Request("requestReputation", f.First, third, data: estimate.Id);
        f.Consent(third, relay); f.World.AdvanceTo(3);
        var received = f.World.Checkpoint().PeopleDomain.Reputation.Claims.Single(c => c.ObserverId == third && c.SubjectId == f.Second && c.Topic == ReputationTopic.Talent);
        PeopleFixture.Check(received.Provenance.Length == 2 && received.Provenance[0] == estimate.Provenance[0] &&
            received.Provenance[1].Origin == ClaimOrigin.Conversation && received.ObservedMonth == estimate.ObservedMonth &&
            received.ReceivedMonth == 3 && received.Confidence < estimate.Confidence,
            "A real relay preserves original dated evidence and adds its actual weaker conversation hop.");
        var detail = f.World.PeopleDetail(f.Second)!;
        PeopleFixture.Check(detail.Innate is null && detail.ActualBody is null && detail.ActualEvaluations is null && detail.KnownReputation.Any(c => c.Id == received.Id),
            "A received claim may be shown while actual other-person foundations/body/evaluation truth remain private.");
        var allSubjects = f.World.PeopleReceivedClaims(third);
        PeopleFixture.Check(allSubjects.Any(c => c.Id == received.Id) && f.World.PeopleReceivedClaims(f.First).Length == 0,
            "All-subject claims remain scoped to the actual current recipient.");
        f.Send("observe"); f.Send("law", value: 1, data: "ai.beliefBudget"); state = f.World.Checkpoint();
        PeopleFixture.Check(state.People.All(p => p.Beliefs.Count + p.ObservedPeople.Count + p.ObservedPlaces.Count +
            state.Inventory.Observations.Where(o => o.Observer == p.Id).Select(o => o.Source).Distinct().Count() +
            state.PeopleDomain.Reputation.Claims.Count(c => c.ObserverId == p.Id) <= 1),
            "Lowering the actual shared cognitive budget must deterministically include received claims alongside other observations.");
        Invariants.Validate(f.World.Checkpoint());
    }
}

public sealed class PeoplePublicOathAndPreferenceScenario : IFeatureScenario
{
    public string Name => "INV-008/015 people social public: source-active oath preference, formal breach and actual AI score change";
    public void Run()
    {
        var f = new PeopleSocialPublicFixture(); var request = f.Request("requestOath", f.First, f.Second, data: "올해 서로의 연구를 돕는다");
        f.Consent(f.Second, request); f.World.AdvanceTo(1); f.Cancel(f.First);
        var active = f.World.Checkpoint(); var oath = active.PeopleDomain.Social.Oaths.Single();
        var promise = active.PeopleDomain.Traits.Traits.Single(t => t.PersonId == f.First && t.DefinitionId == "dao.promise");
        PeopleFixture.Check(Traits.IsActive(promise, 1) && promise.Source.CauseId.StartsWith("event:", StringComparison.Ordinal) &&
            Traits.ActivePreferences(active.PeopleDomain.Traits, f.First, 1).Any(p => p.Preference == GoalPreference.Trust),
            "A source-backed accepted oath can affect only its participants' own active preferences.");
        var stillPromised = GameCodec.Decode(GameCodec.Encode(f.World));
        f.Send("breakOath", f.First, oath.Id);
        var broken = f.World.Checkpoint();
        PeopleFixture.Check(!broken.PeopleDomain.Social.Oaths.Single().Active &&
            broken.PeopleDomain.Traits.Traits.Single(t => t.Id == promise.Id).Status == TraitStatus.Released &&
            !Traits.ActivePreferences(broken.PeopleDomain.Traits, f.First, 1).Any(p => p.Preference == GoalPreference.Trust) &&
            broken.PeopleDomain.Reputation.Claims.Any(c => c.ObserverId == f.Second && c.SubjectId == f.First && c.Topic == ReputationTopic.Reliability && !c.Affirmed),
            "Formal breach releases the actual promise preference and supplies the addressed party a dated reliability claim.");
        PeoplePublicFixture.Accept(stillPromised, new("people-active-planner-observe", 0, "observe"));
        f.Send("observe"); stillPromised.AdvanceTo(2); f.World.AdvanceTo(2);
        var activeChoice = stillPromised.Checkpoint().People.Single(p => p.Id == f.First).Decisions.Last();
        var releasedChoice = f.World.Checkpoint().People.Single(p => p.Id == f.First).Decisions.Last();
        var activeScore = activeChoice.Candidates.Single(c => c.Goal == "교역·교섭").Score;
        var releasedScore = releasedChoice.Candidates.Single(c => c.Goal == "교역·교섭").Score;
        PeopleFixture.Check(activeScore > releasedScore, "The actual next AI goal score must consume the active preference and stop after its source ends.");
        Invariants.Validate(broken);
    }
}

public sealed class PeoplePublicHabitTraitScenario : IFeatureScenario
{
    public string Name => "INV-008/015 people social public: completed martial routine, dated acquisition and inactivity release";
    public void Run()
    {
        var f = new PeopleSocialPublicFixture();
        f.World = PeoplePublicFixture.Configure(f.World, s => s.People[0].Habits["Martial"] = 100);
        f.Control(f.First);
        for (var index = 0; index < 3; index++)
        {
            f.Send("martial", f.First); f.World.AdvanceTo(f.World.Checkpoint().People.Single(p => p.Id == f.First).Due);
        }
        var state = f.World.Checkpoint();
        var routine = state.PeopleDomain.Traits.Traits.Single(t => t.PersonId == f.First && t.DefinitionId == "habit.routine");
        PeopleFixture.Check(Traits.IsActive(routine, state.Month) && routine.AcquiredMonth > 0 && routine.Source.Reference.Contains("Martial:3", StringComparison.Ordinal) &&
            state.PeopleDomain.Social.Habits.Single(h => h.Person == f.First && h.Habit == "Martial") is { Count: 3 } observed && observed.LastCompleted == state.Month,
            "Three actual completed actions form the dated routine even after the legacy habit counter reaches its cap; old unobserved counts grant nothing.");
        f.World = PeoplePublicFixture.Configure(f.World, s => s.PeopleDomain = s.PeopleDomain with
            { Social = s.PeopleDomain.Social with { Policy = s.PeopleDomain.Social.Policy with { HabitLapseMonths = 1 } } });
        f.Send("wait", f.First); f.World.AdvanceTo(f.World.Month + 1); f.Send("wait", f.First); f.World.AdvanceTo(f.World.Month + 1);
        var ended = f.World.Checkpoint().PeopleDomain.Traits.Traits.Single(t => t.Id == routine.Id);
        PeopleFixture.Check(ended.Status == TraitStatus.Released && ended.History.Last().Source.Reference.Contains("종료", StringComparison.Ordinal),
            "Actual inactivity ends a habit's current preference while preserving its acquisition/source history.");
    }
}

public sealed class PeoplePublicTitleAndRebirthScenario : IFeatureScenario
{
    public string Name => "INV-004/014/015 people social public: actual historical founders, lost authority and no title restoration through soul";
    public void Run()
    {
        var world = PeoplePublicFixture.Fresh(new() { ["soul.crossing"] = 0 }); var state = world.Checkpoint();
        var father = state.People.Single(p => !p.Gestates); var mother = state.People.Single(p => p.Gestates); var home = father.Household;
        world = PeoplePublicFixture.Configure(world, s => s.People.Single(p => p.Id == father.Id).KnownPlaces = Enumerable.Range(0, 10).ToList());
        PeoplePublicFixture.Accept(world, new("people-title-control", father.Id, "possess"));
        PeoplePublicFixture.Accept(world, new("people-title-found", father.Id, "found", Data: "sect")); world.AdvanceTo(1);
        PeoplePublicFixture.Accept(world, new("people-title-govern", father.Id, "govern", home, 0));
        state = world.Checkpoint();
        PeopleFixture.Check(state.PeopleDomain.Traits.Traits.Any(t => t.PersonId == father.Id && t.DefinitionId == "status.sect-founder" && Traits.IsActive(t, 1)) &&
            state.PeopleDomain.Traits.Traits.Any(t => t.PersonId == father.Id && t.DefinitionId == "status.dynasty-founder" && Traits.IsActive(t, 1)) &&
            state.PeopleDomain.Traits.Traits.Any(t => t.PersonId == father.Id && t.DefinitionId == "status.landlord" && Traits.IsActive(t, 1)),
            "Actual organization founding and sovereignty create separate historical and current authority references.");
        PeoplePublicFixture.Accept(world, new("people-title-release", father.Id, "releaseProvince", home, 0));
        state = world.Checkpoint();
        PeopleFixture.Check(state.PeopleDomain.Traits.Traits.Single(t => t.PersonId == father.Id && t.DefinitionId == "status.landlord").Status == TraitStatus.Released &&
            state.PeopleDomain.Traits.Traits.Single(t => t.PersonId == father.Id && t.DefinitionId == "status.dynasty-founder").Status == TraitStatus.Active,
            "Losing actual territory releases current authority while retaining the true historical event.");
        PeoplePublicFixture.Accept(world, new("people-title-observe", 0, "observe"));
        var pregnancy = PeoplePublicFixture.Conceive(world);
        world = PeoplePublicFixture.Configure(world, s => s.People.Single(p => p.Id == father.Id).Lifespan = 2 - father.Born);
        PeoplePublicFixture.Accept(world, new("people-title-mother", mother.Id, "possess"));
        var child = PeoplePublicFixture.Birth(world, pregnancy.Id); state = world.Checkpoint();
        PeopleFixture.Check(child.SoulId == father.SoulId && child.Id != father.Id &&
            state.PeopleDomain.Traits.Traits.All(t => t.PersonId != child.Id || TraitCatalog.Get(t.DefinitionId).Category != TraitCategory.Status),
            "A returning soul receives no historical title, office or authority on the new person.");
        Invariants.Validate(state);
    }
}

public sealed class PeoplePublicSpeciesAndProjectionScenario : IFeatureScenario
{
    public string Name => "INV-006/011/015 people public species: explicit body capabilities, maturity/lifespan, habitat evidence and strict projections";
    public void Run()
    {
        var f = new PeopleSocialPublicFixture(); var fox = f.Create("명시 영호", "spirit-fox"); var pine = f.Create("명시 영송", "spirit-pine");
        var ape = f.Create("명시 석원", "stone-ape"); var water = f.Create("명시 수령", "water-spirit");
        var state = f.World.Checkpoint(); var actual = state.People.Single(p => p.Id == fox); var profile = state.PeopleDomain.Profiles.Single(p => p.PersonId == fox);
        PeopleFixture.Check(actual.Species == "spirit-fox" && actual.Lifespan == 1440 && state.Month - actual.Born == 144 &&
            profile.Body.CurrentForm == BodyForm.Quadruped && !profile.Body.Capabilities.Manipulation && profile.Body.Reproduction.MaturityMonths == 144 &&
            profile.Body.Origin == PersonOrigin.ExplicitArrival && state.Items.All(i => i.Owner != fox && i.Owner != pine && i.Owner != ape && i.Owner != water) &&
            SpeciesCatalog.All.All(s => state.People.Any(p => p.Species == s.Id)) &&
            state.PeopleDomain.Profiles.Single(p => p.PersonId == ape).Body.Capabilities.Manipulation &&
            state.PeopleDomain.Profiles.Single(p => p.PersonId == water).Body.Capabilities.SpiritualManipulation,
            "Explicit species entry creates its actual body/maturity/lifespan/source and supplies no imaginary assets.");
        f.Control(fox); var craft = f.Reject("craft", fox, data: "paper");
        f.Control(pine); var travel = f.Reject("travel", pine, 1);
        PeopleFixture.Check(craft.Message.Contains("현재 육신", StringComparison.Ordinal) && travel.Message.Contains("현재 육신", StringComparison.Ordinal),
            "Nonhuman action failures must be caused by the actual manipulation/movement body gate.");
        f.Send("wait", pine); f.World.AdvanceTo(1);
        var ecology = f.World.PeopleEcology(pine)!;
        PeopleFixture.Check(ecology.Month == 1 && !ecology.HabitatSuitable && ecology.GenericMaintenanceAbstraction && ecology.HealthCost > 0 &&
            ecology.Source.PersonId == pine && ecology.Source.Reference.Contains("food", StringComparison.Ordinal),
            "A nonhuman actually senses habitat stress and the existing finite maintenance abstraction is recorded explicitly.");
        PeopleFixture.Check(f.World.Checkpoint().PeopleDomain.Social.Ecology.Single(e => e.Person == water && e.Month == 1).AmbientQiConsumed == 1,
            "A spirit's ambient-qi maintenance must consume the actual finite local qi once.");
        PeopleFixture.Check(f.World.PeopleEcology(fox) is null && f.World.PeopleDetail(fox)!.ActualBody is null,
            "Another person's physiology/body truth is not exposed through personal queries.");
        state = f.World.Checkpoint(); state.People.Single(p => p.Id == fox).Roots[0]++;
        try { _ = GameWorld.Restore(state); throw new InvalidOperationException("A silently stale raw root projection was accepted."); }
        catch (InvalidDataException) { }
        var parsed = PeopleCreationParser.TryParse(new("people-legacy-name", 0, "create", Value: 2, Data: "명시 인간"), state.Laws, out var human, out _);
        PeopleFixture.Check(parsed && human.Species == "human" && human.Gestates, "Legacy plain-name creation retains its human/nonzero gestates semantics.");
    }
}

public sealed class PeoplePublicMutationPolicyScenario : IFeatureScenario
{
    public string Name => "INV-004/011/012 people public genetics: configured zero mutation and frozen model/stream after law changes";
    public void Run()
    {
        var world = PeoplePublicFixture.Fresh(new() { ["genetics.mutationMagnitude"] = 10000, ["genetics.mutationChance"] = 0 });
        var pregnancy = PeoplePublicFixture.Conceive(world); var state = world.Checkpoint();
        var captured = state.PeopleDomain.Inheritance.Conceptions.Single(); var frozen = JsonSerializer.Serialize(captured);
        PeopleFixture.Check(captured.Model.MutationChance == 0 && captured.Model.MutationMagnitude == 10000 && captured.Selections.All(g => g.MutationDelta == 0),
            "Actual conception must use its explicitly configured mutation chance/magnitude.");
        PeoplePublicFixture.Accept(world, new("people-mutation-law-change", 0, "law", Value: 10000, Data: "genetics.mutationChance"));
        var child = PeoplePublicFixture.Birth(world, pregnancy.Id);
        PeopleFixture.Check(frozen == JsonSerializer.Serialize(world.Checkpoint().PeopleDomain.Inheritance.Conceptions.Single()) &&
            child.Aptitudes.Take(5).SequenceEqual(captured.BodyFoundations) && child.Roots.SequenceEqual(captured.ElementRoots),
            "A later law revision cannot rewrite or reroll the already captured allele stream and model.");
        var zeroMagnitude = PeoplePublicFixture.Fresh(new() { ["genetics.mutationMagnitude"] = 0, ["genetics.mutationChance"] = 10000 });
        PeoplePublicFixture.Conceive(zeroMagnitude);
        var zero = zeroMagnitude.Checkpoint().PeopleDomain.Inheritance.Conceptions.Single();
        PeopleFixture.Check(zero.Model.MutationChance == 10000 && zero.Model.MutationMagnitude == 0 && zero.Selections.All(s => s.MutationDelta == 0),
            "A certain mutation draw with explicitly zero magnitude cannot alter the inherited alleles.");
    }
}

public sealed class PeoplePublicNonhumanBirthScenario : IFeatureScenario
{
    public string Name => "INV-004/011/014/015 people public species: actual body maturity, native gestation, frozen offspring and independent birth soul";
    public void Run()
    {
        var f = new PeopleSocialPublicFixture { World = PeoplePublicFixture.Fresh() };
        var mother = f.Create("영송 모체", "spirit-pine", 2, true); var father = f.Create("영송 전달체", "spirit-pine", 2);
        f.World = PeoplePublicFixture.Configure(f.World, state =>
        {
            foreach (var person in state.People.Where(p => p.Id == mother || p.Id == father))
            {
                PeoplePublicFixture.CurrentRealm(state, person, Realm.GoldenCore);
                person.Personality[4] = 60; person.Personality[5] = 60;
            }
            state.People.Single(p => p.Id == mother).Born = -598;
        });
        f.Control(mother); f.Send("explore", mother); f.World.AdvanceTo(1);
        f.Reject("partner", mother, father);
        f.World.AdvanceTo(2); f.Send("partner", mother, father); f.Send("conceive", mother, father);
        var state = f.World.Checkpoint(); var pregnancy = state.Pregnancies.Single();
        var conception = state.PeopleDomain.Inheritance.Conceptions.Single();
        PeopleFixture.Check(pregnancy.Due == state.Month + 12 && conception.SpeciesId == "spirit-pine" &&
            state.People.Single(p => p.Id == mother).Born + 600 == state.Month,
            "The actual pine body must reach its 600-month maturity and retain its native 12-month gestation.");
        var frozen = JsonSerializer.Serialize(conception);
        f.Send("observe"); f.Send("law", value: 1, data: "birth.gestation"); f.Control(mother);
        var child = PeoplePublicFixture.Birth(f.World, pregnancy.Id); state = f.World.Checkpoint();
        var profile = state.PeopleDomain.Profiles.Single(p => p.PersonId == child.Id);
        var actualSoul = state.PeopleDomain.Souls.Single(s => s.SoulId == child.SoulId);
        PeopleFixture.Check(child.Species == "spirit-pine" && child.Lifespan == 3600 && child.Born == pregnancy.Due &&
            profile.Body.CurrentForm == BodyForm.RootedPlant && profile.Body.Reproduction.MaturityMonths == 600 &&
            profile.Body.Origin == PersonOrigin.BiologicalBirth && child.ParentOne == mother && child.ParentTwo == father &&
            child.Roots.SequenceEqual(conception.ElementRoots) && child.Aptitudes.Take(5).SequenceEqual(conception.BodyFoundations),
            "An actual nonhuman birth retains species lifespan/body, biological parents and frozen body-only inheritance.");
        PeopleFixture.Check(child.Aptitudes[5] == actualSoul.Foundation!.Value && child.SoulId != state.People.Single(p => p.Id == mother).SoulId &&
            child.SoulId != state.People.Single(p => p.Id == father).SoulId && child.Realm == Realm.Mortal && child.Skills.Count == 0 && child.Understanding.Count == 0 &&
            frozen == JsonSerializer.Serialize(state.PeopleDomain.Inheritance.Conceptions.Single()),
            "Actual soul assignment remains independent of the body-parent DAG and returns no free parental skill/realm.");
        PeopleFixture.Check(GameCodec.Decode(GameCodec.Encode(f.World)).StateHash() == f.World.StateHash(), "The actual native-species birth resumes identically from the production save.");
    }
}
