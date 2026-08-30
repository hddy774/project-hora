[CmdletBinding()]
param(
    [switch]$SkipTests,
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

Write-Host "[1/4] Release 빌드"
Invoke-DotNet @('build', (Join-Path $repositoryRoot 'Chronicles.slnx'), '-c', 'Release')

if (-not $SkipTests) {
    Write-Host "[2/4] 전체 자동 테스트"
    Invoke-DotNet @(
        'run',
        '--project', (Join-Path $repositoryRoot 'tests\Chronicles.Tests\Chronicles.Tests.csproj'),
        '-c', 'Release',
        '--no-build'
    )
}
else {
    Write-Host "[2/4] 테스트 생략"
}

if (Test-Path -LiteralPath $publishDirectory) {
    if (-not $publishDirectory.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar)) {
        throw "안전하지 않은 출력 경로입니다: $publishDirectory"
    }

    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

Write-Host "[3/4] Windows x64 독립 실행본 생성"
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
- 일시정지, 1x, 4x, 16x 속도 조절
- 이름 또는 A00001 형식으로 인물 검색
- 랭킹 행을 선택해 자질, 경지, 공법, 법술, 전투 경험과 전투력을 관찰
- 저장/불러오기 버튼으로 현재 세계 보존

저장 위치:
%LocalAppData%\ChroniclesOfMyriadImmortalPaths\world.cmip
"@
[IO.File]::WriteAllText(
    (Join-Path $publishDirectory '실행 안내.txt'),
    $guide.Trim() + [Environment]::NewLine,
    [Text.UTF8Encoding]::new($true))

Write-Host "[4/4] 로컬 실행 준비 완료"
Write-Host "실행 파일: $executablePath"

if ($CreateDesktopShortcut) {
    $desktopDirectory = [Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)
    $shortcutPath = Join-Path $desktopDirectory '선로만상록 PC.lnk'
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $executablePath
    $shortcut.WorkingDirectory = $publishDirectory
    $shortcut.Description = "선로만상록 PC $version"
    $shortcut.IconLocation = "$executablePath,0"
    $shortcut.Save()
    Write-Host "바탕화면 바로가기: $shortcutPath"
}

if ($Launch) {
    Start-Process -FilePath $executablePath -WorkingDirectory $publishDirectory
}
