# 0.0.16 로컬 검증 증거

## 범위와 커밋

- 인물 생명/외형, Goal→Plan→Task→Action 인과, 윤회 출생·가계, 관계 provenance:
  `a2f4e2c`
- M03/M04/M06/M07 이식 원장과 수직 계약: `fd6b11a`
- 버전 완료 고정 SHA: `b15253b60e13b73f47be04944c08bda81971327f`
- GitHub Actions / GitHub Release: 실행하지 않음

## 자동 검증

고정 SHA에서 `scripts/build-local.ps1 -CreateDesktopShortcut -Launch`를 실행했다.

- Windows Release 전체 솔루션: 경고 0, 오류 0
- 전체 자동 검사: `RESULT: 49/49 passed`
- 아키텍처 계약: 8개 production module, Desktop authority 직접 접근 0
- 이식 원장: 238개 고유 행과 원본 SHA 대조 PASS
- M03/M04/M06/M07: 각 Master `Gap/Deferred = 0`
- 180년 SOAK: 사냥 참여 1,000명, 전투 경험 973명, 사냥 61,163회,
  전투 28,722회, 사망 1,461회
- 희소 관계: 500명·160년 1,765.8ms, 관계 1,993개, 최대 degree 8

## 성능

- UI 선택 query 80회: 기존 이중 전체 조회 149.8ms → 선택 조회 9.7ms, 93.5% 감소
- 요약 query p95: 0.154ms / 16ms 예산 PASS
- 1,000행 materialization p95: 0.079ms / 50ms 예산 PASS
- 지도 닫힌 탭 allocation: 53.7% 감소
- 10,000명: 초기화 60ms + 20,000 tick 1,146ms + 관찰 0.4ms = 1,206ms
- checkpoint 175ms, authority schema 25 복원 185ms

SQLite 5회 중앙값은 payload 5.82MiB, 저장 987ms, 복원 1,055ms다. 저장은 1초 예산을
통과했고 복원은 55ms 초과했다. live managed peak는 기존 382.33MiB에서 SQLite
51.91MiB로 86.4% 줄어 30% 절감 예산을 통과했다. 1초 미달은 수치 조작 없이
`PC_TEMP_REVIEW` 후속 normalized authority table/streaming codec 최적화로 유지한다.

## 직접 화면 검증

- 창 제목 `선로만상록 PC v0.0.16`, 화면 `VERSION 0.0.16`
- 10,000명 세계가 실시간으로 진행되고 년·월·일, 전투, 랭킹 통계가 갱신됨
- `생애·육신·외형 권위`를 펼쳐 성별, Soul/Body/현생 상태, 외형 definition과 6개 특징,
  선천/후천 미모, 흉터, `PC-TEMP-APPEARANCE-DISTRIBUTION-113` 표시 확인
- `인과선·인지 사실·현재 생 기억`을 펼쳐 Goal/Plan/Task/Action, Event/Result,
  root/parent record, Action/Task/Plan/Goal 실패 범위와 기억 정보 표시 확인
- 화면 확인 후 Alt+F4로 정상 종료했고 동일 제목 창이 0개임을 확인

## 산출물

- 실행 파일: `artifacts/Chronicles-0.0.16-win-x64/Chronicles.exe`
- 크기: 140,784,991 bytes
- SHA-256: `44317EFC99100BE7EC24935E2DC10A2D313DA85CAA0D243EF03FD39E02DA7306`
- 파일 버전: `0.0.16.0`
- 제품 버전: `0.0.16+b15253b60e13b73f47be04944c08bda81971327f`
- 바탕화면 바로가기: `선로만상록 PC.lnk`
