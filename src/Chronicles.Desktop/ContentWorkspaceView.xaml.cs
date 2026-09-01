using System.Windows;
using System.Windows.Controls;
using Chronicles.Core;
using Chronicles.Presentation;
using Chronicles.Simulation;

namespace Chronicles.Desktop;

public partial class ContentWorkspaceView : UserControl
{
    public const int SecretRealmPageSize = 100;
    public const int ItemPageSize = 100;
    public const int KnowledgeEditionPageSize = 100;
    public const int LawCandidatePageSize = LawAuthorityRules.CandidateCount;

    private WorldContentSnapshot _snapshot = WorldContentSnapshot.Empty;
    private long _projectedRevision = long.MinValue;
    private int _projectedOffset = -1;
    private int _projectedSecretRealmCount = -1;
    private int _projectedItemOffset = -1;
    private int _projectedItemCount = -1;
    private long _projectedKnowledgeRevision = long.MinValue;
    private int _projectedKnowledgeOffset = -1;
    private int _projectedKnowledgeCount = -1;
    private long _projectedLawRevision = long.MinValue;
    private int _projectedLawOffset = -1;
    private int _projectedLawCount = -1;
    private SecretRealmNature? _natureFilter;

    public ContentWorkspaceView()
    {
        InitializeComponent();
        ClearDetails();
        ClearItemDetails();
        ClearKnowledgeDetails();
        ClearLawDetails();
    }

    public event EventHandler? PageRequested;

    public event EventHandler? ProjectionChanged;

    public int SecretRealmOffset { get; private set; }

    public int ItemOffset { get; private set; }

    public bool ShouldLoadSecretRealmRows => ContentDetailTabControl.SelectedIndex == 0;

    public bool ShouldLoadItemRows => ContentDetailTabControl.SelectedIndex == 1;

    public bool ShouldLoadKnowledgeRows => ContentDetailTabControl.SelectedIndex == 2;

    public bool ShouldLoadLawRows => ContentDetailTabControl.SelectedIndex == 3;

    public void ResetProjection()
    {
        _snapshot = WorldContentSnapshot.Empty;
        _projectedRevision = long.MinValue;
        _projectedOffset = -1;
        _projectedSecretRealmCount = -1;
        _projectedItemOffset = -1;
        _projectedItemCount = -1;
        _projectedKnowledgeRevision = long.MinValue;
        _projectedKnowledgeOffset = -1;
        _projectedKnowledgeCount = -1;
        _projectedLawRevision = long.MinValue;
        _projectedLawOffset = -1;
        _projectedLawCount = -1;
        SecretRealmOffset = 0;
        ItemOffset = 0;
        SecretRealmGrid.ItemsSource = Array.Empty<SecretRealmRow>();
        ItemInstanceGrid.ItemsSource = Array.Empty<ItemInstanceRow>();
        KnowledgeEditionGrid.ItemsSource = Array.Empty<KnowledgeEditionRow>();
        LawCandidateGrid.ItemsSource = Array.Empty<LawCandidateRow>();
        ClearDetails();
        ClearItemDetails();
        ClearKnowledgeDetails();
        ClearLawDetails();
        ApplySummary();
    }

