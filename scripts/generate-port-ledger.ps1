param(
    [string]$SourceManifestPath,
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$pcRoot = Split-Path -Parent $PSScriptRoot
$workspaceRoot = Split-Path -Parent $pcRoot
if ([string]::IsNullOrWhiteSpace($SourceManifestPath)) {
    $SourceManifestPath = Join-Path $workspaceRoot 'data/alpha110/requirement_coverage_manifest.json'
}
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $pcRoot 'docs/traceability/pc-port-ledger.json'
}

if (-not (Test-Path -LiteralPath $SourceManifestPath -PathType Leaf)) {
    throw "Alpha 1.1.0 요구사항 정본을 찾을 수 없습니다: $SourceManifestPath"
}

$source = Get-Content -LiteralPath $SourceManifestPath -Raw | ConvertFrom-Json
if ($source.contract -ne 'ALPHA_1_1_0_REQUIREMENT_COVERAGE_V1' -or $source.requirement_count -ne 238) {
    throw '지원하지 않는 요구사항 정본입니다. ALPHA_1_1_0_REQUIREMENT_COVERAGE_V1 238행이 필요합니다.'
}

$targetVersions = @{
    M01 = '0.0.15'; M02 = '0.0.15'; M03 = '0.0.16'; M04 = '0.0.16'
    M05 = '0.0.18'; M06 = '0.0.16'; M07 = '0.0.16'; M08 = '0.0.17'
    M09 = '0.0.20'; M10 = '0.0.17'; M11 = '0.0.21'; M12 = '0.0.21'
    M13 = '0.0.21'; M14 = '0.0.22'
}

$masterGap = @{
    M01 = '공통 Stable Registry, Owner/Mutation Map, revision 계보와 정적 무결성 검사가 PC에 아직 없다.'
    M02 = '현재 schema 24 Brotli/JSON 단일 파일은 존재하지만 schema 25 SQLite 슬롯, Candidate World, backup/recovery와 독립 Queries/Runtime 경계가 없다.'
    M03 = '현재 Goal→선택 행동 trace는 있으나 Plan/Task, 객관 사실·인지·Memory 분리, Event Instance/Record와 Causal Thread가 없다.'
    M04 = 'Soul/Body/Person과 생 계보는 있으나 성별·외형·흉터, 육신 소실과 생명 owner command/result 수직 계약이 완성되지 않았다.'
    M05 = '수행 핵심은 존재하지만 모든 상호 도메인 연결, 승인된 실패 효과, 상위 경지 수직 증거와 정식 Formula Registry coverage가 미완성이다.'
    M06 = '단계형 윤회는 존재하지만 부모·가문·출생 후보, 영혼 손상과 장기 대기 예외 등 전체 수직 계약이 미완성이다.'
    M07 = '희소 관계와 Teaching은 존재하지만 일반 상호작용·대화, Knowledge Claim, 조직·평판 연결과 공통 resolution이 미완성이다.'
    M08 = '현재 지도는 3×3 호환 구역뿐이며 층천→대륙→대지역→구역→장소 Atlas, 1·2층천, 영맥과 개인 공간 지식이 없다.'
    M09 = 'Organization Core와 종문·가문·왕조 authority가 PC에 아직 없다.'
    M10 = '도시 시장 정산은 존재하지만 공통 Owner/Holder, Offer/Reservation, 복수 시장·조직 권리와 전체 경제 수직 계약이 미완성이다.'
    M11 = '간략 재료·법보는 존재하지만 비경과 Definition/Instance/Provenance, 고유 실물·창작 전승 authority가 없다.'
    M12 = 'Teaching의 지식 변화는 존재하지만 Content/Edition/Physical Copy, 손상·변조·복원과 Knowledge Claim authority가 없다.'
    M13 = '세계 AI·천도·법칙·대도 registry와 adapter authority가 PC에 아직 없다.'
    M14 = '현재 WPF 관찰 화면은 존재하지만 독립 Queries/Presentation 계약, 전체 도메인 탐색, 관심 목록·설정 복원·접근성·오류 상태가 미완성이다.'
}

