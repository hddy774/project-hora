# 0.0.21 로컬 검증 증거

## 고정 원본과 범위

- VERSION 완료 SHA: `f837e4cb6b40ddeba8029139dd710f4c457869ad`
- 세부 수행 진척: `be56604`, `206ca3c`, `b012d5a`
- 공통 Registry·SQLite runtime 추적: `0d8cc56`, `9687cdc`, `c95879c`, `6a7b65b`
- Content·Knowledge·Law 권위/저장/UI: `357fe3c`~`8d97947`
- 승인 초기 모집단·실물 관찰: `8145c86`, `1e1f3c9`
- 절차 생성·설정·회귀·추적: `8331601`, `a435eb0`, `eabd214`, `b7d6c84`
- 지속 생성 할당 최적화: `f1d9bb4`
- 성능·검증 문서: `7d3038e`
- GitHub Actions / GitHub Release: 실행하지 않음

## 자동 검증

- Windows Release 전체 솔루션: 경고 0, 오류 0
- 전체 자동 검사: `RESULT: 65/65 passed`
- 아키텍처 계약: PASS, production module 8, Desktop authority pattern 0
- 238개 이식 원장: PASS
  - Port 128 / PC Adapt 15 / PC_TEMP_REVIEW 31 / Platform Excluded 0 /
    Gap/Deferred 64
- 저장: 외부 SQLite world schema 25, 내부 checkpoint schema 30
- 180년 SOAK: 축기 이상 896명, 사냥 참여 690명, 전투 경험 964명,
  사냥 4,704회, 전투 9,603회, 사망 137명
- 같은 seed·설정 재현, 도메인별 RNG 분리, 관찰 무영향, 지속 생성, schema 29 이전,
  schema 30 왕복 hash와 손상 checkpoint 거부를 통과했다.

## 성능

별도 Release 프로세스에서 10,000명·20,000 tick을 5회 측정했다.

| 회차 | 초기화 ms | 진행 ms | 관찰 ms | 합계 ms | checkpoint ms | 복원 ms |
|---:|---:|---:|---:|---:|---:|---:|
| 1 | 286 | 1,187 | 62.2 | 1,535 | 117 | 557 |
| 2 | 275 | 1,189 | 64.5 | 1,529 | 118 | 565 |
| 3 | 271 | 1,204 | 63.0 | 1,538 | 115 | 572 |
| 4 | 273 | 1,183 | 61.5 | 1,518 | 115 | 546 |
| 5 | 275 | 1,197 | 64.2 | 1,536 | 143 | 604 |
| 중앙값 | **275** | **1,189** | **63.0** | **1,535** | **117** | **565** |

- 같은 schema 30 최적화 전 1,812ms 대비 15.3% 감소했다.
- 0.0.20 중앙값 1,484ms 대비 3.4% 회귀로 허용 예산 15% 이내다.
- 0.0.13 중앙값 2,212ms 대비 30.6% 감소해 장기 30% 목표를 통과했다.
- 최종 패키징 실행의 선택 UI 80회는 이전 이중 전체 조회 136.3ms, 선택 조회
  8.1ms로 94.1% 감소했다. 요약 query p95 0.106ms / 16ms, 1,000행 p95
  0.053ms / 50ms다.
- 저장 카드 p95 0.95ms / 50ms다.
- SQLite 5회 중앙값은 저장 973ms, 복원 869ms이며 live managed peak는 기존
  container보다 85.8% 줄었다.

## 로컬 산출물과 바로가기

- 실행 파일: `artifacts/Chronicles-0.0.21-win-x64/Chronicles.exe`
- 크기: 141,709,663 bytes
- SHA-256: `CFC62B148A7E580495A418D5D74CFF8310C771664B6A3BD5265A2FBCC75598A6`
- 파일 버전: `0.0.21.0`
- 제품 버전: `0.0.21+f837e4cb6b40ddeba8029139dd710f4c457869ad`
- 바탕화면 `선로만상록 PC.lnk`의 target과 working directory가 위 실행 파일과
  산출물 폴더를 가리키며 target 파일이 실제로 존재함을 다시 확인했다.
- 실행 프로세스는 0개다. 사용자의 이전 Esc 중단 이후 화면 자동 조작과 직접 실행은
  재개하지 않았으며, 이 버전에서 화면 동작을 직접 확인했다고 주장하지 않는다.

## 임시 검토와 남은 범위

- `PC-TEMP-CULT-DETAIL-STAGES-021`: 대경지별 5단계 이름·가중치
- `PC-TEMP-WORLD-GENERATION-021`: 초기 생성 수량·상한
- `PC-TEMP-PROCEDURAL-NAME-EFFECT-021`: 이름 token·효과 분포
- `PC-TEMP-CONTENT-GROWTH-021`: 10년 주기·회차별 4/2종 생성
- 생성 효과는 권위 profile과 UI까지만 연결했다. 승인 공식 없는 실제 장착·복용·수련
  보정은 활성화하지 않았다.
- 다중 대륙, 나머지 99개 주의 Production화, 지점 내부 장소 절차 생성은 후속 지도
  수직 범위다.
- 원장의 Gap/Deferred 64개는 후속 버전 대상으로 남아 있으며 0.0.21 완료로 위장하지
  않는다.

## 원격 기록

- GitHub 비공개 저장소 `hddy774/Chronicles-of-Myriad-Immortal-Paths`의 `main`에
  중간 검증 SHA `f1d9bb4`까지 푸시하고 GitHub 커넥터에서 같은 commit을 확인했다.
- Notion `0.0.21: 수행 진척·비경·콘텐츠·서적·지식·법칙·절차 생성` 작업 페이지에
  구현 커밋, 검사, 성능, PC_TEMP_REVIEW와 남은 검증을 기록했다. 최종 evidence SHA는
  문서 커밋과 함께 다시 동기화한다.