    public void Apply(WorldContentSnapshot? snapshot)
    {
        snapshot ??= WorldContentSnapshot.Empty;
        var knowledge = snapshot.Knowledge ?? WorldKnowledgeSnapshot.Empty;
        var laws = snapshot.Laws ?? WorldLawSnapshot.Empty;
        var unchanged = _projectedRevision == snapshot.AuthorityRevision
            && _projectedOffset == snapshot.SecretRealmResultOffset
            && _projectedSecretRealmCount == snapshot.SecretRealms.Count
            && _projectedItemOffset == snapshot.ItemResultOffset
            && _projectedItemCount == snapshot.ItemsAndMaterials.Count
            && _projectedKnowledgeRevision == knowledge.AuthorityRevision
            && _projectedKnowledgeOffset == knowledge.EditionResultOffset
            && _projectedKnowledgeCount == knowledge.Editions.Count
            && _projectedLawRevision == laws.AuthorityRevision
            && _projectedLawOffset == laws.CandidateResultOffset
            && _projectedLawCount == laws.Candidates.Count
            && _snapshot.IncludesHidden == snapshot.IncludesHidden;
        _snapshot = snapshot;
        SecretRealmOffset = snapshot.SecretRealmResultOffset;
        ItemOffset = snapshot.ItemResultOffset;
        if (unchanged)
        {
            return;
        }

        _projectedRevision = snapshot.AuthorityRevision;
        _projectedOffset = snapshot.SecretRealmResultOffset;
        _projectedSecretRealmCount = snapshot.SecretRealms.Count;
        _projectedItemOffset = snapshot.ItemResultOffset;
        _projectedItemCount = snapshot.ItemsAndMaterials.Count;
        _projectedKnowledgeRevision = knowledge.AuthorityRevision;
        _projectedKnowledgeOffset = knowledge.EditionResultOffset;
        _projectedKnowledgeCount = knowledge.Editions.Count;
        _projectedLawRevision = laws.AuthorityRevision;
        _projectedLawOffset = laws.CandidateResultOffset;
        _projectedLawCount = laws.Candidates.Count;
        ApplySummary();
        if (ShouldLoadSecretRealmRows)
        {
            RebuildSecretRealmRows();
        }
        if (ShouldLoadItemRows)
        {
            RebuildItemRows();
        }
        if (ShouldLoadKnowledgeRows)
        {
            RebuildKnowledgeRows();
        }
        if (ShouldLoadLawRows)
        {
            RebuildLawRows();
        }
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
        CreatedWorkCountText.Text = KoreanNumberFormatter.Format(_snapshot.CreatedWorkCount);
        PhysicalRecordCountText.Text = KoreanNumberFormatter.Format(_snapshot.PhysicalRecordCount);
        var knowledge = _snapshot.Knowledge ?? WorldKnowledgeSnapshot.Empty;
        KnowledgeContentCountText.Text = KoreanNumberFormatter.Format(knowledge.ContentCount);
        KnowledgeEditionCountText.Text = KoreanNumberFormatter.Format(knowledge.EditionCount);
        KnowledgeAccessCountText.Text = KoreanNumberFormatter.Format(knowledge.AccessCount);
        KnowledgeStatusText.Text =
            $"오해 {KoreanNumberFormatter.Format(knowledge.MisunderstoodAccessCount)}건 · " +
            $"복원 판본 {KoreanNumberFormatter.Format(knowledge.RestoredEditionCount)}개 · " +
            "미확정 신뢰도는 DataUnavailable로 유지합니다.";
        var laws = _snapshot.Laws ?? WorldLawSnapshot.Empty;
        LawCandidateCountText.Text =
            $"{KoreanNumberFormatter.Format(laws.CandidateCount)} · " +
            $"기초 {laws.BaseCandidateCount:N0} / 파생 {laws.DerivedCandidateCount:N0}";
        LawReadyCountText.Text = KoreanNumberFormatter.Format(laws.ReadyCandidateCount);
        StableLawCountText.Text = KoreanNumberFormatter.Format(laws.StableLawCount);
        PersonalLawStateCountText.Text = KoreanNumberFormatter.Format(laws.PersonalStateCount);
        LawJudgmentCountText.Text =
            $"{KoreanNumberFormatter.Format(laws.WorldAiJudgmentCount)} · " +
            $"{KoreanNumberFormatter.Format(laws.TiandaoJudgmentCount)}";
        VisibilityModeText.Text = _snapshot.IncludesHidden ? "세계 설정 진단" : "공개 지식";
    }

    private void RebuildKnowledgeRows()
    {
        var knowledge = _snapshot.Knowledge ?? WorldKnowledgeSnapshot.Empty;
        var selectedId = KnowledgeEditionGrid.SelectedItem is KnowledgeEditionRow selected
            ? selected.Snapshot.EditionId
            : (KnowledgeEditionId?)null;
        var rows = knowledge.Editions.Select(value => new KnowledgeEditionRow(
            value,
            FormatKnowledgeContent(value),
            value.EditionId.ToString(),
            FormatEditionRelation(value.Relation),
            KoreanNumberFormatter.Format(value.PhysicalRecordCount),
            KoreanNumberFormatter.Format(value.AccessorCount))).ToArray();
        KnowledgeEditionGrid.ItemsSource = rows;
        KnowledgeEditionGrid.SelectedItem = selectedId is { } id
            ? rows.FirstOrDefault(row => row.Snapshot.EditionId == id) ?? rows.FirstOrDefault()
            : rows.FirstOrDefault();
        RenderSelectedKnowledgeEdition();
    }

