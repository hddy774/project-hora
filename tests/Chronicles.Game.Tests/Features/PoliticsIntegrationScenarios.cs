using System.Text.Json;
using System.Reflection;
using Chronicles.Game.Knowledge;
using Chronicles.Game.Politics;

namespace Chronicles.Game.Tests;

internal sealed class PoliticsWorldFixture
{
    private int commandNumber;
    public GameWorld World { get; set; }
    public long First { get; }
    public long Second { get; }
    public long Home => World.Checkpoint().People.Single(p => p.Id == First).Household;
    public PoliticsWorldFixture()
    {
        World = GameWorld.Create(42, new Dictionary<string, long>
        {
            ["birth.enabled"] = 0, ["ecology.disasterChance"] = 0, ["ecology.spoilage"] = 0, ["politics.salary"] = 0
        });
        var initial = World.Checkpoint(); First = initial.People[0].Id; Second = initial.People[1].Id;
        Update(s =>
        {
            s.People.Single(p => p.Id == Second).Action = ActionKind.Wait;
            s.People.Single(p => p.Id == Second).Due = 10000;
            foreach (var p in s.People) p.Personality[4] = 60;
        });
        Send("possess", First);
    }
    public void Update(Action<GameState> configure)
    {
        var state = World.Checkpoint(); configure(state); World = GameWorld.Restore(state);
    }
    public GameCommand Command(string kind, long actor, long target = 0, long value = 0, string data = "") =>
        new($"politics-integration:{++commandNumber}", actor, kind, target, value, data);
    public void Send(string kind, long actor, long target = 0, long value = 0, string data = "") => Send(Command(kind, actor, target, value, data));
    public void Send(GameCommand command)
    {
        var result = World.Execute(command); Check(result.Accepted, command.Kind + ": " + result.Message);
    }
    public void Reject(GameCommand command)
    {
        var before = World.StateHash(); var result = World.Execute(command);
        Check(!result.Accepted && World.StateHash() == before, "rejected public command must preserve state: " + command.Kind);
    }
    public long FoundSect()
    {
        Send("found", First, data: "sect"); World.AdvanceTo(World.Month + 1);
        var snapshot = World.Checkpoint();
        Check(snapshot.Politics.Labor.All(r => r.Purpose != "founding"), "monthly hook releases actual founding work");
        return snapshot.Groups.Single(g => g.Kind == GroupKind.Sect && !g.Dissolved).Id;
    }
    public static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}

public sealed class PoliticsPublicPolicyIntegrationScenario : IFeatureScenario
{
    public string Name => "V021-G03 public politics: real dispatch, preparation escrow, busy action rejection, save and one-time monthly implementation";
    public void Run()
    {
        var f = new PoliticsWorldFixture(); var sect = f.FoundSect();
        var opening = f.World.Checkpoint(); var command = f.Command("policy", f.First, sect, 2, "경제·분배");
        f.Send(command); var pending = f.World.Checkpoint();
        PoliticsWorldFixture.Check(pending.Politics.Preparations.Single().Stage == PolicyStage.Preparing &&
            pending.Politics.Preparations.Single().Escrow == pending.Laws["politics.policyCost"] &&
            pending.Groups.Single(g => g.Id == sect).PolicyEscrow == 0, "public policy uses the authoritative domain escrow once");
        foreach (var retiredField in new[] { "escrow", "due", "pending" })
        {
            var invalid = f.World.Checkpoint(); var group = invalid.Groups.Single(g => g.Id == sect);
            if (retiredField == "escrow") { group.PolicyEscrow = 1; group.Money--; }
            else if (retiredField == "due") group.PolicyDue = invalid.Month + 1;
            else group.PendingPolicies["경제·분배"] = 1;
            var rejected = false;
            try { GameWorld.Restore(invalid); }
            catch (InvalidDataException error) { rejected = error.Message.StartsWith("INV-020:", StringComparison.Ordinal); }
            PoliticsWorldFixture.Check(rejected && f.World.StateHash() == GameCodec.Decode(GameCodec.Encode(f.World)).StateHash(),
                "retired policy data cannot restore as a second authority, including balanced ghost escrow; the valid source remains saveable");
        }
        var beforeReplay = f.World.StateHash(); f.Send(command);
        PoliticsWorldFixture.Check(f.World.StateHash() == beforeReplay, "repeated command cannot reserve again");
        f.Reject(f.Command("gather", f.First, data: "food"));
        Invariants.Validate(pending);
        f.World = GameCodec.Decode(GameCodec.Encode(f.World));
        f.World.AdvanceTo(2); var settled = f.World.Checkpoint();
        PoliticsWorldFixture.Check(settled.Politics.Preparations.Single().Stage == PolicyStage.Implemented &&
            settled.Groups.Single(g => g.Id == sect).Policies["경제·분배"] == 2 &&
            settled.ConsumedMoney - opening.ConsumedMoney == pending.Laws["politics.policyCost"] &&
            settled.Politics.Preparations.Sum(p => p.Escrow) == 0 && settled.People.Single(p => p.Id == f.First).Action == ActionKind.Idle,
            "monthly implementation consumes one escrow and preserves the controlled person's current object/action");
        var restored = GameCodec.Decode(GameCodec.Encode(f.World));
        f.World.AdvanceTo(3); restored.AdvanceTo(3);
        PoliticsWorldFixture.Check(f.World.StateHash() == restored.StateHash(), "public policy save resumes the same world");
    }
}

