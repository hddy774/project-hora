# 기술 아키텍처

## 선택 기술

- 언어: C#
- 런타임: .NET 10
- PC UI: WPF
- 대상: Windows x64
- 배포: self-contained 단일 폴더 패키지

## 모듈

```text
Chronicles.Core
    └── Chronicles.Simulation
            ├── Chronicles.Persistence
            │       └── Chronicles.Desktop
            └──────────── Chronicles.Desktop

Chronicles.Tests ──> Core + Simulation
```

### Chronicles.Core

안정 Agent ID, 정수 simulation tick, 0.01 단위 공통 품질값과 경계 배율, 고정된 정의 ID, 결정론적 난수와 전투력 값
타입을 소유한다. UI, 파일 시스템과 WPF를 참조하지 않는다.

### Chronicles.Simulation

10,000명 권위 상태, 영혼·육신·인물 식별, 목표→결정 인과 trace, 단계형 윤회·
생 계보·6영역 전생 흔적, 희소 방향 관계와
사제 Teaching, AI 결정, 9층 수행·돌파, 공법·법술 품질·수련 경험·숙련·이해,
도시 임무·재료·영석·법보·단약 경제, 지도 이동·지형별 요수, 교대 전투 타임라인,
실제 전투 속성과 표시 전투력,
전투력·경지·자질·부자 랭킹, 기간별 TOP 1,000 랭킹 이력, 전체 인물 탐색, 생애
일대기, 수명 판정과 윤회를 소유한다. 렌더 프레임과 벽시계에 의존하지
않는다. 안정 Agent ID는 영혼 슬롯으로 유지하고 별도의 `SoulId`는 생을 넘어
연속된다. `BodyId`와 `PersonId`, 생 번호·이름·탄생 tick은 생마다 새로 발급해 현생과
과거 생을 구분한다.

달력의 권위는 계속 정수 tick 하나이며 년·월·일은 `GameCalendar`의 12개월×30일
정수 비율 함수로 파생한다. 각 인물은 `lastActionMonth`, 마지막 행동 tick과 행동 누계를
저장하고 예약 함수가 다음 행동을 반드시 다음 달 이후로 올려 같은 달의 중복 행동을
차단한다.

수행은 Notion Alpha 1.1.3 계약에 맞춰 `CultivationState`와 원인별 병목을 권위 상태로 둔다.
`CultivationProgress`는 현재 단계의 누적 Work이고 Realm Profile이 단계 요구량·수명·
안정 ID·revision을 제공한다. Work 축적, 준비, Preflight, 시도, 병목 또는 안착은 서로
다른 월 행동 경계이며 돌파 시도 전에는 RNG를 소비하지 않는다. `World.Cultivation`은
상태 전이 owner, `RealmRules`는 Work·Gate·확률·이월 공식 owner,
`CultivationModel`은 저장/조회 계약 owner다. 9층 Work와 대원만은 서로 다른 상태다.

`AptitudeModel`은 영근·근골·체질·오성·정신·영혼 6개 영역과 영역별 10개 Component,
안정 ID, `F-TRAIT-002` 대표값 공식과 수행/전투 파생 능력을 소유한다. 권위값 60개는
`AgentStore`의 `ushort[population × 60]` 평탄 SoA에 저장하고 대표값은 영역별 10개
산술평균으로 파생한다. 실제 공식은 대표값이 아니라 핵심 Component 30%+30%, 보조
Component 20%+20% 조합을 사용한다. 5개 역할 점수와 수행/전투 문맥별 자질은 탄생·
환생·복원 시 캐시하며 월 행동 hot path와 관찰에서 60개를 반복 집계하지 않는다.
영혼 Component 10개는 윤회에 보존하고 다른 50개는 새 육신에서 재생성한다.
미확정 분포·역할 조합·영근 세분화는 안정 검토 ID와 `RequiresReview`로 추적한다.

