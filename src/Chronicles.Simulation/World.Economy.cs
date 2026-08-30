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
        var definition = EconomyRules.GetMaterial(material);
        var reward = checked((definition.ShopSellPrice * 5) + ((int)_agents.RealmStages[index] * 10));
        _agents.SpiritStones[index] = checked(_agents.SpiritStones[index] + reward);
        _agents.MissionsCompleted[index]++;
        _totalMissionsCompleted++;
        _agents.ActiveMissionMaterials[index] = byte.MaxValue;
        AddEvent(WorldEventKind.MissionCompleted, index, reward);
    }

    private void ResolveCityTrading(int index)
    {
        var cultivationRequirement = GetCultivationRequirement(index);
        var wantsBreakthroughPill = _agents.CultivationProgress[index] * 4 >= cultivationRequirement * 3;
        if (wantsBreakthroughPill
            && _agents.BreakthroughPills[index] == 0
            && TryBuyPill(index, PillKind.Breakthrough))
        {
            return;
        }

        if (_agents.GrowthPills[index] < 2 && TryBuyPill(index, PillKind.Growth))
        {
            return;
        }

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

            _agents.ConsumeMaterial(index, material, 1);
            var price = EconomyRules.GetMaterial(material).ShopSellPrice;
            _agents.SpiritStones[index] = checked(_agents.SpiritStones[index] + price);
            _totalMaterialsSold++;
            AddEvent(WorldEventKind.MaterialSold, index, materialIndex);
            return;
        }
    }

    private bool TryBuyPill(int index, PillKind kind)
    {
        var price = EconomyRules.GetPillPrice(kind);
        if (_agents.SpiritStones[index] < price)
        {
            return false;
        }

        _agents.SpiritStones[index] -= price;
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
