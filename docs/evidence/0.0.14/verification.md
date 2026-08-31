# 0.0.14 로컬 검증 증거

## 범위와 고정 커밋

- 구조 분리: `0e10312`
- 랭킹 revision 캐시: `6335790`
- 238개 이식 원장과 0.0.13 기준선: `76d5b61`
- 좌측 관찰 작업공간: `133d9e2`
- 저장 DB/schema: 변경 없음, 기존 schema 24 유지
- GitHub Actions / GitHub Release: 실행하지 않음

## 자동 검증

```powershell
& .\scripts\validate-architecture.ps1
& .\scripts\validate-port-ledger.ps1
dotnet build Chronicles.slnx -c Release
dotnet run --project tests/Chronicles.Tests/Chronicles.Tests.csproj -c Release --no-build
```

- architecture manifest: 8개 production module PASS
- port ledger: 238 unique, 원본 SHA 대조 PASS
- Release build: 경고 0, 오류 0
- 전체 검사: 46/46 PASS
- 고정 seed 결정론, 관찰 무영향, schema 24 codec/hash 왕복 PASS
- 무변경 pulse 100회: 랭킹 추가 정렬 0회
- 닫힌 작업공간: 랭킹·통계·시장·지도·세계 기록 collection 0행
- 요약 query p95: 0.249ms, 예산 16ms PASS
- 1,000행 materialization p95: 0.194ms, 예산 50ms PASS

## 10,000명·20,000 tick Release 5회

| 회차 | 초기화 ms | 20,000 tick ms | 관찰 ms | 합계 ms | checkpoint ms | schema 24 복원 ms |
|---:|---:|---:|---:|---:|---:|---:|
| 1 | 67 | 1,242 | 0.4 | 1,309 | 140 | 449 |
| 2 | 81 | 960 | 0.3 | 1,041 | 170 | 419 |
| 3 | 85 | 1,241 | 0.5 | 1,326 | 192 | 499 |
| 4 | 55 | 929 | 0.4 | 984 | 143 | 332 |
| 5 | 77 | 1,017 | 1.2 | 1,095 | 164 | 386 |
| 중앙값 | **77** | **1,017** | **0.4** | **1,095** | **164** | **419** |

0.0.13 중앙값 2,212ms 대비 합계 중앙값은 50.5% 줄었다. checkpoint는 38.3%,
복원은 56.6% 줄었다. 각 열의 중앙값은 독립 계산한다.

## 직접 실행 상태

`src/Chronicles.Desktop/bin/Release/net10.0-windows/Chronicles.exe`를 직접 실행해 다음을
확인했다.

- 창 제목 `선로만상록 PC v0.0.14`와 화면 내부 `VERSION 0.0.14`
- 10,000명 세계가 실시간으로 진행되고 년·월·일과 랭킹/전투 통계가 갱신됨
- 좌측 세계→조직→콘텐츠→세계 경로 전환 정상
- 조직은 `0.0.20`, 콘텐츠는 `0.0.21` 범위임을 밝히는 준비 상태 표시
- 준비 상태에서 기존 랭킹·인물 상세 화면이 숨겨지고 추정 데이터가 표시되지 않음
- 세계로 복귀하면 실제 랭킹·인물·통계 화면이 다시 갱신됨

검증 뒤 게임 창을 정상 종료했다. 첫 자동화 시도는 Escape 입력으로 중단됐지만 두 번째
시도에서 새 프로세스를 선택해 위 결과를 확정했다.
