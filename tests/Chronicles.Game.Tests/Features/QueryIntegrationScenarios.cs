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
        QueryFixture.Check(organization.PolicyEscrow == 0 && organization.PolicyDue is null && organization.Debt == 0 &&
            organization.PendingPolicies!.Count == 0, "A world with no real preparation acquired phantom policy finance.");
        QueryFixture.Check(view.LawHistory.Single().Key == "birth.enabled" && view.CombatAxisNames!.SequenceEqual(Chronicles.Game.Cultivation.CultivationQueries.AxisNames),
            "Actual history or canonical axis names were lost.");
        QueryFixture.Check(view.Species.Select(s => s.Id).SequenceEqual(Chronicles.Game.People.SpeciesCatalog.All.Select(s => s.Id)) &&
            view.Species.All(s => s.BasicLifespanMonths > s.MaturityMonths) && view.CurrencyExchange!.MoneyPerToken == state.Laws["currency.stoneValue"] &&
            view.ItemNames!["resource.02.0"] == Chronicles.Game.Inventory.InventoryDefinitions.Definition("resource.02.0").Item.Name && view.Economy!.Observer,
            "Public species/item/currency metadata or the actual economy scope was fabricated or omitted.");
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
        QueryFixture.Check(!personal.Economy!.Observer && personal.Economy.Regions.Count == 0 && personal.Economy.NaturalFlows.Count == 0 &&
            personal.Economy.Demand.All(d => d.Subject == a.Id && d.Sector == "person"), "Personal economy exposed observer-only stock/flow or private counterpart demand.");
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
        var own = GameWorld.Create(); var ownState = own.Checkpoint(); var seller = ownState.People[0].Id;
        var unappraised = ownState.Items.First(i => i.Owner == seller && i.Definition == "food");
        QueryFixture.Accepted(own, "possess", seller);
        QueryFixture.Check(own.View().Items.Single(i => i.Id == unappraised.Id).QualityValue is null, "Unappraised ownership exposed actual quality.");
        QueryFixture.Accepted(own, "offer", seller, unappraised.Id, 19, "sale");
        var ownOffer = own.View().Contracts.Single();
        QueryFixture.Check(ownOffer.ItemQuality == "미감정" && own.View().Items.Single(i => i.Id == unappraised.Id).QualityValue is null,
            "Advertising an unappraised owned item exposed its authority quality.");
        QueryFixture.Accepted(own, "withdraw", seller, ownOffer.Id);
        QueryFixture.Check(own.View().Items.Single(i => i.Id == unappraised.Id).QualityValue is null &&
            own.View().Contracts.Single().ItemQuality is null or "미감정", "Withdrawing an advertisement created appraisal knowledge.");

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

public sealed class PrivateCommandPerceptionRevisionScenario : IFeatureScenario
{
    public string Name => "INV-009/N03 rich query: real private NPC split preserves actor perception token and valid command context";
    public void Run()
    {
        var world = GameWorld.Create(); var initial = world.Checkpoint(); var a = initial.People[0].Id; var b = initial.People[1].Id;
        QueryFixture.Accepted(world, "possess", a);
        var before = world.View(a); var state = world.Checkpoint();
        var food = state.Items.First(i => i.Owner == b && i.Definition == "food");
        var privateChange = Chronicles.Game.Inventory.InventoryEngine.Apply(
            Chronicles.Game.Inventory.InventoryState.FromWorld(state, state.Inventory),
            new Chronicles.Game.Inventory.SplitInventoryItem("query.private-npc.split", b, food.Id, 1));
        QueryFixture.Check(privateChange.Accepted, privateChange.Message);
        state.Inventory = privateChange.State.ApplyTo(state);
        var changed = GameWorld.Restore(state); var after = changed.View(a);
        QueryFixture.Check(QueryFixture.Json(before) == QueryFixture.Json(after), "A real unobserved NPC inventory command changed the player's response or token.");
        var tool = changed.Checkpoint().Items.Single(i => i.Owner == a && i.Definition == "tool");
        var result = changed.Execute(new("query.valid-own-context", a, "equip", tool.Id, ExpectedRevision: before.Revision));
        QueryFixture.Check(result.Accepted, "The still-current personal context token was unusable after a private NPC command: " + result.Message);
        QueryFixture.Check(result.Revision == changed.View().Revision && changed.Checkpoint().People[0].Equipment.ContainsValue(tool.Id),
            "The own command result exposed a global revision or did not commit its real equipment change.");
    }
}

