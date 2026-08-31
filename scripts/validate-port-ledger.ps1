param(
    [string]$LedgerPath,
    [string]$SourceManifestPath,
    [string]$ArchitectureManifestPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$pcRoot = Split-Path -Parent $PSScriptRoot
$workspaceRoot = Split-Path -Parent $pcRoot
if ([string]::IsNullOrWhiteSpace($LedgerPath)) {
    $LedgerPath = Join-Path $pcRoot 'docs/traceability/pc-port-ledger.json'
}
if ([string]::IsNullOrWhiteSpace($ArchitectureManifestPath)) {
    $ArchitectureManifestPath = Join-Path $pcRoot 'docs/architecture/repository_manifest.json'
}
if ([string]::IsNullOrWhiteSpace($SourceManifestPath)) {
    $candidate = Join-Path $workspaceRoot 'data/alpha110/requirement_coverage_manifest.json'
    if (Test-Path -LiteralPath $candidate -PathType Leaf) {
        $SourceManifestPath = $candidate
    }
}

function Assert-Condition {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

function Get-JsonPropertyValue {
    param([object]$Object, [string]$Name)
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Assert-EvidenceReference {
    param([string]$Reference)

    if ($Reference -match '^(FILE|TRACE)::([^:]+)::(.+)$') {
        $relativePath = $Matches[2]
        $literal = $Matches[3]
        $path = Join-Path $pcRoot $relativePath
        Assert-Condition (Test-Path -LiteralPath $path -PathType Leaf) "근거 파일이 없습니다: $Reference"
        $content = Get-Content -LiteralPath $path -Raw
        Assert-Condition $content.Contains($literal, [StringComparison]::Ordinal) "근거 literal이 없습니다: $Reference"
        return
    }

    if ($Reference -match '^TEST::(.+)$') {
        $testName = $Matches[1]
        $testRunner = Join-Path $pcRoot 'tests/Chronicles.Tests/Program.cs'
        $content = Get-Content -LiteralPath $testRunner -Raw
        Assert-Condition $content.Contains($testName, [StringComparison]::Ordinal) "테스트 이름이 없습니다: $Reference"
        return
    }

    throw "알 수 없는 PC 근거 형식입니다: $Reference"
}

Assert-Condition (Test-Path -LiteralPath $LedgerPath -PathType Leaf) "PC 이식 원장이 없습니다: $LedgerPath"
$ledger = Get-Content -LiteralPath $LedgerPath -Raw | ConvertFrom-Json

Assert-Condition ($ledger.contract -eq 'CMIP_PC_PORT_LEDGER_V1') '원장 contract가 CMIP_PC_PORT_LEDGER_V1이 아닙니다.'
Assert-Condition ($ledger.schema_version -eq 1) '원장 schema_version은 1이어야 합니다.'
Assert-Condition ($ledger.requirement_count -eq 238) '원장 requirement_count는 238이어야 합니다.'
Assert-Condition (@($ledger.rows).Count -eq 238) '원장 rows는 정확히 238행이어야 합니다.'
Assert-Condition ($ledger.source_contract -eq 'ALPHA_1_1_0_REQUIREMENT_COVERAGE_V1') '요구사항 정본 contract가 다릅니다.'
Assert-Condition ([string]$ledger.source_manifest_sha256 -match '^[0-9a-f]{64}$') 'source_manifest_sha256 형식이 잘못되었습니다.'
Assert-Condition ([string]$ledger.pc_baseline_git_sha -match '^[0-9a-f]{40}$') 'PC 기준 Git SHA 형식이 잘못되었습니다.'

$reviewEvidencePaths = @(
    Get-ChildItem -LiteralPath (Join-Path $pcRoot 'src'), (Join-Path $pcRoot 'docs') -Recurse -File |
        Where-Object {
            $_.FullName -ne [System.IO.Path]::GetFullPath($LedgerPath) -and
            $_.Extension -in @('.cs', '.md', '.xaml', '.json')
        } |
        ForEach-Object FullName
)
$reviewEvidenceCorpus = [System.Text.StringBuilder]::new()
foreach ($path in $reviewEvidencePaths) {
    [void]$reviewEvidenceCorpus.AppendLine((Get-Content -LiteralPath $path -Raw))
}
$reviewEvidenceText = $reviewEvidenceCorpus.ToString()

function Test-ReviewIdEvidence {
    param([string]$ReviewId)
    return $reviewEvidenceText.Contains($ReviewId, [StringComparison]::Ordinal)
}

$allowed = @('Port', 'PC Adapt', 'PC_TEMP_REVIEW', 'Platform Excluded', 'Gap/Deferred')
$declaredAllowed = @($ledger.allowed_dispositions)
Assert-Condition ($declaredAllowed.Count -eq $allowed.Count) 'allowed_dispositions 개수가 잘못되었습니다.'
foreach ($status in $allowed) {
    Assert-Condition ($declaredAllowed -contains $status) "허용 disposition 선언이 누락되었습니다: $status"
}

$ids = @{}
$masterRows = @{}
foreach ($masterNumber in 1..14) {
    $masterRows[('M{0:D2}' -f $masterNumber)] = @()
}

foreach ($row in @($ledger.rows)) {
    $id = [string]$row.requirement_id
    Assert-Condition ($id -match '^A110-M(0[1-9]|1[0-4])-V(0[1-9]|1[0-7])$') "잘못된 요구사항 ID입니다: $id"
    Assert-Condition (-not $ids.ContainsKey($id)) "중복 요구사항 ID입니다: $id"
    $ids[$id] = $true

    $master = [string]$row.master_id
    Assert-Condition ($masterRows.ContainsKey($master)) "잘못된 master_id입니다: $id / $master"
    $masterRows[$master] += $row
    Assert-Condition ([int]$row.vertical_spec_index -ge 1 -and [int]$row.vertical_spec_index -le 17) "vertical index 범위 오류: $id"
    Assert-Condition (-not [string]::IsNullOrWhiteSpace([string]$row.notion_requirement_text_ko)) "요구사항 원문이 비었습니다: $id"
    Assert-Condition ([string]$row.notion_requirement_sha256 -match '^[0-9a-f]{64}$') "요구사항 SHA 형식 오류: $id"
    Assert-Condition ([string]$row.notion_master_requirement_sha256 -match '^[0-9a-f]{64}$') "Master SHA 형식 오류: $id"

    $disposition = [string]$row.disposition
    Assert-Condition ($allowed -contains $disposition) "허용되지 않은 disposition입니다: $id / $disposition"
    Assert-Condition (@('Port', 'PC Adapt', 'PC_TEMP_REVIEW') -contains [string]$row.target_port_mode) "잘못된 target_port_mode입니다: $id"

    $pcEvidence = @($row.pc_evidence_refs)
    $pcTests = @($row.pc_test_refs)
    foreach ($reference in @($pcEvidence + $pcTests)) {
        Assert-EvidenceReference ([string]$reference)
    }

    if ($disposition -eq 'Gap/Deferred') {
        Assert-Condition (-not [string]::IsNullOrWhiteSpace([string]$row.gap_summary)) "Gap 설명이 없습니다: $id"
        Assert-Condition ([string]$row.target_version -match '^0\.0\.(1[5-9]|2[0-2])$') "Gap target_version 형식 오류: $id"
    } else {
        Assert-Condition ($pcEvidence.Count -gt 0) "완료/임시 분류에 PC 파일 근거가 없습니다: $id"
        Assert-Condition ($pcTests.Count -gt 0) "완료/임시 분류에 PC 테스트 근거가 없습니다: $id"
        Assert-Condition ($null -eq $row.target_version) "완료/임시 분류에 target_version이 남았습니다: $id"
    }

    $reviewIds = @($row.review_ids | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) })
    if ($disposition -eq 'PC_TEMP_REVIEW') {
        Assert-Condition ($reviewIds.Count -gt 0) "PC_TEMP_REVIEW에 review ID가 없습니다: $id"
        foreach ($reviewId in $reviewIds) {
            Assert-Condition ([string]$reviewId -match '^PC-TEMP-[A-Z0-9-]+$') "review ID 형식 오류: $id / $reviewId"
            Assert-Condition (Test-ReviewIdEvidence ([string]$reviewId)) "review ID의 실제 선언/문서 근거가 없습니다: $id / $reviewId"
        }
    } else {
        Assert-Condition ($reviewIds.Count -eq 0) "PC_TEMP_REVIEW가 아닌 행에 review ID가 있습니다: $id"
    }

    if ([string]$row.target_port_mode -eq 'PC Adapt') {
        Assert-Condition (-not [string]::IsNullOrWhiteSpace([string]$row.adaptation_note)) "PC Adapt 설명이 없습니다: $id"
    }
    if ($disposition -eq 'Platform Excluded') {
        $excludedClauses = @($row.excluded_platform_clauses | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) })
        Assert-Condition ($excludedClauses.Count -gt 0) "Platform Excluded 이유가 없습니다: $id"
    }
}

