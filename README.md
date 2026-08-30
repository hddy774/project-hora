# 선로만상록 PC · Chronicles of Myriad Immortal Paths

10,000명의 수선자가 실시간으로 수행하고 싸우며 전투력 랭킹 1위를 노리는
Windows용 자율 관찰 시뮬레이션입니다. 플레이어는 세계를 조종하지 않고 인물을
추적하고, 판단 이유와 전투 타임라인, 순위 변동과 돌파 기록을 관찰합니다.

## 첫 릴리스 목표

- 10,000명 이벤트 기반 실시간 AI
- 영근·근골·체질·오성 자질
- 수행, 경지 돌파, 공법 숙련과 법술 성장
- 경지·자질·공법·법술·전투 경험을 분해한 전투력
- 전원 1위를 목표로 하는 도전·방어 경쟁과 실시간 랭킹
- 일시정지, 배속, 검색, 즐겨찾기와 사건 타임라인
- 결정론적 저장·불러오기와 Windows x64 배포

기획 정본은 [게임 디자인](docs/GAME_DESIGN.md), 코드 경계는
[아키텍처](docs/ARCHITECTURE.md)를 따릅니다.

## 개발 빌드

```powershell
dotnet build Chronicles.slnx -c Release
dotnet run --project src/Chronicles.Desktop/Chronicles.Desktop.csproj
dotnet run --project tests/Chronicles.Tests/Chronicles.Tests.csproj -c Release
```