public sealed class PersonalOperationalContextScenario : IFeatureScenario
{
    public string Name => "INV-009/015 rich query: own combat social and AI context ignores unseen environment and reverse relations";
    public void Run()
    {
        var state = GameWorld.Create().Checkpoint(); var a = state.People[0].Id; var b = state.People[1].Id;
        state.Controlled = a;
        state.Relations.Add(new(a, b, 0, 0, 0, 0, 0, 0, 0, "창세의 실제 도려 관계"));
        state.Relations.Add(new(b, a, 40, 70, 0, 0, 0, 0, 0, "상대의 비공개 관계 상태"));
        var world = GameWorld.Restore(state); var before = world.View(a);
        QueryFixture.Check(before.Detail!.SocialRelations.Single().Observer == a && before.Detail.Decision is not null,
            "Own directional social/decision context was omitted.");
        var changed = world.Checkpoint();
        changed.Places[0].Qi = 0; changed.Places[0].Turbidity = 99; changed.Places[0].Owner = b;
        changed.CultivationSupply[0] = default;
        changed.Inventory.Effects.Add(new("query.private-effect", b, "defense", 7, 0, 1));
        changed.Relations[1] = changed.Relations[1] with { Trust = -90, Affection = -70, Grievance = 90 };
        changed.People[1].Mood = -80;
        changed.People[1].DecisionCandidates = [new("비공개 후보", 9999, "새로운 비공개 판단 근거")];
        var hidden = GameWorld.Restore(changed);
        QueryFixture.Check(QueryFixture.Json(before) == QueryFixture.Json(hidden.View(a)), "Fresh hidden site/NPC/other-direction facts affected own projected context.");
        QueryFixture.Check(hidden.View(b).Detail!.Decision is null && hidden.View(b).Detail!.SocialRelations.All(r => r.Observer == a),
            "NPC reasoning or reverse private emotion leaked through selected-person detail.");
        QueryFixture.Accepted(hidden, "observe");
        QueryFixture.Check(hidden.View(a).Detail!.Potential!.HasActualRecoverySupply == false &&
            hidden.View(a).Places.First(p => p.Id == 0).Qi == 0, "Observer lost the actual changed environment.");
    }
}

