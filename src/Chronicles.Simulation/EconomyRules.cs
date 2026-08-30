namespace Chronicles.Simulation;

public enum MaterialCategory : byte
{
    Treasure,
    Pill,
    Miscellaneous,
}

public enum MaterialId : byte
{
    MysticIron,
    CrimsonCopper,
    AzureJade,
    Thunderwood,
    FrostCrystal,
    FlameStone,
    WindSand,
    EarthGold,
    StarSand,
    VoidStone,
    SpiritMushroom,
    VermilionFruit,
    ClearMindHerb,
    BloodFlower,
    MoonLotus,
    NourishingGinseng,
    FireRoot,
    ColdPondWater,
    JadeSap,
    AncientHoney,
    BeastHide,
    BeastCoreFragment,
    TalismanPaper,
    SpiritInk,
    MedicineBottle,
    Silk,
    Timber,
    Stone,
    Salt,
    Incense,
}

public enum TreasureSlot : byte
{
    NatalTreasure,
    ArmorOne,
    ArmorTwo,
    ArmorThree,
    WeaponOne,
    WeaponTwo,
    WeaponThree,
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
    Treasure,
    TechniqueBook,
    SpellBook,
}

public readonly record struct MaterialDefinition(
    MaterialId Id,
    string KoreanName,
    MaterialCategory Category,
    int CityBasePrice);

public readonly record struct MarketGoodDefinition(
    int Id,
    MarketGoodKind Kind,
    MaterialId? Material,
    int CityBasePrice);

public static class EconomyRules
{
    public const ushort EmptyTreasureQuality = ushort.MaxValue;
    public const int MaterialCount = 30;
    public const int TreasureSlotCount = 7;
    public const int MissionRequiredItems = 2;
    public const int GrowthPillPrice = 50;
    public const int BreakthroughPillPrice = 100;
    public const int MarketGoodCount = MaterialCount + 5;
    public const int GrowthPillGoodId = MaterialCount;
    public const int BreakthroughPillGoodId = MaterialCount + 1;
    public const int TreasureGoodId = MaterialCount + 2;
    public const int TechniqueBookGoodId = MaterialCount + 3;
    public const int SpellBookGoodId = MaterialCount + 4;
    public const int TreasureBasePrice = 240;
    public const int TechniqueBookBasePrice = 300;
    public const int SpellBookBasePrice = 300;
    public const int TechniqueBookProductionCost = 150;
    public const int SpellBookProductionCost = 150;

    private static readonly MaterialDefinition[] Materials =
    [
        new(MaterialId.MysticIron, "현철", MaterialCategory.Treasure, 12),
        new(MaterialId.CrimsonCopper, "적동", MaterialCategory.Treasure, 12),
        new(MaterialId.AzureJade, "청옥", MaterialCategory.Treasure, 12),
        new(MaterialId.Thunderwood, "뇌격목", MaterialCategory.Treasure, 12),
        new(MaterialId.FrostCrystal, "한빙정", MaterialCategory.Treasure, 12),
        new(MaterialId.FlameStone, "화정석", MaterialCategory.Treasure, 12),
        new(MaterialId.WindSand, "풍령사", MaterialCategory.Treasure, 12),
        new(MaterialId.EarthGold, "지맥금", MaterialCategory.Treasure, 12),
        new(MaterialId.StarSand, "성진사", MaterialCategory.Treasure, 12),
        new(MaterialId.VoidStone, "공명석", MaterialCategory.Treasure, 12),
        new(MaterialId.SpiritMushroom, "영지", MaterialCategory.Pill, 8),
        new(MaterialId.VermilionFruit, "주과", MaterialCategory.Pill, 8),
        new(MaterialId.ClearMindHerb, "청심초", MaterialCategory.Pill, 8),
        new(MaterialId.BloodFlower, "응혈화", MaterialCategory.Pill, 8),
        new(MaterialId.MoonLotus, "월광련", MaterialCategory.Pill, 8),
        new(MaterialId.NourishingGinseng, "자양삼", MaterialCategory.Pill, 8),
        new(MaterialId.FireRoot, "화령근", MaterialCategory.Pill, 8),
        new(MaterialId.ColdPondWater, "한담수", MaterialCategory.Pill, 8),
        new(MaterialId.JadeSap, "옥수액", MaterialCategory.Pill, 8),
        new(MaterialId.AncientHoney, "천년봉밀", MaterialCategory.Pill, 8),
        new(MaterialId.BeastHide, "야수 가죽", MaterialCategory.Miscellaneous, 4),
        new(MaterialId.BeastCoreFragment, "요단 파편", MaterialCategory.Miscellaneous, 4),
        new(MaterialId.TalismanPaper, "부적지", MaterialCategory.Miscellaneous, 4),
        new(MaterialId.SpiritInk, "영묵", MaterialCategory.Miscellaneous, 4),
        new(MaterialId.MedicineBottle, "약병", MaterialCategory.Miscellaneous, 4),
        new(MaterialId.Silk, "비단", MaterialCategory.Miscellaneous, 4),
        new(MaterialId.Timber, "목재", MaterialCategory.Miscellaneous, 4),
        new(MaterialId.Stone, "석재", MaterialCategory.Miscellaneous, 4),
        new(MaterialId.Salt, "소금", MaterialCategory.Miscellaneous, 4),
        new(MaterialId.Incense, "향료", MaterialCategory.Miscellaneous, 4),
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

    public static int GetPillPrice(PillKind kind) => kind switch
    {
        PillKind.Growth => GrowthPillPrice,
        PillKind.Breakthrough => BreakthroughPillPrice,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static int GetPillGoodId(PillKind kind) => kind switch
    {
        PillKind.Growth => GrowthPillGoodId,
        PillKind.Breakthrough => BreakthroughPillGoodId,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static MarketGoodDefinition GetMarketGood(int goodId)
    {
        if (goodId is >= 0 and < MaterialCount)
        {
            var material = GetMaterial((MaterialId)goodId);
            return new MarketGoodDefinition(
                goodId,
                MarketGoodKind.Material,
                material.Id,
                material.CityBasePrice);
        }

        return goodId switch
        {
            GrowthPillGoodId => new(goodId, MarketGoodKind.GrowthPill, null, GrowthPillPrice),
            BreakthroughPillGoodId => new(goodId, MarketGoodKind.BreakthroughPill, null, BreakthroughPillPrice),
            TreasureGoodId => new(goodId, MarketGoodKind.Treasure, null, TreasureBasePrice),
            TechniqueBookGoodId => new(goodId, MarketGoodKind.TechniqueBook, null, TechniqueBookBasePrice),
            SpellBookGoodId => new(goodId, MarketGoodKind.SpellBook, null, SpellBookBasePrice),
            _ => throw new ArgumentOutOfRangeException(nameof(goodId)),
        };
    }

    public static bool IsArmor(TreasureSlot slot) =>
        slot is TreasureSlot.ArmorOne or TreasureSlot.ArmorTwo or TreasureSlot.ArmorThree;

    public static bool IsWeapon(TreasureSlot slot) =>
        slot is TreasureSlot.WeaponOne or TreasureSlot.WeaponTwo or TreasureSlot.WeaponThree;
}
