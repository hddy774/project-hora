using Chronicles.Game;

namespace Chronicles.Game.Tests.Experiments;

internal sealed record ExecutionScript(long Month, GameCommand Command);
internal sealed record ExecutionBoundary(long Month, string StateHash, ulong Rng, long Births, long Deaths, long[] EventIds);
internal sealed record ExecutionComparison(ExecutionBoundary[] Boundaries, GameState Final, long IndexedFrames,
    long IndexedDueChecks, long IndexedTalentCommits);

/// <summary>Small correctness comparison only. No clock, workload timer, population builder or benchmark entry point.</summary>
internal static class ExecutionComparisonHarness
{
    internal static ExecutionComparison Compare(GameState initial, IReadOnlyList<ExecutionScript> script, long end,
        IReadOnlyList<long>? restoreAt = null)
    {
        if (initial.People.Count > 8 || end - initial.Month is < 0 or > 24)
            throw new ArgumentOutOfRangeException(nameof(end), "This functional harness accepts only small, short fixtures.");
        var indexed = GameWorld.Restore(initial); var scalar = GameWorld.Restore(initial);
        scalar.UseScalarExecutionReferenceForVerification();
        var boundaries = new List<ExecutionBoundary>(); long frames = 0, dueChecks = 0, talentCommits = 0;
        for (var month = initial.Month; month <= end; month++)
        {
            indexed.AdvanceTo(month); scalar.AdvanceTo(month);
            Equal(indexed, scalar, "month " + month);
            foreach (var step in script.Where(s => s.Month == month))
            {
                var left = indexed.Execute(step.Command); var right = scalar.Execute(step.Command);
                if (!left.Accepted || left != right) throw new InvalidOperationException(step.Command.Id + ": " + left.Message);
                Equal(indexed, scalar, step.Command.Id);
            }
            var snapshot = indexed.Checkpoint();
            boundaries.Add(new(month, indexed.StateHash(), snapshot.RandomState, snapshot.BirthCount, snapshot.DeathCount,
                snapshot.Events.Select(e => e.Id).ToArray()));
            if (restoreAt?.Contains(month) == true)
            {
                var diagnostics = indexed.ExecutionDiagnostics;
                frames += diagnostics.Frames; dueChecks += diagnostics.DueChecks; talentCommits += diagnostics.TalentCommits;
                indexed = GameCodec.Decode(GameCodec.Encode(indexed)); scalar = GameCodec.Decode(GameCodec.Encode(scalar));
                scalar.UseScalarExecutionReferenceForVerification(); Equal(indexed, scalar, "restored " + month);
            }
        }
        var last = indexed.ExecutionDiagnostics;
        return new(boundaries.ToArray(), indexed.Checkpoint(), frames + last.Frames, dueChecks + last.DueChecks,
            talentCommits + last.TalentCommits);
    }

    private static void Equal(GameWorld indexed, GameWorld scalar, string boundary)
    {
        var left = indexed.Checkpoint(); var right = scalar.Checkpoint();
        if (indexed.StateHash() != scalar.StateHash() || left.RandomState != right.RandomState ||
            !left.Events.SequenceEqual(right.Events) || left.BirthCount != right.BirthCount || left.DeathCount != right.DeathCount ||
            !left.ActiveSlots.Select(s => (s.Person, s.Generation)).SequenceEqual(right.ActiveSlots.Select(s => (s.Person, s.Generation))))
            throw new InvalidOperationException("Indexed/scalar authority differs at " + boundary);
        Invariants.Validate(left); Invariants.Validate(right);
    }
}
