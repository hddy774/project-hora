using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Chronicles.Core;
using Chronicles.Simulation;
using static Chronicles.Presentation.PresentationText;

namespace Chronicles.Desktop;

public partial class MapObservationView : UserControl
{
    private WorldMapSnapshot _snapshot = WorldMapSnapshot.Empty;
    private readonly Brush _accentBrush;
    private readonly Brush _borderBrush;
    private readonly Brush _cityBrush;
    private readonly Brush _forestBrush;
    private readonly Brush _plainsBrush;
    private readonly Brush _mountainBrush;
    private readonly Brush _fallbackBrush;
    private AgentId? _strongestAgent;
    private bool _applying;
    private byte _selectedGeographyLayer = 1;
    private ushort _selectedProvinceId = WorldGeographyRules.OcheonProvinceId;
    private ushort _selectedPointId = 44;
    private bool _statisticsMode;
    private int _projectedGeographyRevision = -1;
    private byte _projectedGeographyLayer;
    private ushort _projectedGeographyPointId = ushort.MaxValue;

    public MapObservationView()
    {
        InitializeComponent();
        _accentBrush = (Brush)FindResource("AccentBrush");
        _borderBrush = (Brush)FindResource("BorderBrush");
        _cityBrush = (Brush)FindResource("CityBiomeBrush");
        _forestBrush = (Brush)FindResource("ForestBiomeBrush");
        _plainsBrush = (Brush)FindResource("PlainsBiomeBrush");
        _mountainBrush = (Brush)FindResource("MountainBiomeBrush");
        _fallbackBrush = (Brush)FindResource("PanelAltBrush");
    }

    public event EventHandler? ObservationRequested;
    public event EventHandler<MapAgentSelectedEventArgs>? AgentSelected;

    // Runtime detailed-agent paging still uses the existing Production location ID internally.
    // The visible selection authority is always the stable continent/province/point node.
    public ushort SelectedLocationId { get; private set; } = WorldMapRules.CityLocationId;
    public int AgentOffset { get; private set; }
    public bool ShouldLoadAgents =>
        !_statisticsMode
        && PointDetailTabControl.SelectedIndex == 2
        && TryGetSelectedProductionRegion(out _);

    public void Apply(WorldMapSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _snapshot = snapshot;
        ApplyVisibleMapMode();
    }

    private void ApplyVisibleMapMode()
    {
        ProvinceMapPanel.Visibility = _statisticsMode ? Visibility.Collapsed : Visibility.Visible;
        PointMapPanel.Visibility = _statisticsMode ? Visibility.Collapsed : Visibility.Visible;
        PointDetailsPanel.Visibility = _statisticsMode ? Visibility.Collapsed : Visibility.Visible;
        MapStatisticsPanel.Visibility = _statisticsMode ? Visibility.Visible : Visibility.Collapsed;
        if (_statisticsMode)
        {
            ApplyMapStatistics();
            return;
        }

        ApplyGeography();
    }

