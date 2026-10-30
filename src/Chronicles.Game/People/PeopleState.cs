namespace Chronicles.Game.People;

public enum PeopleLearningKind { Practice, Teaching, Experiment, Reflection }
public sealed record PeopleLearningEvent(string EventId, int TalentIndex, PeopleLearningKind Kind, int CompletedMonths,
    int LearnedChange, int UnderstandingChange, string KnowledgeReference, bool MayContainMisconception, PeopleSource Source);
public sealed record PeopleProfile(long PersonId, long CreatedMonth, InnateProfile Innate, TalentDevelopment Development,
    Embodiment Body, PeopleLearningEvent[] Learning);
public sealed record PeopleLearningChange(bool Accepted, string Code, PeopleProfile State);

/// <summary>
/// Host-owned serializable module state. The host commits it with Person/Soul/command state in one world transition;
/// observer queries must never create profiles, awaken traces, learn talents, or emit reputation claims.
/// </summary>
public sealed record PeopleState(int Schema, PeopleTuning Tuning, EvaluationPolicy EvaluationPolicy, ReputationPolicy ReputationPolicy,
    PeopleProfile[] Profiles, SoulTraceProfile[] Souls, InheritanceState Inheritance, TraitLedger Traits,
    EvaluationState Evaluations, ReputationState Reputation, FormationState[] CombatFormations,
    TraitObservation[] TraitObservations, TraceObservation[] TraceObservations, PotentialObservation[] PotentialObservations)
{
    public long SettledThroughMonth { get; init; } = -1;
    public ParentGenome[] Genomes { get; init; } = [];
    public PeopleSocialState Social { get; init; } = PeopleSocialState.Empty();
    public static PeopleState Empty() => new(1, new(), new(), new(), [], [], InheritanceState.Empty(), TraitLedger.Empty(),
        EvaluationState.Empty(), ReputationState.Empty(), [], [], [], []);

    public void Validate(long month)
    {
        if (Schema != 1 || month < 0 || SettledThroughMonth > month || SettledThroughMonth < -1) throw new InvalidDataException("Invalid people schema or simulation time.");
        Tuning.Validate(); EvaluationPolicy.Validate();
        Social.Policy.Validate();
        if (Social.LastEvent < 0 || Social.SettledThrough > month || Social.SettledThrough < -1)
            throw new InvalidDataException("People social time/source cursor is invalid.");
        if (ReputationPolicy.MaximumClaimsPerObserver < 1 || ReputationPolicy.Revision < 1) throw new InvalidDataException("Invalid reputation policy.");
        RequireUnique(Profiles.Select(p => p.PersonId), "person profiles"); RequireUnique(Profiles.Select(p => p.Body.BodyId), "bodies");
        RequireUnique(Souls.Select(s => s.SoulId), "souls"); RequireUnique(Traits.Traits.Select(t => t.Id), "traits");
        RequireUnique(Inheritance.Conceptions.Select(c => c.Id), "conceptions"); RequireUnique(Inheritance.Births.Select(b => b.BirthId), "births");
        RequireUnique(Genomes.Select(g => g.PersonId), "transmissible genomes");
        var ids = Profiles.Select(p => p.PersonId).ToHashSet(); var soulIds = Souls.Select(s => s.SoulId).ToHashSet();
        RequireUnique(Social.Sessions.Select(s => s.Id), "personal sessions"); RequireUnique(Social.Oaths.Select(o => o.Id), "personal oaths");
        foreach (var session in Social.Sessions)
        {
            if (!ids.Contains(session.Proposer) || !ids.Contains(session.Recipient) || session.Proposer == session.Recipient ||
                session.ProposerBody <= 0 || session.RecipientBody <= 0 || session.Place is < 0 or >= 100 || session.Requested < 0 ||
                session.Requested > month || session.Expires <= session.Requested || !Enum.IsDefined(session.Status) || !Enum.IsDefined(session.Kind) ||
                session.TalentIndex is < 0 or > 23 || session.Terms.Length > 80 ||
                session.Status == PeopleSessionStatus.Active && (session.Started > month || session.Due <= session.Started) ||
                session.Status == PeopleSessionStatus.Completed && (session.Ended > month || session.Ended < session.Due))
                throw new InvalidDataException("Invalid real personal session, time or participants.");
            if (session.SharedClaim is { } claim) global::Chronicles.Game.People.Reputation.Validate(claim);
        }
        foreach (var oath in Social.Oaths)
            if (!ids.Contains(oath.First) || !ids.Contains(oath.Second) || oath.First == oath.Second || oath.Began > month ||
                oath.Until <= oath.Began || oath.Ended > month || oath.Evidence <= 0 || oath.Terms.Length > 80)
                throw new InvalidDataException("An oath must refer to actual people, consent and a bounded term.");
        if (Social.Habits.GroupBy(h => (h.Person, h.Habit)).Any(g => g.Count() > 1))
            throw new InvalidDataException("Actual completed habit counters must be unique per person and action.");
        foreach (var habit in Social.Habits)
            if (!ids.Contains(habit.Person) || habit.Body <= 0 || habit.Count < 0 || habit.LastObservedCounter < 0 || habit.FirstTracked < 0 ||
                habit.LastCompleted < habit.FirstTracked || habit.LastCompleted > month)
                throw new InvalidDataException("Invalid actual observed habit history.");
        foreach (var ecology in Social.Ecology)
        {
            ecology.Source.Validate();
            if (!ids.Contains(ecology.Person) || ecology.Body <= 0 || ecology.Month > month || ecology.Month < 0 ||
                ecology.Place is < 0 or >= 100 || !Enum.IsDefined(ecology.Habitat) || ecology.AmbientQiConsumed is < 0 or > 1 || ecology.HealthCost is < 0 or > 2)
                throw new InvalidDataException("Invalid personally experienced ecology source.");
        }
        foreach (var genome in Genomes)
        {
            var profile = Profiles.FirstOrDefault(p => p.PersonId == genome.PersonId);
            var birth = Inheritance.Births.FirstOrDefault(b => b.PersonId == genome.PersonId);
            var conception = birth is null ? null : Inheritance.Conceptions.FirstOrDefault(c => c.Id == birth.ConceptionId);
            if (profile is null || profile.Body.BodyId != genome.BodyId || profile.Body.SpeciesId != genome.SpeciesId)
                throw new InvalidDataException("Transmissible genome must belong to its actual biological body.");
            global::Chronicles.Game.People.Inheritance.ValidateGenome(genome, conception?.Model ?? GeneticModel.Provisional());
            if (conception is not null && genome.Genes.Any(g => !conception.Genes.Any(f => f.GeneId == g.GeneId && f.ParentOneAllele == g.FirstAllele && f.ParentTwoAllele == g.SecondAllele)))
                throw new InvalidDataException("A descendant's frozen genetic units cannot be rewritten by current learning or development.");
        }
        foreach (var profile in Profiles)
        {
            PeopleProfiles.Validate(profile);
            if (profile.CreatedMonth > month || !soulIds.Contains(profile.Body.SoulId)) throw new InvalidDataException("People profile time/soul reference is invalid.");
        }
        foreach (var soul in Souls)
        {
            soul.Validate();
            if (soul.History.Any(h => h.Month > month) || soul.CurrentPersonId != 0 && !Profiles.Any(p => p.PersonId == soul.CurrentPersonId && p.Body.SoulId == soul.SoulId))
                throw new InvalidDataException("Soul current-life reference or trace history time is invalid.");
        }
        foreach (var trait in Traits.Traits)
        {
            TraitCatalog.Get(trait.DefinitionId); trait.Source.Validate(); PeopleChecks.Score(trait.Strength, nameof(trait.Strength));
            if (!ids.Contains(trait.PersonId) || !Enum.IsDefined(trait.Status) || trait.AcquiredMonth > month || trait.ExpiresMonth <= trait.AcquiredMonth ||
                trait.History.Length == 0 || trait.History[0].Kind != TraitChangeKind.Acquired || trait.History[0].Month != trait.AcquiredMonth ||
                trait.History.Any(h => h.Month > month || h.Month != h.Source.Month) || trait.Status == TraitStatus.Active && trait.AwakenedMonth is null)
                throw new InvalidDataException("Invalid trait lifecycle or provenance.");
            var expectedStatus = trait.History[^1].Kind switch
            {
                TraitChangeKind.Acquired => TraitStatus.Latent, TraitChangeKind.Awakened => TraitStatus.Active,
                TraitChangeKind.Suppressed => TraitStatus.Suppressed, TraitChangeKind.Released => TraitStatus.Released,
                TraitChangeKind.Expired => TraitStatus.Expired, _ => throw new InvalidDataException("Unknown trait transition.")
            };
            if (trait.Status != expectedStatus) throw new InvalidDataException("Trait status disagrees with its actual transition history.");
            if (trait.History.Select(h => h.EventId).Distinct(StringComparer.Ordinal).Count() != trait.History.Length ||
                trait.History.Zip(trait.History.Skip(1)).Any(pair => pair.First.Month > pair.Second.Month))
                throw new InvalidDataException("Invalid trait event ordering.");
        }
        foreach (var conception in Inheritance.Conceptions)
        {
            conception.Model.Validate(); PeopleChecks.Scores(conception.BodyFoundations, 5, nameof(conception.BodyFoundations));
            PeopleChecks.Scores(conception.ElementRoots, 5, nameof(conception.ElementRoots));
            if (conception.Month > month || conception.ParentOne.PersonId == conception.ParentTwo.PersonId ||
                conception.Genes.Length != conception.Model.Genes.Length || conception.Development.Any(d => d.Stage != DevelopmentStage.Prenatal || d.Source.SoulId != 0))
                throw new InvalidDataException("Invalid frozen biological inheritance.");
        }
        foreach (var birth in Inheritance.Births)
        {
            birth.Innate.Validate();
            if (!ids.Contains(birth.PersonId) || !soulIds.Contains(birth.SoulId) || birth.Month > month || birth.EndedMonth < birth.Month ||
                !Inheritance.Conceptions.Any(c => c.Id == birth.ConceptionId && c.Month <= birth.Month))
                throw new InvalidDataException("Birth, body and soul assignment references must resolve.");
        }
        if (Inheritance.Births.Where(b => b.EndedMonth is null).GroupBy(b => b.SoulId).Any(g => g.Count() > 1))
            throw new InvalidDataException("One soul cannot be bound to concurrent lives.");
        foreach (var claim in Reputation.Claims)
        {
            People.Reputation.Validate(claim);
            if (!ids.Contains(claim.ObserverId) || !ids.Contains(claim.SubjectId) || claim.ReceivedMonth > month)
                throw new InvalidDataException("Observed reputation reference/time is invalid.");
        }
        if (Reputation.Claims.GroupBy(c => (c.ObserverId, c.Id)).Any(g => g.Count() > 1) ||
            Reputation.Claims.GroupBy(c => c.ObserverId).Any(g => g.Count() > ReputationPolicy.MaximumClaimsPerObserver))
            throw new InvalidDataException("Duplicate or over-budget observed reputation.");
        foreach (var formation in CombatFormations)
        {
            CombatAssessment.Validate(formation.Current);
            if (!ids.Contains(formation.Current.PersonId) || formation.Current.FormedMonth > month)
                throw new InvalidDataException("Combat formation person/time reference is invalid.");
        }
        foreach (var observation in TraitObservations)
            if (!ids.Contains(observation.ObserverId) || !ids.Contains(observation.SubjectId) || observation.ObservedMonth < 0 || observation.AcquiredMonth > month || observation.ObservedMonth > observation.AcquiredMonth)
                throw new InvalidDataException("Trait observation is not received evidence.");
        foreach (var observation in TraceObservations)
            if (!ids.Contains(observation.ObserverId) || !ids.Contains(observation.SubjectId) || observation.ReceivedMonth > month || observation.ObservedMonth > observation.ReceivedMonth ||
                observation.Integrated < 0 || observation.Integrated > observation.Accessible || observation.Accessible > 10_000)
                throw new InvalidDataException("Trace observation is not received evidence.");
        foreach (var observation in PotentialObservations)
        {
            CombatAssessment.ValidatePotential(observation.Snapshot);
            if (!ids.Contains(observation.ObserverId) || !ids.Contains(observation.SubjectId) || observation.ReceivedMonth > month || observation.ObservedMonth < 0 || observation.ObservedMonth > observation.ReceivedMonth)
                throw new InvalidDataException("Potential observation is not received evidence.");
        }
        foreach (var evaluation in Evaluations.History)
        {
            evaluation.Scope.Validate();
            if (evaluation.Month > month || evaluation.Population.Select(p => p.PersonId).Distinct().Count() != evaluation.Population.Length ||
                evaluation.Population.Any(p => !ids.Contains(p.PersonId) || p.AbsoluteIndex is < 0 or > 60_000 || p.Population != evaluation.Population.Length))
                throw new InvalidDataException("Invalid dated evaluation snapshot.");
        }
    }

    private static void RequireUnique(IEnumerable<long> values, string label)
    {
        var array = values.ToArray();
        if (array.Any(v => v <= 0) || array.Distinct().Count() != array.Length) throw new InvalidDataException($"Invalid identities in {label}.");
    }
}