public sealed class PoliticsPublicWithdrawalIntegrationScenario : IFeatureScenario
{
    public string Name => "V021-G03 public politics: withdrawing real reserved work refunds unfinished policy without implementation";
    public void Run()
    {
        var f = new PoliticsWorldFixture(); var sect = f.FoundSect();
        var opening = f.World.Checkpoint().Groups.Single(g => g.Id == sect).Money;
        f.Send("policy", f.First, sect, 2, "수행·자원");
        PoliticsWorldFixture.Check(f.World.Checkpoint().Politics.Labor.Any(r => r.Person == f.First && r.Purpose == "policy"), "actual worker reserved");
        f.Send("cancel", f.First); var cancelled = f.World.Checkpoint();
        PoliticsWorldFixture.Check(cancelled.Politics.Preparations.Single().Stage == PolicyStage.Cancelled &&
            cancelled.Politics.Labor.Count == 0 && cancelled.Groups.Single(g => g.Id == sect).Money == opening &&
            cancelled.Politics.Preparations.Single().Escrow == 0, "worker withdrawal atomically releases budget and labor");
        f.Send("gather", f.First, data: "food"); f.World.AdvanceTo(2);
        PoliticsWorldFixture.Check(f.World.Checkpoint().Groups.Single(g => g.Id == sect).Policies["수행·자원"] == 1, "cancelled policy never implements later");
        Invariants.Validate(f.World.Checkpoint());
    }
}

public sealed class PoliticsPublicTerritoryIntegrationScenario : IFeatureScenario
{
    public string Name => "V021-G01/G02 public politics: one province, persistent territoryless dynasty, annual zero revenue and restoration";
    public void Run()
    {
        var f = new PoliticsWorldFixture();
        f.Update(s => s.People.Single(p => p.Id == f.First).KnownPlaces = Enumerable.Range(0, 10).ToList());
        var home = f.Home; f.Send("govern", f.First, home, 0);
        PoliticsWorldFixture.Check(f.World.Checkpoint().Groups.Single(g => g.Id == home).Kind == GroupKind.Dynasty, "actual one-province dynasty");
        f.Send("releaseProvince", f.First, home, 0); f.World.AdvanceTo(12);
        var territoryless = f.World.Checkpoint();
        PoliticsWorldFixture.Check(territoryless.Groups.Single(g => g.Id == home).Provinces.Count == 0 &&
            territoryless.Groups.Single(g => g.Id == home).Kind == GroupKind.Dynasty && territoryless.Politics.Claims.Count == 1 &&
            territoryless.Politics.TaxPeriods.Contains(12) && !territoryless.Politics.Payments.Any(p => p.Kind is "territorial tax" or "vassal tribute") &&
            territoryless.People.Count == 2 && territoryless.Groups.All(g => g.Kind != GroupKind.Sect), "no hidden taxpayers, ghost revenue or loss of historical dynasty identity");
        f.World = GameCodec.Decode(GameCodec.Encode(f.World)); f.Send("govern", f.First, home, 0);
        PoliticsWorldFixture.Check(f.World.Checkpoint().Politics.Claims.Count == 1 && f.World.Checkpoint().Places.Take(10).All(p => p.Sovereign == home), "restoration requires and applies actual sovereignty");
    }
}