`PersonCausalModel`은 Soul/Body/Person 안정 ID, 최종 목표 `랭킹 1위`, 현재 보조 목표,
선택 행동·차선 행동·이유·typed Resolution·판단 revision을 소유한다. 생의 경계에서는
SoulId만 이어지고 육신과
인물 ID는 새로 발급된다. 관찰 query는 이 인과 상태를 읽을 뿐 목표나 판단 revision을
갱신하지 않는다. 임시 AI 효용 가중치는 `PC-TEMP-AI-UTILITY-113`으로 추적한다.

`ReincarnationModel.cs`는 안정 `LifeRecordId`·`JourneyId`, DTO와 기간·단계·등급
규칙을 소유한다. `ReincarnationStore.cs`는 인물별 추가 전용 생 계보, 개인 역사적
최고점, 현재 여정과 6영역 전생 흔적의 상태 전이·revision별 읽기 전용 projection
캐시를 소유한다. `ReincarnationStore.Persistence.cs`는 checkpoint·복원 검증·상태
hash를, `World.Reincarnation.cs`는 사망부터 환생까지 다른 권위 시스템과의 조정을
맡는다. 생 계보는 직전
생 ID, Soul/Body/Person ID, 생 번호·당시 이름·탄생/사망 tick·사망 원인·최고점·
여정 ID를 보존한다. 여정은 `Entering`, `Crossing`, `LateCrossing`,
`CrossingComplete`, `PreparingRebirth`의 정해진 단계와 시작/단계 시작/완료 tick,
revision을 가진다. 연기~화신의 총 3·5·8·13·21년은 정본이고 상위 경지
34·55·89·144·233년은 `PC-TEMP-REINCARNATION-UPPER-DURATION-113`, 누적 단계 경계
1/12·8/12·10/12·11/12·12/12는 `PC-TEMP-REINCARNATION-STAGES-113`으로 추적한다.

전생 흔적은 자서전적 기억, 관계·사회 기억, 수행 원리·경지 지식,
공법·법술·단약·제작 지식, 세계·세력·환경 지식, 육신·행동 흔적의 고정 6영역
평탄 배열로 보관한다. 각 영역의 preserved/access/integrated는 0~10,000 정수고
`integrated <= accessible <= preserved`를 강제한다. 보존 임계는 8,000/5,500/3,000,
접근 임계는 8,500/6,000/3,000이다. 환생 시 integrated를 0으로 두고 현생 경지·
기술·전투 상태에 전생 흔적을 자동 적용하지 않는다. 흔적 후보 가중치는
`PC-TEMP-REINCARNATION-TRACE-113`으로 분리한다. 모바일 부모·환경·용량 출생 후보 계약은
PC에서 제외하고 `PC-TEMP-REINCARNATION-BIRTH-CANDIDATE-113`으로 재검토한다.

`RelationModel`은 인물당 최대 8개 인접 관계, 안정 관계 ID, 호감/반감·신뢰/불신·
존중/경시·은혜·원한·경계의 6개 방향 축과 사제·우정·연애·실제 동료·지속 경쟁의
5개 역할 flag를 소유한다. 사제 방향은 별도의 MasterAgent로 구분한다. 관계
후보는 현재 위치의 제한된 인접 집합에서만 탐색하며 전 인구 쌍을 만들지 않는다.
checkpoint 복원은 관계 ID·양 끝점·중복·degree와 역할 방향을 검증한 뒤 양쪽
adjacency를 재구성한다. 생애 경계에서는 양방향 metrics·상호작용 횟수·대상별 Teaching 연속 상태를
초기화하고 과거 PersonId가 담긴 중요 history만 유지한다. 임시 변화량과 역할 임계값은
`PC-TEMP-RELATION-BALANCE-113`으로 추적한다.

`TeachingModel`과 `World.Teaching`은 같은 지역의 기존 관계와 지식 격차에서 후보를
선택하고 사제 관계를 성립시킨 뒤 공법 또는 법술 지식을 전수하는 권위 owner다.
대상 `KnowledgeRef`, 전수 출처, 결과와 효과,
`관계+대상+KnowledgeRef`별 연속성, 중요 영구 전수 이력을 저장한다. 세계 seed·
provenance·tick·session에서 계산하는 Teaching 전용 무상태 결정론 roll을 사용하며
대경지 수행 Work 직접 보너스는 항상 0이다. 공법·법술
수련 경험·이해·오해/교정·접근만 바꾸고, 전투·사망·윤회 생명주기 경계에서는 양방향
진행 문맥을 원자적으로 중단한다. 임시 확률·성장량은
`PC-TEMP-TEACHING-BALANCE-113`으로 추적한다.

