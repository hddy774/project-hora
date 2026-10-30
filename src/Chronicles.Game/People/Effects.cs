namespace Chronicles.Game.People;

public enum PeopleSourceKind { Founding, Genetic, Mutation, Prenatal, Development, Education, Environment, SoulTrace, Trait, Formation, Equipment, Observation, Conversation, PublicRecord }
public enum EffectDomain { Talent, Cognition, Combat }
public enum EffectOperation { Add, MultiplyBasisPoints }
public enum EffectRole { Contribution, IncludedInBaseline, DerivedMarker }

/// <summary>CauseId identifies the underlying cause across every representation of it.</summary>
public sealed record PeopleSource(string CauseId, PeopleSourceKind Kind, string Reference, long Month, long Revision = 1,
    long PersonId = 0, long BodyId = 0, long SoulId = 0)
{
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(CauseId);
        ArgumentException.ThrowIfNullOrWhiteSpace(Reference);
        if (Month < 0 || Revision < 1 || PersonId < 0 || BodyId < 0 || SoulId < 0 || !Enum.IsDefined(Kind))
            throw new InvalidDataException("Invalid people effect provenance.");
    }
}

public readonly record struct EffectTarget(EffectDomain Domain, int Index);
public sealed record PeopleEffect(string Id, EffectTarget Target, EffectOperation Operation, long Amount,
    PeopleSource Source, EffectRole Role = EffectRole.Contribution, string ExclusiveGroup = "");
public sealed record EffectDecision(string EffectId, string CauseId, string Reason);
public sealed record EffectResolution(PeopleEffect[] Applied, EffectDecision[] Decisions)
{
    public long Apply(EffectTarget target, long basis, long minimum = 0, long maximum = 10_000)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minimum, maximum);
        var relevant = Applied.Where(e => e.Target == target).ToArray();
        var value = checked(basis + relevant.Where(e => e.Operation == EffectOperation.Add).Sum(e => e.Amount));
        foreach (var effect in relevant.Where(e => e.Operation == EffectOperation.MultiplyBasisPoints))
            value = checked(value * (10_000 + effect.Amount)) / 10_000;
        return Math.Clamp(value, minimum, maximum);
    }
}

public static class PeopleEffects
{
    public static EffectResolution Resolve(IEnumerable<PeopleEffect> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);
        var input = effects.OrderBy(e => e.Id, StringComparer.Ordinal).ToArray();
        if (input.Select(e => e.Id).Distinct(StringComparer.Ordinal).Count() != input.Length)
            throw new InvalidDataException("Effect instance identities must be unique.");
        foreach (var effect in input)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(effect.Id);
            effect.Source.Validate();
            var count = effect.Target.Domain switch { EffectDomain.Talent => 24, EffectDomain.Cognition => 10, EffectDomain.Combat => 8, _ => 0 };
            if ((uint)effect.Target.Index >= count || !Enum.IsDefined(effect.Role) || !Enum.IsDefined(effect.Operation) ||
                effect.Amount is < -1_000_000 or > 1_000_000 ||
                (effect.Operation == EffectOperation.MultiplyBasisPoints && effect.Amount < -10_000))
                throw new InvalidDataException("Invalid effect target or amount.");
        }

        var decisions = new List<EffectDecision>();
        var canonical = new List<PeopleEffect>();
        foreach (var group in input.GroupBy(e => (e.Source.CauseId, e.Target, e.Operation)))
        {
            var actual = group.Where(e => e.Role != EffectRole.DerivedMarker).ToArray();
            if (actual.Select(e => e.Amount).Distinct().Skip(1).Any() ||
                actual.Select(e => e.ExclusiveGroup).Distinct(StringComparer.Ordinal).Skip(1).Any())
                throw new InvalidDataException("Representations of one cause disagree; resolve provenance before applying effects.");
            var included = actual.Any(e => e.Role == EffectRole.IncludedInBaseline);
            var selected = included ? null : actual.FirstOrDefault(e => e.Role == EffectRole.Contribution);
            if (selected is not null) canonical.Add(selected);
            foreach (var effect in group)
                decisions.Add(new(effect.Id, effect.Source.CauseId, effect == selected ? "applied" :
                    effect.Role == EffectRole.DerivedMarker ? "derived marker has no effect" :
                    included ? "cause already included in baseline" : "duplicate representation of cause"));
        }

        var applied = new List<PeopleEffect>();
        foreach (var effect in canonical)
        {
            if (effect.ExclusiveGroup.Length == 0) { applied.Add(effect); continue; }
            var winner = canonical.Where(e => e.Target == effect.Target && e.Operation == effect.Operation &&
                    string.Equals(e.ExclusiveGroup, effect.ExclusiveGroup, StringComparison.Ordinal))
                .OrderByDescending(e => Math.Abs(e.Amount)).ThenBy(e => e.Id, StringComparer.Ordinal).First();
            if (effect == winner) applied.Add(effect);
            else
            {
                var i = decisions.FindIndex(d => d.EffectId == effect.Id);
                decisions[i] = decisions[i] with { Reason = "stronger exclusive effect applies" };
            }
        }
        return new(applied.OrderBy(e => e.Source.CauseId, StringComparer.Ordinal).ThenBy(e => e.Id, StringComparer.Ordinal).ToArray(),
            decisions.OrderBy(e => e.EffectId, StringComparer.Ordinal).ToArray());
    }
}
