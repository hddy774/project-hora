using System.Text.Json;
using Chronicles.Game.Knowledge;

namespace Chronicles.Game.Tests.Features;

internal static class QueryFixture
{
    internal static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    internal static string Json(object? value) => JsonSerializer.Serialize(value);
    internal static void Accepted(GameWorld world, string kind, long actor = 0, long target = 0, long value = 0, string data = "")
    {
        var result = world.Execute(new("query." + Guid.NewGuid().ToString("N"), actor, kind, target, value, data));
        Check(result.Accepted, result.Message);
    }
    internal static void Study(KnowledgeAuthority authority, long actor, long item, long month, string source)
    {
        var participant = new KnowledgeParticipant(actor, 0, true);
        var start = authority.StartStudy(new(source, participant, item, [1], month, 1));
        Check(start.Applied, start.Message);
        var done = authority.Complete(new(source + ".done", start.Id, month + 1, [participant]));
        Check(done.Applied, done.Message);
    }
}

public sealed class RichQueryPermissionScenario : IFeatureScenario
{
    public string Name => "INV-009/010 rich query: cached people and places precede hidden truth projection";
    public void Run()
    {
        var world = GameWorld.Create();
        var initial = world.Checkpoint(); var a = initial.People[0].Id; var b = initial.People[1].Id;
        QueryFixture.Accepted(world, "possess", a);
        var before = QueryFixture.Json(world.View(b));
        var changed = world.Checkpoint(); var npc = changed.People[1];
        npc.Health = 43; npc.Roots[0] = 12; npc.Work = 9876; npc.GreatPerfection = false;
        npc.Action = ActionKind.Wait; npc.Due = 17; npc.ActionTarget = a; npc.ActionData = "hidden action";
        npc.ActionRevision = 67; npc.Plan = [new("cultivate", 0, "hidden path")]; npc.PlanExpansions = 23; npc.Mood = -50; npc.Habits["Cultivate"] = 99;
        npc.DecisionCandidates = [new("비공개 계획", 9999, "비공개 근거")];
        npc.Decisions = [new(0, "비공개 판단", "실제 숨은 생각", "Wait", 2, npc.DecisionCandidates.ToArray())];
        var profile = changed.PeopleDomain.Profiles.Single(p => p.PersonId == b);
        profile.Innate.ElementRoots[0] = 12;
        var hiddenItem = changed.Items.First(i => i.Owner == b); hiddenItem.Damage = 42;
        hiddenItem.Quality = 17; hiddenItem.Defects = ItemDefect.Contamination; hiddenItem.BoundSoul = npc.SoulId;
        changed.Places[0].Source /= 2; changed.Places[0].Qi /= 2;
        changed.Places[0].Owner = b; changed.Places[0].Operator = b; changed.Places[0].Delegate = 0; changed.Places[0].BlockedUntil = 20;
        var restored = GameWorld.Restore(changed);
        QueryFixture.Check(before == QueryFixture.Json(restored.View(b)), "Hidden NPC/current-place changes affected the cached play response.");
        var other = restored.View(b).Detail!;
        QueryFixture.Check(other.Person.Due is null && other.Person.Plan is null && other.Person.Body is null &&
            other.Person.GreatPerfection is null && other.Person.Partner is null && other.Person.Guardian is null &&
            other.Person.Household is null && other.Person.Sect is null && other.Person.ActionTarget is null &&
            other.Person.ActionData is null && other.Person.ActionRevision is null && other.Person.Irreversible is null,
            "Hidden NPC rich fields were not unknown.");
        QueryFixture.Check(other.Knowledge is null && other.Decision is null && restored.View().LawHistory.Count == 0,
            "Personal or administrative detail leaked to play.");
        QueryFixture.Check(restored.View().Places.First(p => p.Id == 0).BlockedUntil == initial.People[0].ObservedPlaces[0].BlockedUntil,
            "Blocked passage was refreshed from hidden current truth.");
        var hash = restored.StateHash(); var random = restored.Checkpoint().RandomState;
        restored.View(a); restored.View(b); restored.View(long.MaxValue);
        QueryFixture.Check(hash == restored.StateHash() && random == restored.Checkpoint().RandomState, "Queries changed state or RNG.");
    }
}