`SkillProgressionRules`는 공법·법술의 누적 수련 경험, 0.00~100.00 숙련 곡선과
깨달음 이해도를 계산한다. `AptitudeCombatBonusFormula`는 Component 파생 역할 점수를
50%~200%로 제한해 공법·법술 효과/성장과 전투 숙련·이해·경험에 공급한다. 품질은
기술 객체의 불변 입력이며 시장에서 새 서적을
습득할 때만 교체한다. `PowerInputSignature`는 경지·기술·전투 경험 입력이 실제로
바뀐 행동 뒤에만 전투 파생값을 갱신해 대규모 월 행동의 중복 계산을 막는다.
`SkillCombatScaling`은 품질 경계 배율 계약과 표시/실전 기여 정규화를 함께 소유해
공법·법술 품질 규칙과 전투 공식을 중복 구현하지 않게 한다.

`CombatProgressionRules`는 경험에서 0.00~100.00 전투 숙련을 파생하고 별도 전투
이해 성장과 표시 기여 상한을 소유한다. `CombatAttributeFormula`는 공격·방어·최대
생명/영력·속도·관통·치명타·회피·판단력·집중력·법술/수행 효율을 계산한다.
`CombatPowerFormula`는 자질 직접 기여를 항상 0으로 두고 랭킹용 표시 점수만 계산한다.
`World.Combat`은 한 전투에 단 하나의 turn owner를 저장하고 다음 당사자만 예약한다.
누적 부상과 장기 행동 간격도 전투 checkpoint/hash에 포함한다. 다음 턴 예약은 공격자와
방어자 양쪽의 남은 수명 경계보다 늦을 수 없어 장기 전투 중 자연사 판정을 건너뛰지 않는다.

`WorldMapRules`는 3×3 위치·지형·요수 정의의 안정 ID owner이고 `World.Map`은 이동,
사냥, 전리품, 부상과 읽기 전용 지도 스냅샷을 소유한다. 인물 위치·목적지·사냥 누계는
연속 배열로 저장한다. 지역별 요수 개체수도 checkpoint/hash에 포함하며 사냥 감소와
월별 제한 재생을 결정론적으로 처리한다. 지도 조회 한 번으로 지역별 인구·평균/최고
전투력·최고 경지·최강자·행동 분포를 집계하며, 선택 지역의 `수선자` 내부 탭을 열었을
때만 안정 ID 순서의 100명 페이지를 투영한다. 지역 개요와 요수 생태 탭은 수선자 행을
만들지 않으며 UI는 지도 위치 배열을 직접 바꾸지 않는다.

대규모 인물 경제 상태는 인물별 객체 사전 대신 2칸 재료와 3칸 법보의 평탄 연속 배열로
보관한다. 재료 정의·가격·범주는 `EconomyRules`의 안정 enum/정의표에 있고, UI 문자열이나
상점 화면 상태는 권위 세계에 저장하지 않는다.

`CityMarket`은 법보/단약 재료 2개, 10대경지별 성장/돌파 단약·방어/공격 법보 40개,
공법서·법술서 2개의 총 44개 상품을 연속 배열로 보관한다. 도시 기준가와 현재가,
품질 합계를 분리하고 재고·누적 공급·누적 수요·도시 금고·유한 광맥·최대 4개 생산
대기열을 권위 상태와 save/hash에 포함한다. 임무 납품과 판매는 공동 재고 공급, 구매와
도시 가공은 수요다. 임무와 생산은 미충족 수요가 있을 때만 시작하며, 생산 자원은 시작
시 소비해 중복 예약을 막는다. 선택 알고리즘은 자원이 없는 최상위 수요를 건너뛰어 다른
생산 가능 품목이 굶지 않게 한다. 거래 UI 조회는 가격을 다시 계산하거나 거래를
발생시키지 않는다. 상품 DTO를 생략하는 도시 요약 조회에도 전체/재료/가공품 재고,
공급·수요·미충족 수요와 생산 용량을 파생 집계로 제공해 빈 상품 목록을 0으로 오인하지
않는다.

