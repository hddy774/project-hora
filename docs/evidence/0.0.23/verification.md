# 0.0.23 로컬 검증 증거

## 고정 범위

- 0.0.22 기준 SHA: `ff20d38d00855c1cebd3b675cc13111179706adf`
- 0.0.23 구현 코드 종점: `615d19eda1fb72a851d50b6e79dfbb32095a8c1f`
- VERSION 완료 SHA: `6d3d8bdbdfa6e10ae860175483b1308083ebe186`
- 최종 증거 커밋 SHA는 Git과 PC 변경 기록에 별도로 기록한다.
- 외부 SQLite WorldSchema: 25 유지
- 내부 checkpoint schema: 31 유지
- GitHub Actions / GitHub Release: 실행하지 않음

## 결함 원인과 복구 결과

### 장기 저장

- 사용자 오류의 원인은 디스크 공간이 아니라 압축 전 JSON이 기존 512MiB 안전 상한을
  넘은 것이었다.
- JSON 상한은 1.5GiB, 압축 container 상한은 256MiB로 분리했다. encode는 JSON →
  Brotli scratch file, SQLite write는 256KiB incremental BLOB으로 동작한다.
- 실제 사용자 `default` 슬롯은 읽기 전용 source로 사용했고 별도 GUID 임시 root에만
  probe를 저장했다. probe는 성공·실패 뒤 모두 삭제했다.
- `CMIP_LARGE_SAVE_ADVANCE_YEARS=450` 결과:
  - 원본 JSON: **537,005,007 bytes** — 512MiB(536,870,912 bytes)보다 134,095 bytes 큼
  - 압축 container: **46,391,237 bytes**
  - 저장 / 복원: **7.10s / 4.65s**
  - 왕복 state hash: **`B97D1D68A32CE6F3`**
- 기존 default `world.db`와 primary/backup은 수정·이동·삭제하지 않았다.

### 테스트 러너와 전역 예외

- 존재하지 않는 `--filter 존재하지-않는-필터-023`은 후보 안내 뒤 종료 코드 2로 끝났다.
- 같은 시각 이후 Windows Application 로그에서 `dotnet.exe`/`Chronicles.Tests` 관련
  `.NET Runtime 1026`, `Application Error`, `Windows Error Reporting` 이벤트는 0건이었다.
- recoverable 저장 예외는 단계·종류·실제 크기를 표시하고 권위 world와 이전 일시정지
  상태를 보존한다. 그 밖의 task fault는 UI thread에서 재throw하지 않고 진단 로그로
  관찰하며, 권위 상태가 불확실한 동기 예외는 기록 후 제어 종료한다.

## 자동 검증

- Windows Release 전체 솔루션: 경고 0, 오류 0
- 전체 등록 검사: `RESULT: 73/73 passed`
- 구조 계약: PASS
  - production module 8개
  - Desktop authority pattern 0
- XAML 리소스 계약: PASS
  - XAML 10개, 리소스 참조 743개, 누락 0
- 238개 이식 원장: PASS
  - Port 128 / PC Adapt 16 / PC_TEMP_REVIEW 31 / Platform Excluded 0 /
    Gap·Deferred 63
- SQLite 5회 중앙값:
  - payload 6.08MiB
  - 기존 container save/load 903/693ms
  - SQLite save/load 901/880ms
  - save live managed peak 143.25MiB → 27.77MiB, **80.6% 감소**
  - 1초 권고와 30% peak 절감 모두 PASS
- 직렬·병렬 5개 seed의 월·100년, 별도 1,000년과 `AdvanceTo` 분할·checkpoint
  hash/RNG/사건/연대기/wire 동일성: PASS
- 최근 개인 로그: 정확히 100행, wrap 시간순 조회, nullable 값, checkpoint 왕복,
  100행 이후 10,000회 덮어쓰기 allocation 0: PASS

## 10,000명·20,000 tick 독립 Release 5회

각 회차는 별도 `dotnet` 프로세스에서 같은 10,000명 작업집합을 JIT/PGO warm-up한 뒤
측정했다.

| 회차 | 초기화 ms | 20,000 tick ms | 관찰 ms | 합계 ms | checkpoint ms | schema 31 복원 ms | simulation 할당 MiB | live heap MiB |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 117 | 866 | 2.4 | 999 | 46 | 262 | 14.30 | 68.87 |
| 2 | 91 | 567 | 2.0 | 672 | 47 | 264 | 14.31 | 68.88 |
| 3 | 120 | 795 | 1.7 | 929 | 43 | 256 | 14.32 | 68.86 |
| 4 | 114 | 799 | 1.7 | 927 | 46 | 253 | 14.30 | 68.86 |
| 5 | 124 | 747 | 1.3 | 887 | 45 | 265 | 14.31 | 68.88 |
| 중앙값 | **117** | **795** | **1.7** | **927** | **46** | **262** | **14.31** | **68.87** |

