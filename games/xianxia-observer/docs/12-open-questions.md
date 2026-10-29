# 12. 문제·미결정 사항

상태: 문서 작성 중 발견한 **관측과 검토 제안**. 이번 작업에서 게임 코드를 수정하지 않았다.
우선순위는 작성자 제안이며 오너·클로드의 승인 전이다. 같은 입력의 재현 실패, 입력이 달라 생긴 변화,
표시 의미의 선택, 미검증 위험을 구분한다.

재현값은 [review-observations.json](measurements/review-observations.json), 프로그램과 명령은
[추출 안내](reference/extraction.md)에 있다. private 반사 호출 사례는 경계 연산을 격리한 결과다.

## OQ-01. 관심 표시가 이후 세계를 바꾼다

- **구분:** 재현된 동작 + 기획 결정. **우선 제안:** P1 결정.
- **근거:** [BattleAftermath](../src/Xianxia.Simulation/Systems/BattleAftermath.cs),
  [LifeCycleSystem](../src/Xianxia.Simulation/Systems/LifeCycleSystem.cs),
  [BreakthroughSystem](../src/Xianxia.Simulation/Systems/BreakthroughSystem.cs),
  [SecretRealmSystem](../src/Xianxia.Simulation/Systems/SecretRealmSystem.cs).
  관심이면 로그를 바로 기록하고, 아니면 `Random.Chance`로 표본 로그를 남기는 분기가 있다.
  단축 평가로 호출 횟수가 달라지며 세계는 단일 PCG를 사용한다.
- **실험:** 시드7·100명·600년, 관심0명과100명을 비교하면 RNG 상태와 핵심 상태가 다르다.
  관심도 저장되는 입력이므로 **동일 입력의 결정론 실패**로 부르지는 않는다.
- **결정:** 관심을 순수 관찰로 볼지 세계를 바꾸는 입력으로 볼지. 비개입 관찰이면 세계/기록 난수를
  분리하거나 로그 선택을 상태에 영향 없는 함수로 만들고 관심 A/B 동일성 검사를 제안한다.

## OQ-02. 공법 보상이 기존 고급 공법을 낮춘다

- **구분:** 격리 연산에서 재현된 결함 후보. **우선 제안:** P1.
- **근거:** [ActionEffects.GainTechnique](../src/Xianxia.Simulation/Systems/ActionEffects.cs)는
  `min(15, 기존등급+획득량)`이다. [SecretRealmSystem](../src/Xianxia.Simulation/Systems/SecretRealmSystem.cs)은
  공법 보상을 `min(18, 기존등급+1)`로 처리한다.
- **실험:** 등급18에 +1 기연 연산을 호출하면 등급15로 떨어진다. 등급19의 비경 보상도 정적 식상18이 된다.
  고급 공법의 자연 보상 발생 빈도는 측정하지 않았다.
- **제안:** “보상으로 공법이 하락하지 않는다”를 합의하고, 상한보다 높은 기존 등급도 유지하는지
  전 등급 경계 검사로 확인한다. 상한 자체를15/18로 둘지는 별도 밸런스 결정이다.

## OQ-03. 의미적으로 잘못된 저장이 로드 예외로 빠져나온다

- **구분:** 재현된 입력 검증 공백. **우선 제안:** P1.
- **근거:** [WorldSerializer](../src/Xianxia.Simulation/Persistence/WorldSerializer.cs),
  [World.RebuildIndexes](../src/Xianxia.Simulation/Model/World.cs),
  [SaveStore](../src/Xianxia.App/Services/SaveStore.cs).
  gzip·JSON 파싱 이후 ID/참조 검증 전에 지역 목록을 인덱싱한다.
- **실험:** 인물 RegionId=999를 담은 유효 gzip JSON 로드는 ArgumentOutOfRangeException을 던진다.
  TryLoad의 SaveFormatException·IO·권한 catch 범위 밖이다. 시작 시 자동 로드에도 같은 경로가 있다.
- **제안:** 크기·버전·필수 목록·ID·범위·참조를 검증한 새 World만 교체하고, 예상 가능한 형식 오류는
  일관된 오류로 안내한다. 정상 슬롯·손상 원본 보존과 null/중복/대형 입력 검사가 필요하다.

## OQ-04. 최고 공법 신품의 정상 공급 경로가 보이지 않는다

- **구분:** 정적 경로 조사 + 콘텐츠 결정. **우선 제안:** P1/P2 결정.
- **근거:** [Techniques](../src/Xianxia.Simulation/Data/Techniques.cs),
  [CultivatorFactory](../src/Xianxia.Simulation/Systems/CultivatorFactory.cs),
  [SectEconomy](../src/Xianxia.Simulation/Systems/SectEconomy.cs),
  [Commerce](../src/Xianxia.Simulation/Systems/Commerce.cs),
  [AuctionSystem](../src/Xianxia.Simulation/Systems/AuctionSystem.cs).
  일반 초기0~2, 개창 원로최대5, 상회최대9, 장경각최대18, 기연최대15, 비경최대18,
  전생 각성최대12, 경매10~16이다. 정의에는19 신품과 설명이 존재한다.
