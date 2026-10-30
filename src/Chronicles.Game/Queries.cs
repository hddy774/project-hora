using System.Collections.ObjectModel;
using Chronicles.Core;
using K = Chronicles.Game.Knowledge;
using I = Chronicles.Game.Inventory;

namespace Chronicles.Game;

public sealed record PersonView(long Id, string Name, string Realm, int? Layer, long? Age,
    string Status, int Place, int? Health, long? Money, string? Goal, string? Reason, string? Action, long Observed,
    long ParentOne, long ParentTwo, long Soul, int? Martial)
{
    public long? Due { get; init; }
    public long? ActionTarget { get; init; }
    public string? ActionData { get; init; }
    public long? ActionRevision { get; init; }
    public bool? Irreversible { get; init; }
    public IReadOnlyList<PlanStep>? Plan { get; init; }
    public bool? GreatPerfection { get; init; }
    public long? Partner { get; init; }
    public long? Guardian { get; init; }
    public long? Household { get; init; }
    public long? Sect { get; init; }
    public long? Body { get; init; }
}
public sealed record PlaceView(int Id, int Province, string Name, string Terrain, int? Source, int? Qi,
    IReadOnlyList<string> Facilities, IReadOnlyList<int> Neighbors, long? Sovereign, long Observed)
{
    public long? Owner { get; init; }
    public long? Operator { get; init; }
    public long? Delegate { get; init; }
    public long? BlockedUntil { get; init; }
}
public sealed record ItemView(long Id, long Owner, string Definition, string Name, int Quantity, string Quality,
    int? QualityValue, int? Damage, long Container, long Reserved, int Slot, int Capacity, string Source)
{
    public int? Place { get; init; }
    public int? EquippedSlot { get; init; }
    public ItemDefect? Defects { get; init; }
    public long? BoundSoul { get; init; }
    public long? AppraisedMonth { get; init; }
    public IReadOnlyList<int>? EquippedSlots { get; init; }
}
public sealed record GroupView(long Id, string Name, string Kind, long Leader, long Overlord, long? Money,
    IReadOnlyList<long> Members, IReadOnlyList<int> Provinces, IReadOnlyDictionary<string, int> Policies, bool Dissolved)
{
    public IReadOnlyDictionary<string, int>? PendingPolicies { get; init; }
    public long? PolicyDue { get; init; }
    public long? PolicyEscrow { get; init; }
    public long? Debt { get; init; }
    public long? Jurisdiction { get; init; }
}
public sealed record ContractView(long Id, string Kind, long Seller, long Buyer, long Item, long Price, long Due, string Status, int Place)
{
    public long? Escrow { get; init; }
    public long? Carrier { get; init; }
    public string? ItemName { get; init; }
    public int? ItemQuantity { get; init; }
    public string? ItemQuality { get; init; }
}
public sealed record RecipeInputView(string Definition, string Name, int Quantity);
public sealed record RecipeView(string Id, string Name, string Skill, string Output, string OutputName, int Quantity, IReadOnlyList<RecipeInputView> Inputs);
public sealed record ResourceSourceView(long Id, int Place, string Definition, string Name, string Activity, long Remaining,
    long Observed, string Evidence, long? Capacity, int? Regeneration, string? Origin);
public sealed record KnowledgeActivityView(long Id, string Kind, string Status, long Actor, long Teacher,
    string WorkId, string WorkTitle, IReadOnlyList<int> Chapters, long Started, long Due, long? Ended,
    long MediumItem, string Source, IReadOnlyList<K.KnowledgeAdoption> Adopted, K.KnowledgePracticeOutcome? Outcome);
public sealed record PersonalKnowledgeDetail(IReadOnlyList<K.PersonalKnowledgeView> Chapters, int PersonalTotal,
    IReadOnlyList<K.AccessibleKnowledgeView> Media, int AccessibleTotal, IReadOnlyList<K.PersonalKnowledge> History,
    IReadOnlyList<K.PracticalKnowledge> PracticeHistory, IReadOnlyList<K.KnowledgeEvidence> Evidence,
    IReadOnlyList<KnowledgeActivityView> Activities, IReadOnlyList<K.KnowledgeWork> Works,
    IReadOnlyList<K.KnowledgeEdition>? Editions);
