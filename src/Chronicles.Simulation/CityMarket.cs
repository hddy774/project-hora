using Chronicles.Core;

namespace Chronicles.Simulation;

internal sealed class CityMarket
{
    private const ushort NoQuality = ushort.MaxValue;

    private readonly int[] _stock = new int[EconomyRules.MarketGoodCount];
    private readonly long[] _prices = new long[EconomyRules.MarketGoodCount];
    private readonly long[] _supply = new long[EconomyRules.MarketGoodCount];
    private readonly long[] _demand = new long[EconomyRules.MarketGoodCount];
    private readonly long[] _qualityHundredths = new long[EconomyRules.MarketGoodCount];
    private readonly List<CityProductionOrder> _productionQueue = new(EconomyRules.MaximumConcurrentProduction);
    private long _lastIndustryMonth;
    private long _purchaseTransactions;
    private long _saleTransactions;
    private long _missionDeliveries;
    private long _materialsProcessed;
    private long _pillsProduced;
    private long _treasuresProduced;
    private long _techniqueBooksProduced;
    private long _spellBooksProduced;
    private long _spiritStonesMined;
    private long _remainingSpiritStoneOre;
    private long _miningMissionCount;
    private long _spiritStoneReserveTarget;

    public CityMarket(int population)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(population);
        TreasurySpiritStones = checked(Math.Max(100_000L, population * 100L));
        _spiritStoneReserveTarget = checked(Math.Max(500_000L, population * 500L));
        _remainingSpiritStoneOre = checked(Math.Max(10_000_000L, population * 100_000L));

