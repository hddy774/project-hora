using System.Globalization;
using Chronicles.Core;

namespace Chronicles.Game;

public sealed record PersonView(long Id, string Name, string Realm, int? Layer, long? Age,
    string Status, int Place, int? Health, long? Money, string? Goal, string? Reason, string? Action, long Observed,
    long ParentOne, long ParentTwo, long Soul, int? Martial);
public sealed record PlaceView(int Id, int Province, string Name, string Terrain, int? Source, int? Qi,
    IReadOnlyList<string> Facilities, IReadOnlyList<int> Neighbors, long? Sovereign, long Observed);
public sealed record ItemView(long Id, long Owner, string Definition, string Name, int Quantity, string Quality,
    int? QualityValue, int? Damage, long Container, long Reserved, int Slot, int Capacity, string Source);
public sealed record GroupView(long Id, string Name, string Kind, long Leader, long Overlord, long? Money,
    IReadOnlyList<long> Members, IReadOnlyList<int> Provinces, IReadOnlyDictionary<string, int> Policies, bool Dissolved);
public sealed record ContractView(long Id, string Kind, long Seller, long Buyer, long Item, long Price, long Due, string Status, int Place);
public sealed record DetailView(PersonView Person, IReadOnlyList<int>? Roots, IReadOnlyList<int>? Aptitudes,
    IReadOnlyList<int>? Talents, IReadOnlyList<int>? Personality, IReadOnlyList<int>? Processes, IReadOnlyList<int>? Components,
    IReadOnlyList<Formation>? Formations, IReadOnlyDictionary<string, int>? Skills, IReadOnlyDictionary<string, int>? Understanding,
    IReadOnlyList<TraitState>? Traits, IReadOnlyList<Belief>? Beliefs, IReadOnlyList<Trace>? Traces, IReadOnlyList<long>? Combat);
public sealed record LawView(string Id, string Name, string Unit, long Minimum, long Maximum, long DefaultValue, long Value, bool Provisional, string Rationale);
public sealed record SoulView(long Id, long Parent, long Root, long CurrentPerson, string Status, int Route, string Evidence);
public sealed record GameView(string Title, string Version, long Month, long Revision, long Generation,
    long Actor, bool Observer, string Hash, IReadOnlyList<PersonView> People, IReadOnlyList<PlaceView> Places,
    IReadOnlyList<ItemView> Items, IReadOnlyList<GroupView> Groups, IReadOnlyList<ContractView> Contracts,
    IReadOnlyList<GameEvent> Events, IReadOnlyList<MonthlyCensus> Census, IReadOnlyList<LawView> Laws, DetailView? Detail,
    IReadOnlyList<SoulView> Souls, IReadOnlyList<PathDefinition> Paths);