foreach ($masterNumber in 1..14) {
    $master = 'M{0:D2}' -f $masterNumber
    $rows = @($masterRows[$master])
    Assert-Condition ($rows.Count -eq 17) "$master 행 수가 17이 아닙니다: $($rows.Count)"
    $indices = @($rows | ForEach-Object { [int]$_.vertical_spec_index } | Sort-Object -Unique)
    Assert-Condition ($indices.Count -eq 17) "$master vertical index가 중복되거나 누락되었습니다."
    foreach ($index in 1..17) {
        Assert-Condition ($indices -contains $index) "$master V$('{0:D2}' -f $index)가 누락되었습니다."
    }
}

foreach ($status in $allowed) {
    $actual = @($ledger.rows | Where-Object disposition -eq $status).Count
    $declared = [int](Get-JsonPropertyValue $ledger.status_counts $status)
    Assert-Condition ($actual -eq $declared) "status_counts가 실제 행 수와 다릅니다: $status ($declared / $actual)"
}

Assert-Condition (Test-Path -LiteralPath $ArchitectureManifestPath -PathType Leaf) "PC architecture manifest가 없습니다: $ArchitectureManifestPath"
$architecture = Get-Content -LiteralPath $ArchitectureManifestPath -Raw | ConvertFrom-Json
Assert-Condition ($architecture.contract -eq 'CMIP_PC_REPOSITORY_MANIFEST_V1') 'PC architecture manifest contract가 잘못되었습니다.'
Assert-Condition ($architecture.schema_version -eq 1) 'PC architecture manifest schema_version은 1이어야 합니다.'
$modules = @($architecture.modules)
Assert-Condition ($modules.Count -eq 8) 'PC production module은 8개여야 합니다.'
$moduleById = @{}
$moduleByProject = @{}
foreach ($module in $modules) {
    $moduleId = [string]$module.id
    Assert-Condition (-not [string]::IsNullOrWhiteSpace($moduleId)) '빈 module ID가 있습니다.'
    Assert-Condition (-not $moduleById.ContainsKey($moduleId)) "중복 module ID입니다: $moduleId"
    $moduleById[$moduleId] = $module
    $projectPath = [System.IO.Path]::GetFullPath((Join-Path $pcRoot ([string]$module.project)))
    Assert-Condition (Test-Path -LiteralPath $projectPath -PathType Leaf) "module project가 없습니다: $moduleId / $projectPath"
    Assert-Condition (-not $moduleByProject.ContainsKey($projectPath)) "두 module이 같은 project를 소유합니다: $projectPath"
    $moduleByProject[$projectPath] = $moduleId
    foreach ($root in @($module.roots)) {
        Assert-Condition (Test-Path -LiteralPath (Join-Path $pcRoot ([string]$root)) -PathType Container) "module root가 없습니다: $moduleId / $root"
    }
}

