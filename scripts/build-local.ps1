[CmdletBinding()]
param(
    [switch]$SkipTests,
    [switch]$SkipLaunchSmoke,
    [switch]$CreateDesktopShortcut,
    [switch]$Launch
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts'))
$versionPath = Join-Path $repositoryRoot 'VERSION'
$version = [IO.File]::ReadAllText($versionPath).Trim()

if ($version -notmatch '^\d+\.\d+\.\d+([-.][0-9A-Za-z.-]+)?$') {
    throw "VERSION 형식이 올바르지 않습니다: $version"
}

$publishDirectory = [IO.Path]::GetFullPath(
    (Join-Path $artifactRoot "Chronicles-$version-win-x64"))
$executablePath = Join-Path $publishDirectory 'Chronicles.exe'

function Invoke-DotNet {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet 명령이 종료 코드 $LASTEXITCODE 로 실패했습니다: dotnet $($Arguments -join ' ')"
    }
}

function Resolve-HostArtifactPath {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$HostProfileDirectory
    )

    $fullPath = [IO.Path]::GetFullPath($Path)
    $sandboxProfilePattern = '^[A-Za-z]:\\Users\\CodexSandboxOffline(?<relative>\\.*)$'
    if ($fullPath -notmatch $sandboxProfilePattern) {
        return $fullPath
    }

    $relativePath = $Matches['relative'].TrimStart([IO.Path]::DirectorySeparatorChar)
    $hostPath = [IO.Path]::GetFullPath((Join-Path $HostProfileDirectory $relativePath))
    if (-not (Test-Path -LiteralPath $hostPath)) {
        throw "샌드박스 산출물을 사용자 바로가기에 연결할 수 없습니다. 사용자 프로필에서 다시 빌드하십시오: $hostPath"
    }
    return $hostPath
}

function Invoke-DesktopLaunchSmoke {
    param(
        [Parameter(Mandatory)][string]$LaunchPath,
        [Parameter(Mandatory)][string]$ExpectedExecutablePath,
        [Parameter(Mandatory)][string]$WorkingDirectory,
        [Parameter(Mandatory)][string]$ExpectedWindowTitle,
        [Parameter(Mandatory)][string]$Label
    )

    $expectedExecutable = [IO.Path]::GetFullPath($ExpectedExecutablePath)
    $processName = [IO.Path]::GetFileNameWithoutExtension($expectedExecutable)
    $existingProcessIds = @{}
    foreach ($process in @(Get-Process -Name $processName -ErrorAction SilentlyContinue)) {
        $existingProcessIds[$process.Id] = $true
        $process.Dispose()
    }

    $startedProcess = if ([IO.Path]::GetExtension($LaunchPath) -eq '.lnk') {
        Start-Process -FilePath $LaunchPath -PassThru
    }
    else {
        Start-Process -FilePath $LaunchPath -WorkingDirectory $WorkingDirectory -PassThru
    }

    $createdProcesses = @{}
    $verifiedProcess = $null
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    try {
        while ([DateTime]::UtcNow -lt $deadline) {
            $candidates = @()
            if ($null -ne $startedProcess) {
                $candidates += $startedProcess
            }
            $candidates += @(Get-Process -Name $processName -ErrorAction SilentlyContinue)

            foreach ($candidate in $candidates) {
                if ($existingProcessIds.ContainsKey($candidate.Id)) {
                    continue
                }

                try {
                    if ($candidate.HasExited) {
                        continue
                    }
                    $candidate.Refresh()
                    $candidatePath = [IO.Path]::GetFullPath($candidate.Path)
                    if (-not [StringComparer]::OrdinalIgnoreCase.Equals(
                        $candidatePath,
                        $expectedExecutable)) {
                        continue
                    }

                    $createdProcesses[$candidate.Id] = $candidate
                    if ($candidate.MainWindowHandle -ne [IntPtr]::Zero -and
                        [StringComparer]::Ordinal.Equals(
                            $candidate.MainWindowTitle,
                            $ExpectedWindowTitle)) {
                        $verifiedProcess = $candidate
                        break
                    }
                }
                catch [InvalidOperationException] {
                    continue
                }
            }

            if ($null -ne $verifiedProcess) {
                break
            }
            Start-Sleep -Milliseconds 100
        }

        if ($null -eq $verifiedProcess) {
            $exitDescription = if ($null -ne $startedProcess -and $startedProcess.HasExited) {
                "종료 코드 $($startedProcess.ExitCode)"
            }
            else {
                '20초 안에 예상 창을 찾지 못함'
            }
            throw "$Label 실행 스모크 검사 실패: $exitDescription · 예상 제목=$ExpectedWindowTitle"
        }

        Write-Host "$Label 실행 스모크: PASS · PID $($verifiedProcess.Id) · $($verifiedProcess.MainWindowTitle)"
    }
    finally {
        foreach ($process in $createdProcesses.Values) {
            try {
                if (-not $process.HasExited) {
                    [void]$process.CloseMainWindow()
                    if (-not $process.WaitForExit(3000)) {
                        $process.Kill($true)
                        [void]$process.WaitForExit(3000)
                    }
                }
            }
            catch [InvalidOperationException] {
                # The smoke process already exited; there is nothing left to clean up.
            }
            finally {
                $process.Dispose()
            }
        }
        if ($null -ne $startedProcess -and
            -not $createdProcesses.ContainsKey($startedProcess.Id)) {
            $startedProcess.Dispose()
        }
    }
}

