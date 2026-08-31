using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed partial class World
{
    private AgentActivity SelectNonCombatActivity(int index, int roll)
    {
        if (roll < 45)
        {
            return AgentActivity.Cultivating;
        }
        if (roll < 65)
        {
            return AgentActivity.TrainingTechnique;
        }
        if (roll < 80)
        {
            return AgentActivity.TrainingSpell;
        }
        if (roll < 88)
        {
            if (_market.NeedsSpiritStoneMining)
            {
                return AgentActivity.MiningSpiritStones;
            }
            if (_market.TryGetRequestedMissionMaterial(out _))
            {
                return AgentActivity.DoingCityMission;
            }
        }
        if (roll < 96)
        {
            return AgentActivity.TradingInCity;
        }

        var realm = _agents.RealmStages[index];
        var materialCost = EconomyRules.GetProductionMaterialCost(
            MarketGoodKind.AttackTreasure,
            realm);
        var craftCost = EconomyRules.GetNatalCraftSpiritStoneCost(realm);
        if (_agents.GetMaterialCount(index, MaterialId.TreasureMaterial) >= materialCost
            && _agents.SpiritStones[index] >= craftCost)
        {
            return AgentActivity.CraftingTreasure;
        }

        return roll % 2 == 0
            ? AgentActivity.Cultivating
            : AgentActivity.TradingInCity;
    }

    private void ResolveCityMission(int index)
    {
        if (_agents.ActiveMissionMaterials[index] == byte.MaxValue)
        {
            if (!_market.TryGetRequestedMissionMaterial(out var requested))
            {
                return;
            }

            _agents.ActiveMissionMaterials[index] = (byte)requested;
        }

        var material = (MaterialId)_agents.ActiveMissionMaterials[index];
        _agents.AddMaterial(index, material, 1 + _random.NextInt(3));
        if (_agents.GetMaterialCount(index, material) < EconomyRules.MissionRequiredItems)
        {
            return;
        }

        if (!_market.TryAcceptMissionDelivery(
                material,
                EconomyRules.MissionRequiredItems,
                (int)_agents.RealmStages[index] * 10,
                out var reward))
        {
            _agents.ActiveMissionMaterials[index] = byte.MaxValue;
            return;
        }

        _agents.ConsumeMaterial(index, material, EconomyRules.MissionRequiredItems);
        _agents.SpiritStones[index] = checked(_agents.SpiritStones[index] + reward);
        _agents.MissionsCompleted[index]++;
        _totalMissionsCompleted++;
        _agents.ActiveMissionMaterials[index] = byte.MaxValue;
        AddEvent(WorldEventKind.MissionCompleted, index, reward);
    }

    private void ResolveSpiritStoneMining(int index)
    {
        if (!_market.TryCompleteMining(
                _agents.RealmStages[index],
                out var reward,
                out var extracted))
        {
            return;
        }

        _agents.SpiritStones[index] = checked(_agents.SpiritStones[index] + reward);
        _agents.MissionsCompleted[index]++;
        _totalMissionsCompleted++;
        AddEvent(WorldEventKind.SpiritStoneMined, index, extracted);
    }

    private void ResolveCityTrading(int index)
    {
        var choice = _random.NextInt(8);
        var traded = choice switch
        {
            0 => TryBuyPillFromMarket(index, PillKind.Growth),
            1 => TryBuyPillFromMarket(index, PillKind.Breakthrough),
            2 => TryBuyTreasureFromMarket(index, TreasureSlot.DefenseTreasure),
            3 => TryBuyTreasureFromMarket(index, TreasureSlot.AttackTreasure),
            4 => TryBuyBookFromMarket(index, technique: true),
            5 => TryBuyBookFromMarket(index, technique: false),
            6 => TryBuyMaterialFromMarket(index, MaterialCategory.Treasure),
            _ => TryBuyMaterialFromMarket(index, MaterialCategory.Pill),
        };

        if (!traded)
        {
            _ = TrySellPillToMarket(index) || TrySellMaterialToMarket(index);
        }
    }

    private bool TrySellMaterialToMarket(int index)
    {
        var protectedMaterial = _agents.ActiveMissionMaterials[index];
        for (var materialIndex = 0; materialIndex < EconomyRules.MaterialCount; materialIndex++)
        {
            var material = (MaterialId)materialIndex;
            var count = _agents.GetMaterialCount(index, material);
            var reserved = materialIndex == protectedMaterial ? EconomyRules.MissionRequiredItems : 0;
            if (count <= reserved || !_market.TrySell(materialIndex, 1, out var proceeds))
            {
                continue;
            }

            _agents.ConsumeMaterial(index, material, 1);
            _agents.SpiritStones[index] = checked(_agents.SpiritStones[index] + proceeds);
            _totalMaterialsSold++;
            AddEvent(WorldEventKind.MaterialSold, index, materialIndex);
            return true;
        }

        return false;
    }

    private bool TryBuyPillFromMarket(int index, PillKind kind)
    {
        if ((kind == PillKind.Growth && _agents.GrowthPills[index] >= 2)
            || (kind == PillKind.Breakthrough && _agents.BreakthroughPills[index] >= 1))
        {
            return false;
        }

        var goodId = EconomyRules.GetPillGoodId(kind, _agents.RealmStages[index]);
        if (!_market.TryBuy(goodId, 1, _agents.SpiritStones[index], out var cost))
        {
            return false;
        }

        _agents.SpiritStones[index] -= cost;
        if (kind == PillKind.Growth)
        {
            _agents.GrowthPills[index]++;
        }
        else
        {
            _agents.BreakthroughPills[index]++;
        }

        AddEvent(WorldEventKind.PillPurchased, index, (int)kind);
        return true;
    }

    private bool TryBuyMaterialFromMarket(int index, MaterialCategory category)
    {
        foreach (var definition in EconomyRules.AllMaterials)
        {
            if (definition.Category != category
                || !_market.TryBuy(
                    (int)definition.Id,
                    1,
                    _agents.SpiritStones[index],
                    out var cost))
            {
                continue;
            }

            _agents.SpiritStones[index] -= cost;
            _agents.AddMaterial(index, definition.Id, 1);
            AddEvent(WorldEventKind.MaterialPurchased, index, (int)definition.Id);
            return true;
        }

        return false;
    }

    private bool TryBuyTreasureFromMarket(int index, TreasureSlot slot)
    {
        var goodId = EconomyRules.GetTreasureGoodId(slot, _agents.RealmStages[index]);
        if (!_market.TryBuyQualityGood(
                goodId,
                _agents.SpiritStones[index],
                out var cost,
                out var quality))
        {
            return false;
        }

        _agents.SpiritStones[index] -= cost;
        var current = _agents.GetTreasureQuality(index, slot);
        if (current == EconomyRules.EmptyTreasureQuality || quality.Hundredths > current)
        {
            _agents.SetTreasureQuality(index, slot, quality.Hundredths);
            _agents.RecalculatePower(index);
        }
        else if (_market.TrySellQualityGood(goodId, quality, out var proceeds))
        {
            _agents.SpiritStones[index] = checked(_agents.SpiritStones[index] + proceeds);
        }

        AddEvent(WorldEventKind.TreasurePurchased, index, quality.Hundredths);
        return true;
    }

    private bool TryBuyBookFromMarket(int index, bool technique)
    {
        var raw = technique ? _agents.TechniqueQualities[index] : _agents.SpellQualities[index];
        var current = new QualityValue(raw);
        if (current.Tier == QualityTier.Flawless)
        {
            return false;
        }

        var goodId = technique ? EconomyRules.TechniqueBookGoodId : EconomyRules.SpellBookGoodId;
        if (!_market.TryBuyQualityGood(
                goodId,
                _agents.SpiritStones[index],
                out var cost,
                out var offered))
        {
            return false;
        }

        _agents.SpiritStones[index] -= cost;
        if (technique && offered > current)
        {
            _agents.TechniqueQualities[index] = offered.Hundredths;
            _agents.TechniqueMasteries[index] = 0;
            _agents.TechniqueTrainingExperience[index] = 0;
            _agents.TechniqueUnderstanding[index] = 0;
            AddChronicle(index, ChronicleEventKind.TechniqueAcquired, offered.Hundredths);
            AddEvent(WorldEventKind.TechniqueAcquired, index, offered.Hundredths);
        }
        else if (!technique && offered > current)
        {
            _agents.SpellQualities[index] = offered.Hundredths;
            _agents.SpellMasteries[index] = 0;
            _agents.SpellTrainingExperience[index] = 0;
            _agents.SpellUnderstanding[index] = 0;
            AddChronicle(index, ChronicleEventKind.SpellAcquired, offered.Hundredths);
            AddEvent(WorldEventKind.SpellAcquired, index, offered.Hundredths);
        }

        AddEvent(
            technique ? WorldEventKind.TechniqueBookPurchased : WorldEventKind.SpellBookPurchased,
            index,
            offered.Hundredths);
        return true;
    }

    private bool TrySellPillToMarket(int index)
    {
        var kind = _agents.GrowthPills[index] > 2
            ? PillKind.Growth
            : _agents.BreakthroughPills[index] > 1
                ? PillKind.Breakthrough
                : (PillKind?)null;
        if (kind is null)
        {
            return false;
        }

        var goodId = EconomyRules.GetPillGoodId(kind.Value, _agents.RealmStages[index]);
        if (!_market.TrySell(goodId, 1, out var proceeds))
        {
            return false;
        }

        if (kind == PillKind.Growth)
        {
            _agents.GrowthPills[index]--;
        }
        else
        {
            _agents.BreakthroughPills[index]--;
        }

        _agents.SpiritStones[index] = checked(_agents.SpiritStones[index] + proceeds);
        return true;
    }

    private void ResolveTreasureCrafting(int index)
    {
        var realm = _agents.RealmStages[index];
        var materialCost = EconomyRules.GetProductionMaterialCost(
            MarketGoodKind.AttackTreasure,
            realm);
        var spiritStoneCost = EconomyRules.GetNatalCraftSpiritStoneCost(realm);
        if (_agents.GetMaterialCount(index, MaterialId.TreasureMaterial) < materialCost
            || !_market.TryCollectCraftingFee(
                _agents.SpiritStones[index],
                spiritStoneCost))
        {
            return;
        }

        _agents.ConsumeMaterial(index, MaterialId.TreasureMaterial, materialCost);
        _agents.SpiritStones[index] -= spiritStoneCost;
        var baseQuality = (_agents.Comprehensions[index] + _agents.Souls[index]) / 2;
        var variance = _random.NextInt(1_001) - 500;
        var quality = checked((ushort)Math.Clamp(baseQuality + variance, 0, 10_000));
        var current = _agents.GetTreasureQuality(index, TreasureSlot.NatalTreasure);
        if (current == EconomyRules.EmptyTreasureQuality || quality > current)
        {
            _agents.SetTreasureQuality(index, TreasureSlot.NatalTreasure, quality);
            _agents.RecalculatePower(index);
        }

        _totalTreasuresCrafted++;
        AddEvent(WorldEventKind.TreasureCrafted, index, quality);
    }

    // schema 13 저장에서 이 행동이 남아 있을 수 있다. 새 규칙에서는 수선자가 단약을
    // 직접 만들지 않고 도시 시장에서 현재 대경지용 단약을 구매한다.
    private void ResolvePillCrafting(int index) => ResolveCityTrading(index);

    private ActiveMissionSnapshot? GetActiveMissionSnapshot(int index)
    {
        var raw = _agents.ActiveMissionMaterials[index];
        if (raw == byte.MaxValue)
        {
            return null;
        }

        var material = (MaterialId)raw;
        return new ActiveMissionSnapshot(
            material,
            EconomyRules.MissionRequiredItems,
            _agents.GetMaterialCount(index, material));
    }

    private MaterialHoldingSnapshot[] GetMaterialHoldingSnapshots(int index)
    {
        var holdings = new List<MaterialHoldingSnapshot>(EconomyRules.MaterialCount);
        foreach (var definition in EconomyRules.AllMaterials)
        {
            var count = _agents.GetMaterialCount(index, definition.Id);
            if (count > 0)
            {
                holdings.Add(new MaterialHoldingSnapshot(definition.Id, count));
            }
        }

        return holdings.ToArray();
    }

    private EquippedTreasureSnapshot[] GetEquippedTreasureSnapshots(int index)
    {
        var equipment = new EquippedTreasureSnapshot[EconomyRules.TreasureSlotCount];
        for (var slotIndex = 0; slotIndex < EconomyRules.TreasureSlotCount; slotIndex++)
        {
            var slot = (TreasureSlot)slotIndex;
            var quality = _agents.GetTreasureQuality(index, slot);
            equipment[slotIndex] = new EquippedTreasureSnapshot(
                slot,
                quality == EconomyRules.EmptyTreasureQuality ? null : new QualityValue(quality),
                quality == EconomyRules.EmptyTreasureQuality ? null : _agents.RealmStages[index]);
        }

        return equipment;
    }
}
