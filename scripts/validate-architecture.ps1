param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
& python (Join-Path $PSScriptRoot 'validate-v021-product-boundary.py') --self-test
if ($LASTEXITCODE -ne 0) { throw 'Current product boundary validation failed.' }
& (Join-Path $PSScriptRoot 'validate-xaml-resources.ps1') -DesktopRoot (Join-Path $root 'src/Chronicles.Desktop')
Write-Host '0.2.1 current product architecture validation: PASS'
