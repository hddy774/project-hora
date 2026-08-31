using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Chronicles.Core;
using Chronicles.Simulation;
using static Chronicles.Desktop.PresentationText;

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
    public bool ShouldLoadAgents => RegionTabControl.SelectedIndex == 2;

    public void Apply(WorldMapSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _snapshot = snapshot;
        MapSummaryText.Text =
            $"요수 사냥 {KoreanNumberFormatter.Format(snapshot.TotalMonsterHunts)}회 · " +
            $"승리 {KoreanNumberFormatter.Format(snapshot.TotalMonsterVictories)}회 · " +
            "지역을 선택해 개요·생태·수선자를 관찰합니다.";
        MapTileList.ItemsSource = snapshot.Locations
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
            $"{FormatBiome(region.Biome)} · 좌표 {region.X},{region.Y} · " +
            (region.Monsters.Count == 0
                ? "안전지대"
                : $"요수 {KoreanNumberFormatter.Format(region.Monsters.Sum(item => (long)item.Population))}마리");
        RegionPopulationText.Text = $"{KoreanNumberFormatter.Format(region.Population)}명";
        RegionHighestRealmText.Text = region.Population == 0
            ? "인물 없음"
            : FormatRealm(region.HighestRealm, region.HighestRealmLayer);
        RegionAveragePowerText.Text = KoreanNumberFormatter.Format(region.AverageCombatPower);
        RegionMaximumPowerText.Text = KoreanNumberFormatter.Format(region.MaximumCombatPower);
        RegionActivityText.Text = FormatActivitySummary(region.Activities, region.Population);

        _strongestAgent = region.StrongestAgent?.AgentId;
        StrongestAgentButton.IsEnabled = _strongestAgent is not null;
        StrongestAgentButton.Content = region.StrongestAgent is { } strongest
            ? $"지역 최강 · #{strongest.CombatRank:N0} {strongest.Name} · {KoreanNumberFormatter.Format(strongest.CombatPower)}"
            : "지역 최강 · 인물 없음";

        var monsters = region.Monsters.Select(monster => new MonsterRow(
            monster.Species.KoreanName,
            KoreanNumberFormatter.Format(monster.Population),
            KoreanNumberFormatter.Format(monster.Species.BaseDanger),
            EconomyRules.GetMaterial(monster.Species.DropMaterial).KoreanName)).ToArray();
        SafeRegionText.Visibility = monsters.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        MonsterGrid.Visibility = monsters.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        MonsterGrid.ItemsSource = monsters;

        _applying = true;
        try
        {
            MapAgentGrid.ItemsSource = region.Agents.Select(agent => new MapAgentRow(
                agent.AgentId,
                agent.CombatRank.ToString("N0", CultureInfo.CurrentCulture),
                agent.Name,
                FormatRealm(agent.Realm, agent.RealmLayer),
                KoreanNumberFormatter.Format(agent.CombatPower))).ToArray();
        }
        finally
        {
            _applying = false;
        }

        var first = region.Population == 0 ? 0 : region.AgentPageOffset + 1;
        var last = Math.Min(region.Population, region.AgentPageOffset + region.Agents.Count);
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

    private void RegionTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_applying || !ReferenceEquals(e.Source, sender) || !ShouldLoadAgents)
        {
            return;
        }

        AgentOffset = 0;
        ObservationRequested?.Invoke(this, EventArgs.Empty);
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
}

public sealed class MapAgentSelectedEventArgs(AgentId agentId) : EventArgs
{
    public AgentId AgentId { get; } = agentId;
}