public sealed class PersonalSocialReceiptsProjectionScenario : IFeatureScenario
{
    public string Name => "INV-009/015 rich query: delivered social requests, consented assessment and observed oath receipts";
    public void Run()
    {
        var world = GameWorld.Create(42, new Dictionary<string, long> { ["birth.enabled"] = 0 });
        var opening = world.Checkpoint(); var first = opening.People[0].Id; var second = opening.People[1].Id;
        void Control(long id) => QueryFixture.Accepted(world, "possess", id);
        void Cancel(long id)
        {
            Control(id);
            if (world.Checkpoint().People.Single(p => p.Id == id).Action != ActionKind.Idle)
                QueryFixture.Accepted(world, "cancel", id);
        }
        Control(first); var empty = world.View();
        QueryFixture.Accepted(world, "requestAssessment", first, second, 21);
        var proposed = world.View(); var request = proposed.PeopleRequests.Single();
        QueryFixture.Check(proposed.Revision != empty.Revision && request.Proposer == first && request.Recipient == second &&
            request.Requested == 0 && request.Expires > 0 && request.Status == Chronicles.Game.People.PeopleSessionStatus.Proposed &&
            proposed.ReceivedClaims.Count == 0 && proposed.Detail is null && world.Checkpoint().People.All(p => p.Action == ActionKind.Idle),
            "A delivered request omitted its dated scoped token or fabricated consent, knowledge or another person's labor.");
        Control(second);
        var received = world.View(first);
        QueryFixture.Check(received.PeopleRequests.Single().Id == request.Id && received.Detail!.Decision is null && received.Detail.PeopleEcology is null,
            "Incoming requests required private counterpart state or exposed hidden detail.");
        QueryFixture.Accepted(world, "acceptPeopleRequest", second, request.Id);
        QueryFixture.Check(world.View().PeopleRequests.Single().Status == Chronicles.Game.People.PeopleSessionStatus.Active,
            "Actual accepted bilateral participation was not projected.");
        world.AdvanceTo(1);
        var assessed = world.View(second);
        QueryFixture.Check(assessed.PeopleRequests.Count == 0 && assessed.ReceivedClaims.Any(c => c.ObserverId == second && c.SubjectId == second &&
            c.ObservedMonth == 1 && c.ReceivedMonth == 1 && c.Provenance.Length > 0 && c.Confidence < 10000) && assessed.Detail!.People!.Innate is null,
            "A real assessment did not yield dated received evidence, or became innate appraisal.");
        var actual = world.StateHash(); var claim = assessed.ReceivedClaims[0];
        claim.Provenance[0] = claim.Provenance[0] with { EvidenceId = "detached-caller-only" };
        QueryFixture.Check(actual == world.StateHash() && world.View().ReceivedClaims.All(c => c.Provenance.All(h => h.EvidenceId != "detached-caller-only")),
            "A returned provenance array retained mutable authority.");
        Cancel(first); Cancel(second);
        QueryFixture.Accepted(world, "requestOath", second, first, data: "실제로 서로의 연구를 돕는다");
        var proposal = world.View().PeopleRequests.Single(); Control(first);
        QueryFixture.Accepted(world, "acceptPeopleRequest", first, proposal.Id); world.AdvanceTo(2);
        var known = world.View().KnownOaths.Single();
        QueryFixture.Check(known.Person == first && known.Other == second && known.Began == 2 && known.ObservedMonth == 2 && known.KnownActive,
            "Oath view was not the actual party's received formation snapshot.");
        QueryFixture.Accepted(world, "breakOath", first, known.Id);
        var ended = world.View().KnownOaths.Single();
        QueryFixture.Check(!ended.KnownActive && ended.ObservedMonth == 2 && ended.Terms == known.Terms,
            "Formal witnessed breach did not update the party's received oath record.");
        var before = world.StateHash(); world.View(first); world.View(second);
        QueryFixture.Check(before == world.StateHash(), "Social receipt queries changed authority.");
    }
}

public sealed class GuardTargetProjectionScenario : IFeatureScenario
{
    public string Name => "INV-009/015 rich query: voluntary guard names a person while combat cooldown names a receipt";
    public void Run()
    {
        var world = GameWorld.Create(); var initial = world.Checkpoint(); var first = initial.People[0].Id; var second = initial.People[1].Id;
        QueryFixture.Accepted(world, "possess", first);
        QueryFixture.Accepted(world, "guard", first, second);
        QueryFixture.Check(world.View(first).Detail!.Person.GuardTargetKind == "person" && world.View(first).Detail!.Person.ActionTarget == second,
            "Actual voluntary allied support did not classify its known person target.");
        QueryFixture.Accepted(world, "cancel", first);
        QueryFixture.Accepted(world, "attack", first, second);
        var self = world.View(first).Detail!.Person;
        QueryFixture.Check(self.Action == "Guard" && self.GuardTargetKind == "receipt" && self.ActionData!.StartsWith("combat:", StringComparison.Ordinal),
            "An actual combat receipt was exposed as a person target.");
        var other = world.View(second).Detail!.Person;
        QueryFixture.Check(other.GuardTargetKind is null && other.ActionTarget is null && other.ActionData is null,
            "Combat receipt classification exposed another person's unobserved action context.");
    }
}