foreach ($module in $modules) {
    $moduleId = [string]$module.id
    $projectPath = [System.IO.Path]::GetFullPath((Join-Path $pcRoot ([string]$module.project)))
    [xml]$projectXml = Get-Content -LiteralPath $projectPath -Raw
    $actualDependencies = @()
    foreach ($reference in @($projectXml.SelectNodes('//ProjectReference'))) {
        if ($null -eq $reference) { continue }
        $referencedPath = [System.IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $projectPath) ([string]$reference.GetAttribute('Include'))))
        Assert-Condition ($moduleByProject.ContainsKey($referencedPath)) "manifest 밖 production project 참조입니다: $moduleId / $referencedPath"
        $actualDependencies += $moduleByProject[$referencedPath]
    }
    $actualDependencies = @($actualDependencies | Sort-Object -Unique)
    $allowedDependencies = @($module.allowed_dependencies | ForEach-Object { [string]$_ } | Sort-Object -Unique)
    Assert-Condition ($actualDependencies.Count -eq $allowedDependencies.Count) "project reference 개수가 manifest와 다릅니다: $moduleId"
    foreach ($dependency in $actualDependencies) {
        Assert-Condition ($allowedDependencies -contains $dependency) "허용되지 않은 project reference입니다: $moduleId -> $dependency"
    }
    foreach ($dependency in $allowedDependencies) {
        Assert-Condition ($actualDependencies -contains $dependency) "manifest에만 있고 실제 project에 없는 reference입니다: $moduleId -> $dependency"
    }
}
Write-Host "Architecture manifest: PASS ($($modules.Count) production modules)"

