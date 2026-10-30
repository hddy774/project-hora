namespace Chronicles.Game.People;

/// <summary>Read-only aptitude input for the cultivation-owned martial engine. It has no spirit-root input.</summary>
public sealed record MartialAptitudeContext(int Bone, int Constitution, int Mind, int Health);
public sealed record MartialAptitudeResult(int BodilyFoundation, int CurrentExpression,
    string PolicyId = "PC-TEMP-V021-MARTIAL-APTITUDE-01", long Revision = 1, bool RequiresReview = true);

public static class MartialAptitude
{
    public static MartialAptitudeResult Assess(MartialAptitudeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        PeopleChecks.Score(context.Bone, nameof(context.Bone)); PeopleChecks.Score(context.Constitution, nameof(context.Constitution));
        PeopleChecks.Score(context.Mind, nameof(context.Mind)); PeopleChecks.Score(context.Health, nameof(context.Health));
        var foundation = (int)((context.Bone * 5000L + context.Constitution * 3500L + context.Mind * 1500L) / 10_000);
        return new(foundation, foundation * context.Health / 10_000);
    }
}
