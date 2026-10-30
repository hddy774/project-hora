using Chronicles.Core;

namespace Chronicles.Simulation;

internal sealed partial class CityMarket
{
    private const ushort NoQuality = ushort.MaxValue;

    private readonly int[] _stock = new int[EconomyRules.MarketGoodCount];
    private readonly long[] _prices = new long[EconomyRules.MarketGoodCount];
    private readonly long[] _supply = new long[EconomyRules.MarketGoodCount];
    private readonly long[] _demand = new long[EconomyRules.MarketGoodCount];
    private readonly long[] _recentSupply = new long[EconomyRules.MarketGoodCount];
    private readonly long[] _recentDemand = new long[EconomyRules.MarketGoodCount];
    private readonly long[] _supplyWindowBuckets = new long[
        EconomyRules.MarketGoodCount * EconomyRules.MarketFlowWindowMonths];
    private readonly long[] _demandWindowBuckets = new long[
        EconomyRules.MarketGoodCount * EconomyRules.MarketFlowWindowMonths];
    private readonly long[] _qualityHundredths = new long[EconomyRules.MarketGoodCount];
    private readonly long[] _lastSettlementSourceByAgent;
    private readonly List<CityProductionOrder> _productionQueue = new(EconomyRules.MaximumConcurrentProduction);
    private readonly Queue<MarketTradeRecord> _recentTrades = new(EconomyRules.RecentTradeCapacity);
    private readonly long[] _materialMissionScoreCache = new long[EconomyRules.MaterialCount];
    private long _lastIndustryMonth;
    private int _currentFlowBucket;
    private long _authorityRevision = 1;
    private long _nextTradeSequence = 1;
    private long _derivedCacheRevision = -1;
    private bool _productionBlockedByTreasuryCache;
    private long _summaryCacheRevision = -1;
    private long _goodsCacheRevision = -1;
    private CityMarketSnapshot? _summaryCache;
    private CityMarketSnapshot? _goodsCache;
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
        _lastSettlementSourceByAgent = new long[population];
        Array.Fill(_lastSettlementSourceByAgent, -1L);
        TreasurySpiritStones = checked(Math.Max(100_000L, population * 100L));
        _spiritStoneReserveTarget = checked(Math.Max(500_000L, population * 500L));
        _remainingSpiritStoneOre = checked(Math.Max(10_000_000L, population * 100_000L));