if (-not [string]::IsNullOrWhiteSpace($SourceManifestPath)) {
    Assert-Condition (Test-Path -LiteralPath $SourceManifestPath -PathType Leaf) "정본 manifest가 없습니다: $SourceManifestPath"
    $actualHash = (Get-FileHash -LiteralPath $SourceManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Assert-Condition ($actualHash -eq [string]$ledger.source_manifest_sha256) '정본 manifest SHA가 원장과 다릅니다. 원장을 재생성해야 합니다.'

    $source = Get-Content -LiteralPath $SourceManifestPath -Raw | ConvertFrom-Json
    Assert-Condition ($source.requirement_count -eq 238 -and @($source.rows).Count -eq 238) '정본 요구사항 행 수가 238이 아닙니다.'
    $sourceById = @{}
    foreach ($sourceRow in @($source.rows)) { $sourceById[[string]$sourceRow.requirement_id] = $sourceRow }
    foreach ($row in @($ledger.rows)) {
        $id = [string]$row.requirement_id
        Assert-Condition ($sourceById.ContainsKey($id)) "정본에 없는 원장 행입니다: $id"
        $sourceRow = $sourceById[$id]
        foreach ($field in @(
            'master_id', 'master_title_ko', 'vertical_spec_index', 'vertical_spec_type_ko',
            'implementation_phase', 'notion_url', 'notion_requirement_text_ko',
            'notion_requirement_sha256', 'notion_master_requirement_sha256')) {
            Assert-Condition (
                [string](Get-JsonPropertyValue $row $field) -eq [string](Get-JsonPropertyValue $sourceRow $field)) `
                "정본과 원장 필드가 다릅니다: $id / $field"
        }
        Assert-Condition ([string]$row.source_planned_pr -eq [string]$sourceRow.planned_pr) "정본 planned_pr 불일치: $id"
        Assert-Condition ([string]$row.source_disposition -eq [string]$sourceRow.disposition) "정본 disposition 불일치: $id"
        Assert-Condition ([string]$row.source_evidence_state -eq [string]$sourceRow.evidence_state) "정본 evidence_state 불일치: $id"
    }
    Write-Host "Source cross-check: PASS ($SourceManifestPath)"
} else {
    Write-Host 'Source cross-check: SKIPPED (독립 PC clone에는 원본 저장소가 없을 수 있음)'
}

Write-Host 'PC port ledger validation: PASS'
Write-Host "Rows: $(@($ledger.rows).Count) / Unique IDs: $($ids.Count)"
foreach ($status in $allowed) {
    Write-Host ("{0}: {1}" -f $status, @($ledger.rows | Where-Object disposition -eq $status).Count)
}
