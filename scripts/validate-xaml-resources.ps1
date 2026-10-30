[CmdletBinding()]
param(
    [string]$DesktopRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$pcRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($DesktopRoot)) {
    $DesktopRoot = Join-Path $pcRoot 'src/Chronicles.Desktop'
}
$DesktopRoot = [IO.Path]::GetFullPath($DesktopRoot)

function Assert-Condition {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Get-XamlKeys {
    param([Parameter(Mandatory)][string]$Path)

    $document = [xml]([IO.File]::ReadAllText($Path))
    $namespaceManager = [Xml.XmlNamespaceManager]::new($document.NameTable)
    $namespaceManager.AddNamespace('x', 'http://schemas.microsoft.com/winfx/2006/xaml')
    $keys = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($node in @($document.SelectNodes('//*[@x:Key]', $namespaceManager))) {
        if ($null -eq $node) { continue }
        $key = $node.GetAttribute('Key', 'http://schemas.microsoft.com/winfx/2006/xaml')
        if (-not [string]::IsNullOrWhiteSpace($key)) {
            [void]$keys.Add($key)
        }
    }
    return ,$keys
}

Assert-Condition (Test-Path -LiteralPath $DesktopRoot -PathType Container) "Desktop XAML directory is missing: $DesktopRoot"
$appXamlPath = Join-Path $DesktopRoot 'App.xaml'
Assert-Condition (Test-Path -LiteralPath $appXamlPath -PathType Leaf) "App.xaml is missing: $appXamlPath"

$applicationKeys = Get-XamlKeys $appXamlPath
$xamlFiles = @(Get-ChildItem -LiteralPath $DesktopRoot -File -Filter '*.xaml' | Sort-Object FullName)
Assert-Condition ($xamlFiles.Count -gt 0) "No Desktop XAML files were found: $DesktopRoot"

$resourcePattern = [regex]::new(
    '\{(?<kind>StaticResource|DynamicResource)\s+(?<key>[^{}\s,]+)',
    [Text.RegularExpressions.RegexOptions]::CultureInvariant)
$missing = [Collections.Generic.List[string]]::new()
$referenceCount = 0

foreach ($file in $xamlFiles) {
    $content = [IO.File]::ReadAllText($file.FullName)
    $localKeys = Get-XamlKeys $file.FullName
    foreach ($match in $resourcePattern.Matches($content)) {
        $referenceCount++
        $key = $match.Groups['key'].Value
        if ($applicationKeys.Contains($key) -or $localKeys.Contains($key)) {
            continue
        }

        $prefix = $content.Substring(0, $match.Index)
        $line = 1 + ([regex]::Matches($prefix, '\r\n|\n|\r')).Count
        $missing.Add("$($file.Name):$line - $($match.Groups['kind'].Value) $key")
    }
}

if ($missing.Count -gt 0) {
    throw "Undefined XAML resource references were found:`n$($missing -join [Environment]::NewLine)"
}

Write-Host 'Desktop XAML resource validation: PASS'
Write-Host "XAML files: $($xamlFiles.Count) / resource references: $referenceCount / missing: 0"