$masterEvidence = @{
    M01 = @(
        'FILE::src/Chronicles.Core/Identifiers.cs::AgentId',
        'FILE::src/Chronicles.Simulation/PersonCausalModel.cs::SoulIdentityId')
    M02 = @(
        'FILE::src/Chronicles.Runtime/SimulationRunner.cs::SimulationRunner',
        'FILE::src/Chronicles.Persistence/WorldSaveCodec.cs::WorldSaveCodec',
        'FILE::src/Chronicles.Persistence/WorldSaveStore.cs::WorldSaveStore')
    M03 = @(
        'FILE::src/Chronicles.Simulation/AgentCausalRuntime.cs::AgentCausalPathState',
        'FILE::src/Chronicles.Simulation/AgentCausalRuntime.cs::CausalResolutionCode',
        'FILE::src/Chronicles.Simulation/PersonCausalModel.cs::AgentDecisionTraceState')
    M04 = @(
        'FILE::src/Chronicles.Simulation/PersonLifeAuthority.cs::PersonLifeAuthorityRules',
        'FILE::src/Chronicles.Simulation/PersonCausalModel.cs::PersonIdentitySnapshot',
        'FILE::src/Chronicles.Simulation/ReincarnationStore.cs::ReincarnationStore',
        'FILE::src/Chronicles.Simulation/SimulationModel.cs::LifeChronicleEntry')
    M05 = @(
        'FILE::src/Chronicles.Simulation/CultivationModel.cs::CultivationFormulaRegistry',
        'FILE::src/Chronicles.Simulation/RealmRules.cs::RealmRules',
        'FILE::src/Chronicles.Simulation/AptitudeModel.cs::AptitudeRules')
    M06 = @(
        'FILE::src/Chronicles.Simulation/ReincarnationModel.cs::ReincarnationRules',
        'FILE::src/Chronicles.Simulation/ReincarnationStore.cs::ReincarnationStore',
        'FILE::src/Chronicles.Simulation/ReincarnationStore.Persistence.cs::CreateCheckpoint',
        'FILE::src/Chronicles.Simulation/World.Reincarnation.cs::ResolveReincarnationStage')
    M07 = @(
        'FILE::src/Chronicles.Simulation/RelationModel.cs::SparseRelationStore',
        'FILE::src/Chronicles.Simulation/RelationModel.cs::RelationExternalContextSnapshot',
        'FILE::src/Chronicles.Simulation/TeachingModel.cs::TeachingRules',
        'FILE::src/Chronicles.Simulation/World.Teaching.cs::TryPrepareTeaching')
    M08 = @(
        'FILE::src/Chronicles.Simulation/WorldAtlasModel.cs::WorldAtlasRules',
        'FILE::src/Chronicles.Simulation/SpatialAuthorityStore.cs::SpatialAuthorityStore',
        'FILE::src/Chronicles.Simulation/WorldMapModel.cs::WorldMapRules',
        'FILE::src/Chronicles.Simulation/World.Map.cs::CreateMapSnapshot',
        'FILE::src/Chronicles.Desktop/MapObservationView.xaml.cs::MapObservationView',
        'TRACE::docs/traceability/PC-ATLAS-ECONOMY-017.md::Atlas와 이동 권위')
    M09 = @()
    M10 = @(
        'FILE::src/Chronicles.Simulation/MarketAuthorityModel.cs::EconomicOwnerRef',
        'FILE::src/Chronicles.Simulation/CityMarket.Contracts.cs::ReserveCityOffer',
        'FILE::src/Chronicles.Simulation/CityMarket.cs::CityMarket',
        'FILE::src/Chronicles.Simulation/CityMarket.Settlement.cs::SettlePurchase',
        'FILE::src/Chronicles.Simulation/EconomyRules.cs::EconomyRules',
        'TRACE::docs/traceability/PC-ECON-MARKET-013.md::구현 계약',
        'TRACE::docs/traceability/PC-ATLAS-ECONOMY-017.md::시장 권위')
    M11 = @(
        'FILE::src/Chronicles.Simulation/EconomyRules.cs::MarketGoodKind',
        'FILE::src/Chronicles.Simulation/World.Map.cs::ResolveMonsterHunt')
    M12 = @(
        'FILE::src/Chronicles.Simulation/TeachingModel.cs::TeachingTargetKind',
        'FILE::src/Chronicles.Simulation/SkillProgressionRules.cs::SkillProgressionRules')
    M13 = @()
    M14 = @(
        'FILE::src/Chronicles.Simulation/SimulationModel.cs::ObservationSections',
        'FILE::src/Chronicles.Desktop/MainWindow.xaml.cs::GetVisibleObservationSections',
        'FILE::src/Chronicles.Desktop/WorldRecordsView.xaml.cs::WorldRecordsView')
}

