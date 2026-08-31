using Chronicles.Core;

namespace Chronicles.Simulation;

internal sealed partial class CityMarket
{
    public MarketSettlementReceipt SettlePurchase(
        int agentIndex,
        long sourceResultId,
        long tick,
        int goodId,
        int count,
        long availableSpiritStones,
        QualityValue? minimumQuality = null)
    {
        ValidateCount(count);
        goodId = ValidateGood(goodId);
        ArgumentOutOfRangeException.ThrowIfNegative(availableSpiritStones);
        if (!TryBeginSettlement(agentIndex, sourceResultId))
        {
            return new MarketSettlementReceipt(MarketSettlementStatus.DuplicateSource);
        }

        var definition = EconomyRules.GetMarketGood(goodId);
        var isQualityGood = IsQualityGood(definition.Kind);
        if (isQualityGood && count != 1)
        {
            throw new InvalidOperationException("품질 상품은 한 번에 하나씩 정산해야 합니다.");
        }

        var unitPrice = GetPrice(goodId);
        var total = checked(unitPrice * count);
        if (availableSpiritStones < total)
        {
            return new MarketSettlementReceipt(MarketSettlementStatus.InsufficientFunds, total);
        }
        if (_stock[goodId] < count)
        {
            RegisterDemand(goodId, count);
            return new MarketSettlementReceipt(MarketSettlementStatus.OutOfStock, total);
        }

        QualityValue? purchasedQuality = null;
        if (isQualityGood)
        {
            var raw = checked((ushort)Math.Clamp(
                _qualityHundredths[goodId] / _stock[goodId],
                0L,
                QualityValue.MaximumHundredths));
            purchasedQuality = new QualityValue(raw);
            if (minimumQuality is { } minimum && purchasedQuality.Value <= minimum)
            {
                return new MarketSettlementReceipt(
                    MarketSettlementStatus.NotAnImprovement,
                    total,
                    purchasedQuality);
            }

            _qualityHundredths[goodId] -= raw;
        }

        RegisterDemand(goodId, count);
        _stock[goodId] -= count;
        _purchaseTransactions = checked(_purchaseTransactions + 1);
        TreasurySpiritStones = checked(TreasurySpiritStones + total);
        Reprice(goodId);
        RecordTrade(
            tick,
            agentIndex,
            MarketTradeKind.Purchase,
            goodId,
            count,
            unitPrice,
            total,
            purchasedQuality);
        return new MarketSettlementReceipt(MarketSettlementStatus.Settled, total, purchasedQuality);
    }

    public MarketSettlementReceipt SettleSale(
        int agentIndex,
        long sourceResultId,
        long tick,
        int goodId,
        int count,
        QualityValue? quality = null)
    {
        ValidateCount(count);
        goodId = ValidateGood(goodId);
        if (!TryBeginSettlement(agentIndex, sourceResultId))
        {
            return new MarketSettlementReceipt(MarketSettlementStatus.DuplicateSource);
        }

        var isQualityGood = IsQualityGood(EconomyRules.GetMarketGood(goodId).Kind);
        if (isQualityGood != quality.HasValue || (isQualityGood && count != 1))
        {
            throw new InvalidOperationException("품질 상품과 일반 상품의 판매 정산 계약이 일치하지 않습니다.");
        }

        var quotedPrice = quality is { } actualQuality
            ? EconomyRules.GetQualityAdjustedPrice(_prices[goodId], actualQuality)
            : GetPrice(goodId);
        var unitProceeds = checked(quotedPrice * 4 / 5);
        var proceeds = checked(unitProceeds * count);
        if (TreasurySpiritStones < proceeds)
        {
            return new MarketSettlementReceipt(
                MarketSettlementStatus.CityTreasuryInsufficient,
                proceeds,
                quality);
        }

        TreasurySpiritStones -= proceeds;
        _saleTransactions = checked(_saleTransactions + 1);
        if (quality is { } suppliedQuality)
        {
            _qualityHundredths[goodId] = checked(
                _qualityHundredths[goodId] + suppliedQuality.Hundredths);
        }
        AddSupply(goodId, count);
        RecordTrade(
            tick,
            agentIndex,
            MarketTradeKind.Sale,
            goodId,
            count,
            unitProceeds,
            proceeds,
            quality);
        return new MarketSettlementReceipt(MarketSettlementStatus.Settled, proceeds, quality);
    }

    public MarketSettlementReceipt SettleMissionDelivery(
        int agentIndex,
        long sourceResultId,
        long tick,
        MaterialId material,
        int count,
        int realmBonus)
    {
        ValidateCount(count);
        var goodId = ValidateGood((int)material);
        if (!TryBeginSettlement(agentIndex, sourceResultId))
        {
            return new MarketSettlementReceipt(MarketSettlementStatus.DuplicateSource);
        }

        var unitPrice = GetPrice(goodId);
        var reward = checked((unitPrice * count) + realmBonus);
        if (TreasurySpiritStones < reward)
        {
            return new MarketSettlementReceipt(
                MarketSettlementStatus.CityTreasuryInsufficient,
                reward);
        }

        TreasurySpiritStones -= reward;
        _missionDeliveries = checked(_missionDeliveries + 1);
        AddSupply(goodId, count);
        RecordTrade(
            tick,
            agentIndex,
            MarketTradeKind.MissionDelivery,
            goodId,
            count,
            unitPrice,
            reward,
            null);
        return new MarketSettlementReceipt(MarketSettlementStatus.Settled, reward);
    }

    private bool TryBeginSettlement(int agentIndex, long sourceResultId)
    {
        if ((uint)agentIndex >= (uint)_lastSettlementSourceByAgent.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(agentIndex));
        }
        ArgumentOutOfRangeException.ThrowIfNegative(sourceResultId);
        if (sourceResultId <= _lastSettlementSourceByAgent[agentIndex])
        {
            return false;
        }

        _lastSettlementSourceByAgent[agentIndex] = sourceResultId;
        return true;
    }

    private void RecordTrade(
        long tick,
        int agentIndex,
        MarketTradeKind kind,
        int goodId,
        int count,
        long unitPrice,
        long totalSpiritStones,
        QualityValue? quality)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        if (_recentTrades.Count == EconomyRules.RecentTradeCapacity)
        {
            _recentTrades.Dequeue();
        }

        _recentTrades.Enqueue(new MarketTradeRecord(
            _nextTradeSequence++,
            tick,
            agentIndex,
            kind,
            goodId,
            count,
            unitPrice,
            totalSpiritStones,
            quality?.Hundredths ?? NoQuality));
        TouchAuthority();
    }
}
