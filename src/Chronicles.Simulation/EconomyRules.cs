using Chronicles.Core;

namespace Chronicles.Simulation;

public enum MaterialCategory : byte
{
    Treasure,
    Pill,
}

public enum MaterialId : byte
{
    TreasureMaterial,
    PillMaterial,
}

public enum TreasureSlot : byte
{
    NatalTreasure,
    DefenseTreasure,
    AttackTreasure,
}

public enum PillKind : byte
{
    Growth,
    Breakthrough,
}

public enum MarketGoodKind : byte
{
    Material,
    GrowthPill,
    BreakthroughPill,
    DefenseTreasure,
    AttackTreasure,
    TechniqueBook,
    SpellBook,
}

public readonly record struct MaterialDefinition(
    MaterialId Id,
    string KoreanName,
    MaterialCategory Category,
    long CityBasePrice);

public readonly record struct MarketGoodDefinition(
    int Id,
    MarketGoodKind Kind,
    MaterialId? Material,
    RealmStage? Realm,
    long CityBasePrice);

public static class EconomyRules
{
    public const int CurrentRevision = 3;
    public const ushort EmptyTreasureQuality = ushort.MaxValue;
    public const int MaterialCount = 2;
    public const int TreasureSlotCount = 3;
    public const int MissionRequiredItems = 2;
    public const int RealmItemCount = 4;
    public const int RealmCount = 10;
    public const int RealmGoodStartId = MaterialCount;
    public const int TechniqueBookGoodId = RealmGoodStartId + (RealmCount * RealmItemCount);
    public const int SpellBookGoodId = TechniqueBookGoodId + 1;
    public const int MarketGoodCount = SpellBookGoodId + 1;
    public const int LegacyMaterialCount = 30;
    public const int LegacyTreasureSlotCount = 7;
    public const int LegacyMarketGoodCount = 35;
    public const int InitialSkillMaximumHundredths = 7_999;
    public const long TechniqueBookBasePrice = 20;
    public const long SpellBookBasePrice = 20;
    public const int CityProductionTargetStock = 3;
    public const int CityProductionMonths = 3;

    private static readonly long[] RealmPriceScales =
    [
        1,
        4,
        16,
        64,
        256,
        1_024,
        4_096,
        16_384,
        65_536,
        262_144,
    ];

    private static readonly MaterialDefinition[] Materials =
    [
        new(MaterialId.TreasureMaterial, "법보 재료", MaterialCategory.Treasure, 24),
        new(MaterialId.PillMaterial, "단약 재료", MaterialCategory.Pill, 16),
    ];

    public static IReadOnlyList<MaterialDefinition> AllMaterials => Materials;

    public static MaterialDefinition GetMaterial(MaterialId id)
    {
        var index = (int)id;
        if ((uint)index >= (uint)Materials.Length || Materials[index].Id != id)
        {
            throw new ArgumentOutOfRangeException(nameof(id));
        }

        return Materials[index];
    }

    public static int GetPillGoodId(PillKind kind, RealmStage realm) =>
        GetRealmGoodId(kind == PillKind.Growth
            ? MarketGoodKind.GrowthPill
            : MarketGoodKind.BreakthroughPill, realm);

    public static int GetTreasureGoodId(TreasureSlot slot, RealmStage realm) => slot switch
    {
        TreasureSlot.DefenseTreasure => GetRealmGoodId(MarketGoodKind.DefenseTreasure, realm),
        TreasureSlot.AttackTreasure => GetRealmGoodId(MarketGoodKind.AttackTreasure, realm),
        TreasureSlot.NatalTreasure => throw new ArgumentOutOfRangeException(
            nameof(slot),
            "본명법보는 시장 상품이 아니며 수선자가 직접 제작합니다."),
        _ => throw new ArgumentOutOfRangeException(nameof(slot)),
    };

