using System.Collections.ObjectModel;
using Chronicles.Core;
using K = Chronicles.Game.Knowledge;
using I = Chronicles.Game.Inventory;
using P = Chronicles.Game.People;
using C = Chronicles.Game.Cultivation;

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
    public string? GuardTargetKind { get; init; }
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
public sealed record InventoryActivityView(long Id, string Kind, string Name, string Status, int Place, long? Started, long Due, string? OutputName, int OutputQuantity);
public sealed record SpeciesView(string Id, string Name, string NaturalForm, long BasicLifespanMonths, long MaturityMonths,
    IReadOnlyList<string> Habitats, IReadOnlyList<string> ResourceNeeds);
public sealed record KnowledgeActivityView(long Id, string Kind, string Status, long Actor, long Teacher,
    string WorkId, string WorkTitle, IReadOnlyList<int> Chapters, long Started, long Due, long? Ended,
    long MediumItem, string Source, IReadOnlyList<K.KnowledgeAdoption> Adopted, K.KnowledgePracticeOutcome? Outcome);
public sealed record AccessibleBookView(long ItemId, long EditionId, string WorkId, string Title, int? Quality,
    K.KnowledgeRights Rights, int? ReadableChapterCount, bool? Unreadable, long? AppraisedMonth);
public sealed record PersonalKnowledgeDetail(IReadOnlyList<K.PersonalKnowledgeView> Chapters, int PersonalTotal,
    IReadOnlyList<AccessibleBookView> Media, int AccessibleTotal, IReadOnlyList<K.PersonalKnowledge> History,
    IReadOnlyList<K.PracticalKnowledge> PracticeHistory, IReadOnlyList<K.KnowledgeEvidence> Evidence,
    IReadOnlyList<KnowledgeActivityView> Activities, IReadOnlyList<K.KnowledgeWork> Works,
    IReadOnlyList<K.KnowledgeEdition>? Editions);
public sealed record DecisionView(string Goal, string Reason, string Action, long Due, int PlanExpansions,
    IReadOnlyList<PlanStep> Plan)
{
    public int Mood { get; init; }
    public long LastCare { get; init; }
    public IReadOnlyDictionary<string, int>? Habits { get; init; }
    public IReadOnlyList<DecisionCandidate> Candidates { get; init; } = [];
    public IReadOnlyList<DecisionRecord> History { get; init; } = [];
}
public sealed record RootProgressDetail(string Element, int? InnateQuality, long Growth, long Required, int Layer, bool Complete, int Damage);
public sealed record FormationKnowledgeDetail(string WorkId, string EditionId, long Revision, int Understanding,
    int Practice, int? Error, bool Memorized, bool CanConsult, string Source);
public sealed record CultivationFormationDetail(Realm Realm, string Path, int? Quality, IReadOnlyList<C.FormationFactor>? Factors,
    long Month, int Place, long LawRevision, FormationKnowledgeDetail? Knowledge, C.ParentStructureEvidence? Parent, string Source);
public sealed record CultivationDetail(IReadOnlyList<RootProgressDetail> Roots, IReadOnlyList<C.CultivationStructureView> Current,
    CultivationFormationDetail? Formation, int Maturity, int Damage, int Load, string Path, string Auxiliary,
    C.AttemptPhase? Phase, C.PendingTribulation? Trial);
public sealed record PotentialRouteDetail(string Path, Realm TargetRealm, int? ParentQualityCarried, int CurrentMaturityCarried,
    long? MinimumSupplyToStart, IReadOnlyList<C.CultivationBlocker>? Missing, string Condition);
public sealed record CultivationPotentialDetail(int StructuralHeadroom, int CurrentDamage, bool? HasActualRecoverySupply,
    IReadOnlyList<PotentialRouteDetail> KnownNextRoutes, string InformationBasis);
