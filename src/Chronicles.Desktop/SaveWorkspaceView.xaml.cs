using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Chronicles.Application;
using Chronicles.Core;
using static Chronicles.Presentation.PresentationText;

namespace Chronicles.Desktop;

public sealed class SaveSlotActionEventArgs(string slotId) : EventArgs
{
    public string SlotId { get; } = slotId;
}

public partial class SaveWorkspaceView : UserControl
{
    private IReadOnlyList<SaveSlotCatalogEntry> _entries = [];
    private string? _selectedSlotId;
    private bool _operationActive;

    public SaveWorkspaceView()
    {
        InitializeComponent();
    }

    public event EventHandler? RefreshRequested;
    public event EventHandler? RootFolderRequested;
    public event EventHandler<SaveSlotActionEventArgs>? SaveRequested;
    public event EventHandler<SaveSlotActionEventArgs>? LoadRequested;
    public event EventHandler<SaveSlotActionEventArgs>? FolderRequested;

    public void Apply(IReadOnlyList<SaveSlotCatalogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _entries = entries;
        if (_selectedSlotId is null
            || !_entries.Any(entry => StringComparer.Ordinal.Equals(
                entry.Definition.SlotId,
                _selectedSlotId)))
        {
            _selectedSlotId = _entries.Count == 0
                ? null
                : _entries[0].Definition.SlotId;
        }

        SlotItemsControl.ItemsSource = entries.Select(CreateCard).ToArray();
        RefreshSelectedDetails();
    }

    public void SetBusy(bool busy, string message)
    {
        _operationActive = false;
        OperationProgressPanel.Visibility = Visibility.Collapsed;
        IsEnabled = !busy;
        StatusText.Text = message;
        StatusText.Foreground = (Brush)FindResource(
            busy ? "AccentBrush" : "SecondaryTextBrush");
    }

    public void SetStatus(string message, bool isError = false)
    {
        _operationActive = false;
        OperationProgressPanel.Visibility = Visibility.Collapsed;
        IsEnabled = true;
        StatusText.Text = message;
        StatusText.Foreground = (Brush)FindResource(
            isError ? "DangerBrush" : "PositiveBrush");
    }

    public void BeginOperation(string message)
    {
        _operationActive = true;
        IsEnabled = false;
        StatusText.Text = message;
        StatusText.Foreground = (Brush)FindResource("AccentBrush");
        OperationProgressBar.Value = 0;
        OperationProgressText.Text = "0% · 준비";
        OperationProgressPanel.Visibility = Visibility.Visible;
    }

    public void SetOperationProgress(SaveOperationProgress progress)
    {
        if (!_operationActive)
        {
            return;
        }

        var percent = Math.Clamp(progress.Percent, 0, 100);
        OperationProgressBar.Value = percent;
        OperationProgressText.Text = $"{percent}% · {progress.Stage}";
    }

