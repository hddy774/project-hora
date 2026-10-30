using System.Text.Json;
using Chronicles.Game.Inventory;
using Chronicles.Game.Knowledge;

namespace Chronicles.Game.Tests.Features;

internal static class AgentPlanningFixture
{
    internal static GameWorld Fresh(string? goal = null, int budget = 64)
    {
        var world = GameWorld.Create(42, new Dictionary<string, long>(StringComparer.Ordinal)
        {
            ["birth.enabled"] = 0, ["ecology.foodUse"] = 0, ["ecology.disasterChance"] = 0,
            ["ai.planBudget"] = budget, ["ai.beliefBudget"] = 64, ["knowledge.misreadChance"] = 0
        });
        var state = world.Checkpoint();
        state.People[1].Action = ActionKind.Guard; state.People[1].Due = 1000;
        var actor = state.People[0];
        state.People[1].Money = checked(state.People[1].Money + actor.Money); actor.Money = 0;
        if (goal is not null) { actor.Plan = [new("replan", 0, goal)]; actor.Goal = "실제 생산 목표"; }
        return GameWorld.Restore(state);
    }
    internal static Person Actor(GameWorld world) => world.Checkpoint().People[0];
    internal static GameState Copy(GameState state) => GameWorld.Restore(state).Checkpoint();
    internal static void Accept(GameWorld world, string id, string kind, long actor = 0, long target = 0, long value = 0, string data = "")
    {
        var result = world.Execute(new(id, actor, kind, target, value, data));
        Check(result.Accepted, result.Message);
    }
    internal static void Check(bool valid, string message)
    { if (!valid) throw new InvalidOperationException(message); }
    internal static void Stock(GameState state, long owner, string definition, int quantity)
    {
        state.Items.Add(new() { Id = state.NextId++, Owner = owner, Definition = definition, Quantity = quantity,
            Place = -1, Quality = 5000, Source = "인지 기능 검사의 명시 초기 실물" });
        if (!state.Materials.TryGetValue(definition, out var flow)) state.Materials.Add(definition, flow = new());
        flow.Opening += quantity;
    }
    internal static void Until(GameWorld world, Func<GameState, bool> condition, int maximumMonths)
    {
        for (var count = 0; count < maximumMonths && !condition(world.Checkpoint()); count++) world.AdvanceTo(world.Month + 1);
        Check(condition(world.Checkpoint()), "Bounded functional goal did not complete: " + Actor(world).Reason);
    }
    internal static bool Completed(GameState state, long actor, string recipe) => state.People.Single(p => p.Id == actor)
        .Beliefs.GetValueOrDefault("plan:completed")?.Value == recipe;
}

public sealed class AgentPlanningProductionScenario : IFeatureScenario
{
    public string Name => "INV-004/008/017/018 agent planning: unknown source discovery then real extraction and production complete a saved goal";
    public void Run()
    {
        var world = AgentPlanningFixture.Fresh("paper"); var actor = AgentPlanningFixture.Actor(world).Id;
        var initial = world.Checkpoint();
        AgentPlanningFixture.Check(!initial.Inventory.Observations.Any(o => o.Observer == actor), "Fixture already knew hidden sources.");
        world.AdvanceTo(1);
        var first = world.Checkpoint();
        AgentPlanningFixture.Check(first.People[0].ActionData == "inventory:exploration" && first.Materials.GetValueOrDefault("paper")?.Production is not > 0,
            "A missing resource was obtained from hidden stock without real exploration.");
        world = GameCodec.Decode(GameCodec.Encode(world));
        AgentPlanningFixture.Until(world, s => AgentPlanningFixture.Completed(s, actor, "paper"), 40);
        var done = world.Checkpoint();
        AgentPlanningFixture.Check(done.Inventory.Observations.Any(o => o.Observer == actor && o.Definition == "wood" && o.Evidence.Contains("job:", StringComparison.Ordinal)) &&
            done.Inventory.Jobs.Any(j => j.Actor == actor && j.Source > 0 && j.Status == "completed") &&
            done.Inventory.Jobs.Any(j => j.Actor == actor && j.Recipe == "paper" && j.Status == "completed") && done.Materials["paper"].Production >= 3,
            "The completed goal lacked real observation, extraction, consumption or output receipts.");
        AgentPlanningFixture.Check(done.People[0].Decisions.Any(d => d.Expansions > 0) && done.People[0].Decisions.All(d => d.Expansions <= 64),
            "Decision history did not record actual bounded expansions.");
        Invariants.Validate(done);
    }
}