public sealed record DetailView(PersonView Person, IReadOnlyList<int>? Roots, IReadOnlyList<int>? Aptitudes,
    IReadOnlyList<int>? Talents, IReadOnlyList<int>? Personality, IReadOnlyList<int>? Processes, IReadOnlyList<int>? Components,
    IReadOnlyList<Formation>? Formations, IReadOnlyDictionary<string, int>? Skills, IReadOnlyDictionary<string, int>? Understanding,
    IReadOnlyList<TraitState>? Traits, IReadOnlyList<Belief>? Beliefs, IReadOnlyList<Trace>? Traces, IReadOnlyList<long>? Combat)
{
    public PersonalKnowledgeDetail? Knowledge { get; init; }
    public IReadOnlyList<InventoryActivityView>? InventoryActivities { get; init; }
    public DecisionView? Decision { get; init; }
    public P.PeopleDetailView? People { get; init; }
    public CultivationDetail? Cultivation { get; init; }
    public CultivationPotentialDetail? Potential { get; init; }
    public C.CombatProjection? CombatProjection { get; init; }
    public IReadOnlyList<SocialRelation> SocialRelations { get; init; } = [];
    public P.PeopleEcologyObservation? PeopleEcology { get; init; }
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
    public IReadOnlyDictionary<string, string>? TraitNames { get; init; }
    public IReadOnlyList<RecipeView> Recipes { get; init; } = [];
    public IReadOnlyList<ResourceSourceView> ResourceSources { get; init; } = [];
    public Ecology.EconomyView? Economy { get; init; }
    public Currency.CurrencyExchangeInfo? CurrencyExchange { get; init; }
    public IReadOnlyList<SpeciesView> Species { get; init; } = [];
    public IReadOnlyDictionary<string, string>? ItemNames { get; init; }
    public IReadOnlyList<P.PeopleRequestView> PeopleRequests { get; init; } = [];
    public IReadOnlyList<P.PeopleOathView> KnownOaths { get; init; } = [];
    public IReadOnlyList<P.ReputationClaim> ReceivedClaims { get; init; } = [];
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
                    Partner = p.Partner, Guardian = p.Guardian, Household = p.Household, Sect = p.Sect, Body = p.BodyId,
                    GuardTargetKind = p.Action != ActionKind.Guard || p.ActionTarget == 0 ? "none" :
                        p.ActionData.StartsWith("combat:", StringComparison.Ordinal) ? "receipt" : "person"
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
            var advertised = state.Inventory.Contracts.FirstOrDefault(known => known.Id == c.Id);
            var offered = listing ? items.GetValueOrDefault(c.Item) : null;
            return new ContractView(c.Id, c.Kind, c.Seller, c.Buyer, c.Item, c.Price, c.Due, c.Status, c.Destination)
            {
                Escrow = party ? c.Escrow : null, Carrier = party ? c.Carrier : null,
                ItemName = advertised?.AdvertisedName.Length > 0 ? advertised.AdvertisedName : offered is null ? null : I.InventoryDefinitions.Definition(offered.Definition).Item.Name,
                ItemQuantity = advertised?.AdvertisedQuantity > 0 ? advertised.AdvertisedQuantity : offered?.Quantity,
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
            var innate = permitted && (observer || !state.PeopleDomain.Profiles.Any(profile => profile.PersonId == p.Id));
            var hasCultivation = permitted && state.Cultivation.ContainsKey(p.BodyId);
            var inspection = hasCultivation ? CultivationInspectionFor(p) : null;
            var canonicalCombat = hasCultivation ? CultivationCombatFor(p, observer) : null;
            detail = new(pv, innate ? Array.AsReadOnly(p.Roots.ToArray()) : null, innate ? Array.AsReadOnly(p.Aptitudes.ToArray()) : null,
                innate ? Array.AsReadOnly(Enumerable.Range(0, 24).Select(i => Catalog.Talent(p, i)).ToArray()) : null,
                permitted ? Array.AsReadOnly(p.Personality.ToArray()) : null, permitted ? Array.AsReadOnly(p.Processes.ToArray()) : null,
                innate ? Array.AsReadOnly(p.Components.ToArray()) : null, innate ? Array.AsReadOnly(p.Formations.ToArray()) : null,
                permitted ? ReadOnly(p.Skills) : null, permitted ? ReadOnly(p.Understanding) : null,
                innate ? Array.AsReadOnly(p.Traits.ToArray()) : null, permitted ? Array.AsReadOnly(p.Beliefs.Values.ToArray()) : null,
                observer ? Array.AsReadOnly(souls[p.SoulId].Traces.ToArray()) : null, canonicalCombat is null ? null : Array.AsReadOnly(Enumerable.Range(0, 8).Select(canonicalCombat.Axes.At).ToArray()))
            {
                Knowledge = permitted ? PersonalKnowledgeFor(p, observer) : null,
                InventoryActivities = permitted ? Array.AsReadOnly(state.Inventory.Jobs.Where(job => job.Actor == p.Id).OrderBy(job => job.Id)
                    .Select(job => new InventoryActivityView(job.Id, job.Recipe,
                        I.InventoryDefinitions.Recipes.TryGetValue(job.Recipe, out var recipe) ? I.InventoryDefinitions.Definition(recipe.Output).Item.Name + " 제작" :
                        job.Recipe switch { "extraction" => "자원 채취", "repair" => "실물 수리", "appraisal" => "실물 감정", "exploration" => "원천 답사", "medical-treatment" => "의료", _ => "생업 작업" },
                        job.Status, job.Place, state.Inventory.Labor.FirstOrDefault(l => l.Id == job.Labor)?.Start,
                        job.Due, job.OutputDefinition.Length > 0 ? I.InventoryDefinitions.Definition(job.OutputDefinition).Item.Name : null, job.OutputQuantity)).ToArray()) : null,
                Decision = permitted ? new(p.Goal, p.Reason, p.Action.ToString(), p.Due, p.PlanExpansions, Array.AsReadOnly(p.Plan.ToArray()))
                {
                    Mood = p.Mood, LastCare = p.LastCare, Habits = ReadOnly(p.Habits),
                    Candidates = Array.AsReadOnly(p.DecisionCandidates.ToArray()),
                    History = Array.AsReadOnly(p.Decisions.Select(d => d with { Candidates = Array.AsReadOnly(d.Candidates.ToArray()) }).ToArray())
                } : null,
                People = PeopleDetail(p.Id),
                Cultivation = inspection is null ? null : ProjectCultivation(inspection, observer),
                Potential = inspection is null ? null : ProjectPotential(p, inspection, observer),
                CombatProjection = canonicalCombat,
                PeopleEcology = permitted ? PeopleEcology(p.Id) : null,
                SocialRelations = Array.AsReadOnly(state.Relations.Where(r => r.Month <= Month && (observer ? r.Observer == p.Id :
                    r.Observer == actor!.Id && actor.KnownPeople.Contains(r.Other) && (p.Id == actor.Id || r.Other == p.Id))).ToArray())
            };
        }
        var projection = new GameView(ProductInfo.KoreanTitle, ProductInfo.Version, Month, Revision, ViewGeneration, state.Controlled, observer,
            observer ? StateHash() : "", peopleView, placesView, itemView, groupView, contracts, events,
            observer ? state.Census.TakeLast(600).ToArray() : [], laws, detail,
            observer ? state.Souls.Select(s => new SoulView(s.Id, s.Parent, s.Root, s.CurrentPerson, s.Status, s.Route, s.Evidence)).ToArray() : [],
            Catalog.Paths.Where(path => observer || actor!.Understanding.GetValueOrDefault(path.Id) > 0).ToArray())
        {
            LawHistory = observer ? Array.AsReadOnly(state.LawHistory.ToArray()) : [],
            // Every combat value exposed here comes from the canonical projection. Public labels must not
            // depend on which detail is open: navigation is excluded from the personal command token.
            CombatAxisNames = C.CultivationQueries.AxisNames,
            TraitNames = new ReadOnlyDictionary<string, string>(P.TraitCatalog.All.ToDictionary(t => t.Id, t => t.Name, StringComparer.Ordinal)),
            Recipes = Array.AsReadOnly(I.InventoryDefinitions.Recipes.Values.OrderBy(r => r.Id, StringComparer.Ordinal).Select(r =>
                new RecipeView(r.Id, Catalog.Recipes.FirstOrDefault(legacy => legacy.Id == r.Id)?.Name ?? I.InventoryDefinitions.Definition(r.Output).Item.Name + " 제작",
                    r.Skill, r.Output, I.InventoryDefinitions.Definition(r.Output).Item.Name, r.Quantity,
                    Array.AsReadOnly(r.Inputs.OrderBy(input => input.Key, StringComparer.Ordinal).Select(input =>
                        new RecipeInputView(input.Key, I.InventoryDefinitions.Definition(input.Key).Item.Name, input.Value)).ToArray()))).ToArray()),
            ResourceSources = ResourceSourcesFor(actor, observer),
            Economy = observer || actor!.Status == LifeStatus.Alive ? EconomyDetail() : null,
            CurrencyExchange = CurrencyExchangeInfo(),
            Species = Array.AsReadOnly(P.SpeciesCatalog.All.Select(s => new SpeciesView(s.Id, s.Name, s.NaturalForm.ToString(),
                s.BasicLifespanMonths, s.MaturityMonths, Array.AsReadOnly(s.Habitats.Select(h => h.ToString()).ToArray()),
                Array.AsReadOnly(s.ResourceNeeds.ToArray()))).ToArray()),
            ItemNames = new ReadOnlyDictionary<string, string>(I.InventoryDefinitions.Items.ToDictionary(i => i.Key, i => i.Value.Item.Name, StringComparer.Ordinal)),
            // Newly delivered personal requests and evidence participate in the perception token even when no detail is open.
            PeopleRequests = Array.AsReadOnly(PeopleRequests(observer ? 0 : actor!.Id)),
            KnownOaths = Array.AsReadOnly(PeopleKnownOaths(observer ? 0 : actor!.Id)),
            ReceivedClaims = Array.AsReadOnly(PeopleReceivedClaims(observer ? 0 : actor!.Id))
        };
        return observer ? projection : projection with { Revision = PerceptionRevision(projection) };
    }

    private static CultivationDetail ProjectCultivation(C.CultivationInspection source, bool observer)
    {
        var roots = source.Roots.Select(r => new RootProgressDetail(r.Element, observer ? r.InnateQuality : null,
            r.Growth, r.Required, r.Layer, r.Complete, r.Damage)).ToArray();
        var formed = source.Formation;
        var known = formed?.Knowledge;
        var formation = formed is null ? null : new CultivationFormationDetail(formed.Realm, formed.Path, observer ? formed.Quality : null,
            observer ? Array.AsReadOnly(formed.Factors.Select(f => f with { }).ToArray()) : null,
            formed.Month, formed.Place, formed.LawRevision, known is null ? null : new(known.WorkId, known.EditionId, known.Revision,
                known.Understanding, known.Practice, observer ? known.Error : null, known.Memorized, known.CanConsult, known.Source),
            observer && formed.Parent is not null ? formed.Parent with { } : null, formed.Source);
        return new(Array.AsReadOnly(roots), Array.AsReadOnly(source.Current.Select(c => c with { }).ToArray()), formation,
            source.Maturity, source.Damage, source.Load, source.Path, source.Auxiliary, source.Phase, source.Trial is null ? null : source.Trial with { });
    }

    private CultivationPotentialDetail ProjectPotential(Person p, C.CultivationInspection inspection, bool observer)
    {
        if (observer)
        {
            var actual = CultivationPotentialFor(p);
            return new(actual.StructuralHeadroom, actual.CurrentDamage, actual.HasActualRecoverySupply,
                Array.AsReadOnly(actual.KnownNextRoutes.Select(r => new PotentialRouteDetail(r.Path, r.TargetRealm, r.ParentQualityCarried,
                    r.CurrentMaturityCarried, r.MinimumSupplyToStart, Array.AsReadOnly(r.Missing.Select(m => m with { }).ToArray()), r.Condition)).ToArray()),
                actual.InformationBasis);
        }
        // Known definitions and one's own completed structure are legitimate personal inputs. Do not run an actual-site
        // preflight merely to redact its result: hidden supply/occupancy/rights must not determine personal recommendations.
        var known = state.Knowledge.Memories.Where(m => m.PersonId == p.Id && m.Decoding > 0 && m.Comprehension > 0)
            .Select(m => m.WorkId).ToHashSet(StringComparer.Ordinal);
        var routes = p.Realm == Realm.HalfImmortal ? [] : Catalog.Paths.Where(path => path.Realm == p.Realm + 1 && known.Contains(path.Id))
            .Select(path => new PotentialRouteDetail(path.Id, path.Realm, null, inspection.Maturity, null, null,
                C.CultivationProfiles.Find(path.Id)?.NextRequirement ?? "배운 경로의 조건을 실제 관측으로 확인해야 합니다.")).ToArray();
        return new(10000 - inspection.Maturity, inspection.Damage, null, Array.AsReadOnly(routes),
            "자기 구조와 실제 학습한 경로 정의 기준·미관측 공급/자리/권리/미래 결과는 미상");
    }

    private ReadOnlyCollection<ResourceSourceView> ResourceSourcesFor(Person? actor, bool observer)
    {
        if (observer) return Array.AsReadOnly(state.Inventory.Sources.OrderBy(s => s.Id).Select(s =>
            new ResourceSourceView(s.Id, s.Place, s.Definition, I.InventoryDefinitions.Definition(s.Definition).Item.Name,
                s.Activity, s.Remaining, Month, s.Origin, s.Capacity, s.Regeneration, s.Origin)).ToArray());
        return Array.AsReadOnly(state.Inventory.Observations.Where(o => o.Observer == actor!.Id && o.Month <= Month)
            .GroupBy(o => o.Source).Select(g => g.OrderByDescending(o => o.Month).First()).OrderBy(o => o.Source)
            .Select(o => new ResourceSourceView(o.Source, o.Place, o.Definition,
                I.InventoryDefinitions.Items.TryGetValue(o.Definition, out var known) ? known.Item.Name : "원천 종류 미상·재탐사 필요",
                o.Activity, o.Remaining, o.Month, o.Evidence, null, null, null)).ToArray());
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
        var media = accessible.Rows.Select(m =>
        {
            var appraisal = observer ? null : state.Inventory.Appraisals.Where(a => a.Appraiser == p.Id && a.Item == m.ItemId && a.Month <= Month)
                .OrderByDescending(a => a.Month).FirstOrDefault();
            int? quality = observer ? m.Quality : appraisal is null ? null : Array.IndexOf(Catalog.QualityNames.ToArray(), Catalog.Quality(appraisal.EstimatedQuality));
            return new AccessibleBookView(m.ItemId, m.EditionId, m.WorkId, m.Title, quality, m.Rights,
                observer ? m.ReadableChapterCount : null, observer ? m.Unreadable : null, observer ? Month : appraisal?.Month);
        }).ToArray();
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
        return new(personal.Rows, personal.Total, Array.AsReadOnly(media), accessible.Total, Array.AsReadOnly(history), Array.AsReadOnly(practice),
            Array.AsReadOnly(evidence), Array.AsReadOnly(activities), Array.AsReadOnly(works), editions is null ? null : Array.AsReadOnly(editions));
    }
}
