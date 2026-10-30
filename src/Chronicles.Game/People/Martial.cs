namespace Chronicles.Game.People;

public enum MartialStage { Untrained, Stage01, Stage02, Stage03, Stage04, Stage05, Stage06, Stage07, Stage08, Stage09, Stage10 }
public enum MartialActionKind { Training, Breakthrough }
public sealed record MartialAction(string EventId, MartialActionKind Kind, long Month, long Work, int SkillGain, PeopleSource Source);
public sealed record MartialProfile(long PersonId, long BodyId, MartialStage Stage, long Work, int Skill, MartialAction[] History)
{
    public static MartialProfile Beginner(long personId, long bodyId) => new(personId, bodyId, MartialStage.Untrained, 0, 0, []);
}
public sealed record MartialPolicy(long[] RequiredWork, int[] RequiredSkill, long[] BasicLifespanMonths,
    string PolicyId = "PC-TEMP-V021-MARTIAL-01", long Revision = 1, bool RequiresReview = true)
{
    public static MartialPolicy Provisional() => new(Enumerable.Range(1, 10).Select(i => 12_000L * i * i).ToArray(),
        Enumerable.Range(1, 10).Select(i => i * 500).ToArray(), Enumerable.Range(1, 10).Select(i => 1200L + i * 600).ToArray());
    public void Validate()
    {
        if (Revision < 1 || RequiredWork.Length != 10 || RequiredSkill.Length != 10 || BasicLifespanMonths.Length != 10 ||
            RequiredWork.Any(w => w < 1) || RequiredSkill.Any(s => s is < 0 or > 10_000) ||
            BasicLifespanMonths.Any(l => l is < 1 or > 7200) || BasicLifespanMonths[^1] != 7200)
            throw new InvalidDataException("Martial policy requires ten independent stages and a 600-year maximum basic lifespan.");
    }
}
/// <summary>Root aptitude and cultivation realm are intentionally absent.</summary>
public sealed record MartialTrainingContext(string EventId, long Month, int CompletedMonths, int Bone, int Constitution, int Mind,
    int Health, int ActualPracticeSkillGain, PeopleSource Source);
public sealed record MartialBreakthroughContext(string EventId, long Month, int Health, bool ActualTechniqueKnown, PeopleSource Source);
public sealed record MartialChange(bool Accepted, string Code, MartialProfile State);

public static class MartialTraining
{
    public static MartialChange Train(MartialProfile state, MartialTrainingContext context)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(context);
        Validate(state); context.Source.Validate(); ArgumentException.ThrowIfNullOrWhiteSpace(context.EventId);
        var existing = state.History.FirstOrDefault(h => h.EventId == context.EventId);
        if (existing is not null) return new(existing.Kind == MartialActionKind.Training, "training event already resolved", state);
        PeopleChecks.Score(context.Bone, nameof(context.Bone)); PeopleChecks.Score(context.Constitution, nameof(context.Constitution));
        PeopleChecks.Score(context.Mind, nameof(context.Mind)); PeopleChecks.Score(context.Health, nameof(context.Health));
        if (context.CompletedMonths < 1 || context.CompletedMonths > 1200 || context.ActualPracticeSkillGain is < 0 or > 10_000 ||
            context.Month != context.Source.Month || (state.History.Length > 0 && context.Month < state.History[^1].Month))
            return new(false, "invalid completed martial practice", state);
        var aptitude = (context.Bone * 5000L + context.Constitution * 3500L + context.Mind * 1500L) / 10_000;
        var work = context.CompletedMonths * aptitude * context.Health / 10_000;
        var action = new MartialAction(context.EventId, MartialActionKind.Training, context.Month, work, context.ActualPracticeSkillGain, context.Source);
        return new(true, "martial practice advanced independently of spirit roots", state with
        {
            Work = checked(state.Work + work), Skill = Math.Min(10_000, state.Skill + context.ActualPracticeSkillGain), History = [.. state.History, action]
        });
    }

    public static MartialChange Breakthrough(MartialProfile state, MartialBreakthroughContext context, MartialPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(policy);
        Validate(state); policy.Validate(); context.Source.Validate(); PeopleChecks.Score(context.Health, nameof(context.Health));
        ArgumentException.ThrowIfNullOrWhiteSpace(context.EventId);
        var existing = state.History.FirstOrDefault(h => h.EventId == context.EventId);
        if (existing is not null) return new(existing.Kind == MartialActionKind.Breakthrough, "martial breakthrough already resolved", state);
        if (context.Month != context.Source.Month || (state.History.Length > 0 && context.Month < state.History[^1].Month) ||
            state.Stage == MartialStage.Stage10 || !context.ActualTechniqueKnown || context.Health < 5000)
            return new(false, "martial breakthrough conditions missing", state);
        var index = (int)state.Stage;
        if (state.Work < policy.RequiredWork[index] || state.Skill < policy.RequiredSkill[index]) return new(false, "martial preparation incomplete", state);
        var action = new MartialAction(context.EventId, MartialActionKind.Breakthrough, context.Month, -policy.RequiredWork[index], 0, context.Source);
        return new(true, "one independent martial stage completed", state with
        {
            Stage = state.Stage + 1, Work = state.Work - policy.RequiredWork[index], History = [.. state.History, action]
        });
    }

    public static long BasicLifespanMonths(MartialProfile state, MartialPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(policy); Validate(state); policy.Validate();
        return state.Stage == MartialStage.Untrained ? 1200 : policy.BasicLifespanMonths[(int)state.Stage - 1];
    }

    public static long CombatContribution(MartialProfile state)
    {
        ArgumentNullException.ThrowIfNull(state); Validate(state);
        return (long)state.Stage * (long)state.Stage * 64;
    }

    public static void Validate(MartialProfile state)
    {
        ArgumentNullException.ThrowIfNull(state);
        PeopleChecks.Score(state.Skill, nameof(state.Skill));
        if (state.PersonId <= 0 || state.BodyId <= 0 || !Enum.IsDefined(state.Stage) || state.Work < 0 ||
            state.History.Select(h => h.EventId).Distinct(StringComparer.Ordinal).Count() != state.History.Length)
            throw new InvalidDataException("Invalid independent martial identity/progress.");
    }
}