public sealed partial class GameWorld
{
    public GameView View(long detailId = 0)
    {
        var observer = state.Controlled == 0;
        var actor = observer ? null : people[state.Controlled];
        var visible = observer ? state.People.ToArray() : actor!.KnownPeople.Where(id => actor.ObservedPeople.ContainsKey(id))
            .Append(actor.Id).Distinct().Select(id => people[id]).ToArray();
        PersonView Projection(Person p)
        {
            if (observer || p.Id == actor!.Id)
                return new(p.Id, p.Name, Catalog.RealmNames[(int)p.Realm], p.Layer, (Month - p.Born) / 12,
                    p.Status.ToString(), p.Place, p.Health, p.Money, p.Goal, p.Reason, p.Action.ToString(), Month,
                    p.ParentOne, p.ParentTwo, observer ? p.SoulId : 0, p.Martial);
            var belief = actor.ObservedPeople[p.Id];
            return new(p.Id, belief.Name, Catalog.RealmNames[(int)belief.Realm],
                belief.Layer, null, "지난 관측", belief.Place,
                null, null, null, null, null, belief.Month, 0, 0, 0, null);
        }
        var peopleView = visible.Select(Projection).OrderBy(p => p.Id).ToArray();
        var knownPlaces = observer ? state.Places : state.Places.Where(p => actor!.KnownPlaces.Contains(p.Id));
        var placesView = knownPlaces.Select(p =>
        {
            PlaceObservation? belief = actor is not null && actor.ObservedPlaces.TryGetValue(p.Id, out var seen) ? seen : null;
            return new PlaceView(p.Id, p.Province, p.Name, p.Terrain,
                observer ? p.Source : belief?.Source,
                observer ? p.Qi : belief?.Qi,
                observer ? p.Facilities.ToArray() : belief?.Facilities.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [],
                p.Neighbors.Where(id => observer || actor!.KnownPlaces.Contains(id)).ToArray(),
                observer ? p.Sovereign : null, observer ? Month : belief?.Month ?? 0);
        }).ToArray();
        var inventoryOwner = observer ? detailId : actor!.Id;
        var itemView = state.Items.Where(i => observer ? inventoryOwner == 0 || i.Owner == inventoryOwner : i.Owner == inventoryOwner &&
            (i.Place == -1 || i.Place == actor!.Place)).Select(i => new ItemView(i.Id, i.Owner, i.Definition,
                Catalog.Items[i.Definition].Name, i.Quantity, observer ? Catalog.Quality(i.Quality) : "미감정",
                observer ? i.Quality : null, observer ? i.Damage : null, i.Container, i.Reserved,
                Catalog.Items[i.Definition].EquipmentSlot, Catalog.Items[i.Definition].Capacity, observer ? i.Source : "소유 실물")).ToArray();
        var groupView = state.Groups.Where(g => observer || g.Members.Contains(actor!.Id)).Select(g => new GroupView(g.Id, g.Name,
            g.Kind.ToString(), g.Leader, g.Overlord, observer || g.Leader == actor!.Id ? g.Money : null,
            g.Members.Where(id => observer || id == actor!.Id || actor.KnownPeople.Contains(id)).ToArray(),
            g.Provinces.ToArray(), new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(new SortedDictionary<string, int>(g.Policies, StringComparer.Ordinal)), g.Dissolved)).ToArray();
        var contracts = state.Contracts.Where(c => observer || (c.Destination == actor!.Place && c.Status == "open") || c.Seller == actor!.Id || c.Buyer == actor!.Id)
            .Select(c => new ContractView(c.Id, c.Kind, c.Seller, c.Buyer, c.Item, c.Price, c.Due, c.Status, c.Destination)).ToArray();
        var events = observer ? state.Events.TakeLast(150).ToArray() : actor!.Journal.TakeLast(100).ToArray();
        var laws = observer ? Laws.All.Select(d => new LawView(d.Id, d.Name, d.Unit, d.Minimum, d.Maximum,
            d.DefaultValue, state.Laws[d.Id], d.Provisional, d.Rationale)).ToArray() : [];
        DetailView? detail = null;
        if (peopleView.FirstOrDefault(p => p.Id == detailId) is { } pv)
        {
            var p = people[detailId]; var permitted = observer || p.Id == actor!.Id;
            detail = new(pv, permitted ? p.Roots.ToArray() : null, permitted ? p.Aptitudes.ToArray() : null,
                permitted ? Enumerable.Range(0, 24).Select(i => Catalog.Talent(p, i)).ToArray() : null,
                permitted ? p.Personality.ToArray() : null, permitted ? p.Processes.ToArray() : null,
                permitted ? p.Components.ToArray() : null, permitted ? p.Formations.ToArray() : null,
                permitted ? new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(new SortedDictionary<string, int>(p.Skills, StringComparer.Ordinal)) : null,
                permitted ? new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(new SortedDictionary<string, int>(p.Understanding, StringComparer.Ordinal)) : null,
                permitted ? p.Traits.ToArray() : null, permitted ? p.Beliefs.Values.ToArray() : null,
                observer ? souls[p.SoulId].Traces.ToArray() : null, permitted ? CombatAttributes(p, items) : null);
        }
        return new(ProductInfo.KoreanTitle, ProductInfo.Version, Month, Revision, ViewGeneration, state.Controlled, observer,
            observer ? StateHash() : "", peopleView, placesView, itemView, groupView, contracts, events,
            observer ? state.Census.TakeLast(600).ToArray() : [], laws, detail,
            observer ? state.Souls.Select(s => new SoulView(s.Id, s.Parent, s.Root, s.CurrentPerson, s.Status, s.Route, s.Evidence)).ToArray() : [],
            Catalog.Paths.Where(path => observer || actor!.Understanding.GetValueOrDefault(path.Id) > 0).ToArray());
    }
}
