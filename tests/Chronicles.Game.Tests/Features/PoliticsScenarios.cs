using System.Text.Json;
using Chronicles.Game.Politics;

namespace Chronicles.Game.Tests;

internal sealed class PoliticalFixture
{
    public const long FirstGroup = 10001;
    public const long SecondGroup = 10002;
    public PoliticalContext Context { get; private set; }
    public PoliticalState State { get; private set; } = new();
    public long First { get; }
    public long Second { get; }

    public PoliticalFixture()
    {
        var initial = GameWorld.Create(41).Checkpoint();
        First = initial.People[0].Id; Second = initial.People[1].Id;
        foreach (var person in initial.People)
        {
            person.Action = ActionKind.Idle; person.Money = 1000;
            person.KnownPlaces = initial.Places.Select(p => p.Id).ToList();
            person.KnownPeople = initial.People.Where(p => p.Id != person.Id).Select(p => p.Id).ToList();
        }
        initial.People[0].Household = FirstGroup; initial.People[1].Household = SecondGroup;
        Context = new(12, 1, 1, initial.People,
        [ NewGroup(FirstGroup, First), NewGroup(SecondGroup, Second) ], initial.Places, new());
    }
    private static Group NewGroup(long id, long leader) => new() { Id = id, Kind = GroupKind.Family, Leader = leader,
        Name = $"fixture {id}", Money = 1000, Members = [leader],
        Policies = new(Catalog.PolicyNames.ToDictionary(x => x, _ => 1, StringComparer.Ordinal), StringComparer.Ordinal) };
    public Person Person(long id) => Context.People.Single(p => p.Id == id);
    public Group Group(long id) => Context.Groups.Single(p => p.Id == id);
    public Place Place(int id) => Context.Places.Single(p => p.Id == id);
    public void SetRules(PoliticalRules rules) => Context = Context with { Rules = rules };
    public void SetMonth(long month) => Context = Context with { Month = month, Revision = Context.Revision + 1 };
    public void AddPerson(long id, long group, int place)
    {
        var person = new Person { Id = id, Born = -1200, Status = LifeStatus.Alive, Place = place,
            Household = group, Money = 1000, Roots = [10, 10, 10, 10, 10], KnownPlaces = Context.Places.Select(p => p.Id).ToList() };
        Context = Context with { People = Context.People.Append(person).ToList(),
            Groups = Context.Groups.Append(NewGroup(group, id)).ToList() };
    }
    public void GiveSovereignty(long group, int province)
    {
        Group(group).Kind = GroupKind.Dynasty; Group(group).Provinces.Add(province);
        foreach (var p in Context.Places.Where(p => p.Province == province)) p.Sovereign = group;
    }
    public PoliticalTransition Apply(PoliticalResult result)
    {
        Check(result.Accepted && result.Transition is not null, result.Message);
        var transition = result.Transition!;
        Check(PoliticsEngine.ValidateForApplication(Context, State, transition, out var reason), reason);
        Context = Context with { People = Merge(Context.People, transition.People, p => p.Id),
            Groups = Merge(Context.Groups, transition.Groups, p => p.Id), Places = Merge(Context.Places, transition.Places, p => p.Id),
            Revision = Context.Revision + 1 };
        State = transition.NextState;
        Check(PoliticalInvariants.Validate(Context, State).Count == 0, "political invariants after application");
        return transition;
    }
    private static List<T> Merge<T>(IReadOnlyList<T> before, IReadOnlyList<T> changes, Func<T, long> key) =>
        before.Where(x => !changes.Any(y => key(y) == key(x))).Concat(changes).OrderBy(key).ToList();
    public void ReleaseWork()
    {
        SetMonth(Context.Month + 1);
        Apply(PoliticsEngine.PlanReleaseCompletedLabor(Context, State));
    }
    public long Adopt(string category, int choice = 2, int place = 0)
    {
        Apply(PoliticsEngine.PlanAdoptPolicy(Context, State, First, FirstGroup, category, choice, place));
        return State.Preparations[^1].Id;
    }
    public void Activate(string category, int choice)
    {
        var id = Adopt(category, choice);
        Apply(PoliticsEngine.PlanPreparePolicy(Context, State, First, id));
        SetMonth(Context.Month + Context.Rules.PreparationMonths);
        Apply(PoliticsEngine.PlanImplementPolicy(Context, State, id));
        Check(State.Preparations.Single(p => p.Id == id).Stage == PolicyStage.Implemented, "implemented policy");
    }
    public static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    public static void Rejected(PoliticalResult result) => Check(!result.Accepted && result.Transition is null, "expected atomic rejection: " + result.Message);
}