public sealed class AgentPlanningTradeScenario : IFeatureScenario
{
    public string Name => "INV-008/017/018 agent planning: production surplus becomes a real public offer and another actor buys from its own need";
    public void Run()
    {
        var world = AgentPlanningFixture.Fresh("tool"); var state = world.Checkpoint(); var actor = state.People[0].Id;
        AgentPlanningFixture.Stock(state, actor, "ore", 2); AgentPlanningFixture.Stock(state, actor, "wood", 2);
        state.People[0].Personality = new int[14]; state.People[0].Personality[4] = 100; state.People[0].Personality[5] = 100;
        world = GameWorld.Restore(state);
        AgentPlanningFixture.Until(world, s => s.Inventory.Contracts.Any(c => c.Seller == actor && c.Kind == "sale" && c.Status == "open"), 8);
        state = world.Checkpoint(); var contract = state.Inventory.Contracts.Single(c => c.Seller == actor && c.Status == "open");
        AgentPlanningFixture.Check(state.Inventory.Jobs.Any(j => j.Actor == actor && j.Recipe == "tool" && j.Status == "completed") &&
            state.Items.Where(i => i.Owner == actor && i.Definition == "tool" && i.Reserved == 0).Sum(i => i.Quantity) >= 1,
            "Selling surplus either skipped real production or sold the last necessary tool.");
        var buyer = state.People[1];
        foreach (var tool in state.Items.Where(i => i.Owner == buyer.Id && i.Definition == "tool").ToArray())
        { state.Items.Remove(tool); state.Materials[tool.Definition].Loss += tool.Quantity; state.Inventory.Metadata.RemoveAll(m => m.Item == tool.Id); }
        state.People[0].Action = ActionKind.Guard; state.People[0].Due = state.Month + 100;
        buyer.Action = ActionKind.Idle; buyer.Due = 0; buyer.Personality = new int[14]; buyer.Personality[4] = 100; buyer.Personality[5] = 100;
        world = GameWorld.Restore(state); world.AdvanceTo(world.Month + 1);
        var purchased = world.Checkpoint().Inventory.Contracts.Single(c => c.Id == contract.Id);
        AgentPlanningFixture.Check(purchased.Status == "completed" && purchased.Buyer == buyer.Id &&
            world.Checkpoint().Items.Single(i => i.Id == contract.Item).Owner == buyer.Id, "AI trade did not use the real local advertised contract.");
        Invariants.Validate(world.Checkpoint());
    }
}

public sealed class AgentPlanningFailureScenario : IFeatureScenario
{
    public string Name => "INV-008/024 agent planning: an incorrect source belief is rejected by actual execution then replaced through observation";
    public void Run()
    {
        var world = AgentPlanningFixture.Fresh("paper"); var state = world.Checkpoint(); var actor = state.People[0].Id;
        state.Laws["ecology.regrowth"] = 0;
        var source = state.Inventory.Sources.Single(s => s.Place == 0 && s.Definition == "wood");
        state.Inventory.Observations.Add(new(actor, source.Id, 0, 12, 0, "기능 검사: 과거의 실제 원천 주장", "wood", source.Activity));
        source.Remaining = 0; state.Places[0].Source = (int)state.Inventory.Sources.Where(s => s.Place == 0).Sum(s => s.Remaining);
        world = GameWorld.Restore(state); world.AdvanceTo(1);
        var failed = world.Checkpoint();
        AgentPlanningFixture.Check(failed.People[0].Beliefs.ContainsKey("plan:lastFailure") &&
            !failed.Inventory.Observations.Any(o => o.Observer == actor && o.Source == source.Id) &&
            failed.Materials.GetValueOrDefault("paper")?.Production is not > 0,
            "A failed real extraction retained an unsupported positive source claim or invented output.");
        AgentPlanningFixture.Until(world, s => s.Inventory.Observations.Any(o => o.Observer == actor && o.Source == source.Id && o.Remaining == 0), 40);
        var corrected = world.Checkpoint();
        AgentPlanningFixture.Check(corrected.Inventory.Observations.Where(o => o.Observer == actor && o.Source == source.Id).All(o => o.Remaining == 0) &&
            corrected.People[0].Decisions.Any(d => d.Reason.Contains("실제", StringComparison.Ordinal)), "Belief correction lacked actual observation and decision evidence.");
    }
}