- **판단:** 조사한 정상 획득 경로에19가 없다. 상태 공간 전체의 도달 불가능성 증명은 아니다.
- **결정:** 특별 보상 경로를 만들지, 정의·도감 설명을 정리할지, 미사용 예약 등급으로 명시할지.

## OQ-05. 사건 영기 보정이 서로 덮인다

- **구분:** 격리 상호작용에서 재현. **우선 제안:** P1.
- **근거:** [SecretRealmSystem](../src/Xianxia.Simulation/Systems/SecretRealmSystem.cs)은 시작1.2·종료1,
  [BeastTideSystem](../src/Xianxia.Simulation/Systems/BeastTideSystem.cs)은 시작.8·종료1을 직접 대입한다.
  자연 발생은 같은 지역의 기존 사건을 포괄 배제하지 않는다. UI는 같은 종류 중복만 막는다.
- **실험:** 지역8에 두 사건을 겹친 뒤 비경 Close만 호출하면 수조1개가 남아도 영기 .8→1이 된다.
  같은 종류 복수 중첩도 코어에서 강제 금지하지 않는다.
- **결정:** 사건 배타 또는 효과 합성·우선순위 정책을 정하고, 종료 순서와 개입 비용의 원자성을 검사한다.

## OQ-06. 표시·주석과 수식의 배율이 다르다

- **구분:** 정적 불일치. **우선 제안:** P2.
- **근거:** [SectRules](../src/Xianxia.Simulation/Data/SectRules.cs)의 영맥진 표시는 수련+6%/단계,
  [Growth.PlaceFactor](../src/Xianxia.Simulation/Systems/Growth.cs)는+4%/단계다.
  [Traits.IsVariant](../src/Xianxia.Simulation/Data/Traits.cs) 주석은 변이 수련+8%지만 실제 Growth는+5%다.
- **결정:** 정식 수치를 선택해 소스·도감·설명·문서를 맞춘다. 문서는 현재 실제 식을 기준으로 썼다.

## OQ-07. 영혼 우승 횟수가 실제 우승 총횟수와 다르다

- **구분:** 정적 집계 의미 불일치 후보. **우선 제안:** P2.
- **근거:** [TournamentSystem.Conclude](../src/Xianxia.Simulation/Systems/TournamentSystem.cs)는 매 대회
  종문 우승 수를 증가시키지만, [RecordSystem.UpdateSoul](../src/Xianxia.Simulation/Systems/RecordSystem.cs)는
  생애 종료 때 TournamentBest==1이면 영혼 우승 수를 한 번 증가시킨다.
- **영향:** 한 생에서 여러 번 우승해도 영혼 누계에는1이 더해진다. 현재 표시는 “우승 N회”다.
- **결정:** 우승한 생의 수를 셀지 실제 대회 우승 수를 셀지. 현생·영혼·종문 수치를 분리해 정의한다.

## OQ-08. 최초 경지 기록에 개창 원로가 포함되지 않는다

- **구분:** 재현된 동작 + 기획 결정. **우선 제안:** P2.
- **근거:** [WorldFactory](../src/Xianxia.Simulation/Systems/WorldFactory.cs)는 초기 금단 원로를 만들고,
  [RecordSystem](../src/Xianxia.Simulation/Systems/RecordSystem.cs)의 최초 기록은 진행 중 돌파에서 남긴다.
- **실험:** 0년에 금단 이상2명이 있는데 FirstReach[2]는 null이다.
- **결정:** “플레이 시작 이후 최초 돌파”인지 “세계의 최초 도달”인지 명명과 초기화 정책을 맞춘다.

## OQ-09. Android 전체 입력·가독성 수용 증거가 부족하다

- **구분:** 확인된 검증 공백. **우선 제안:** P1(입력 증거), P2(UX 개선).
- **근거:** 공식 APK 에뮬레이터의 Pixel Launcher ANR가 탭 입력을 가렸다. 프로세스·게임 화면·진행·
  저장 재실행은 확인했으나 전 탭의 성공은 증명하지 못한다([08](08-validation.md)).
- **검토:** 시스템 UI가 정상인 환경에서 전 탭·상세·IME·큰 글자·safe area·뒤로 가기·복귀를 확인한다.
  관찰된 상단 여백·토스트 겹침은 앱 단독 재현과 원인 분리가 필요하다.
  360폭 헤드리스 렌더가 터치 영역·접근성·기기별 표시를 보장하지 않는다.

## OQ-10. 일부 장기 이력과 저장 크기가 계속 증가한다

- **구분:** 정적 구조 + 성능 미검증. **우선 제안:** P2.
- **근거:** [World](../src/Xianxia.Simulation/Model/World.cs)의 Wars·Tournaments·Immortals·Souls에는
  상한이 없다. 로그·연대기·통계에는 제한이 있지만 세계 전체 크기의 상한은 아니다.
- **제안:** 목표 연도와 인구에서 메모리·압축 크기·저장/로드·조회 지연을 측정하고 요약·보관 정책을 정한다.
  현재 자료로 메모리 누수나 OOM이 실제 발생했다고 주장하지 않는다.