public sealed class PoliticsPublicDebtSuccessionIntegrationScenario : IFeatureScenario
{
    public string Name => "V021-G02 public politics: actual loan, independence, death succession and territorial loss keep public accounts distinct";
    public void Run()
    {
        var f = new PoliticsWorldFixture(); var home = f.Home; long other = 0;
        f.Update(s =>
        {
            other = s.NextId++;
            var second = s.People.Single(p => p.Id == f.Second); second.Money -= 100; second.Household = other;
            s.People.Single(p => p.Id == f.First).Money -= 100;
            var group = s.Groups.Single(g => g.Id == home); group.Kind = GroupKind.Dynasty; group.Money = 100; group.Provinces = [0];
            s.Groups.Add(new Group { Id = other, Name = "실제 봉신 가계", Kind = GroupKind.Family, Leader = f.Second, Members = [f.Second], Money = 100 });
            foreach (var place in s.Places.Take(10)) place.Sovereign = home;
        });
        f.Send("observe", 0); f.Send("vassal", f.Second, other, home); f.Send("possess", f.First);
        f.Send("loan", f.First, other, 50, JsonSerializer.Serialize(new
        { Creditor = PoliticalAccount.Group(home), Enforcer = home, Province = 0, Basis = "실제 공금 대여" }));
        var original = f.World.Checkpoint().Politics.Obligations.Single();
        f.Send("observe", 0); f.Send("possess", f.Second); f.Send("vassal", f.Second, other, 0);
        PoliticsWorldFixture.Check(f.World.Checkpoint().Politics.Obligations.Single() == original, "independence does not transfer or duplicate principal");
        f.Send("repayDebt", f.Second, original.Id, 25);
        f.Update(s => s.People.Single(p => p.Id == f.First).Health = 1);
        if (f.World.Checkpoint().People.Single(p => p.Id == f.Second).Action != ActionKind.Idle) f.Send("cancel", f.Second);
        f.Send("attack", f.Second, f.First); var succeeded = f.World.Checkpoint();
        var debt = succeeded.Politics.Obligations.Single();
        PoliticsWorldFixture.Check(succeeded.People.Single(p => p.Id == f.First).Status == LifeStatus.Dead &&
            succeeded.Groups.Single(g => g.Id == home).Leader == f.Second &&
            debt.Id == original.Id && debt.Creditor == original.Creditor && debt.Debtor == original.Debtor && debt.Enforcer == original.Enforcer && debt.Outstanding == 25 &&
            succeeded.Groups.Single(g => g.Id == home).Money == 75 && succeeded.Groups.Single(g => g.Id == other).Money == 125,
            "actual succession preserves separate public treasuries and one remaining receivable");
        f.Send("releaseProvince", f.Second, home, 0);
        var lost = f.World.Checkpoint();
        var context = new PoliticalContext(lost.Month, lost.Revision, lost.LawRevision, lost.People, lost.Groups, lost.Places, new());
        PoliticsWorldFixture.Check(!PoliticsEngine.CanEnforce(context, lost.Politics.Obligations.Single()) && lost.Politics.Obligations.Single() == debt,
            "territorial loss changes actual enforcement capacity, not ownership");
        Invariants.Validate(GameCodec.Decode(GameCodec.Encode(f.World)).Checkpoint());
    }
}

