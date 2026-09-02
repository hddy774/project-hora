using Chronicles.Core;

namespace Chronicles.Simulation;

/// <summary>
/// 도시의 수요 기반 생산, 가격·수급 창과 파생 수요 cache 전이를 소유한다.
/// 거래 계약·snapshot·복원 작업집합과 월별 산업 hot path를 분리한다.
/// </summary>
internal sealed partial class CityMarket
{
    private void AdvanceProductionOneMonth()
    {
        for (var index = _productionQueue.Count - 1; index >= 0; index--)
        {
            var order = _productionQueue[index];
            order.CompletedMonths++;
            if (order.CompletedMonths < order.RequiredMonths)
            {
                continue;
            }

            CompleteProduction(order);
            _productionQueue.RemoveAt(index);
        }
    }

    private void FillProductionQueue()
    {
        while (_productionQueue.Count < EconomyRules.MaximumConcurrentProduction)
        {
            var goodId = SelectProductionGood();
            if (goodId < 0 || !TryStartProduction(goodId))
            {
                return;
            }
        }
    }

    private int SelectProductionGood()
    {
        var selected = -1;
        var selectedScore = 0L;
        for (var goodId = EconomyRules.RealmGoodStartId; goodId < EconomyRules.MarketGoodCount; goodId++)
        {
            var definition = EconomyRules.GetMarketGood(goodId);
            var queued = 0;
            for (var index = 0; index < _productionQueue.Count; index++)
            {
                if (_productionQueue[index].GoodId == goodId)
                {
                    queued++;
                }
            }

            var unmet = GetUnmetDemand(goodId) - queued;
            var projectedStock = _stock[goodId] + queued;
            if (unmet <= 0
                || (projectedStock >= EconomyRules.CityProductionTargetStock
                    && projectedStock >= unmet))
            {
                continue;
            }

            if (!CanStartProduction(definition))
            {
                continue;
            }

            var score = checked((unmet * 100L) + Math.Max(0, 10 - (int)(definition.Realm ?? 0)));
            if (score > selectedScore)
            {
                selected = goodId;
                selectedScore = score;
            }
        }

        return selected;
    }

    private bool TryStartProduction(int goodId)
    {
        var definition = EconomyRules.GetMarketGood(goodId);
        if (!CanStartProduction(definition))
        {
            return false;
        }

        var materialCost = EconomyRules.GetProductionMaterialCost(definition.Kind, definition.Realm);
        var spiritStoneCost = EconomyRules.GetProductionSpiritStoneCost(definition);
        MaterialId? material = null;
        if (TryGetRequiredMaterial(definition.Kind, out var requiredMaterial))
        {
            material = requiredMaterial;
        }

        if (material is { } consumed)
        {
            _stock[(int)consumed] -= materialCost;
            RegisterDemand((int)consumed, materialCost);
            _materialsProcessed = SaturatingAdd(_materialsProcessed, materialCost);
            Reprice((int)consumed);
        }

        TreasurySpiritStones -= spiritStoneCost;
        var quality = GetProductionQuality(definition);
        _productionQueue.Add(new CityProductionOrder(
            goodId,
            quality?.Hundredths ?? NoQuality,
            0,
            EconomyRules.CityProductionMonths,
            materialCost,
            spiritStoneCost));
        TouchAuthority();
        return true;
    }

    private bool CanStartProduction(MarketGoodDefinition definition)
    {
        var materialCost = EconomyRules.GetProductionMaterialCost(definition.Kind, definition.Realm);
        if (TryGetRequiredMaterial(definition.Kind, out var material)
            && _stock[(int)material] < materialCost)
        {
            return false;
        }

        return TreasurySpiritStones >= EconomyRules.GetProductionSpiritStoneCost(definition);
    }

