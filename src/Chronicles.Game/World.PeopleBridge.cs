using System.Text.Json;
using Chronicles.Core;
using Chronicles.Game.People;

namespace Chronicles.Game;

public sealed partial class GameWorld
{
    // Called by creation/explicit-arrival hooks only. Queries do not initialize or repair domain state.
    internal void InitializePeopleDomain()
    {
        foreach (var soul in state.Souls.OrderBy(s => s.Id)) EnsurePeopleSoul(soul);
        foreach (var person in state.People.OrderBy(p => p.Id)) InitializePeoplePerson(person, person.ParentOne == 0 && person.Born < 0 && Month == 0);
        if (state.PeopleDomain.SettledThroughMonth < 0)
        {
            state.PeopleDomain = state.PeopleDomain with { SettledThroughMonth = Month };
            EvaluatePeopleScopes();
        }
    }

    internal void InitializePeoplePerson(Person person, bool founder = false)
    {
        if (state.PeopleDomain.Profiles.Any(p => p.PersonId == person.Id)) return;
        var source = new PeopleSource($"person.{person.Id}.initial-profile", founder ? PeopleSourceKind.Founding : PeopleSourceKind.Development,
            founder ? "창세 조건·가짜 과거 숙련 없음" : "실제 현생 편입/출생 프로필", Month, state.LawRevision, person.Id, person.BodyId, person.SoulId);
        var profile = PeopleAdapters.FromPerson(person, source);
        var soul = EnsurePeopleSoul(souls[person.SoulId]);
        if (soul.Foundation is null)
        {
            soul = soul with { Foundation = new(person.Aptitudes[(int)FoundationKind.Soul], source with { CauseId = $"soul.{soul.SoulId}.foundation" }) };
            StorePeopleSoul(soul);
        }
        // A same-soul rebirth uses the soul's own foundation, never the biological parents' soul values.
        var foundations = profile.Innate.Foundations.ToArray(); foundations[5] = soul.Foundation.Value;
        profile = profile with { Innate = profile.Innate with { Foundations = foundations } };
        state.PeopleDomain = state.PeopleDomain with { Profiles = [.. state.PeopleDomain.Profiles, profile] };
        if (profile.Body.Origin is PersonOrigin.Founder or PersonOrigin.ExplicitArrival)
            StorePeopleGenome(PeopleAdapters.FoundingGenome(profile, GeneticModel.Provisional()));
    }

    // Pregnancy.Id is the stable conception key; selections are from a dedicated stream and are saved once.
    internal void FreezePeopleConception(Pregnancy pregnancy, Person mother, Person father)
    {
        if (state.PeopleDomain.Inheritance.Conceptions.FirstOrDefault(c => c.Id == pregnancy.Id) is { } captured)
        {
            ProjectPeoplePregnancy(pregnancy, captured); return;
        }
        InitializePeoplePerson(mother); InitializePeoplePerson(father);
        var model = GeneticModel.Provisional();
        var random = new DeterministicRandom(state.Seed ^ unchecked((ulong)pregnancy.Id * 0x9E3779B97F4A7C15UL) ^ 0x47454E4554494353UL);
        var selections = model.Genes.Select(g =>
        {
            var one = random.NextInt(2); var two = random.NextInt(2); var mutation = random.NextInt(401) - 200;
            return new AlleleSelection(g.Id, one, two, mutation, mutation == 0 ? null : new PeopleSource($"conception.{pregnancy.Id}.mutation.{g.Id}",
                PeopleSourceKind.Mutation, "정의된 잉태 stream의 전달 단위 변이·±200 잠정 범위", Month, model.Revision), random.NextInt(2));
        }).ToArray();
        var change = Inheritance.Capture(state.PeopleDomain.Inheritance, new(pregnancy.Id, Month, pregnancy.LawRevision, mother.Species,
            PeopleGenome(mother), PeopleGenome(father), model, selections));
        if (!change.Accepted) throw new InvalidDataException(change.Code);
        state.PeopleDomain = state.PeopleDomain with { Inheritance = change.State };
        ProjectPeoplePregnancy(pregnancy, change.State.Conceptions.Single(c => c.Id == pregnancy.Id));
    }

