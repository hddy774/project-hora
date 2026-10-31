namespace Chronicles.Game.People;

public enum BodyForm { Humaniform, Quadruped, Avian, Reptilian, Aquatic, RootedPlant, Spirit, Construct }
public enum PersonOrigin { Founder, BiologicalBirth, AwakenedBeast, AwakenedPlant, ManifestedSpirit, ConstructedBody, ExplicitArrival }
public enum YaoguaiTitle { None, BeastCultivator, PlantCultivator, SpiritBeing }
public enum HabitatKind { Settlement, Grassland, Forest, Mountain, Marsh, Water, Desert, Cave }
public enum ParticipationKind { Conversation, Family, Organization, Cultivation, Martial, Craft, Agriculture, Governance }
[Flags]
public enum ReproductiveFunction { None = 0, Donor = 1, Gestator = 2 }
public sealed record ReproductionProfile(string GroupId, ReproductiveFunction Functions, long MaturityMonths, int GestationMonths,
    PeopleSource Source);
public sealed record BodyCapabilities(bool Communication, bool Manipulation, bool Mobility, bool SpiritualManipulation);
public sealed record Embodiment(long PersonId, long BodyId, long SoulId, string SpeciesId, BodyForm CurrentForm, PersonOrigin Origin,
    string GenderDescription, YaoguaiTitle Title, ReproductionProfile Reproduction, BodyCapabilities Capabilities, PeopleSource Source);
public sealed record SpeciesDefinition(string Id, string Name, BodyForm NaturalForm, long BasicLifespanMonths, HabitatKind[] Habitats,
    string[] ResourceNeeds, bool Sapient, string ReproductionGroup, long MaturityMonths, int GestationMonths,
    string PolicyId = "PC-TEMP-V021-SPECIES-01", bool RequiresReview = true, BodyCapabilities? NaturalCapabilities = null);
public sealed record HabitatAssessment(bool Suitable, string[] RequiredResources, string Reason);
public sealed record ReproductivePerson(Embodiment Body, Realm CultivationRealm, int MartialStage, long AgeMonths, int Health,
    bool Alive, bool Consents, bool AlreadyGestating, long RecoveryUntilMonth);
public sealed record ReproductionCompatibility(string GroupOne, string GroupTwo, string ChildSpeciesId, PeopleSource Source);
public sealed record ReproductionPolicy(Realm MinimumRealm = Realm.GoldenCore, bool AllowBiologicalKin = true,
    int MinimumHealth = 5000, string PolicyId = "PC-TEMP-V021-REPRODUCTION-01", long Revision = 1, bool RequiresReview = true,
    bool RequireBothParents = true, bool AllowMartialEquivalent = false);
public sealed record ReproductionContext(long Month, ReproductivePerson Gestator, ReproductivePerson Donor, bool BiologicalKin,
    bool HasCareSupport, ReproductionPolicy Policy, ReproductionCompatibility[] ExplicitCompatibility);
public sealed record ReproductionEligibility(bool Eligible, string Code, string? ChildSpeciesId, int? GestationMonths);

public static class SpeciesCatalog
{
    public static IReadOnlyList<SpeciesDefinition> All { get; } = Array.AsReadOnly<SpeciesDefinition>(
    [
        new("human", "인간", BodyForm.Humaniform, 1200, [HabitatKind.Settlement, HabitatKind.Grassland, HabitatKind.Forest], ["food", "water"], true, "human", 216, 9, NaturalCapabilities: new(true, true, true, false)),
        new("spirit-fox", "영호", BodyForm.Quadruped, 1440, [HabitatKind.Forest, HabitatKind.Mountain], ["food", "water"], true, "fox", 144, 6, NaturalCapabilities: new(true, false, true, false)),
        new("stone-ape", "석원", BodyForm.Quadruped, 1800, [HabitatKind.Mountain, HabitatKind.Cave], ["food", "water"], true, "ape", 240, 12, NaturalCapabilities: new(true, true, true, false)),
        new("spirit-pine", "영송", BodyForm.RootedPlant, 3600, [HabitatKind.Forest, HabitatKind.Mountain], ["water", "soil"], true, "pine", 600, 12, NaturalCapabilities: new(true, true, false, false)),
        new("water-spirit", "수령", BodyForm.Spirit, 2400, [HabitatKind.Water, HabitatKind.Marsh], ["water", "ambient-qi"], true, "water-spirit", 240, 12, NaturalCapabilities: new(true, false, true, true))
    ]);
    public static SpeciesDefinition Get(string id) => All.FirstOrDefault(s => s.Id == id) ?? throw new InvalidDataException("Unknown species definition.");
}

public static class Embodiments
{
    public static void Validate(Embodiment body)
    {
        ArgumentNullException.ThrowIfNull(body); body.Source.Validate(); body.Reproduction.Source.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(body.SpeciesId); ArgumentException.ThrowIfNullOrWhiteSpace(body.Reproduction.GroupId);
        if (body.PersonId <= 0 || body.BodyId <= 0 || body.SoulId <= 0 || !Enum.IsDefined(body.CurrentForm) ||
            !Enum.IsDefined(body.Origin) || !Enum.IsDefined(body.Title) || body.Reproduction.MaturityMonths < 0 ||
            body.Reproduction.GestationMonths < 1 || (body.Reproduction.Functions & ~(ReproductiveFunction.Donor | ReproductiveFunction.Gestator)) != 0)
            throw new InvalidDataException("Species, current body, social title and reproductive functions must have valid independent identities.");
    }

