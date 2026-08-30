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

        if (roll < 90)
        {
            return AgentActivity.DoingCityMission;
        }

        if (roll < 94)
        {
            return AgentActivity.TradingInCity;
        }

        if (roll < 97 && HasMaterials(index, MaterialCategory.Treasure, 2))
        {
            return AgentActivity.CraftingTreasure;
        }

        if (HasMaterials(index, MaterialCategory.Pill, 2))
        {
            return AgentActivity.CraftingPill;
        }

        return AgentActivity.DoingCityMission;
    }

    private void ResolveCityMission(int index)
    {
        if (_agents.ActiveMissionMaterials[index] == byte.MaxValue)
        {
            _agents.ActiveMissionMaterials[index] = checked((byte)_random.NextInt(EconomyRules.MaterialCount));
        }

        var material = (MaterialId)_agents.ActiveMissionMaterials[index];
        _agents.AddMaterial(index, material, 1 + _random.NextInt(3));
        if (_agents.GetMaterialCount(index, material) < EconomyRules.MissionRequiredItems)
        {
            return;
        }

        _agents.ConsumeMaterial(index, material, EconomyRules.MissionRequiredItems);
        var reward = _market.AcceptMissionDelivery(
            material,
            EconomyRules.MissionRequiredItems,
            (int)_agents.RealmStages[index] * 10);
        _agents.SpiritStones[index] = checked(_agents.SpiritStones[index] + reward);
        _agents.MissionsCompleted[index]++;
        _totalMissionsCompleted++;
        _agents.ActiveMissionMaterials[index] = byte.MaxValue;
        AddEvent(WorldEventKind.MissionCompleted, index, reward);
        ProcessCityIndustry();
    }

    private void ResolveCityTrading(int index)
    {
        var choice = _random.NextInt(7);
        var traded = choice switch
        {
            0 => TryBuyPillFromMarket(index, PillKind.Growth),
            1 => TryBuyPillFromMarket(index, PillKind.Breakthrough),
            2 => TryBuyTreasureFromMarket(index),
            3 => TryBuyBookFromMarket(index, technique: true),
            4 => TryBuyBookFromMarket(index, technique: false),
            5 => TryBuyMaterialFromMarket(index, MaterialCategory.Treasure),
            _ => TryBuyMaterialFromMarket(index, MaterialCategory.Pill),
        };

        if (!traded)
        {
            traded = TrySellPillToMarket(index) || TrySellMaterialToMarket(index);
        }

        if (!traded)
        {
            _ = TryBuyMaterialFromMarket(index, MaterialCategory.Miscellaneous);
        }

        ProcessCityIndustry();
    }

    private bool TrySellMaterialToMarket(int index)
    {
        var protectedMaterial = _agents.ActiveMissionMaterials[index];
        for (var materialIndex = 0; materialIndex < EconomyRules.MaterialCount; materialIndex++)
        {
            var material = (MaterialId)materialIndex;
            var count = _agents.GetMaterialCount(index, material);
            var reserved = materialIndex == protectedMaterial ? EconomyRules.MissionRequiredItems : 0;
            if (count <= reserved)
            {
                continue;
            }

            if (!_market.TrySell(materialIndex, 1, out var proceeds))
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
            || (kind == PillKind.Breakthrough && _agents.BreakthroughPills[index] >= 1)
            || !_market.TryBuy(
                EconomyRules.GetPillGoodId(kind),
                1,
                _agents.SpiritStones[index],
                out var cost))
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
        var start = _random.NextInt(EconomyRules.MaterialCount);
        for (var offset = 0; offset < EconomyRules.MaterialCount; offset++)
        {
            var material = (MaterialId)((start + offset) % EconomyRules.MaterialCount);
            if (EconomyRules.GetMaterial(material).Category != category
                || !_market.TryBuy(
                    (int)material,
                    1,
                    _agents.SpiritStones[index],
                    out var cost))
            {
                continue;
            }

            _agents.SpiritStones[index] -= cost;
            _agents.AddMaterial(index, material, 1);
            AddEvent(WorldEventKind.MaterialPurchased, index, (int)material);
            return true;
        }

        return false;
    }

    private bool TryBuyTreasureFromMarket(int index)
    {
        if (!_market.TryBuyTreasure(
            _agents.SpiritStones[index],
            out var cost,
            out var quality))
        {
            return false;
        }

        _agents.SpiritStones[index] -= cost;
        var slot = FindWeakestTreasureSlot(index);
        var current = _agents.GetTreasureQuality(index, slot);
        if (current == EconomyRules.EmptyTreasureQuality || quality.Hundredths > current)
        {
            _agents.SetTreasureQuality(index, slot, quality.Hundredths);
            _agents.RecalculatePower(index);
        }
        else if (_market.TrySellTreasure(quality, out var proceeds))
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
        if (!_market.TryBuy(goodId, 1, _agents.SpiritStones[index], out var cost))
        {
            return false;
        }

        _agents.SpiritStones[index] -= cost;
        var improved = QualityRules.GetNextTierMinimum(current);
        if (technique)
        {
            _agents.TechniqueQualities[index] = improved.Hundredths;
            _agents.TechniqueMasteries[index] = 200;
            AddChronicle(index, ChronicleEventKind.TechniqueEnlightenment, improved.Hundredths);
            AddEvent(WorldEventKind.TechniqueBookPurchased, index, improved.Hundredths);
        }
        else
        {
            _agents.SpellQualities[index] = improved.Hundredths;
            _agents.SpellMasteries[index] = 200;
            AddChronicle(index, ChronicleEventKind.SpellEnlightenment, improved.Hundredths);
            AddEvent(WorldEventKind.SpellBookPurchased, index, improved.Hundredths);
        }

        _agents.RecalculatePower(index);
        return true;
    }

    private bool TrySellPillToMarket(int index)
    {
        var kind = _agents.GrowthPills[index] > 2
            ? PillKind.Growth
            : _agents.BreakthroughPills[index] > 1
                ? PillKind.Breakthrough
                : (PillKind?)null;
        if (kind is null
            || !_market.TrySell(EconomyRules.GetPillGoodId(kind.Value), 1, out var proceeds))
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
        if (!TryConsumeMaterials(index, MaterialCategory.Treasure, 2))
        {
            return;
        }

        var baseQuality = (_agents.Comprehensions[index] + _agents.Souls[index]) / 2;
        var realmBonus = (int)_agents.RealmStages[index] * 100;
        var variance = _random.NextInt(1_001) - 500;
        var quality = checked((ushort)Math.Clamp(baseQuality + realmBonus + variance, 0, 10_000));
        var slot = FindWeakestTreasureSlot(index);
        var current = _agents.GetTreasureQuality(index, slot);
        if (current == EconomyRules.EmptyTreasureQuality || quality > current)
        {
            _agents.SetTreasureQuality(index, slot, quality);
            _agents.RecalculatePower(index);
        }
        else if (_market.TrySellTreasure(new QualityValue(quality), out var proceeds))
        {
            _agents.SpiritStones[index] = checked(_agents.SpiritStones[index] + proceeds);
        }

        _totalTreasuresCrafted++;
        AddEvent(WorldEventKind.TreasureCrafted, index, quality);
    }

    private void ResolvePillCrafting(int index)
    {
        if (!TryConsumeMaterials(index, MaterialCategory.Pill, 2))
        {
            return;
        }

        var kind = _agents.CultivationProgress[index] * 4 >= GetCultivationRequirement(index) * 3
            ? PillKind.Breakthrough
            : PillKind.Growth;
        if (kind == PillKind.Growth)
        {
            _agents.GrowthPills[index]++;
        }
        else
        {
            _agents.BreakthroughPills[index]++;
        }

        _totalPillsCrafted++;
        AddEvent(WorldEventKind.PillCrafted, index, (int)kind);
    }

    private void ProcessCityIndustry()
    {
        if (TryConsumeCityMaterials(MaterialCategory.Treasure, 2))
        {
            _market.AddTreasureProduction(QualityValue.FromWhole(50));
            _totalTreasuresCrafted++;
        }

        if (TryConsumeCityMaterials(MaterialCategory.Pill, 2))
        {
            var pillGood = _market.GetStock(EconomyRules.GrowthPillGoodId)
                <= _market.GetStock(EconomyRules.BreakthroughPillGoodId)
                ? EconomyRules.GrowthPillGoodId
                : EconomyRules.BreakthroughPillGoodId;
            _market.AddCityProduction(pillGood, 1);
            _totalPillsCrafted++;
        }

        if (_market.GetStock(EconomyRules.TechniqueBookGoodId) < 4
            && _market.TrySpendTreasury(EconomyRules.TechniqueBookProductionCost))
        {
            _market.AddCityProduction(EconomyRules.TechniqueBookGoodId, 1);
        }

        if (_market.GetStock(EconomyRules.SpellBookGoodId) < 4
            && _market.TrySpendTreasury(EconomyRules.SpellBookProductionCost))
        {
            _market.AddCityProduction(EconomyRules.SpellBookGoodId, 1);
        }
    }

    private bool TryConsumeCityMaterials(MaterialCategory category, int required)
    {
        var available = EconomyRules.AllMaterials
            .Where(item => item.Category == category)
            .Sum(item => _market.GetStock((int)item.Id));
        if (available < required)
        {
            return false;
        }

        var remaining = required;
        foreach (var definition in EconomyRules.AllMaterials)
        {
            if (definition.Category != category)
            {
                continue;
            }

            var count = Math.Min(remaining, _market.GetStock((int)definition.Id));
            if (count > 0)
            {
                _ = _market.TryConsumeForProduction((int)definition.Id, count);
                remaining -= count;
            }

            if (remaining == 0)
            {
                return true;
            }
        }

        throw new InvalidOperationException("도시 생산 재료 검사와 소비가 일치하지 않습니다.");
    }

    private bool HasMaterials(int index, MaterialCategory category, int required)
    {
        var total = 0;
        foreach (var definition in EconomyRules.AllMaterials)
        {
            if (definition.Category != category)
            {
                continue;
            }

            total += _agents.GetMaterialCount(index, definition.Id);
            if (total >= required)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryConsumeMaterials(int index, MaterialCategory category, int required)
    {
        if (!HasMaterials(index, category, required))
        {
            return false;
        }

        var remaining = required;
        foreach (var definition in EconomyRules.AllMaterials)
        {
            if (definition.Category != category)
            {
                continue;
            }

            var count = Math.Min(remaining, _agents.GetMaterialCount(index, definition.Id));
            if (count > 0)
            {
                _agents.ConsumeMaterial(index, definition.Id, count);
                remaining -= count;
            }

            if (remaining == 0)
            {
                return true;
            }
        }

        throw new InvalidOperationException("재료 수량 검사와 소비 결과가 일치하지 않습니다.");
    }

    private TreasureSlot FindWeakestTreasureSlot(int index)
    {
        var weakestSlot = TreasureSlot.NatalTreasure;
        var weakestQuality = int.MaxValue;
        for (var slotIndex = 0; slotIndex < EconomyRules.TreasureSlotCount; slotIndex++)
        {
            var slot = (TreasureSlot)slotIndex;
            var quality = _agents.GetTreasureQuality(index, slot);
            if (quality == EconomyRules.EmptyTreasureQuality)
            {
                return slot;
            }

            if (quality < weakestQuality)
            {
                weakestQuality = quality;
                weakestSlot = slot;
            }
        }

        return weakestSlot;
    }

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
        var holdings = new List<MaterialHoldingSnapshot>();
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
                quality == EconomyRules.EmptyTreasureQuality ? null : new QualityValue(quality));
        }

        return equipment;
    }
}