    internal ReproductionEligibility CheckPeopleReproduction(Person mother, Person father)
    {
        ReproductivePerson Context(Person person)
        {
            var body = PeopleProfile(person).Body;
            if (body.SpeciesId == "human") body = body with { Reproduction = body.Reproduction with
                { MaturityMonths = Law("birth.maturity"), GestationMonths = IntLaw("birth.gestation") } };
            return new(body, person.Realm, person.Martial, Math.Max(0, Month - person.Born), person.Health * 100,
                person.Status == LifeStatus.Alive, person.Partner == (person.Id == mother.Id ? father.Id : mother.Id),
                state.Pregnancies.Any(p => p.Mother == person.Id && !p.Completed), person.NextBirth);
        }
        return Embodiments.CheckReproduction(new(Month, Context(mother), Context(father), Related(mother, father),
            mother.Place == father.Place, new((Realm)Law("birth.minimumRealm"), Law("birth.kinship") != 0, 3000,
                Revision: state.LawRevision, RequireBothParents: Law("birth.bothParents") != 0, AllowMartialEquivalent: Law("birth.martialEquivalent") != 0), []));
    }

    private static void ProjectPeoplePregnancy(Pregnancy pregnancy, ConceptionRecord conception)
    {
        pregnancy.Roots = conception.ElementRoots.ToArray();
        pregnancy.Aptitudes = [.. conception.BodyFoundations, 0]; // compatibility only: soul foundation is unresolved before actual birth
    }

    internal void InitializePeopleBirth(Person person, Pregnancy pregnancy)
    {
        if (state.PeopleDomain.Inheritance.Births.FirstOrDefault(b => b.BirthId == pregnancy.Id) is { } existing)
        {
            person.Aptitudes = existing.Innate.Foundations.ToArray(); person.Roots = existing.Innate.ElementRoots.ToArray(); return;
        }
        var conception = state.PeopleDomain.Inheritance.Conceptions.SingleOrDefault(c => c.Id == pregnancy.Id)
            ?? throw new InvalidDataException("Actual birth requires its original frozen conception; never regenerate it at birth.");
        var legacySoul = souls[person.SoulId]; var soul = EnsurePeopleSoul(legacySoul);
        var source = new PeopleSource($"birth.{pregnancy.Id}.soul.{person.SoulId}", PeopleSourceKind.SoulTrace,
            $"실제 출생 {pregnancy.Id}의 단일 영혼 배정", Month, state.LawRevision, person.Id, person.BodyId, person.SoulId);
        if (soul.Foundation is null)
        {
            var random = new DeterministicRandom(state.Seed ^ unchecked((ulong)person.SoulId * 0xD6E8FEB86659FD93UL) ^ 0x534F554C42415345UL);
            soul = soul with { Foundation = new(4000 + random.NextInt(2001), source with { Reference = "실제 신규 영혼의 선천 기반·부모 영혼/숙련 비복제" }) };
        }
        var binding = Inheritance.BindBirth(state.PeopleDomain.Inheritance,
            new(pregnancy.Id, conception.Id, person.Id, person.BodyId, person.SoulId, Month, soul.Foundation.Value, source));
        if (!binding.Accepted) throw new InvalidDataException(binding.Code);
        var birth = binding.State.Births.Single(b => b.BirthId == pregnancy.Id);
        person.Aptitudes = birth.Innate.Foundations.ToArray(); person.Roots = birth.Innate.ElementRoots.ToArray(); person.Species = conception.SpeciesId;
        var profile = PeopleAdapters.FromPerson(person, source) with { Innate = birth.Innate };
        var reset = SoulTraces.ResetAtBirth(soul, new($"birth.{pregnancy.Id}.trace-reset", SoulTraceActionKind.BirthReset, Month, source, RelatedIdentity: person.Id));
        if (!reset.Accepted) throw new InvalidDataException(reset.Code);
        state.PeopleDomain = state.PeopleDomain with
        {
            Inheritance = binding.State,
            Profiles = state.PeopleDomain.Profiles.Where(p => p.PersonId != person.Id).Append(profile).OrderBy(p => p.PersonId).ToArray()
        };
        StorePeopleSoul(reset.State); StorePeopleGenome(Inheritance.TransmissibleGenome(birth, conception)); ProjectPeopleSoul(reset.State);
        foreach (var marker in conception.Markers)
            RecordPeopleTrait(person, marker.TraitDefinitionId, marker.Source with { Month = Month, Reference = $"출생 시 잠재 편입: {marker.Source.Reference}" },
                [TraitCatalog.Get(marker.TraitDefinitionId).Acquisition], activate: false);
        if (legacySoul.Route > 0 || legacySoul.Parent != 0)
            RecordPeopleTrait(person, "soul.reborn", source, [TraitFact.RebirthEvidence], activate: true);
    }