    private void ApplyMapStatistics()
    {
        var geography = _snapshot.Geography ?? WorldGeographyBlueprint.Empty;
        var points = GetCurrentPointList(geography);
        var locations = _snapshot.Locations
            .Where(location => location.Atlas.HeavenlyLayer == _selectedGeographyLayer)
            .ToArray();
        var population = locations.Sum(location => (long)location.Population);
        var monsterPopulation = locations.Sum(location =>
            location.Monsters.Sum(monster => (long)monster.Population));
        var cultivation = locations.Sum(location => (long)location.Activities.Cultivation);
        var combat = locations.Sum(location => (long)location.Activities.Combat);
        var economy = locations.Sum(location => (long)location.Activities.Economy);
        var settlements = points.Select(point => point.Settlements).ToArray();
        var veinPoints = points.Count(point => point.Resources.HasSpiritVein);
        var minePoints = points.Count(point => point.Resources.Mine != MineResourceKind.None);
        var settlementCount = settlements.Sum(item =>
            (long)item.LargeCityCount + item.SmallCityCount + item.VillageCount
            + item.SectCount + item.ClanCount);
        var revision = unchecked(
            ((long)geography.Revision << 48)
            ^ ((long)_selectedGeographyLayer << 40)
            ^ ((long)_snapshot.TotalMonsterHunts << 16)
            ^ (uint)_snapshot.TotalMonsterVictories);
        MapStatisticsPanel.Apply(
            $"제{_selectedGeographyLayer:N0}층천 지도 통계",
            "오천주 10×10 지리 기준선과 현재 연결된 Production 지역만 집계합니다. 미연결 지점의 현재 활동은 0으로 추정하지 않습니다.",
            [
                new("현재 인구", population, KoreanNumberFormatter.Format(population),
                    $"권위 연결 지역 {locations.Length:N0}곳"),
                new("요수 개체", monsterPopulation, KoreanNumberFormatter.Format(monsterPopulation),
                    "현재 연결 서식지 합계"),
                new("수행 중", cultivation, KoreanNumberFormatter.Format(cultivation),
                    "현재 지역 활동"),
                new("전투·사냥", combat, KoreanNumberFormatter.Format(combat),
                    "현재 지역 활동"),
                new("경제 활동", economy, KoreanNumberFormatter.Format(economy),
                    "현재 지역 활동"),
                new("정착지", settlementCount, KoreanNumberFormatter.Format(settlementCount),
                    "대·소도시, 촌, 종문, 가문 기준선"),
                new("영맥 지점", veinPoints, KoreanNumberFormatter.Format(veinPoints),
                    $"전체 지점 {points.Count:N0}"),
                new("광산 지점", minePoints, KoreanNumberFormatter.Format(minePoints),
                    "영석·법보 재료·혼합 광산"),
                new("누적 사냥", _snapshot.TotalMonsterHunts,
                    KoreanNumberFormatter.Format(_snapshot.TotalMonsterHunts),
                    $"승리 {_snapshot.TotalMonsterVictories:N0} · 세계 누계"),
            ],
            revision);
    }

    private void ApplyGeography()
    {
        var geography = _snapshot.Geography ?? WorldGeographyBlueprint.Empty;
        if (geography.Revision == 0 || geography.Provinces.Count == 0)
        {
            GeographySummaryText.Text = "DataUnavailable · 지리 기준선을 불러오지 못했습니다.";
            ProvinceTileList.ItemsSource = Array.Empty<ProvinceTileRow>();
            PointTileList.ItemsSource = Array.Empty<PointTileRow>();
            ApplyUnavailablePoint();
            return;
        }

        var activeProvince = geography.Provinces.FirstOrDefault(
            province => province.Id == geography.ActiveProvinceId);
        if (!activeProvince.HasPointDetail)
        {
            GeographySummaryText.Text = "DataUnavailable · 상세 구현된 주(권역)가 없습니다.";
            ProvinceTileList.ItemsSource = Array.Empty<ProvinceTileRow>();
            PointTileList.ItemsSource = Array.Empty<PointTileRow>();
            ApplyUnavailablePoint();
            return;
        }

        GeographySummaryText.Text =
            $"{geography.Continent.KoreanName} · 주 {geography.Provinces.Count:N0}칸 · " +
            $"주 간 기준 거리 {KoreanNumberFormatter.Format(geography.Continent.ProvinceTileDistanceLi)}리 · " +
            $"현재 상세 권역 {activeProvince.KoreanName} · 층천별 지점 {WorldGeographyRules.PointsPerProvince:N0}칸";

        if (_projectedGeographyRevision != geography.Revision)
        {
            ProvinceTileList.ItemsSource = geography.Provinces.Select(province =>
                new ProvinceTileRow(
                    province.Id,
                    province.HasPointDetail ? "오천" : $"{province.X + 1},{province.Y + 1}",
                    province.HasPointDetail
                        ? $"{province.KoreanName} · 지점 상세 사용 가능"
                        : $"{province.KoreanName} · DataUnavailable · 지점 상세 미구현",
                    province.HasPointDetail,
                    province.HasPointDetail ? _cityBrush : _fallbackBrush,
                    province.Id == _selectedProvinceId ? _accentBrush : _borderBrush))
                .ToArray();
        }

        var points = GetCurrentPointList(geography);
        if (_selectedPointId >= points.Count)
        {
            _selectedPointId = 44;
            AgentOffset = 0;
        }

        if (_projectedGeographyRevision != geography.Revision
            || _projectedGeographyLayer != _selectedGeographyLayer
            || _projectedGeographyPointId != _selectedPointId)
        {
            PointGridTitleText.Text =
                $"{activeProvince.KoreanName} · 제{_selectedGeographyLayer:N0}층천 지점 10×10";
            PointTileList.ItemsSource = points.Select(point =>
            {
                var activityRegion = FindProductionRegion(point);
                return new PointTileRow(
                    point.PhysicalPointId,
                    $"{point.X + 1},{point.Y + 1}",
                    FormatPointMarker(point, activityRegion is not null),
                    CreatePointToolTip(point, activityRegion),
                    GetTerrainBrush(point.Terrain),
                    point.PhysicalPointId == _selectedPointId ? _accentBrush : _borderBrush);
            }).ToArray();
        }

        if (!TryGetSelectedPoint(out var selected))
        {
            ApplyUnavailablePoint();
            return;
        }

        var region = FindProductionRegion(selected);
        if (region is not null)
        {
            SelectedLocationId = region.Id;
        }
        ApplyPointDetails(selected, region);
        _projectedGeographyRevision = geography.Revision;
        _projectedGeographyLayer = _selectedGeographyLayer;
        _projectedGeographyPointId = _selectedPointId;
    }