public sealed class PoliticsPublicTeachingIntegrationScenario : IFeatureScenario
{
    public string Name => "V021-S02 public politics: funded education queues one real request and gains knowledge only through timed teaching";
    public void Run()
    {
        var f = new PoliticsWorldFixture(); var sect = f.FoundSect();
        f.Send("observe", 0); f.Send("create", 0, 0, data: "명시 편입 제자");
        var pupil = f.World.Checkpoint().People.Single(p => p.Id != f.First && p.Id != f.Second).Id;
        f.Send("possess", f.First); f.World.AdvanceTo(2);
        f.Send("observe", 0); f.Send("join", pupil, sect);
        if (f.World.Checkpoint().People.Single(p => p.Id == pupil).Action != ActionKind.Idle) f.Send("cancel", pupil);
        f.Send("possess", f.First);
        f.Send("policyActivity", f.First, sect, pupil, JsonSerializer.Serialize(new
        { Category = "교육·전승", Counterparty = f.First, Knowledge = "entry", Place = 0 }));
        var funded = f.World.Checkpoint();
        PoliticsWorldFixture.Check(funded.People.Single(p => p.Id == pupil).Understanding.GetValueOrDefault("entry") == 0 &&
            funded.Politics.Effects.Count(e => e.Kind == PoliticalEffectKind.Teaching) == 1, "funding transfers real budget without inventing learning");
        f.World.AdvanceTo(3); var queued = f.World.Checkpoint();
        PoliticsWorldFixture.Check(queued.People.Single(p => p.Id == f.First).LessonRequests.Count(r => r.Pupil == pupil && r.Knowledge == "entry") == 1 &&
            queued.Politics.RoutedEffects.Count == 1 && queued.People.Single(p => p.Id == pupil).Understanding.GetValueOrDefault("entry") == 0 &&
            !queued.Knowledge.Memories.Any(m => m.PersonId == pupil && m.WorkId == "entry"), "political preparation queues a real message once and grants no free facts");
        f.World = GameCodec.Decode(GameCodec.Encode(f.World));
        f.Send("observe", 0);
        if (f.World.Checkpoint().People.Single(p => p.Id == pupil).Action != ActionKind.Idle) f.Send("cancel", pupil);
        f.Send("possess", f.First); f.Send("teach", f.First, pupil, 1, "entry"); f.World.AdvanceTo(4);
        var learned = f.World.Checkpoint();
        PoliticsWorldFixture.Check(learned.Knowledge.Activities.Any(a => a.Kind == KnowledgeActivityKind.Teaching &&
            a.ActorId == pupil && a.TeacherId == f.First && a.Status == KnowledgeActivityStatus.Completed) &&
            learned.Knowledge.Memories.Any(m => m.PersonId == pupil && m.WorkId == "entry" && m.LearnedMonth == 4) &&
            learned.People.Single(p => p.Id == pupil).Understanding.GetValueOrDefault("entry") > 0, "only actual timed teaching produces source-backed understanding");
    }
}