    private SaveSlotCardViewModel CreateCard(SaveSlotCatalogEntry entry)
    {
        var empty = entry.Status == SaveSlotCatalogStatus.Empty;
        var status = entry.Status switch
        {
            SaveSlotCatalogStatus.Empty => "비어 있음",
            SaveSlotCatalogStatus.Ready => "정상",
            SaveSlotCatalogStatus.Recoverable => "복구 가능",
            SaveSlotCatalogStatus.Corrupt => "손상",
            SaveSlotCatalogStatus.Incompatible => "비호환",
            _ => "확인 필요",
        };
        var statusBrush = (Brush)FindResource(entry.Status switch
        {
            SaveSlotCatalogStatus.Ready => "PositiveBrush",
            SaveSlotCatalogStatus.Recoverable => "AccentBrush",
            SaveSlotCatalogStatus.Corrupt or SaveSlotCatalogStatus.Incompatible => "DangerBrush",
            _ => "SecondaryTextBrush",
        });
        var borderBrush = StringComparer.Ordinal.Equals(
            _selectedSlotId,
            entry.Definition.SlotId)
                ? (Brush)FindResource("AccentBrush")
                : (Brush)FindResource("BorderBrush");
        return new SaveSlotCardViewModel(
            entry.Definition.SlotId,
            $"{entry.Definition.Number}. {entry.Definition.DisplayName}",
            status,
            statusBrush,
            borderBrush,
            empty
                ? "아직 저장되지 않음"
                : entry.UpdatedAtUtc?.ToLocalTime().ToString(
                    "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture) ?? "저장 시각 없음",
            entry.HasDetailedSummary
                ? $"세계 {FormatGameDate(GameCalendar.GetDate(entry.Tick))} · {entry.Population:N0}명 · seed {entry.Seed}"
                : empty ? "새 세계를 저장할 수 있습니다." : "세부 요약을 확인하는 중",
            entry.HasDetailedSummary
                ? $"1위 {entry.LeaderName} · {FormatLeaderRealm(entry)} · {KoreanNumberFormatter.Format(entry.LeaderCombatPower)}"
                : string.Empty,
            empty
                ? string.Empty
                : $"DB {FormatBytes(entry.DatabaseBytes)} · schema {entry.WorldSchema}/{entry.CheckpointSchema} · seq {entry.CreatedSequence:N0}",
            empty ? "저장" : "덮어쓰기",
            entry.CanLoad,
            entry.Status != SaveSlotCatalogStatus.Empty);
    }

    private void RefreshSelectedDetails()
    {
        var entry = _entries.FirstOrDefault(item => StringComparer.Ordinal.Equals(
            item.Definition.SlotId,
            _selectedSlotId));
        if (entry is null)
        {
            SelectedSlotTitleText.Text = "슬롯을 선택해 주세요.";
            SelectedWorldText.Text = string.Empty;
            SelectedRecordText.Text = string.Empty;
            SelectedFileText.Text = string.Empty;
            return;
        }

        SelectedSlotTitleText.Text =
            $"{entry.Definition.Number}. {entry.Definition.DisplayName} · {entry.StatusMessage}";
        SelectedWorldText.Text = entry.HasDetailedSummary
            ? $"세계 시간  {FormatGameDate(GameCalendar.GetDate(entry.Tick))}\n" +
              $"인구  {entry.Population:N0}명\nseed  {entry.Seed}\n" +
              $"전투력 1위  {entry.LeaderName} ({entry.LeaderAgentId})\n" +
              $"경지  {FormatLeaderRealm(entry)}\n표시 전투력  {KoreanNumberFormatter.Format(entry.LeaderCombatPower)}"
            : "상세 세계 요약이 없습니다.";
        SelectedRecordText.Text = entry.HasDetailedSummary
            ? $"사망  {KoreanNumberFormatter.Format(entry.TotalDeaths)}\n" +
              $"윤회  {KoreanNumberFormatter.Format(entry.TotalReincarnations)}\n" +
              $"완료 전투  {KoreanNumberFormatter.Format(entry.TotalCombatsCompleted)}\n" +
              $"진행 전투  {KoreanNumberFormatter.Format(entry.ActiveCombats)}\n" +
              $"경지 돌파  {KoreanNumberFormatter.Format(entry.TotalBreakthroughs)}\n" +
              $"조직  {KoreanNumberFormatter.Format(entry.OrganizationCount)}"
            : "상세 누계가 없습니다.";
        SelectedFileText.Text =
            $"상태  {entry.StatusMessage}\nDB 크기  {FormatBytes(entry.DatabaseBytes)}\n" +
            $"world/checkpoint schema  {entry.WorldSchema}/{entry.CheckpointSchema}\n" +
            $"생성 sequence  {entry.CreatedSequence:N0}\nstate hash  {entry.StateHash:X16}\n" +
            $"primary  {FormatHealth(entry.PrimaryReadable)} · temp  {FormatHealth(entry.TemporaryReadable)} · backup  {FormatHealth(entry.BackupReadable)}";
    }

    private void SelectButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string slotId })
        {
            _selectedSlotId = slotId;
            SlotItemsControl.ItemsSource = _entries.Select(CreateCard).ToArray();
            RefreshSelectedDetails();
            SaveWorkspaceTabControl.SelectedItem = SlotDetailsTab;
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e) =>
        RaiseSlotEvent(sender, SaveRequested);

    private void LoadButton_Click(object sender, RoutedEventArgs e) =>
        RaiseSlotEvent(sender, LoadRequested);

    private void FolderButton_Click(object sender, RoutedEventArgs e) =>
        RaiseSlotEvent(sender, FolderRequested);

    private void RefreshButton_Click(object sender, RoutedEventArgs e) =>
        RefreshRequested?.Invoke(this, EventArgs.Empty);

    private void OpenRootButton_Click(object sender, RoutedEventArgs e) =>
        RootFolderRequested?.Invoke(this, EventArgs.Empty);

    private void RaiseSlotEvent(
        object sender,
        EventHandler<SaveSlotActionEventArgs>? handler)
    {
        if (sender is Button { Tag: string slotId })
        {
            _selectedSlotId = slotId;
            handler?.Invoke(this, new SaveSlotActionEventArgs(slotId));
        }
    }

    private static string FormatLeaderRealm(SaveSlotCatalogEntry entry) =>
        entry.LeaderHasEnteredCultivation
            ? $"{FormatRealmStage(entry.LeaderRealmStage)} · " +
              $"{(entry.LeaderGreatPerfectionAchieved ? "대원만" : entry.LeaderRealmLayer <= 3 ? "초기" : entry.LeaderRealmLayer <= 6 ? "중기" : "후기")} · " +
              $"{entry.LeaderRealmLayer:N0}층"
            : "범인";

    private static string FormatGameDate(GameDate date) =>
        $"{KoreanNumberFormatter.Format(date.Year)}년 {date.Month:N0}월 {date.Day:N0}일";

    private static string FormatBytes(long bytes) => bytes <= 0
        ? "0 B"
        : bytes >= 1024L * 1024L
            ? $"{bytes / 1024d / 1024d:N2} MiB"
            : $"{bytes / 1024d:N1} KiB";

    private static string FormatHealth(bool readable) => readable ? "읽기 가능" : "없음/손상";

    private sealed record SaveSlotCardViewModel(
        string SlotId,
        string Title,
        string Status,
        Brush StatusBrush,
        Brush BorderBrush,
        string SavedAt,
        string WorldLine,
        string LeaderLine,
        string FileLine,
        string SaveLabel,
        bool CanLoad,
        bool HasFiles)
    {
        public string AccessibilityName => $"{Title}, {Status}, {SavedAt}";
        public string SelectAccessibilityName => $"{Title} 상세 보기";
        public string SaveAccessibilityName => $"{Title} {SaveLabel}";
        public string LoadAccessibilityName => $"{Title} 불러오기";
        public string FolderAccessibilityName => $"{Title} 폴더 열기";
    }
}
