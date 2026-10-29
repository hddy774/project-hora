# [XO-08] 선협 관찰 — 표시·집계 용어 정합

상태: 승인 대기 · 우선순위 P2 · 선행 결정: D-04(신품 예약 표기), D-05(라벨 정정) · 검토 근거: [13 §2 R-05·R-08~R-11](../../games/xianxia-observer/docs/13-claude-review.md)

## 배경 (왜)
화면·도감·주석의 수치와 용어가 실제 계산과 다르다. 이 작업은 **계산은 그대로 두고 표시를 계산에 맞춘다.**
저장 형식·난수·규칙 변경이 없다.

## 범위
- 포함: 아래 표의 문자열·XML 주석만 바꾼다. 테스트 `tests/Xianxia.App.Tests/LabelTests.cs`(신규) 또는 기존 UI 테스트에 문자열 단언 추가.
- 제외: 실제 우승 횟수 필드 추가, 개창 원로를 최초 기록에 넣는 규칙, 영맥진·변이 수치 변경, 19등급 획득 경로.

## 동작 규칙 (바꿀 문자열 전체)

| # | 파일:줄(기준 `137fbfa`) | 현재 | 변경 후 |
|---|---|---|---|
| 1 | `Xianxia.Simulation/Data/SectRules.cs:50` | `"본산·영지 수련 +6%/단계"` | `"본산·영지 수련 +4%/단계"` |
| 2 | `Xianxia.Simulation/Data/Traits.cs:47` 주석 | `수련 +8%, 전투 +5%` | `수련 +5%, 전투 +5%` (Growth.cs:40, CombatSystem.cs:76 모두 1.05) |
| 3 | `Xianxia.Simulation/Data/Techniques.cs:15` 주석 | `신품(19)은 비경·경매로만 얻는다.` | `신품(19)은 현재 정상 획득 경로가 없는 예약 등급이다(비경 최대 18, 경매 최대 16).` |
| 4 | 도감(`Xianxia.App/Sheets/HelpSheet.cs`)의 공법 등급 표 또는 설명 | 19등급을 일반 등급처럼 표시 | 19등급 행/설명 끝에 `" (현재 얻을 수 없음)"` |
| 5 | `Xianxia.Simulation/Systems/RecordSystem.cs:228` | `"대비무 우승 " + N + "회"` | `"대비무 우승한 생 " + N + "번"` |
| 6 | `Xianxia.App/Pages/HistoryPage.cs:125` | `"최다 대비무 우승"` | `"대비무 우승이 가장 많은 영혼(생 단위)"` |
| 7 | `Xianxia.App/Pages/HistoryPage.cs:116` | `Realms.Name(realm) + " 최초"` | `Realms.Name(realm) + " 첫 돌파(개벽 이후)"` |
| 8 | `Xianxia.App/Sheets/CultivatorDetails.cs:41` | `… + " 시작"` | `… + " 입세"` (현재 생을 16세로 시작한 해) |
| 9 | `Xianxia.App/Sheets/CultivatorSheet.cs:133` | `BornYear~DeathYear` 표시 | 앞에 `"입세 "`를 붙임: `"입세 {BornYear}~{DeathYear}"` |
| 10 | `Xianxia.App/Sheets/WarSheet.cs:39` | `"사상자 / 배상 영석"` | `"경지 하락 / 배상 영석"` |
| 11 | `Xianxia.App/Sheets/WarSheet.cs:52` | `"사상자"` | `"경지 하락"` |
| 12 | `Xianxia.App/Pages/WorldPage.cs:139` | `… Casualties + "명"` 주변 문구 | `"경지 하락 " + N + "건"` |
| 13 | `Xianxia.App/Pages/SectsPage.cs:50` | `"전쟁 사상자"` | `"전쟁 경지 하락"` |
| 14 | `Xianxia.Simulation/Model/*.cs`의 `BornYear`·`Casualties`·`TournamentWins`(SoulRecord) XML 주석 | 의미 불명확 | 위 의미를 한 문장으로 명시 |

- 바꾼 문구가 내장 폰트 부분집합에 없는 글자를 쓰면 `tools/make-font-subset.py`로 폰트를 다시 만든다(현재 문구 글자는 모두 KS 완성형이므로 불필요할 가능성이 높다; PR에 확인 결과를 적는다).
- 문서 `games/xianxia-observer/docs/02·03·04`의 같은 용어도 함께 고친다.

## 수용 기준 (Acceptance Criteria)
- [ ] 위 14개 변경이 모두 반영됐고, `grep -rn "사상자" src/`가 0건.
- [ ] 헤드리스 UI 테스트에서 전쟁 상세·사기 기록 화면에 새 라벨이 보인다(문자열 단언 2건 이상).
- [ ] 같은 시드 1,000년 진행의 직렬화 바이트가 변경 전과 같다(규칙 무변경 증명). 단 #5는 기록 문자열이 저장되므로 해당 문자열만 다를 수 있다 — 비교 시 `Records` 아래 `RecordEntry.Detail` 값은 제외한다.
- [ ] 360폭 렌더 스모크 통과, 새 라벨 잘림 없음(PNG를 PR에 첨부).
- [ ] 포맷·코드 규칙·Release 빌드 경고 0·전체 테스트 통과.

## 테스트 케이스
- `WarSheet_ShowsRankLossLabel`
- `HistoryPage_ShowsFirstBreakthroughLabel`
- `Labels_DoNotChangeSimulation`

## 참고
- 실제 값 근거: `Growth.cs:40·91~105`(변이 1.05, 영맥진 0.04), `CombatSystem.cs:76`(변이 1.05), `RecordSystem.cs:65~70·161~163·224~228`.