세계 중요 로그는 고정 100줄 순환 버퍼, 인물별 최근 로그도 고정 100줄 리스트다.
`LifeChronicleEntry`는 별도 무제한 목록으로 모든 생에 걸쳐 누적한다. 세 구조는 모두
사건 enum·안정 ID·정수값을 중심으로 저장하고 Desktop에서 문구로 변환한다. 세계
사건만 환생 뒤의 이름 혼동을 막기 위해 사건 당시 권위 인물명을 함께 보존한다. 전투
부상/사망, 이동과 요수 사건도 같은 타입 계약을 사용한다. 최근
로그는 오래된 줄을 버리지만 연대기는 절대 잘라내지 않으며 schema 7 체크포인트에 함께
저장한다. 연속된 같은 장기 행동은 새 행 대신 마지막 행의 지속 개월을 누적한다.
깨달음은 개인 최근 로그에만 남기고 영구 연대기에는 넣지 않으며, 과거 저장의 깨달음
연대기 항목도 schema 이전 과정에서 제거한다.
세계에는 별도의 무제한 영구 연대기를 두고 1위 교체·대경지 돌파·대원만·상위권
죽음처럼 세계적으로 지속할 사건만 추가한다. 세계 사건은 안정 ID와 함께 사건 당시
인물 이름을 보존해 영혼 슬롯이 환생해도 과거 기록의 이름이 변하지 않는다.
`WorldCenturyChronicle`은 이 개별 사건 열과 분리된 100년 단위 세계 역사서 owner다.
대경지별 돌파 횟수/고유 인원, 네 랭킹 TOP 1·3·5·10의 누적 자리 교체와 세기 말
지도자, 사망·윤회·환생 횟수/고유 인원과 윤회의 강 체류 인원, 전투·사냥·경제 합계를
작은 정수 배열과 agent bitset으로 집계한다. 100년이 닫힐 때만 한 권을 영구 확정하며
역사서 문장은 현재 선택한 UI 세부 탭에서만 지연 생성한다.
`SimulationRunner`는 UI 탭 상태와 무관하게 중요 사건을 파일 기록 계층에 전달하는
최대 10,000건 feed만 소유하며 파일 I/O를 직접 수행하지 않는다.

### Chronicles.Desktop