    /// <summary>Call after a real completed action, with its actual gains; queries and inherited traces never call this.</summary>
    internal void RecordPeopleLearning(Person person, string knowledge, int learnedGain, int understandingGain, string eventId, int completedMonths = 1,
        PeopleLearningKind kind = PeopleLearningKind.Practice, bool mayContainMisconception = false)
    {
        if (learnedGain <= 0 && understandingGain == 0) return;
        var profile = PeopleProfile(person);
        var source = new PeopleSource(eventId, PeopleSourceKind.Education, $"실제 완료한 {knowledge} 학습/실습", Month, state.LawRevision,
            person.Id, person.BodyId, person.SoulId);
        var index = PeopleTalentForKnowledge(knowledge);
        var change = PeopleProfiles.Learn(profile, new(eventId, index, kind, completedMonths,
            Math.Clamp(learnedGain, 0, 10_000), Math.Clamp(understandingGain, -10_000, 10_000), knowledge, mayContainMisconception, source));
        if (!change.Accepted) throw new InvalidDataException(change.Code);
        StorePeopleProfile(change.State);
        var soul = EnsurePeopleSoul(souls[person.SoulId]);
        var domain = knowledge.StartsWith("path.", StringComparison.Ordinal) || knowledge == "entry"
            ? SoulTraceDomain.CultivationPrinciplesRealmKnowledge : SoulTraceDomain.TechniqueSpellPillCraftKnowledge;
        if (soul.Values.Single(v => v.Domain == domain) is { HasEvidence: true, Accessible: > 0 })
        {
            var integration = SoulTraces.Integrate(soul, new($"{eventId}.trace-integration", SoulTraceActionKind.Integration, Month, source, domain,
                Math.Clamp(understandingGain + learnedGain, 1, 10_000)));
            if (integration.Accepted) { StorePeopleSoul(integration.State); ProjectPeopleSoul(integration.State); RecordSelfTraceObservation(person, integration.State, domain, eventId); }
        }
    }

