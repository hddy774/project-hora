param(
    [string]$ManifestPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$pcRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $pcRoot 'docs/architecture/repository_manifest.json'
}

function Assert-Condition {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Normalize-RelativePath {
    param([string]$Path)
    return $Path.Replace('\', '/').TrimStart('./')
}

Assert-Condition (Test-Path -LiteralPath $ManifestPath -PathType Leaf) "architecture manifest가 없습니다: $ManifestPath"
$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
Assert-Condition ($manifest.contract -eq 'CMIP_PC_REPOSITORY_MANIFEST_V1') '지원하지 않는 architecture manifest입니다.'

$moduleByProject = @{}
$moduleById = @{}
foreach ($module in @($manifest.modules)) {
    $id = [string]$module.id
    $project = Normalize-RelativePath ([string]$module.project)
    Assert-Condition (-not $moduleById.ContainsKey($id)) "중복 module id: $id"
    Assert-Condition (-not $moduleByProject.ContainsKey($project)) "중복 project owner: $project"
    Assert-Condition (Test-Path -LiteralPath (Join-Path $pcRoot $project) -PathType Leaf) "project가 없습니다: $project"
    $moduleById[$id] = $module
    $moduleByProject[$project] = $id
}

$solution = Get-Content -LiteralPath (Join-Path $pcRoot 'Chronicles.slnx') -Raw
foreach ($project in $moduleByProject.Keys) {
    Assert-Condition $solution.Contains($project, [StringComparison]::Ordinal) "solution에 project가 없습니다: $project"
}

foreach ($entry in $moduleByProject.GetEnumerator()) {
    $project = $entry.Key
    $moduleId = $entry.Value
    $module = $moduleById[$moduleId]
    [xml]$xml = Get-Content -LiteralPath (Join-Path $pcRoot $project) -Raw
    $projectDirectory = Split-Path -Parent (Join-Path $pcRoot $project)
    $actualDependencies = @()
    foreach ($reference in @($xml.SelectNodes('/Project/ItemGroup/ProjectReference'))) {
        if ($null -eq $reference) { continue }
        $absoluteReference = [System.IO.Path]::GetFullPath((Join-Path $projectDirectory ([string]$reference.Include)))
        $relativeReference = Normalize-RelativePath ([System.IO.Path]::GetRelativePath($pcRoot, $absoluteReference))
        Assert-Condition ($moduleByProject.ContainsKey($relativeReference)) "manifest에 없는 project 참조: $moduleId -> $relativeReference"
        $actualDependencies += $moduleByProject[$relativeReference]
    }

    $allowedDependencies = @($module.allowed_dependencies | ForEach-Object { [string]$_ })
    foreach ($dependency in $actualDependencies) {
        Assert-Condition ($allowedDependencies -contains $dependency) "금지된 project 참조: $moduleId -> $dependency"
    }
}

$desktopRoot = Join-Path $pcRoot 'src/Chronicles.Desktop'
$desktopSources = Get-ChildItem -LiteralPath $desktopRoot -Recurse -File -Include '*.cs'
$forbiddenPatterns = @(
    '\bnew\s+World\s*\(',
    '\bWorld\.Create(Default)?\s*\(',
    '\bAgentStore\b',
    '\bSimulationRunner\b',
    '\bWorldSave(Store|Codec)\b'
)
foreach ($source in $desktopSources) {
    $content = Get-Content -LiteralPath $source.FullName -Raw
    foreach ($pattern in $forbiddenPatterns) {
        Assert-Condition (-not [regex]::IsMatch($content, $pattern)) "Desktop authority 접근 금지 패턴: $($source.FullName) / $pattern"
    }
}

Write-Host 'PC architecture validation: PASS'
Write-Host "Modules: $($moduleById.Count) / Desktop authority patterns: 0"
