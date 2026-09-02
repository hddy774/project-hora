using Chronicles.Core;
using Chronicles.Simulation;

namespace Chronicles.Desktop;

internal static class ObservationStatistics
{
    public static StatisticsMetric[] CreateWorld(WorldSnapshot snapshot)
    {
        var statistics = snapshot.Statistics;
        return
        [
            new("현재 인구", snapshot.Population,
                KoreanNumberFormatter.Format(snapshot.Population), "살아 있는 인물"),
            new("진행 중 전투", snapshot.ActiveCombats,
                KoreanNumberFormatter.Format(snapshot.ActiveCombats), "현재 장기 전투 포함"),
            new("완료 전투", snapshot.TotalCombatsCompleted,
                KoreanNumberFormatter.Format(snapshot.TotalCombatsCompleted), "세계 누계"),
            new("경지 돌파", snapshot.TotalBreakthroughs,
                KoreanNumberFormatter.Format(snapshot.TotalBreakthroughs), "소·대경지 누계"),
            new("사망", statistics.TotalDeaths,
                KoreanNumberFormatter.Format(statistics.TotalDeaths), "세계 누계"),
            new("윤회", statistics.TotalReincarnations,
                KoreanNumberFormatter.Format(statistics.TotalReincarnations), "새 생으로 이어진 횟수"),
            new("임무 완료", statistics.TotalMissionsCompleted,
                KoreanNumberFormatter.Format(statistics.TotalMissionsCompleted), "도시 임무 누계"),
            new("보유 영석", statistics.TotalSpiritStones,
                KoreanNumberFormatter.Format(statistics.TotalSpiritStones), "개인·조직 권위 합계"),
        ];
    }

    public static StatisticsMetric[] CreatePeople(WorldSnapshot snapshot)
    {
        var mortalPopulation = snapshot.Statistics.RealmDistribution
            .FirstOrDefault(item => item.Realm is null)
            .Count;
        var cultivatorPopulation = Math.Max(0, snapshot.Population - mortalPopulation);
        return
        [
            new("전체 인구", snapshot.Population,
                KoreanNumberFormatter.Format(snapshot.Population), "현재 생존 인물"),
            new("범인", mortalPopulation,
                KoreanNumberFormatter.Format(mortalPopulation), "연기경 입문 전"),
            new("수선자", cultivatorPopulation,
                KoreanNumberFormatter.Format(cultivatorPopulation), "연기경 이상"),
            new("누적 사망", snapshot.Statistics.TotalDeaths,
                KoreanNumberFormatter.Format(snapshot.Statistics.TotalDeaths), "육신 사망"),
            new("누적 윤회", snapshot.Statistics.TotalReincarnations,
                KoreanNumberFormatter.Format(snapshot.Statistics.TotalReincarnations), "새 Life 생성"),
            new("전투 승리", snapshot.Statistics.TotalWins,
                KoreanNumberFormatter.Format(snapshot.Statistics.TotalWins), "전체 인물 합계"),
            new("임무 완료", snapshot.Statistics.TotalMissionsCompleted,
                KoreanNumberFormatter.Format(snapshot.Statistics.TotalMissionsCompleted), "전체 인물 합계"),
            new("개인·조직 영석", snapshot.Statistics.TotalSpiritStones,
                KoreanNumberFormatter.Format(snapshot.Statistics.TotalSpiritStones), "권위 자산 합계"),
        ];
    }

    public static StatisticsMetric[] CreateMarket(CityMarketSnapshot market)
    {
        var activeOffers = market.Offers?.Count(item => item.Status == MarketOfferStatus.Active) ?? 0;
        var activeReservations = market.Reservations?.Count(
            item => item.Status == MarketReservationStatus.Reserved) ?? 0;
        var transactions = SaturatingAdd(market.PurchaseTransactions, market.SaleTransactions);
        return
        [
            new("도시 금고", market.TreasurySpiritStones,
                KoreanNumberFormatter.Format(market.TreasurySpiritStones), "영석"),
            new("최근 공급", market.RecentSupply,
                KoreanNumberFormatter.Format(market.RecentSupply), "최근 12개월"),
            new("최근 수요", market.RecentDemand,
                KoreanNumberFormatter.Format(market.RecentDemand), "최근 12개월"),
            new("미충족 수요", market.UnmetDemand,
                KoreanNumberFormatter.Format(market.UnmetDemand), "생산·임무 필요량"),
            new("전체 재고", market.TotalStock,
                KoreanNumberFormatter.Format(market.TotalStock),
                $"재료 {market.MaterialStock:N0} · 가공품 {market.ProcessedStock:N0}"),
            new("시장 거래", transactions,
                KoreanNumberFormatter.Format(transactions),
                $"구매 {market.PurchaseTransactions:N0} · 판매 {market.SaleTransactions:N0}"),
            new("활성 제안", activeOffers,
                KoreanNumberFormatter.Format(activeOffers), "현재 판매 가능 Offer"),
            new("활성 예약", activeReservations,
                KoreanNumberFormatter.Format(activeReservations), "정산 전 Reservation"),
            new("제작 중", market.ProductionQueue?.Count ?? 0,
                KoreanNumberFormatter.Format(market.ProductionQueue?.Count ?? 0),
                $"동시 생산 상한 {market.ProductionCapacity:N0}"),
        ];
    }

    public static long CreateRevision(IEnumerable<StatisticsMetric> metrics)
    {
        var revision = 1_469_598_103_934_665_603L;
        foreach (var metric in metrics)
        {
            revision = unchecked((revision ^ metric.Value) * 1_099_511_628_211L);
        }

        return revision & long.MaxValue;
    }

    private static long SaturatingAdd(long left, long right) =>
        left > long.MaxValue - right ? long.MaxValue : left + right;
}