public sealed class PoliticsRightsScenario : IFeatureScenario
{
    public string Name => "INV-019 politics: ownership, administration, passage and claims remain separate from sovereignty";
    public void Run()
    {
        var f = new PoliticalFixture(); var a = f.First; var b = f.Second;
        f.Place(0).Owner = a;
        f.Apply(PoliticsEngine.PlanClaim(f.Context, f.State, a, PoliticalFixture.FirstGroup, 0, "historic claim"));
        PoliticalFixture.Check(f.Place(0).Sovereign == 0 && f.Group(PoliticalFixture.FirstGroup).Kind == GroupKind.Family, "a claim does not found a dynasty");
        f.Apply(PoliticsEngine.PlanProperty(f.Context, f.State, a, 0, PoliticalRight.Operation, PoliticalAccount.Person(b)));
        PoliticalFixture.Check(f.Place(0).Owner == a && f.Place(0).Operator == b && f.Place(0).Sovereign == 0, "operation does not transfer land or sovereignty");
        f.Apply(PoliticsEngine.PlanSovereignty(f.Context, f.State, a, PoliticalFixture.FirstGroup, 0));
        f.Apply(PoliticsEngine.PlanDelegate(f.Context, f.State, a, 0, PoliticalFixture.SecondGroup));
        PoliticalFixture.Check(f.Group(PoliticalFixture.FirstGroup).Kind == GroupKind.Dynasty && f.Group(PoliticalFixture.SecondGroup).Kind == GroupKind.Family, "administration does not create a dynasty");
        PoliticalFixture.Check(PoliticsEngine.HasRight(f.Context, f.State, PoliticalAccount.Group(PoliticalFixture.SecondGroup), 0, PoliticalRight.Administration), "actual delegation exists");
        PoliticalFixture.Check(!PoliticsEngine.HasRight(f.Context, f.State, PoliticalAccount.Person(b), 0, PoliticalRight.Sovereignty), "operator lacks sovereignty");
        f.Apply(PoliticsEngine.PlanPassage(f.Context, f.State, a, 0, PoliticalAccount.Person(b), 15));
        PoliticalFixture.Check(PoliticsEngine.HasRight(f.Context, f.State, PoliticalAccount.Person(b), 0, PoliticalRight.Passage), "explicit passage exists");
        f.Apply(PoliticsEngine.PlanProperty(f.Context, f.State, a, 0, PoliticalRight.Ownership, PoliticalAccount.Person(b)));
        PoliticalFixture.Check(f.Place(0).Owner == b && f.Place(0).Operator == 0 && f.Place(0).Sovereign == PoliticalFixture.FirstGroup, "ownership transfer preserves political authority");
        f.SetMonth(15);
        PoliticalFixture.Check(!PoliticsEngine.HasRight(f.Context, f.State, PoliticalAccount.Person(b), 0, PoliticalRight.Passage), "passage expires in integer time");
    }
}