public static class PeopleProfiles
{
    public static PeopleLearningChange Learn(PeopleProfile state, PeopleLearningEvent change)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(change);
        Validate(state); change.Source.Validate(); ArgumentException.ThrowIfNullOrWhiteSpace(change.EventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(change.KnowledgeReference);
        var existing = state.Learning.FirstOrDefault(e => e.EventId == change.EventId);
        if (existing is not null) return new(existing == change, "learning event already resolved", state);
        if (!Enum.IsDefined(change.Kind) || (uint)change.TalentIndex >= 24 || change.CompletedMonths is < 1 or > 1200 ||
            change.LearnedChange is < 0 or > 10_000 || change.UnderstandingChange is < -10_000 or > 10_000 ||
            change.Source.Kind is not (PeopleSourceKind.Education or PeopleSourceKind.Development or PeopleSourceKind.Environment) ||
            change.Source.Month < state.CreatedMonth || (state.Learning.Length > 0 && change.Source.Month < state.Learning[^1].Source.Month))
            return new(false, "actual completed practice, teaching, experiment or reflection required", state);
        var learned = state.Development.Learned.ToArray(); var understanding = state.Development.Understanding.ToArray();
        learned[change.TalentIndex] = Math.Clamp(learned[change.TalentIndex] + change.LearnedChange, 0, 10_000);
        understanding[change.TalentIndex] = Math.Clamp(understanding[change.TalentIndex] + change.UnderstandingChange, 0, 10_000);
        return new(true, "learned aptitude changed with explicit current-life evidence; innate genome unchanged", state with
        {
            Development = new(learned, understanding), Learning = [.. state.Learning, change]
        });
    }