        for (var goodId = 0; goodId < EconomyRules.MarketGoodCount; goodId++)
        {
            var definition = EconomyRules.GetMarketGood(goodId);
            _prices[goodId] = definition.CityBasePrice;
            _stock[goodId] = EconomyRules.GetInitialCityStock(definition);
            if (IsQualityGood(definition.Kind) && _stock[goodId] > 0)
            {
                _qualityHundredths[goodId] = checked(
                    (long)EconomyRules.InitialSkillMaximumHundredths * _stock[goodId]);
            }
            Reprice(goodId);
        }
        InitializeMarketAuthority(currentMonth: 0);
    }

    public CityMarket(CityMarketCheckpoint checkpoint, int schemaVersion, int population)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(population);
        _lastSettlementSourceByAgent = new long[population];
        Array.Fill(_lastSettlementSourceByAgent, -1L);
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
        _remainingSpiritStoneOre = schemaVersion >= 14
            ? checkpoint.RemainingSpiritStoneOre
            : checkpoint.RemainingSpiritStoneOre > 0
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

            if (schemaVersion >= 24)
            {
                Array.Copy(checkpoint.RecentSupply!, _recentSupply, _recentSupply.Length);
                Array.Copy(checkpoint.RecentDemand!, _recentDemand, _recentDemand.Length);
                Array.Copy(
                    checkpoint.SupplyWindowBuckets!,
                    _supplyWindowBuckets,
                    _supplyWindowBuckets.Length);
                Array.Copy(
                    checkpoint.DemandWindowBuckets!,
                    _demandWindowBuckets,
                    _demandWindowBuckets.Length);
                _currentFlowBucket = checkpoint.CurrentFlowBucket;
                _authorityRevision = Math.Max(1L, checkpoint.AuthorityRevision);
                Array.Copy(
                    checkpoint.LastSettlementSourceByAgent!,
                    _lastSettlementSourceByAgent,
                    _lastSettlementSourceByAgent.Length);
                _nextTradeSequence = Math.Max(1L, checkpoint.NextTradeSequence);
                foreach (var trade in checkpoint.RecentTrades!)
                {
                    _recentTrades.Enqueue(new MarketTradeRecord(
                        trade.Sequence,
                        trade.SourceResultId,
                        trade.Tick,
                        trade.AgentId,
                        trade.Kind,
                        trade.GoodId,
                        trade.Count,
                        trade.UnitPrice,
                        trade.TotalSpiritStones,
                        trade.QualityHundredths));
                }
            }
            else
            {
                SeedRecentFlowFromLegacyTotals();
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
            SeedRecentFlowFromLegacyTotals();
        }

        for (var goodId = 0; goodId < EconomyRules.MarketGoodCount; goodId++)
        {
            if (_prices[goodId] <= 0)
            {
                _prices[goodId] = EconomyRules.GetMarketGood(goodId).CityBasePrice;
            }
        }

        if (checkpoint.Authority is { } authority)
        {
            RestoreMarketAuthority(authority);
        }
        else
        {
            InitializeMarketAuthority(_lastIndustryMonth);
        }
    }

    public void SeedInitialCultivatorDemand()
    {
        var starterDemand = Math.Clamp(_lastSettlementSourceByAgent.Length / 500, 1, 20);
        Span<int> starterGoods =
        [
            EconomyRules.GetPillGoodId(
                PillKind.Growth,
                RealmStage.QiRefining),
            EconomyRules.GetPillGoodId(
                PillKind.Breakthrough,
                RealmStage.QiRefining),
            EconomyRules.GetTreasureGoodId(
                TreasureSlot.DefenseTreasure,
                RealmStage.QiRefining),
            EconomyRules.GetTreasureGoodId(
                TreasureSlot.AttackTreasure,
                RealmStage.QiRefining),
            EconomyRules.TechniqueBookGoodId,
            EconomyRules.SpellBookGoodId,
        ];
        foreach (var goodId in starterGoods)
        {
            // 첫 실물 공급 전까지는 12개월 수급 창이 만료되어도 입문 수요가 사라지지
            // 않게 한다. 첫 생산 뒤에는 일반 수선자의 실제 주문만 가격·생산을 움직인다.
            if (_supply[goodId] == 0 && _recentDemand[goodId] == 0)
            {
                RegisterDemand(goodId, starterDemand);
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
        return Math.Max(0L, _recentDemand[goodId] - _recentSupply[goodId]);
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

    public bool TryCompleteMining(
        RealmStage realm,
        long ownerSpiritStones,
        out long workerReward,
        out long extracted,
        long maximumExtractable = long.MaxValue)
        => TryCompleteMiningCore(
            realm,
            ownerSpiritStones,
            out workerReward,
            out extracted,
            maximumExtractable,
            requireCityDemand: true);

    public bool TryCompleteOrganizationPreparationMining(
        RealmStage realm,
        long preparationReserve,
        out long workerReward,
        out long extracted,
        long maximumExtractable = long.MaxValue)
        => TryCompleteMiningCore(
            realm,
            preparationReserve,
            out workerReward,
            out extracted,
            maximumExtractable,
            requireCityDemand: false);

    private bool TryCompleteMiningCore(
        RealmStage realm,
        long ownerSpiritStones,
        out long workerReward,
        out long extracted,
        long maximumExtractable,
        bool requireCityDemand)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ownerSpiritStones);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumExtractable);
        workerReward = 0;
        extracted = 0;
        if (_remainingSpiritStoneOre <= 0
            || (requireCityDemand && !NeedsSpiritStoneMining))
        {
            return false;
        }

        extracted = Math.Min(
            Math.Min(_remainingSpiritStoneOre, maximumExtractable),
            EconomyRules.GetMiningOutput(realm));
        if (extracted <= 0)
        {
            return false;
        }

        workerReward = Math.Max(10L, extracted / 2);
        var treasuryIncome = extracted - workerReward;
        if (ownerSpiritStones > long.MaxValue - workerReward
            || TreasurySpiritStones > long.MaxValue - treasuryIncome)
        {
            workerReward = 0;
            extracted = 0;
            return false;
        }

        _remainingSpiritStoneOre -= extracted;
        _spiritStonesMined = SaturatingAdd(_spiritStonesMined, extracted);
        _miningMissionCount = SaturatingAdd(_miningMissionCount, 1);
        TreasurySpiritStones += treasuryIncome;
        TouchAuthority();
        return true;
    }

    public bool TryCollectCraftingFee(long availableSpiritStones, long fee)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fee);
        if (availableSpiritStones < fee)
        {
            return false;
        }

        if (TreasurySpiritStones > long.MaxValue - fee)
        {
            return false;
        }

        TreasurySpiritStones += fee;
        TouchAuthority();
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
            AdvanceFlowWindowOneMonth();
            AdvanceProductionOneMonth();
            FillProductionQueue();
            AdvanceContractAuthority(_lastIndustryMonth);
            TouchAuthority();
        }
    }

    public CityMarketSnapshot CreateSnapshot(bool includeGoods = true)
    {
        if (includeGoods && _goodsCacheRevision == _authorityRevision)
        {
            return _goodsCache!;
        }
        if (!includeGoods && _summaryCacheRevision == _authorityRevision)
        {
            return _summaryCache!;
        }

        var snapshot = BuildSnapshot(includeGoods);
        if (includeGoods)
        {
            _goodsCache = snapshot;
            _goodsCacheRevision = _authorityRevision;
        }
        else
        {
            _summaryCache = snapshot;
            _summaryCacheRevision = _authorityRevision;
        }

        return snapshot;
    }

    private CityMarketSnapshot BuildSnapshot(bool includeGoods)
    {
        var goods = includeGoods
            ? new MarketGoodSnapshot[EconomyRules.MarketGoodCount]
            : Array.Empty<MarketGoodSnapshot>();
        long totalStock = 0;
        long materialStock = 0;
        long totalSupply = 0;
        long totalDemand = 0;
        long unmetDemand = 0;
        long recentSupply = 0;
        long recentDemand = 0;
        for (var goodId = 0; goodId < EconomyRules.MarketGoodCount; goodId++)
        {
            var definition = EconomyRules.GetMarketGood(goodId);
            totalStock = checked(totalStock + _stock[goodId]);
            totalSupply = checked(totalSupply + _supply[goodId]);
            totalDemand = checked(totalDemand + _demand[goodId]);
            recentSupply = checked(recentSupply + _recentSupply[goodId]);
            recentDemand = checked(recentDemand + _recentDemand[goodId]);
            unmetDemand = checked(
                unmetDemand + Math.Max(0L, _recentDemand[goodId] - _recentSupply[goodId]));
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
                    GetAverageQuality(goodId),
                    _recentSupply[goodId],
                    _recentDemand[goodId],
                    GetProductionState(goodId, definition),
                    TryGetRequiredMaterial(definition.Kind, out var requiredMaterial)
                        ? requiredMaterial
                        : null,
                    definition.Kind == MarketGoodKind.Material
                        ? 0
                        : EconomyRules.GetProductionMaterialCost(definition.Kind, definition.Realm),
                    definition.Kind == MarketGoodKind.Material
                        ? 0
                        : EconomyRules.GetProductionSpiritStoneCost(definition));
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

        var trades = includeGoods
            ? _recentTrades.Select(trade => new MarketTradeSnapshot(
                trade.Sequence,
                trade.SourceResultId,
                new SimulationTick(trade.Tick),
                new AgentId(trade.AgentId),
                trade.Kind,
                trade.GoodId,
                trade.Count,
                trade.UnitPrice,
                trade.TotalSpiritStones,
                trade.QualityHundredths == NoQuality
                    ? null
                    : new QualityValue(trade.QualityHundredths))).ToArray()
            : Array.Empty<MarketTradeSnapshot>();

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
            EconomyRules.MaximumConcurrentProduction,
            _authorityRevision,
            recentSupply,
            recentDemand,
            trades,
            includeGoods ? CreateOfferSnapshots() : Array.Empty<MarketOfferSnapshot>(),
            includeGoods ? CreateReservationSnapshots() : Array.Empty<MarketReservationSnapshot>());
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
        _spiritStoneReserveTarget,
        _recentSupply.ToArray(),
        _recentDemand.ToArray(),
        _supplyWindowBuckets.ToArray(),
        _demandWindowBuckets.ToArray(),
        _currentFlowBucket,
        _authorityRevision,
        _lastSettlementSourceByAgent.ToArray(),
        _recentTrades.Select(trade => new MarketTradeCheckpoint(
            trade.Sequence,
            trade.SourceResultId,
            trade.Tick,
            trade.AgentId,
            trade.Kind,
            trade.GoodId,
            trade.Count,
            trade.UnitPrice,
            trade.TotalSpiritStones,
            trade.QualityHundredths)).ToArray(),
        _nextTradeSequence,
        CreateMarketAuthorityCheckpoint());

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

    private void TouchAuthority()
    {
        _authorityRevision = _authorityRevision == long.MaxValue
            ? 1
            : _authorityRevision + 1;
        _derivedCacheRevision = -1;
    }

    private static long SaturatingAdd(long left, long right) =>
        left > long.MaxValue - right ? long.MaxValue : left + right;

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

    private readonly record struct MarketTradeRecord(
        long Sequence,
        long SourceResultId,
        long Tick,
        int AgentId,
        MarketTradeKind Kind,
        int GoodId,
        int Count,
        long UnitPrice,
        long TotalSpiritStones,
        ushort QualityHundredths);
}