Write-Host "[1/5] Desktop XAML 리소스 계약 검사"
& (Join-Path $PSScriptRoot 'validate-xaml-resources.ps1')

Write-Host "[2/5] Release 빌드"
Invoke-DotNet @('build', (Join-Path $repositoryRoot 'Chronicles.slnx'), '-c', 'Release')

if (-not $SkipTests) {
    Write-Host "[3/5] 전체 자동 테스트"
    Invoke-DotNet @(
        'run',
        '--project', (Join-Path $repositoryRoot 'tests\Chronicles.Tests\Chronicles.Tests.csproj'),
        '-c', 'Release',
        '--no-build'
    )
}
else {
    Write-Host "[3/5] 테스트 생략"
}

if (Test-Path -LiteralPath $publishDirectory) {
    if (-not $publishDirectory.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar)) {
        throw "안전하지 않은 출력 경로입니다: $publishDirectory"
    }

    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

Write-Host "[4/5] Windows x64 독립 실행본 생성"
Invoke-DotNet @(
    'publish',
    (Join-Path $repositoryRoot 'src\Chronicles.Desktop\Chronicles.Desktop.csproj'),
    '-c', 'Release',
    '-r', 'win-x64',
    '--self-contained', 'true',
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:PublishTrimmed=false',
    '-p:DebugType=None',
    '-p:DebugSymbols=false',
    "-p:Version=$version",
    '-o', $publishDirectory
)

if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw "실행 파일이 생성되지 않았습니다: $executablePath"
}

$guide = @"
선로만상록 PC $version

실행 파일: Chronicles.exe
지원 환경: Windows 10/11 x64
필수 설치: 없음 (.NET 런타임 포함)

게임은 자동으로 실시간 진행됩니다.
- 일시정지, 1x, 10x, 25x, 50x, 100x 속도 조절
- 이름 또는 A00001 형식으로 인물 검색
- 전투력·경지·자질·부자 TOP 1,000과 전체 10,000명 탐색
- 자질, 경지, 공법, 법술, 법보, 단약, 임무, 시장 거래와 개인 일대기 관찰
- 경지·전투력 분포, 기간별 TOP 1,000 랭킹 변동률, 승패·사망 및 시장 공급·수요 통계
- 세계 영구 연대기와 게임 디버그 로그, 자동·수동 로컬 파일 출력
- 저장/불러오기 버튼으로 현재 세계 보존

저장 위치:
%LocalAppData%\ChroniclesOfMyriadImmortalPaths\saves\default\world.db

