# 선로만상록 PC · Chronicles of Myriad Immortal Paths

현재 버전은 루트 `VERSION`에서 관리하며 `대표.중간.소규모` 형식을 사용합니다. 기능
업데이트마다 `scripts/bump-patch-version.ps1`이 소규모 버전을 한 단계 올립니다.

10,000명의 수선자가 실시간으로 수행하고 싸우며 전투력 랭킹 1위를 노리는
Windows용 자율 관찰 시뮬레이션입니다. 플레이어는 세계를 조종하지 않고 인물을
추적하고, 판단 이유와 전투 타임라인, 순위 변동과 돌파 기록을 관찰합니다.

## 현재 구현

- 10,000명 이벤트 기반 실시간 AI
- 영근·근골·체질·오성·정신·영혼의 6대 수행자질과 영혼 연속 윤회
- 수행, 경지 돌파, 공법 숙련과 법술 성장
- 경지·자질·공법·법술·전투 경험을 분해한 전투력
- 전원 1위를 목표로 하는 도전·방어 경쟁과 실시간 랭킹
- 일시정지, 배속, 검색, 즐겨찾기와 사건 타임라인
- 결정론적 저장·불러오기와 Windows x64 배포
- 0세 탄생부터 소·대경지 돌파, 깨달음, 사망과 환생을 잇는 개인 일대기

게임 내 **저장**과 **불러오기**는 `%LocalAppData%\ChroniclesOfMyriadImmortalPaths\world.cmip`을
사용합니다. 저장 파일은 압축되며 checksum 검증을 통과한 경우에만 복원됩니다.

기획 정본은 [게임 디자인](docs/GAME_DESIGN.md), 코드 경계는
[아키텍처](docs/ARCHITECTURE.md)를 따릅니다.

## 개발 빌드

```powershell
dotnet build Chronicles.slnx -c Release
dotnet run --project src/Chronicles.Desktop/Chronicles.Desktop.csproj
dotnet run --project tests/Chronicles.Tests/Chronicles.Tests.csproj -c Release
```

## 이 PC에서 바로 실행

저장소 루트의 `로컬_빌드_및_실행.cmd`를 두 번 클릭하면 전체 테스트 후 Windows x64
독립 실행본을 만들고 바탕화면에 `선로만상록 PC` 바로가기를 생성합니다. .NET 런타임은
실행 파일에 포함됩니다.

자세한 로컬 점검 절차는 [PC 로컬 실행 및 테스트](docs/LOCAL_TESTING.md)를 참고하세요.
