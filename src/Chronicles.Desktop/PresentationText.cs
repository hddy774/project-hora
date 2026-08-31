using System.Globalization;
using Chronicles.Core;
using Chronicles.Simulation;

namespace Chronicles.Desktop;

internal static class PresentationText
{
    public static string FormatRealm(
        RealmStage stage,
        int layer,
        bool greatPerfection = false)
    {
        var boundedLayer = Math.Clamp(layer, 1, RealmRules.MinorStageCount);
        var minorStage = greatPerfection && boundedLayer == RealmRules.MinorStageCount
            ? "대원만"
            : boundedLayer switch
            {
                <= 3 => "초기",
                <= 6 => "중기",
                _ => "후기",
            };
        return $"{FormatRealmStage(stage)} · {minorStage} · {boundedLayer.ToString(CultureInfo.InvariantCulture)}층";
    }

    public static string FormatRealmStage(RealmStage stage) => stage switch
    {
        RealmStage.QiRefining => "연기경",
        RealmStage.FoundationEstablishment => "축기경",
        RealmStage.GoldenCore => "금단경",
        RealmStage.NascentSoul => "원영경",
        RealmStage.SpiritTransformation => "화신경",
        RealmStage.VoidRefinement => "연허경",
        RealmStage.BodyIntegration => "합체경",
        RealmStage.TribulationCrossing => "도겁경",
        RealmStage.Mahayana => "대승경",
        RealmStage.HalfImmortal => "반선경",
        _ => stage.ToString(),
    };

    public static string FormatActivity(AgentActivity activity) => activity switch
    {
        AgentActivity.Cultivating => "수행",
        AgentActivity.TrainingTechnique => "공법 수련",
        AgentActivity.TrainingSpell => "법술 연마",
        AgentActivity.Recovering => "회복",
        AgentActivity.SeekingChallenge => "도전 탐색",
        AgentActivity.Fighting => "전투 중",
        AgentActivity.Reincarnating => "윤회 중",
        AgentActivity.DoingCityMission => "도시 임무",
        AgentActivity.TradingInCity => "도시 거래",
        AgentActivity.CraftingTreasure => "법보 제작",
        AgentActivity.CraftingPill => "단약 제작",
        AgentActivity.Traveling => "지역 이동",
        AgentActivity.HuntingMonster => "요수 사냥",
        AgentActivity.MiningSpiritStones => "영석 광맥 채굴",
        _ => activity.ToString(),
    };

    public static string FormatMarketGood(MarketGoodSnapshot item) => item.Kind switch
    {
        MarketGoodKind.Material when item.Material is { } material =>
            EconomyRules.GetMaterial(material).KoreanName,
        MarketGoodKind.GrowthPill => $"{FormatRealmStage(item.Realm ?? RealmStage.QiRefining)} 수행단",
        MarketGoodKind.BreakthroughPill => $"{FormatRealmStage(item.Realm ?? RealmStage.QiRefining)} 돌파단",
        MarketGoodKind.DefenseTreasure => $"{FormatRealmStage(item.Realm ?? RealmStage.QiRefining)} 방어 법보",
        MarketGoodKind.AttackTreasure => $"{FormatRealmStage(item.Realm ?? RealmStage.QiRefining)} 공격 법보",
        MarketGoodKind.TechniqueBook => "공법서",
        MarketGoodKind.SpellBook => "법술서",
        _ => item.Kind.ToString(),
    };

    public static string FormatMarketGood(
        MarketGoodDefinition item,
        QualityValue? quality) => item.Kind switch
    {
        MarketGoodKind.GrowthPill => $"{FormatRealmStage(item.Realm ?? RealmStage.QiRefining)} 수행단",
        MarketGoodKind.BreakthroughPill => $"{FormatRealmStage(item.Realm ?? RealmStage.QiRefining)} 돌파단",
        MarketGoodKind.DefenseTreasure =>
            $"{FormatRealmStage(item.Realm ?? RealmStage.QiRefining)} 방어 법보" +
            (quality is { } defenseQuality ? $" · {QualityRules.Format(defenseQuality)}" : string.Empty),
        MarketGoodKind.AttackTreasure =>
            $"{FormatRealmStage(item.Realm ?? RealmStage.QiRefining)} 공격 법보" +
            (quality is { } attackQuality ? $" · {QualityRules.Format(attackQuality)}" : string.Empty),
        MarketGoodKind.TechniqueBook => "공법서" +
            (quality is { } techniqueQuality ? $" · {QualityRules.Format(techniqueQuality)}" : string.Empty),
        MarketGoodKind.SpellBook => "법술서" +
            (quality is { } spellQuality ? $" · {QualityRules.Format(spellQuality)}" : string.Empty),
        _ => item.Kind.ToString(),
    };

    public static string FormatBiome(BiomeType biome) => biome switch
    {
        BiomeType.City => "도시",
        BiomeType.Forest => "숲",
        BiomeType.Plains => "평야",
        BiomeType.Mountains => "산맥",
        _ => biome.ToString(),
    };

    public static string FormatMonsterList(MapLocationSnapshot location) =>
        location.Monsters.Count == 0
            ? "안전지대"
            : string.Join(
                " · ",
                location.Monsters.Select(monster =>
                    $"{monster.Species.KoreanName} {KoreanNumberFormatter.Format(monster.Population)}"));

    public static string FormatPriceChange(long basePrice, long currentPrice)
    {
        if (basePrice <= 0)
        {
            return "-";
        }

        var change = (currentPrice - basePrice) * 100m / basePrice;
        return change switch
        {
            > 0 => $"+{change:0.##}%",
            < 0 => $"{change:0.##}%",
            _ => "0%",
        };
    }
}
