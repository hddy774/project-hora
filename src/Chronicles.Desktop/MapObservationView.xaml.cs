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
    private byte _selectedHeavenlyLayer = 1;
    private byte _selectedGeographyLayer = 1;
    private ushort _selectedProvinceId = WorldGeographyRules.OcheonProvinceId;
    private ushort _selectedPointId = 44;
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

    public ushort SelectedLocationId { get; private set; } = WorldMapRules.CityLocationId;
    public int AgentOffset { get; private set; }
    public bool ShouldLoadAgents =>
        MapModeTabControl.SelectedIndex == 0 && RegionTabControl.SelectedIndex == 2;

    public void Apply(WorldMapSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _snapshot = snapshot;
        if (MapModeTabControl.SelectedIndex == 1)
        {
            ApplyGeography();
            return;
        }

        if (WorldMapRules.GetLocation(SelectedLocationId).HeavenlyLayer
            != _selectedHeavenlyLayer)
        {
            SelectedLocationId = _selectedHeavenlyLayer == 1
                ? WorldMapRules.CityLocationId
                : WorldMapRules.SecondHeavenCityLocationId;
            AgentOffset = 0;
        }
        var atlas = snapshot.Atlas ?? WorldAtlasSnapshot.Empty;
        MapSummaryText.Text =
            $"요수 사냥 {KoreanNumberFormatter.Format(snapshot.TotalMonsterHunts)}회 · " +
            $"승리 {KoreanNumberFormatter.Format(snapshot.TotalMonsterVictories)}회 · " +
            $"Production 지점 {atlas.ProductionLocationCount:N0} · " +
            $"영맥 Source {atlas.SpiritVeinSourceCount:N0} · " +
            "현재 층천의 지역을 선택해 세부 권위를 관찰합니다.";
        var search = MapSearchBox.Text.Trim();
        MapTileList.ItemsSource = snapshot.Locations
            .Where(item => item.Atlas.HeavenlyLayer == _selectedHeavenlyLayer)
            .Where(item => search.Length == 0
                || item.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(item => item.Y)
            .ThenBy(item => item.X)
            .Select(item => new MapTileRow(
                item.Id,
                item.Name,
                FormatBiome(item.Biome),
                $"{KoreanNumberFormatter.Format(item.Population)}명",
                item.Biome == BiomeType.City
                    ? "안전지대"
                    : $"요수 {KoreanNumberFormatter.Format(item.Monsters.Sum(monster => (long)monster.Population))}",
                GetBiomeBrush(item.Biome),
                item.Id == SelectedLocationId
                    ? _accentBrush
                    : _borderBrush))
            .ToArray();

        var selected = snapshot.Locations.FirstOrDefault(item => item.Id == SelectedLocationId);
        if (selected is null)
        {
            return;
        }

        ApplyRegion(selected);
    }

    private void ApplyRegion(MapLocationSnapshot region)
    {
        RegionTitleText.Text = region.Name;
        RegionSubtitleText.Text =
            $"{region.Atlas.HeavenlyLayerName} · {FormatBiome(region.Biome)} · 좌표 {region.X},{region.Y} · " +
            (region.Monsters.Count == 0
                ? "안전지대"
                : $"요수 {KoreanNumberFormatter.Format(region.Monsters.Sum(item => (long)item.Population))}마리");

        switch (RegionTabControl.SelectedIndex)
        {
            case 0:
                ApplyOverview(region);
                break;
            case 1:
                ApplyMonsterEcology(region);
                break;
            case 2:
                ApplyAgentPage(region);
                break;
            case 3:
                ApplyAtlas(region);
                break;
        }
    }

    private void ApplyAtlas(MapLocationSnapshot region)
    {
        AtlasHierarchyText.Text =
            $"{region.Atlas.HeavenlyLayerName}  ›  {region.Atlas.ContinentName}  ›  " +
            $"{region.Atlas.GrandRegionName}  ›  {region.Atlas.AreaName}  ›  {region.Name}";
        AtlasLocationRefText.Text =
            $"{region.Atlas.LocationNodeId} · Atlas revision " +
            $"{_snapshot.Atlas?.Revision ?? 0:N0}";
        SpiritVeinText.Text =
            $"{region.Atlas.SpiritVeinSourceCount:N0}개 · " +
            $"잔여 {KoreanNumberFormatter.Format(region.Atlas.RemainingSpiritVeinSupply)} · " +
            $"월 생산력 {KoreanNumberFormatter.Format(region.Atlas.MonthlySpiritVeinYield)}";
        AtlasLayerGrid.ItemsSource = (_snapshot.Atlas?.Layers
                ?? Array.Empty<AtlasLayerSnapshot>())
            .Select(layer => new AtlasLayerRow(
                layer.Name,
                layer.ImplementationState == AtlasImplementationState.Production
                    ? "실구현"
                    : "개발 중",
                layer.LocationCount.ToString("N0", CultureInfo.CurrentCulture),
                KoreanNumberFormatter.Format(layer.Population),
                layer.SpiritVeinSourceCount.ToString("N0", CultureInfo.CurrentCulture)))
            .ToArray();
    }

    private void ApplyOverview(MapLocationSnapshot region)
    {
        RegionPopulationText.Text = $"{KoreanNumberFormatter.Format(region.Population)}명";
        RegionHighestRealmText.Text = !region.HasCultivator
            ? "범인"
            : FormatRealm(region.HighestRealm, region.HighestRealmLayer);
        RegionAveragePowerText.Text = KoreanNumberFormatter.Format(region.AverageCombatPower);
        RegionMaximumPowerText.Text = KoreanNumberFormatter.Format(region.MaximumCombatPower);
        RegionActivityText.Text = FormatActivitySummary(region.Activities, region.Population);

        _strongestAgent = region.StrongestAgent?.AgentId;
        StrongestAgentButton.IsEnabled = _strongestAgent is not null;
        StrongestAgentButton.Content = region.StrongestAgent is { } strongest
            ? $"지역 최강 · #{strongest.CombatRank:N0} {strongest.Name} · {KoreanNumberFormatter.Format(strongest.CombatPower)}"
            : "지역 최강 · 인물 없음";
    }

    private void ApplyMonsterEcology(MapLocationSnapshot region)
    {
        var monsters = region.Monsters.Select(monster => new MonsterRow(
            monster.Species.KoreanName,
            KoreanNumberFormatter.Format(monster.Population),
            KoreanNumberFormatter.Format(monster.Species.BaseDanger),
            EconomyRules.GetMaterial(monster.Species.DropMaterial).KoreanName)).ToArray();
        SafeRegionText.Visibility = monsters.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        MonsterGrid.Visibility = monsters.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        MonsterGrid.ItemsSource = monsters;
    }

    private void ApplyAgentPage(MapLocationSnapshot region)
    {
        if (_snapshot.DetailedLocationId != region.Id)
        {
            MapAgentGrid.ItemsSource = Array.Empty<MapAgentRow>();
            AgentPageText.Text = "선택 지역 수선자를 불러오는 중";
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

    private static string FormatActivitySummary(
        MapRegionActivitySnapshot activities,
        int population)
    {
        if (population == 0)
        {
            return "현재 지역에 수선자가 없습니다.";
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

    private Brush GetBiomeBrush(BiomeType biome) => biome switch
    {
        BiomeType.City => _cityBrush,
        BiomeType.Forest => _forestBrush,
        BiomeType.Plains => _plainsBrush,
        BiomeType.Mountains => _mountainBrush,
        _ => _fallbackBrush,
    };

    private void ApplyGeography()
    {
        var geography = _snapshot.Geography ?? WorldGeographyBlueprint.Empty;
        if (geography.Revision == 0)
        {
            GeographySummaryText.Text = "지리 기준선을 불러오지 못했습니다.";
            ProvinceTileList.ItemsSource = Array.Empty<ProvinceTileRow>();
            PointTileList.ItemsSource = Array.Empty<PointTileRow>();
            return;
        }

        var activeProvince = geography.Provinces[geography.ActiveProvinceId];
        GeographySummaryText.Text =
            $"{geography.Continent.KoreanName} · 주 {geography.Provinces.Count:N0}칸 · " +
            $"주 간 기준 거리 {KoreanNumberFormatter.Format(geography.Continent.ProvinceTileDistanceLi)}리 · " +
            $"현재 상세 지역 {activeProvince.KoreanName} · 층천별 지점 {WorldGeographyRules.PointsPerProvince:N0}칸";

        if (_projectedGeographyRevision != geography.Revision)
        {
            ProvinceTileList.ItemsSource = geography.Provinces.Select(province =>
                new ProvinceTileRow(
                    province.Id,
                    province.HasPointDetail ? "오천" : $"{province.X + 1},{province.Y + 1}",
                    province.KoreanName,
                    province.HasPointDetail,
                    province.HasPointDetail ? _cityBrush : _fallbackBrush,
                    province.Id == _selectedProvinceId ? _accentBrush : _borderBrush))
                .ToArray();
        }

        if (_projectedGeographyRevision != geography.Revision
            || _projectedGeographyLayer != _selectedGeographyLayer
            || _projectedGeographyPointId != _selectedPointId)
        {
            var points = _selectedGeographyLayer == 1
                ? geography.FirstHeavenPoints
                : geography.SecondHeavenPoints;
            PointGridTitleText.Text =
                $"{activeProvince.KoreanName} · 제{_selectedGeographyLayer:N0}층천 지점 10×10";
            PointTileList.ItemsSource = points.Select(point => new PointTileRow(
                point.PhysicalPointId,
                $"{point.X + 1},{point.Y + 1}",
                FormatPointMarker(point),
                $"{point.KoreanName} · {FormatTerrain(point.Terrain)} · " +
                $"{FormatSettlementSummary(point.Settlements)}",
                GetTerrainBrush(point.Terrain),
                point.PhysicalPointId == _selectedPointId ? _accentBrush : _borderBrush))
                .ToArray();
        }

        var selected = WorldGeographyRules.GetPoint(
            _selectedGeographyLayer,
            _selectedPointId);
        ApplyPointDetails(selected);
        _projectedGeographyRevision = geography.Revision;
        _projectedGeographyLayer = _selectedGeographyLayer;
        _projectedGeographyPointId = _selectedPointId;
    }

    private void ApplyPointDetails(PointHeavenViewDefinition point)
    {
        PointTitleText.Text = point.KoreanName;
        PointCoordinateText.Text =
            $"{(_selectedGeographyLayer == 1 ? "제1층천 · 범인계" : "제2층천 · 수선계")} · " +
            $"좌표 {point.X + 1},{point.Y + 1} · {point.NodeId}";
        PointDescriptionText.Text = point.Description;
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
            $"{KoreanNumberFormatter.Format(point.Resources.EstimatedBeastPopulation)}마리 · " +
            $"생령 다양성 {point.Resources.LivingSpeciesDiversity:N0}";
        PointDistanceText.Text =
            $"{KoreanNumberFormatter.Format(point.Resources.OrthogonalTravelDistanceLi)}리";
    }

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

    private static string FormatPointMarker(PointHeavenViewDefinition point) =>
        point.Settlements.Stronghold switch
        {
            PointStrongholdKind.City => "성",
            PointStrongholdKind.Sect => "종",
            PointStrongholdKind.Clan => "가",
            _ when point.Resources.HasSpiritVein => "맥",
            _ when point.Resources.Mine != MineResourceKind.None => "광",
            _ => string.Empty,
        };

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

    private void MapTileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ushort locationId }
            || locationId == SelectedLocationId)
        {
            return;
        }

        SelectedLocationId = locationId;
        AgentOffset = 0;
        Apply(_snapshot);
        if (ShouldLoadAgents)
        {
            ObservationRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void HeavenlyLayerButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string raw }
            || !byte.TryParse(raw, out var layer)
            || layer == _selectedHeavenlyLayer)
        {
            return;
        }

        _selectedHeavenlyLayer = layer;
        SelectedLocationId = layer == 1
            ? WorldMapRules.CityLocationId
            : WorldMapRules.SecondHeavenCityLocationId;
        AgentOffset = 0;
        Apply(_snapshot);
        if (ShouldLoadAgents)
        {
            ObservationRequested?.Invoke(this, EventArgs.Empty);
        }
    }

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
        ApplyGeography();
    }

    private void ProvinceTileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ushort provinceId })
        {
            return;
        }

        var province = WorldGeographyRules.GetProvince(provinceId);
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
        ApplyGeography();
    }

    private void MapModeTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_applying || !ReferenceEquals(e.Source, sender) || !IsInitialized)
        {
            return;
        }

        if (MapModeTabControl.SelectedIndex == 1)
        {
            ApplyGeography();
            return;
        }

        Apply(_snapshot);
        if (ShouldLoadAgents)
        {
            ObservationRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void MapSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (IsInitialized)
        {
            Apply(_snapshot);
        }
    }

    private void RegionTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_applying || !ReferenceEquals(e.Source, sender))
        {
            return;
        }

        if (ShouldLoadAgents)
        {
            AgentOffset = 0;
            ObservationRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            Apply(_snapshot);
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
        var region = _snapshot.Locations.FirstOrDefault(item => item.Id == SelectedLocationId);
        if (region is null
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

    private sealed record MapTileRow(
        ushort LocationId,
        string Name,
        string Biome,
        string Population,
        string Summary,
        Brush Background,
        Brush Border);

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
        string Drop);

    private sealed record MapAgentRow(
        AgentId AgentId,
        string Rank,
        string Name,
        string Realm,
        string CombatPower);

    private sealed record AtlasLayerRow(
        string Name,
        string State,
        string Locations,
        string Population,
        string Veins);
}

public sealed class MapAgentSelectedEventArgs(AgentId agentId) : EventArgs
{
    public AgentId AgentId { get; } = agentId;
}