public sealed class AuthoritativePolicyProjectionScenario : IFeatureScenario
{
    public string Name => "INV-007/015 policy query: public adopted drafts, funded real labor, cancellation and implementation replace legacy escrow";
    public void Run()
    {
        var world = GameWorld.Create(42, new Dictionary<string, long>
        { ["birth.enabled"] = 0, ["politics.salary"] = 0, ["ecology.disasterChance"] = 0, ["ecology.spoilage"] = 0 });
        var opening = world.Checkpoint(); var first = opening.People[0].Id; var second = opening.People[1].Id;
        QueryFixture.Accepted(world, "possess", first);
        QueryFixture.Accepted(world, "found", first, data: "sect"); world.AdvanceTo(1);
        var sect = world.Checkpoint().Groups.Single(g => g.Kind == GroupKind.Sect && g.Leader == first).Id;
        QueryFixture.Accepted(world, "loan", first, sect, 40, JsonSerializer.Serialize(new { basis = "실제 정책 준비 예산의 한정 대여" }));
        var treasury = world.Checkpoint().Groups.Single(g => g.Id == sect).Money;
        QueryFixture.Accepted(world, "adoptPolicy", first, sect, 2, "경제·분배");
        var adopted = world.View().Groups.Single(g => g.Id == sect); var draft = adopted.PolicyPreparations!.Single();
        QueryFixture.Check(draft.Stage == "Adopted" && adopted.PendingPolicies!["경제·분배"] == 2 && adopted.PolicyDue is null &&
            adopted.PolicyEscrow == 0 && adopted.Money == treasury && adopted.Debt == 40,
            "An unprepared actual draft became paid work, or its real separate-account loan was omitted.");
        QueryFixture.Accepted(world, "preparePolicy", first, draft.Id);
        var preparedState = world.Checkpoint(); var prepared = world.View(first).Groups.Single(g => g.Id == sect);
        var actual = preparedState.Politics.Preparations.Single(p => p.Id == draft.Id);
        QueryFixture.Check(actual.Stage == Chronicles.Game.Politics.PolicyStage.Preparing && prepared.PolicyEscrow == actual.Escrow &&
            actual.Escrow == preparedState.Laws["politics.policyCost"] && prepared.PolicyDue == actual.Due && prepared.Money == treasury - actual.Escrow &&
            prepared.PolicyPreparations!.Single().Workers.SequenceEqual(actual.Workers) && preparedState.Politics.Labor.Any(l => l.Reference == actual.Id && l.Person == first) &&
            world.View(first).Detail!.Person.GuardTargetKind == "organization",
            "The projection did not follow the single real budget/labor reservation or misidentified political work as allied guard.");
        QueryFixture.Accepted(world, "adoptPolicy", first, sect, 1, "교육·전승");
        var unfundedLabor = world.Checkpoint().Politics.Preparations.Single(p => p.Group == sect && p.Category == "교육·전승");
        QueryFixture.Accepted(world, "preparePolicy", first, unfundedLabor.Id);
        var busy = world.View().Groups.Single(g => g.Id == sect);
        QueryFixture.Check(busy.PolicyPreparations!.Single(p => p.Id == unfundedLabor.Id).Stage == "InsufficientLabor" &&
            busy.PendingPolicies!.Count == 2 && busy.PolicyEscrow == actual.Escrow && busy.PolicyDue == actual.Due,
            "Real unavailable labor fabricated a second escrow/deadline or hid the adopted choice.");
        QueryFixture.Accepted(world, "cancelPolicy", first, unfundedLabor.Id);
        QueryFixture.Check(world.View().Groups.Single(g => g.Id == sect).PendingPolicies!.Count == 1,
            "Cancelled policy history remained a pending choice.");
        world = GameCodec.Decode(GameCodec.Encode(world)); world.AdvanceTo(2);
        var implemented = world.View().Groups.Single(g => g.Id == sect);
        QueryFixture.Check(implemented.Policies["경제·분배"] == 2 && implemented.PendingPolicies!.Count == 0 && implemented.PolicyDue is null &&
            implemented.PolicyEscrow == 0 && implemented.Debt == 40 && implemented.PolicyHistory!.Any(h => h.Stage == Chronicles.Game.Politics.PolicyStage.Implemented) &&
            implemented.PolicyPreparations!.Any(p => p.Stage == "Cancelled"), "Settled/cancelled actual history was confused with active preparation finance.");
        QueryFixture.Check(world.Checkpoint().Groups.All(g => g.PendingPolicies.Count == 0 && g.PolicyDue == 0 && g.PolicyEscrow == 0),
            "A public policy flow wrote the retired duplicate preparation authority.");
        QueryFixture.Accepted(world, "possess", second);
        var other = world.View().Groups.Single(g => g.Kind == "Family");
        QueryFixture.Check(other.PendingPolicies is null && other.PolicyEscrow is null && other.PolicyDue is null && other.Debt is null &&
            other.PolicyPreparations is null && other.PolicyHistory is null && !world.View().Groups.Any(g => g.Id == sect),
            "Nonleader/member scope revealed a private preparation or another organization's finance.");
        var hash = world.StateHash(); world.View(); world.View(first);
        QueryFixture.Check(hash == world.StateHash(), "Policy projection mutated authority.");
    }
}
