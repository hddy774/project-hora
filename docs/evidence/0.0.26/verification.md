# PC 0.0.26 검증 증거

검증 일자: 2026-09-02 (Asia/Seoul)

## 구현 범위

- `823eee5`: 설정·저장 화면의 상세 관찰 분리, lock-free shell, busy 관찰 프레임 생략,
  실제 query 구간 계측
- `d9c4fd5`: 월·수명 산술, compact 개인 로그, 관계 인접 index, ranking radix hot path
  최적화
- `5cf56c6`: 100배속 관찰 보폭, 표본 기반 UI 바인딩, 성능 상태 판정 정돈
- 게임 공식, RNG stream, AgentId 처리 순서, 저장 schema 25/checkpoint schema 32는
  변경하지 않았다.

## 자동 검사

- Release 전체 검사: `RESULT: 75/75 passed`
- 수행 직렬/병렬 처리량 표본: 직렬 408ms, 병렬 331ms, 30,496명/38묶음
- 5 seed 직렬·병렬 결정론: PASS
- `AdvanceTo` 분할·runtime pulse·checkpoint 결정론: PASS
- runtime lock-free shell·busy 관찰 프레임 생략: PASS
- 저장 실패 복구·분할 SQLite 후보 복원·로그 5MiB 상한: PASS
- 구조 계약: production module 8개, Desktop authority 우회 0건
- XAML 계약: 10개, 리소스 참조 746개, 누락 0
- 이식 원장: 238행, Port 128 / PC Adapt 16 / PC_TEMP_REVIEW 31 /
  Gap·Deferred 63

## 100배속 지속 측정

독립 Release 실행, 60초 중 앞 30초를 warm-up으로 제외했다.

```text
100X SOAK: 60s · warm-up 30s · 표본 600 · p95 52.16 ms · p99 60.46 ms ·
정상 100.00% · backlog 최대/최종 37.75/0.00 ms ·
진행 1,178,000/1,200,000 ticks (98.17%)
```

모든 지속 예산(p95 80ms, p99 100ms, 정상 95%, 진행 95%, 최종 backlog 1초)을
통과했다. 5초 warm 상태 smoke도 p95 41.89ms, p99 50.90ms, 정상 100%, 진행률
99%였다.

## 10,000명 처리량

build server를 종료한 뒤 같은 Release 실행 파일을 독립 프로세스로 5회 실행했다.

| 회차 | 초기화 | 20,000 tick | 관찰 | 합계 | checkpoint | 복원 |
|---:|---:|---:|---:|---:|---:|---:|
| 1 | 160ms | 950ms | 2.0ms | 1,127ms | 68ms | 361ms |
| 2 | 182ms | 922ms | 1.7ms | 1,122ms | 69ms | 331ms |
| 3 | 108ms | 540ms | 2.6ms | 670ms | 85ms | 366ms |
| 4 | 128ms | 487ms | 3.3ms | 638ms | 83ms | 372ms |
| 5 | 106ms | 479ms | 2.0ms | 606ms | 83ms | 367ms |
| 중앙값 | 128ms | 540ms | 2.0ms | 670ms | 83ms | 366ms |

초기 두 cold 프로세스의 tiered JIT 변동을 포함해 중앙값으로 판정했다. 0.0.24 기록의
진행 741ms 대비 201ms(27.1%), 합계 879ms 대비 209ms(23.8%) 감소했다. 전체 회귀
검사 내부 warm 표본은 20,000 tick 324ms였다.

## 관찰·메모리·저장

- 활성 요약 query p95 0.205ms / 1,000행 materialization p95 0.055ms
- 비인물 화면의 숨은 인물 projection 할당 감소 19.7%
- 닫힌 지도 내부 탭의 할당 감소 36.2%
- simulation 할당 14.27MiB, tick당 748B
- SQLite 5회 중앙값: 저장 1,277ms / 복원 1,415ms / 저장 live managed peak
  28.26MiB / 기존 단일 container 대비 peak 80.5% 감소

SQLite 저장·복원 1초는 권고 예산을 아직 넘는다. 수치를 완화하거나 runtime 개선으로
위장하지 않고 normalized authority table·streaming codec 후속 최적화 대상으로 유지한다.

## 로컬 실행본

- VERSION 완료 커밋: `cd701a0fea1783d4556241540cd3b0ea8e0abb59`
- 실행 파일: `artifacts/Chronicles-0.0.26-win-x64/Chronicles.exe`
- 크기: 141,945,695 bytes (135.37MiB)
- SHA-256: `D940DF74565A4DFE4C961EAFE0E398742AED3EF9084C08A913EE2A18B57D550A`
- 파일 버전 `0.0.26.0`, 제품 버전
  `0.0.26+cd701a0fea1783d4556241540cd3b0ea8e0abb59`
- 게시 실행 파일과 `C:\Users\jungh\Desktop\선로만상록 PC.lnk` 모두 창 제목
  `선로만상록 PC v0.0.26`으로 시작 검사 PASS
- 단독 실행 PID 47540은 5초 간격 12회, 총 60초 동안 같은 창 핸들과 `Responding=True`를
  유지했다. 작업 집합 관측 범위는 187.8~196.0MiB였다.