public sealed class AgentPlanningInformationFairnessScenario : IFeatureScenario
{
    public string Name => "INV-006/008 agent planning: unchanged personal information produces the same choice despite hidden NPC inventory knowledge and wealth";
    public void Run()
    {
        var a = AgentPlanningFixture.Fresh("paper");
        AgentPlanningFixture.Accept(a, "unknown-funds-recipient", "create", target: 99, data: "정보 밖 실제 인물");
        var baseline = a.Checkpoint(); baseline.People[1].Money -= 17; baseline.People[2].Money += 17;
        baseline.People[2].Action = ActionKind.Guard; baseline.People[2].Due = 1000;
        a = GameWorld.Restore(baseline); var hidden = a.Checkpoint(); var other = hidden.People[1];
        other.Money += 17; hidden.People[2].Money -= 17; AgentPlanningFixture.Stock(hidden, other.Id, "herb", 1);
        var authority = KnowledgeAuthority.Restore(hidden.Knowledge);
        var edition = hidden.Knowledge.Editions.First(e => e.WorkId == "path.1.0").Id;
        AgentPlanningFixture.Check(authority.SeedInitialKnowledge(other.Id, edition, [2], 70, "fixture:hidden-private-knowledge").Applied,
            "Valid hidden-person knowledge fixture failed.");
        hidden.Knowledge = authority.Checkpoint();
        var b = GameWorld.Restore(hidden); a.AdvanceTo(1); b.AdvanceTo(1);
        var left = AgentPlanningFixture.Actor(a); var right = AgentPlanningFixture.Actor(b);
        AgentPlanningFixture.Check(left.Goal == right.Goal && left.Reason == right.Reason && left.Action == right.Action &&
            left.ActionData == right.ActionData && left.Due == right.Due && left.PlanExpansions == right.PlanExpansions &&
            JsonSerializer.Serialize(left.Decisions[^1]) == JsonSerializer.Serialize(right.Decisions[^1]),
            "Planner used a hidden NPC fact or global knowledge revision to change its decision or timing.");
    }
}

public sealed class AgentPlanningBudgetScenario : IFeatureScenario
{
    public string Name => "INV-004/008/021 agent planning: actual search and source cognition budgets are bounded and a deferred goal resumes after save";
    public void Run()
    {
        var world = AgentPlanningFixture.Fresh("paper", 1); world.AdvanceTo(1);
        var actor = AgentPlanningFixture.Actor(world);
        AgentPlanningFixture.Check(actor.PlanExpansions <= 1 && actor.Plan.Any(s => s.Action == "replan" && s.Data == "paper") && actor.Action == ActionKind.Wait,
            "Budget exhaustion lost its goal, exceeded actual expansions, or granted free work.");
        world = GameCodec.Decode(GameCodec.Encode(world));
        AgentPlanningFixture.Accept(world, "raise-real-budget", "law", value: 64, data: "ai.planBudget");
        var id = AgentPlanningFixture.Actor(world).Id;
        AgentPlanningFixture.Until(world, s => AgentPlanningFixture.Completed(s, id, "paper"), 40);
        AgentPlanningFixture.Accept(world, "shrink-memory", "law", value: 3, data: "ai.beliefBudget");
        world.AdvanceTo(world.Month + 1); actor = AgentPlanningFixture.Actor(world);
        var ownSources = world.Checkpoint().Inventory.Observations.Count(o => o.Observer == id);
        AgentPlanningFixture.Check(actor.Beliefs.Count + actor.ObservedPeople.Count + actor.ObservedPlaces.Count + ownSources <= 3 &&
            world.Checkpoint().Inventory.Jobs.Any(j => j.Actor == id && j.Status == "completed"),
            "Memory eviction exceeded the shared budget or erased actual process history.");
    }
}

