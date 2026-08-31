using System.Windows;
using System.Windows.Controls;

namespace Chronicles.Desktop;

internal enum WorldLogCategory : byte
{
    All,
    Competition,
    Combat,
    Cultivation,
    Economy,
    Reincarnation,
}

internal enum WorldChronicleCategory : byte
{
    History,
    Cultivation,
    Ranking,
    LifeAndReincarnation,
}

public partial class WorldRecordsView : UserControl
{
    private bool _applying;

    public WorldRecordsView()
    {
        InitializeComponent();
    }

    public event EventHandler? ObservationRequested;
    public event EventHandler? AutoOutputChanged;
    public event EventHandler? WorldExportRequested;
    public event EventHandler? DebugExportRequested;
    public event EventHandler? FolderOpenRequested;

    public int SelectedRecordIndex => RecordTabControl.SelectedIndex;
    internal WorldLogCategory SelectedWorldLogCategory =>
        (WorldLogCategory)Math.Max(0, WorldLogCategoryTabControl.SelectedIndex);
    internal WorldChronicleCategory SelectedWorldChronicleCategory =>
        (WorldChronicleCategory)Math.Max(0, WorldChronicleCategoryTabControl.SelectedIndex);
    public bool AutoWorldOutput => AutoWorldOutputCheckBox.IsChecked == true;
    public bool AutoDebugOutput => AutoDebugOutputCheckBox.IsChecked == true;

    internal void SelectRecordTab(int index)
    {
        if (index < 0 || index >= RecordTabControl.Items.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        RecordTabControl.SelectedIndex = index;
    }

    internal void ApplyRecent(IReadOnlyList<EventRow> rows, int sourceCount)
    {
        _applying = true;
        try
        {
            RecentEventList.ItemsSource = rows;
            RecentCountText.Text =
                $"{FormatWorldLogCategory(SelectedWorldLogCategory)} {rows.Count:N0}건 · " +
                $"최근 중요 기록 {sourceCount:N0} / 최대 100줄";
        }
        finally
        {
            _applying = false;
        }
    }

    internal void ApplyChronicle(IReadOnlyList<EventRow> rows, int totalCount)
    {
        _applying = true;
        try
        {
            WorldChronicleList.ItemsSource = rows;
            ChronicleCountText.Text = totalCount > rows.Count
                ? $"{FormatWorldChronicleCategory(SelectedWorldChronicleCategory)} · " +
                    $"영구 보존 {totalCount:N0}권 · 화면 최신 {rows.Count:N0}권 · 파일 출력은 전체"
                : $"{FormatWorldChronicleCategory(SelectedWorldChronicleCategory)} · " +
                    $"100년 단위 세계 역사서 {totalCount:N0}권";
        }
        finally
        {
            _applying = false;
        }
    }

    internal void ApplyDebug(IReadOnlyList<EventRow> rows)
    {
        _applying = true;
        try
        {
            DebugLogList.ItemsSource = rows;
            DebugCountText.Text = $"현재 세션 {rows.Count:N0} / 최대 {GameLogService.DebugLogCapacity:N0}줄";
        }
        finally
        {
            _applying = false;
        }
    }

    public void SetOutputStatus(string text, bool isError = false)
    {
        OutputStatusText.Text = text;
        OutputStatusText.Foreground = (System.Windows.Media.Brush)FindResource(
            isError ? "DangerBrush" : "PositiveBrush");
    }

    private void RecordTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_applying && ReferenceEquals(e.Source, sender))
        {
            ObservationRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RecordCategoryTabControl_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (!_applying && ReferenceEquals(e.Source, sender))
        {
            ObservationRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private static string FormatWorldLogCategory(WorldLogCategory category) => category switch
    {
        WorldLogCategory.All => "전체",
        WorldLogCategory.Competition => "경쟁",
        WorldLogCategory.Combat => "전투",
        WorldLogCategory.Cultivation => "수행",
        WorldLogCategory.Economy => "경제",
        WorldLogCategory.Reincarnation => "생사·윤회",
        _ => throw new ArgumentOutOfRangeException(nameof(category)),
    };

    private static string FormatWorldChronicleCategory(WorldChronicleCategory category) => category switch
    {
        WorldChronicleCategory.History => "세계 역사서",
        WorldChronicleCategory.Cultivation => "수행사",
        WorldChronicleCategory.Ranking => "경쟁사",
        WorldChronicleCategory.LifeAndReincarnation => "생사·윤회사",
        _ => throw new ArgumentOutOfRangeException(nameof(category)),
    };

    private void AutoOutputCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_applying)
        {
            AutoOutputChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void WorldExportButton_Click(object sender, RoutedEventArgs e) =>
        WorldExportRequested?.Invoke(this, EventArgs.Empty);

    private void DebugExportButton_Click(object sender, RoutedEventArgs e) =>
        DebugExportRequested?.Invoke(this, EventArgs.Empty);

    private void FolderOpenButton_Click(object sender, RoutedEventArgs e) =>
        FolderOpenRequested?.Invoke(this, EventArgs.Empty);
}