public sealed class RichQueryMappingScenario : IFeatureScenario
{
    public string Name => "INV-007/010 rich query: action equipment policy history mapping and detached snapshots";
    public void Run()
    {
        var world = GameWorld.Create(); QueryFixture.Accepted(world, "law", value: 0, data: "birth.enabled");
        var state = world.Checkpoint();
        var a = state.People[0]; var b = state.People[1]; var home = state.Groups[0];
        a.Action = ActionKind.Wait; a.Due = 9; a.ActionTarget = b.Id; a.ActionData = "entry"; a.ActionRevision = state.LawRevision;
        a.Plan = [new("gather", 0, "ore")]; a.Guardian = b.Id;
        var tool = state.Items.Single(i => i.Owner == a.Id && i.Definition == "tool"); a.Equipment[8] = tool.Id;
        tool.Damage = 25; tool.Defects = ItemDefect.Contamination; tool.BoundSoul = a.SoulId;
        home.Money = 60; home.PolicyEscrow = 10; home.PolicyDue = 3; home.Debt = 40; state.CreatedMoney += 70;
        home.PendingPolicies[Catalog.PolicyNames[0]] = 2;
        state.Places[0].Owner = a.Id; state.Places[0].Operator = a.Id; home.Kind = GroupKind.Dynasty; home.Provinces = [0];
        foreach (var site in state.Places.Where(p => p.Province == 0)) site.Sovereign = home.Id;
        state.Places[0].Delegate = home.Id; state.Places[0].BlockedUntil = 4;
        a.ObservedPlaces[0] = new(0, 77, 88, "실제 관측 시설", 0, home.Id, b.Id, b.Id, home.Id, 2);
        world = GameWorld.Restore(state);
        var hash = world.StateHash(); var view = world.View(a.Id); var person = view.Detail!.Person;
        QueryFixture.Check(person.Due == 9 && person.ActionTarget == b.Id && person.ActionData == "entry" &&
            person.ActionRevision == state.LawRevision && person.Plan!.Single().Data == "ore" && person.Body == a.BodyId &&
            person.Guardian == b.Id && person.Partner == b.Id && person.GreatPerfection == a.GreatPerfection,
            "Observer person fields differ from their real sources.");
        var gear = view.Items.Single(i => i.Id == tool.Id);
        QueryFixture.Check(gear.EquippedSlot == 8 && gear.Place == -1 && gear.Defects == ItemDefect.Contamination &&
            gear.BoundSoul == a.SoulId && gear.Damage == 25, "Equipment fields were fabricated or omitted.");
        var organization = view.Groups.Single(g => g.Id == home.Id);
        QueryFixture.Check(organization.PolicyEscrow == 10 && organization.PolicyDue == 3 && organization.Debt == 40 &&
            organization.PendingPolicies![Catalog.PolicyNames[0]] == 2, "Pending policy/finance mapping failed.");
        QueryFixture.Check(view.LawHistory.Single().Key == "birth.enabled" && view.CombatAxisNames!.SequenceEqual(Chronicles.Game.Cultivation.CultivationQueries.AxisNames),
            "Actual history or canonical axis names were lost.");
        QueryFixture.Accepted(world, "possess", a.Id);
        var personal = world.View(a.Id); var remembered = personal.Places.Single(p => p.Id == 0);
        QueryFixture.Check(remembered.Source == 77 && remembered.Qi == 88 && remembered.Owner == b.Id &&
            remembered.Operator == b.Id && remembered.Delegate == home.Id && remembered.BlockedUntil == 2,
            "Place projection used current truth instead of the actual observation snapshot.");
        QueryFixture.Check(personal.Detail!.Roots is null && personal.Detail.Aptitudes is null && personal.Detail.Talents is null &&
            personal.Detail.People!.CurrentTalents.Length == 24 && personal.Detail.People.Innate is null &&
            personal.Detail.Cultivation!.Roots.All(r => r.InnateQuality is null) &&
            personal.Detail.Cultivation.Formation!.Quality is null && personal.Detail.Cultivation.Formation.Factors is null &&
            personal.Detail.Cultivation.Formation.Knowledge?.Error is null && personal.Detail.Potential!.HasActualRecoverySupply is null &&
            personal.Detail.Potential.KnownNextRoutes.All(r => r.ParentQualityCarried is null && r.Missing is null),
            "Founder initialization became automatic innate/latent or external-context appraisal.");
        QueryFixture.Check(personal.Detail.Combat!.SequenceEqual(Enumerable.Range(0,8).Select(personal.Detail.CombatProjection!.Axes.At)),
            "Legacy combat values were relabeled as canonical axes.");
        var ownGear = personal.Items.Single(i => i.Id == tool.Id);
        QueryFixture.Check(ownGear.EquippedSlot == 8 && ownGear.Damage is null && ownGear.Defects is null && ownGear.BoundSoul is null,
            "Physical ownership became automatic appraisal.");
        // Returned dictionaries and lists are detached from authority even if the caller attempts mutation.
        try { ((IDictionary<string,int>)organization.Policies)[Catalog.PolicyNames[0]] = 0; } catch (NotSupportedException) { }
        try { ((IList<PlanStep>)person.Plan!)[0] = new("heal", 0, ""); } catch (NotSupportedException) { }
        QueryFixture.Check(view.Detail.Decision!.Plan[0].Data == "ore" && world.Checkpoint().People[0].Plan[0].Data == "ore",
            "A projection retained mutable authority lists.");
        QueryFixture.Check(hash != world.StateHash(), "Possession command did not advance authority.");
        var current = world.StateHash();
        GameCodec.Decode(GameCodec.Encode(world)).View(a.Id);
        QueryFixture.Check(current == world.StateHash(), "Save/query round trip changed authority.");
    }
}

