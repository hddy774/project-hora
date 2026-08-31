using System.Windows;
using System.Windows.Controls;
using Chronicles.Core;
using Chronicles.Simulation;
using static Chronicles.Desktop.PresentationText;

namespace Chronicles.Desktop;

public partial class MarketObservationView : UserControl
{
    private CityMarketSnapshot _snapshot = CityMarketSnapshot.Empty;
    private MarketFilter _filter;
    private int _selectedGoodId;
    private bool _applying;

    public MarketObservationView()
    {
        InitializeComponent();
    }

    public void Apply(CityMarketSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _snapshot = snapshot;
        TreasuryText.Text = $"도시 영석 {KoreanNumberFormatter.Format(snapshot.TreasurySpiritStones)}";
        TotalStockText.Text = KoreanNumberFormatter.Format(snapshot.TotalStock);
        UnmetDemandText.Text = KoreanNumberFormatter.Format(snapshot.UnmetDemand);
        ProductionCountText.Text =
            $"{snapshot.ProductionQueue?.Count ?? 0:N0} / {snapshot.ProductionCapacity:N0}";
        RebuildRows();
    }

    private void RebuildRows()
    {
        var search = SearchBox?.Text.Trim() ?? string.Empty;
        var rows = _snapshot.Goods
            .Where(item => MatchesFilter(item.Kind))
            .Select(item => new MarketListRow(
                item.GoodId,
                FormatMarketGood(item),
                KoreanNumberFormatter.Format(item.CurrentPrice),
                KoreanNumberFormatter.Format(item.Stock),
                FormatFlow(item)))
            .Where(item => search.Length == 0
                || item.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase))
            .ToArray();