    private void RebuildItemRows()
    {
        var selectedId = ItemInstanceGrid.SelectedItem is ItemInstanceRow selected
            ? selected.Instance.Id
            : (ContentInstanceId?)null;
        var rows = _snapshot.ItemsAndMaterials.Select(instance => new ItemInstanceRow(
            instance,
            instance.Id.ToString(),
            instance.DefinitionName,
            FormatInstanceKind(instance.Kind),
            $"지점 {instance.LocationId + 1}",
            FormatOwner(instance.Owner),
            FormatStatus(instance.Status))).ToArray();
        ItemInstanceGrid.ItemsSource = rows;
        ItemInstanceGrid.SelectedItem = selectedId is { } id
            ? rows.FirstOrDefault(row => row.Instance.Id == id) ?? rows.FirstOrDefault()
            : rows.FirstOrDefault();

        var first = _snapshot.ItemResultTotal == 0 ? 0 : _snapshot.ItemResultOffset + 1;
        var last = Math.Min(
            _snapshot.ItemResultTotal,
            _snapshot.ItemResultOffset + _snapshot.ItemsAndMaterials.Count);
        ItemListStatusText.Text = rows.Length == 0
            ? "표시할 아이템·재료 Instance가 없습니다."
            : $"현재 페이지 {rows.Length:N0}개";
        ItemPageText.Text = $"{first:N0}–{last:N0} / {_snapshot.ItemResultTotal:N0}";
        PreviousItemPageButton.IsEnabled = _snapshot.ItemResultOffset > 0;
        NextItemPageButton.IsEnabled = last < _snapshot.ItemResultTotal;
        RenderSelectedItem();
    }

    private void RenderSelectedItem()
    {
        if (ItemInstanceGrid.SelectedItem is not ItemInstanceRow row)
        {
            ClearItemDetails();
            return;
        }

        var value = row.Instance;
        ItemTitleText.Text = $"{value.DefinitionName} · {value.Id}";
        ItemIdentityText.Text = $"{value.DefinitionId} · {FormatInstanceKind(value.Kind)}";
        ItemOwnerText.Text =
            $"소유 {FormatOwner(value.Owner)} · 보유 {value.Holder.Kind}:{value.Holder.Id:N0}\n" +
            $"지점 {value.LocationId + 1:N0} · 수량 {value.Quantity:N0}";
        ItemQualityText.Text = value.QualityAvailability == ContentQualityAvailability.Specified
            ? $"품질 {value.QualityHundredths / 100d:0.00} · 내구 {value.ConditionHundredths / 100d:0.00}"
            : $"품질 DataUnavailable · 내구 {value.ConditionHundredths / 100d:0.00}";
        ItemAuthorityText.Text =
            $"{FormatStatus(value.Status)} · revision {value.AuthorityRevision:N0}\n" +
            $"최신 계보 {value.LatestProvenanceId}";
    }

