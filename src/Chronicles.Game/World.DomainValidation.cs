namespace Chronicles.Game;

public sealed partial class GameWorld
{
    internal static void ValidateDomainState(GameState snapshot)
    {
        var world = new GameWorld(snapshot);
        world.ValidateInventoryBridge();
        world.ValidatePeopleDomain();
        Knowledge.KnowledgeAuthority.Validate(snapshot.Knowledge);
        if (snapshot.Knowledge.Month > snapshot.Month || snapshot.Knowledge.Works.Count == 0)
            throw new InvalidDataException("Knowledge clock/schema does not match its owning world.");
        if (snapshot.PeopleDomain.Profiles.Length != snapshot.People.Count || snapshot.PeopleDomain.Souls.Length != snapshot.Souls.Count)
            throw new InvalidDataException("People profiles and souls must correspond to their real world identities.");
        var politicalErrors = Politics.PoliticalInvariants.Validate(world.PoliticsContext(), snapshot.Politics);
        if (politicalErrors.Count > 0) throw new InvalidDataException(string.Join("; ", politicalErrors));
        foreach (var person in snapshot.People)
        {
            if (!snapshot.Cultivation.TryGetValue(person.BodyId, out var metadata))
                throw new InvalidDataException("Missing cultivation state for actual body.");
            Cultivation.CultivationInvariants.Validate(metadata, world.CultivationActorFor(person), world.CultivationPolicyForWorld());
        }
        if (snapshot.Cultivation.Count != snapshot.People.Count || snapshot.CultivationSupply.Any(x =>
            x.Key < 0 || x.Key >= snapshot.Places.Count || !x.Value.Nonnegative))
            throw new InvalidDataException("Cultivation owner/source stock mismatch.");
        foreach (var medium in snapshot.Knowledge.Media.Where(m => !m.Destroyed))
            if (!world.items.TryGetValue(medium.ItemId, out var item) || item.Owner != medium.OwnerId ||
                Catalog.Items[item.Definition].Kind != ItemKind.Book)
                throw new InvalidDataException("Knowledge medium must refer to its real physical book and current owner.");
    }
}