SimulationRunner가 알린 관찰 무효화 신호를 dispatcher에서 합친 뒤, 현재 보이는
구역 플래그로 요청한 단일 불변 observation만 읽는다. 일시정지, 배속, 검색과 추적
명령을 전달하고 WPF 화면을 그린다. 인물 상세는 최근 개인 로그와 영구 연대기를
분리하고, 우측 외부 탭은 통계·도시·시장 상세·3×3 지도·세계 기록 DTO만 표시한다.
통계 분포는 Expander 상태에 따라 투영하며, 외부 탭·인물 내부 탭·접힌 하위 표는
simulation 조회 단계부터 계산하지 않는다. 시장은 분류 토글·검색·상품/가격·수급/생산
내부 탭, 지도는 지역 개요·요수 생태·100명 수선자 페이지 내부 탭을 사용한다. 시장과
지도도 현재 열린 내부 탭의 행과 문구만 갱신하며 대형 표와 연대기는 WPF recycling
virtualization을 사용한다.
최근 로그는 사건 종류별 내부 탭, 영구 연대기는 사건 종류 탭과 생별 토글을 사용하며
이미 받은 선택 인물 DTO만 필터링한다. 지도 수선자 내부 탭은 선택 지역의 현재 페이지만
요청하며 인구 변동으로 페이지가 사라지면 마지막 유효 페이지로 자동 보정한다. 전투력
상세 Expander는 공식 revision, 원시 기여, 자질 보정 후 기여와
최종 `경지+공법+법술+전투 성장+법보` 합을 표시한다.
세부 선천자질 Expander는 접혀 있으면 `ObservationSections.AptitudeDetails`를 요청하지
않는다. 펼친 경우에도 선택 인물 하나의 60개 값만 DTO로 복사하고, 6개 내부 탭 중
현재 탭의 Component와 파생 공식 문자열만 갱신한다.
관계 Expander는 접혀 있으면 `ObservationSections.Relations`를 요청하지 않고,
`사제·가르침·전승 추적` Expander도 접혀 있으면 `ObservationSections.Teaching`을
요청하지 않는다.
관계 Expander를 펼친 경우에만 선택 인물의 최근 중요 관계·전수 history를 투영한다.
Teaching Expander는 선택 인물의 최신 교사 trace와 최신 학습자 trace만 투영하며,
교사·학습자 이름/ID, PersonId로 생애 연대기에서 복원한 전생 당시 이름, 대상
KnowledgeRef·출처와 결과별 효과 문구를 지연 생성한다.
인물 기록 영역의 세 번째 `윤회 연속성` 내부 탭은 선택한 때만
`ObservationSections.Reincarnation`을 요청하고 선택 인물 하나의 생 계보·현재 여정·
6영역 흔적만 투영한다. 다른 내부 탭을 보는 동안에는 이 DTO·표시 문구·행을
만들지 않으며 관찰 조회는 권위 상태와 RNG를 바꾸지 않는다.
`MapObservationView`, `MarketObservationView`, `PresentationText`와
`MainWindow.AgentObservation`, `MainWindow.Rankings`, `MainWindow.WorldRecords`가 지역,
시장, 공통 문구, 인물, 랭킹과 기록 관찰 책임을 분리한다. `WorldRecordsView`는 세계
로그·영구 연대기·디버그 내부 탭과 출력 제어를, `GameLogService`는 최대 500줄 세션
디버그 버퍼와 background UTF-8 파일 쓰기를 소유한다. 기록 탭은 마지막 사건 sequence가
바뀐 경우에만 현재 내부 탭의 행을 갱신하고 세계 연대기 화면은 최신 1,000건만 투영한다.
대경지 표기는 조회 DTO의 층/대원만 flag로 `대경지 · 초기/중기/후기/대원만 · N층`을
파생하며 simulation에 UI 문구를 저장하지 않는다.

### Chronicles.Persistence

Simulation의 명시적 체크포인트를 Brotli 압축 payload와 SHA-256 checksum을 가진
versioned `.cmip` container로 변환한다. 현재 schema 23은 수행 trace/구성요소와
입문 Gate 단련 보정, 기술 경험/이해, 전투 경험/이해·turn owner/부상, 지도 위치/사냥 상태를
비롯해 간략화 시장·생산 대기열·유한 광맥·요수 개체수, TOP 1,000 부자 순위,
기간별 랭킹 frame·누적 자리 변동, 개별 세계 사건, 100년 세계 역사서와 60개 선천자질
Component, Soul/Body/Person ID·AI 판단 인과, 희소 방향 관계, Teaching 진행·연속성·
영구 전수 이력, 생 계보·역사적 최고점·현재 윤회 여정·6영역 전생 흔적을
저장한다. schema 7~22에 안전한
이전 기본값과 진행 전투 단일 turn owner 복구를
제공하며, schema 13의 30종 재료·7칸 법보·35개 상품은 새 2종·3칸·44개 구조로
명시적으로 합산 이전하고 schema 17 이하 자질 대표값은 그룹별 균일 Component로
이전한다. 정상 10,000명 저장의 개인 로그/연대기가 압축 전 128MiB를
넘을 수 있어 JSON 해제 상한은 512MiB로 두되 압축 payload 64MiB 상한과 checksum은
유지한다. 같은 폴더의 임시 파일을 flush한 뒤 원자적으로
교체하며, 손상되거나 지원하지 않는 schema는 세계를 바꾸기 전에 거부한다.
하위 schema는 증거가 있는 생 계보만 재구성하며 기존 저장에 없던 6영역 수치를
조작하지 않고 `HasEvidence=false`로 표시한다. schema 23 복원은 여정 단계·예약 tick,
생 계보의 선행자·ID 중복·현재 신원과의 일치, 6영역 수치의
`integrated <= accessible <= preserved`를 검증한 뒤에만 세계를 교체한다.