    private void RenderSelectedKnowledgeEdition()
    {
        if (KnowledgeEditionGrid.SelectedItem is not KnowledgeEditionRow row)
        {
            ClearKnowledgeDetails();
            return;
        }

        var value = row.Snapshot;
        KnowledgeEditionTitleText.Text = $"{row.Content} · {row.Edition}";
        KnowledgeEditionIdentityText.Text = $"{value.ContentId} · {value.ContentKind}";
        KnowledgeEditionLineageText.Text = value.ParentEditionId is { } parent
            ? $"{FormatEditionRelation(value.Relation)} · 원본 계보 {parent}"
            : "최초 원본 Edition · parent 없음";
        KnowledgeEditionStateText.Text =
            $"실물 {KoreanNumberFormatter.Format(value.PhysicalRecordCount)}권 · " +
            $"접근 Owner {KoreanNumberFormatter.Format(value.AccessorCount)}명/조직 · " +
            $"신뢰 {FormatKnowledgeValue(value.ConfidenceAvailability, value.ConfidenceHundredths)}";
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

    private void RebuildLawRows()
    {
        var laws = _snapshot.Laws ?? WorldLawSnapshot.Empty;
        var selectedOrdinal = LawCandidateGrid.SelectedItem is LawCandidateRow selected
            ? selected.Snapshot.Candidate.Ordinal
            : (byte?)null;
        var rows = laws.Candidates.Select(value => new LawCandidateRow(
            value,
            value.Candidate.Kind == LawCandidateKind.Base
                ? $"기초 법칙 후보 {value.Candidate.Ordinal + 1:00}"
                : $"파생 법칙 후보 " +
                    $"{value.Candidate.Ordinal - LawAuthorityRules.BaseCandidateCount + 1:00}",
            value.Candidate.Kind == LawCandidateKind.Base ? "기초" : "파생",
            value.MissingEvidence == LawEvidenceFacet.None ? "준비 완료" : "검토 중",
            KoreanNumberFormatter.Format(value.ApplicationCount),
            KoreanNumberFormatter.Format(value.PersonalStateCount))).ToArray();
        LawCandidateGrid.ItemsSource = rows;
        LawCandidateGrid.SelectedItem = selectedOrdinal is { } ordinal
            ? rows.FirstOrDefault(row => row.Snapshot.Candidate.Ordinal == ordinal)
                ?? rows.FirstOrDefault()
            : rows.FirstOrDefault();
        RenderSelectedLawCandidate();
    }

    private void RenderSelectedLawCandidate()
    {
        if (LawCandidateGrid.SelectedItem is not LawCandidateRow row)
        {
            ClearLawDetails();
            return;
        }

        var value = row.Snapshot;
        LawCandidateTitleText.Text = row.Candidate;
        if (_snapshot.IncludesHidden)
        {
            LawCandidateIdentityText.Text =
                $"{value.Candidate} · {row.Kind} ordinal {value.Candidate.Ordinal:N0} · " +
                "Stable LawId 미발급";
            LawCandidateContractText.Text =
                $"Scope {FormatLawAvailability(value.ScopeAvailability)} · " +
                $"Relation {FormatLawAvailability(value.RelationAvailability)}\n" +
                $"실제 적용 {value.ApplicationCount:N0} · 개인 이해 {value.PersonalStateCount:N0} · " +
                $"세계 판단 {value.WorldJudgmentCount:N0}";
            LawCandidateEvidenceText.Text = value.MissingEvidence == LawEvidenceFacet.None
                ? "Adapter·Result·Metric·State I/O·SQLite·Save/Replay 증거가 모두 준비되었습니다."
                : $"부족 증거: {FormatMissingLawEvidence(value.MissingEvidence)}";
            LawStatusText.Text =
                "개발 진단: 객관 Law, 개인 이해, 세계 AI, 천도는 별도 권위입니다. " +
                "실행 증거 Gate 전에는 후보 효과를 적용하지 않습니다.";
            return;
        }

        LawCandidateIdentityText.Text = "객관 법칙 후보 · 안정 법칙 ID는 아직 발급되지 않았습니다.";
        LawCandidateContractText.Text =
            $"관찰된 실제 작용 {value.ApplicationCount:N0}회 · " +
            $"개인 이해 기록 {value.PersonalStateCount:N0}건";
        LawCandidateEvidenceText.Text = value.MissingEvidence == LawEvidenceFacet.None
            ? "실행 준비가 검증되었습니다."
            : "실행 검증 중이며 공개적으로 확정된 작용 범위와 관계가 없습니다.";
        LawStatusText.Text =
            "개인의 이론이나 오해가 세계의 객관 법칙으로 자동 승격되지 않습니다.";
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

    private void ClearKnowledgeDetails()
    {
        KnowledgeEditionTitleText.Text = "판본을 선택해 주세요.";
        KnowledgeEditionIdentityText.Text = "의미 내용과 물리 서적은 서로 다른 Stable Ref로 관리됩니다.";
        KnowledgeEditionLineageText.Text = "판본 계보와 실물 binding을 선택한 경우에만 표시합니다.";
        KnowledgeEditionStateText.Text = "—";
    }

    private void ClearItemDetails()
    {
        ItemTitleText.Text = "아이템·재료를 선택해 주세요.";
        ItemIdentityText.Text = "Definition과 실제 Instance를 분리해 표시합니다.";
        ItemOwnerText.Text = "—";
        ItemQualityText.Text = "—";
        ItemAuthorityText.Text = "—";
    }

    private void ClearLawDetails()
    {
        LawCandidateTitleText.Text = "법칙 후보를 선택해 주세요.";
        LawCandidateIdentityText.Text = "Stable LawId는 실행 증거 Gate 이후에만 발급됩니다.";
        LawCandidateContractText.Text = "Scope · Relation · 실제 Application을 분리해 표시합니다.";
        LawCandidateEvidenceText.Text = "—";
        LawStatusText.Text =
            "기획 완료 상태를 실행 READY로 오표시하지 않습니다.";
    }

    private void SecretRealmGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        RenderSelectedRealm();

    private void ItemInstanceGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        RenderSelectedItem();

    private void KnowledgeEditionGrid_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e) => RenderSelectedKnowledgeEdition();

    private void LawCandidateGrid_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e) => RenderSelectedLawCandidate();

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

    private void PreviousItemPageButton_Click(object sender, RoutedEventArgs e)
    {
        ItemOffset = Math.Max(0, ItemOffset - ItemPageSize);
        PageRequested?.Invoke(this, EventArgs.Empty);
    }

