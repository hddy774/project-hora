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

function Get-RepositoryRelativePath {
    param([string]$RootPath, [string]$TargetPath)
    $root = [System.IO.Path]::GetFullPath($RootPath).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    $target = [System.IO.Path]::GetFullPath($TargetPath)
    Assert-Condition ($target.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) "project reference is outside repository: $target"
    return $target.Substring($root.Length)
}

Assert-Condition (Test-Path -LiteralPath $ManifestPath -PathType Leaf) "architecture manifest is missing: $ManifestPath"
$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
Assert-Condition ($manifest.contract -eq 'CMIP_PC_REPOSITORY_MANIFEST_V1') 'unsupported architecture manifest contract'

$moduleByProject = @{}
$moduleById = @{}
foreach ($module in @($manifest.modules)) {
    $id = [string]$module.id
    $project = Normalize-RelativePath ([string]$module.project)
    Assert-Condition (-not $moduleById.ContainsKey($id)) "duplicate module id: $id"
    Assert-Condition (-not $moduleByProject.ContainsKey($project)) "duplicate project owner: $project"
    Assert-Condition (Test-Path -LiteralPath (Join-Path $pcRoot $project) -PathType Leaf) "project is missing: $project"
    $moduleById[$id] = $module
    $moduleByProject[$project] = $id
}

$solution = Get-Content -LiteralPath (Join-Path $pcRoot 'Chronicles.slnx') -Raw
foreach ($project in $moduleByProject.Keys) {
    Assert-Condition ($solution.IndexOf($project, [StringComparison]::Ordinal) -ge 0) "project is missing from solution: $project"
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
        $relativeReference = Normalize-RelativePath (Get-RepositoryRelativePath $pcRoot $absoluteReference)
        Assert-Condition ($moduleByProject.ContainsKey($relativeReference)) "project reference is missing from manifest: $moduleId -> $relativeReference"
        $actualDependencies += $moduleByProject[$relativeReference]
    }

    $allowedDependencies = @()
    $allowedDependencies += @($module.allowed_dependencies | ForEach-Object { [string]$_ })
    foreach ($dependency in $actualDependencies) {
        Assert-Condition ($allowedDependencies -contains $dependency) "forbidden project reference: $moduleId -> $dependency"
    }
}

$desktopRoot = Join-Path $pcRoot 'src/Chronicles.Desktop'
$desktopSources = Get-ChildItem -LiteralPath $desktopRoot -Recurse -File -Filter '*.cs'
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
        Assert-Condition (-not [regex]::IsMatch($content, $pattern)) "Desktop authority access pattern: $($source.FullName) / $pattern"
    }
}

& (Join-Path $PSScriptRoot 'validate-xaml-resources.ps1') -DesktopRoot $desktopRoot

Write-Host 'PC architecture validation: PASS'
Write-Host "Modules: $($moduleById.Count) / Desktop authority patterns: 0"
