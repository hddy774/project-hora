using Chronicles.Core;

namespace Chronicles.Simulation;

internal sealed partial class CityMarket
{
    private readonly MarketOfferAuthorityState[] _offers =
        new MarketOfferAuthorityState[EconomyRules.MarketGoodCount];
    private readonly List<MarketReservationAuthorityState> _reservations =
        new(EconomyRules.ReservationHistoryCapacity);
    private readonly int[] _reservedStock = new int[EconomyRules.MarketGoodCount];
    private long _nextOfferId = 1;
    private long _nextReservationId = 1;
    private long _lastContractExpiryMonth;

    public MarketContractReceipt ReserveCityOffer(
        int agentIndex,
        int goodId,
        int count,
        long availableSpiritStones,
        long currentMonth,
        long sourceResultId,
        MarketTradeChannel channel = MarketTradeChannel.CityOpenMarket)
    {
        ValidateCount(count);
        goodId = ValidateGood(goodId);
        ArgumentOutOfRangeException.ThrowIfNegative(availableSpiritStones);
        ArgumentOutOfRangeException.ThrowIfNegative(currentMonth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceResultId);
        if ((uint)agentIndex >= (uint)_lastSettlementSourceByAgent.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(agentIndex));
        }
        if (channel != MarketTradeChannel.CityOpenMarket)
        {
            return new MarketContractReceipt(MarketContractStatus.ChannelUnavailable);
        }

        ExpireContracts(currentMonth);
        RefreshOfferForGood(goodId, currentMonth);
        var offer = _offers[goodId];
        if (offer.Id <= 0 || offer.Status != MarketOfferStatus.Active)
        {
            return new MarketContractReceipt(MarketContractStatus.OfferUnavailable);
        }

        var available = _stock[goodId] - _reservedStock[goodId];
        if (available < count)
        {
            return new MarketContractReceipt(
                MarketContractStatus.OutOfStock,
                new MarketOfferId(offer.Id));
        }

        var unitPrice = GetPrice(goodId);
        var total = checked(unitPrice * count);
        if (availableSpiritStones < total)
        {
            return new MarketContractReceipt(
                MarketContractStatus.InsufficientFunds,
                new MarketOfferId(offer.Id),
                TotalSpiritStones: total);
        }
        if (_nextReservationId >= long.MaxValue - 1L
            || _reservations.Count >= EconomyRules.ReservationHistoryCapacity
                && _reservations.All(item => item.Status == MarketReservationStatus.Reserved))
        {
            return new MarketContractReceipt(MarketContractStatus.CapacityExceeded);
        }