public sealed class PoliticsDynastyScenario : IFeatureScenario
{
    public string Name => "INV-019 politics: real province founding, territorial loss, restoration and territoryless persistence";
    public void Run()
    {
        var f = new PoliticalFixture();
        f.Person(f.First).KnownPlaces.Remove(9);
        PoliticalFixture.Rejected(PoliticsEngine.PlanSovereignty(f.Context, f.State, f.First, PoliticalFixture.FirstGroup, 0));
        f.Person(f.First).KnownPlaces.Add(9);
        f.Apply(PoliticsEngine.PlanSovereignty(f.Context, f.State, f.First, PoliticalFixture.FirstGroup, 0));
        f.ReleaseWork(); f.Place(0).Owner = f.First;
        var treasury = f.Group(PoliticalFixture.FirstGroup).Money;
        f.Apply(PoliticsEngine.PlanReleaseProvince(f.Context, f.State, f.First, PoliticalFixture.FirstGroup, 0));
        PoliticalFixture.Check(f.Group(PoliticalFixture.FirstGroup).Kind == GroupKind.Dynasty && f.Group(PoliticalFixture.FirstGroup).Provinces.Count == 0 &&
            f.Group(PoliticalFixture.FirstGroup).Money == treasury && f.Place(0).Owner == f.First, "territoryless dynasty retains identity and actual assets");
        PoliticalFixture.Check(f.State.Claims.Count == 1 && f.Place(0).Sovereign == 0, "claim survives without physical authority");
        f.SetMonth(24); var opening = f.Person(f.First).Money;
        f.Apply(PoliticsEngine.PlanCollectTax(f.Context, f.State));
        PoliticalFixture.Check(f.State.Payments.Count == 0 && f.Person(f.First).Money == opening, "no hidden taxpayers or ghost revenue");
        f.Apply(PoliticsEngine.PlanSovereignty(f.Context, f.State, f.First, PoliticalFixture.FirstGroup, 0));
        PoliticalFixture.Check(f.State.Claims.Count == 1 && f.Group(PoliticalFixture.FirstGroup).Provinces.SequenceEqual([0]), "restoration uses real sovereignty without duplicating claims");
    }
}

public sealed class PoliticsTaxScenario : IFeatureScenario
{
    public string Name => "INV-018/019 politics: vassal DAG, one resident tax and one direct-tax tribute per layer";
    public void Run()
    {
        var f = new PoliticalFixture(); const long third = 20001; const long thirdGroup = 10003;
        f.Person(f.Second).Place = 10; f.AddPerson(third, thirdGroup, 20);
        f.GiveSovereignty(PoliticalFixture.FirstGroup, 0); f.GiveSovereignty(PoliticalFixture.SecondGroup, 1); f.GiveSovereignty(thirdGroup, 2);
        foreach (var group in f.Context.Groups) group.Money = 100000;
        f.Apply(PoliticsEngine.PlanVassalage(f.Context, f.State, f.First, PoliticalFixture.FirstGroup, PoliticalFixture.SecondGroup, f.Second));
        f.Apply(PoliticsEngine.PlanVassalage(f.Context, f.State, f.Second, PoliticalFixture.SecondGroup, thirdGroup, third));
        var before = PoliticsEngine.Fingerprint(f.Context, f.State);
        PoliticalFixture.Rejected(PoliticsEngine.PlanVassalage(f.Context, f.State, third, thirdGroup, PoliticalFixture.FirstGroup, f.First));
        PoliticalFixture.Check(before == PoliticsEngine.Fingerprint(f.Context, f.State), "cycle rejection does not mutate inputs");
        PoliticalFixture.Check(PoliticsEngine.TopOverlord(f.Context, PoliticalFixture.FirstGroup) == thirdGroup, "top relation follows the real DAG");
        f.Apply(PoliticsEngine.PlanCollectTax(f.Context, f.State));
        PoliticalFixture.Check(f.Context.People.All(p => p.Money == 990), "each real resident is taxed exactly once");
        PoliticalFixture.Check(f.Group(PoliticalFixture.FirstGroup).Money == 100009 && f.Group(PoliticalFixture.SecondGroup).Money == 100010 &&
            f.Group(thirdGroup).Money == 100011, "treasury and received tribute are never retaxed");
        PoliticalFixture.Check(f.State.Payments.Count(p => p.Kind == "territorial tax") == 3 && f.State.Payments.Count(p => p.Kind == "vassal tribute") == 2, "one transfer per actual tax relationship");
        PoliticalFixture.Rejected(PoliticsEngine.PlanCollectTax(f.Context, f.State));
    }
}