    internal void EndPeopleLife(Person person)
    {
        var soul = EnsurePeopleSoul(souls[person.SoulId]); var id = $"life.{person.Id}.trace-capture";
        if (soul.History.Any(h => h.EventId == id)) return;
        var source = new PeopleSource(id, PeopleSourceKind.SoulTrace, "실제 현생 종료 시점의 기억/지식/행동 근거", Month,
            state.LawRevision, person.Id, person.BodyId, person.SoulId);
        int Mean(IEnumerable<int> values) { var array = values.ToArray(); return array.Length == 0 ? 0 : (int)(array.Sum(v => (long)v) / array.Length); }
        var measures = new[]
        {
            Math.Min(10_000, person.Journal.Count * 100),
            Math.Min(10_000, person.ObservedPeople.Count * 200),
            Mean(person.Understanding.Where(k => k.Key == "entry" || k.Key.StartsWith("path.", StringComparison.Ordinal)).Select(k => k.Value)),
            Mean(person.Skills.Where(k => k.Key is "alchemy" or "smith" or "talisman" or "array" or "farm" or "medicine").Select(k => k.Value)),
            Math.Min(10_000, person.ObservedPlaces.Count * 200),
            Math.Max(person.Skills.GetValueOrDefault("combat"), person.Martial * 500)
        };
        soul = soul with { CurrentPersonId = 0, Values = soul.Values.Select(v => v with
        {
            Preserved = Math.Max(v.Preserved, measures[(int)v.Domain]), Accessible = 0, Integrated = 0, HasEvidence = true,
            DefinitionIds = v.DefinitionIds.Append($"life.{person.Id}.trace.{(int)v.Domain}").Distinct(StringComparer.Ordinal).ToArray(), Source = source
        }).ToArray(), History = [.. soul.History, new(id, SoulTraceActionKind.LifeCapture, Month, source, RelatedIdentity: person.Id)] };
        if (souls[person.SoulId].Integrity < 100)
        {
            var damage = SoulTraces.Damage(soul, new($"{id}.actual-soul-damage", SoulTraceActionKind.CrossingDamage, Month, source,
                DomainAmounts: Enumerable.Repeat((100 - souls[person.SoulId].Integrity) * 100, 6).ToArray()));
            if (damage.Accepted) soul = damage.State;
        }
        StorePeopleSoul(soul); ProjectPeopleSoul(soul);
        var ending = Inheritance.EndLife(state.PeopleDomain.Inheritance, person.Id, person.Ended ?? Month);
        if (ending.Accepted) state.PeopleDomain = state.PeopleDomain with { Inheritance = ending.State };
    }

    internal void SplitPeopleSoul(Soul root, Soul child)
    {
        var source = EnsurePeopleSoul(root);
        if (state.PeopleDomain.Souls.Any(s => s.SoulId == child.Id)) return;
        var evidence = new PeopleSource($"split.{root.Id}.{child.Id}", PeopleSourceKind.SoulTrace, child.Evidence, Month,
            state.LawRevision, SoulId: root.Id);
        var split = SoulTraces.Split(source, child.Id, new(evidence.CauseId, SoulTraceActionKind.SplitSource, Month, evidence,
            RelatedIdentity: child.Id, DomainAmounts: Enumerable.Repeat(5000, 6).ToArray()), IntLaw("soul.splitCost") * 100);
        if (!split.Accepted || split.Child is null) throw new InvalidDataException(split.Code);
        StorePeopleSoul(split.Source); StorePeopleSoul(split.Child); ProjectPeopleSoul(split.Source); ProjectPeopleSoul(split.Child);
    }