$masterTests = @{
    M01 = @()
    M02 = @(
        'TEST::관찰 조회 무영향',
        'TEST::저장 codec 결정론 왕복',
        'TEST::손상 저장 거부',
        'TEST::원자 저장 파일 왕복')
    M03 = @(
        'TEST::영혼·육신·현생 신원과 AI 행동 이유 권위',
        'TEST::Goal→Plan→Task→Action 인과·인지·생별 기억·외형 권위',
        'TEST::관찰 조회 무영향')
    M04 = @(
        'TEST::0세 탄생·6대 자질·일대기·윤회',
        'TEST::영혼·육신·현생 신원과 AI 행동 이유 권위',
        'TEST::Goal→Plan→Task→Action 인과·인지·생별 기억·외형 권위',
        'TEST::단계형 윤회·계보·6영역 흔적·schema 22 이전')
    M05 = @(
        'TEST::자질 50.00 대경지 1층→9층 대원만 전체 기준',
        'TEST::Notion 수행 Work·준비·Preflight·안착 상태',
        'TEST::9층 소경지·대원만 분리와 수행 Formula Trace',
        'TEST::6대 자질 60개 Component·대표값·지연 조회·저장 이전')
    M06 = @(
        'TEST::0세 탄생·6대 자질·일대기·윤회',
        'TEST::단계형 윤회·계보·6영역 흔적·schema 22 이전',
        'TEST::관찰 조회 무영향')
    M07 = @(
        'TEST::희소 방향 관계 6축·초기 Role·생애 경계',
        'TEST::사제 성립·Teaching 결과·전승 provenance·직접 수행 0%',
        'TEST::관찰 조회 무영향')
    M08 = @(
        'TEST::5층천 Atlas·1·2층천·900 영맥·개인 공간 지식',
        'TEST::3×3 지도와 지형별 요수 생태',
        'TEST::지도 내부 탭 지연 투영 성능',
        'TEST::관찰 조회 무영향')
    M09 = @()
    M10 = @(
        'TEST::도시 임무·영석·재료·단약·법보 경제 순환',
        'TEST::시장 원자 정산·중복 source·품질 개선 구매',
        'TEST::시장 Offer→Reservation→만료·원자 체결',
        'TEST::시장 소유자 용량 초과 원자 거부',
        'TEST::최근 12개월 수급 창·관찰 snapshot 재사용',
        'TEST::schema 24 시장 수급·원장 codec·source 재생 방지',
        'TEST::관찰 조회 무영향')
    M11 = @()
    M12 = @('TEST::사제 성립·Teaching 결과·전승 provenance·직접 수행 0%')
    M13 = @()
    M14 = @(
        'TEST::관찰 조회 무영향',
        'TEST::활성 UI 구역 선택 조회 성능',
        'TEST::지도 내부 탭 지연 투영 성능')
}

