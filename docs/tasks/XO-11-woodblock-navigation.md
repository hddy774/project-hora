# [XO-11] 고서 목판 테마·공통 프레임·탐색

상태: 승인(사용자 전체 개발 지시, 2026-10-29) · 선행: XO-07 · 근거: [15](../../games/xianxia-observer/docs/15-ui-ux-improvement.md).

## 배경과 범위
사용자가 지정한 고서 목판을 공통 테마로 적용하고 작은 버튼·7탭의 밀도를 낮춘다.
포함: `Xianxia.App/Theme`, `Views/HeaderBar·NavBar·MainView·Navigation·SheetHost`, 속도 패널·더보기 화면.
세계 모델·RNG·저장 스키마·아이템·성장 규칙은 변경하지 않는다. 새 배치는 검토 승인 후 구현한다.

## 계약과 동작
1. 15 §2의 토큰·터치48·문자 대비를 적용하고 기존 내장 글꼴을 재사용한다. 추가 자산은 별도 라이선스와 용량을 검토한다.
2. 루트 safe area 한 번, 헤더/본문/진행바/탐색 순 배치. 확대 글자는 높이가 늘고 본문을 스크롤한다.
3. 주요 탭 `천하/종문/인물/사기/더보기`. 더보기에 업적·현황·기록·도감·설정. 기존 콘텐츠 전체 유지.
4. 탐색 목적지에 문자열 상수 `world/sects/people/history/more/legacy/stats/log`를 부여한다. 플랫폼 로그·테스트가 배열 위치에 의존하지 않는다.
5. 속도는 현재 값+선택 패널6종, 진행·정지는 상시 접근. faulted이면 XO-07의 재개 제한과 복구 안내를 유지한다.
6. 뒤로는 상세→더보기 하위→천하→플랫폼 순. 탭/상세 상태를 재사용하고 변경된 화면만 갱신한다.

## 수용 기준
- [ ] 기존7화면·7상세가 새 탐색에서 모두 열리고 더보기 하위의 뒤로가 올바르다.
- [ ] 360×640·412×860과 글자100/130/200%에서 조작 잘림·수평 overflow가 없다.
- [ ] 기본 본문 높이·48 hit 영역·4.5:1 대비가 15의 기준을 충족한다.
- [ ] 속도6종과 정지·재개·faulted 상태가 실제 실행기에 반영되며 표시와 다르지 않다.
- [ ] 렌더·뒤로·safe area·속도 선택·faulted UI 회귀 검사와 전후 PNG를 첨부한다.
- [ ] 포맷·코드 규칙·Release 경고0·전체 테스트 통과. UI만의 변경으로 세계 RNG·저장 값이 변하지 않는다.

검증: `Navigation_PreservesAllDestinations`, `Back_FromMoreChild_ReturnsToMore`, `SpeedSheet_UsesRunnerSpeeds`,
`Layout_LargeTextKeepsControlsReachable`. Android 새 배치의 최종 증거는 UI-QA에서 다시 확보한다.
