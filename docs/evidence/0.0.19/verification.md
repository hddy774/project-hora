# 0.0.19 로컬 검증 증거

## 범위와 커밋

- 장기 수행 계획·조건·저빈도 pulse·경제 흐름: `4703e1b`
- 계획 checkpoint/hash/codec·손상 검증: `8ba1cc8`
- 수행 계획 내부 탭 UI: `ef2fdfc`
- 버전·상세 변경 이력·추적 계약: `4032afbbde8cdb3550757cc4e53f572cece0e43e`
- GitHub Actions / GitHub Release: 실행하지 않음

0.0.19에는 0.0.18 버전 완료 뒤의 어두운 탭 테마 수정, 10MiB 로그 회전, 최근 100개
성능/상태 계측과 그래프, 개인 생별 100년 종합 연대기·파일 출력도 함께 포함한다.

## 자동 검증

고정 SHA `4032afbbde8cdb3550757cc4e53f572cece0e43e`에서
`scripts/build-local.ps1 -CreateDesktopShortcut -Launch`를 실행했다.

- Windows Release 전체 솔루션: 경고 0, 오류 0
- 전체 자동 검사: `RESULT: 56/56 passed`
- 장기 계획 1~120,000개월·조건 범위·계획 경계 재판단·schema 25/hash/codec PASS
- 사제 공동 수행 교사/학습자 Ref·학습 보너스·교사 경험 귀속 PASS
- 모든 로그 파일별 10MiB 상한과 자동 회전 PASS
- 180년 SOAK: 입문 정체 7명, 축기 이상 883명, 사냥 참여 676명, 전투 경험
  967명, 사냥 4,582회, 전투 9,603회, 사망 156명
- 아키텍처 계약: 8개 production module, Desktop authority 직접 접근 0
- 이식 원장: 238개 고유 행, `Port 86 / PC Adapt 14 / PC_TEMP_REVIEW 29 /
  Gap·Deferred 109` PASS

## 성능

- 10,000명·20,000 tick 별도 프로세스 5회: 1,292 / 1,285 / 1,278 / 1,291 /
  1,296ms, 중앙값 1,291ms
- 0.0.13 고정 중앙값 2,212ms 대비 41.6% 감소 / 30% 목표 PASS
- 같은 측정의 tick 진행 중앙값 1,056ms, checkpoint 중앙값 97ms, 복원 중앙값 469ms
- 최종 통합 UI 측정: 기존 이중 전체 조회 143.8ms → 선택 조회 10.0ms,
  93.1% 감소
- 요약 query p95 0.204ms / 16ms, 1,000행 materialization p95 0.068ms /
  50ms PASS
- 지도 닫힌 수선자 탭 managed allocation 37.2% 감소

최종 전체 실행 중 SQLite 5회 중앙값은 저장/복원 1,101/1,134ms로 1초 초기 예산을
넘었다. 게임 프로세스를 종료한 독립 프로세스 3회(각 내부 5회)의 결과는
`998/1,050`, `867/984`, `980/961ms`였고 세 결과 중앙값은 저장 980ms·복원 984ms로
1초 예산을 통과했다. 저장 live managed peak는 기존 대비 87.8~90.3% 줄었다.
통합 부하에서의 일시 초과는 숨기지 않고 normalized authority table·streaming codec
후속 최적화 항목으로 유지한다.

## 직접 실행·산출물

- 실행 파일: `artifacts/Chronicles-0.0.19-win-x64/Chronicles.exe`
- 크기: 141,050,719 bytes
- SHA-256: `5830705AF4835AE9025741B8B46E9E913C4F4B2BBA99C4427B29FA6795AEE575`
- 파일 버전: `0.0.19.0`
- 제품 버전: `0.0.19+4032afbbde8cdb3550757cc4e53f572cece0e43e`
- 바탕화면 바로가기: `선로만상록 PC.lnk`, 대상/작업 폴더가 위 산출물과 일치
- 직접 실행 창: `선로만상록 PC v0.0.19`, 응답 상태 True 확인 뒤 정상 종료

이번 직접 실행은 정확한 산출물·창·응답·바로가기 연결을 검증했다. 화면 전경 입력을
사용한 세부 탭 자동 조작은 이전 사용자 입력으로 중단된 세션을 재사용하지 않았으며,
수행 내부 탭과 닫힌 Expander 무계산 계약은 Release 빌드와 자동 회귀로 검증했다.