### Chronicles.Tests

외부 테스트 프레임워크 없이 실행 가능한 결정론적 계약 러너다. 기존 검사에 단계형
윤회·생 계보·전생 흔적 전용 자동 검사를 추가했다. 전체 검사는 공식,
9층 수행, 숙련/이해/품질 불변, 교대 전투/부상 사망, 지도/요수, 경제, 관찰 무영향,
자질 간접 보정 경계, 전투 성장, 선택 UI 조회, 과거 저장 복구, 장기 성장·사냥·경쟁,
재료 부족 생산 건너뛰기, 지도 내부 탭 지연 투영의 시간·할당량, 부자 순위·기간별
랭킹 이력·누적 자리 변동·100년 세계 역사서, 60개 자질 Component·윤회 보존,
Soul/Body/Person 인과 trace, 희소 관계 양끝 연결·상한, Teaching 직접 Work 0·전수 이력·
schema 22 이전·지연 관찰, 윤회 총기간·단계 경계·3생 선행자 계보·Soul/Body/Person
연속성·6영역 대소 관계·현생 자동 복구 금지·schema 23/하위 schema 이전·지연 관찰,
여정 중간 저장 왕복과 10,000명 성능 예산을
자동 검증한다. 0.0.12 개발 시점의 Debug 통합 검사는 40/40, 경고 0·오류 0이며,
10,000명 초기화+20,000 tick+관찰은 3,102ms, schema 23 checkpoint/복원은
각각 122/567ms였다.

## 실행 모델

시뮬레이션은 전용 worker에서 고정 tick으로 실행한다. 1배속은 80ms마다 16 tick,
100배속은 1,600 tick을 진행해 초당 200×배속 tick 비율을 보존하면서 월 표시가
연도 경계에 고정되지 않게 한다. 각 AI는 매 tick 전부 판단하지
않고 `nextDecisionTick` 또는 예약된 전투 행동 시점에만 활성화된다. 활성화 뒤에도
마지막 행동 월을 확인해 달마다 최대 한 번만 상태 전이를 실행한다. worker는 제한된
주기로 관찰 무효화만 알리고, UI는 대기 중인 신호를 합쳐 현재 보이는 구역의 원자
observation 하나만 요청한다.

전투력·경지·자질·부자 순위 배열은 공식 갱신 경계에서 결정론적 동률 규칙과 함께 정렬한다.
UI에는 현재 선택한 범주의 상위 1,000명만 복사하고 전체 탐색은 랭킹 투영 없이 안정
Agent ID 기반 페이지 조회를 사용한다. 구현 초기에는 정확성을 우선하고 성능 측정
결과에 따라 순위 자료구조를 최적화한다.

전체 경지·전투력 분포와 승패·사망·경제 합계는 권위 배열을 읽는 불변 통계 DTO에서
계산한다. 랭킹 변동값만 공식 정렬 직전/직후의 순위를 비교해 권위 상태로 누적하며,
통계 화면 조회 자체는 난수·상태 hash·랭킹에 영향을 주지 않는다. 통계 요약, 경지
분포, 전투력 분포와 도시 경제 요약, 시장 상품은 서로 다른 `ObservationSections`
bit로 요청해 보이지 않는 표의 10,000명 집계와 배열 할당을 건너뛴다. 전투력·경지
랭킹의 직전 갱신 통계도 읽기 전용으로 제공한다. `RankingHistory`는 TOP 1,000 안정
ID를 연 1회 기록하고 최근 12년, 10년·100년·
1,000년 버킷으로 오래된 기준점을 압축한다. 통계 탭을 보고 있을 때만 이 frame들로
전투력·경지·부자의 TOP 1/3/5/10/100/500/1,000 자리 변동을 1년·10년·100년·
1,000년·1만년·전체 기간에 대해 파생하며 조회 자체는 이력을 추가하지 않는다.