    private void CompleteProduction(CityProductionOrder order)
    {
        var definition = EconomyRules.GetMarketGood(order.GoodId);
        switch (definition.Kind)
        {
            case MarketGoodKind.GrowthPill:
            case MarketGoodKind.BreakthroughPill:
                _pillsProduced = SaturatingAdd(_pillsProduced, 1);
                break;
            case MarketGoodKind.DefenseTreasure:
            case MarketGoodKind.AttackTreasure:
                _treasuresProduced = SaturatingAdd(_treasuresProduced, 1);
                break;
            case MarketGoodKind.TechniqueBook:
                _techniqueBooksProduced = SaturatingAdd(_techniqueBooksProduced, 1);
                break;
            case MarketGoodKind.SpellBook:
                _spellBooksProduced = SaturatingAdd(_spellBooksProduced, 1);
                break;
        }

        if (order.QualityHundredths != NoQuality)
        {
            _qualityHundredths[order.GoodId] = checked(
                _qualityHundredths[order.GoodId] + order.QualityHundredths);
        }

        AddSupply(order.GoodId, 1);
    }

    private MarketProductionState GetProductionState(
        int goodId,
        MarketGoodDefinition definition)
    {
        if (definition.Kind == MarketGoodKind.Material)
        {
            return MarketProductionState.NotProduced;
        }
        if (_productionQueue.Any(order => order.GoodId == goodId))
        {
            return MarketProductionState.Queued;
        }
        if (GetUnmetDemand(goodId) <= 0)
        {
            return MarketProductionState.NoDemand;
        }
        if (_stock[goodId] >= EconomyRules.CityProductionTargetStock
            && _stock[goodId] >= GetUnmetDemand(goodId))
        {
            return MarketProductionState.TargetStockReached;
        }

        var materialCost = EconomyRules.GetProductionMaterialCost(definition.Kind, definition.Realm);
        if (TryGetRequiredMaterial(definition.Kind, out var material)
            && _stock[(int)material] < materialCost)
        {
            return MarketProductionState.BlockedByMaterial;
        }
        if (TreasurySpiritStones < EconomyRules.GetProductionSpiritStoneCost(definition))
        {
            return MarketProductionState.BlockedByTreasury;
        }
        if (_productionQueue.Count >= EconomyRules.MaximumConcurrentProduction)
        {
            return MarketProductionState.BlockedByCapacity;
        }

        return MarketProductionState.Ready;
    }

    private QualityValue? GetAverageQuality(int goodId)
    {
        if (_stock[goodId] <= 0 || !IsQualityGood(EconomyRules.GetMarketGood(goodId).Kind))
        {
            return null;
        }

        return new QualityValue(checked((ushort)Math.Clamp(
            _qualityHundredths[goodId] / _stock[goodId],
            0L,
            QualityValue.MaximumHundredths)));
    }

    private static QualityValue? GetProductionQuality(MarketGoodDefinition definition) => definition.Kind switch
    {
        MarketGoodKind.DefenseTreasure or MarketGoodKind.AttackTreasure =>
            new QualityValue(checked((ushort)Math.Min(
                EconomyRules.InitialSkillMaximumHundredths,
                5_000 + ((int)(definition.Realm ?? 0) * 300)))),
        MarketGoodKind.TechniqueBook or MarketGoodKind.SpellBook =>
            new QualityValue(EconomyRules.InitialSkillMaximumHundredths),
        _ => null,
    };

    private long GetMaterialMissionScore(MaterialId material)
    {
        EnsureDerivedDemandCache();
        return _materialMissionScoreCache[(int)material];
    }

