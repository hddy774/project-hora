# PC 0.0.27 검증 증거

## 고정 범위

- 기준선: `0.0.26` / `c3100c42ef86a3ff28a8aca0d8bebed0ca23e770`
- 구현 범위: `a3f0b9a`부터 완료 SHA까지
- 구성: .NET 10 Release, Windows x64, 인구 10,000명
- 저장 계약: 외부 WorldSchema 25 / checkpoint schema 32

## 자동 검사

- Release 전체 검사: **75/75 PASS**
- architecture + XAML resource: **PASS**
- 이식 원장: **238/238 유효**, Port 128 / PC Adapt 16 / PC_TEMP_REVIEW 31 /
  Gap 63. Gap은 이 버전의 성능 리팩터링으로 구현 완료를 위장하지 않는다.
- 경고 0, 오류 0

## 10,000명·20,000 tick

현재 Release 독립 프로세스 5회 tick 값은 866 / 841 / 867 / 925 / 910ms이며 중앙값은
**867ms**다. 합계 중앙값은 **1,014ms**, simulation 할당은 14.29MiB,
tick당 749B다.

동일 머신에서 0.0.26과 교차 실행한 5쌍은 외부 부하에 따라 572~1,042ms로 흔들렸다.
쌍별 current/base 비율 중앙값은 **0.982**, 즉 현재 구현이 약 **1.8% 빠르다**.
수행 계산만 비교한 독립 5회에서는 직렬 중앙값 633ms, 기존 동적 병렬 475ms로 병렬
계산이 약 **25.0% 빠르다**.

## 100배속 60초 SOAK

- warm-up: 30초
- 표본: 600
- pulse p95: **54.30ms** (예산 80ms)
- pulse p99: **63.52ms** (예산 100ms)
- 정상 상태: **99.83%** (예산 95%)
- 최대/최종 backlog: **126.93 / 0.00ms**
- 진행: **1,200,000 / 1,200,000 tick, 100.00%**

모든 지속 예산을 통과했다.

## 조회·저장

- 활성 UI 80회: 기존 이중 전체 조회 230.1ms → 선택 조회 10.0ms, 95.6% 감소
- 요약 query p95 0.228ms, 1,000행 materialization p95 0.095ms
- 비인물 화면의 숨은 인물 투영 할당 19.7% 제거
- 지도 닫힌 수선자 탭 할당 36.4% 감소
- SQLite 저장 live managed peak: 400.75MiB → 27.92MiB, 93.0% 감소
- SQLite 5회 중앙값 저장/복원 1,014/1,222ms는 1초 권고 중 복원이 미달하므로 기존
  `PC_TEMP_REVIEW`를 유지한다.

## 화면·패키지

- self-contained win-x64, 바로가기, 실제 실행, 화면 모드·UI 배율 검증은 완료 SHA에서
  수행해 아래에 확정한다.
- 완료 SHA: `PENDING`
- 실행 파일 SHA-256: `PENDING`

