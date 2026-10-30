using System.Buffers;
using System.Numerics;
using Chronicles.Game.People;

namespace Chronicles.Game.Execution;

internal readonly record struct TalentInputStamp(ExecutionIdentity Identity, long WorldRevision, long LawRevision, string SourceFingerprint,
    LifeStatus Life = LifeStatus.Alive, long CurrentSlotPerson = 0, uint CurrentSlotGeneration = 0);

internal sealed class TalentBatchInput
{
    private readonly int[] foundations, roots, indices;
    internal TalentInputStamp Stamp { get; }
    internal int Count => indices.Length;
    internal ReadOnlySpan<int> Foundations => foundations;
    internal ReadOnlySpan<int> Roots => roots;
    internal int IndexAt(int n) => indices[n];
    internal int CanonicalAt(int n) => Aptitudes.InnateTalent(foundations, roots, indices[n]);
    internal TalentBatchInput(TalentInputStamp stamp, int[] foundations, int[] roots, IReadOnlyList<int>? indices = null)
    {
        if (stamp.Identity.Person <= 0 || stamp.Identity.Body <= 0 || stamp.Identity.Soul <= 0 || !Enum.IsDefined(stamp.Life) ||
            stamp.Life == LifeStatus.Alive && (stamp.Identity.Slot < 0 || stamp.Identity.Generation == 0) ||
            stamp.WorldRevision < 0 || stamp.LawRevision < 0 || string.IsNullOrWhiteSpace(stamp.SourceFingerprint))
            throw new ArgumentException("A batch requires current identity, revisions and an explicit source fingerprint.", nameof(stamp));
        if (foundations.Length != 6 || roots.Length != 5 || foundations.Concat(roots).Any(v => v is < 0 or > 10000))
            throw new ArgumentException("Talent input requires six foundations and five roots in 0..10000.");
        this.indices = indices?.ToArray() ?? Enumerable.Range(0, 24).ToArray();
        if (this.indices.Any(i => i is < 0 or >= 24)) throw new ArgumentOutOfRangeException(nameof(indices));
        this.foundations = (int[])foundations.Clone(); this.roots = (int[])roots.Clone(); Stamp = stamp;
    }
}

internal interface ITalentBatchBackend
{
    string Id { get; }
    bool Calculate(TalentBatchInput input, Span<int> output);
}

internal sealed class ScalarTalentBackend : ITalentBatchBackend
{
    public string Id => "canonical-csharp-scalar-v1";
    public bool Calculate(TalentBatchInput input, Span<int> output)
    {
        for (var n = 0; n < input.Count; n++) output[n] = input.CanonicalAt(n);
        return true;
    }
}

/// <summary>Comparison candidate only. Weights come from the same catalog; no native dependency or adoption claim.</summary>
internal sealed class VectorTalentBackend : ITalentBatchBackend
{
    public string Id => "catalog-csharp-vector-int32-v1";
    public bool Calculate(TalentBatchInput input, Span<int> output)
    {
        if (!Vector.IsHardwareAccelerated) return false;
        var width = Vector<int>.Count; Span<int> weights = stackalloc int[width]; Span<int> sums = stackalloc int[width];
        var n = 0;
        for (; n + width <= input.Count; n += width)
        {
            var sum = Vector<int>.Zero;
            for (var channel = 0; channel < 12; channel++)
            {
                for (var lane = 0; lane < width; lane++)
                {
                    var definition = AptitudeCatalog.Talents[input.IndexAt(n + lane)];
                    if (definition.Inputs.Sum(w => w.BasisPoints) != 10000 || definition.Inputs.Any(w =>
                        w.BasisPoints < 0 || w.Kind == AptitudeInputKind.Talent)) return false;
                    weights[lane] = definition.Inputs.Where(w => Channel(w) == channel).Sum(w => w.BasisPoints);
                }
                var value = channel < 6 ? input.Foundations[channel] : channel < 11 ? input.Roots[channel - 6] : Minimum(input.Roots);
                // Nonnegative inputs <=10000 and total weights=10000 bound the complete sum to 100,000,000.
                sum += new Vector<int>(weights) * new Vector<int>(value);
            }
            sum.CopyTo(sums);
            for (var lane = 0; lane < width; lane++) output[n + lane] = sums[lane] / 10000;
        }
        for (; n < input.Count; n++) output[n] = input.CanonicalAt(n);
        return true;
    }
    private static int Minimum(ReadOnlySpan<int> values)
    {
        var result = 10000; foreach (var value in values) result = Math.Min(result, value); return result;
    }
    private static int Channel(AptitudeWeight weight) => weight.Kind switch
    {
        AptitudeInputKind.Foundation when weight.Index is >= 0 and < 6 => weight.Index,
        AptitudeInputKind.Element when weight.Index is >= 0 and < 5 => weight.Index + 6,
        AptitudeInputKind.MinimumElement => 11,
        _ => throw new InvalidDataException("Unsupported catalog weight in a vector comparison.")
    };
}

internal enum TalentBatchStatus { Ready, CandidateDiscarded, Applied, Stale, AlreadyApplied }

internal sealed class PendingTalentBatch(TalentBatchInput input, string backend, int[]? values)
{
    internal TalentBatchInput Input { get; } = input;
    internal string Backend { get; } = backend;
    internal int[]? Values { get; } = values;
    internal TalentBatchStatus Status { get; set; } = values is null ? TalentBatchStatus.CandidateDiscarded : TalentBatchStatus.Ready;
}

internal static class InnateTalentBatch
{
    internal static PendingTalentBatch Calculate(TalentBatchInput input, ITalentBatchBackend backend)
    {
        var rented = ArrayPool<int>.Shared.Rent(Math.Max(1, input.Count));
        try
        {
            var output = rented.AsSpan(0, input.Count); output.Fill(-1);
            bool complete;
            try { complete = backend.Calculate(input, output); }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or OverflowException or NotSupportedException)
            { complete = false; }
            if (!complete) return new(input, backend.Id, null);
            for (var n = 0; n < output.Length; n++)
                if (output[n] is < 0 or > 10000 || output[n] != input.CanonicalAt(n)) return new(input, backend.Id, null);
            return new(input, backend.Id, output.ToArray());
        }
        finally { ArrayPool<int>.Shared.Return(rented, clearArray: true); }
    }

    // The caller supplies current authority identity/revisions/source before publishing the complete output.
    internal static TalentBatchStatus Commit(PendingTalentBatch pending, TalentInputStamp current, out int[] values)
    {
        values = [];
        if (pending.Status is TalentBatchStatus.Applied or TalentBatchStatus.AlreadyApplied)
        { pending.Status = TalentBatchStatus.AlreadyApplied; return pending.Status; }
        if (pending.Status == TalentBatchStatus.Stale || current != pending.Input.Stamp)
        { pending.Status = TalentBatchStatus.Stale; return pending.Status; }
        // Failed/unsupported/partial output was discarded before fallback; no stale input is recalculated here.
        var complete = pending.Values;
        if (complete is null || complete.Length != pending.Input.Count || complete.Where((v, i) =>
            v is < 0 or > 10000 || v != pending.Input.CanonicalAt(i)).Any())
            complete = Enumerable.Range(0, pending.Input.Count).Select(pending.Input.CanonicalAt).ToArray();
        values = (int[])complete.Clone(); pending.Status = TalentBatchStatus.Applied; return pending.Status;
    }
}