## OQ-11. 실패한 해의 상태와 정지·재개 계약이 불분명하다

- **구분:** 정적 오류 처리 공백. **우선 제안:** P2.
- **근거:** [WorldSimulator](../src/Xianxia.Simulation/WorldSimulator.cs)는 Year를 먼저 증가시키고 상태를
  제자리 변경한다. [SimulationRunner](../src/Xianxia.App/Services/SimulationRunner.cs)는 일반 Step 예외를
  잡아 정지하지만 롤백하지 않는다. “오류 직전 해까지 그대로”라는 주석은 원자성을 구현하지 않는다.
- **영향:** 부분 처리 상태에서 다시 진행·저장할 수 있다. 8ms 묶음도 한 Step이 길면 초과한다.
- **결정:** 실패 시 마지막 정상 스냅샷 복구·재개 금지·진단 저장 등 정책과 사용자 안내를 정한다.
  Stop 뒤 Start는 `_stopping`을 초기화하지 않으므로 실행기는 일회 수명으로 문서화하거나 재사용 계약을 구현한다.

## OQ-12. 정지 중 자동 저장과 파일 복구의 경계가 있다

- **구분:** 정적 동작·내구성 계약 공백. **우선 제안:** P1/P2 결정.
- **근거:** Runner는 정지 중 Monitor.Wait하므로 정지 상태의 관심·개입 변경 후90초만 기다려도
  자동 저장이 수행된다고 보장하지 않는다. [GameSession](../src/Xianxia.App/Services/GameSession.cs)의
  압축·[SaveStore](../src/Xianxia.App/Services/SaveStore.cs)의 임시 파일 교체에는 fsync·세대 백업이 없다.
- **영향:** 강제 종료 전 미저장 입력, 전원 차단·큰 저장의 지연을 현재 증거로 보장하지 못한다.
  `.broken` 하나가 덮어써지고 시작 자동 로드는 동기식이다. 저장 지연·파일 고장 주입은 미측정이다.
- **결정:** 자동 저장의 실시간 계약, 보존 세대, 실패 안내, 강제 종료 수용 범위를 정한다.

## OQ-13. 공통 표준의 장르별 적용 범위를 확정해야 한다

- **구분:** 기술 거버넌스 결정. **우선 제안:** P2 결정.
- **근거:** 루트 Hora의 Money·월별 모델·JSON 설정·다중 RNG 설계와 실제 Xianxia 구현이 다르다.
  가까운 props는 net10.0·현재 C#을 사용한다([11](11-legacy-and-decisions.md)).
- **결정:** 영석 정수 장부, 실수 확률·성장, 데이터 표·RNG 스트림의 허용 범위를 Xianxia ADR로 남긴다.
  다른 게임의 표준 차이만으로 바로 플레이 결함이라고 단정하지 않는다.

## OQ-14. 연도와 전쟁 사상자의 표시 의미를 정리해야 한다

- **구분:** 정적 용어 문제. **우선 제안:** P2.
- **근거:** [CultivatorFactory](../src/Xianxia.Simulation/Systems/CultivatorFactory.cs)의 BornYear는16세
  재시작 기준이며 통상 출생 연도가 아니다. [BattleAftermath](../src/Xianxia.Simulation/Systems/BattleAftermath.cs)의
  War.Casualties는 경지 순위 하락을 세며 좌화·전투 즉사 총계가 아니다.
- **결정:** 필드 의미를 유지하고 표시를 명확히 할지, 실제 생년·사망 수 등 별도 지표를 도입할지.

## OQ-15. 코어 개입 API는 UI만큼 입력을 검증하지 않는다

- **구분:** 정적 API 계약 공백. **우선 제안:** P2.
- **근거:** [InterventionSystem](../src/Xianxia.Simulation/Systems/InterventionSystem.cs)은 천기를 먼저
  차감하고 지역 사건 생성 API를 호출한다. 지역 ID·중복은 모든 코어 진입점에 강제되지 않는다.
  인물 기연도 대상 객체가 해당 World의 생존 인물인지 포괄 검증하지 않는다.
- **해석:** 현재 UI는 정상 링크·같은 종류 중복 제한을 제공하지만, 향후 도구·자동화·저장 복구 코드가
  코어를 직접 호출하면 다른 전제가 필요하다. 실제 UI에서 천기 유실을 관측한 것은 아니다.
- **결정:** 코어가 신뢰할 입력 범위와 실패 시 비용 불변성을 정의하고 경계 검사한다.

## 검토 결과

2026-10-29 클로드 검토 결과는 [13](13-claude-review.md)에 있다. OQ-02는 범위가 넓어졌고(16~18등급 모두 하락),
OQ-05에는 `Region.Status` 덮어쓰기가 추가됐다. 비승 0의 구조적 원인(R-04)이 새 항목으로 추가됐다.

## 검토 결과를 남기는 법

클로드는 각 항목을 확정 결함·제품 선택·검증 필요·기각으로 분류하고 근거와 권장 수용 기준을 적는다.
기각한 항목도 이유를 남긴다. 오너가 채택한 항목만 다음 구현 명세로 옮긴다.
