using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed partial class World
{
    private AgentActivity SelectNonCombatActivity(int index, int roll)
    {
        var economyStart = _balance.DeveloperModeEnabled
            ? Math.Clamp(
                100 - (20 * _balance.EconomyActivityPercent / 100),
                66,
                95)
            : 80;
        var economyWidth = 100 - economyStart;
        var missionEnd = economyStart + (economyWidth * 2 / 5);
        var tradeEnd = economyStart + (economyWidth * 4 / 5);
        if (roll < 45)
        {
            return AgentActivity.Cultivating;
        }
        if (roll < 65)
        {
            return AgentActivity.TrainingTechnique;
        }
        if (roll < economyStart)
        {
            return AgentActivity.TrainingSpell;
        }
        if (roll < missionEnd)
        {
            var needsMining = _market.NeedsSpiritStoneMining;
            var needsMaterials = _market.TryGetRequestedMissionMaterial(out _);
            if (needsMining && needsMaterials)
            {
                return ((index + _agents.ActionsTaken[index]) & 1) == 0
                    ? AgentActivity.MiningSpiritStones
                    : AgentActivity.DoingCityMission;
            }
            if (needsMining)
            {
                return AgentActivity.MiningSpiritStones;
            }
            if (needsMaterials)
            {
                return AgentActivity.DoingCityMission;
            }
        }
        if (roll < tradeEnd)
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

    private bool ResolveCityMission(int index)
    {
        var completedBefore = _agents.MissionsCompleted[index];
        for (var month = 0; month < 6; month++)
        {
            ResolveCityMissionMonth(index);
            if (_agents.MissionsCompleted[index] != completedBefore)
            {
                break;
            }
        }

        return _agents.MissionsCompleted[index] != completedBefore
            && SchedulePostMissionTrading(index);
    }

    private void ResolveCityMissionMonth(int index)
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
        // PC_TEMP_REVIEW: 6개월 임무 행동은 계약량과 최소 1개의 자유 처분분을
        // 함께 채집한다. 임무 납품 외 수선자 시장 판매 경로가 실제로 유지된다.
        var gathered = EconomyRules.MissionRequiredItems + 1 + _random.NextInt(2);
        if (_agents.GetMaterialCount(index, material) > int.MaxValue - gathered)
        {
            return;
        }

        _agents.AddMaterial(index, material, gathered);
        if (_agents.GetMaterialCount(index, material) < EconomyRules.MissionRequiredItems)
        {
            return;
        }
        if (_agents.MissionsCompleted[index] == int.MaxValue
            || _totalMissionsCompleted == int.MaxValue)
        {
            return;
        }

        var settlement = _market.SettleMissionDelivery(
                index,
                CreateMarketSourceResultId(11),
                Tick.Value,
                material,
                EconomyRules.MissionRequiredItems,
                (int)_agents.RealmStages[index] * 10,
                _agents.SpiritStones[index]);
        if (!settlement.IsSettled)
        {
            return;
        }

        _agents.ConsumeMaterial(index, material, EconomyRules.MissionRequiredItems);
        _agents.SpiritStones[index] = checked(
            _agents.SpiritStones[index] + settlement.TotalSpiritStones);
        _agents.MissionsCompleted[index]++;
        _totalMissionsCompleted++;
        _agents.ActiveMissionMaterials[index] = byte.MaxValue;
        AddEvent(WorldEventKind.MissionCompleted, index, settlement.TotalSpiritStones);
        // 계약 요구량을 넘겨 채집한 실물은 같은 도시 방문에서 시장에 별도 판매한다.
        // 임무 납품과 일반 판매는 source result를 분리해 각각 원자 정산한다.
        _ = TrySellMaterialToMarket(index, sourcePhase: 12);
    }

    private bool ResolveSpiritStoneMining(int index)
    {
        var completedBefore = _agents.MissionsCompleted[index];
        for (var month = 0; month < 6; month++)
        {
            ResolveSpiritStoneMiningMonth(index);
        }

        return _agents.MissionsCompleted[index] != completedBefore
            && SchedulePostMissionTrading(index);
    }

    private bool SchedulePostMissionTrading(int index)
    {
        SetActivityDecision(
            index,
            AgentActivity.TradingInCity,
            AgentGoalKind.AcquireResources,
            DecisionReasonCode.GrowthBalance,
            DecisionResolutionKind.Selected,
            AgentActivity.Cultivating);
        ScheduleAgent(index, GameCalendar.GetNextMonthStart(Tick).Value);
        return true;
    }

    private void ResolveSpiritStoneMiningMonth(int index)
    {
        if (_agents.MissionsCompleted[index] == int.MaxValue
            || _totalMissionsCompleted == int.MaxValue)
        {
            return;
        }

        var locationId = _agents.LocationIds[index];
        var availableVeinSupply = _atlas.GetRemainingSpiritStoneSupply(locationId);
        if (!_market.TryCompleteMining(
                _agents.RealmStages[index],
                _agents.SpiritStones[index],
                out var reward,
                out var extracted,
                availableVeinSupply))
        {
            return;
        }

        if (!_atlas.TryExtractSpiritStones(locationId, extracted, out var atlasExtracted)
            || atlasExtracted != extracted)
        {
            throw new InvalidOperationException("영맥 공급과 도시 채굴 정산이 일치하지 않습니다.");
        }

        _agents.SpiritStones[index] = checked(_agents.SpiritStones[index] + reward);
        _agents.MissionsCompleted[index]++;
        _totalMissionsCompleted++;
        AddEvent(WorldEventKind.SpiritStoneMined, index, extracted);
    }

    private void ResolveCityTrading(int index)
    {
        // 임무 예약분을 제외한 잉여 실물은 먼저 시장에 공급한다. 구매만 반복되어
        // 수선자 간 공급 경로가 멈추는 것을 방지한다.
        if (TrySellMaterialToMarket(index))
        {
            return;
        }

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

    private bool TrySellMaterialToMarket(int index, int sourcePhase = 10)
    {
        if (_totalMaterialsSold == int.MaxValue)
        {
            return false;
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

            var settlement = _market.SettleSale(
                index,
                CreateMarketSourceResultId(sourcePhase),
                Tick.Value,
                materialIndex,
                1,
                _agents.SpiritStones[index]);
            if (!settlement.IsSettled)
            {
                continue;
            }

            _agents.ConsumeMaterial(index, material, 1);
            _agents.SpiritStones[index] = checked(
                _agents.SpiritStones[index] + settlement.TotalSpiritStones);
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
        var settlement = _market.SettlePurchase(
            index,
            CreateMarketSourceResultId(kind == PillKind.Growth ? 1 : 2),
            Tick.Value,
            goodId,
            1,
            _agents.SpiritStones[index]);
        if (!settlement.IsSettled)
        {
            return false;
        }

        _agents.SpiritStones[index] -= settlement.TotalSpiritStones;
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
            if (definition.Category != category)
            {
                continue;
            }
            if (_agents.GetMaterialCount(index, definition.Id) == int.MaxValue)
            {
                continue;
            }

            var settlement = _market.SettlePurchase(
                index,
                CreateMarketSourceResultId(
                    category == MaterialCategory.Treasure ? 7 : 8),
                Tick.Value,
                (int)definition.Id,
                1,
                _agents.SpiritStones[index]);
            if (!settlement.IsSettled)
            {
                continue;
            }

            _agents.SpiritStones[index] -= settlement.TotalSpiritStones;
            _agents.AddMaterial(index, definition.Id, 1);
            AddEvent(WorldEventKind.MaterialPurchased, index, (int)definition.Id);
            return true;
        }

        return false;
    }

    private bool TryBuyTreasureFromMarket(int index, TreasureSlot slot)
    {
        var goodId = EconomyRules.GetTreasureGoodId(slot, _agents.RealmStages[index]);
        var current = _agents.GetTreasureQuality(index, slot);
        var settlement = _market.SettlePurchase(
            index,
            CreateMarketSourceResultId(slot == TreasureSlot.DefenseTreasure ? 3 : 4),
            Tick.Value,
            goodId,
            1,
            _agents.SpiritStones[index],
            current == EconomyRules.EmptyTreasureQuality
                ? null
                : new QualityValue(current));
        if (!settlement.IsSettled || settlement.Quality is not { } quality)
        {
            return false;
        }

        _agents.SpiritStones[index] -= settlement.TotalSpiritStones;
        _agents.SetTreasureQuality(index, slot, quality.Hundredths);
        RecalculatePowerAndUpdatePeak(index);

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
        var settlement = _market.SettlePurchase(
            index,
            CreateMarketSourceResultId(technique ? 5 : 6),
            Tick.Value,
            goodId,
            1,
            _agents.SpiritStones[index],
            current);
        if (!settlement.IsSettled || settlement.Quality is not { } offered)
        {
            return false;
        }

        _agents.SpiritStones[index] -= settlement.TotalSpiritStones;
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
        var settlement = _market.SettleSale(
            index,
            CreateMarketSourceResultId(9),
            Tick.Value,
            goodId,
            1,
            _agents.SpiritStones[index]);
        if (!settlement.IsSettled)
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

        _agents.SpiritStones[index] = checked(
            _agents.SpiritStones[index] + settlement.TotalSpiritStones);
        return true;
    }

    private long CreateMarketSourceResultId(int phase)
    {
        if (phase is <= 0 or >= 16)
        {
            throw new ArgumentOutOfRangeException(nameof(phase));
        }

        return checked((Tick.Value * 16L) + phase);
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
        var baseQuality = (
            _agents.GetDerivedAptitude(index, AptitudeDerivedAbility.ComprehensionLearning)
            + _agents.GetDerivedAptitude(index, AptitudeDerivedAbility.SoulGrowth)) / 2;
        var variance = _random.NextInt(1_001) - 500;
        var quality = checked((ushort)Math.Clamp(baseQuality + variance, 0, 10_000));
        var current = _agents.GetTreasureQuality(index, TreasureSlot.NatalTreasure);
        if (current == EconomyRules.EmptyTreasureQuality || quality > current)
        {
            _agents.SetTreasureQuality(index, TreasureSlot.NatalTreasure, quality);
            RecalculatePowerAndUpdatePeak(index);
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