    private void EnsureDerivedDemandCache()
    {
        if (_derivedCacheRevision == _authorityRevision)
        {
            return;
        }

        for (var materialId = 0; materialId < EconomyRules.MaterialCount; materialId++)
        {
            _materialMissionScoreCache[materialId] = Math.Max(
                0L,
                _recentDemand[materialId] - _recentSupply[materialId]);
        }

        _productionBlockedByTreasuryCache = false;
        for (var goodId = EconomyRules.RealmGoodStartId; goodId < EconomyRules.MarketGoodCount; goodId++)
        {
            var definition = EconomyRules.GetMarketGood(goodId);
            var unmet = GetUnmetDemand(goodId);
            if (unmet <= 0)
            {
                continue;
            }

            if (TryGetRequiredMaterial(definition.Kind, out var required))
            {
                var materialId = (int)required;
                _materialMissionScoreCache[materialId] = SaturatingAdd(
                    _materialMissionScoreCache[materialId],
                    checked(unmet * EconomyRules.GetProductionMaterialCost(
                    definition.Kind,
                    definition.Realm)));
            }
            if (TreasurySpiritStones < EconomyRules.GetProductionSpiritStoneCost(definition))
            {
                _productionBlockedByTreasuryCache = true;
            }
        }

        for (var materialId = 0; materialId < EconomyRules.MaterialCount; materialId++)
        {
            _materialMissionScoreCache[materialId] = Math.Max(
                0L,
                _materialMissionScoreCache[materialId] - _stock[materialId]);
        }

        _derivedCacheRevision = _authorityRevision;
    }

    private bool IsProductionBlockedByTreasury()
    {
        EnsureDerivedDemandCache();
        return _productionBlockedByTreasuryCache;
    }

    private void RegisterDemand(int goodId, int count)
    {
        _demand[goodId] = SaturatingAdd(_demand[goodId], count);
        AddCurrentFlow(_recentDemand, _demandWindowBuckets, goodId, count);
        Reprice(goodId);
        TouchAuthority();
    }

    private void AddSupply(int goodId, int count)
    {
        _stock[goodId] = checked(_stock[goodId] + count);
        _supply[goodId] = SaturatingAdd(_supply[goodId], count);
        AddCurrentFlow(_recentSupply, _supplyWindowBuckets, goodId, count);
        Reprice(goodId);
        RefreshOfferForGood(goodId, _lastIndustryMonth);
        TouchAuthority();
    }

    private void Reprice(int goodId)
    {
        _prices[goodId] = EconomyRules.GetMarketPrice(
            EconomyRules.GetMarketGood(goodId),
            _stock[goodId],
            _supply[goodId],
            _demand[goodId],
            _recentSupply[goodId],
            _recentDemand[goodId]);
    }

    private void AddCurrentFlow(
        long[] recent,
        long[] buckets,
        int goodId,
        int count)
    {
        recent[goodId] = SaturatingAdd(recent[goodId], count);
        var bucketIndex = (_currentFlowBucket * EconomyRules.MarketGoodCount) + goodId;
        buckets[bucketIndex] = SaturatingAdd(buckets[bucketIndex], count);
    }

    private void AdvanceFlowWindowOneMonth()
    {
        _currentFlowBucket = (_currentFlowBucket + 1) % EconomyRules.MarketFlowWindowMonths;
        var start = _currentFlowBucket * EconomyRules.MarketGoodCount;
        var changed = false;
        for (var goodId = 0; goodId < EconomyRules.MarketGoodCount; goodId++)
        {
            var bucketIndex = start + goodId;
            var expiredSupply = _supplyWindowBuckets[bucketIndex];
            var expiredDemand = _demandWindowBuckets[bucketIndex];
            if (expiredSupply == 0 && expiredDemand == 0)
            {
                continue;
            }

            _recentSupply[goodId] -= expiredSupply;
            _recentDemand[goodId] -= expiredDemand;
            _supplyWindowBuckets[bucketIndex] = 0;
            _demandWindowBuckets[bucketIndex] = 0;
            Reprice(goodId);
            changed = true;
        }

        if (changed)
        {
            TouchAuthority();
        }
    }

    private void SeedRecentFlowFromLegacyTotals()
    {
        const long maximumSeedPerGood = 100;
        var start = _currentFlowBucket * EconomyRules.MarketGoodCount;
        for (var goodId = 0; goodId < EconomyRules.MarketGoodCount; goodId++)
        {
            _recentSupply[goodId] = Math.Min(_supply[goodId], maximumSeedPerGood);
            _recentDemand[goodId] = Math.Min(_demand[goodId], maximumSeedPerGood);
            _supplyWindowBuckets[start + goodId] = _recentSupply[goodId];
            _demandWindowBuckets[start + goodId] = _recentDemand[goodId];
            Reprice(goodId);
        }
    }

}
