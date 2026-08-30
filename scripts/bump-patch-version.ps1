[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$versionPath = Join-Path $repositoryRoot 'VERSION'
$current = [IO.File]::ReadAllText($versionPath).Trim()

if ($current -notmatch '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)$') {
    throw "VERSION must use major.minor.patch: $current"
}

$nextPatch = checked([int]$Matches.patch + 1)
$next = "$($Matches.major).$($Matches.minor).$nextPatch"
[IO.File]::WriteAllText($versionPath, $next + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
Write-Output $next