public sealed class PoliticsDebtScenario : IFeatureScenario
{
    public string Name => "INV-018/019/020 politics: succession and independence preserve one creditor, debtor and enforcement owner";
    public void Run()
    {
        var f = new PoliticalFixture(); f.GiveSovereignty(PoliticalFixture.FirstGroup, 0); f.GiveSovereignty(PoliticalFixture.SecondGroup, 1);
        f.Apply(PoliticsEngine.PlanVassalage(f.Context, f.State, f.Second, PoliticalFixture.SecondGroup, PoliticalFixture.FirstGroup, f.First));
        f.Apply(PoliticsEngine.PlanLoan(f.Context, f.State, f.First, PoliticalAccount.Group(PoliticalFixture.FirstGroup),
            PoliticalAccount.Group(PoliticalFixture.SecondGroup), f.Second, 50, PoliticalFixture.FirstGroup, 0, "actual principal"));
        var loan = f.State.Obligations.Single();
        f.Apply(PoliticsEngine.PlanVassalage(f.Context, f.State, f.Second, PoliticalFixture.SecondGroup, 0));
        PoliticalFixture.Check(f.State.Obligations.Single() == loan, "independence does not duplicate or transfer the debt");
        f.Group(PoliticalFixture.FirstGroup).Members.Add(f.Second);
        f.Person(f.First).Status = LifeStatus.Dead;
        f.Apply(PoliticsEngine.PlanSuccession(f.Context, f.State, PoliticalFixture.FirstGroup, f.Second));
        PoliticalFixture.Check(f.State.Obligations.Single() == loan && f.Group(PoliticalFixture.FirstGroup).Money == 950, "political office succession preserves public treasury and account ownership");
        f.Apply(PoliticsEngine.PlanReleaseProvince(f.Context, f.State, f.Second, PoliticalFixture.FirstGroup, 0));
        PoliticalFixture.Check(!PoliticsEngine.CanEnforce(f.Context, f.State.Obligations.Single()) && f.State.Obligations.Single() == loan, "territory loss removes actual enforcement capacity while preserving its named owner and debt");
        f.Apply(PoliticsEngine.PlanRepayment(f.Context, f.State, f.Second, loan.Id, 20));
        PoliticalFixture.Check(f.State.Obligations.Single().Outstanding == 30 && f.Group(PoliticalFixture.FirstGroup).Money == 970, "actual creditor receives repayment");
        f.Apply(PoliticsEngine.PlanAssignReceivable(f.Context, f.State, f.Second, loan.Id, PoliticalAccount.Person(f.Second), f.Second));
        PoliticalFixture.Check(f.State.Obligations.Count == 1 && f.State.Obligations[0].Creditor == PoliticalAccount.Person(f.Second) &&
            f.State.Obligations[0].Debtor == loan.Debtor && f.State.Obligations[0].Enforcer == loan.Enforcer, "assignment changes only one existing receivable owner");
        PoliticalFixture.Rejected(PoliticsEngine.PlanDissolve(f.Context, f.State, f.Second, PoliticalFixture.SecondGroup));
    }
}

