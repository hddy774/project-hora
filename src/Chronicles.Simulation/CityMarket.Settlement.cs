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
        if (!IsFreshSettlementSource(agentIndex, sourceResultId, tick))
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
            CommitSettlementSource(agentIndex, sourceResultId);
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
        }

        if (!CanRecordTrade()
            || TreasurySpiritStones > long.MaxValue - total)
        {
            return new MarketSettlementReceipt(
                MarketSettlementStatus.CapacityExceeded,
                total,
                purchasedQuality);
        }

        CommitSettlementSource(agentIndex, sourceResultId);
        if (purchasedQuality is { } quality)
        {
            _qualityHundredths[goodId] -= quality.Hundredths;
        }
        RegisterDemand(goodId, count);
        _stock[goodId] -= count;
        _purchaseTransactions = SaturatingAdd(_purchaseTransactions, 1);
        TreasurySpiritStones += total;
        Reprice(goodId);
        RecordTrade(
            tick,
            agentIndex,
            sourceResultId,
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
        long ownerSpiritStones,
        QualityValue? quality = null)
    {
        ValidateCount(count);
        goodId = ValidateGood(goodId);
        ArgumentOutOfRangeException.ThrowIfNegative(ownerSpiritStones);
        if (!IsFreshSettlementSource(agentIndex, sourceResultId, tick))
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

        if (!CanRecordTrade()
            || ownerSpiritStones > long.MaxValue - proceeds
            || _stock[goodId] > int.MaxValue - count
            || (quality is { } supplied
                && _qualityHundredths[goodId] > long.MaxValue - supplied.Hundredths))
        {
            return new MarketSettlementReceipt(
                MarketSettlementStatus.CapacityExceeded,
                proceeds,
                quality);
        }

        CommitSettlementSource(agentIndex, sourceResultId);
        TreasurySpiritStones -= proceeds;
        _saleTransactions = SaturatingAdd(_saleTransactions, 1);
        if (quality is { } suppliedQuality)
        {
            _qualityHundredths[goodId] = checked(
                _qualityHundredths[goodId] + suppliedQuality.Hundredths);
        }
        AddSupply(goodId, count);
        RecordTrade(
            tick,
            agentIndex,
            sourceResultId,
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
        int realmBonus,
        long ownerSpiritStones)
    {
        ValidateCount(count);
        var goodId = ValidateGood((int)material);
        ArgumentOutOfRangeException.ThrowIfNegative(realmBonus);
        ArgumentOutOfRangeException.ThrowIfNegative(ownerSpiritStones);
        if (!IsFreshSettlementSource(agentIndex, sourceResultId, tick))
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

        if (!CanRecordTrade()
            || ownerSpiritStones > long.MaxValue - reward
            || _stock[goodId] > int.MaxValue - count)
        {
            return new MarketSettlementReceipt(MarketSettlementStatus.CapacityExceeded, reward);
        }

        CommitSettlementSource(agentIndex, sourceResultId);
        TreasurySpiritStones -= reward;
        _missionDeliveries = SaturatingAdd(_missionDeliveries, 1);
        AddSupply(goodId, count);
        RecordTrade(
            tick,
            agentIndex,
            sourceResultId,
            MarketTradeKind.MissionDelivery,
            goodId,
            count,
            unitPrice,
            reward,
            null);
        return new MarketSettlementReceipt(MarketSettlementStatus.Settled, reward);
    }

    private bool IsFreshSettlementSource(int agentIndex, long sourceResultId, long tick)
    {
        if ((uint)agentIndex >= (uint)_lastSettlementSourceByAgent.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(agentIndex));
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceResultId);
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        return sourceResultId > _lastSettlementSourceByAgent[agentIndex];
    }

    private void CommitSettlementSource(int agentIndex, long sourceResultId) =>
        _lastSettlementSourceByAgent[agentIndex] = sourceResultId;

    // NextTradeSequence == long.MaxValue인 저장 상태를 만들지 않도록 마지막 값 하나를
    // 여유로 둔다. 모든 성공 정산은 거래 원장까지 기록되거나 아무것도 바꾸지 않는다.
    private bool CanRecordTrade() => _nextTradeSequence < long.MaxValue - 1L;

    private void RecordTrade(
        long tick,
        int agentIndex,
        long sourceResultId,
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
            _nextTradeSequence,
            sourceResultId,
            tick,
            agentIndex,
            kind,
            goodId,
            count,
            unitPrice,
            totalSpiritStones,
            quality?.Hundredths ?? NoQuality));
        _nextTradeSequence = checked(_nextTradeSequence + 1L);
        TouchAuthority();
    }
}
