namespace Chronicles.Game.People;

public enum HeritableTargetKind { BodyFoundation, ElementRoot }
public enum GeneExpressionRule { Codominant, Dominant, Recessive, ParentOne, ParentTwo }
public enum DevelopmentStage { Prenatal, AfterBirth }
public readonly record struct HeritableTarget(HeritableTargetKind Kind, int Index);
public sealed record GeneDefinition(string Id, HeritableTarget Target, int Weight, GeneExpressionRule Rule);
public sealed record GenePair(string GeneId, int FirstAllele, int SecondAllele, PeopleSource Source);
public sealed record InheritableMarker(string Id, string TraitDefinitionId, string GeneId, int MinimumExpression, PeopleSource Source);
public sealed record ParentGenome(long PersonId, long BodyId, string SpeciesId, GenePair[] Genes, InheritableMarker[] Markers);
public sealed record GeneticModel(string PolicyId, long Revision, GeneDefinition[] Genes, string Rationale, bool RequiresReview = true,
    int MutationMagnitude = 200, int MutationChance = 10_000)
{
    public static GeneticModel Provisional() => new("PC-TEMP-V021-GENETICS-01", 1,
        Enumerable.Range(0, 10).SelectMany(target => Enumerable.Range(0, 2).Select(unit =>
            new GeneDefinition($"{(target < 5 ? "body" : "element")}.{target % 5}.{unit}",
                new(target < 5 ? HeritableTargetKind.BodyFoundation : HeritableTargetKind.ElementRoot, target % 5),
                5000, GeneExpressionRule.Codominant))).ToArray(),
        "Two quantitative loci per body/root target; explicit allele selections and mutations are frozen at conception. Marker definitions may use dominance rules.");

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(PolicyId);
        if (Revision < 1 || MutationMagnitude is < 0 or > 10_000 || MutationChance is < 0 or > 10_000 ||
            Genes.Length == 0 || Genes.Select(g => g.Id).Distinct(StringComparer.Ordinal).Count() != Genes.Length)
            throw new InvalidDataException("Invalid genetic model identity.");
        foreach (var gene in Genes)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(gene.Id);
            if (!Enum.IsDefined(gene.Rule) || !Enum.IsDefined(gene.Target.Kind) || (uint)gene.Target.Index >= 5 || gene.Weight is < 1 or > 10_000)
                throw new InvalidDataException("Invalid heritable locus.");
        }
        foreach (var kind in Enum.GetValues<HeritableTargetKind>())
            for (var index = 0; index < 5; index++)
                if (Genes.Where(g => g.Target == new HeritableTarget(kind, index)).Sum(g => g.Weight) != 10_000)
                    throw new InvalidDataException("Every body/root target must have 100% of its genetic source weights.");
    }
}

/// <summary>Explicit outputs of the host's stable conception stream; this module consumes no random source.</summary>
public sealed record AlleleSelection(string GeneId, int ParentOneAllele, int ParentTwoAllele, int MutationDelta, PeopleSource? MutationSource = null, int MutationAllele = 0);
public sealed record FrozenGene(string GeneId, HeritableTarget Target, int ParentOneAllele, int ParentTwoAllele, int Expression,
    int Weight, GeneExpressionRule Rule, PeopleSource[] Sources);
public sealed record DevelopmentRecord(string EventId, DevelopmentStage Stage, HeritableTarget Target, int Amount, PeopleSource Source);
public sealed record ConceptionRecord(long Id, long Month, long LawRevision, string SpeciesId, ParentGenome ParentOne, ParentGenome ParentTwo,
    GeneticModel Model, AlleleSelection[] Selections, FrozenGene[] Genes, int[] BodyFoundations, int[] ElementRoots,
    InheritableMarker[] Markers, DevelopmentRecord[] Development);
public sealed record BirthProfile(long BirthId, long ConceptionId, long PersonId, long BodyId, long SoulId, long Month,
    InnateProfile Innate, InheritableMarker[] Markers, long? EndedMonth = null);
public sealed record InheritanceState(ConceptionRecord[] Conceptions, BirthProfile[] Births)
{
    public static InheritanceState Empty() => new([], []);
}
public sealed record ConceptionContext(long Id, long Month, long LawRevision, string SpeciesId, ParentGenome ParentOne,
    ParentGenome ParentTwo, GeneticModel Model, AlleleSelection[] Selections);