    internal void SettlePeopleDomain()
    {
        if (state.PeopleDomain.SettledThroughMonth >= Month) return;
        foreach (var person in state.People.OrderBy(p => p.Id))
        {
            InitializePeoplePerson(person);
            if (person.Status != LifeStatus.Alive) { EndPeopleLife(person); continue; }
            var soul = EnsurePeopleSoul(souls[person.SoulId]);
            if (soul.CurrentPersonId != person.Id) { soul = soul with { CurrentPersonId = person.Id }; StorePeopleSoul(soul); }
            if (Month - person.Born >= Law("birth.maturity") && souls[person.SoulId].Route > 0)
                foreach (var value in soul.Values.Where(v => v.HasEvidence && v.Accessible < v.Preserved).ToArray())
                {
                    var id = $"life.{person.Id}.dream.{Month}.{(int)value.Domain}";
                    var source = new PeopleSource(id, PeopleSourceKind.Development, "실제 성장 월의 꿈/기시감·보존된 흔적만 개방", Month,
                        state.LawRevision, person.Id, person.BodyId, person.SoulId);
                    var awakening = SoulTraces.Awaken(soul, new(id, SoulTraceActionKind.Awakening, Month, source, value.Domain, 100));
                    if (awakening.Accepted) { soul = awakening.State; RecordSelfTraceObservation(person, soul, value.Domain, id); }
                }
            StorePeopleSoul(soul); ProjectPeopleSoul(soul);
            ReconcilePeopleMarker(person, "scar.foundation", person.StructureDamage > 0, TraitFact.FoundationDamage, $"body.{person.BodyId}.structure-damage");
            ReconcilePeopleMarker(person, "scar.elements", person.Roots.Max() - person.Roots.Min() >= 3000, TraitFact.ElementImbalance, $"body.{person.BodyId}.element-balance");
            ReconcilePeopleMarker(person, "soul.injured", souls[person.SoulId].Integrity < 100, TraitFact.SoulDamaged, $"soul.{person.SoulId}.integrity");
            ReconcilePeopleMarker(person, "soul.insight", soul.Values.Any(v => v.Accessible > 0), TraitFact.TraceAccessible, $"soul.{person.SoulId}.accessible-traces");
        }
        foreach (var trait in state.PeopleDomain.Traits.Traits.Where(t => t.Status is not (TraitStatus.Expired or TraitStatus.Released) && t.ExpiresMonth <= Month).ToArray())
        {
            var source = trait.Source with { Month = Month, Reference = $"실제 특성 기한 {trait.ExpiresMonth}" };
            var expired = Traits.Transition(state.PeopleDomain.Traits, new(trait.Id, $"trait.{trait.Id}.expiry", source, []), TraitChangeKind.Expired);
            if (expired.Accepted) state.PeopleDomain = state.PeopleDomain with { Traits = expired.State };
        }
        if (Month % 12 == 0) EvaluatePeopleScopes();
        state.PeopleDomain = state.PeopleDomain with { SettledThroughMonth = Month };
        foreach (var person in state.People) ProjectPeopleTraits(person);
    }

    internal AptitudeResult PeopleAptitude(Person person) => Aptitudes.Assess(PeopleProfiles.Context(PeopleProfile(person), state.PeopleDomain.Traits,
        Month, person.Health * 100, person.Health * 100, [], state.PeopleDomain.Tuning));
    internal int PeopleTalent(Person person, int index) => PeopleAptitude(person).Talents[index].Innate;
    internal int PeopleTalentExpression(Person person, int index) => PeopleAptitude(person).Talents[index].CurrentExpression;

    public PeopleDetailView? PeopleDetail(long personId)
    {
        var profile = state.PeopleDomain.Profiles.FirstOrDefault(p => p.PersonId == personId);
        if (profile is null) return null;
        var observer = state.Controlled == 0; var actor = observer ? null : people[state.Controlled];
        if (!observer && actor!.Id != personId && !actor.ObservedPeople.ContainsKey(personId)) return null;
        var self = actor?.Id == personId;
        var aptitude = observer || self ? PeopleAptitude(people[personId]) : null;
        var birth = state.PeopleDomain.Inheritance.Births.FirstOrDefault(b => b.PersonId == personId);
        var genetic = birth is null ? null : state.PeopleDomain.Inheritance.Conceptions.First(c => c.Id == birth.ConceptionId);
        var soul = state.PeopleDomain.Souls.FirstOrDefault(s => s.SoulId == profile.Body.SoulId);
        var view = new PeopleDetailView(personId, Month, observer, observer ? profile.Innate : null,
            aptitude?.Talents.Select(t => new TalentExpressionView(t.Index, t.Learned, t.Understanding, t.CurrentExpression)).ToArray() ?? [],
            aptitude?.Cognition.Select(c => new CognitionExpressionView(c.Capability, c.Practice, c.CurrentExpression)).ToArray() ?? [],
            observer ? profile.Body : null, observer ? genetic : null, observer ? birth : null,
            observer ? state.PeopleDomain.Genomes.FirstOrDefault(g => g.PersonId == personId) : null,
            observer ? state.PeopleDomain.Traits.Traits.Where(t => t.PersonId == personId).ToArray() : null,
            Traits.Project(state.PeopleDomain.Traits, new(actor?.Id ?? 0, personId, Month, observer, state.PeopleDomain.TraitObservations)),
            observer ? soul : null,
            soul is null ? [] : SoulTraces.Project(soul, new(actor?.Id ?? 0, personId, Month, observer, state.PeopleDomain.TraceObservations)),
            observer ? state.PeopleDomain.Evaluations.History.Where(e => e.Population.Any(p => p.PersonId == personId)).ToArray() : null,
            observer ? state.PeopleDomain.Reputation.Claims.Where(c => c.SubjectId == personId).ToArray() :
                Reputation.KnownClaims(state.PeopleDomain.Reputation, new(actor!.Id, personId, Month)),
            observer || self ? profile.Learning : null, state.PeopleDomain.Tuning.PolicyId, state.PeopleDomain.Tuning.Revision);
        return JsonSerializer.Deserialize<PeopleDetailView>(JsonSerializer.Serialize(view))!;
    }