public sealed class PoliticsPolicyLifecycleScenario : IFeatureScenario
{
    public string Name => "INV-020 politics: adopted, insufficient, prepared, implemented and cancelled policy states reserve real resources";
    public void Run()
    {
        var f = new PoliticalFixture(); var id = f.Adopt("경제·분배");
        PoliticalFixture.Check(f.Group(PoliticalFixture.FirstGroup).Money == 1000 && f.Group(PoliticalFixture.FirstGroup).Policies["경제·분배"] == 1, "adoption alone does not spend or implement");
        f.Group(PoliticalFixture.FirstGroup).Money = 0;
        f.Apply(PoliticsEngine.PlanPreparePolicy(f.Context, f.State, f.First, id));
        PoliticalFixture.Check(f.State.Preparations[0].Stage == PolicyStage.InsufficientFunds, "insufficient actual funds are explicit");
        f.Group(PoliticalFixture.FirstGroup).Money = 1000; f.Person(f.First).Action = ActionKind.Cultivate;
        f.Apply(PoliticsEngine.PlanPreparePolicy(f.Context, f.State, f.First, id));
        PoliticalFixture.Check(f.State.Preparations[0].Stage == PolicyStage.InsufficientLabor, "busy real worker cannot prepare");
        f.Person(f.First).Action = ActionKind.Idle;
        f.Apply(PoliticsEngine.PlanPreparePolicy(f.Context, f.State, f.First, id));
        PoliticalFixture.Check(f.Group(PoliticalFixture.FirstGroup).Money == 990 && f.State.Preparations[0].Escrow == 10, "preparation reserves treasury");
        PoliticalFixture.Rejected(PoliticsEngine.PlanImplementPolicy(f.Context, f.State, id));
        var second = f.Adopt("교육·전승");
        f.Apply(PoliticsEngine.PlanPreparePolicy(f.Context, f.State, f.First, second));
        PoliticalFixture.Check(f.State.Preparations.Single(p => p.Id == second).Stage == PolicyStage.InsufficientLabor && f.Group(PoliticalFixture.FirstGroup).Money == 990, "categories share the same worker and reserve budget atomically");
        f.SetMonth(13);
        f.Apply(PoliticsEngine.PlanImplementPolicy(f.Context, f.State, id));
        PoliticalFixture.Check(f.State.ConsumedMoney == 10 && f.Group(PoliticalFixture.FirstGroup).Policies["경제·분배"] == 2, "implementation consumes escrow once");
        f.Apply(PoliticsEngine.PlanPreparePolicy(f.Context, f.State, f.First, second));
        f.Apply(PoliticsEngine.PlanCancelPolicy(f.Context, f.State, f.First, second));
        PoliticalFixture.Check(f.Group(PoliticalFixture.FirstGroup).Money == 990 && f.State.Labor.Count == 0 && f.State.ConsumedMoney == 10, "cancellation refunds escrow without resource creation");
    }
}

public sealed class PoliticsPolicyRevalidationScenario : IFeatureScenario
{
    public string Name => "INV-019/020 politics: implementation rechecks current leader, charter, jurisdiction and application revision";
    public void Run()
    {
        var f = new PoliticalFixture(); f.GiveSovereignty(PoliticalFixture.FirstGroup, 0);
        var id = f.Adopt("영토·방위", 0);
        f.Apply(PoliticsEngine.PlanPreparePolicy(f.Context, f.State, f.First, id));
        f.Apply(PoliticsEngine.PlanReleaseProvince(f.Context, f.State, f.First, PoliticalFixture.FirstGroup, 0));
        f.SetMonth(13);
        f.Apply(PoliticsEngine.PlanImplementPolicy(f.Context, f.State, id));
        PoliticalFixture.Check(f.State.Preparations[0].Stage == PolicyStage.Cancelled && f.Group(PoliticalFixture.FirstGroup).Money == 1000 &&
            f.State.ConsumedMoney == 0 && f.Group(PoliticalFixture.FirstGroup).Policies["영토·방위"] == 1, "territory change cancels and refunds prepared policy");
        var plan = PoliticsEngine.PlanClaim(f.Context, f.State, f.First, PoliticalFixture.FirstGroup, 0, "restoration");
        PoliticalFixture.Check(plan.Accepted, plan.Message); f.Person(f.Second).Money++;
        PoliticalFixture.Check(!PoliticsEngine.ValidateForApplication(f.Context, f.State, plan.Transition!, out _), "stale world snapshot cannot apply an old atomic plan");
        var other = new PoliticalFixture(); var pending = other.Adopt("경제·분배", 0);
        other.Apply(PoliticsEngine.PlanPreparePolicy(other.Context, other.State, other.First, pending));
        other.Group(PoliticalFixture.FirstGroup).Members.Add(other.Second); other.Group(PoliticalFixture.FirstGroup).Leader = other.Second;
        other.SetMonth(13); other.Apply(PoliticsEngine.PlanImplementPolicy(other.Context, other.State, pending));
        PoliticalFixture.Check(other.State.Preparations[0].Stage == PolicyStage.Cancelled && other.Group(PoliticalFixture.FirstGroup).Money == 1000, "new leader must reauthorize policy through a new preparation");
    }
}