    public static bool CanParticipate(SpeciesDefinition species, Embodiment body, ParticipationKind activity)
    {
        ArgumentNullException.ThrowIfNull(species); ArgumentNullException.ThrowIfNull(body);
        Validate(body);
        if (species.Id != body.SpeciesId || !Enum.IsDefined(activity)) throw new InvalidDataException("Participation requires the actual species/body profile.");
        if (!species.Sapient) return false;
        return activity switch
        {
            ParticipationKind.Conversation or ParticipationKind.Organization or ParticipationKind.Governance => body.Capabilities.Communication,
            ParticipationKind.Craft or ParticipationKind.Agriculture => body.Capabilities.Manipulation || body.Capabilities.SpiritualManipulation,
            _ => true
        };
    }

    public static HabitatAssessment AssessHabitat(SpeciesDefinition species, HabitatKind habitat)
    {
        ArgumentNullException.ThrowIfNull(species);
        if (!Enum.IsDefined(habitat)) throw new ArgumentOutOfRangeException(nameof(habitat));
        var suitable = species.Habitats.Contains(habitat);
        return new(suitable, species.ResourceNeeds.ToArray(), suitable ? "species habitat match; actual resources still required" : "requires defined shelter or environmental adaptation");
    }

    public static ReproductionEligibility CheckReproduction(ReproductionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Validate(context.Gestator.Body); Validate(context.Donor.Body);
        var policy = context.Policy;
        PeopleChecks.Score(policy.MinimumHealth, nameof(policy.MinimumHealth));
        if (context.Month < 0 || policy.Revision < 1 || !Enum.IsDefined(policy.MinimumRealm)) throw new InvalidDataException("Invalid reproduction policy/time.");
        var one = context.Gestator; var two = context.Donor;
        foreach (var person in new[] { one, two })
        {
            PeopleChecks.Score(person.Health, nameof(person.Health));
            if (person.AgeMonths < 0 || person.MartialStage is < 0 or > 10 || !Enum.IsDefined(person.CultivationRealm))
                throw new InvalidDataException("Invalid reproductive body state.");
        }
        if (one.Body.PersonId == two.Body.PersonId || one.Body.BodyId == two.Body.BodyId || one.Body.SoulId == two.Body.SoulId)
            return new(false, "distinct actual parents required", null, null);
        if (!one.Alive || !two.Alive || !one.Consents || !two.Consents) return new(false, "living consenting parents required", null, null);
        // Martial equivalence exists only when an explicit world-law override enables it.
        bool Qualified(ReproductivePerson person) => person.CultivationRealm >= policy.MinimumRealm || policy.AllowMartialEquivalent && person.MartialStage == 10;
        if (!Qualified(one) || policy.RequireBothParents && !Qualified(two))
            return new(false, "both parents must satisfy the cultivation realm law", null, null);
        if (!policy.AllowBiologicalKin && context.BiologicalKin) return new(false, "biological kin disallowed by this explicit world policy", null, null);
        if ((one.Body.Reproduction.Functions & ReproductiveFunction.Gestator) == 0 || (two.Body.Reproduction.Functions & ReproductiveFunction.Donor) == 0)
            return new(false, "actual body reproductive functions are incompatible", null, null);
        if (one.AgeMonths < one.Body.Reproduction.MaturityMonths || two.AgeMonths < two.Body.Reproduction.MaturityMonths)
            return new(false, "body maturity not reached", null, null);
        if (one.Health < policy.MinimumHealth || two.Health < policy.MinimumHealth || one.AlreadyGestating || one.RecoveryUntilMonth > context.Month || !context.HasCareSupport)
            return new(false, "health, recovery or actual care support missing", null, null);
        var explicitRule = context.ExplicitCompatibility.FirstOrDefault(c =>
            c.GroupOne == one.Body.Reproduction.GroupId && c.GroupTwo == two.Body.Reproduction.GroupId ||
            c.GroupTwo == one.Body.Reproduction.GroupId && c.GroupOne == two.Body.Reproduction.GroupId);
        if (explicitRule is not null)
        {
            explicitRule.Source.Validate(); ArgumentException.ThrowIfNullOrWhiteSpace(explicitRule.ChildSpeciesId);
            return new(true, "explicit reproductive compatibility", explicitRule.ChildSpeciesId, one.Body.Reproduction.GestationMonths);
        }
        if (one.Body.SpeciesId != two.Body.SpeciesId || one.Body.Reproduction.GroupId != two.Body.Reproduction.GroupId)
            return new(false, "cross-species reproduction requires a defined compatibility rule", null, null);
        return new(true, "eligible; conception is a separate actual action", one.Body.SpeciesId, one.Body.Reproduction.GestationMonths);
    }
}