public sealed class AgentPlanningReplayScenario : IFeatureScenario
{
    public string Name => "INV-004/006 agent planning: identical real goal commands and save/resume preserve choices receipts and world hash";
    public void Run()
    {
        var a = AgentPlanningFixture.Fresh("paper"); var b = GameCodec.Decode(GameCodec.Encode(a));
        for (var month = 1; month <= 24; month++)
        {
            a.AdvanceTo(month); b.AdvanceTo(month);
            if (month == 4 || month == 11) b = GameCodec.Decode(GameCodec.Encode(b));
            AgentPlanningFixture.Check(a.StateHash() == b.StateHash(), "Saved planner resumed with a different choice, receipt, budget or RNG stream.");
        }
    }
}

public sealed class AgentPlanningRegistryScenario : IFeatureScenario
{
    public string Name => "INV-008/017/024 agent planning: an advanced authoritative recipe uses learned method and discovered actual family resources";
    public void Run()
    {
        const string recipe = "produce.resource.01.0";
        var world = AgentPlanningFixture.Fresh(recipe); var state = world.Checkpoint(); var actor = state.People[0];
        actor.Place = 1; actor.KnownPlaces.Add(1);
        var authority = KnowledgeAuthority.Restore(state.Knowledge);
        foreach (var medium in state.Knowledge.Media.Where(m => m.OwnerId == actor.Id))
            AgentPlanningFixture.Check(authority.SynchronizeMedium(medium.ItemId, actor.Id, 1, medium.Quality,
                medium.MissingChapters, medium.Unreadable, false, 0, "fixture:actual-move:" + medium.ItemId).Applied, "Actual carried book fixture failed.");
        state.Knowledge = authority.Checkpoint(); world = GameWorld.Restore(state);
        AgentPlanningFixture.Until(world, s => AgentPlanningFixture.Completed(s, actor.Id, recipe), 40);
        var complete = world.Checkpoint();
        AgentPlanningFixture.Check(complete.Inventory.Jobs.Any(j => j.Actor == actor.Id && j.Recipe == recipe && j.Status == "completed") &&
            complete.Materials["resource.01.0"].Production > 0 && complete.Inventory.Observations.Any(o => o.Observer == actor.Id && o.Definition == "resource.00.0"),
            "Planner reverted to legacy general skills/recipes or invented advanced inputs.");
    }
}

public sealed class AgentPlanningTeacherScenario : IFeatureScenario
{
    public string Name => "INV-008/017/024 agent planning: missing recipe knowledge requests an encountered teacher and waits for actual independent teaching";
    public void Run()
    {
        var world = AgentPlanningFixture.Fresh();
        AgentPlanningFixture.Accept(world, "real-adult-arrival", "create", data: "도제");
        var state = world.Checkpoint(); var teacher = state.People[0].Id; var pupil = state.People[^1].Id;
        AgentPlanningFixture.Stock(state, pupil, "food", 100);
        var tutor = state.People[0]; tutor.Personality[4] = 100;
        if (!tutor.KnownPeople.Contains(pupil)) tutor.KnownPeople.Add(pupil);
        var learner = state.People[^1];
        if (!learner.KnownPeople.Contains(teacher)) learner.KnownPeople.Add(teacher);
        world = GameWorld.Restore(state);
        foreach (var work in new[] { "entry", "recipe:weapon" })
        {
            AgentPlanningFixture.Accept(world, "teacher-control-open-" + work, "observe");
            foreach (var person in world.Checkpoint().People.Where(p => p.Id == teacher || p.Id == pupil))
                if (person.Action != ActionKind.Idle) AgentPlanningFixture.Accept(world, "cancel-setup-" + work + ":" + person.Id, "cancel", person.Id);
            AgentPlanningFixture.Accept(world, "real-course-" + work, "teach", teacher, pupil, data: work);
            AgentPlanningFixture.Accept(world, "teacher-controls-course-" + work, "possess", teacher);
            world.AdvanceTo(world.Month + 1);
        }
        AgentPlanningFixture.Accept(world, "open-planning-observation", "observe");
        foreach (var person in world.Checkpoint().People.Where(p => p.Id == teacher || p.Id == pupil))
            if (person.Action != ActionKind.Idle) AgentPlanningFixture.Accept(world, "cancel-final-setup:" + person.Id, "cancel", person.Id);
        state = world.Checkpoint(); learner = state.People.Single(p => p.Id == pupil); tutor = state.People.Single(p => p.Id == teacher);
        tutor.Action = ActionKind.Guard; tutor.Due = state.Month + 100;
        learner.Plan = [new("replan", 0, "tool")]; learner.Goal = "도구 제작 목표";
        AgentPlanningFixture.Check(learner.Understanding.GetValueOrDefault("smith") > 0 &&
            !state.Knowledge.Memories.Any(m => m.PersonId == pupil && m.WorkId == "recipe:tool"), "Fixture must know another smith method, not this recipe.");
        world = GameWorld.Restore(state); world.AdvanceTo(world.Month + 1); state = world.Checkpoint();
        AgentPlanningFixture.Check(state.People.Single(p => p.Id == pupil).Action == ActionKind.Wait &&
            state.People.Single(p => p.Id == teacher).LessonRequests.Any(r => r.Pupil == pupil && r.Knowledge == "recipe:tool") &&
            !state.Inventory.Jobs.Any(j => j.Actor == pupil && j.Recipe == "tool"), "A general skill shortcut granted the missing method or skipped a real request.");
        tutor = state.People.Single(p => p.Id == teacher); tutor.Action = ActionKind.Idle; tutor.Due = 0;
        world = GameWorld.Restore(state);
        AgentPlanningFixture.Until(world, s => s.Knowledge.Memories.Any(m => m.PersonId == pupil && m.WorkId == "recipe:tool"), 5);
        var learned = world.Checkpoint();
        AgentPlanningFixture.Check(learned.Knowledge.Activities.Any(a => a.Kind == KnowledgeActivityKind.Teaching && a.ActorId == pupil &&
            a.TeacherId == teacher && a.WorkId == "recipe:tool" && a.Status == KnowledgeActivityStatus.Completed) &&
            !learned.Knowledge.Mastery.Any(m => m.PersonId == pupil), "Missing knowledge appeared without an actual teacher/pupil action or granted free skill.");
    }
}