public sealed class PersonalKnowledgeProjectionScenario : IFeatureScenario
{
    public string Name => "INV-009/024 rich query: knowledge provenance is personal and hidden revision independent";
    public void Run()
    {
        var world = GameWorld.Create(); var state = world.Checkpoint(); var a = state.People[0].Id; var b = state.People[1].Id;
        var itemOne = state.Items.First(i => i.Owner == a && i.Definition.StartsWith("path.", StringComparison.Ordinal)).Id;
        var itemTwo = state.Items.First(i => i.Owner == b && i.Definition.StartsWith("path.", StringComparison.Ordinal)).Id;
        var knowledge = KnowledgeAuthority.Restore(state.Knowledge);
        var work = state.Items.Single(i => i.Id == itemOne).Definition;
        var beforeUnderstanding = knowledge.ViewPersonal(a, limit: 128).Rows.Single(r => r.WorkId == work && r.Chapter == 1).Comprehension;
        var medium = state.Knowledge.Media.Single(m => m.ItemId == itemOne);
        var actualClaim = state.Knowledge.Editions.Single(e => e.Id == medium.EditionId).Chapters.Single(c => c.Number == 1).Claim;
        QueryFixture.Study(knowledge, a, itemOne, 0, "query.study.actor");
        state.Month = 2; state.Knowledge = knowledge.Checkpoint(); state.Controlled = a;
        world = GameWorld.Restore(state); var before = QueryFixture.Json(world.View(a));
        var projected = world.View(a).Detail!.Knowledge!;
        QueryFixture.Check(projected.Chapters.Single(c => c.WorkId == work).Claim == actualClaim && projected.Chapters.Single(c => c.WorkId == work).Comprehension > beforeUnderstanding &&
            projected.Chapters.Single(c => c.WorkId == work).Mastery == 0 && projected.History.Last(h => h.WorkId == work).SourceId.Length > 0 && projected.Editions is null,
            "Learned claims/provenance were lost, practice was invented, or unread editions were exposed.");
        QueryFixture.Check(projected.Media.All(m => state.Items.Single(i => i.Id == m.ItemId).Owner == a && m.Quality is null && m.ReadableChapterCount is null) && projected.Activities.Single().Actor == a,
            "Another person's accessible books or activity leaked.");
        var changed = world.Checkpoint();
        QueryFixture.Study(knowledge, b, itemTwo, 1, "query.study.hidden");
        changed.Knowledge = knowledge.Checkpoint();
        var hidden = GameWorld.Restore(changed);
        QueryFixture.Check(before == QueryFixture.Json(hidden.View(a)), "Hidden knowledge activity/global module revision changed the actor projection.");
        QueryFixture.Check(hidden.View(b).Detail!.Knowledge is null, "Known NPC personal memories were exposed.");
        var hash = hidden.StateHash(); hidden.View(a); hidden.View(b);
        QueryFixture.Check(hash == hidden.StateHash(), "Knowledge projection initialized or changed authority.");
        QueryFixture.Accepted(hidden, "observe");
        var observer = hidden.View(b).Detail!.Knowledge!;
        QueryFixture.Check(observer.Chapters.Single(c => c.WorkId == work).Claim == actualClaim && observer.Editions!.Single(e => e.Id == medium.EditionId).Chapters.Single(c => c.Number == 1).Claim == actualClaim,
            "Observer actual knowledge/edition detail was omitted.");
    }
}

