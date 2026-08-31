using System.Windows;
using System.Windows.Controls;

namespace Chronicles.Desktop;

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
    public bool AutoWorldOutput => AutoWorldOutputCheckBox.IsChecked == true;
    public bool AutoDebugOutput => AutoDebugOutputCheckBox.IsChecked == true;

    internal void ApplyRecent(IReadOnlyList<EventRow> rows)
    {
        _applying = true;
        try
        {
            RecentEventList.ItemsSource = rows;
            RecentCountText.Text = $"최근 중요 기록 {rows.Count:N0} / 최대 100줄";
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
                ? $"영구 보존 {totalCount:N0}건 · 화면 최신 {rows.Count:N0}건 · 파일 출력은 전체"
                : $"영구 보존 {totalCount:N0}건 · 1위 교체와 세계적 성취";
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