$portIds = @(
    'A110-M02-V09', 'A110-M02-V10', 'A110-M02-V14',
    'A110-M03-V01', 'A110-M03-V02', 'A110-M03-V03', 'A110-M03-V05',
    'A110-M03-V06', 'A110-M03-V07', 'A110-M03-V08', 'A110-M03-V10',
    'A110-M03-V11', 'A110-M03-V13', 'A110-M03-V14', 'A110-M03-V16',
    'A110-M03-V17',
    'A110-M04-V01', 'A110-M04-V02', 'A110-M04-V03', 'A110-M04-V05',
    'A110-M04-V06', 'A110-M04-V07', 'A110-M04-V08', 'A110-M04-V09',
    'A110-M04-V10', 'A110-M04-V11', 'A110-M04-V13', 'A110-M04-V14',
    'A110-M04-V15', 'A110-M04-V16', 'A110-M04-V17',
    'A110-M05-V02', 'A110-M05-V03', 'A110-M05-V05', 'A110-M05-V06',
    'A110-M05-V10', 'A110-M05-V11', 'A110-M05-V13', 'A110-M05-V15',
    'A110-M06-V02', 'A110-M06-V03', 'A110-M06-V06', 'A110-M06-V09',
    'A110-M06-V10', 'A110-M06-V11', 'A110-M06-V13', 'A110-M06-V15',
    'A110-M06-V17',
    'A110-M07-V01', 'A110-M07-V03', 'A110-M07-V04', 'A110-M07-V06',
    'A110-M07-V09', 'A110-M07-V10', 'A110-M07-V11', 'A110-M07-V13',
    'A110-M07-V15', 'A110-M07-V16', 'A110-M07-V17',
    'A110-M08-V01', 'A110-M08-V03', 'A110-M08-V04', 'A110-M08-V07',
    'A110-M08-V09', 'A110-M08-V10', 'A110-M08-V11', 'A110-M08-V13',
    'A110-M08-V14', 'A110-M08-V15', 'A110-M08-V16', 'A110-M08-V17',
    'A110-M10-V01', 'A110-M10-V03', 'A110-M10-V04', 'A110-M10-V05',
    'A110-M10-V06', 'A110-M10-V10', 'A110-M10-V11', 'A110-M10-V13',
    'A110-M10-V15', 'A110-M10-V16', 'A110-M10-V17'
)

$pcAdaptIds = @(
    'A110-M03-V12',
    'A110-M04-V12', 'A110-M05-V12', 'A110-M06-V12', 'A110-M07-V12',
    'A110-M08-V12', 'A110-M10-V12',
    'A110-M14-V01', 'A110-M14-V06', 'A110-M14-V09', 'A110-M14-V10',
    'A110-M14-V12', 'A110-M14-V14', 'A110-M14-V15'
)

$tempReviewIds = @(
    'A110-M03-V04', 'A110-M03-V09', 'A110-M03-V15',
    'A110-M04-V04',
    'A110-M05-V09', 'A110-M05-V14',
    'A110-M06-V01', 'A110-M06-V04', 'A110-M06-V05', 'A110-M06-V07',
    'A110-M06-V08', 'A110-M06-V14', 'A110-M06-V16',
    'A110-M07-V02', 'A110-M07-V05', 'A110-M07-V07', 'A110-M07-V08',
    'A110-M07-V14',
    'A110-M08-V02', 'A110-M08-V05', 'A110-M08-V06', 'A110-M08-V08',
    'A110-M10-V02', 'A110-M10-V07', 'A110-M10-V08', 'A110-M10-V09',
    'A110-M10-V14'
)