        TrimReservationHistory();
        var reservation = new MarketReservationAuthorityState(
            _nextReservationId++,
            offer.Id,
            agentIndex,
            goodId,
            count,
            unitPrice,
            currentMonth,
            checked(currentMonth + EconomyRules.ReservationLifetimeMonths),
            MarketReservationStatus.Reserved,
            sourceResultId);
        _reservations.Add(reservation);
        _reservedStock[goodId] = checked(_reservedStock[goodId] + count);
        offer.Revision++;
        TouchAuthority();
        return new MarketContractReceipt(
            MarketContractStatus.Reserved,
            new MarketOfferId(offer.Id),
            new MarketReservationId(reservation.Id),
            total);
    }

    public MarketSettlementReceipt SettleReservedPurchase(
        int agentIndex,
        MarketReservationId reservationId,
        long sourceResultId,
        long tick,
        long availableSpiritStones,
        QualityValue? minimumQuality = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        ArgumentOutOfRangeException.ThrowIfNegative(availableSpiritStones);
        var reservation = _reservations.FirstOrDefault(item => item.Id == reservationId.Value);
        if (reservation is null
            || reservation.AgentIndex != agentIndex
            || reservation.SourceResultId != sourceResultId
            || reservation.Status != MarketReservationStatus.Reserved)
        {
            return new MarketSettlementReceipt(MarketSettlementStatus.NoDemand);
        }

        var currentMonth = GameCalendar.GetMonthIndex(new SimulationTick(tick));
        if (currentMonth >= reservation.ExpiresMonth)
        {
            ReleaseReservation(reservation, MarketReservationStatus.Expired);
            return new MarketSettlementReceipt(MarketSettlementStatus.NoDemand);
        }
        if (!IsFreshSettlementSource(agentIndex, sourceResultId, tick))
        {
            reservation.Status = MarketReservationStatus.Revalidate;
            return new MarketSettlementReceipt(MarketSettlementStatus.DuplicateSource);
        }

        var definition = EconomyRules.GetMarketGood(reservation.GoodId);
        var qualityGood = IsQualityGood(definition.Kind);
        QualityValue? purchasedQuality = null;
        if (qualityGood)
        {
            if (reservation.Count != 1 || _stock[reservation.GoodId] <= 0)
            {
                reservation.Status = MarketReservationStatus.Revalidate;
                return new MarketSettlementReceipt(MarketSettlementStatus.OutOfStock);
            }
            purchasedQuality = new QualityValue(checked((ushort)Math.Clamp(
                _qualityHundredths[reservation.GoodId] / _stock[reservation.GoodId],
                0L,
                QualityValue.MaximumHundredths)));
            if (minimumQuality is { } minimum && purchasedQuality.Value <= minimum)
            {
                ReleaseReservation(reservation, MarketReservationStatus.Cancelled);
                return new MarketSettlementReceipt(
                    MarketSettlementStatus.NotAnImprovement,
                    reservation.UnitPrice,
                    purchasedQuality);
            }
        }

        var total = checked(reservation.UnitPrice * reservation.Count);
        if (availableSpiritStones < total)
        {
            reservation.Status = MarketReservationStatus.Revalidate;
            return new MarketSettlementReceipt(MarketSettlementStatus.InsufficientFunds, total);
        }
        if (!CanRecordTrade()
            || TreasurySpiritStones > long.MaxValue - total)
        {
            reservation.Status = MarketReservationStatus.Revalidate;
            return new MarketSettlementReceipt(
                MarketSettlementStatus.CapacityExceeded,
                total,
                purchasedQuality);
        }

        CommitSettlementSource(agentIndex, sourceResultId);
        ReleaseReservation(reservation, MarketReservationStatus.Settled);
        if (purchasedQuality is { } quality)
        {
            _qualityHundredths[reservation.GoodId] -= quality.Hundredths;
        }
        RegisterDemand(reservation.GoodId, reservation.Count);
        _stock[reservation.GoodId] -= reservation.Count;
        _purchaseTransactions = SaturatingAdd(_purchaseTransactions, 1);
        TreasurySpiritStones += total;
        Reprice(reservation.GoodId);
        RefreshOfferForGood(reservation.GoodId, currentMonth);
        RecordTrade(
            tick,
            agentIndex,
            sourceResultId,
            MarketTradeKind.Purchase,
            reservation.GoodId,
            reservation.Count,
            reservation.UnitPrice,
            total,
            purchasedQuality);
        return new MarketSettlementReceipt(
            MarketSettlementStatus.Settled,
            total,
            purchasedQuality);
    }

    public bool CancelReservation(MarketReservationId reservationId, int agentIndex)
    {
        var reservation = _reservations.FirstOrDefault(item => item.Id == reservationId.Value);
        if (reservation is null
            || reservation.AgentIndex != agentIndex
            || reservation.Status != MarketReservationStatus.Reserved)
        {
            return false;
        }

        ReleaseReservation(reservation, MarketReservationStatus.Cancelled);
        return true;
    }

    private void InitializeMarketAuthority(long currentMonth)
    {
        _nextOfferId = 1;
        _nextReservationId = 1;
        _lastContractExpiryMonth = currentMonth;
        for (var goodId = 0; goodId < EconomyRules.MarketGoodCount; goodId++)
        {
            RefreshOfferForGood(goodId, currentMonth);
        }
    }

    private void RestoreMarketAuthority(MarketAuthorityCheckpoint checkpoint)
    {
        ValidateMarketAuthorityCheckpoint(checkpoint);
        _nextOfferId = checkpoint.NextOfferId;
        _nextReservationId = checkpoint.NextReservationId;
        _lastContractExpiryMonth = checkpoint.LastExpiryMonth;
        Array.Copy(checkpoint.ReservedStock, _reservedStock, _reservedStock.Length);
        foreach (var persisted in checkpoint.Offers)
        {
            _offers[persisted.GoodId] = new MarketOfferAuthorityState(
                persisted.OfferId,
                persisted.Revision,
                persisted.GoodId,
                persisted.Channel,
                persisted.Side,
                persisted.CreatedMonth,
                persisted.ExpiresMonth,
                persisted.Status);
        }
        foreach (var persisted in checkpoint.Reservations)
        {
            _reservations.Add(new MarketReservationAuthorityState(
                persisted.ReservationId,
                persisted.OfferId,
                persisted.AgentIndex,
                persisted.GoodId,
                persisted.Count,
                persisted.UnitPrice,
                persisted.CreatedMonth,
                persisted.ExpiresMonth,
                persisted.Status,
                persisted.SourceResultId));
        }
    }

    private void RecordImmediateSettlementContract(
        int agentIndex,
        int goodId,
        int count,
        long unitPrice,
        long tick,
        long sourceResultId)
    {
        var currentMonth = GameCalendar.GetMonthIndex(new SimulationTick(tick));
        ExpireContracts(currentMonth);
        RefreshOfferForGood(goodId, currentMonth);
        var offer = _offers[goodId];
        if (offer.Id <= 0)
        {
            throw new InvalidOperationException("정산 가능한 시장 제안이 없습니다.");
        }

        TrimReservationHistory();
        _reservations.Add(new MarketReservationAuthorityState(
            _nextReservationId++,
            offer.Id,
            agentIndex,
            goodId,
            count,
            unitPrice,
            currentMonth,
            currentMonth,
            MarketReservationStatus.Settled,
            sourceResultId));
        offer.Revision++;
    }

    private void AdvanceContractAuthority(long currentMonth)
    {
        ExpireContracts(currentMonth);
        for (var goodId = 0; goodId < EconomyRules.MarketGoodCount; goodId++)
        {
            RefreshOfferForGood(goodId, currentMonth);
        }
    }

    private void ExpireContracts(long currentMonth)
    {
        if (currentMonth < _lastContractExpiryMonth)
        {
            throw new InvalidOperationException("시장 계약 시간을 되돌릴 수 없습니다.");
        }

        var changed = false;
        foreach (var reservation in _reservations)
        {
            if (reservation.Status == MarketReservationStatus.Reserved
                && currentMonth >= reservation.ExpiresMonth)
            {
                _reservedStock[reservation.GoodId] -= reservation.Count;
                reservation.Status = MarketReservationStatus.Expired;
                changed = true;
            }
        }
        foreach (var offer in _offers)
        {
            if (offer is { Status: MarketOfferStatus.Active }
                && currentMonth >= offer.ExpiresMonth)
            {
                offer.Status = MarketOfferStatus.Expired;
                offer.Revision++;
                changed = true;
            }
        }

        _lastContractExpiryMonth = currentMonth;
        if (changed)
        {
            TouchAuthority();
        }
    }

    private void RefreshOfferForGood(int goodId, long currentMonth)
    {
        var offer = _offers[goodId];
        var available = _stock[goodId] - _reservedStock[goodId];
        if (available <= 0)
        {
            if (offer is { Status: MarketOfferStatus.Active })
            {
                offer.Status = MarketOfferStatus.Filled;
                offer.Revision++;
            }
            return;
        }

        if (offer is { Status: MarketOfferStatus.Active }
            && currentMonth < offer.ExpiresMonth)
        {
            return;
        }
        if (_nextOfferId >= long.MaxValue - 1L)
        {
            return;
        }

        _offers[goodId] = new MarketOfferAuthorityState(
            _nextOfferId++,
            1,
            goodId,
            MarketTradeChannel.CityOpenMarket,
            MarketOfferSide.Sell,
            currentMonth,
            checked(currentMonth + EconomyRules.OfferLifetimeMonths),
            MarketOfferStatus.Active);
    }

    private void ReleaseReservation(
        MarketReservationAuthorityState reservation,
        MarketReservationStatus status)
    {
        if (reservation.Status == MarketReservationStatus.Reserved)
        {
            _reservedStock[reservation.GoodId] -= reservation.Count;
        }
        reservation.Status = status;
        var offer = _offers[reservation.GoodId];
        if (offer is not null)
        {
            offer.Revision++;
        }
        TouchAuthority();
    }

    private void TrimReservationHistory()
    {
        while (_reservations.Count >= EconomyRules.ReservationHistoryCapacity)
        {
            var removable = _reservations.FindIndex(item =>
                item.Status != MarketReservationStatus.Reserved);
            if (removable < 0)
            {
                return;
            }
            _reservations.RemoveAt(removable);
        }
    }

    private MarketOfferSnapshot[] CreateOfferSnapshots()
    {
        var result = new List<MarketOfferSnapshot>(EconomyRules.MarketGoodCount);
        for (var goodId = 0; goodId < _offers.Length; goodId++)
        {
            var offer = _offers[goodId];
            if (offer is null || offer.Id <= 0)
            {
                continue;
            }

            result.Add(new MarketOfferSnapshot(
                new MarketOfferId(offer.Id),
                offer.Revision,
                new EconomicOwnerRef(EconomicOwnerKind.City, 1),
                new AssetHolderRef(
                    AssetHolderKind.CityWarehouse,
                    1,
                    WorldMapRules.CityLocationId),
                offer.Channel,
                offer.Side,
                goodId,
                Math.Max(0, _stock[goodId] - _reservedStock[goodId]),
                GetPrice(goodId),
                offer.CreatedMonth,
                offer.ExpiresMonth,
                offer.Status));
        }
        return result.ToArray();
    }

    private MarketReservationSnapshot[] CreateReservationSnapshots() =>
        _reservations
            .TakeLast(50)
            .Select(item => new MarketReservationSnapshot(
                new MarketReservationId(item.Id),
                new MarketOfferId(item.OfferId),
                new EconomicOwnerRef(EconomicOwnerKind.Agent, item.AgentIndex + 1L),
                item.GoodId,
                item.Count,
                item.UnitPrice,
                item.CreatedMonth,
                item.ExpiresMonth,
                item.Status,
                item.SourceResultId))
            .ToArray();

    private MarketAuthorityCheckpoint CreateMarketAuthorityCheckpoint() => new(
        _nextOfferId,
        _nextReservationId,
        _reservedStock.ToArray(),
        _offers
            .Where(item => item is not null && item.Id > 0)
            .Select(item => new MarketOfferAuthorityCheckpoint(
                item.Id,
                item.Revision,
                item.GoodId,
                item.Channel,
                item.Side,
                item.CreatedMonth,
                item.ExpiresMonth,
                item.Status))
            .ToArray(),
        _reservations.Select(item => new MarketReservationAuthorityCheckpoint(
            item.Id,
            item.OfferId,
            item.AgentIndex,
            item.GoodId,
            item.Count,
            item.UnitPrice,
            item.CreatedMonth,
            item.ExpiresMonth,
            item.Status,
            item.SourceResultId)).ToArray(),
        _lastContractExpiryMonth);

    internal static void ValidateMarketAuthorityCheckpoint(
        MarketAuthorityCheckpoint checkpoint,
        int? population = null)
    {
        if (checkpoint.NextOfferId is < 1 or >= long.MaxValue
            || checkpoint.NextReservationId is < 1 or >= long.MaxValue
            || checkpoint.LastExpiryMonth < 0
            || checkpoint.ReservedStock is not { Length: EconomyRules.MarketGoodCount }
            || checkpoint.ReservedStock.Any(value => value < 0)
            || checkpoint.Offers is null
            || checkpoint.Reservations is null
            || checkpoint.Offers.Select(item => item.OfferId).Distinct().Count()
                != checkpoint.Offers.Length
            || checkpoint.Offers.Select(item => item.GoodId).Distinct().Count()
                != checkpoint.Offers.Length
            || checkpoint.Offers.Any(item =>
                item.OfferId <= 0
                || item.OfferId >= checkpoint.NextOfferId
                || item.Revision <= 0
                || item.GoodId < 0
                || item.GoodId >= EconomyRules.MarketGoodCount
                || item.CreatedMonth < 0
                || item.ExpiresMonth < item.CreatedMonth
                || !Enum.IsDefined(item.Channel)
                || !Enum.IsDefined(item.Side)
                || !Enum.IsDefined(item.Status))
            || checkpoint.Reservations.Select(item => item.ReservationId).Distinct().Count()
                != checkpoint.Reservations.Length
            || checkpoint.Reservations.Any(item =>
                item.ReservationId <= 0
                || item.ReservationId >= checkpoint.NextReservationId
                || item.OfferId <= 0
                || item.AgentIndex < 0
                || population is { } count && item.AgentIndex >= count
                || item.GoodId < 0
                || item.GoodId >= EconomyRules.MarketGoodCount
                || item.Count <= 0
                || item.UnitPrice <= 0
                || item.CreatedMonth < 0
                || item.ExpiresMonth < item.CreatedMonth
                || item.SourceResultId <= 0
                || !Enum.IsDefined(item.Status)))
        {
            throw new InvalidDataException("시장 Offer/Reservation 권위 체크포인트가 잘못되었습니다.");
        }

        var activeReserved = new int[EconomyRules.MarketGoodCount];
        foreach (var reservation in checkpoint.Reservations)
        {
            if (reservation.Status == MarketReservationStatus.Reserved)
            {
                activeReserved[reservation.GoodId] = checked(
                    activeReserved[reservation.GoodId] + reservation.Count);
            }
        }
        if (!activeReserved.SequenceEqual(checkpoint.ReservedStock))
        {
            throw new InvalidDataException("시장 예약 수량 합계가 권위 재고와 일치하지 않습니다.");
        }
    }

    private sealed class MarketOfferAuthorityState(
        long id,
        long revision,
        int goodId,
        MarketTradeChannel channel,
        MarketOfferSide side,
        long createdMonth,
        long expiresMonth,
        MarketOfferStatus status)
    {
        public long Id { get; } = id;
        public long Revision { get; set; } = revision;
        public int GoodId { get; } = goodId;
        public MarketTradeChannel Channel { get; } = channel;
        public MarketOfferSide Side { get; } = side;
        public long CreatedMonth { get; } = createdMonth;
        public long ExpiresMonth { get; } = expiresMonth;
        public MarketOfferStatus Status { get; set; } = status;
    }

    private sealed class MarketReservationAuthorityState(
        long id,
        long offerId,
        int agentIndex,
        int goodId,
        int count,
        long unitPrice,
        long createdMonth,
        long expiresMonth,
        MarketReservationStatus status,
        long sourceResultId)
    {
        public long Id { get; } = id;
        public long OfferId { get; } = offerId;
        public int AgentIndex { get; } = agentIndex;
        public int GoodId { get; } = goodId;
        public int Count { get; } = count;
        public long UnitPrice { get; } = unitPrice;
        public long CreatedMonth { get; } = createdMonth;
        public long ExpiresMonth { get; } = expiresMonth;
        public MarketReservationStatus Status { get; set; } = status;
        public long SourceResultId { get; } = sourceResultId;
    }
}
