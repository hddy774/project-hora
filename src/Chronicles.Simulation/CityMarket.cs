using Chronicles.Core;

namespace Chronicles.Simulation;

internal sealed class CityMarket
{
    private readonly int[] _stock;
    private readonly int[] _prices;
    private readonly long[] _supply;
    private readonly long[] _demand;
    private long _treasureQualityHundredths;

    public CityMarket(int population)
    {
        _stock = new int[EconomyRules.MarketGoodCount];
        _prices = new int[EconomyRules.MarketGoodCount];
        _supply = new long[EconomyRules.MarketGoodCount];
        _demand = new long[EconomyRules.MarketGoodCount];
        TreasurySpiritStones = checked(Math.Max(100_000L, population * 500L));

        for (var goodId = 0; goodId < EconomyRules.MarketGoodCount; goodId++)
        {
            _prices[goodId] = EconomyRules.GetMarketGood(goodId).CityBasePrice;
            _stock[goodId] = goodId switch
            {
                < EconomyRules.MaterialCount => 4,
                EconomyRules.GrowthPillGoodId or EconomyRules.BreakthroughPillGoodId => 10,
                EconomyRules.TechniqueBookGoodId or EconomyRules.SpellBookGoodId => 2,
                _ => 0,
            };
        }
    }

    public CityMarket(CityMarketCheckpoint checkpoint)
    {
        TreasurySpiritStones = checkpoint.TreasurySpiritStones;
        _stock = checkpoint.Stock.ToArray();
        _prices = checkpoint.Prices.ToArray();
        _supply = checkpoint.Supply.ToArray();
        _demand = checkpoint.Demand.ToArray();
        _treasureQualityHundredths = checkpoint.TreasureQualityHundredths;
    }

    public long TreasurySpiritStones { get; private set; }

    public int GetPrice(int goodId) => _prices[ValidateGood(goodId)];

    public int GetStock(int goodId) => _stock[ValidateGood(goodId)];

    public bool TryBuy(int goodId, int count, long availableSpiritStones, out long cost)
    {
        ValidateCount(count);
        goodId = ValidateGood(goodId);
        cost = checked((long)_prices[goodId] * count);
        if (_stock[goodId] < count || availableSpiritStones < cost)
        {
            return false;
        }

        _stock[goodId] -= count;
        _demand[goodId] = checked(_demand[goodId] + count);
        TreasurySpiritStones = checked(TreasurySpiritStones + cost);
        Reprice(goodId);
        return true;
    }

    public bool TrySell(int goodId, int count, out long proceeds)
    {
        ValidateCount(count);
        goodId = ValidateGood(goodId);
        proceeds = checked((long)_prices[goodId] * count);
        if (TreasurySpiritStones < proceeds)
        {
            return false;
        }

        TreasurySpiritStones -= proceeds;
        AddSupply(goodId, count);
        return true;
    }

    public long AcceptMissionDelivery(MaterialId material, int count, int realmBonus)
    {
        ValidateCount(count);
        var goodId = (int)material;
        var reward = checked(((long)_prices[goodId] * count) + realmBonus);
        reward = Math.Min(reward, TreasurySpiritStones);
        TreasurySpiritStones -= reward;
        AddSupply(goodId, count);
        return reward;
    }

    public bool TryConsumeForProduction(int goodId, int count)
    {
        ValidateCount(count);
        goodId = ValidateGood(goodId);
        if (_stock[goodId] < count)
        {
            return false;
        }

        _stock[goodId] -= count;
        _demand[goodId] = checked(_demand[goodId] + count);
        Reprice(goodId);
        return true;
    }

    public void AddCityProduction(int goodId, int count)
    {
        ValidateCount(count);
        AddSupply(ValidateGood(goodId), count);
    }

    public bool TrySpendTreasury(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        if (TreasurySpiritStones < amount)
        {
            return false;
        }

        TreasurySpiritStones -= amount;
        return true;
    }

    public void AddTreasureProduction(QualityValue quality)
    {
        _treasureQualityHundredths = checked(_treasureQualityHundredths + quality.Hundredths);
        AddSupply(EconomyRules.TreasureGoodId, 1);
    }

    public bool TryBuyTreasure(long availableSpiritStones, out long cost, out QualityValue quality)
    {
        var stock = _stock[EconomyRules.TreasureGoodId];
        cost = 0;
        if (stock <= 0
            || !TryBuy(EconomyRules.TreasureGoodId, 1, availableSpiritStones, out cost))
        {
            quality = default;
            return false;
        }

        var raw = checked((ushort)Math.Clamp(_treasureQualityHundredths / stock, 0, 10_000));
        _treasureQualityHundredths -= raw;
        quality = new QualityValue(raw);
        return true;
    }

    public bool TrySellTreasure(QualityValue quality, out long proceeds)
    {
        if (!TrySell(EconomyRules.TreasureGoodId, 1, out proceeds))
        {
            return false;
        }

        _treasureQualityHundredths = checked(_treasureQualityHundredths + quality.Hundredths);
        return true;
    }

    public CityMarketSnapshot CreateSnapshot()
    {
        var goods = new MarketGoodSnapshot[EconomyRules.MarketGoodCount];
        for (var goodId = 0; goodId < goods.Length; goodId++)
        {
            var definition = EconomyRules.GetMarketGood(goodId);
            goods[goodId] = new MarketGoodSnapshot(
                goodId,
                definition.Kind,
                definition.Material,
                definition.CityBasePrice,
                _prices[goodId],
                _stock[goodId],
                _supply[goodId],
                _demand[goodId]);
        }

        return new CityMarketSnapshot(TreasurySpiritStones, goods);
    }

    public CityMarketCheckpoint CreateCheckpoint() => new(
        TreasurySpiritStones,
        _stock.ToArray(),
        _prices.ToArray(),
        _supply.ToArray(),
        _demand.ToArray(),
        _treasureQualityHundredths);

    private void AddSupply(int goodId, int count)
    {
        _stock[goodId] = checked(_stock[goodId] + count);
        _supply[goodId] = checked(_supply[goodId] + count);
        Reprice(goodId);
    }

    private void Reprice(int goodId)
    {
        var definition = EconomyRules.GetMarketGood(goodId);
        var targetStock = definition.Kind == MarketGoodKind.Material ? 20 : 10;
        var tradeImbalance = Math.Clamp(_demand[goodId] - _supply[goodId], -100L, 100L);
        var stockPressure = Math.Clamp(targetStock - _stock[goodId], -50, 100);
        var percentage = Math.Clamp(100L + (tradeImbalance * 2) + (stockPressure * 3L), 25L, 400L);
        _prices[goodId] = checked((int)Math.Max(1, definition.CityBasePrice * percentage / 100));
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
}
