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

Release WPF 실행 파일을 자동 실행해 화면 검증을 시작했으나 첫 시도는 사용자의 물리적
Escape 입력으로 Windows 앱 제어가 중단되어 결과가 확정되지 않았다. 다음 자동화 시도에서
실행·좌측 탐색·조직/콘텐츠 준비 상태를 확인한 뒤 이 절을 완료 상태로 갱신한다.