public sealed class AgentPlanningDispositionScenario : IFeatureScenario
{
    public string Name => "INV-006/008 agent planning: all fourteen axes affect scored goals and actual mood and completed habits change choice";
    public void Run()
    {
        var world = AgentPlanningFixture.Fresh(); var baseline = world.Checkpoint();
        AgentPlanningFixture.Stock(baseline, baseline.People[0].Id, "paper", 2); AgentPlanningFixture.Stock(baseline, baseline.People[0].Id, "ink", 2);
        // Isolate the close mood/habit comparison from the independently tested food-surplus goal.
        // Food use is zero here; keep the same finite food as the stationary other actor's ground stock.
        foreach (var food in baseline.Items.Where(i => i.Owner == baseline.People[0].Id && i.Definition == "food"))
        {
            food.Owner = baseline.People[1].Id; food.Place = baseline.People[1].Place;
            if (baseline.Inventory.Metadata.FirstOrDefault(m => m.Item == food.Id) is { } metadata)
            { metadata.Carrier = 0; metadata.Custodian = food.Owner; }
        }
        baseline.People[0].Personality = Enumerable.Repeat(50, 14).ToArray();
        var original = GameWorld.Restore(baseline); original.AdvanceTo(1);
        var scores = AgentPlanningFixture.Actor(original).Decisions[^1].Candidates.ToDictionary(c => c.Goal, c => c.Score, StringComparer.Ordinal);
        for (var axis = 0; axis < 14; axis++)
        {
            var changed = AgentPlanningFixture.Copy(baseline); changed.People[0].Personality[axis]++;
            var comparison = GameWorld.Restore(changed); comparison.AdvanceTo(1);
            var alternatives = AgentPlanningFixture.Actor(comparison).Decisions[^1].Candidates;
            AgentPlanningFixture.Check(alternatives.Count(c => scores.TryGetValue(c.Goal, out var before) && before != c.Score) >= 2,
                "A personality axis was defined but inactive in actual goal choice.");
        }
        var cheerful = AgentPlanningFixture.Copy(baseline); var cautious = AgentPlanningFixture.Copy(baseline);
        foreach (var candidate in new[] { cheerful, cautious })
        { candidate.People[0].Personality = new int[14]; WorldScenarioFixtures.SetCultivation(candidate, candidate.People[0], Realm.Qi); }
        cheerful.People[0].Mood = 100; cautious.People[0].Mood = -100;
        var high = GameWorld.Restore(cheerful); var low = GameWorld.Restore(cautious); high.AdvanceTo(1); low.AdvanceTo(1);
        AgentPlanningFixture.Check(AgentPlanningFixture.Actor(high).Action != AgentPlanningFixture.Actor(low).Action,
            "Real mood affected a label but never changed a chosen action.");

        var habitState = AgentPlanningFixture.Copy(cheerful); habitState.People[0].Mood = 0; habitState.People[0].Personality[1] = 16;
        var habitual = GameWorld.Restore(habitState); var actor = habitState.People[0].Id;
        AgentPlanningFixture.Accept(habitual, "habit-possession", "possess", actor);
        AgentPlanningFixture.Accept(habitual, "actual-habit-practice", "cultivate", actor);
        habitual.AdvanceTo(1); var after = habitual.Checkpoint();
        AgentPlanningFixture.Check(after.People[0].Habits.GetValueOrDefault("Cultivate") == 1, "The habit was not earned from an actual completed action.");
        after.Controlled = 0; var without = AgentPlanningFixture.Copy(after); without.People[0].Habits.Remove("Cultivate");
        var withHabit = GameWorld.Restore(after); var withoutHabit = GameWorld.Restore(without);
        withHabit.AdvanceTo(2); withoutHabit.AdvanceTo(2);
        AgentPlanningFixture.Check(AgentPlanningFixture.Actor(withHabit).Action != AgentPlanningFixture.Actor(withoutHabit).Action,
            "A completed-action habit did not affect a close actual choice.");
    }
}

