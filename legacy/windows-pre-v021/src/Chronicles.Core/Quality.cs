using System.Globalization;

namespace Chronicles.Core;

public enum QualityTier : byte
{
    Scrap,
    ExtremeLow,
    Low,
    Medium,
    High,
    ExtremeHigh,
    Heaven,
    Flawless,
}

public readonly record struct QualityValue : IComparable<QualityValue>
{
    public const ushort MaximumHundredths = 10_000;

    public QualityValue(ushort hundredths)
    {
        if (hundredths > MaximumHundredths)
        {
            throw new ArgumentOutOfRangeException(nameof(hundredths));
        }

        Hundredths = hundredths;
    }

    public ushort Hundredths { get; }
    public decimal Amount => Hundredths / 100m;
    public QualityTier Tier => QualityRules.GetTier(this);

    public static QualityValue FromHundredths(int hundredths) =>
        new(checked((ushort)hundredths));

    public static QualityValue FromWhole(int value) =>
        FromHundredths(checked(value * 100));

    public int CompareTo(QualityValue other) => Hundredths.CompareTo(other.Hundredths);

    public static bool operator <(QualityValue left, QualityValue right) =>
        left.Hundredths < right.Hundredths;

    public static bool operator <=(QualityValue left, QualityValue right) =>
        left.Hundredths <= right.Hundredths;

    public static bool operator >(QualityValue left, QualityValue right) =>
        left.Hundredths > right.Hundredths;

    public static bool operator >=(QualityValue left, QualityValue right) =>
        left.Hundredths >= right.Hundredths;

    public override string ToString() => Amount.ToString("0.##", CultureInfo.CurrentCulture);
}

public readonly record struct QualityProfile(
    QualityTier Tier,
    string KoreanName,
    ushort MinimumHundredths,
    ushort MaximumHundredths,
    int EffectNumeratorHalves);

public static class QualityRules
{
    private static readonly QualityProfile[] Profiles =
    [
        new(QualityTier.Scrap, "폐품", 0, 999, 2),
        new(QualityTier.ExtremeLow, "극하품", 1_000, 2_999, 10),
        // 29.99와 30.00의 명시값을 따라 이 경계는 약 2배로 적용한다.
        new(QualityTier.Low, "하품", 3_000, 3_999, 20),
        new(QualityTier.Medium, "중품", 4_000, 5_999, 30),
        new(QualityTier.High, "상품", 6_000, 7_999, 45),
        new(QualityTier.ExtremeHigh, "극상품", 8_000, 8_999, 90),
        new(QualityTier.Heaven, "천품", 9_000, 9_999, 360),
        new(QualityTier.Flawless, "무결품", 10_000, 10_000, 3_600),
    ];

    public static IReadOnlyList<QualityProfile> All => Profiles;

    public static QualityTier GetTier(QualityValue value)
    {
        var raw = value.Hundredths;
        return raw switch
        {
            < 1_000 => QualityTier.Scrap,
            < 3_000 => QualityTier.ExtremeLow,
            < 4_000 => QualityTier.Low,
            < 6_000 => QualityTier.Medium,
            < 8_000 => QualityTier.High,
            < 9_000 => QualityTier.ExtremeHigh,
            < 10_000 => QualityTier.Heaven,
            _ => QualityTier.Flawless,
        };
    }

    public static QualityProfile GetProfile(QualityTier tier)
    {
        var index = (int)tier;
        if ((uint)index >= (uint)Profiles.Length || Profiles[index].Tier != tier)
        {
            throw new ArgumentOutOfRangeException(nameof(tier));
        }

        return Profiles[index];
    }

    public static long GetEffectiveHundredths(QualityValue value)
    {
        var profile = GetProfile(value.Tier);
        return checked((long)value.Hundredths * profile.EffectNumeratorHalves / 2);
    }

    public static int GetEffectiveWhole(QualityValue value) =>
        checked((int)(GetEffectiveHundredths(value) / 100));

    public static QualityValue GetNextTierMinimum(QualityValue value)
    {
        if (value.Tier == QualityTier.Flawless)
        {
            return value;
        }

        return new QualityValue(GetProfile((QualityTier)((int)value.Tier + 1)).MinimumHundredths);
    }

    public static string Format(QualityValue value) =>
        $"{value} · {GetProfile(value.Tier).KoreanName}";
}