public sealed class PoliticsPublicRightsIntegrationScenario : IFeatureScenario
{
    public string Name => "V021-M03 public politics: real operation/property transfers respect personal knowledge without granting sovereignty";
    public void Run()
    {
        var f = new PoliticsWorldFixture(); f.Update(s => s.Places[0].Owner = f.First);
        f.Send("setOperator", f.First, 0, f.Second);
        var operated = f.World.Checkpoint();
        PoliticsWorldFixture.Check(operated.Places[0].Operator == f.Second && operated.Places[0].Owner == f.First &&
            operated.Places[0].Sovereign == 0 && operated.Groups.Single(g => g.Id == f.Home).Kind == GroupKind.Family,
            "actual operation is independent from property ownership and political sovereignty");
        f.Send("observe", 0); f.Send("create", 0, 0, data: "명시 편입 수령인");
        var newcomer = f.World.Checkpoint().People.Single(p => p.Id != f.First && p.Id != f.Second).Id;
        f.Send("possess", f.First);
        PoliticsWorldFixture.Check(!f.World.Checkpoint().People.Single(p => p.Id == f.First).KnownPeople.Contains(newcomer), "recipient has not yet been personally observed");
        f.Reject(f.Command("transferOwnership", f.First, 0, newcomer));
        f.World.AdvanceTo(1);
        PoliticsWorldFixture.Check(f.World.Checkpoint().People.Single(p => p.Id == f.First).KnownPeople.Contains(newcomer), "actual local observation discovers the recipient");
        f.Send("transferOwnership", f.First, 0, newcomer);
        var transferred = f.World.Checkpoint();
        PoliticsWorldFixture.Check(transferred.Places[0].Owner == newcomer && transferred.Places[0].Operator == f.Second &&
            transferred.Places[0].Sovereign == 0 && transferred.Groups.All(g => g.Kind != GroupKind.Dynasty), "public property transfer changes only the specified real rights");
        Invariants.Validate(GameCodec.Decode(GameCodec.Encode(f.World)).Checkpoint());
    }
}

public sealed class PoliticsPublicEstateRightsIntegrationScenario : IFeatureScenario
{
    public string Name => "V021-M03/G02 public politics: actual death inheritance transfers personal land and preserves another operator's rights";
    public void Run()
    {
        var f = new PoliticsWorldFixture(); var home = f.Home;
        f.Update(s =>
        {
            s.Places[0].Owner = f.First; s.Places[0].Operator = f.Second;
            s.Places[1].Owner = f.Second; s.Places[1].Operator = f.First;
            s.People.Single(p => p.Id == f.First).Health = 1;
        });
        f.Send("observe", 0); f.Send("cancel", f.Second); f.Send("possess", f.Second); f.Send("attack", f.Second, f.First);
        var estate = f.World.Checkpoint();
        PoliticsWorldFixture.Check(estate.People.Single(p => p.Id == f.First).Status == LifeStatus.Dead &&
            estate.Places[0].Owner == f.Second && estate.Places[0].Operator == f.Second &&
            estate.Places[1].Owner == f.Second && estate.Places[1].Operator == 0 && estate.Places.Take(2).All(p => p.Sovereign == 0) &&
            estate.Groups.Single(g => g.Id == home).Leader == f.Second, "actual physical heir receives private land, current third-party operation survives, and departed personal operation ends");
        Invariants.Validate(GameCodec.Decode(GameCodec.Encode(f.World)).Checkpoint());
    }
}

public sealed class PoliticsAiRejectionAtomicityScenario : IFeatureScenario
{
    public string Name => "V021-G03 AI politics: direct rejected founding and second-phase preparation consume no IDs or resources";
    public void Run()
    {
        var f = new PoliticsWorldFixture(); f.Send("observe", 0); f.Send("law", 0, value: 0, data: "politics.sectLimit");
        var state = (GameState)typeof(GameWorld).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.World)!;
        var actor = state.People.Single(p => p.Id == f.First);
        var before = f.World.StateHash();
        var planning = typeof(GameWorld).GetMethod("ExecutePlanningStep", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var rejected = planning.Invoke(f.World, [actor, new PlanStep("found", 0, "sect"), null]);
        PoliticsWorldFixture.Check(rejected is string && f.World.StateHash() == before, "the actual AI typed planning dispatch has no candidate rollback and must not consume a proposed global ID");
        rejected = planning.Invoke(f.World, [actor, new PlanStep("policy", f.Home, "영토·방위"), null]);
        PoliticsWorldFixture.Check(rejected is string && f.World.StateHash() == before && f.World.Checkpoint().Politics.Preparations.Count == 0,
            "a direct AI policy failure after adoption preflight cannot leave a pending category, budget reservation or history");
    }
}