        for (var goodId = 0; goodId < EconomyRules.MarketGoodCount; goodId++)
        {
            var definition = EconomyRules.GetMarketGood(goodId);
            _prices[goodId] = definition.CityBasePrice;
            _stock[goodId] = definition.Kind switch
            {
                MarketGoodKind.Material => 2,
                MarketGoodKind.GrowthPill or MarketGoodKind.BreakthroughPill
                    when definition.Realm == RealmStage.QiRefining => 3,
                MarketGoodKind.TechniqueBook or MarketGoodKind.SpellBook => 2,
                _ => 0,
            };
            if (IsQualityGood(definition.Kind) && _stock[goodId] > 0)
            {
                _qualityHundredths[goodId] = checked(
                    (long)EconomyRules.InitialSkillMaximumHundredths * _stock[goodId]);
            }
        }
    }

    public CityMarket(CityMarketCheckpoint checkpoint, int schemaVersion)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        TreasurySpiritStones = checkpoint.TreasurySpiritStones;
        _purchaseTransactions = checkpoint.PurchaseTransactions;
        _saleTransactions = checkpoint.SaleTransactions;
        _missionDeliveries = checkpoint.MissionDeliveries;
        _materialsProcessed = checkpoint.MaterialsProcessed;
        _pillsProduced = checkpoint.PillsProduced;
        _treasuresProduced = checkpoint.TreasuresProduced;
        _techniqueBooksProduced = checkpoint.TechniqueBooksProduced;
        _spellBooksProduced = checkpoint.SpellBooksProduced;
        _lastIndustryMonth = checkpoint.LastIndustryMonth;
        _spiritStonesMined = checkpoint.SpiritStonesMined;
        _miningMissionCount = checkpoint.MiningMissionCount;
        _spiritStoneReserveTarget = checkpoint.SpiritStoneReserveTarget > 0
            ? checkpoint.SpiritStoneReserveTarget
            : Math.Max(500_000L, TreasurySpiritStones);
        _remainingSpiritStoneOre = checkpoint.RemainingSpiritStoneOre > 0
            ? checkpoint.RemainingSpiritStoneOre
            : 100_000_000L;

        if (checkpoint.Stock.Length == EconomyRules.MarketGoodCount)
        {
            Array.Copy(checkpoint.Stock, _stock, _stock.Length);
            Array.Copy(checkpoint.Prices, _prices, _prices.Length);
            Array.Copy(checkpoint.Supply, _supply, _supply.Length);
            Array.Copy(checkpoint.Demand, _demand, _demand.Length);
            if (checkpoint.QualityHundredths is { Length: EconomyRules.MarketGoodCount } qualities)
            {
                Array.Copy(qualities, _qualityHundredths, _qualityHundredths.Length);
            }

            if (checkpoint.ProductionQueue is { } queue)
            {
                foreach (var order in queue.Take(EconomyRules.MaximumConcurrentProduction))
                {
                    _productionQueue.Add(new CityProductionOrder(
                        order.GoodId,
                        order.QualityHundredths,
                        order.CompletedMonths,
                        order.RequiredMonths,
                        order.MaterialCost,
                        order.SpiritStoneCost));
                }
            }
        }
        else
        {
            RestoreLegacyMarket(checkpoint);
        }

        for (var goodId = 0; goodId < EconomyRules.MarketGoodCount; goodId++)
        {
            if (_prices[goodId] <= 0)
            {
                _prices[goodId] = EconomyRules.GetMarketGood(goodId).CityBasePrice;
            }
        }
    }

    public long TreasurySpiritStones { get; private set; }
    public long PillsProduced => _pillsProduced;
    public long TreasuresProduced => _treasuresProduced;

    public long GetPrice(int goodId)
    {
        goodId = ValidateGood(goodId);
        var average = GetAverageQuality(goodId);
        return average is { } quality
            ? EconomyRules.GetQualityAdjustedPrice(_prices[goodId], quality)
            : _prices[goodId];
    }

    public int GetStock(int goodId) => _stock[ValidateGood(goodId)];

    public long GetUnmetDemand(int goodId)
    {
        goodId = ValidateGood(goodId);
        return Math.Max(0L, _demand[goodId] - _supply[goodId]);
    }

    public bool TryBuy(int goodId, int count, long availableSpiritStones, out long cost)
    {
        ValidateCount(count);
        goodId = ValidateGood(goodId);
        if (IsQualityGood(EconomyRules.GetMarketGood(goodId).Kind))
        {
            throw new InvalidOperationException("품질 상품은 품질 포함 구매 API를 사용해야 합니다.");
        }

        RegisterDemand(goodId, count);
        cost = checked(GetPrice(goodId) * count);
        if (_stock[goodId] < count || availableSpiritStones < cost)
        {
            return false;
        }

        _stock[goodId] -= count;
        _purchaseTransactions = checked(_purchaseTransactions + 1);
        TreasurySpiritStones = checked(TreasurySpiritStones + cost);
        Reprice(goodId);
        return true;
    }

    public bool TryBuyQualityGood(
        int goodId,
        long availableSpiritStones,
        out long cost,
        out QualityValue quality)
    {
        goodId = ValidateGood(goodId);
        if (!IsQualityGood(EconomyRules.GetMarketGood(goodId).Kind))
        {
            throw new InvalidOperationException("일반 상품은 일반 구매 API를 사용해야 합니다.");
        }

        RegisterDemand(goodId, 1);
        cost = GetPrice(goodId);
        if (_stock[goodId] <= 0 || availableSpiritStones < cost)
        {
            quality = default;
            return false;
        }

        var raw = checked((ushort)Math.Clamp(
            _qualityHundredths[goodId] / _stock[goodId],
            0L,
            QualityValue.MaximumHundredths));
        _stock[goodId]--;
        _qualityHundredths[goodId] -= raw;
        _purchaseTransactions = checked(_purchaseTransactions + 1);
        TreasurySpiritStones = checked(TreasurySpiritStones + cost);
        quality = new QualityValue(raw);
        Reprice(goodId);
        return true;
    }

    public bool TrySell(int goodId, int count, out long proceeds)
    {
        ValidateCount(count);
        goodId = ValidateGood(goodId);
        if (IsQualityGood(EconomyRules.GetMarketGood(goodId).Kind))
        {
            throw new InvalidOperationException("품질 상품은 품질 포함 판매 API를 사용해야 합니다.");
        }

        proceeds = checked(GetPrice(goodId) * count * 4 / 5);
        if (TreasurySpiritStones < proceeds)
        {
            return false;
        }

        TreasurySpiritStones -= proceeds;
        _saleTransactions = checked(_saleTransactions + 1);
        AddSupply(goodId, count);
        return true;
    }

    public bool TrySellQualityGood(int goodId, QualityValue quality, out long proceeds)
    {
        goodId = ValidateGood(goodId);
        if (!IsQualityGood(EconomyRules.GetMarketGood(goodId).Kind))
        {
            throw new InvalidOperationException("일반 상품은 일반 판매 API를 사용해야 합니다.");
        }

        proceeds = checked(EconomyRules.GetQualityAdjustedPrice(_prices[goodId], quality) * 4 / 5);
        if (TreasurySpiritStones < proceeds)
        {
            return false;
        }

        TreasurySpiritStones -= proceeds;
        _saleTransactions = checked(_saleTransactions + 1);
        _qualityHundredths[goodId] = checked(_qualityHundredths[goodId] + quality.Hundredths);
        AddSupply(goodId, 1);
        return true;
    }

    public bool TryAcceptMissionDelivery(
        MaterialId material,
        int count,
        int realmBonus,
        out long reward)
    {
        ValidateCount(count);
        var goodId = ValidateGood((int)material);
        reward = checked((GetPrice(goodId) * count) + realmBonus);
        if (!HasMaterialDemand(material) || TreasurySpiritStones < reward)
        {
            reward = 0;
            return false;
        }

        TreasurySpiritStones -= reward;
        _missionDeliveries = checked(_missionDeliveries + 1);
        AddSupply(goodId, count);
        return true;
    }

    public bool TryGetRequestedMissionMaterial(out MaterialId material)
    {
        var treasureDemand = GetMaterialMissionScore(MaterialId.TreasureMaterial);
        var pillDemand = GetMaterialMissionScore(MaterialId.PillMaterial);
        if (treasureDemand <= 0 && pillDemand <= 0)
        {
            material = default;
            return false;
        }

        material = treasureDemand >= pillDemand
            ? MaterialId.TreasureMaterial
            : MaterialId.PillMaterial;
        return true;
    }

    public bool NeedsSpiritStoneMining =>
        _remainingSpiritStoneOre > 0
        && (TreasurySpiritStones < _spiritStoneReserveTarget || IsProductionBlockedByTreasury());

    public bool TryCompleteMining(RealmStage realm, out long workerReward, out long extracted)
    {
        workerReward = 0;
        extracted = 0;
        if (!NeedsSpiritStoneMining)
        {
            return false;
        }

        extracted = Math.Min(_remainingSpiritStoneOre, EconomyRules.GetMiningOutput(realm));
        if (extracted <= 0)
        {
            return false;
        }

        workerReward = Math.Max(10L, extracted / 2);
        _remainingSpiritStoneOre -= extracted;
        _spiritStonesMined = checked(_spiritStonesMined + extracted);
        _miningMissionCount = checked(_miningMissionCount + 1);
        TreasurySpiritStones = checked(TreasurySpiritStones + extracted - workerReward);
        return true;
    }

    public bool TryCollectCraftingFee(long availableSpiritStones, long fee)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fee);
        if (availableSpiritStones < fee)
        {
            return false;
        }

        TreasurySpiritStones = checked(TreasurySpiritStones + fee);
        return true;
    }

    public void AdvanceIndustry(long currentMonth)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(currentMonth);
        if (currentMonth <= _lastIndustryMonth)
        {
            return;
        }

        while (_lastIndustryMonth < currentMonth)
        {
            _lastIndustryMonth++;
            AdvanceProductionOneMonth();
            FillProductionQueue();
        }
    }

    public CityMarketSnapshot CreateSnapshot(bool includeGoods = true)
    {
        var goods = includeGoods
            ? new MarketGoodSnapshot[EconomyRules.MarketGoodCount]
            : Array.Empty<MarketGoodSnapshot>();
        long totalStock = 0;
        long materialStock = 0;
        long totalSupply = 0;
        long totalDemand = 0;
        long unmetDemand = 0;
        for (var goodId = 0; goodId < EconomyRules.MarketGoodCount; goodId++)
        {
            var definition = EconomyRules.GetMarketGood(goodId);
            totalStock = checked(totalStock + _stock[goodId]);
            totalSupply = checked(totalSupply + _supply[goodId]);
            totalDemand = checked(totalDemand + _demand[goodId]);
            unmetDemand = checked(unmetDemand + Math.Max(0L, _demand[goodId] - _supply[goodId]));
            if (definition.Kind == MarketGoodKind.Material)
            {
                materialStock = checked(materialStock + _stock[goodId]);
            }

            if (includeGoods)
            {
                goods[goodId] = new MarketGoodSnapshot(
                    goodId,
                    definition.Kind,
                    definition.Material,
                    definition.Realm,
                    definition.CityBasePrice,
                    GetPrice(goodId),
                    _stock[goodId],
                    _supply[goodId],
                    _demand[goodId],
                    GetAverageQuality(goodId));
            }
        }

        var production = new CityProductionSnapshot[_productionQueue.Count];
        for (var index = 0; index < production.Length; index++)
        {
            var order = _productionQueue[index];
            var definition = EconomyRules.GetMarketGood(order.GoodId);
            production[index] = new CityProductionSnapshot(
                order.GoodId,
                definition.Kind,
                definition.Realm,
                order.CompletedMonths,
                order.RequiredMonths,
                TryGetRequiredMaterial(definition.Kind, out var material) ? material : null,
                order.MaterialCost,
                order.SpiritStoneCost,
                order.QualityHundredths == NoQuality
                    ? null
                    : new QualityValue(order.QualityHundredths));
        }

        return new CityMarketSnapshot(
            TreasurySpiritStones,
            _purchaseTransactions,
            _saleTransactions,
            _missionDeliveries,
            _materialsProcessed,
            _pillsProduced,
            _treasuresProduced,
            _techniqueBooksProduced,
            _spellBooksProduced,
            goods,
            _spiritStonesMined,
            _remainingSpiritStoneOre,
            _miningMissionCount,
            production,
            totalStock,
            materialStock,
            checked(totalStock - materialStock),
            totalSupply,
            totalDemand,
            unmetDemand,
            EconomyRules.MaximumConcurrentProduction);
    }

    public CityMarketCheckpoint CreateCheckpoint() => new(
        TreasurySpiritStones,
        _purchaseTransactions,
        _saleTransactions,
        _missionDeliveries,
        _materialsProcessed,
        _pillsProduced,
        _treasuresProduced,
        _techniqueBooksProduced,
        _spellBooksProduced,
        _stock.ToArray(),
        _prices.ToArray(),
        _supply.ToArray(),
        _demand.ToArray(),
        0,
        _qualityHundredths.ToArray(),
        _productionQueue.Select(order => new CityProductionCheckpoint(
            order.GoodId,
            order.QualityHundredths,
            order.CompletedMonths,
            order.RequiredMonths,
            order.MaterialCost,
            order.SpiritStoneCost)).ToArray(),
        _lastIndustryMonth,
        _spiritStonesMined,
        _remainingSpiritStoneOre,
        _miningMissionCount,
        _spiritStoneReserveTarget);

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
            if (unmet <= 0 || _stock[goodId] + queued >= EconomyRules.CityProductionTargetStock)
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
            _demand[(int)consumed] = checked(_demand[(int)consumed] + materialCost);
            _materialsProcessed = checked(_materialsProcessed + materialCost);
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
                _pillsProduced = checked(_pillsProduced + 1);
                break;
            case MarketGoodKind.DefenseTreasure:
            case MarketGoodKind.AttackTreasure:
                _treasuresProduced = checked(_treasuresProduced + 1);
                break;
            case MarketGoodKind.TechniqueBook:
                _techniqueBooksProduced = checked(_techniqueBooksProduced + 1);
                break;
            case MarketGoodKind.SpellBook:
                _spellBooksProduced = checked(_spellBooksProduced + 1);
                break;
        }

        if (order.QualityHundredths != NoQuality)
        {
            _qualityHundredths[order.GoodId] = checked(
                _qualityHundredths[order.GoodId] + order.QualityHundredths);
        }

        AddSupply(order.GoodId, 1);
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

    private bool HasMaterialDemand(MaterialId material) => GetMaterialMissionScore(material) > 0;

    private long GetMaterialMissionScore(MaterialId material)
    {
        var materialId = (int)material;
        var score = Math.Max(0L, _demand[materialId] - _supply[materialId]);
        for (var goodId = EconomyRules.RealmGoodStartId; goodId < EconomyRules.MarketGoodCount; goodId++)
        {
            var definition = EconomyRules.GetMarketGood(goodId);
            if (!TryGetRequiredMaterial(definition.Kind, out var required) || required != material)
            {
                continue;
            }

            var unmet = GetUnmetDemand(goodId);
            if (unmet > 0)
            {
                score = checked(score + unmet * EconomyRules.GetProductionMaterialCost(
                    definition.Kind,
                    definition.Realm));
            }
        }

        return Math.Max(0L, score - _stock[materialId]);
    }

    private bool IsProductionBlockedByTreasury()
    {
        for (var goodId = EconomyRules.RealmGoodStartId; goodId < EconomyRules.MarketGoodCount; goodId++)
        {
            if (GetUnmetDemand(goodId) <= 0)
            {
                continue;
            }

            var definition = EconomyRules.GetMarketGood(goodId);
            if (TreasurySpiritStones < EconomyRules.GetProductionSpiritStoneCost(definition))
            {
                return true;
            }
        }

        return false;
    }

    private void RegisterDemand(int goodId, int count)
    {
        _demand[goodId] = checked(_demand[goodId] + count);
        Reprice(goodId);
    }

    private void AddSupply(int goodId, int count)
    {
        _stock[goodId] = checked(_stock[goodId] + count);
        _supply[goodId] = checked(_supply[goodId] + count);
        Reprice(goodId);
    }

    private void Reprice(int goodId)
    {
        var definition = EconomyRules.GetMarketGood(goodId);
        var targetStock = definition.Kind == MarketGoodKind.Material
            ? 12
            : EconomyRules.CityProductionTargetStock;
        var tradeImbalance = Math.Clamp(_demand[goodId] - _supply[goodId], -100L, 100L);
        var stockPressure = Math.Clamp(targetStock - _stock[goodId], -50, 100);
        var percentage = Math.Clamp(100L + (tradeImbalance * 2) + (stockPressure * 3L), 25L, 400L);
        _prices[goodId] = Math.Max(1L, checked(definition.CityBasePrice * percentage / 100L));
    }

    private void RestoreLegacyMarket(CityMarketCheckpoint checkpoint)
    {
        for (var goodId = 0; goodId < EconomyRules.MarketGoodCount; goodId++)
        {
            _prices[goodId] = EconomyRules.GetMarketGood(goodId).CityBasePrice;
        }

        for (var oldId = 0; oldId < Math.Min(EconomyRules.LegacyMaterialCount, checkpoint.Stock.Length); oldId++)
        {
            var target = oldId < 10 || oldId is >= 20 and < 25
                ? (int)MaterialId.TreasureMaterial
                : (int)MaterialId.PillMaterial;
            _stock[target] = checked(_stock[target] + checkpoint.Stock[oldId]);
            if (oldId < checkpoint.Supply.Length)
            {
                _supply[target] = checked(_supply[target] + checkpoint.Supply[oldId]);
            }
            if (oldId < checkpoint.Demand.Length)
            {
                _demand[target] = checked(_demand[target] + checkpoint.Demand[oldId]);
            }
        }

        RestoreLegacyGood(checkpoint, 30, EconomyRules.GetPillGoodId(PillKind.Growth, RealmStage.QiRefining));
        RestoreLegacyGood(checkpoint, 31, EconomyRules.GetPillGoodId(PillKind.Breakthrough, RealmStage.QiRefining));
        RestoreLegacyTreasure(checkpoint);
        RestoreLegacyGood(checkpoint, 33, EconomyRules.TechniqueBookGoodId, quality: 7_999);
        RestoreLegacyGood(checkpoint, 34, EconomyRules.SpellBookGoodId, quality: 7_999);
    }

    private void RestoreLegacyGood(
        CityMarketCheckpoint checkpoint,
        int oldId,
        int newId,
        ushort? quality = null)
    {
        if (oldId >= checkpoint.Stock.Length)
        {
            return;
        }

        _stock[newId] = checked(_stock[newId] + checkpoint.Stock[oldId]);
        if (oldId < checkpoint.Supply.Length)
        {
            _supply[newId] = checked(_supply[newId] + checkpoint.Supply[oldId]);
        }
        if (oldId < checkpoint.Demand.Length)
        {
            _demand[newId] = checked(_demand[newId] + checkpoint.Demand[oldId]);
        }
        if (quality is { } raw)
        {
            _qualityHundredths[newId] = checked((long)raw * _stock[newId]);
        }
    }

    private void RestoreLegacyTreasure(CityMarketCheckpoint checkpoint)
    {
        const int oldTreasureId = 32;
        if (oldTreasureId >= checkpoint.Stock.Length)
        {
            return;
        }

        var total = checkpoint.Stock[oldTreasureId];
        var defense = total / 2;
        var attack = total - defense;
        var defenseId = EconomyRules.GetTreasureGoodId(
            TreasureSlot.DefenseTreasure,
            RealmStage.QiRefining);
        var attackId = EconomyRules.GetTreasureGoodId(
            TreasureSlot.AttackTreasure,
            RealmStage.QiRefining);
        var average = total > 0
            ? checked((ushort)Math.Clamp(
                checkpoint.TreasureQualityHundredths / total,
                0L,
                QualityValue.MaximumHundredths))
            : (ushort)0;
        _stock[defenseId] = defense;
        _stock[attackId] = attack;
        _qualityHundredths[defenseId] = checked((long)average * defense);
        _qualityHundredths[attackId] = checked((long)average * attack);
        var supply = oldTreasureId < checkpoint.Supply.Length ? checkpoint.Supply[oldTreasureId] : 0;
        var demand = oldTreasureId < checkpoint.Demand.Length ? checkpoint.Demand[oldTreasureId] : 0;
        _supply[defenseId] = supply / 2;
        _supply[attackId] = supply - _supply[defenseId];
        _demand[defenseId] = demand / 2;
        _demand[attackId] = demand - _demand[defenseId];
    }

    private static bool IsQualityGood(MarketGoodKind kind) => kind is
        MarketGoodKind.DefenseTreasure
        or MarketGoodKind.AttackTreasure
        or MarketGoodKind.TechniqueBook
        or MarketGoodKind.SpellBook;

    private static bool TryGetRequiredMaterial(MarketGoodKind kind, out MaterialId material)
    {
        if (kind is MarketGoodKind.DefenseTreasure or MarketGoodKind.AttackTreasure)
        {
            material = MaterialId.TreasureMaterial;
            return true;
        }
        if (kind is MarketGoodKind.GrowthPill or MarketGoodKind.BreakthroughPill)
        {
            material = MaterialId.PillMaterial;
            return true;
        }

        material = default;
        return false;
    }

    private static int ValidateGood(int goodId)
    {
        if ((uint)goodId >= EconomyRules.MarketGoodCount)
        {
            throw new ArgumentOutOfRangeException(nameof(goodId));
        }

        return goodId;
    }

    private static void ValidateCount(int count) =>
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

    private sealed class CityProductionOrder(
        int goodId,
        ushort qualityHundredths,
        int completedMonths,
        int requiredMonths,
        int materialCost,
        long spiritStoneCost)
    {
        public int GoodId { get; } = goodId;
        public ushort QualityHundredths { get; } = qualityHundredths;
        public int CompletedMonths { get; set; } = completedMonths;
        public int RequiredMonths { get; } = requiredMonths;
        public int MaterialCost { get; } = materialCost;
        public long SpiritStoneCost { get; } = spiritStoneCost;
    }
}