$reviewIds = @{
    M03 = @('PC-TEMP-AI-UTILITY-113', 'PC-TEMP-MEMORY-POLICY-113')
    M04 = @('PC-TEMP-APPEARANCE-DISTRIBUTION-113')
    M05 = @('PC-TEMP-TRAIT-DIST-113', 'PC-TEMP-TRAIT-ROLE-113', 'PC-TEMP-SPIRIT-ROOT-113')
    M06 = @(
        'PC-TEMP-REINCARNATION-UPPER-DURATION-113',
        'PC-TEMP-REINCARNATION-STAGES-113',
        'PC-TEMP-REINCARNATION-TRACE-113',
        'PC-TEMP-REINCARNATION-BIRTH-CANDIDATE-113',
        'PC-TEMP-REINCARNATION-SOUL-DAMAGE-113')
    M07 = @(
        'PC-TEMP-RELATION-BALANCE-113',
        'PC-TEMP-TEACHING-BALANCE-113',
        'PC-TEMP-RELATION-EXTERNAL-CONTEXT-113')
    M08 = @(
        'PC-TEMP-ATLAS-NAMES-113',
        'PC-TEMP-VEIN-PROFILES-113',
        'PC-TEMP-LAYER-ACCESS-113')
    M10 = @(
        'PC-TEMP-MARKET-WINDOW-113',
        'PC-TEMP-MARKET-LEDGER-113',
        'PC-TEMP-MARKET-BOOK-BASE-113',
        'PC-TEMP-MARKET-CONTRACT-LIFETIME-113',
        'PC-TEMP-MARKET-ORGANIZATION-CHANNEL-113')
}

$classification = @{}
foreach ($id in $portIds) { $classification[$id] = 'Port' }
foreach ($id in $pcAdaptIds) {
    if ($classification.ContainsKey($id)) { throw "분류가 중복되었습니다: $id" }
    $classification[$id] = 'PC Adapt'
}
foreach ($id in $tempReviewIds) {
    if ($classification.ContainsKey($id)) { throw "분류가 중복되었습니다: $id" }
    $classification[$id] = 'PC_TEMP_REVIEW'
}

$rootReferenceNames = @(
    'registry_ref', 'authority_owner_ref', 'command_ref', 'result_ref', 'event_ref',
    'save_ref', 'query_ref', 'test_ref', 'benchmark_ref', 'evidence_ref')

$rows = foreach ($sourceRow in $source.rows) {
    $id = [string]$sourceRow.requirement_id
    $master = [string]$sourceRow.master_id
    $disposition = if ($classification.ContainsKey($id)) {
        [string]$classification[$id]
    } else {
        'Gap/Deferred'
    }

    $targetPortMode = if ($disposition -eq 'PC_TEMP_REVIEW') {
        'PC_TEMP_REVIEW'
    } elseif ($master -eq 'M14' -or [int]$sourceRow.vertical_spec_index -eq 12) {
        'PC Adapt'
    } else {
        'Port'
    }

    $excludedClauses = @()
    if ($id -eq 'A110-M02-V17') {
        $excludedClauses += 'Android 실기기 검증은 PC 범위에서 제외하고 Windows x64 직접 실행 검증으로 대체한다.'
    }
    if ($id -eq 'A110-M14-V01') {
        $excludedClauses += '모바일 세로 UI 표면은 제외하고 Windows WPF 관찰 화면으로 대체한다.'
    }
    if ($id -eq 'A110-M14-V17') {
        $excludedClauses += 'Android 실기기 성능은 제외하고 Windows x64 UI/GC 성능으로 대체한다.'
    }

    $rootRefs = [ordered]@{}
    foreach ($name in $rootReferenceNames) {
        $rootRefs[$name] = $sourceRow.$name
    }

    $pcRefs = @($masterEvidence[$master])
    $testRefs = @($masterTests[$master])
    [ordered]@{
        requirement_id = $id
        master_id = $master
        master_title_ko = [string]$sourceRow.master_title_ko
        vertical_spec_index = [int]$sourceRow.vertical_spec_index
        vertical_spec_type_ko = [string]$sourceRow.vertical_spec_type_ko
        implementation_phase = [string]$sourceRow.implementation_phase
        source_planned_pr = [string]$sourceRow.planned_pr
        notion_url = [string]$sourceRow.notion_url
        notion_requirement_text_ko = [string]$sourceRow.notion_requirement_text_ko
        notion_requirement_sha256 = [string]$sourceRow.notion_requirement_sha256
        notion_master_requirement_sha256 = [string]$sourceRow.notion_master_requirement_sha256
        source_disposition = [string]$sourceRow.disposition
        source_evidence_state = [string]$sourceRow.evidence_state
        source_root_refs = $rootRefs
        disposition = $disposition
        target_port_mode = $targetPortMode
        target_version = if ($disposition -eq 'Gap/Deferred') { $targetVersions[$master] } else { $null }
        pc_evidence_refs = $pcRefs
        pc_test_refs = $testRefs
        review_ids = if ($disposition -eq 'PC_TEMP_REVIEW') { @($reviewIds[$master]) } else { @() }
        excluded_platform_clauses = $excludedClauses
        adaptation_note = if ($targetPortMode -eq 'PC Adapt') {
            'Android/모바일 전달 표면을 제외하고 같은 관찰·조회 의미를 Windows WPF와 Windows x64 검증으로 제공한다.'
        } else {
            $null
        }
        gap_summary = if ($disposition -eq 'Gap/Deferred') { $masterGap[$master] } else { $null }
    }
}