    internal void ValidatePeopleDomain()
    {
        state.PeopleDomain.Validate(Month);
        foreach (var profile in state.PeopleDomain.Profiles)
            if (!people.TryGetValue(profile.PersonId, out var person) || person.BodyId != profile.Body.BodyId || person.SoulId != profile.Body.SoulId)
                throw new InvalidDataException("People domain and world person/body/soul identities disagree.");
    }

    private void EvaluatePeopleScopes()
    {
        var population = state.People.Select(p => new EvaluationPerson(p.Id, p.Species, p.Place / 10, p.Realm, p.Layer, p.Born,
            Month - p.Born < 60 ? GrowthPhase.Infant : Month - p.Born < Law("birth.maturity") ? GrowthPhase.Developing :
                (Month - p.Born) * 100 > p.Lifespan * 80 ? GrowthPhase.Declining : GrowthPhase.Mature,
            p.Status == LifeStatus.Alive, PeopleProfile(p).Innate)).ToArray();
        var scopes = new List<EvaluationScope> { new("world") };
        scopes.AddRange(Enumerable.Range(0, 10).Select(region => new EvaluationScope($"region.{region}", Region: region)));
        scopes.AddRange(SpeciesCatalog.All.Select(s => new EvaluationScope($"species.{s.Id}", SpeciesId: s.Id)));
        scopes.AddRange(Enum.GetValues<Realm>().Select(realm => new EvaluationScope($"realm.{(int)realm}", CultivationRealm: realm)));
        foreach (var scope in scopes)
        {
            var change = PeopleEvaluation.Record(state.PeopleDomain.Evaluations, new($"evaluation.{scope.Id}.{Month}", scope, Month,
                state.LawRevision, state.PeopleDomain.EvaluationPolicy, population));
            if (change.Accepted) state.PeopleDomain = state.PeopleDomain with { Evaluations = change.State };
        }
    }

