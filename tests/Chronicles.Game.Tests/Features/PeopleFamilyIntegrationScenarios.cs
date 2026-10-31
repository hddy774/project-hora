using System.Text.Json;
using Chronicles.Game.Knowledge;
using Chronicles.Game.People;
using Chronicles.Game.Politics;

namespace Chronicles.Game.Tests.Features;

public sealed class PeoplePublicFamilyFlowScenario : IFeatureScenario
{
    public string Name => "INV-001/004/006/012/017/022 people family public: paid care, real teaching, consented adoption, adult separation and estate versus office succession";
    public void Run()
    {
        var f = new PeopleSocialPublicFixture { World = PeoplePublicFixture.Fresh(new()
        {
            ["birth.gestation"] = 1, ["birth.maturity"] = 6, ["care.age"] = 6, ["care.interval"] = 1,
            ["ecology.foodUse"] = 0, ["soul.crossing"] = 0
        }) };
        var father = f.First; var mother = f.Second; var originalHome = f.World.Checkpoint().People[0].Household;
        var adopter = f.Create("별도 가계의 양육자");
        f.World = PeoplePublicFixture.Configure(f.World, state =>
        {
            foreach (var person in state.People) person.Personality[4] = 70;
        });
        f.Control(mother); f.Send("conceive", mother, father); var pregnancy = f.World.Checkpoint().Pregnancies.Single();
        var child = PeoplePublicFixture.Birth(f.World, pregnancy.Id).Id;
        var born = f.World.Checkpoint(); var innate = JsonSerializer.Serialize(born.PeopleDomain.Profiles.Single(p => p.PersonId == child).Innate);
        var frozen = JsonSerializer.Serialize(born.PeopleDomain.Inheritance.Conceptions.Single());
        var foodBefore = born.Items.Where(i => i.Owner == mother && i.Definition == "food").Sum(i => i.Quantity);
        var foodFlowBefore = born.Materials["food"].Consumption;
        f.Send("care", mother, child); var caring = f.World.Checkpoint().People.Single(p => p.Id == mother);
        PeopleFixture.Check(caring.Action == ActionKind.Care && caring.Due == 2 && f.World.Checkpoint().People.Single(p => p.Id == child).LastCare == 1,
            "Care must reserve the real guardian's month before recording its result.");
        f.World.AdvanceTo(2); var cared = f.World.Checkpoint();
        PeopleFixture.Check(cared.People.Single(p => p.Id == child).LastCare == 2 &&
            cared.Items.Where(i => i.Owner == mother && i.Definition == "food").Sum(i => i.Quantity) == foodBefore - cared.Laws["care.food"] &&
            cared.Materials["food"].Consumption == foodFlowBefore + cared.Laws["care.food"] &&
            cared.Events.Any(e => e.Kind == "돌봄" && e.Actor == mother && e.Target == child),
            "Actual completed care consumes its separate finite food input and updates the child's dated care receipt.");
        f.Send("teach", mother, child, 1, "entry"); var lesson = f.World.Checkpoint().Knowledge.Activities.Single(a => a.Kind == KnowledgeActivityKind.Teaching);
        PeopleFixture.Check(lesson.Status == KnowledgeActivityStatus.Active && f.World.Checkpoint().People.Where(p => p.Id == mother || p.Id == child).All(p => p.Due == 3),
            "A real lesson reserves teacher and pupil time rather than granting inherited knowledge.");
        f.World = GameCodec.Decode(GameCodec.Encode(f.World)); f.World.AdvanceTo(3); var taught = f.World.Checkpoint();
        PeopleFixture.Check(taught.Knowledge.Activities.Single(a => a.Id == lesson.Id).Status == KnowledgeActivityStatus.Completed &&
            taught.Knowledge.Memories.Any(m => m.PersonId == child && m.WorkId == "entry") &&
            taught.PeopleDomain.Profiles.Single(p => p.PersonId == child).Learning is [{ Kind: PeopleLearningKind.Teaching }] &&
            JsonSerializer.Serialize(taught.PeopleDomain.Profiles.Single(p => p.PersonId == child).Innate) == innate,
            "Only the actual completed lesson adds a source-backed current-life learning receipt; innate inheritance stays fixed.");
        var food = taught.Items.First(i => i.Owner == mother && i.Definition == "food");
        f.Send("split", mother, food.Id, 1);
        var portion = f.World.Checkpoint().Items.Single(i => i.Owner == mother && i.Definition == "food" && i.Id != food.Id && i.Quantity == 1);
        f.Send("offer", mother, portion.Id, 1);
        var sale = f.World.Checkpoint().Inventory.Contracts.Single(c => c.Seller == mother && c.Item == portion.Id && c.Status == "open");
        f.Cancel(father); var beforeFunding = f.World.Checkpoint();
        f.Send("loan", father, adopter, 101, JsonSerializer.Serialize(new { Basis = "독립 양육 가계의 실제 재원", Creditor = PoliticalAccount.Person(father) }));
        var funded = f.World.Checkpoint();
        PeopleFixture.Check(funded.CreatedMoney == beforeFunding.CreatedMoney && funded.People.Single(p => p.Id == father).Money == beforeFunding.People.Single(p => p.Id == father).Money - 101 &&
            funded.People.Single(p => p.Id == adopter).Money == 101, "An actual consenting funded loan moves existing personal money into the new caregiver's account.");
        f.Cancel(adopter); f.Send("trade", adopter, sale.Id); f.Send("found", adopter, data: "family"); f.World.AdvanceTo(4);
        var adoptedHome = f.World.Checkpoint().People.Single(p => p.Id == adopter).Household;
        var refused = PeoplePublicFixture.Configure(f.World, state => state.People.Single(p => p.Id == mother).Personality[4] = 0);
        var refusedHash = refused.StateHash(); var refusal = refused.Execute(new("people-family-refused-adoption", adopter, "adopt", child));
        PeopleFixture.Check(!refusal.Accepted && refused.StateHash() == refusedHash, "A living guardian's refusal cannot be bypassed by adoption or partially alter either family.");
        f.Cancel(mother); f.Control(adopter); f.Send("adopt", adopter, child); var adoption = f.World.Checkpoint();
        PeopleFixture.Check(adoption.People.Single(p => p.Id == child) is { ParentOne: var one, ParentTwo: var two, Guardian: var guardian, Household: var household } &&
            one == mother && two == father && guardian == adopter && household == adoptedHome &&
            !adoption.Groups.Single(g => g.Id == originalHome).Members.Contains(child) && adoption.Groups.Single(g => g.Id == adoptedHome).Members.Contains(child) &&
            adoption.PeopleDomain.Traits.Traits.Any(t => t.PersonId == child && t.DefinitionId == "relation.teacher" && t.RelatedEntityId == mother && Traits.IsActive(t, 4)) &&
            adoption.PeopleDomain.Traits.Traits.Any(t => t.PersonId == child && t.DefinitionId == "relation.family" && t.RelatedEntityId == adopter && Traits.IsActive(t, 4)),
            "Consented non-biological adoption moves actual care responsibility and membership without replacing biological parents or the actual teacher.");
        f.World.AdvanceTo(5); f.Send("care", adopter, child); f.World.AdvanceTo(6);
        PeopleFixture.Check(f.World.Checkpoint().People.Single(p => p.Id == child).LastCare == 6 && f.World.Checkpoint().Items.All(i => i.Id != portion.Id),
            "The adopter must actually spend a month and the purchased finite food portion on the child's later care.");
        f.World.AdvanceTo(7); f.Cancel(father);
        f.Send("loan", father, child, 100, JsonSerializer.Serialize(new { Basis = "성년 자립 가계의 실제 재원" }));
        f.Control(child); f.Send("found", child, data: "family"); f.World.AdvanceTo(8); var separated = f.World.Checkpoint();
        var independentHome = separated.People.Single(p => p.Id == child).Household;
        PeopleFixture.Check(independentHome != adoptedHome && separated.Groups.Single(g => g.Id == independentHome).Money == 100 &&
            !separated.Groups.Single(g => g.Id == adoptedHome).Members.Contains(child) && separated.People.Single(p => p.Id == child).Realm == Realm.Mortal &&
            separated.PeopleDomain.Profiles.Single(p => p.PersonId == child).Learning.Length == 1,
            "A real adult with actual founding capital forms a separate family while still mortal; fertility qualification and inherited training are independent.");
        f.Cancel(father);
        foreach (var offer in f.World.Checkpoint().Inventory.Contracts.Where(c => c.Seller == father && c.Status == "open" && c.Buyer == 0 && c.Kind is "sale" or "auction").ToArray())
            f.Send("withdraw", father, offer.Id);
        var beforeDeath = f.World.Checkpoint(); var estateMoney = beforeDeath.People.Single(p => p.Id == father).Money;
        var estate = beforeDeath.Items.Where(i => i.Owner == father && i.Reserved == 0 && i.BoundSoul == 0).Select(i => i.Id).ToArray();
        var childMoney = beforeDeath.People.Single(p => p.Id == child).Money;
        f.World = PeoplePublicFixture.Configure(f.World, state => state.People.Single(p => p.Id == father).Lifespan = 9 - state.People.Single(p => p.Id == father).Born);
        f.Control(child); f.World.AdvanceTo(9); var inherited = f.World.Checkpoint();
        var closingCredits = inherited.Politics.Payments.Where(p => p.Month == 9 && p.To == PoliticalAccount.Person(father)).Sum(p => p.Amount) -
            inherited.Politics.Payments.Where(p => p.Month == 9 && p.From == PoliticalAccount.Person(father)).Sum(p => p.Amount);
        PeopleFixture.Check(inherited.People.Single(p => p.Id == father).Status == LifeStatus.Dead && inherited.People.Single(p => p.Id == child).Money == childMoney + estateMoney + closingCredits &&
            estate.All(id => inherited.Items.Single(i => i.Id == id).Owner == child) && inherited.Groups.Single(g => g.Id == originalHome).Leader == mother &&
            inherited.Groups.Single(g => g.Id == independentHome).Leader == child,
            $"The biological descendant receives the real personal estate across separated households; the old family office succeeds to its actual current member. " +
            $"status={inherited.People.Single(p => p.Id == father).Status}; child money={inherited.People.Single(p => p.Id == child).Money}/{childMoney + estateMoney + closingCredits}; " +
            $"missing estate={string.Join(',', estate.Where(id => inherited.Items.Single(i => i.Id == id).Owner != child))}; " +
            $"old leader={inherited.Groups.Single(g => g.Id == originalHome).Leader}/{mother}; child leader={inherited.Groups.Single(g => g.Id == independentHome).Leader}/{child}");
        PeopleFixture.Check(inherited.People.Single(p => p.Id == child).SoulId == born.People.Single(p => p.Id == child).SoulId &&
            inherited.PeopleDomain.Profiles.Single(p => p.PersonId == child).Learning.Length == 1 &&
            JsonSerializer.Serialize(inherited.PeopleDomain.Inheritance.Conceptions.Single()) == frozen &&
            JsonSerializer.Serialize(inherited.PeopleDomain.Profiles.Single(p => p.PersonId == child).Innate) == innate &&
            inherited.Politics.Obligations.Count == 2 && inherited.Politics.Obligations.All(o => o.Creditor == PoliticalAccount.Person(child)) &&
            inherited.Politics.Obligations.Single(o => o.Debtor == PoliticalAccount.Person(adopter)).Outstanding == 101 &&
            inherited.Politics.Obligations.Single(o => o.Debtor == PoliticalAccount.Person(child)).Outstanding == 0,
            "Care, education, legal family, original soul, innate genome, private estate and explicitly owned receivables remain separate source histories.");
        PeopleFixture.Check(GameCodec.Decode(GameCodec.Encode(f.World)).StateHash() == f.World.StateHash(), "The complete actual family flow resumes through the production save.");
        Invariants.Validate(inherited);
    }
}