        _applying = true;
        try
        {
            MarketGrid.ItemsSource = rows;
            if (rows.Length == 0)
            {
                ClearDetail();
                return;
            }

            var selected = rows.FirstOrDefault(item => item.GoodId == _selectedGoodId) ?? rows[0];
            _selectedGoodId = selected.GoodId;
            MarketGrid.SelectedItem = selected;
            UpdateDetail(FindGood(selected.GoodId));
        }
        finally
        {
            _applying = false;
        }
    }

    private bool MatchesFilter(MarketGoodKind kind) => _filter switch
    {
        MarketFilter.All => true,
        MarketFilter.Material => kind == MarketGoodKind.Material,
        MarketFilter.Pill => kind is MarketGoodKind.GrowthPill or MarketGoodKind.BreakthroughPill,
        MarketFilter.Treasure => kind is MarketGoodKind.DefenseTreasure or MarketGoodKind.AttackTreasure,
        MarketFilter.Book => kind is MarketGoodKind.TechniqueBook or MarketGoodKind.SpellBook,
        _ => false,
    };

    private MarketGoodSnapshot FindGood(int goodId)
    {
        if ((uint)goodId < (uint)_snapshot.Goods.Count
            && _snapshot.Goods[goodId].GoodId == goodId)
        {
            return _snapshot.Goods[goodId];
        }

        return _snapshot.Goods.First(item => item.GoodId == goodId);
    }

    private void UpdateDetail(MarketGoodSnapshot item)
    {
        DetailNameText.Text = FormatMarketGood(item);
        var (type, description, production) = Describe(item);
        DetailTypeText.Text = type;
        DetailDescriptionText.Text = description;
        DetailQualityText.Text = item.AverageQuality is { } quality
            ? $"평균 품질 {QualityRules.Format(quality)} · 품질이 실제 거래 가격에 반영됩니다."
            : "품질을 사용하지 않는 상품 또는 현재 품질 재고 없음";

        DetailPriceText.Text =
            $"현재 {KoreanNumberFormatter.Format(item.CurrentPrice)} 영석 · 기준 {KoreanNumberFormatter.Format(item.CityBasePrice)} · {FormatPriceChange(item.CityBasePrice, item.CurrentPrice)}";
        DetailFlowText.Text =
            $"재고 {KoreanNumberFormatter.Format(item.Stock)} · 누적 공급 {KoreanNumberFormatter.Format(item.Supply)} · 누적 수요 {KoreanNumberFormatter.Format(item.Demand)}";
        var unmet = Math.Max(0L, item.Demand - item.Supply);
        DetailDemandText.Text = unmet > 0
            ? $"미충족 수요 {KoreanNumberFormatter.Format(unmet)} · 공급 확대 필요"
            : item.Supply > item.Demand
                ? $"공급 여유 {KoreanNumberFormatter.Format(item.Supply - item.Demand)}"
                : "공급과 수요가 균형입니다.";

        var activeOrders = _snapshot.ProductionQueue?
            .Where(order => order.GoodId == item.GoodId)
            .ToArray() ?? [];
        DetailProductionStateText.Text = activeOrders.Length > 0
            ? $"현재 {activeOrders.Length:N0}건 제작 중 · " + string.Join(
                ", ",
                activeOrders.Select(order => $"{order.CompletedMonths:N0}/{order.RequiredMonths:N0}개월"))
            : unmet > 0
                ? "수요 있음 · 재료·영석·대기열 조건을 검토 중"
                : "현재 추가 생산 수요 없음";
        DetailProductionText.Text = production;
    }

    private static string FormatFlow(MarketGoodSnapshot item)
    {
        var imbalance = item.Demand - item.Supply;
        return imbalance switch
        {
            > 0 => $"수요 +{KoreanNumberFormatter.Format(imbalance)}",
            < 0 => $"공급 +{KoreanNumberFormatter.Format(-imbalance)}",
            _ => "균형",
        };
    }

    private static (string Type, string Description, string Production) Describe(
        MarketGoodSnapshot item) => item.Kind switch
    {
        MarketGoodKind.Material when item.Material is { } material => DescribeMaterial(material),
        MarketGoodKind.GrowthPill => (
            $"{FormatRealmStage(item.Realm ?? RealmStage.QiRefining)}급 단약 · 성장",
            "현재 대경지에서 수행 Work 축적을 돕습니다. 대경지 돌파 뒤에는 새 등급을 구입해야 합니다.",
            "미충족 수요가 있고 단약 재료와 영석이 모두 있을 때만 도시 생산 대기열에 들어갑니다."),
        MarketGoodKind.BreakthroughPill => (
            $"{FormatRealmStage(item.Realm ?? RealmStage.QiRefining)}급 단약 · 돌파",
            "현재 대경지의 돌파 자원 병목에 사용합니다. 대경지 돌파 뒤에는 새 등급을 구입해야 합니다.",
            "미충족 수요가 있고 단약 재료와 영석이 모두 있을 때만 도시 생산 대기열에 들어갑니다."),
        MarketGoodKind.DefenseTreasure or MarketGoodKind.AttackTreasure => (
            $"{FormatRealmStage(item.Realm ?? RealmStage.QiRefining)}급 {(item.Kind == MarketGoodKind.DefenseTreasure ? "방어" : "공격")} 법보",
            "현재 대경지 전용 법보입니다. 본명법보는 소유자가 직접 제작하며 대경지 돌파와 동기화됩니다.",
            "미충족 수요가 있고 법보 재료와 영석이 모두 있을 때만 도시 생산 대기열에 들어갑니다."),
        MarketGoodKind.TechniqueBook => (
            "서적 · 공법",
            "현재 공법보다 품질이 높을 때만 새 공법으로 교체합니다. 기존 공법의 품질은 변하지 않습니다.",
            $"도시는 수요가 있을 때 영석 {KoreanNumberFormatter.Format(EconomyRules.GetProductionSpiritStoneCost(EconomyRules.GetMarketGood(item.GoodId)))}개로 1권을 제작합니다."),
        MarketGoodKind.SpellBook => (
            "서적 · 법술",
            "현재 법술보다 품질이 높을 때만 새 법술로 교체합니다. 기존 법술의 품질은 변하지 않습니다.",
            $"도시는 수요가 있을 때 영석 {KoreanNumberFormatter.Format(EconomyRules.GetProductionSpiritStoneCost(EconomyRules.GetMarketGood(item.GoodId)))}개로 1권을 제작합니다."),
        _ => ("상품", "상세 정보가 아직 등록되지 않았습니다.", "생산 정보가 없습니다."),
    };

    private static (string Type, string Description, string Production) DescribeMaterial(
        MaterialId material)
    {
        var definition = EconomyRules.GetMaterial(material);
        var (category, usage) = definition.Category switch
        {
            MaterialCategory.Treasure => ("법보 제작 재료", "법보 제작과 도시 법보 생산에 사용됩니다."),
            MaterialCategory.Pill => ("단약 제작 재료", "수행단·돌파단 도시 생산에 사용됩니다."),
            _ => ("재료", "용도가 미확정입니다."),
        };
        return (
            $"재료 · {category}",
            $"{definition.KoreanName}. {usage}",
            "수선자가 수요 임무·요수 사냥으로 획득해 납품하거나 판매하면 도시 실물 재고와 공급이 증가합니다.");
    }

    private void ClearDetail()
    {
        DetailNameText.Text = "조건에 맞는 상품 없음";
        DetailTypeText.Text = string.Empty;
        DetailDescriptionText.Text = "분류 또는 검색어를 변경하세요.";
        DetailQualityText.Text = string.Empty;
        DetailPriceText.Text = string.Empty;
        DetailFlowText.Text = string.Empty;
        DetailDemandText.Text = string.Empty;
        DetailProductionStateText.Text = string.Empty;
        DetailProductionText.Text = string.Empty;
    }

    private void MarketFilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string raw }
            && Enum.TryParse(raw, out MarketFilter filter))
        {
            _filter = filter;
            RebuildRows();
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (IsInitialized)
        {
            RebuildRows();
        }
    }

    private void MarketGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_applying || MarketGrid.SelectedItem is not MarketListRow row)
        {
            return;
        }

        _selectedGoodId = row.GoodId;
        UpdateDetail(FindGood(row.GoodId));
    }

    private enum MarketFilter : byte
    {
        All,
        Material,
        Pill,
        Treasure,
        Book,
    }

    private sealed record MarketListRow(
        int GoodId,
        string Name,
        string CurrentPrice,
        string Stock,
        string Flow);
}