- 20,000 tick 중앙값 795ms로 0.90초 완료 기준을 통과했다.
- 같은 PC·독립 Release 5회의 0.0.13 기준선 1,945ms보다 **59.1%** 짧고,
  합계 중앙값 927ms는 0.0.13의 2,212ms보다 **58.1%** 짧다.
- 같은 warm 측정의 0.0.22 개인 로그 구조는 allocation 28.65MiB, live heap
  121.20MiB였다. 0.0.23은 각각 **50.1%**, **43.2%** 감소했다.
- tick당 시뮬레이션 할당은 중앙값 약 750 bytes다.

## 순수 수행 CPU 병렬 처리량

각 회차는 같은 checkpoint를 직렬·병렬 world로 복원하고 최종 hash 일치를 먼저
검사했다.

| 회차 | 직렬 ms | 병렬 ms | 실제 병렬 후보 | batch |
|---:|---:|---:|---:|---:|
| 1 | 629 | 405 | 30,496 | 38 |
| 2 | 695 | 527 | 30,496 | 38 |
| 3 | 566 | 408 | 30,496 | 38 |
| 4 | 699 | 468 | 30,496 | 38 |
| 5 | 639 | 415 | 30,496 | 38 |
| 중앙값 | **639** | **415** | **30,496** | **38** |

병렬 중앙값은 직렬보다 **35.1%** 짧다. 공유 권위 commit과 RNG 순서는 직렬이고,
fingerprint가 바뀐 인물은 기존 직렬 경로로 다시 계산한다.

## 100배속 10분 SOAK

- 80ms 고정 대기+작업 방식의 최초 600초 결과는 표본 3,848개, p95 84.13ms,
  p99 98.68ms, 정상 93.01%, backlog 최대/최종 191.32/0ms로 **FAIL**이었다.
  단순 통과로 숨기지 않고 작업시간이 다음 대기 앞에 더해지던 결함으로 기록했다.
- 고정 deadline과 50ms 권위 계산·100ms 관찰 분리 후 최종 실행:
  - 실행: `CMIP_SOAK_SECONDS=600`, warm-up 30초
  - 표본: **11,401개**
  - p95 / p99: **48.27 / 57.77ms**
  - 정상 상태: **99.89%**
  - backlog 최대 / 최종: **307.85 / 0.00ms**
  - 실제 진행: **12,000,000 / 12,000,000 tick, 100.00%**
  - 결과: **PASS**

## 화면 자동 조작

- 실제 호스트에서 1707×1067 전체화면과 1650×1016 창 모드를 전환했다. 요청 배율
  90%·100%와 140%를 조작했고, 140%는 현재 가용 영역의 안전 상한인 131%로 적용되어
  헤더·설정 카드·내부 탭·성능 계측 카드가 잘리지 않았다.
- 1920×1080 기본/채우기와 1280×780의 90~140% 안전 배율은 XAML·
  `AdaptiveUiScalePolicy` 계약 검사로 확인했다. 정확한 1280×780 물리 화면 조작은 이
  호스트 표시 영역과 달라 자동 계약으로만 검증했다.
- 실행 파일과 바탕화면 바로가기는 각각 `선로만상록 PC v0.0.23` 창 제목과 실제
  실행 파일 경로를 확인했다. 종료 확인 창과 세이브 카드의 마지막 자동 조작은 사용자
  Escape 입력으로 중단되어 완료로 기록하지 않는다.

## 로컬 산출물

- 경로: `artifacts/Chronicles-0.0.23-win-x64/Chronicles.exe`
- 크기: `141,866,847 bytes`
- SHA-256: `A6A1D84C491FBFE2C2D1D345AFF47990B7065B974C3B7FB969E9CF453E1B7920`
- 파일 버전: `0.0.23.0`
- 제품 버전: `0.0.23+6d3d8bdbdfa6e10ae860175483b1308083ebe186`
- 직접 실행과 바탕화면 바로가기: 모두 정확한 버전 창 제목으로 시작 확인

## 남은 범위와 판정

- 이 버전은 저장·runtime·메모리·수행 CPU와 이미 구현된 관찰 UI 회귀를 닫는다.
- M14 UI/접근성의 기존 63개 Gap·Deferred를 완료로 위장하지 않는다.
- 다른 99개 주 Production, 미승인 사회 자질·법칙 효과·종합 세력 공식은 추가하지 않았다.
- GitHub Actions와 GitHub Release는 실행하지 않으며 최종 상태는 로컬 `Packaged`로만
  기록한다.