    private void ReconcilePeopleMarker(Person person, string definitionId, bool actual, TraitFact fact, string causeId)
    {
        var existing = state.PeopleDomain.Traits.Traits.LastOrDefault(t => t.PersonId == person.Id && t.DefinitionId == definitionId && t.Status is not (TraitStatus.Released or TraitStatus.Expired));
        var source = new PeopleSource(causeId, definitionId.StartsWith("soul.", StringComparison.Ordinal) ? PeopleSourceKind.SoulTrace : PeopleSourceKind.Development,
            $"실제 현재 구조/흔적의 {definitionId} 표식·원인 효과에 추가 가산 없음", Month, state.LawRevision, person.Id, person.BodyId, person.SoulId);
        if (actual && existing is null) RecordPeopleTrait(person, definitionId, source, [fact], activate: true);
        else if (!actual && existing is not null)
        {
            var release = Traits.Transition(state.PeopleDomain.Traits, new(existing.Id, $"trait.{existing.Id}.resolved.{Month}", source, [TraitFact.Resolved]), TraitChangeKind.Released);
            if (release.Accepted) state.PeopleDomain = state.PeopleDomain with
            {
                Traits = release.State,
                TraitObservations = state.PeopleDomain.TraitObservations.Where(o => o.ObserverId != person.Id || o.SubjectId != person.Id || o.DefinitionId != definitionId)
                    .Append(new(person.Id, person.Id, definitionId, TraitStatus.Released, Month, Month, existing.ExpiresMonth, source.CauseId)).ToArray()
            };
        }
    }

    internal void RecordPeopleTrait(Person person, string definitionId, PeopleSource source, TraitFact[] facts, bool activate,
        long relatedEntityId = 0, long? expiresMonth = null)
    {
        var same = state.PeopleDomain.Traits.Traits.FirstOrDefault(t => t.PersonId == person.Id && t.DefinitionId == definitionId &&
            t.Source.CauseId == source.CauseId && t.Status is not (TraitStatus.Expired or TraitStatus.Released));
        if (same is not null)
        {
            if (activate && same.Status != TraitStatus.Active)
            {
                var manifest = Traits.Transition(state.PeopleDomain.Traits, new(same.Id, $"trait.{same.Id}.awakened.{Month}", source, facts), TraitChangeKind.Awakened);
                if (manifest.Accepted) state.PeopleDomain = state.PeopleDomain with
                {
                    Traits = manifest.State,
                    TraitObservations = state.PeopleDomain.TraitObservations.Where(o => o.ObserverId != person.Id || o.SubjectId != person.Id || o.DefinitionId != definitionId)
                        .Append(new(person.Id, person.Id, definitionId, TraitStatus.Active, Month, Month, same.ExpiresMonth, source.CauseId)).ToArray()
                };
            }
            ProjectPeopleTraits(person); return;
        }
        var id = Id();
        var acquire = Traits.Acquire(state.PeopleDomain.Traits, new(id, person.Id, definitionId, $"trait.{id}.acquired", source, facts,
            ExpiresMonth: expiresMonth, RelatedEntityId: relatedEntityId));
        if (!acquire.Accepted) return;
        state.PeopleDomain = state.PeopleDomain with { Traits = acquire.State };
        if (!activate) return;
        var awakening = Traits.Transition(state.PeopleDomain.Traits, new(id, $"trait.{id}.awakened", source, facts), TraitChangeKind.Awakened);
        if (!awakening.Accepted) return;
        state.PeopleDomain = state.PeopleDomain with
        {
            Traits = awakening.State,
            TraitObservations = state.PeopleDomain.TraitObservations.Where(o => o.ObserverId != person.Id || o.SubjectId != person.Id || o.DefinitionId != definitionId)
                .Append(new(person.Id, person.Id, definitionId, TraitStatus.Active, Month, Month, expiresMonth, source.CauseId)).ToArray()
        };
        ProjectPeopleTraits(person);
    }

    private void ProjectPeopleTraits(Person person)
    {
        var names = TraitCatalog.All.Select(d => d.Name).ToHashSet(StringComparer.Ordinal);
        person.Traits.RemoveAll(t => names.Contains(t.Id));
        foreach (var trait in state.PeopleDomain.Traits.Traits.Where(t => t.PersonId == person.Id && t.Status is not (TraitStatus.Released or TraitStatus.Expired)).OrderBy(t => t.Id))
            person.Traits.Add(new(TraitCatalog.Get(trait.DefinitionId).Name, trait.Source.Reference, trait.AcquiredMonth, trait.ExpiresMonth, Traits.IsActive(trait, Month)));
    }