public sealed class AgentPlanningCooperationScenario : IFeatureScenario
{
    public string Name => "INV-008/018 agent planning: cooperation repays an actual personal obligation without inventing another person's intent";
    public void Run()
    {
        var world = AgentPlanningFixture.Fresh(); var state = world.Checkpoint();
        var debtor = state.People[0]; var creditor = state.People[1];
        debtor.Personality = new int[14];
        foreach (var axis in new[] { 2, 4, 5, 9, 12 }) debtor.Personality[axis] = 100;
        creditor.Personality[4] = 100;
        world = GameWorld.Restore(state);
        AgentPlanningFixture.Accept(world, "real-agreed-loan", "loan", creditor.Id, debtor.Id, 10);
        var obligation = world.Checkpoint().Politics.Obligations.Single(o => o.Debtor.Id == debtor.Id);
        world.AdvanceTo(1); state = world.Checkpoint();
        AgentPlanningFixture.Check(state.Politics.Obligations.Single(o => o.Id == obligation.Id).Outstanding == 0 &&
            state.Politics.Payments.Any(p => p.Reference == obligation.Id && p.Amount == 10 && p.Kind == "debt repayment"),
            "Cooperation bypassed real debt settlement or used an imagined counterparty.");
        AgentPlanningFixture.Check(state.People.Count == 2 && state.Souls.Count == 2, "Cooperation minted social actors or population.");
    }
}

public sealed class AgentPlanningRetreatScenario : IFeatureScenario
{
    public string Name => "INV-006/008/021 agent planning: cached hostility chooses a known retreat and baseline founders are never forced into fatal conflict";
    public void Run()
    {
        var world = AgentPlanningFixture.Fresh(); var state = world.Checkpoint(); var actor = state.People[0]; var other = state.People[1];
        actor.Personality = new int[14]; actor.KnownPlaces.Add(1);
        actor.ObservedPlaces[1] = new(1, 100, 100, "", 0, BlockedUntil: 0);
        var encounter = new GameEvent(state.NextId++, 0, "전투", other.Id, actor.Id, "기능 검사: 이미 관측한 비치명 적대 사건", "명시된 개인 사건 증거", "person");
        state.Events.Add(encounter); actor.Journal.Add(encounter); other.Journal.Add(encounter);
        world = GameWorld.Restore(state); world.AdvanceTo(1);
        var selected = AgentPlanningFixture.Actor(world);
        AgentPlanningFixture.Check(selected.Action == ActionKind.Travel && selected.ActionTarget == 1 && selected.Goal == "위험 회피·후퇴",
            "Retreat used no known hostile event/route or silently chose attack.");
        var peaceful = AgentPlanningFixture.Fresh(); peaceful.AdvanceTo(2);
        AgentPlanningFixture.Check(peaceful.Checkpoint().People.All(p => p.Status == LifeStatus.Alive) &&
            !peaceful.Checkpoint().Events.Any(e => e.Kind == "전투"), "Ordinary founder goals created forced fatal conflict.");
    }
}