$sourceGitSha = $null
try {
    $sourceGitSha = (& git -C $workspaceRoot rev-parse HEAD 2>$null).Trim()
} catch {
    $sourceGitSha = $null
}

$statusCounts = [ordered]@{}
foreach ($name in @('Port', 'PC Adapt', 'PC_TEMP_REVIEW', 'Platform Excluded', 'Gap/Deferred')) {
    $statusCounts[$name] = @($rows | Where-Object disposition -eq $name).Count
}

$ledger = [ordered]@{
    schema_version = 1
    contract = 'CMIP_PC_PORT_LEDGER_V1'
    ledger_revision = 1
    requirement_count = 238
    source_contract = [string]$source.contract
    source_manifest_path = 'data/alpha110/requirement_coverage_manifest.json'
    source_manifest_sha256 = (Get-FileHash -LiteralPath $SourceManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
    source_git_sha = $sourceGitSha
    source_traceability_refs = @(
        'docs/traceability/alpha-1.1.0-vertical-acceptance.md',
        'docs/traceability/alpha-1.1.2-release-scope.md',
        'docs/traceability/alpha-1.1.2-release-evidence.md')
    pc_baseline_version = '0.0.13'
    pc_baseline_git_sha = '558a184d2cf88b0f1a2318eb028125257786d261'
    allowed_dispositions = @('Port', 'PC Adapt', 'PC_TEMP_REVIEW', 'Platform Excluded', 'Gap/Deferred')
    status_semantics = [ordered]@{
        Port = 'PC에서 요구 문장 전체의 권위·조회·검증 증거가 연결됨.'
        'PC Adapt' = '모바일 전달 표면을 PC로 바꿨으며 PC 범위 요구 문장 전체의 증거가 연결됨.'
        PC_TEMP_REVIEW = 'PC 동작과 검증은 있으나 하나 이상의 수치·공식이 안정 review ID로 다음 검토에 이관됨.'
        'Platform Excluded' = '행 전체가 Android 전용이라 PC 제품 범위에서 제외됨.'
        'Gap/Deferred' = '요구 문장 일부 또는 전부를 증명하지 못해 완료로 주장하지 않음.'
    }
    status_counts = $statusCounts
    rows = @($rows)
}

$json = $ledger | ConvertTo-Json -Depth 16
[System.IO.File]::WriteAllText($OutputPath, $json + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
Write-Host "PC port ledger generated: $OutputPath"
Write-Host ($statusCounts | ConvertTo-Json -Compress)