public sealed class PoliticsActualSpeciesMaturityScenario : IFeatureScenario
{
    public string Name => "V021-W02/G01 public politics: actual fox and pine body maturity govern consent and family leadership independently of human law";
    public void Run()
    {
        var f = new PoliticsWorldFixture(); f.Send("observe", 0);
        f.Send("create", 0, data: JsonSerializer.Serialize(new { Name = "성년 영호", Species = "spirit-fox" }));
        f.Send("create", 0, data: JsonSerializer.Serialize(new { Name = "미성년 영송", Species = "spirit-pine" }));
        var created = f.World.Checkpoint(); var fox = created.People.Single(p => p.Species == "spirit-fox").Id;
        var pine = created.People.Single(p => p.Species == "spirit-pine").Id;
        f.Update(s =>
        {
            var young = s.People.Single(p => p.Id == pine); young.Born = s.Month - 300;
            s.People.Single(p => p.Id == f.First).Money -= s.Laws["politics.foundCost"];
            young.Money += s.Laws["politics.foundCost"]; // Declared actual sponsor gift; no money creation.
        });
        f.Send("possess", f.First); f.World.AdvanceTo(1);
        f.Send("loan", f.First, fox, f.World.Checkpoint().Laws["politics.foundCost"]);
        f.Send("observe", 0);
        if (f.World.Checkpoint().People.Single(p => p.Id == fox).Action != ActionKind.Idle) f.Send("cancel", fox);
        f.Send("found", fox, data: "family");
        PoliticsWorldFixture.Check(f.World.Checkpoint().Groups.Any(g => g.Kind == GroupKind.Family && g.Leader == fox) &&
            f.World.Checkpoint().People.Single(p => p.Id == fox).Realm == Realm.Mortal,
            "a real 144-month adult fox can consent and lead a funded family before the human 216-month threshold");
        if (f.World.Checkpoint().People.Single(p => p.Id == pine).Action != ActionKind.Idle) f.Send("cancel", pine);
        f.Send("law", 0, value: 1, data: "birth.maturity");
        f.Reject(f.Command("found", pine, data: "family"));
        f.Update(s => s.People.Single(p => p.Id == pine).Born = s.Month - 600);
        f.Send("found", pine, data: "family");
        var accepted = f.World.Checkpoint();
        PoliticsWorldFixture.Check(accepted.Groups.Any(g => g.Kind == GroupKind.Family && g.Leader == pine) &&
            accepted.PeopleDomain.Profiles.Single(p => p.PersonId == pine).Body.Reproduction.MaturityMonths == 600,
            "human maturity edits cannot adultify a 300-month pine; its actual saved 600-month boundary applies");
        Invariants.Validate(GameCodec.Decode(GameCodec.Encode(f.World)).Checkpoint());
    }
}

