using System.Security.Cryptography;
using System.Text.Json;

namespace Chronicles.Game.People;

// A derived, immutable publication. It cannot grant learned proficiency or change the source profile.
internal sealed class InnateTalentSnapshot
{
    private readonly int[] values;
    private readonly string sourceFingerprint;
    internal InnateTalentSnapshot(InnateProfile source, int[] completeValues)
    {
        source.Validate();
        if (completeValues.Length != 24 || completeValues.Where((v, i) =>
            v is < 0 or > 10000 || v != Aptitudes.InnateTalent(source.Foundations, source.ElementRoots, i)).Any())
            throw new InvalidDataException("Precomputed innate talents must equal the complete canonical source calculation.");
        values = (int[])completeValues.Clone(); sourceFingerprint = Fingerprint(source);
    }
    internal int[] Read(InnateProfile current)
    {
        if (sourceFingerprint != Fingerprint(current)) throw new InvalidDataException("Precomputed innate source changed.");
        return (int[])values.Clone();
    }
    private static string Fingerprint(InnateProfile source) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(source)));
}