    public static int GetRealmGoodId(MarketGoodKind kind, RealmStage realm)
    {
        var kindOffset = kind switch
        {
            MarketGoodKind.GrowthPill => 0,
            MarketGoodKind.BreakthroughPill => 1,
            MarketGoodKind.DefenseTreasure => 2,
            MarketGoodKind.AttackTreasure => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        var realmIndex = ValidateRealm(realm);
        return checked(RealmGoodStartId + (realmIndex * RealmItemCount) + kindOffset);
    }

    public static MarketGoodDefinition GetMarketGood(int goodId)
    {
        if (goodId is >= 0 and < MaterialCount)
        {
            var material = GetMaterial((MaterialId)goodId);
            return new MarketGoodDefinition(
                goodId,
                MarketGoodKind.Material,
                material.Id,
                null,
                material.CityBasePrice);
        }

        if (goodId is >= RealmGoodStartId and < TechniqueBookGoodId)
        {
            var realmIndex = (goodId - RealmGoodStartId) / RealmItemCount;
            var kindOffset = (goodId - RealmGoodStartId) % RealmItemCount;
            var kind = kindOffset switch
            {
                0 => MarketGoodKind.GrowthPill,
                1 => MarketGoodKind.BreakthroughPill,
                2 => MarketGoodKind.DefenseTreasure,
                3 => MarketGoodKind.AttackTreasure,
                _ => throw new InvalidOperationException("대경지 상품 종류가 잘못되었습니다."),
            };
            var realm = (RealmStage)realmIndex;
            return new MarketGoodDefinition(
                goodId,
                kind,
                null,
                realm,
                GetRealmItemBasePrice(kind, realm));
        }

        return goodId switch
        {
            TechniqueBookGoodId => new(
                goodId,
                MarketGoodKind.TechniqueBook,
                null,
                null,
                TechniqueBookBasePrice),
            SpellBookGoodId => new(
                goodId,
                MarketGoodKind.SpellBook,
                null,
                null,
                SpellBookBasePrice),
            _ => throw new ArgumentOutOfRangeException(nameof(goodId)),
        };
    }

    public static long GetRealmItemBasePrice(MarketGoodKind kind, RealmStage realm)
    {
        var scale = RealmPriceScales[ValidateRealm(realm)];
        var basePrice = kind switch
        {
            MarketGoodKind.GrowthPill => 5L,
            MarketGoodKind.BreakthroughPill => 10L,
            MarketGoodKind.DefenseTreasure => 20L,
            MarketGoodKind.AttackTreasure => 24L,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        return checked(basePrice * scale);
    }

    public static int GetProductionMaterialCost(MarketGoodKind kind, RealmStage? realm) => kind switch
    {
        MarketGoodKind.GrowthPill or MarketGoodKind.BreakthroughPill =>
            1 + (realm is { } pillRealm ? (int)pillRealm / 2 : 0),
        MarketGoodKind.DefenseTreasure or MarketGoodKind.AttackTreasure =>
            2 + (realm is { } treasureRealm ? (int)treasureRealm / 2 : 0),
        MarketGoodKind.TechniqueBook or MarketGoodKind.SpellBook => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static long GetProductionSpiritStoneCost(MarketGoodDefinition definition) =>
        Math.Max(10L, definition.CityBasePrice / 2);

    public static long GetNatalCraftSpiritStoneCost(RealmStage realm) =>
        GetRealmItemBasePrice(MarketGoodKind.AttackTreasure, realm);

    public static long GetMiningOutput(RealmStage realm) =>
        checked(200L * ((int)realm + 1));

    public static long GetQualityPriceMultiplierBasisPoints(QualityValue quality)
    {
        if (quality.Hundredths < 8_000)
        {
            var profile = QualityRules.GetProfile(quality.Tier);
            return checked(profile.EffectNumeratorHalves * 5_000L);
        }

        // 80.00부터는 품질 1.00마다 가격을 두 배로 올린다. 같은 1.00 안의 0.01은
        // 선형 보간해 가격이 계단식으로 멈추지 않게 한다.
        var wholeSteps = (quality.Hundredths / 100) - 80;
        var remainder = quality.Hundredths % 100;
        var baseMultiplier = checked(450_000L << wholeSteps);
        return checked(baseMultiplier + (baseMultiplier * remainder / 100));
    }

    public static long GetQualityAdjustedPrice(long basePrice, QualityValue quality)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(basePrice);
        var multiplier = GetQualityPriceMultiplierBasisPoints(quality);
        var quotient = basePrice / 10_000L;
        var remainder = basePrice % 10_000L;
        return checked((quotient * multiplier) + (remainder * multiplier / 10_000L));
    }

    public static bool IsArmor(TreasureSlot slot) => slot == TreasureSlot.DefenseTreasure;

    public static bool IsWeapon(TreasureSlot slot) => slot == TreasureSlot.AttackTreasure;

    public static MaterialId GetRequiredMaterial(MarketGoodKind kind) => kind switch
    {
        MarketGoodKind.DefenseTreasure or MarketGoodKind.AttackTreasure => MaterialId.TreasureMaterial,
        MarketGoodKind.GrowthPill or MarketGoodKind.BreakthroughPill => MaterialId.PillMaterial,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static int ValidateRealm(RealmStage realm)
    {
        var value = (int)realm;
        if ((uint)value >= RealmCount)
        {
            throw new ArgumentOutOfRangeException(nameof(realm));
        }

        return value;
    }
}