    private IReadOnlyList<PointHeavenViewDefinition> GetCurrentPointList(
        WorldGeographyBlueprint geography) => _selectedGeographyLayer == 1
            ? geography.FirstHeavenPoints
            : geography.SecondHeavenPoints;

    private bool TryGetSelectedPoint(out PointHeavenViewDefinition point)
    {
        var geography = _snapshot.Geography ?? WorldGeographyBlueprint.Empty;
        var points = GetCurrentPointList(geography);
        if (_selectedPointId >= points.Count)
        {
            point = default;
            return false;
        }

        point = points[_selectedPointId];
        return point.PhysicalPointId == _selectedPointId
            && point.HeavenlyLayer == _selectedGeographyLayer;
    }

    private MapLocationSnapshot? FindProductionRegion(PointHeavenViewDefinition point)
    {
        if (!WorldGeographyRules.TryGetProductionRegionCoordinates(
                point.PhysicalPointId,
                out var regionX,
                out var regionY))
        {
            return null;
        }

        return _snapshot.Locations.FirstOrDefault(region =>
            region.Atlas.HeavenlyLayer == point.HeavenlyLayer
            && region.X == regionX
            && region.Y == regionY);
    }

    private bool TryGetSelectedProductionRegion(out MapLocationSnapshot region)
    {
        if (TryGetSelectedPoint(out var point)
            && FindProductionRegion(point) is { } mapped)
        {
            region = mapped;
            return true;
        }

        region = null!;
        return false;
    }

    private void ApplyPointDetails(
        PointHeavenViewDefinition point,
        MapLocationSnapshot? region)
    {
        PointDetailHeaderText.Text = point.KoreanName;
        PointStableIdText.Text =
            $"{(_snapshot.Geography?.Continent.KoreanName ?? "하천대륙")} › 오천주 › {point.NodeId}";
        PointAuthorityStatusText.Text = region is null
            ? "지역 활동 DataUnavailable"
            : $"현재 활동 권위 연결 · {region.Name}";

        switch (PointDetailTabControl.SelectedIndex)
        {
            case 0:
                ApplyPointOverview(point);
                break;
            case 1:
                ApplyRegionActivity(region);
                break;
            case 2:
                ApplyAgentPage(region);
                break;
            case 3:
                ApplyMonsterEcology(region, point);
                break;
            case 4:
                ApplySettlement(point, region);
                break;
            case 5:
                ApplyResources(point, region);
                break;
        }
    }

    private void ApplyPointOverview(PointHeavenViewDefinition point)
    {
        PointCoordinateText.Text =
            $"{(_selectedGeographyLayer == 1 ? "제1층천 · 범인계" : "제2층천 · 수선계")} · " +
            $"좌표 {point.X + 1},{point.Y + 1}";
        PointTerrainSummaryText.Text =
            $"{FormatTerrain(point.Terrain)} · {FormatSettlementSummary(point.Settlements)}";
        PointDescriptionText.Text = point.Description;
    }

