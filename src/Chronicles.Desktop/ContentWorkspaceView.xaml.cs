using System.Windows;
using System.Windows.Controls;
using Chronicles.Core;
using Chronicles.Presentation;
using Chronicles.Simulation;

namespace Chronicles.Desktop;

public partial class ContentWorkspaceView : UserControl
{
    public const int SecretRealmPageSize = 100;

    private WorldContentSnapshot _snapshot = WorldContentSnapshot.Empty;
    private long _projectedRevision = long.MinValue;
    private int _projectedOffset = -1;
    private SecretRealmNature? _natureFilter;

    public ContentWorkspaceView()
    {
        InitializeComponent();
        ClearDetails();
    }

    public event EventHandler? PageRequested;

    public event EventHandler? ProjectionChanged;

    public int SecretRealmOffset { get; private set; }

    public bool ShouldLoadSecretRealmRows => ContentDetailTabControl.SelectedIndex == 0;

    public void ResetProjection()
    {
        _snapshot = WorldContentSnapshot.Empty;
        _projectedRevision = long.MinValue;
        _projectedOffset = -1;
        SecretRealmOffset = 0;
        SecretRealmGrid.ItemsSource = Array.Empty<SecretRealmRow>();
        ClearDetails();
        ApplySummary();
    }

    public void Apply(WorldContentSnapshot? snapshot)
    {
        snapshot ??= WorldContentSnapshot.Empty;
        var unchanged = _projectedRevision == snapshot.AuthorityRevision
            && _projectedOffset == snapshot.SecretRealmResultOffset
            && _snapshot.IncludesHidden == snapshot.IncludesHidden;
        _snapshot = snapshot;
        SecretRealmOffset = snapshot.SecretRealmResultOffset;
        if (unchanged)
        {
            return;
        }

        _projectedRevision = snapshot.AuthorityRevision;
        _projectedOffset = snapshot.SecretRealmResultOffset;
        ApplySummary();
        RebuildSecretRealmRows();
    }

    private void ApplySummary()
    {
        DefinitionCountText.Text = KoreanNumberFormatter.Format(_snapshot.DefinitionCount);
        InstanceCountText.Text = KoreanNumberFormatter.Format(_snapshot.TotalInstances);
        NaturalRealmCountText.Text = KoreanNumberFormatter.Format(_snapshot.NaturalSecretRealms);
        ArtificialRealmCountText.Text = KoreanNumberFormatter.Format(_snapshot.ArtificialSecretRealms);
        PublicRealmCountText.Text = KoreanNumberFormatter.Format(_snapshot.PubliclyLocatedSecretRealms);
        KnownRealmCountText.Text = KoreanNumberFormatter.Format(_snapshot.ViewerKnownSecretRealms);
        MaterialStackCountText.Text = KoreanNumberFormatter.Format(_snapshot.MaterialStackCount);
        ItemInstanceCountText.Text = KoreanNumberFormatter.Format(_snapshot.ItemInstanceCount);
        PhysicalRecordCountText.Text = KoreanNumberFormatter.Format(_snapshot.PhysicalRecordCount);
        VisibilityModeText.Text = _snapshot.IncludesHidden ? "세계 설정 진단" : "공개 지식";
    }

    private void RebuildSecretRealmRows()
    {
        var selectedId = SecretRealmGrid.SelectedItem is SecretRealmRow selected
            ? selected.Instance.Id
            : (Chronicles.Core.ContentInstanceId?)null;
        var rows = _snapshot.SecretRealms
            .Where(instance => _natureFilter is null || instance.SecretRealmNature == _natureFilter)
            .Select(instance => new SecretRealmRow(
                instance,
                instance.Id.ToString(),
                FormatNature(instance.SecretRealmNature),
                $"지점 {instance.LocationId + 1}",
                FormatDiscovery(instance.ViewerDiscovery),
                FormatStatus(instance.Status)))
            .ToArray();
        SecretRealmGrid.ItemsSource = rows;
        SecretRealmGrid.SelectedItem = selectedId is { } id
            ? rows.FirstOrDefault(row => row.Instance.Id == id) ?? rows.FirstOrDefault()
            : rows.FirstOrDefault();

        var first = _snapshot.SecretRealmResultTotal == 0
            ? 0
            : _snapshot.SecretRealmResultOffset + 1;
        var last = Math.Min(
            _snapshot.SecretRealmResultTotal,
            _snapshot.SecretRealmResultOffset + _snapshot.SecretRealms.Count);
        RealmListStatusText.Text = rows.Length == 0
            ? _snapshot.IncludesHidden
                ? "현재 필터와 일치하는 비경이 없습니다."
                : "아직 공개적으로 발견된 비경이 없습니다."
            : $"현재 페이지 {rows.Length:N0}개";
        RealmPageText.Text = $"{first:N0}–{last:N0} / {_snapshot.SecretRealmResultTotal:N0}";
        PreviousRealmPageButton.IsEnabled = _snapshot.SecretRealmResultOffset > 0;
        NextRealmPageButton.IsEnabled = last < _snapshot.SecretRealmResultTotal;
        RenderSelectedRealm();
    }