public sealed class PoliticsPublicSectLifecycleScenario : IFeatureScenario
{
    public string Name => "V021-S02/S03 public politics: paid education and service cause succession, current charter revocation applies, rebirth restores no office";
    public void Run()
    {
        var f = new PoliticsWorldFixture(); f.Send("observe", 0);
        f.Send("law", 0, value: 1, data: "birth.enabled"); f.Send("law", 0, value: 0, data: "soul.crossing");
        f.Send("law", 0, value: 1, data: "politics.salary");
        f.Send("conceive", f.Second, f.First);
        f.Update(s => s.People.Single(p => p.Id == f.First).KnownPlaces = Enumerable.Range(0, 10).ToList());
        var dynasty = f.Home; f.Send("possess", f.First); f.Send("govern", f.First, dynasty, 0); f.World.AdvanceTo(1);
        var sect = f.FoundSect();
        f.Send("observe", 0); f.Send("join", f.Second, sect);
        f.Send("create", 0, data: "명시 편입 후계 제자");
        var pupil = f.World.Checkpoint().People.Single(p => p.Id != f.First && p.Id != f.Second).Id;
        f.Send("possess", f.First); f.World.AdvanceTo(3); f.Send("observe", 0); f.Send("join", pupil, sect);
        if (f.World.Checkpoint().People.Single(p => p.Id == pupil).Action != ActionKind.Idle) f.Send("cancel", pupil);
        f.Send("possess", f.First);
        f.Send("charterSect", f.First, sect, 0, JsonSerializer.Serialize(new { Until = 20, Teaching = true, Industry = true, Fee = 3 }));
        var charter = f.World.Checkpoint().Politics.Charters.Single().Id;
        f.Send("appointOffice", f.First, sect, f.Second, "treasurer");
        f.Send("policyActivity", f.First, sect, pupil, JsonSerializer.Serialize(new { Category = "교육·전승", Counterparty = f.First, Knowledge = "entry", Place = 0 }));
        f.World.AdvanceTo(4); f.Send("observe", 0);
        if (f.World.Checkpoint().People.Single(p => p.Id == pupil).Action != ActionKind.Idle) f.Send("cancel", pupil);
        f.Send("possess", f.First); f.Send("teach", f.First, pupil, 1, "entry"); f.World.AdvanceTo(5);
        var educated = f.World.Checkpoint(); var treasury = educated.Groups.Single(g => g.Id == sect).Money;
        PoliticsWorldFixture.Check(educated.Politics.Service.Single(s => s.Group == sect && s.Person == pupil).Contribution >
            educated.Politics.Service.FirstOrDefault(s => s.Group == sect && s.Person == f.Second)?.Contribution &&
            educated.Knowledge.Activities.Any(a => a.ActorId == pupil && a.Kind == KnowledgeActivityKind.Teaching && a.Status == KnowledgeActivityStatus.Completed) &&
            educated.Politics.Payments.Any(p => p.Kind.StartsWith($"payroll:{sect}:", StringComparison.Ordinal) && p.To == PoliticalAccount.Person(pupil)),
            "actual funded class work, timed knowledge and paid salary precede succession");
        f.Send("policy", f.First, sect, 2, "교육·전승");
        f.Send("observe", 0);
        if (f.World.Checkpoint().People.Single(p => p.Id == f.Second).Action != ActionKind.Idle) f.Send("cancel", f.Second);
        f.Send("possess", f.Second); f.Update(s => s.People.Single(p => p.Id == f.First).Health = 1);
        var originalSoul = f.World.Checkpoint().People.Single(p => p.Id == f.First).SoulId;
        f.Send("attack", f.Second, f.First); var succeeded = f.World.Checkpoint();
        PoliticsWorldFixture.Check(succeeded.Groups.Single(g => g.Id == sect).Leader == pupil && succeeded.Groups.Single(g => g.Id == dynasty).Leader == f.Second &&
            succeeded.Groups.Single(g => g.Id == sect).Money == treasury && succeeded.Politics.Preparations.Single().Stage == PolicyStage.Cancelled &&
            succeeded.Politics.Obligations.All(o => o.Debtor != PoliticalAccount.Person(pupil)),
            "real service selects the current adult pupil; death refunds unfinished policy and preserves public treasury rather than copying estate authority");
        f.Send("revokeCharter", f.Second, charter); f.Send("observe", 0); f.Send("possess", pupil);
        f.Reject(f.Command("policy", pupil, sect, 0, "교육·전승"));
        f.Send("observe", 0); f.Send("possess", f.Second);
        var birth = f.World.Checkpoint().Pregnancies.Single().Due; f.World.AdvanceTo(birth);
        var returned = f.World.Checkpoint().People.Single(p => p.Id != f.First && p.SoulId == originalSoul);
        PoliticsWorldFixture.Check(returned.Sect == 0 && f.World.Checkpoint().Groups.Single(g => g.Id == sect).Leader == pupil &&
            returned.Realm == Realm.Mortal && returned.Skills.Count == 0, "actual RC-02 birth has a new person/body and no restored sect office or mastery");
        f.Send("possess", returned.Id); f.Reject(f.Command("policy", returned.Id, sect, 2, "승계·인사"));
        f.Reject(f.Command("payroll", returned.Id, sect));
        Invariants.Validate(GameCodec.Decode(GameCodec.Encode(f.World)).Checkpoint());
    }
}