    private void NextItemPageButton_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOffset + ItemPageSize >= _snapshot.ItemResultTotal)
        {
            return;
        }
        ItemOffset += ItemPageSize;
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
        if (ReferenceEquals(e.Source, sender) && IsInitialized && ContentDetailTabControl.SelectedIndex == 1)
        {
            RebuildItemRows();
        }
        if (ReferenceEquals(e.Source, sender) && IsInitialized && ContentDetailTabControl.SelectedIndex == 2)
        {
            RebuildKnowledgeRows();
        }
        if (ReferenceEquals(e.Source, sender) && IsInitialized && ContentDetailTabControl.SelectedIndex == 3)
        {
            RebuildLawRows();
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

    private static string FormatInstanceKind(ContentInstanceKind kind) => kind switch
    {
        ContentInstanceKind.MaterialStack => "재료 stack",
        ContentInstanceKind.Item => "법보·단약",
        ContentInstanceKind.CreatedWork => "독립 전승",
        ContentInstanceKind.PhysicalRecord => "물리 기록",
        ContentInstanceKind.SecretRealm => "비경",
        _ => kind.ToString(),
    };

    private static string FormatOwner(ContentOwnerRef owner) => owner.Kind switch
    {
        ContentOwnerKind.World => "세계",
        ContentOwnerKind.Agent => $"인물 {owner.Id + 1:N0}",
        ContentOwnerKind.City => $"도시 {owner.Id + 1:N0}",
        ContentOwnerKind.Organization => $"조직 {owner.Id:N0}",
        _ => $"{owner.Kind}:{owner.Id:N0}",
    };

    private static string FormatKnowledgeContent(KnowledgeEditionSnapshot value) =>
        value.ContentKind switch
        {
            KnowledgeContentKind.Technique => $"공법 전승 {value.CatalogOrdinal:000}",
            KnowledgeContentKind.Spell => $"법술 전승 {value.CatalogOrdinal:000}",
            KnowledgeContentKind.CraftingMethod => $"제작법 {value.CatalogOrdinal:000}",
            KnowledgeContentKind.HistoricalRecord => $"역사 기록 {value.CatalogOrdinal:000}",
            _ => value.ContentId.ToString(),
        };

    private static string FormatEditionRelation(KnowledgeEditionRelation relation) => relation switch
    {
        KnowledgeEditionRelation.Original => "원본",
        KnowledgeEditionRelation.Copy => "필사",
        KnowledgeEditionRelation.Revision => "개정",
        KnowledgeEditionRelation.Summary => "요약",
        KnowledgeEditionRelation.Translation => "번역",
        KnowledgeEditionRelation.Tampered => "변조",
        KnowledgeEditionRelation.Restored => "복원",
        KnowledgeEditionRelation.Reconstructed => "재구성",
        _ => relation.ToString(),
    };

    private static string FormatKnowledgeValue(
        KnowledgeValueAvailability availability,
        ushort hundredths) => availability == KnowledgeValueAvailability.Specified
            ? $"{hundredths / 100d:0.00}"
            : "DataUnavailable";

    private static string FormatLawAvailability(LawContractAvailability availability) =>
        availability == LawContractAvailability.Specified
            ? "Specified"
            : "DataUnavailable";

    private static string FormatMissingLawEvidence(LawEvidenceFacet value)
    {
        var names = new List<string>(7);
        if ((value & LawEvidenceFacet.Adapter) != 0) names.Add("Adapter");
        if ((value & LawEvidenceFacet.ResultContract) != 0) names.Add("Result");
        if ((value & LawEvidenceFacet.Metric) != 0) names.Add("Metric");
        if ((value & LawEvidenceFacet.StateInput) != 0) names.Add("State Input");
        if ((value & LawEvidenceFacet.StateOutput) != 0) names.Add("State Output");
        if ((value & LawEvidenceFacet.SqliteRoundTrip) != 0) names.Add("SQLite");
        if ((value & LawEvidenceFacet.SaveReplay) != 0) names.Add("Save/Replay");
        return string.Join(" · ", names);
    }

    private sealed record SecretRealmRow(
        ContentInstanceSnapshot Instance,
        string Id,
        string Nature,
        string Location,
        string Discovery,
        string Status);

    private sealed record ItemInstanceRow(
        ContentInstanceSnapshot Instance,
        string Id,
        string Definition,
        string Kind,
        string Location,
        string Owner,
        string Status);

    private sealed record KnowledgeEditionRow(
        KnowledgeEditionSnapshot Snapshot,
        string Content,
        string Edition,
        string Relation,
        string Records,
        string Accessors);

    private sealed record LawCandidateRow(
        LawCandidateSnapshot Snapshot,
        string Candidate,
        string Kind,
        string Readiness,
        string Applications,
        string Personal);
}