    private void RecordSelfTraceObservation(Person person, SoulTraceProfile soul, SoulTraceDomain domain, string evidenceId)
    {
        var value = soul.Values.Single(v => v.Domain == domain);
        state.PeopleDomain = state.PeopleDomain with
        {
            TraceObservations = state.PeopleDomain.TraceObservations.Where(o => o.ObserverId != person.Id || o.SubjectId != person.Id || o.Domain != domain)
                .Append(new(person.Id, person.Id, domain, value.DefinitionIds.ToArray(), value.Accessible, value.Integrated, value.MayContainMisconception,
                    evidenceId, Month, Month)).ToArray()
        };
    }

    private PeopleProfile PeopleProfile(Person person) => state.PeopleDomain.Profiles.FirstOrDefault(p => p.PersonId == person.Id)
        ?? throw new InvalidDataException("People profile must be initialized at the actual creation/birth hook before simulation use.");

    private ParentGenome PeopleGenome(Person person)
    {
        if (state.PeopleDomain.Genomes.FirstOrDefault(g => g.PersonId == person.Id) is { } genome) return genome;
        if (state.PeopleDomain.Inheritance.Births.FirstOrDefault(b => b.PersonId == person.Id) is { } birth)
        {
            genome = Inheritance.TransmissibleGenome(birth, state.PeopleDomain.Inheritance.Conceptions.Single(c => c.Id == birth.ConceptionId));
            StorePeopleGenome(genome); return genome;
        }
        genome = PeopleAdapters.FoundingGenome(PeopleProfile(person), GeneticModel.Provisional()); StorePeopleGenome(genome); return genome;
    }

    private SoulTraceProfile EnsurePeopleSoul(Soul soul)
    {
        var profile = state.PeopleDomain.Souls.FirstOrDefault(s => s.SoulId == soul.Id);
        if (profile is not null) return profile;
        profile = PeopleAdapters.FromSoul(soul, Month); StorePeopleSoul(profile); return profile;
    }

    private void StorePeopleProfile(PeopleProfile profile) => state.PeopleDomain = state.PeopleDomain with
    { Profiles = state.PeopleDomain.Profiles.Where(p => p.PersonId != profile.PersonId).Append(profile).OrderBy(p => p.PersonId).ToArray() };
    private void StorePeopleSoul(SoulTraceProfile soul) => state.PeopleDomain = state.PeopleDomain with
    { Souls = state.PeopleDomain.Souls.Where(s => s.SoulId != soul.SoulId).Append(soul).OrderBy(s => s.SoulId).ToArray() };
    private void StorePeopleGenome(ParentGenome genome) => state.PeopleDomain = state.PeopleDomain with
    { Genomes = state.PeopleDomain.Genomes.Where(g => g.PersonId != genome.PersonId).Append(genome).OrderBy(g => g.PersonId).ToArray() };

    private void ProjectPeopleSoul(SoulTraceProfile soul)
    {
        if (souls.TryGetValue(soul.SoulId, out var legacy))
            legacy.Traces = soul.Values.OrderBy(v => v.Domain).Select(v => new Trace(v.Preserved / 100, v.Accessible / 100, v.Integrated / 100)).ToArray();
    }

    private static int PeopleTalentForKnowledge(string knowledge) => knowledge switch
    {
        "entry" or "absorption" => 6, "circulation" => 7, "refinement" or "assimilation" => 8,
        "martial" => 9, "mind" or "sense" => 10, "reasoning" => 11, "spell" => 12, "combat" => 13,
        "alchemy" => 14, "smith" => 15, "talisman" => 16, "array" => 17, "farm" or "herbalism" => 18,
        "beast" or "ecology" => 19, "medicine" or "heal" => 20, "trade" => 22, "governance" or "teaching" => 23,
        _ when knowledge.StartsWith("path.", StringComparison.Ordinal) => 7,
        _ => 21
    };
}