    private void ApplyRegionActivity(MapLocationSnapshot? region)
    {
        if (region is null)
        {
            PointRegionPopulationText.Text = "DataUnavailable";
            PointRegionHighestRealmText.Text = "DataUnavailable";
            PointRegionAveragePowerText.Text = "DataUnavailable";
            PointRegionMaximumPowerText.Text = "DataUnavailable";
            PointRegionActivityText.Text =
                "DataUnavailable · 이 지점에는 현재 지역 활동 권위가 연결되지 않았습니다.";
            _strongestAgent = null;
            StrongestAgentButton.IsEnabled = false;
            StrongestAgentButton.Content = "지역 최강 · DataUnavailable";
            return;
        }

        PointRegionPopulationText.Text = $"{KoreanNumberFormatter.Format(region.Population)}명";
        PointRegionHighestRealmText.Text = !region.HasCultivator
            ? "범인"
            : FormatRealm(region.HighestRealm, region.HighestRealmLayer);
        PointRegionAveragePowerText.Text = KoreanNumberFormatter.Format(region.AverageCombatPower);
        PointRegionMaximumPowerText.Text = KoreanNumberFormatter.Format(region.MaximumCombatPower);
        PointRegionActivityText.Text = FormatActivitySummary(region.Activities, region.Population);

        _strongestAgent = region.StrongestAgent?.AgentId;
        StrongestAgentButton.IsEnabled = _strongestAgent is not null;
        StrongestAgentButton.Content = region.StrongestAgent is { } strongest
            ? $"지역 최강 · #{strongest.CombatRank:N0} {strongest.Name} · {KoreanNumberFormatter.Format(strongest.CombatPower)}"
            : "지역 최강 · 인물 없음";
    }