public sealed record DecisionView(string Goal, string Reason, string Action, long Due, int PlanExpansions,
    IReadOnlyList<PlanStep> Plan);
public sealed record DetailView(PersonView Person, IReadOnlyList<int>? Roots, IReadOnlyList<int>? Aptitudes,
    IReadOnlyList<int>? Talents, IReadOnlyList<int>? Personality, IReadOnlyList<int>? Processes, IReadOnlyList<int>? Components,
    IReadOnlyList<Formation>? Formations, IReadOnlyDictionary<string, int>? Skills, IReadOnlyDictionary<string, int>? Understanding,
    IReadOnlyList<TraitState>? Traits, IReadOnlyList<Belief>? Beliefs, IReadOnlyList<Trace>? Traces, IReadOnlyList<long>? Combat)
{
    public PersonalKnowledgeDetail? Knowledge { get; init; }
    public DecisionView? Decision { get; init; }
}
public sealed record LawView(string Id, string Name, string Unit, long Minimum, long Maximum, long DefaultValue, long Value, bool Provisional, string Rationale);
public sealed record SoulView(long Id, long Parent, long Root, long CurrentPerson, string Status, int Route, string Evidence);
public sealed record GameView(string Title, string Version, long Month, long Revision, long Generation,
    long Actor, bool Observer, string Hash, IReadOnlyList<PersonView> People, IReadOnlyList<PlaceView> Places,
    IReadOnlyList<ItemView> Items, IReadOnlyList<GroupView> Groups, IReadOnlyList<ContractView> Contracts,
    IReadOnlyList<GameEvent> Events, IReadOnlyList<MonthlyCensus> Census, IReadOnlyList<LawView> Laws, DetailView? Detail,
    IReadOnlyList<SoulView> Souls, IReadOnlyList<PathDefinition> Paths)
{
    // Names are set only together with values from the canonical eight-axis projection.
    public IReadOnlyList<string>? CombatAxisNames { get; init; }
    public IReadOnlyList<LawChange> LawHistory { get; init; } = [];
    public IReadOnlyList<RecipeView> Recipes { get; init; } = [];
    public IReadOnlyList<ResourceSourceView> ResourceSources { get; init; } = [];
}

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
                    p.ParentOne, p.ParentTwo, observer ? p.SoulId : 0, p.Martial)
                {
                    Due = p.Due, ActionTarget = p.ActionTarget, ActionData = p.ActionData, ActionRevision = p.ActionRevision,
                    Irreversible = p.Irreversible, Plan = Array.AsReadOnly(p.Plan.ToArray()), GreatPerfection = p.GreatPerfection,
                    Partner = p.Partner, Guardian = p.Guardian, Household = p.Household, Sect = p.Sect, Body = p.BodyId
                };
            var belief = actor.ObservedPeople[p.Id];
            return new(p.Id, belief.Name, Catalog.RealmNames[(int)belief.Realm],
                belief.Layer, null, "지난 관측", belief.Place,
                null, null, null, null, null, belief.Month, 0, 0, 0, null);
        }
        var peopleView = visible.Select(Projection).OrderBy(p => p.Id).ToArray();
        var visibleIds = peopleView.Select(p => p.Id).ToHashSet();
        var knownPlaces = observer ? state.Places : state.Places.Where(p => actor!.KnownPlaces.Contains(p.Id));
        var placesView = knownPlaces.Select(p =>
        {
            PlaceObservation? belief = actor is not null && actor.ObservedPlaces.TryGetValue(p.Id, out var seen) ? seen : null;
            return new PlaceView(p.Id, p.Province, p.Name, p.Terrain,
                observer ? p.Source : belief?.Source, observer ? p.Qi : belief?.Qi,
                observer ? Array.AsReadOnly(p.Facilities.ToArray()) : Array.AsReadOnly(belief?.Facilities.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? []),
                Array.AsReadOnly(p.Neighbors.Where(id => observer || actor!.KnownPlaces.Contains(id)).ToArray()),
                observer ? p.Sovereign : belief?.Sovereign, observer ? Month : belief?.Month ?? 0)
            {
                Owner = observer ? p.Owner : belief?.Owner, Operator = observer ? p.Operator : belief?.Operator,
                Delegate = observer ? p.Delegate : belief?.Delegate, BlockedUntil = observer ? p.BlockedUntil : belief?.BlockedUntil
            };
        }).ToArray();
        var inventoryOwner = observer ? detailId : actor!.Id;
        var itemView = state.Items.Where(i => observer ? inventoryOwner == 0 || i.Owner == inventoryOwner : i.Owner == inventoryOwner &&
            (i.Place == -1 || i.Place == actor!.Place)).Select(i =>
        {
            var definition = I.InventoryDefinitions.Definition(i.Definition).Item;
            var equippedSlots = people.TryGetValue(i.Owner, out var owner)
                ? owner.Equipment.Where(e => e.Value == i.Id).Select(e => e.Key).Order().ToArray() : [];
            var appraisal = observer ? null : state.Inventory.Appraisals.Where(a => a.Appraiser == actor!.Id && a.Item == i.Id && a.Month <= Month)
                .OrderByDescending(a => a.Month).FirstOrDefault();
            var binding = observer ? null : state.Inventory.Bindings.FirstOrDefault(b => b.Item == i.Id && b.Soul == actor!.SoulId && b.Established <= Month);
            return new ItemView(i.Id, i.Owner, i.Definition, definition.Name, i.Quantity,
                observer ? Catalog.Quality(i.Quality) : appraisal is null ? "미감정" : Catalog.Quality(appraisal.EstimatedQuality),
                observer ? i.Quality : appraisal?.EstimatedQuality, observer ? i.Damage : appraisal?.Damage,
                i.Container, i.Reserved, definition.EquipmentSlot, definition.Capacity,
                observer ? i.Source : "소유 실물")
            {
                Place = i.Place, EquippedSlot = equippedSlots.Length == 0 ? -1 : equippedSlots[0], EquippedSlots = Array.AsReadOnly(equippedSlots),
                // Physical ownership is not appraisal evidence; personal measurements stay at their actual observation date.
                Defects = observer ? i.Defects : appraisal?.DetectedDefects, BoundSoul = observer ? i.BoundSoul : binding?.Soul,
                AppraisedMonth = observer ? Month : appraisal?.Month
            };
        }).ToArray();
        var groupView = state.Groups.Where(g => observer || g.Members.Contains(actor!.Id)).Select(g =>
        {
            var finance = observer || g.Leader == actor!.Id;
            return new GroupView(g.Id, g.Name, g.Kind.ToString(), g.Leader, g.Overlord, finance ? g.Money : null,
                Array.AsReadOnly(g.Members.Where(id => observer || visibleIds.Contains(id)).ToArray()), Array.AsReadOnly(g.Provinces.ToArray()),
                ReadOnly(g.Policies), g.Dissolved)
            {
                PendingPolicies = finance ? ReadOnly(g.PendingPolicies) : null, PolicyDue = finance ? g.PolicyDue : null,
                PolicyEscrow = finance ? g.PolicyEscrow : null, Debt = finance ? g.Debt : null, Jurisdiction = finance ? g.Jurisdiction : null
            };
        }).ToArray();
        var contracts = state.Contracts.Where(c => observer || (c.Destination == actor!.Place && c.Status == "open") ||
            c.Seller == actor!.Id || c.Buyer == actor!.Id).Select(c =>
        {
            var party = observer || c.Seller == actor!.Id || c.Buyer == actor.Id || c.Carrier == actor.Id;
            // Current public listings are visible. Closed contracts never inspect a counterparty's later private inventory.
            var listing = observer || c.Status == "open" && (c.Destination == actor!.Place || c.Seller == actor.Id);
            var offered = listing ? items.GetValueOrDefault(c.Item) : null;
            return new ContractView(c.Id, c.Kind, c.Seller, c.Buyer, c.Item, c.Price, c.Due, c.Status, c.Destination)
            {
                Escrow = party ? c.Escrow : null, Carrier = party ? c.Carrier : null,
                ItemName = offered is null ? null : I.InventoryDefinitions.Definition(offered.Definition).Item.Name,
                ItemQuantity = offered?.Quantity,
                ItemQuality = offered is null ? null : observer ? Catalog.Quality(offered.Quality) : "미감정"
            };
        }).ToArray();
        var events = observer ? state.Events.TakeLast(150).ToArray() : actor!.Journal.TakeLast(100).ToArray();
        var laws = observer ? Laws.All.Select(d => new LawView(d.Id, d.Name, d.Unit, d.Minimum, d.Maximum,
            d.DefaultValue, state.Laws[d.Id], d.Provisional, d.Rationale)).ToArray() : [];
        DetailView? detail = null;
        if (peopleView.FirstOrDefault(p => p.Id == detailId) is { } pv)
        {
            var p = people[detailId]; var permitted = observer || p.Id == actor!.Id;
            detail = new(pv, permitted ? Array.AsReadOnly(p.Roots.ToArray()) : null, permitted ? Array.AsReadOnly(p.Aptitudes.ToArray()) : null,
                permitted ? Array.AsReadOnly(Enumerable.Range(0, 24).Select(i => Catalog.Talent(p, i)).ToArray()) : null,
                permitted ? Array.AsReadOnly(p.Personality.ToArray()) : null, permitted ? Array.AsReadOnly(p.Processes.ToArray()) : null,
                permitted ? Array.AsReadOnly(p.Components.ToArray()) : null, permitted ? Array.AsReadOnly(p.Formations.ToArray()) : null,
                permitted ? ReadOnly(p.Skills) : null, permitted ? ReadOnly(p.Understanding) : null,
                permitted ? Array.AsReadOnly(p.Traits.ToArray()) : null, permitted ? Array.AsReadOnly(p.Beliefs.Values.ToArray()) : null,
                observer ? Array.AsReadOnly(souls[p.SoulId].Traces.ToArray()) : null, permitted ? Array.AsReadOnly(CombatAttributes(p, items)) : null)
            {
                Knowledge = permitted ? PersonalKnowledgeFor(p, observer) : null,
                Decision = permitted ? new(p.Goal, p.Reason, p.Action.ToString(), p.Due, p.PlanExpansions, Array.AsReadOnly(p.Plan.ToArray())) : null
            };
        }
        return new(ProductInfo.KoreanTitle, ProductInfo.Version, Month, Revision, ViewGeneration, state.Controlled, observer,
            observer ? StateHash() : "", peopleView, placesView, itemView, groupView, contracts, events,
            observer ? state.Census.TakeLast(600).ToArray() : [], laws, detail,
            observer ? state.Souls.Select(s => new SoulView(s.Id, s.Parent, s.Root, s.CurrentPerson, s.Status, s.Route, s.Evidence)).ToArray() : [],
            Catalog.Paths.Where(path => observer || actor!.Understanding.GetValueOrDefault(path.Id) > 0).ToArray())
        {
            LawHistory = observer ? Array.AsReadOnly(state.LawHistory.ToArray()) : [],
            Recipes = Array.AsReadOnly(I.InventoryDefinitions.Recipes.Values.OrderBy(r => r.Id, StringComparer.Ordinal).Select(r =>
                new RecipeView(r.Id, Catalog.Recipes.FirstOrDefault(legacy => legacy.Id == r.Id)?.Name ?? I.InventoryDefinitions.Definition(r.Output).Item.Name + " 제작",
                    r.Skill, r.Output, I.InventoryDefinitions.Definition(r.Output).Item.Name, r.Quantity,
                    Array.AsReadOnly(r.Inputs.OrderBy(input => input.Key, StringComparer.Ordinal).Select(input =>
                        new RecipeInputView(input.Key, I.InventoryDefinitions.Definition(input.Key).Item.Name, input.Value)).ToArray()))).ToArray()),
            ResourceSources = ResourceSourcesFor(actor, observer)
        };
    }

    private ReadOnlyCollection<ResourceSourceView> ResourceSourcesFor(Person? actor, bool observer)
    {
        if (observer) return Array.AsReadOnly(state.Inventory.Sources.OrderBy(s => s.Id).Select(s =>
            new ResourceSourceView(s.Id, s.Place, s.Definition, I.InventoryDefinitions.Definition(s.Definition).Item.Name,
                s.Activity, s.Remaining, Month, s.Origin, s.Capacity, s.Regeneration, s.Origin)).ToArray());
        var sources = state.Inventory.Sources.ToDictionary(s => s.Id);
        return Array.AsReadOnly(state.Inventory.Observations.Where(o => o.Observer == actor!.Id && o.Month <= Month)
            .GroupBy(o => o.Source).Select(g => g.OrderByDescending(o => o.Month).First()).Where(o => sources.ContainsKey(o.Source))
            .OrderBy(o => o.Source).Select(o =>
            {
                var source = sources[o.Source];
                return new ResourceSourceView(o.Source, o.Place, source.Definition, I.InventoryDefinitions.Definition(source.Definition).Item.Name,
                    source.Activity, o.Remaining, o.Month, o.Evidence, null, null, null);
            }).ToArray());
    }

    private static ReadOnlyDictionary<string, int> ReadOnly(IDictionary<string, int> values) =>
        new ReadOnlyDictionary<string, int>(new SortedDictionary<string, int>(values, StringComparer.Ordinal));

    private PersonalKnowledgeDetail PersonalKnowledgeFor(Person p, bool observer)
    {
        // The caller has already checked observer/self authority. Global module revision is intentionally omitted:
        // another person's unseen learning must not change this person's otherwise identical projection.
        var authority = K.KnowledgeAuthority.Restore(state.Knowledge);
        var personal = authority.ViewPersonal(p.Id, limit: 128);
        var accessible = authority.ViewAccessible(p.Id, p.Place, Month, limit: 128);
        var workIds = personal.Rows.Select(r => r.WorkId).Concat(accessible.Rows.Select(r => r.WorkId)).ToHashSet(StringComparer.Ordinal);
        var history = state.Knowledge.Memories.Where(m => m.PersonId == p.Id).OrderBy(m => m.Revision).ToArray();
        var practice = state.Knowledge.Mastery.Where(m => m.PersonId == p.Id).OrderBy(m => m.Revision).ToArray();
        var evidence = state.Knowledge.Evidence.Where(e => e.ActorId == p.Id || e.OtherPersonId == p.Id)
            .OrderBy(e => e.Revision).Select(e => e with { Chapters = e.Chapters.ToArray() }).ToArray();
        var activities = state.Knowledge.Activities.Where(a => a.ActorId == p.Id || a.TeacherId == p.Id).OrderBy(a => a.Id)
            .Select(a => new KnowledgeActivityView(a.Id, a.Kind.ToString(), a.Status.ToString(), a.ActorId, a.TeacherId,
                a.WorkId, state.Knowledge.Works.First(w => w.Id == a.WorkId).Title, Array.AsReadOnly(a.Chapters.ToArray()),
                a.StartedMonth, a.DueMonth, a.Status == K.KnowledgeActivityStatus.Active ? null : a.EndedMonth,
                a.MediumItemId, a.SourceId, Array.AsReadOnly(a.Adopted.Where(k => observer || k.PersonId == p.Id).ToArray()), a.PracticeOutcome)).ToArray();
        var works = state.Knowledge.Works.Where(w => workIds.Contains(w.Id)).Select(w => w with { Chapters = w.Chapters.ToArray() }).ToArray();
        var editions = observer ? state.Knowledge.Editions.Where(e => workIds.Contains(e.WorkId))
            .Select(e => e with { Chapters = e.Chapters.ToArray() }).ToArray() : null;
        return new(personal.Rows, personal.Total, accessible.Rows, accessible.Total, Array.AsReadOnly(history), Array.AsReadOnly(practice),
            Array.AsReadOnly(evidence), Array.AsReadOnly(activities), Array.AsReadOnly(works), editions is null ? null : Array.AsReadOnly(editions));
    }
}