    public static AptitudeContext Context(PeopleProfile profile, TraitLedger traits, long month, int physicalCondition, int mentalCondition,
        PeopleEffect[] otherEffects, PeopleTuning tuning, int domainTalentIndex = 9)
    {
        ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(traits); ArgumentNullException.ThrowIfNull(otherEffects);
        var baselineCauses = profile.Innate.Sources.Select(s => s.CauseId).ToHashSet(StringComparer.Ordinal);
        var effects = Traits.ActiveEffects(traits, profile.PersonId, month).Concat(otherEffects).Select(e =>
            baselineCauses.Contains(e.Source.CauseId) && e.Role == EffectRole.Contribution ? e with { Role = EffectRole.IncludedInBaseline } : e).ToArray();
        return new(profile.Innate, profile.Development, physicalCondition, mentalCondition, effects, tuning, domainTalentIndex);
    }

    public static void Validate(PeopleProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile); profile.Innate.Validate(); profile.Development.Validate(); Embodiments.Validate(profile.Body);
        if (profile.PersonId <= 0 || profile.CreatedMonth < 0 || profile.Body.PersonId != profile.PersonId ||
            profile.Learning.Select(e => e.EventId).Distinct(StringComparer.Ordinal).Count() != profile.Learning.Length ||
            profile.Learning.Zip(profile.Learning.Skip(1)).Any(pair => pair.First.Source.Month > pair.Second.Source.Month))
            throw new InvalidDataException("Invalid people profile identity or development history.");
        var learned = new int[24]; var understanding = new int[24];
        foreach (var change in profile.Learning)
        {
            change.Source.Validate();
            if (string.IsNullOrWhiteSpace(change.EventId) || string.IsNullOrWhiteSpace(change.KnowledgeReference) ||
                !Enum.IsDefined(change.Kind) || (uint)change.TalentIndex >= 24 || change.CompletedMonths is < 1 or > 1200 ||
                change.LearnedChange is < 0 or > 10_000 || change.UnderstandingChange is < -10_000 or > 10_000 ||
                change.Source.Month < profile.CreatedMonth || change.Source.Kind is not (PeopleSourceKind.Education or PeopleSourceKind.Development or PeopleSourceKind.Environment))
                throw new InvalidDataException("Talent development requires an actual completed learning event.");
            learned[change.TalentIndex] = Math.Clamp(learned[change.TalentIndex] + change.LearnedChange, 0, 10_000);
            understanding[change.TalentIndex] = Math.Clamp(understanding[change.TalentIndex] + change.UnderstandingChange, 0, 10_000);
        }
        if (!learned.SequenceEqual(profile.Development.Learned) || !understanding.SequenceEqual(profile.Development.Understanding))
            throw new InvalidDataException("Learned development must match its source events; soul traces and inheritance grant no free proficiency.");
    }
}