기존 world.cmip 파일은 변경하지 않으며 새 버전과 호환되지 않습니다.

로그 위치:
%LocalAppData%\ChroniclesOfMyriadImmortalPaths\logs
"@
[IO.File]::WriteAllText(
    (Join-Path $publishDirectory '실행 안내.txt'),
    $guide.Trim() + [Environment]::NewLine,
    [Text.UTF8Encoding]::new($true))

$shortcutPath = $null
$shortcutExecutablePath = $executablePath
$shortcutWorkingDirectory = $publishDirectory

if ($CreateDesktopShortcut) {
    $desktopDirectory = [Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)
    $hostProfileDirectory = [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)
    $shortcutPath = Join-Path $desktopDirectory '선로만상록 PC.lnk'
    $shortcutExecutablePath = Resolve-HostArtifactPath $executablePath $hostProfileDirectory
    $shortcutWorkingDirectory = Split-Path -Parent $shortcutExecutablePath

    $resolvedDesktopDirectory = [IO.Path]::GetFullPath($desktopDirectory).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    $resolvedShortcutPath = [IO.Path]::GetFullPath($shortcutPath)
    if (-not $resolvedShortcutPath.StartsWith(
        $resolvedDesktopDirectory,
        [StringComparison]::OrdinalIgnoreCase)) {
        throw "안전하지 않은 바로가기 경로입니다: $resolvedShortcutPath"
    }
    if (Test-Path -LiteralPath $shortcutPath) {
        Remove-Item -LiteralPath $shortcutPath -Force
    }

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $shortcutExecutablePath
    $shortcut.WorkingDirectory = $shortcutWorkingDirectory
    $shortcut.Description = "선로만상록 PC $version"
    $shortcut.IconLocation = "$shortcutExecutablePath,0"
    $shortcut.Save()

    $savedShortcut = $shell.CreateShortcut($shortcutPath)
    $savedTargetPath = [IO.Path]::GetFullPath($savedShortcut.TargetPath)
    $savedWorkingDirectory = [IO.Path]::GetFullPath($savedShortcut.WorkingDirectory)
    if (-not [StringComparer]::OrdinalIgnoreCase.Equals($savedTargetPath, $shortcutExecutablePath) -or
        -not [StringComparer]::OrdinalIgnoreCase.Equals($savedWorkingDirectory, $shortcutWorkingDirectory) -or
        -not (Test-Path -LiteralPath $savedTargetPath -PathType Leaf) -or
        -not (Test-Path -LiteralPath $savedWorkingDirectory -PathType Container) -or
        $savedTargetPath -match '(?i)[\\/]CodexSandboxOffline([\\/]|$)') {
        throw "바탕화면 바로가기 검증에 실패했습니다: 대상=$savedTargetPath, 작업 폴더=$savedWorkingDirectory"
    }

    Write-Host "바탕화면 바로가기: $shortcutPath"
}

Write-Host "[5/5] 게시 실행본 시작 검사"
if ($SkipLaunchSmoke) {
    Write-Host '게시 실행본 시작 검사 생략'
}
else {
    $expectedWindowTitle = "선로만상록 PC v$version"
    Invoke-DesktopLaunchSmoke `
        -LaunchPath $executablePath `
        -ExpectedExecutablePath $executablePath `
        -WorkingDirectory $publishDirectory `
        -ExpectedWindowTitle $expectedWindowTitle `
        -Label '게시 실행 파일'
    if ($null -ne $shortcutPath) {
        Invoke-DesktopLaunchSmoke `
            -LaunchPath $shortcutPath `
            -ExpectedExecutablePath $shortcutExecutablePath `
            -WorkingDirectory $shortcutWorkingDirectory `
            -ExpectedWindowTitle $expectedWindowTitle `
            -Label '바탕화면 바로가기'
    }
}

Write-Host '로컬 실행 준비 완료'
Write-Host "실행 파일: $executablePath"

if ($Launch) {
    Start-Process -FilePath $shortcutExecutablePath -WorkingDirectory $shortcutWorkingDirectory
}