public sealed record BirthContext(long BirthId, long ConceptionId, long PersonId, long BodyId, long SoulId, long Month,
    int SoulFoundation, PeopleSource SoulSource);
public sealed record InheritanceChange(bool Accepted, string Code, InheritanceState State);

public static class Inheritance
{
    public static InheritanceChange Capture(InheritanceState state, ConceptionContext context)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(context);
        var existing = state.Conceptions.FirstOrDefault(c => c.Id == context.Id);
        if (existing is not null)
            return new(existing.ParentOne.PersonId == context.ParentOne.PersonId && existing.ParentTwo.PersonId == context.ParentTwo.PersonId &&
                existing.Month == context.Month && existing.LawRevision == context.LawRevision && existing.SpeciesId == context.SpeciesId,
                "conception already frozen; no reroll", state);
        if (context.Id <= 0 || context.Month < 0 || context.LawRevision < 1 || context.ParentOne.PersonId == context.ParentTwo.PersonId)
            return new(false, "invalid conception identity", state);
        ArgumentException.ThrowIfNullOrWhiteSpace(context.SpeciesId);
        context.Model.Validate(); ValidateParent(context.ParentOne, context.Model); ValidateParent(context.ParentTwo, context.Model);
        if (context.Selections.Length != context.Model.Genes.Length || context.Selections.Select(s => s.GeneId).Distinct(StringComparer.Ordinal).Count() != context.Selections.Length)
            return new(false, "one fixed draw per locus required", state);
        var frozen = new List<FrozenGene>();
        foreach (var gene in context.Model.Genes.OrderBy(g => g.Id, StringComparer.Ordinal))
        {
            var draw = context.Selections.FirstOrDefault(s => s.GeneId == gene.Id);
            if (draw is null || draw.ParentOneAllele is < 0 or > 1 || draw.ParentTwoAllele is < 0 or > 1 ||
                Math.Abs((long)draw.MutationDelta) > context.Model.MutationMagnitude || context.Model.MutationChance == 0 && draw.MutationDelta != 0 ||
                draw.MutationAllele is < 0 or > 1)
                return new(false, "invalid fixed allele selection", state);
            if (draw.MutationDelta != 0 && draw.MutationSource is null) return new(false, "mutation requires a cause", state);
            draw.MutationSource?.Validate();
            if (draw.MutationSource is { Kind: not PeopleSourceKind.Mutation }) return new(false, "mutation source kind required", state);
            var one = context.ParentOne.Genes.Single(p => p.GeneId == gene.Id);
            var two = context.ParentTwo.Genes.Single(p => p.GeneId == gene.Id);
            var a = draw.ParentOneAllele == 0 ? one.FirstAllele : one.SecondAllele;
            var b = draw.ParentTwoAllele == 0 ? two.FirstAllele : two.SecondAllele;
            if (draw.MutationAllele == 0) a = Math.Clamp(a + draw.MutationDelta, 0, 10_000);
            else b = Math.Clamp(b + draw.MutationDelta, 0, 10_000);
            var expression = gene.Rule switch
            {
                GeneExpressionRule.Codominant => (a + b) / 2,
                GeneExpressionRule.Dominant => Math.Max(a, b),
                GeneExpressionRule.Recessive => Math.Min(a, b),
                GeneExpressionRule.ParentOne => a,
                GeneExpressionRule.ParentTwo => b,
                _ => throw new InvalidDataException("Unknown inheritance rule.")
            };
            var source = new PeopleSource($"conception.{context.Id}.{gene.Id}", PeopleSourceKind.Genetic,
                $"parent bodies {context.ParentOne.BodyId}/{context.ParentTwo.BodyId}; locus {gene.Id}", context.Month, context.Model.Revision);
            frozen.Add(new(gene.Id, gene.Target, a, b, expression, gene.Weight, gene.Rule,
                draw.MutationSource is { } mutation ? [one.Source, two.Source, source, mutation] : [one.Source, two.Source, source]));
        }
        int[] Values(HeritableTargetKind kind) => Enumerable.Range(0, 5).Select(index =>
            (int)(frozen.Where(g => g.Target == new HeritableTarget(kind, index)).Sum(g => (long)g.Expression * g.Weight) / 10_000)).ToArray();
        var markers = context.ParentOne.Markers.Concat(context.ParentTwo.Markers)
            .Where(m => frozen.Any(g => g.GeneId == m.GeneId && g.Expression >= m.MinimumExpression))
            .Select(m => m with { Source = frozen.Single(g => g.GeneId == m.GeneId).Sources.Single(s => s.CauseId == $"conception.{context.Id}.{m.GeneId}") with
                { Reference = $"inherited marker {m.Id}; original source {m.Source.Reference}" } })
            .DistinctBy(m => m.Id, StringComparer.Ordinal).OrderBy(m => m.Id, StringComparer.Ordinal).ToArray();
        var record = new ConceptionRecord(context.Id, context.Month, context.LawRevision, context.SpeciesId,
            Copy(context.ParentOne), Copy(context.ParentTwo), context.Model with { Genes = context.Model.Genes.ToArray() },
            context.Selections.ToArray(), frozen.ToArray(), Values(HeritableTargetKind.BodyFoundation), Values(HeritableTargetKind.ElementRoot), markers, []);
        return new(true, "conception captured without a soul", state with { Conceptions = [.. state.Conceptions, record] });
    }

    public static InheritanceChange Develop(InheritanceState state, long conceptionId, DevelopmentRecord change)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(change);
        change.Source.Validate(); ArgumentException.ThrowIfNullOrWhiteSpace(change.EventId);
        var record = state.Conceptions.FirstOrDefault(c => c.Id == conceptionId);
        if (record is null) return new(false, "unknown conception", state);
        var existing = record.Development.FirstOrDefault(e => e.EventId == change.EventId);
        if (existing is not null) return new(existing == change, "development event already applied", state);
        if (change.Stage != DevelopmentStage.Prenatal || state.Births.Any(b => b.ConceptionId == conceptionId))
            return new(false, "use current-life development after birth", state);
        if (!Enum.IsDefined(change.Target.Kind) || (uint)change.Target.Index >= 5 || change.Amount is < -10_000 or > 10_000 ||
            change.Source.Month < record.Month || (record.Development.Length > 0 && change.Source.Month < record.Development[^1].Source.Month) ||
            change.Source.Kind is not (PeopleSourceKind.Prenatal or PeopleSourceKind.Environment) || change.Source.SoulId != 0)
            return new(false, "prenatal change requires a body/environment cause; unassigned soul unresolved", state);
        var body = record.BodyFoundations.ToArray(); var roots = record.ElementRoots.ToArray();
        var values = change.Target.Kind == HeritableTargetKind.BodyFoundation ? body : roots;
        values[change.Target.Index] = Math.Clamp(values[change.Target.Index] + change.Amount, 0, 10_000);
        var updated = record with { BodyFoundations = body, ElementRoots = roots, Development = [.. record.Development, change] };
        return new(true, "prenatal development recorded", state with { Conceptions = state.Conceptions.Select(c => c.Id == conceptionId ? updated : c).ToArray() });
    }

    public static InheritanceChange BindBirth(InheritanceState state, BirthContext context)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(context);
        context.SoulSource.Validate(); PeopleChecks.Score(context.SoulFoundation, nameof(context.SoulFoundation));
        var existing = state.Births.FirstOrDefault(b => b.BirthId == context.BirthId);
        if (existing is not null) return new(existing.ConceptionId == context.ConceptionId && existing.PersonId == context.PersonId &&
            existing.BodyId == context.BodyId && existing.SoulId == context.SoulId, "birth already assigned; no recomputation", state);
        var conception = state.Conceptions.FirstOrDefault(c => c.Id == context.ConceptionId);
        if (conception is null || context.BirthId <= 0 || context.PersonId <= 0 || context.BodyId <= 0 || context.SoulId <= 0 ||
            context.Month < conception.Month || conception.Development.Any(d => d.Source.Month > context.Month) ||
            context.SoulSource.SoulId != context.SoulId || context.SoulSource.Month != context.Month)
            return new(false, "invalid actual birth or soul evidence", state);
        if (state.Births.Any(b => b.ConceptionId == context.ConceptionId || b.PersonId == context.PersonId || b.BodyId == context.BodyId ||
            (b.SoulId == context.SoulId && b.EndedMonth is null))) return new(false, "identity already assigned", state);
        var sources = conception.Genes.SelectMany(g => g.Sources).Concat(conception.Development.Select(d => d.Source))
            .Append(context.SoulSource).Distinct().OrderBy(s => s.CauseId, StringComparer.Ordinal).ToArray();
        var innate = new InnateProfile([.. conception.BodyFoundations, context.SoulFoundation], conception.ElementRoots.ToArray(), sources);
        var birth = new BirthProfile(context.BirthId, context.ConceptionId, context.PersonId, context.BodyId, context.SoulId, context.Month, innate, conception.Markers.ToArray());
        return new(true, "soul-derived foundation bound at actual birth; skills remain separate", state with { Births = [.. state.Births, birth] });
    }

    public static InheritanceChange EndLife(InheritanceState state, long personId, long month)
    {
        ArgumentNullException.ThrowIfNull(state);
        var birth = state.Births.FirstOrDefault(b => b.PersonId == personId);
        if (birth is null || month < birth.Month) return new(false, "unknown life or invalid end time", state);
        if (birth.EndedMonth is { } ended) return new(ended == month, "life already ended", state);
        return new(true, "soul assignment released", state with { Births = state.Births.Select(b => b.PersonId == personId ? b with { EndedMonth = month } : b).ToArray() });
    }

    public static ParentGenome TransmissibleGenome(BirthProfile birth, ConceptionRecord conception)
    {
        ArgumentNullException.ThrowIfNull(birth); ArgumentNullException.ThrowIfNull(conception);
        if (birth.ConceptionId != conception.Id) throw new InvalidDataException("Birth and frozen genome must refer to the same conception.");
        return new(birth.PersonId, birth.BodyId, conception.SpeciesId, conception.Genes.Select(g => new GenePair(g.GeneId,
            g.ParentOneAllele, g.ParentTwoAllele, new PeopleSource($"conception.{conception.Id}.{g.GeneId}", PeopleSourceKind.Genetic,
                $"transmissible locus {g.GeneId}; conception {conception.Id}", conception.Month, conception.Model.Revision, birth.PersonId, birth.BodyId))).ToArray(),
            conception.Markers.ToArray());
    }

    private static ParentGenome Copy(ParentGenome parent) => parent with { Genes = parent.Genes.ToArray(), Markers = parent.Markers.ToArray() };

    public static void ValidateGenome(ParentGenome parent, GeneticModel model)
    {
        ArgumentNullException.ThrowIfNull(parent); ArgumentNullException.ThrowIfNull(model); model.Validate(); ValidateParent(parent, model);
    }

    private static void ValidateParent(ParentGenome parent, GeneticModel model)
    {
        if (parent.PersonId <= 0 || parent.BodyId <= 0 || parent.Genes.Length != model.Genes.Length ||
            parent.Genes.Select(g => g.GeneId).Distinct(StringComparer.Ordinal).Count() != parent.Genes.Length)
            throw new InvalidDataException("Invalid biological parent snapshot.");
        ArgumentException.ThrowIfNullOrWhiteSpace(parent.SpeciesId);
        foreach (var definition in model.Genes)
        {
            var pair = parent.Genes.SingleOrDefault(p => p.GeneId == definition.Id)
                ?? throw new InvalidDataException("Parent has no transmissible units for a locus.");
            PeopleChecks.Score(pair.FirstAllele, nameof(pair.FirstAllele)); PeopleChecks.Score(pair.SecondAllele, nameof(pair.SecondAllele)); pair.Source.Validate();
            if (pair.Source.SoulId != 0 || pair.Source.Kind is PeopleSourceKind.Education or PeopleSourceKind.SoulTrace or
                PeopleSourceKind.Observation or PeopleSourceKind.Conversation or PeopleSourceKind.PublicRecord)
                throw new InvalidDataException("A biological locus cannot originate from skills, reputation or an unassigned soul.");
        }
        foreach (var marker in parent.Markers)
        {
            marker.Source.Validate(); PeopleChecks.Score(marker.MinimumExpression, nameof(marker.MinimumExpression));
            var definition = TraitCatalog.Get(marker.TraitDefinitionId);
            if (!definition.MayInherit || !model.Genes.Any(g => g.Id == marker.GeneId))
                throw new InvalidDataException("Acquired skills, soul traces and social titles cannot enter a biological genome.");
        }
    }
}
