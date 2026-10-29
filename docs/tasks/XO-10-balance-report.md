# [XO-10] 선협 관찰 — 장기 밸런스·저장 크기 측정 보고서

상태: 승인 대기 · 우선순위 P2 · 선행: XO-06 병합 후 실행(난수 변경 뒤 측정) · 검토 근거: [13 §2 R-04·R-16·R-20, §3](../../games/xianxia-observer/docs/13-claude-review.md)

## 배경 (왜)
3시드 측정으로는 비승·종문 균형 목표를 정할 수 없다. 식으로 계산하면 자연 비승이 사실상 불가능하다(추정).
밸런스 수치를 바꾸기 전에 **경지별 도달 시간·체류 시간·영기 출처**를 측정하는 도구가 필요하다. 이 작업은 수치를 바꾸지 않는다.

## 범위
- 포함: `games/xianxia-observer/tools/Xianxia.Tools/`(보고 항목 추가, 필요하면 파일 분리), 코어의 **읽기 전용 통계 누계**,
  결과 원문 `games/xianxia-observer/docs/measurements/balance-10-seeds.txt`.
- 제외: 밸런스 상수 변경, UI 변경, 성능 최적화.

## 동작 규칙
1. 새 옵션: `report --seeds 1-10 --years 30000 --population 1000 --balance`(시드 범위 표기 `a-b` 지원).
2. 시드별 출력(표 형식, 기존 보고서 뒤에 추가):
   - 경지별 **첫 도달 연도**(0년 이후 돌파 기준, 없으면 `-`).
   - 5,000년마다 경지 분포 스냅샷.
   - 경지별 **평균 체류 연수**(그 경지 초기 도달 ~ 다음 경지 도달 또는 생 종료), 표본 수.
   - 경지 6~8 인물의 영기 출처 비율: 수련 / 기연(비율형) / 비경 영기 / 천기 기연 / 지도. (영기 증가 지점 5곳에 출처별 누계 추가.
     `Statistics`에 저장되지 않는 `[JsonIgnore]` 진단 누계로 두고, 기존 `XIANXIA_GROWTH=1`과 같은 환경 변수로만 켠다.)
   - 비승 횟수·연도, 전쟁별 승자·점령지 변화, 연속 패배 최대 길이.
   - 영석 총량을 5,000년마다 출력.
   - 마지막 해 저장 크기(gzip 바이트), 저장·로드 시간(ms, 5회 평균).
3. 10시드 요약: 각 지표의 최소·중앙값·최대.
4. 추가 측정(같은 도구, `--population 3000 --years 100000 --seeds 1`): 저장 크기·로드 시간·Souls/Wars/Tournaments 개수.
5. 진단 누계가 꺼져 있을 때 시뮬레이션 결과(직렬화 바이트)가 바뀌지 않아야 한다. 켜져 있어도 난수 호출을 하지 않는다.

## 수용 기준 (Acceptance Criteria)
- [ ] 2의 모든 항목이 10시드 각각과 요약에 출력된다.
- [ ] 진단 켬/끔 두 실행의 시드 1·1,000년 직렬화 바이트가 같다(테스트).
- [ ] 결과 원문과 실행 명령·환경(SDK 버전, CPU)을 `measurements/balance-10-seeds.txt`와 `measurements/README.md`에 기록한다.
- [ ] 결과 해석은 쓰지 않는다(목표 확정은 오너·클로드 몫). 표만 남긴다.
- [ ] 포맷·코드 규칙·Release 빌드 경고 0·전체 테스트 통과.

## 테스트 케이스
- `Diagnostics_DoNotChangeSimulation`
- `BalanceReport_ParsesSeedRange`

## 참고
- 기존 도구: `tools/Xianxia.Tools/Program.cs`(197줄, 400줄 제한 주의).
- 영기 증가 지점: `ActionEffects.cs:23·321`, `SecretRealmSystem.cs:167`, `InterventionSystem.cs:72`, `RelationshipSystem.cs:95`.
- 측정 후 클로드가 [13 §3](../../games/xianxia-observer/docs/13-claude-review.md) 목표를 수치로 제안하고, 오너 결정(D-02·D-03) 뒤 밸런스 명세를 쓴다.