public sealed class PoliticsSectScenario : IFeatureScenario
{
    public string Name => "INV-020 politics: actual sect founding, concurrent limit, local charter, offices, payroll and dissolution";
    public void Run()
    {
        var initial = GameWorld.Create(42).Checkpoint();
        PoliticalFixture.Check(initial.People.Count == 2 && initial.Groups.All(g => g.Kind != GroupKind.Sect), "module preserves two Half Immortal founders and zero initial sects");
        var f = new PoliticalFixture(); f.GiveSovereignty(PoliticalFixture.FirstGroup, 0); const long sect = 10005;
        f.Apply(PoliticsEngine.PlanFound(f.Context, f.State, f.Second, sect, "test sect", GroupKind.Sect));
        f.SetRules(f.Context.Rules with { SectLimit = 1 });
        PoliticalFixture.Rejected(PoliticsEngine.PlanFound(f.Context, f.State, f.First, sect + 1, "over limit", GroupKind.Sect));
        f.ReleaseWork();
        f.Apply(PoliticsEngine.PlanOffice(f.Context, f.State, f.Second, sect, "treasurer", f.Second));
        f.Person(f.Second).Understanding["path.1.0"] = 20;
        f.Apply(PoliticsEngine.PlanAdoptPolicy(f.Context, f.State, f.Second, sect, "교육·전승", 0));
        var id = f.State.Preparations.Single().Id;
        PoliticalFixture.Rejected(PoliticsEngine.PlanPreparePolicy(f.Context, f.State, f.Second, id));
        f.Apply(PoliticsEngine.PlanCharter(f.Context, f.State, f.First, sect, f.Second, 0, true, true, 20, 3));
        f.Apply(PoliticsEngine.PlanPreparePolicy(f.Context, f.State, f.Second, id));
        f.SetMonth(14); f.Apply(PoliticsEngine.PlanImplementPolicy(f.Context, f.State, id));
        var before = f.Person(f.Second).Money;
        f.Apply(PoliticsEngine.PlanPayroll(f.Context, f.State, f.Second, sect));
        PoliticalFixture.Check(f.Person(f.Second).Money == before + 2, "salary is an actual treasury transfer");
        f.Apply(PoliticsEngine.PlanDissolve(f.Context, f.State, f.Second, sect));
        PoliticalFixture.Check(f.Group(sect).Dissolved && f.Group(sect).Money == 0 && f.Person(f.Second).Sect == 0 && !f.State.Offices.Any(o => o.Group == sect), "dissolution leaves historical organization and clears current authority");
    }
}