/// <summary>Explicit import helpers for the shared 0.2.1 Person/Soul model; these do not create citizens, skills or past events.</summary>
public static class PeopleAdapters
{
    public static PeopleProfile FromPerson(Person person, PeopleSource source, SpeciesDefinition? species = null)
    {
        ArgumentNullException.ThrowIfNull(person); ArgumentNullException.ThrowIfNull(source);
        source.Validate(); species ??= SpeciesCatalog.Get(person.Species);
        if (species.Id != person.Species) throw new InvalidDataException("Species definition does not match the actual person.");
        var origin = source.Kind == PeopleSourceKind.Founding ? PersonOrigin.Founder : person.ParentOne != 0 ? PersonOrigin.BiologicalBirth : PersonOrigin.ExplicitArrival;
        var body = new Embodiment(person.Id, person.BodyId, person.SoulId, person.Species, species.NaturalForm, origin, "", YaoguaiTitle.None,
            new(species.ReproductionGroup, person.Gestates ? ReproductiveFunction.Gestator : ReproductiveFunction.Donor,
                species.MaturityMonths, species.GestationMonths, source), species.NaturalCapabilities ??
                new(species.Sapient, species.NaturalForm == BodyForm.Humaniform, species.NaturalForm != BodyForm.RootedPlant, false), source);
        var profile = new PeopleProfile(person.Id, source.Month, new(person.Aptitudes.ToArray(), person.Roots.ToArray(), [source]), TalentDevelopment.Empty(), body, []);
        PeopleProfiles.Validate(profile);
        return profile;
    }