    private void RenderSelectedRealm()
    {
        if (SecretRealmGrid.SelectedItem is not SecretRealmRow row)
        {
            ClearDetails();
            return;
        }

        var instance = row.Instance;
        RealmTitleText.Text = $"{instance.DefinitionName} · {instance.Id}";
        RealmIdentityText.Text =
            $"{instance.DefinitionId} · {FormatNature(instance.SecretRealmNature)} · 지점 {instance.LocationId + 1}";
        RealmOwnerText.Text =
            $"소유 {instance.Owner.Kind}:{instance.Owner.Id:N0}\n보유 {instance.Holder.Kind}:{instance.Holder.Id:N0}";
        RealmAuthorityText.Text =
            $"{FormatStatus(instance.Status)} · revision {instance.AuthorityRevision:N0}\n{instance.LatestProvenanceId}";
        RealmKnowledgeText.Text = instance.ViewerDiscovery switch
        {
            ContentDiscoveryState.AccessGranted => "진입 권한까지 확인된 비경입니다.",
            ContentDiscoveryState.Located => "정확한 위치가 공개된 비경입니다.",
            ContentDiscoveryState.Aware => "존재만 인지했으며 위치는 아직 확인되지 않았습니다.",
            _ when _snapshot.IncludesHidden => "세계 설정 진단에서만 보이는 미발견 권위 상태입니다.",
            _ => "관찰자가 알 수 없는 비경입니다.",
        };
    }

    private void ClearDetails()
    {
        RealmTitleText.Text = "비경을 선택해 주세요.";
        RealmIdentityText.Text = "발견 권한이 있는 실제 Instance만 일반 관찰에 표시합니다.";
        RealmOwnerText.Text = "—";
        RealmAuthorityText.Text = "—";
        RealmKnowledgeText.Text = "세계 설정 모드에서는 검증 목적으로 숨은 비경을 확인할 수 있습니다.";
    }

    private void SecretRealmGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        RenderSelectedRealm();

    private void RealmFilter_Click(object sender, RoutedEventArgs e)
    {
        _natureFilter = sender is RadioButton { Tag: string tag }
            ? tag switch
            {
                "Natural" => SecretRealmNature.Natural,
                "Artificial" => SecretRealmNature.Artificial,
                _ => null,
            }
            : null;
        RebuildSecretRealmRows();
    }

    private void PreviousRealmPageButton_Click(object sender, RoutedEventArgs e)
    {
        SecretRealmOffset = Math.Max(0, SecretRealmOffset - SecretRealmPageSize);
        PageRequested?.Invoke(this, EventArgs.Empty);
    }

    private void NextRealmPageButton_Click(object sender, RoutedEventArgs e)
    {
        if (SecretRealmOffset + SecretRealmPageSize >= _snapshot.SecretRealmResultTotal)
        {
            return;
        }
        SecretRealmOffset += SecretRealmPageSize;
        PageRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ContentDetailTabControl_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (ReferenceEquals(e.Source, sender) && IsInitialized && ContentDetailTabControl.SelectedIndex == 0)
        {
            RebuildSecretRealmRows();
        }
        if (ReferenceEquals(e.Source, sender) && IsInitialized)
        {
            ProjectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static string FormatNature(SecretRealmNature nature) => nature switch
    {
        SecretRealmNature.Natural => "자연",
        SecretRealmNature.Artificial => "인공",
        _ => "해당 없음",
    };

    private static string FormatDiscovery(ContentDiscoveryState state) => state switch
    {
        ContentDiscoveryState.Unknown => "미발견",
        ContentDiscoveryState.Aware => "존재 인지",
        ContentDiscoveryState.Located => "위치 확인",
        ContentDiscoveryState.AccessGranted => "진입 가능",
        _ => state.ToString(),
    };

    private static string FormatStatus(ContentInstanceStatus status) => status switch
    {
        ContentInstanceStatus.Active => "활성",
        ContentInstanceStatus.Reserved => "예약",
        ContentInstanceStatus.Consumed => "소비됨",
        ContentInstanceStatus.Destroyed => "파괴됨",
        ContentInstanceStatus.Collapsed => "붕괴됨",
        _ => status.ToString(),
    };

    private sealed record SecretRealmRow(
        ContentInstanceSnapshot Instance,
        string Id,
        string Nature,
        string Location,
        string Discovery,
        string Status);
}