public sealed class PoliticsPolicyEffectsScenario : IFeatureScenario
{
    public string Name => "INV-018/020 politics: all ten detailed policy categories use actual input and effect semantics";
    public void Run()
    {
        PoliticalFixture.Check(PolicyCatalog.All.Count == 10 && PolicyCatalog.All.All(p => p.Choices.Count == 3 && p.Input.Length > 0 && p.Effect.Length > 0), "ten actual detailed policy contracts");
        foreach (var category in Catalog.PolicyNames)
        {
            var f = new PoliticalFixture(); f.GiveSovereignty(PoliticalFixture.FirstGroup, 0);
            f.Group(PoliticalFixture.FirstGroup).Members.Add(f.Second);
            f.Person(f.Second).Understanding["alchemy"] = 20; f.Person(f.Second).Skills["alchemy"] = 20;
            f.Person(f.First).Understanding["alchemy"] = 10;
            f.Person(f.Second).Roots = [10, 10, 10, 10, 10];
            f.Activate(category, 0);
            if (category == "입문·선발") f.Group(PoliticalFixture.FirstGroup).Members.Remove(f.Second);
            if (category == "민생·구휼") f.Person(f.Second).Money = 0;
            var candidate = category is "대외·외교" ? PoliticalFixture.SecondGroup : category is "승계·인사" ? f.First : f.Second;
            var knowledge = category == "교육·전승" ? "alchemy" : category == "기예·산업" ? "alchemy" : "";
            var activity = new PolicyActivity(f.First, PoliticalFixture.FirstGroup, category, candidate,
                category == "교육·전승" ? f.First : f.Second, knowledge, 0);
            var opening = f.Person(f.Second).Money; var treasury = f.Group(PoliticalFixture.FirstGroup).Money;
            f.Apply(PoliticsEngine.PlanPolicyActivity(f.Context, f.State, activity));
            switch (category)
            {
                case "입문·선발": PoliticalFixture.Check(f.Group(PoliticalFixture.FirstGroup).Members.Contains(f.Second), "admission changes actual membership"); break;
                case "교육·전승": PoliticalFixture.Check(f.State.Effects.Any(e => e.Kind == PoliticalEffectKind.Teaching && e.Subject == "alchemy") && f.Person(f.Second).Understanding["alchemy"] == 20, "funded actual teaching directive does not clone knowledge"); break;
                case "수행·자원": case "기예·산업": case "민생·구휼":
                    PoliticalFixture.Check(f.Person(f.Second).Money == opening + 5 && f.Group(PoliticalFixture.FirstGroup).Money == treasury - 5, "actual grant is transferred"); break;
                case "경제·분배": PoliticalFixture.Check(f.Person(f.Second).Money == opening + 1, "detailed salary effect"); break;
                case "규율·상벌": PoliticalFixture.Check(f.Person(f.Second).Money == opening - 5, "actual capped fine"); break;
                case "대외·외교": PoliticalFixture.Check(f.State.Accords.Single().Level == 0, "actual accord level"); break;
                case "영토·방위": PoliticalFixture.Check(PoliticsEngine.HasDefense(f.Context, f.State, 0) && f.State.Labor.Count == 1, "real local guard labor effect"); break;
                case "승계·인사": PoliticalFixture.Check(f.Group(PoliticalFixture.FirstGroup).Leader == f.First && f.State.Offices.Any(o => o.Name == "leader"), "actual succession office effect"); break;
            }
        }
    }
}

public sealed class PoliticsSaveScenario : IFeatureScenario
{
    public string Name => "INV-004/020 politics: domain save roundtrip, resumed preparation and deterministic pure queries";
    public void Run()
    {
        var f = new PoliticalFixture(); var id = f.Adopt("경제·분배");
        f.Apply(PoliticsEngine.PlanPreparePolicy(f.Context, f.State, f.First, id));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(f.State);
        var restored = JsonSerializer.Deserialize<PoliticalState>(bytes)!;
        PoliticalFixture.Check(bytes.SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(restored)), "all reservation and history facts survive serialization");
        var before = PoliticsEngine.Fingerprint(f.Context, f.State);
        _ = PoliticsEngine.HasRight(f.Context, f.State, PoliticalAccount.Person(f.First), 0, PoliticalRight.Ownership);
        _ = PoliticsEngine.TopOverlord(f.Context, PoliticalFixture.FirstGroup);
        _ = PoliticalInvariants.Validate(f.Context, f.State);
        PoliticalFixture.Check(PoliticsEngine.Fingerprint(f.Context, f.State) == before, "queries consume no state or random values");
        f.SetMonth(13);
        var original = PoliticsEngine.PlanImplementPolicy(f.Context, f.State, id);
        var resumed = PoliticsEngine.PlanImplementPolicy(f.Context, restored, id);
        PoliticalFixture.Check(original.Accepted && resumed.Accepted && JsonSerializer.Serialize(original) == JsonSerializer.Serialize(resumed), "resumed policy plan equals uninterrupted plan");
    }
}