    public static ParentGenome FoundingGenome(PeopleProfile profile, GeneticModel model)
    {
        ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(model); PeopleProfiles.Validate(profile); model.Validate();
        if (profile.Body.Origin is not (PersonOrigin.Founder or PersonOrigin.ExplicitArrival))
            throw new InvalidDataException("Biological descendants must retain their actual frozen alleles; do not regenerate a genome from current aptitudes.");
        return new(profile.PersonId, profile.Body.BodyId, profile.Body.SpeciesId, model.Genes.Select(g =>
        {
            var value = g.Target.Kind == HeritableTargetKind.BodyFoundation ? profile.Innate.Foundations[g.Target.Index] : profile.Innate.ElementRoots[g.Target.Index];
            var source = new PeopleSource($"founding.body.{profile.Body.BodyId}.{g.Id}", PeopleSourceKind.Founding,
                "declared founding body profile; no fabricated ancestry or historical training", profile.CreatedMonth,
                PersonId: profile.PersonId, BodyId: profile.Body.BodyId);
            return new GenePair(g.Id, value, value, source);
        }).ToArray(), []);
    }

    public static SoulTraceProfile FromSoul(Soul soul, long month)
    {
        ArgumentNullException.ThrowIfNull(soul);
        if (soul.Traces.Length != 6 || soul.Traces.Any(t => t.Integrated < 0 || t.Integrated > t.Accessible || t.Accessible > t.Preserved || t.Preserved > 100))
            throw new InvalidDataException("Legacy soul trace percentages violate preservation/access/integration bounds.");
        var profile = new SoulTraceProfile(soul.Id, soul.Parent, soul.Root == 0 ? soul.Id : soul.Root, soul.CurrentPerson,
            soul.Traces.Select((trace, index) => new SoulTraceValue((SoulTraceDomain)index, trace.Preserved * 100, trace.Accessible * 100, trace.Integrated * 100,
                trace.Preserved > 0, [], false, trace.Preserved > 0 ? new PeopleSource($"legacy.soul.{soul.Id}.trace.{index}", PeopleSourceKind.SoulTrace,
                    string.IsNullOrWhiteSpace(soul.Evidence) ? "explicit legacy trace measurement" : soul.Evidence, month, SoulId: soul.Id) : null)).ToArray(), []);
        profile.Validate(); return profile;
    }
}