public sealed class ResourceAndAppraisalProjectionScenario : IFeatureScenario
{
    public string Name => "INV-009/016 rich query: actual recipes and stale personal resource/appraisal snapshots";
    public void Run()
    {
        var state = GameWorld.Create().Checkpoint(); var a = state.People[0].Id; var b = state.People[1].Id;
        state.Month = 3; state.Controlled = a;
        var knownId = state.NextId++; var hiddenId = state.NextId++;
        state.Inventory.Sources.Add(new() { Id = knownId, Place = 0, Definition = "resource.15.0", Activity = Chronicles.Game.Inventory.InventoryDefinitions.ExtractionActivity("resource.15.0")!,
            Remaining = 50, Capacity = 100, Regeneration = 2, LastMonth = 3, Origin = "검증 원천" });
        state.Inventory.Sources.Add(new() { Id = hiddenId, Place = 0, Definition = "resource.03.3", Activity = Chronicles.Game.Inventory.InventoryDefinitions.ExtractionActivity("resource.03.3")!,
            Remaining = 70, Capacity = 100, Regeneration = 1, LastMonth = 3, Origin = "미발견 원천" });
        state.Inventory.Observations.Add(new(a, knownId, 0, 17, 1, "실제 원천 답사", "resource.15.0", Chronicles.Game.Inventory.InventoryDefinitions.ExtractionActivity("resource.15.0")!));
        state.Inventory.Observations.Add(new(b, hiddenId, 0, 70, 2, "다른 인물의 답사", "resource.03.3", Chronicles.Game.Inventory.InventoryDefinitions.ExtractionActivity("resource.03.3")!));
        var tool = state.Items.First(i => i.Owner == a && i.Definition == "tool");
        state.Inventory.Appraisals.Add(new(a, tool.Id, tool.Definition, 4200, 3, ItemDefect.None, 1));
        tool.Damage = 44; tool.Quality = 7000; tool.Defects = ItemDefect.LostCore;
        var world = GameWorld.Restore(state); var view = world.View(a); var before = QueryFixture.Json(view);
        var source = view.ResourceSources.Single();
        QueryFixture.Check(source.Id == knownId && source.Remaining == 17 && source.Observed == 1 &&
            source.Capacity is null && source.Regeneration is null && source.Origin is null,
            "Resource projection inspected current or another person's source knowledge.");
        var appraised = view.Items.Single(i => i.Id == tool.Id);
        QueryFixture.Check(appraised.QualityValue == 4200 && appraised.Damage == 3 && appraised.Defects == ItemDefect.None &&
            appraised.AppraisedMonth == 1, "Appraisal facts were silently refreshed from current item truth.");
        var bag = view.Recipes.Single(r => r.Id == "bag");
        QueryFixture.Check(view.Recipes.Count == Chronicles.Game.Inventory.InventoryDefinitions.Recipes.Count &&
            bag.Inputs.Any(i => i.Definition == "resource.15.0" && i.Quantity == 1 && i.Name.Length > 0),
            "Recipe projection lost the real additional bag material or its human name.");
        var changed = world.Checkpoint(); changed.Inventory.Sources.Single(s => s.Id == knownId).Remaining = 2; changed.Inventory.Sources.Single(s => s.Id == knownId).Capacity = 200;
        changed.Inventory.Sources.Single(s => s.Id == knownId).Regeneration = 7; changed.Inventory.Sources.Single(s => s.Id == hiddenId).Remaining = 1;
        changed.Items.Single(i => i.Id == tool.Id).Damage = 90;
        QueryFixture.Check(before == QueryFixture.Json(GameWorld.Restore(changed).View(a)), "Unobserved stock or item changes affected cached personal facts.");
        QueryFixture.Accepted(world, "observe");
        QueryFixture.Check(world.View().ResourceSources.Any(s => s.Id == hiddenId) && world.View().ResourceSources.Single(s => s.Id == knownId).Remaining == 50,
            "Observer resource truth was replaced by personal cache.");
    }
}

public sealed class ContractPrivacyProjectionScenario : IFeatureScenario
{
    public string Name => "INV-009/017 rich query: public offers reveal goods and closed contracts hide later buyer inventory";
    public void Run()
    {
        var world = GameWorld.Create(); var state = world.Checkpoint(); var a = state.People[0].Id; var b = state.People[1].Id;
        var food = state.Items.First(i => i.Owner == b && i.Definition == "food");
        QueryFixture.Accepted(world, "offer", b, food.Id, 17, "sale");
        var contract = world.Checkpoint().Contracts.Single();
        QueryFixture.Accepted(world, "possess", a);
        var listing = world.View().Contracts.Single(c => c.Id == contract.Id);
        QueryFixture.Check(listing.ItemName == "영곡" && listing.ItemQuantity == food.Quantity && listing.ItemQuality == "미감정" && listing.Escrow is null,
            "A real public offer omitted its goods or exposed hidden appraisal/private finance.");
        QueryFixture.Accepted(world, "trade", a, contract.Id);
        QueryFixture.Accepted(world, "observe"); QueryFixture.Accepted(world, "possess", b);
        var before = QueryFixture.Json(world.View()); var changed = world.Checkpoint();
        var sold = changed.Items.Single(i => i.Id == food.Id); sold.Quantity--;
        changed.Materials[food.Definition].Consumption++;
        QueryFixture.Check(before == QueryFixture.Json(GameWorld.Restore(changed).View()), "Closed sale read a buyer's later private inventory.");
        QueryFixture.Check(world.View().Contracts.Single(c => c.Id == contract.Id).ItemQuantity == food.Quantity,
            "Immutable advertised quantity was lost after the sale closed.");
    }
}