    private void ApplyAgentPage(MapLocationSnapshot? region)
    {
        if (region is null)
        {
            MapAgentGrid.ItemsSource = Array.Empty<MapAgentRow>();
            MapAgentGrid.Visibility = Visibility.Collapsed;
            AgentDataUnavailableText.Visibility = Visibility.Visible;
            AgentPageText.Text = "DataUnavailable";
            PreviousAgentPageButton.IsEnabled = false;
            NextAgentPageButton.IsEnabled = false;
            return;
        }

        MapAgentGrid.Visibility = Visibility.Visible;
        AgentDataUnavailableText.Visibility = Visibility.Collapsed;
        if (_snapshot.DetailedLocationId != region.Id)
        {
            MapAgentGrid.ItemsSource = Array.Empty<MapAgentRow>();
            AgentPageText.Text = "선택 지점 수선자를 불러오는 중";
            PreviousAgentPageButton.IsEnabled = false;
            NextAgentPageButton.IsEnabled = false;
            return;
        }

        _applying = true;
        try
        {
            MapAgentGrid.ItemsSource = region.Agents.Select(agent => new MapAgentRow(
                agent.AgentId,
                agent.CombatRank.ToString("N0", CultureInfo.CurrentCulture),
                agent.Name,
                agent.HasEnteredCultivation
                    ? FormatRealm(agent.Realm, agent.RealmLayer)
                    : "범인",
                KoreanNumberFormatter.Format(agent.CombatPower))).ToArray();
        }
        finally
        {
            _applying = false;
        }

        var first = region.Population == 0 ? 0 : region.AgentPageOffset + 1;
        var last = Math.Min(region.Population, region.AgentPageOffset + region.Agents.Count);
        var maximumOffset = region.Population == 0
            ? 0
            : ((region.Population - 1) / WorldMapRules.DetailedAgentPageSize)
                * WorldMapRules.DetailedAgentPageSize;
        if (ShouldLoadAgents && region.AgentPageOffset > maximumOffset)
        {
            AgentOffset = maximumOffset;
            AgentPageText.Text = "인구 변동에 맞춰 페이지를 조정하는 중";
            PreviousAgentPageButton.IsEnabled = false;
            NextAgentPageButton.IsEnabled = false;
            _ = Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => ObservationRequested?.Invoke(this, EventArgs.Empty)));
            return;
        }

        AgentPageText.Text = $"{first:N0}~{last:N0} / {region.Population:N0}명";
        PreviousAgentPageButton.IsEnabled = region.AgentPageOffset > 0;
        NextAgentPageButton.IsEnabled = last < region.Population;
    }

    private void ApplyMonsterEcology(
        MapLocationSnapshot? region,
        PointHeavenViewDefinition point)
    {
        if (region is null)
        {
            MonsterGrid.ItemsSource = Array.Empty<MonsterRow>();
            MonsterGrid.Visibility = Visibility.Collapsed;
            MonsterDataStatusText.Visibility = Visibility.Visible;
            MonsterDataStatusText.Text =
                $"DataUnavailable · 현재 요수 권위 미연결 · 지리 기준선 추정 " +
                $"{KoreanNumberFormatter.Format(point.Resources.EstimatedBeastPopulation)}마리";
            return;
        }

        var monsters = region.Monsters.Select(monster => new MonsterRow(
            monster.Species.KoreanName,
            KoreanNumberFormatter.Format(monster.Population),
            KoreanNumberFormatter.Format(monster.Species.BaseDanger),
            FormatMonsterTrait(monster.Species.Trait),
            EconomyRules.GetMaterial(monster.Species.DropMaterial).KoreanName)).ToArray();
        MonsterDataStatusText.Visibility = monsters.Length == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        MonsterDataStatusText.Text = monsters.Length == 0
            ? "현재 요수 서식 권위 · 확인된 개체 없음"
            : string.Empty;
        MonsterGrid.Visibility = monsters.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        MonsterGrid.ItemsSource = monsters;
    }

    private void ApplySettlement(
        PointHeavenViewDefinition point,
        MapLocationSnapshot? region)
    {
        PointSocietyText.Text = point.Settlements.Society switch
        {
            PointSocietyKind.Mortal => string.IsNullOrEmpty(point.Settlements.PolityName)
                ? "범인 사회"
                : $"범인 · {point.Settlements.PolityName}",
            PointSocietyKind.Cultivator => string.IsNullOrEmpty(point.Settlements.PolityName)
                ? "수선 사회"
                : $"수선 · {point.Settlements.PolityName}",
            _ => "정착지 없음",
        };
        PointLargeCityText.Text = $"{point.Settlements.LargeCityCount:N0}개";
        PointSmallCityText.Text = $"{point.Settlements.SmallCityCount:N0}개";
        PointVillageText.Text = $"{point.Settlements.VillageCount:N0}개";
        PointSectText.Text = $"{point.Settlements.SectCount:N0}개";
        PointClanText.Text = $"{point.Settlements.ClanCount:N0}개";
        PointSettlementAuthorityText.Text = region is { Biome: BiomeType.City }
            ? $"현재 도시 활동 권위 · {region.Name} · 개별 종문·가문·촌 목록 DataUnavailable"
            : "지리 생성 기준선 · 개별 도시·종문·가문·촌의 현재 목록 DataUnavailable";
    }

    private void ApplyResources(
        PointHeavenViewDefinition point,
        MapLocationSnapshot? region)
    {
        PointTerrainText.Text = FormatTerrain(point.Terrain);
        PointVeinText.Text = point.Resources.HasSpiritVein ? "자연 영맥 있음" : "확인된 영맥 없음";
        PointMineText.Text = point.Resources.Mine switch
        {
            MineResourceKind.SpiritStone => "영석 광산",
            MineResourceKind.TreasureMaterial => "법보 재료 광산",
            MineResourceKind.Mixed => "혼합 광산",
            _ => "확인된 광산 없음",
        };
        PointBeastText.Text =
            $"추정 {KoreanNumberFormatter.Format(point.Resources.EstimatedBeastPopulation)}마리 · " +
            $"다양성 {point.Resources.LivingSpeciesDiversity:N0}";
        PointDistanceText.Text =
            $"{KoreanNumberFormatter.Format(point.Resources.OrthogonalTravelDistanceLi)}리";
        PointProductionVeinText.Text = region is null
            ? "현재 영맥 Source·잔여 공급·월 생산력 DataUnavailable"
            : $"현재 영맥 권위 · Source {region.Atlas.SpiritVeinSourceCount:N0}개 · " +
              $"잔여 {KoreanNumberFormatter.Format(region.Atlas.RemainingSpiritVeinSupply)} · " +
              $"월 생산력 {KoreanNumberFormatter.Format(region.Atlas.MonthlySpiritVeinYield)}";
    }

    private void ApplyUnavailablePoint()
    {
        PointDetailHeaderText.Text = "지점 DataUnavailable";
        PointStableIdText.Text = string.Empty;
        PointAuthorityStatusText.Text = "DataUnavailable";
        PointCoordinateText.Text = "DataUnavailable";
        PointTerrainSummaryText.Text = "DataUnavailable";
        PointDescriptionText.Text = "선택한 지점의 지리 기준선을 사용할 수 없습니다.";
        ApplyRegionActivity(null);
        ApplyAgentPage(null);
        MonsterGrid.ItemsSource = Array.Empty<MonsterRow>();
        MonsterGrid.Visibility = Visibility.Collapsed;
        MonsterDataStatusText.Visibility = Visibility.Visible;
        MonsterDataStatusText.Text = "DataUnavailable · 현재 요수 권위를 사용할 수 없습니다.";
        PointSocietyText.Text = "DataUnavailable";
        PointLargeCityText.Text = "DataUnavailable";
        PointSmallCityText.Text = "DataUnavailable";
        PointVillageText.Text = "DataUnavailable";
        PointSectText.Text = "DataUnavailable";
        PointClanText.Text = "DataUnavailable";
        PointSettlementAuthorityText.Text =
            "DataUnavailable · 도시·종문·가문·촌의 지리 기준선을 사용할 수 없습니다.";
        PointTerrainText.Text = "DataUnavailable";
        PointVeinText.Text = "DataUnavailable";
        PointMineText.Text = "DataUnavailable";
        PointBeastText.Text = "DataUnavailable";
        PointDistanceText.Text = "DataUnavailable";
        PointProductionVeinText.Text =
            "DataUnavailable · 현재 영맥 Source·잔여 공급·월 생산력을 사용할 수 없습니다.";
    }

    private static string FormatActivitySummary(
        MapRegionActivitySnapshot activities,
        int population)
    {
        if (population == 0)
        {
            return "현재 지점에 수선자가 없습니다.";
        }

        static string Item(string label, int count, int total) =>
            $"{label} {KoreanNumberFormatter.Format(count)} ({count * 100m / total:0.#}%)";

        return string.Join(
            " · ",
            Item("수행", activities.Cultivation, population),
            Item("전투·사냥", activities.Combat, population),
            Item("경제", activities.Economy, population),
            Item("이동", activities.Traveling, population),
            Item("회복", activities.Recovering, population),
            Item("윤회", activities.Reincarnating, population));
    }

    private static string FormatMonsterTrait(GeneratedMonsterTrait trait) => trait switch
    {
        GeneratedMonsterTrait.Swift => "신속",
        GeneratedMonsterTrait.Armored => "갑주",
        GeneratedMonsterTrait.Venomous => "맹독",
        GeneratedMonsterTrait.Regenerating => "재생",
        GeneratedMonsterTrait.Piercing => "관통",
        GeneratedMonsterTrait.SpiritDraining => "흡령",
        _ => trait.ToString(),
    };

    private Brush GetTerrainBrush(NaturalTerrainType terrain) => terrain switch
    {
        NaturalTerrainType.Forest => _forestBrush,
        NaturalTerrainType.Plains => _plainsBrush,
        NaturalTerrainType.Mountains => _mountainBrush,
        NaturalTerrainType.River => _cityBrush,
        _ => _fallbackBrush,
    };

    private static string FormatTerrain(NaturalTerrainType terrain) => terrain switch
    {
        NaturalTerrainType.Forest => "숲",
        NaturalTerrainType.Plains => "평야",
        NaturalTerrainType.Mountains => "산맥",
        NaturalTerrainType.River => "하천",
        _ => "미확인",
    };

    private static string FormatPointMarker(
        PointHeavenViewDefinition point,
        bool hasActivityAuthority)
    {
        var marker = point.Settlements.Stronghold switch
        {
            PointStrongholdKind.City => "성",
            PointStrongholdKind.Sect => "종",
            PointStrongholdKind.Clan => "가",
            _ when point.Resources.HasSpiritVein => "맥",
            _ when point.Resources.Mine != MineResourceKind.None => "광",
            _ => string.Empty,
        };
        return hasActivityAuthority
            ? string.IsNullOrEmpty(marker) ? "활" : $"{marker}·활"
            : marker;
    }

    private static string CreatePointToolTip(
        PointHeavenViewDefinition point,
        MapLocationSnapshot? region) =>
        $"{point.KoreanName} · {FormatTerrain(point.Terrain)} · " +
        $"{FormatSettlementSummary(point.Settlements)} · " +
        (region is null
            ? "지역 활동 DataUnavailable"
            : $"현재 활동 권위 {region.Name}");

    private static string FormatSettlementSummary(PointSettlementProfile settlements) =>
        settlements.Society switch
        {
            PointSocietyKind.Mortal => "범인 정착지",
            PointSocietyKind.Cultivator => settlements.Stronghold switch
            {
                PointStrongholdKind.City => "수선 도시",
                PointStrongholdKind.Sect => "종문 거점",
                PointStrongholdKind.Clan => "가문 거점",
                _ => "수선 정착지",
            },
            _ => "자연 지역",
        };

    private void GeographyLayerButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string raw }
            || !byte.TryParse(raw, out var layer)
            || layer == _selectedGeographyLayer)
        {
            return;
        }

        _selectedGeographyLayer = layer;
        _projectedGeographyLayer = 0;
        AgentOffset = 0;
        ApplyVisibleMapMode();
        RequestAgentPageIfVisible();
    }

    private void MapViewModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string mode })
        {
            return;
        }

        var statisticsMode = StringComparer.Ordinal.Equals(mode, "Statistics");
        if (_statisticsMode == statisticsMode)
        {
            return;
        }

        _statisticsMode = statisticsMode;
        ApplyVisibleMapMode();
        RequestAgentPageIfVisible();
    }

    private void ProvinceTileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ushort provinceId })
        {
            return;
        }

        var geography = _snapshot.Geography ?? WorldGeographyBlueprint.Empty;
        var province = geography.Provinces.FirstOrDefault(value => value.Id == provinceId);
        if (!province.HasPointDetail)
        {
            return;
        }

        _selectedProvinceId = provinceId;
        ApplyGeography();
    }

    private void PointTileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ushort pointId }
            || pointId == _selectedPointId)
        {
            return;
        }

        _selectedPointId = pointId;
        _projectedGeographyPointId = ushort.MaxValue;
        AgentOffset = 0;
        ApplyGeography();
        RequestAgentPageIfVisible();
    }

    private void PointDetailTabControl_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_applying || !ReferenceEquals(e.Source, sender) || !IsInitialized)
        {
            return;
        }

        if (PointDetailTabControl.SelectedIndex == 2)
        {
            AgentOffset = 0;
            RequestAgentPageIfVisible();
            if (!ShouldLoadAgents)
            {
                ApplyGeography();
            }
            return;
        }

        ApplyGeography();
    }

    private void RequestAgentPageIfVisible()
    {
        if (ShouldLoadAgents)
        {
            ObservationRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void PreviousAgentPageButton_Click(object sender, RoutedEventArgs e)
    {
        if (AgentOffset <= 0)
        {
            return;
        }

        AgentOffset = Math.Max(0, AgentOffset - WorldMapRules.DetailedAgentPageSize);
        ObservationRequested?.Invoke(this, EventArgs.Empty);
    }

    private void NextAgentPageButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedProductionRegion(out var region)
            || AgentOffset + WorldMapRules.DetailedAgentPageSize >= region.Population)
        {
            return;
        }

        AgentOffset = checked(AgentOffset + WorldMapRules.DetailedAgentPageSize);
        ObservationRequested?.Invoke(this, EventArgs.Empty);
    }

    private void MapAgentGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_applying || MapAgentGrid.SelectedItem is not MapAgentRow row)
        {
            return;
        }

        AgentSelected?.Invoke(this, new MapAgentSelectedEventArgs(row.AgentId));
    }

    private void StrongestAgentButton_Click(object sender, RoutedEventArgs e)
    {
        if (_strongestAgent is { } agentId)
        {
            AgentSelected?.Invoke(this, new MapAgentSelectedEventArgs(agentId));
        }
    }

    private sealed record ProvinceTileRow(
        ushort ProvinceId,
        string Label,
        string ToolTip,
        bool IsEnabled,
        Brush Background,
        Brush Border);

    private sealed record PointTileRow(
        ushort PointId,
        string Label,
        string Marker,
        string ToolTip,
        Brush Background,
        Brush Border);

    private sealed record MonsterRow(
        string Name,
        string Population,
        string Danger,
        string Trait,
        string Drop);

    private sealed record MapAgentRow(
        AgentId AgentId,
        string Rank,
        string Name,
        string Realm,
        string CombatPower);
}

public sealed class MapAgentSelectedEventArgs(AgentId agentId) : EventArgs
{
    public AgentId AgentId { get; } = agentId;
}
