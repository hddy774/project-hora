# 소스·테스트 색인

기준 소스 17b48ad. 아래는 bin/obj·잠금 파일·이진 리소스를 제외한 읽기 대상 색인이다. 책임과 전체 호출 순서는 [아키텍처](../05-architecture.md)를 본다.

## 구현·도구 파일

### src/Xianxia.Simulation

- [src/Xianxia.Simulation/Core/Pcg32.cs](../../src/Xianxia.Simulation/Core/Pcg32.cs): 결정론적 난수 생성기 PCG32(XSH-RR 64/32). 같은 시드와 같은 호출 순서면 어느 기기에서도 같은 수열을 만든다.
- [src/Xianxia.Simulation/Data/Balance.cs](../../src/Xianxia.Simulation/Data/Balance.cs): 세계 전체에 걸친 밸런스 상수. 숫자를 바꿀 때는 tools/Xianxia.Tools 보고서로 분포를 다시 확인한다.
- [src/Xianxia.Simulation/Data/Enums.cs](../../src/Xianxia.Simulation/Data/Enums.cs): 시뮬레이션 전역에서 쓰는 열거형. 정수 값은 저장 파일에 기록되므로 순서를 바꾸지 않고 끝에만 추가한다.
- [src/Xianxia.Simulation/Data/Labels.cs](../../src/Xianxia.Simulation/Data/Labels.cs): 열거형의 한국어 표시 이름. 화면과 로그 문장이 같은 이름을 쓰도록 한곳에 모은다.
- [src/Xianxia.Simulation/Data/NameData.cs](../../src/Xianxia.Simulation/Data/NameData.cs): 수선자 이름·도호·비경 이름에 쓰는 음절 표. 원본 음절(NA·NB)을 포함해 중복 이름이 거의 없도록 넓혔다.
- [src/Xianxia.Simulation/Data/Realms.cs](../../src/Xianxia.Simulation/Data/Realms.cs): 경지(9개)와 단계(4개)의 이름·필요 영기·수명·전투력 기준 표. 수치 기준은 현행 02·03 문서에 정리했다.
- [src/Xianxia.Simulation/Data/RegionCatalog.cs](../../src/Xianxia.Simulation/Data/RegionCatalog.cs): 13개 지역의 고정 정의(원본 RGD 계승 + 위험도·영초·영철 풍부도·설명 추가).
- [src/Xianxia.Simulation/Data/SectRules.cs](../../src/Xianxia.Simulation/Data/SectRules.cs): 종문 직위 정원·녹봉·수련 배율, 시설 효과와 비용, 이념별 보정 표.
- [src/Xianxia.Simulation/Data/Techniques.cs](../../src/Xianxia.Simulation/Data/Techniques.cs): 공법 20등급의 이름·경지 한계·수련 배율·장경각 공헌 비용 표.
- [src/Xianxia.Simulation/Data/TitleCatalog.cs](../../src/Xianxia.Simulation/Data/TitleCatalog.cs): 호칭(칭호) 정의. 수선자가 생애 중 업적으로 얻으며 업적 점수와 화면 표시에 쓰인다.
- [src/Xianxia.Simulation/Data/Traits.cs](../../src/Xianxia.Simulation/Data/Traits.cs): 영근 속성 상극, 성격별 행동 가중치, 재능 등급 규칙.
- [src/Xianxia.Simulation/Model/Chronicle.cs](../../src/Xianxia.Simulation/Model/Chronicle.cs): 연대기(세계의 주요 사건)와 화면 알림 대기열.
- [src/Xianxia.Simulation/Model/Cultivator.Life.cs](../../src/Xianxia.Simulation/Model/Cultivator.Life.cs): 수선자의 인연·현생 전적·호칭·생애 기록·전생 요약.
- [src/Xianxia.Simulation/Model/Cultivator.cs](../../src/Xianxia.Simulation/Model/Cultivator.cs): 수선자(영혼 하나)의 상태. 영혼 번호는 윤회해도 유지되고, 현생 상태는 환생 때 초기화된다.
- [src/Xianxia.Simulation/Model/EventLog.cs](../../src/Xianxia.Simulation/Model/EventLog.cs): 사건 기록(최신 600건 링 버퍼)과 분류별 누적 건수.
- [src/Xianxia.Simulation/Model/Records.cs](../../src/Xianxia.Simulation/Model/Records.cs): 세계 기록, 영혼별 업적(원본 HD), 영석 장부.
- [src/Xianxia.Simulation/Model/Region.cs](../../src/Xianxia.Simulation/Model/Region.cs): 지역의 변하는 상태(지배 세력·세력 점수·전투 수·일시적 영기 변화)와 거주자 색인.
- [src/Xianxia.Simulation/Model/Sect.cs](../../src/Xianxia.Simulation/Model/Sect.cs): 종문 상태: 금고·시설·직위 인원·전적. 구성원은 수선자의 SectId로 정하고 여기에는 집계만 둔다.
- [src/Xianxia.Simulation/Model/Statistics.cs](../../src/Xianxia.Simulation/Model/Statistics.cs): 시간에 따른 통계 표본. 그래프와 균형 점검에 쓴다.
- [src/Xianxia.Simulation/Model/WarState.cs](../../src/Xianxia.Simulation/Model/WarState.cs): 전쟁 진행 상태와 끝난 전쟁의 기록.
- [src/Xianxia.Simulation/Model/World.cs](../../src/Xianxia.Simulation/Model/World.cs): 세계 전체 상태. 이 객체 하나를 저장하면 같은 시드의 진행을 그대로 이어 갈 수 있다.
- [src/Xianxia.Simulation/Model/WorldEvents.cs](../../src/Xianxia.Simulation/Model/WorldEvents.cs): 세계 사건 상태: 시대, 대비무 대회 기록, 비경, 수조, 경매 기록.
- [src/Xianxia.Simulation/Persistence/WorldSerializer.cs](../../src/Xianxia.Simulation/Persistence/WorldSerializer.cs): 세계 저장·불러오기. System.Text.Json 소스 생성기를 써서 반사(reflection) 없이 동작한다(Android 트리밍 안전).
- [src/Xianxia.Simulation/Systems/ActionEffects.cs](../../src/Xianxia.Simulation/Systems/ActionEffects.cs): 행동 효과: 수련, 탐색(자원·기연·위험), 비무 상대 찾기, 종문 임무, 지역 이동, 요양.
- [src/Xianxia.Simulation/Systems/ActionSystem.cs](../../src/Xianxia.Simulation/Systems/ActionSystem.cs): 수선자의 한 해: 자연 회복 → 행동 선택(성격·상황 가중치) → 행동 실행.
- [src/Xianxia.Simulation/Systems/AuctionSystem.cs](../../src/Xianxia.Simulation/Systems/AuctionSystem.cs): 중원 평원 경매: 300년마다 고급 공법서·법보·단약·부적이 나온다. 부유한 수선자가 입찰하고 낙찰가는 세계에서 사라진다.
- [src/Xianxia.Simulation/Systems/BattleAftermath.cs](../../src/Xianxia.Simulation/Systems/BattleAftermath.cs): 대결 뒤 처리: 부상·경지 하락·전리품·공헌·숙적 형성·로그. 영석은 패자에게서 승자로 옮겨 갈 뿐 새로 생기지 않는다.
- [src/Xianxia.Simulation/Systems/BeastTideSystem.cs](../../src/Xianxia.Simulation/Systems/BeastTideSystem.cs): 수조: 영수 떼가 지역을 습격한다. 거주자와 종문 증원이 막아 내면 격퇴, 아니면 시간이 지나 물러간다.
- [src/Xianxia.Simulation/Systems/BreakthroughSystem.cs](../../src/Xianxia.Simulation/Systems/BreakthroughSystem.cs): 돌파 규칙: 소경지(단계) 돌파, 대경지 돌파, 화신경 이상의 천겁, 반선경 대원만의 비승.
- [src/Xianxia.Simulation/Systems/CombatSystem.cs](../../src/Xianxia.Simulation/Systems/CombatSystem.cs): 전투력 계산과 1:1 대결. 원본은 재능 항이 경지 항보다 3배 컸다(평균 45% 대 14%). 여기서는 경지가 기준값을 정하고
- [src/Xianxia.Simulation/Systems/Commerce.cs](../../src/Xianxia.Simulation/Systems/Commerce.cs): 거래와 기예. 기예가가 만든 단약·부적은 지역 시장에 쌓이고 필요한 사람이 사 간다.
- [src/Xianxia.Simulation/Systems/Crafting.cs](../../src/Xianxia.Simulation/Systems/Crafting.cs): 기예별 제작: 연단(단약), 연기(법보), 부록(부적), 진법(진 유지). 숙련이 높을수록 성공률과 품질이 오른다.
- [src/Xianxia.Simulation/Systems/CultivatorFactory.cs](../../src/Xianxia.Simulation/Systems/CultivatorFactory.cs): 새 영혼 만들기와 한 생(재능·영근·성격·기예·소지품)을 굴리는 규칙.
- [src/Xianxia.Simulation/Systems/EraSystem.cs](../../src/Xianxia.Simulation/Systems/EraSystem.cs): 천지 대세(시대). 3천~8천 년마다 바뀌며 수련 속도와 비경·수조 빈도를 조절한다.
- [src/Xianxia.Simulation/Systems/Growth.cs](../../src/Xianxia.Simulation/Systems/Growth.cs): 수련 속도(영기·깨달음) 계산. 원본의 재능·공법·지역·직위·기명제자 배율을 계승하고, 무시되던 이념 배율을 적용한다.
- [src/Xianxia.Simulation/Systems/InterventionSystem.cs](../../src/Xianxia.Simulation/Systems/InterventionSystem.cs): 천기 개입: 관찰자가 천기(200년마다 1점, 최대 10점)를 써서 세계에 작은 변수를 던진다.
- [src/Xianxia.Simulation/Systems/LifeCycleSystem.cs](../../src/Xianxia.Simulation/Systems/LifeCycleSystem.cs): 생애 주기: 지역 이동, 노화·좌화, 윤회, 비승(새 영혼 탄생), 전생 각성.
- [src/Xianxia.Simulation/Systems/Lifespans.cs](../../src/Xianxia.Simulation/Systems/Lifespans.cs): 수명 계산. 경지 기본 수명에 체질과 연수단 보정을 더한다.
- [src/Xianxia.Simulation/Systems/Narrator.cs](../../src/Xianxia.Simulation/Systems/Narrator.cs): 사건을 로그·연대기·생애 기록·관심 인물 알림으로 남기는 공용 도우미와 한국어 숫자 표기.
- [src/Xianxia.Simulation/Systems/RecordSystem.cs](../../src/Xianxia.Simulation/Systems/RecordSystem.cs): 생애 점수, 영혼 업적(원본 HD), 세계 기록. 원본은 최고 단계 칸에 경지 값을 넣어 "반선경 undefined"가 표시됐다.
- [src/Xianxia.Simulation/Systems/Recruitment.cs](../../src/Xianxia.Simulation/Systems/Recruitment.cs): 입문 시험(떠돌이 영입)과 배신·귀순. 원본에는 떠돌이가 사실상 없어 종문 가입 규칙이 작동하지 않았다.
- [src/Xianxia.Simulation/Systems/RelationshipSystem.cs](../../src/Xianxia.Simulation/Systems/RelationshipSystem.cs): 인연: 기명 제자(사제), 도려(도반), 숙적. 원본의 기명 제자 규칙을 계승하고, 쓰이지 않던 숙적(ei)·도려(dw) 칸을 실제 규칙으로 만들었다.
- [src/Xianxia.Simulation/Systems/SecretRealmSystem.cs](../../src/Xianxia.Simulation/Systems/SecretRealmSystem.cs): 비경: 드물게 열리는 보물 공간. 경지 범위가 맞는 수선자가 들어가 보물을 다투고, 닫힐 때 최대 수혜자를 기록한다.
- [src/Xianxia.Simulation/Systems/SectEconomy.cs](../../src/Xianxia.Simulation/Systems/SectEconomy.cs): 종문 재정: 영지 산출, 녹봉, 시설 건설, 연단방 단약 생산, 장경각·연단방 공헌 교환.
- [src/Xianxia.Simulation/Systems/SectSystem.cs](../../src/Xianxia.Simulation/Systems/SectSystem.cs): 종문 구성원과 직위. 원본 버그(종주 표시 미갱신, 재편 뒤 명부 밖 직위 유지, 환생 직후 고위직 배정)를 고친 규칙.
- [src/Xianxia.Simulation/Systems/Setbacks.cs](../../src/Xianxia.Simulation/Systems/Setbacks.cs): 부상·경지 하락 처리. 전투 패배·돌파 실패·천겁 실패가 같은 규칙을 쓴다.
- [src/Xianxia.Simulation/Systems/StatisticsSystem.cs](../../src/Xianxia.Simulation/Systems/StatisticsSystem.cs): 통계 표본 수집과 100년 주기 정리(기록 갱신·숙적 정리).
- [src/Xianxia.Simulation/Systems/TitleSystem.cs](../../src/Xianxia.Simulation/Systems/TitleSystem.cs): 호칭 수여 규칙. 업적이 생긴 그 순간에 조건을 확인한다(매 틱 전체 검사를 하지 않는다).
- [src/Xianxia.Simulation/Systems/TournamentSystem.cs](../../src/Xianxia.Simulation/Systems/TournamentSystem.cs): 천하 대비무 대회: 휴전 중 천 년마다(종전 500년 뒤부터) 종문 고수와 떠돌이 고수가 토너먼트로 겨룬다.
- [src/Xianxia.Simulation/Systems/TribulationSystem.cs](../../src/Xianxia.Simulation/Systems/TribulationSystem.cs): 천겁(뇌겁)과 비승. 화신경 이상으로 올라설 때 하늘의 벼락을 견뎌야 하고, 반선경 대원만은 비승 천겁에 도전한다.
- [src/Xianxia.Simulation/Systems/WarSettlement.cs](../../src/Xianxia.Simulation/Systems/WarSettlement.cs): 종전 처리: 대표 결투, 승패 판정, 배상, 전쟁 영웅, 전쟁 기록.
- [src/Xianxia.Simulation/Systems/WarSystem.cs](../../src/Xianxia.Simulation/Systems/WarSystem.cs): 정마대전: 3000년부터 11000년 주기로 천 년씩 벌어진다. 파병·접경지 전투·점령·종전(배상·대표 결투·전쟁 영웅).
- [src/Xianxia.Simulation/Systems/WorldFactory.cs](../../src/Xianxia.Simulation/Systems/WorldFactory.cs): 새 세계 만들기: 두 종문·13개 지역·첫 세대 수선자·개창 원로·시대.
- [src/Xianxia.Simulation/WorldSimulator.cs](../../src/Xianxia.Simulation/WorldSimulator.cs): 한 해(1틱) 진행 순서. 순서를 바꾸면 같은 시드의 결과가 달라지므로 시스템을 끝에 추가한다.
- [src/Xianxia.Simulation/Xianxia.Simulation.csproj](../../src/Xianxia.Simulation/Xianxia.Simulation.csproj): 설정·정의 파일

### src/Xianxia.App

- [src/Xianxia.App/App.axaml](../../src/Xianxia.App/App.axaml): 설정·정의 파일
- [src/Xianxia.App/App.axaml.cs](../../src/Xianxia.App/App.axaml.cs): 선로만상록 프로젝트 Avalonia 애플리케이션 진입점. 게임 세션을 하나 만들고 플랫폼별 수명 주기에 맞춰 화면과 저장을 연결한다.
- [src/Xianxia.App/AppBuilderExtensions.cs](../../src/Xianxia.App/AppBuilderExtensions.cs): 앱 공통 AppBuilder 설정(내장 한글 글꼴 등록). 데스크톱·Android·테스트가 같은 설정을 쓴다.
- [src/Xianxia.App/Controls/BadgeView.cs](../../src/Xianxia.App/Controls/BadgeView.cs): 둥근 배지(원본 Bdg). 같은 배지를 재사용하며 글자와 색만 바꾼다.
- [src/Xianxia.App/Controls/Charts.cs](../../src/Xianxia.App/Controls/Charts.cs): 통계 그래프: 여러 계열 꺾은선(종문 세력 추이)과 누적 띠(경지 구성 추이). 원본에는 시간 추이 그래프가 없었다.
- [src/Xianxia.App/Controls/ChipBar.cs](../../src/Xianxia.App/Controls/ChipBar.cs): 칩 선택 막대(원본 정렬·필터·하위 탭 단추 줄). 하나만 선택되며, 분류별 색이 있으면 선택된 칩을 그 색으로 칠한다.
- [src/Xianxia.App/Controls/InfoRow.cs](../../src/Xianxia.App/Controls/InfoRow.cs): 목록 한 줄(원본 AgCard·AchCard·로그 카드). 순위 원·제목·부제·배지·오른쪽 수치를 한 모양으로 통일했다.
- [src/Xianxia.App/Controls/KeyValueList.cs](../../src/Xianxia.App/Controls/KeyValueList.cs): 키-값 줄 목록(원본 KV). 줄을 위치로 갱신하고 남는 줄은 숨긴다.
- [src/Xianxia.App/Controls/Meters.cs](../../src/Xianxia.App/Controls/Meters.cs): 직접 그리는 막대 두 종류: 진행 막대(원본 B)와 접경지 세력 줄다리기 막대(원본 세력도).
- [src/Xianxia.App/Controls/RegionMap.cs](../../src/Xianxia.App/Controls/RegionMap.cs): 13개 지역 약도. 서쪽은 청운종, 동쪽은 천마문, 가운데는 접경지다(원본은 지역을 글자 목록으로만 보여 줬다).
- [src/Xianxia.App/Controls/RowList.cs](../../src/Xianxia.App/Controls/RowList.cs): 재사용 줄 목록과 쪽 넘김(원본 PgCtrl: "< 1/10 >"). 목록 길이가 바뀌어도 줄 객체를 다시 만들지 않는다.
- [src/Xianxia.App/Controls/Ui.cs](../../src/Xianxia.App/Controls/Ui.cs): 코드로 화면을 짜는 작은 도우미. 원본의 패널(.xp)·카드(.xc)·제목·키-값(KV) 모양을 같은 크기와 색으로 만든다.
- [src/Xianxia.App/Pages/HistoryPage.cs](../../src/Xianxia.App/Pages/HistoryPage.cs): 사기 탭: 연대기·세계 기록·전쟁사·대비무·비승록·경매 기록을 20건씩 읽는다.
- [src/Xianxia.App/Pages/LegacyPage.cs](../../src/Xianxia.App/Pages/LegacyPage.cs): 업적 탭: 역대 최고·상위 5생 합·종합 순위 및 호칭 조건 도감.
- [src/Xianxia.App/Pages/LogPage.cs](../../src/Xianxia.App/Pages/LogPage.cs): 기록 탭: 사건 종류와 관심 인물 필터. 최신 기록부터 40건씩 보인다.
- [src/Xianxia.App/Pages/PeoplePage.cs](../../src/Xianxia.App/Pages/PeoplePage.cs): 인물 탭: 8종 정렬, 소속·관심 필터, 이름 검색과 20명 단위 쪽 넘김을 제공한다.
- [src/Xianxia.App/Pages/SectsPage.cs](../../src/Xianxia.App/Pages/SectsPage.cs): 종문 탭: 두 종문 상세 현황과 산수·사망 원인 요약을 표시한다.
- [src/Xianxia.App/Pages/StatsPage.cs](../../src/Xianxia.App/Pages/StatsPage.cs): 현황 탭: 경지 분포와 통계 추이, 영석 보존 장부, 누적 사건 건수를 표시한다.
- [src/Xianxia.App/Pages/WorldPage.cs](../../src/Xianxia.App/Pages/WorldPage.cs): 천하 탭(원본 "전쟁" 탭 확장): 전쟁·휴전 현황, 두 종문 대결 카드, 지역 약도, 접경지 세력도, 진행 중인 사건,
- [src/Xianxia.App/Pages/WorldWidgets.cs](../../src/Xianxia.App/Pages/WorldWidgets.cs): 천하 탭의 부품: 종문 대결 카드(원본 VS 카드)와 접경지 세력도 줄(원본 세력 막대).
- [src/Xianxia.App/Services/Fmt.cs](../../src/Xianxia.App/Services/Fmt.cs): 화면 표기 규칙: 연도·수·배율·비율. 시뮬레이션 문장과 같은 한국어 숫자 표기(KoreanNumber)를 쓴다.
- [src/Xianxia.App/Services/GameSession.cs](../../src/Xianxia.App/Services/GameSession.cs): 게임 세션: 앱 하나에 하나. 세계·실행기·저장소를 묶고 수명 주기(백그라운드·종료)에 맞춰 저장한다.
- [src/Xianxia.App/Services/LegacyQuery.cs](../../src/Xianxia.App/Services/LegacyQuery.cs): 영혼 업적 순위. 최종 순위는 원본처럼 최고 점수 순위와 5생 합 순위의 합으로 정한다.
- [src/Xianxia.App/Services/PeopleQuery.cs](../../src/Xianxia.App/Services/PeopleQuery.cs): 인물 목록의 검색·소속 필터·8종 정렬. 화면 갱신에서만 실행하며 세계와 난수 상태를 바꾸지 않는다.
- [src/Xianxia.App/Services/Queries.cs](../../src/Xianxia.App/Services/Queries.cs): 화면용 집계: 종문 요약, 지역 거주자, 강자 순위. 세계를 바꾸지 않는 읽기 전용 계산만 둔다.
- [src/Xianxia.App/Services/SaveStore.cs](../../src/Xianxia.App/Services/SaveStore.cs): 저장 파일 관리. 앱 전용 폴더에 world.sav(gzip JSON) 하나를 둔다. 인터넷 권한 없이 기기 안에만 저장한다.
- [src/Xianxia.App/Services/SimulationRunner.cs](../../src/Xianxia.App/Services/SimulationRunner.cs): 시뮬레이션 실행기: 별도 스레드에서 정해진 속도(년/초)로 세계를 진행한다.
- [src/Xianxia.App/Sheets/CultivatorDetails.cs](../../src/Xianxia.App/Sheets/CultivatorDetails.cs): 수선자 상세의 수치 표시. 전투력과 돌파 확률은 코어의 계산 결과를 표시하며 별도 규칙을 만들지 않는다.
- [src/Xianxia.App/Sheets/CultivatorSheet.cs](../../src/Xianxia.App/Sheets/CultivatorSheet.cs): 수선자 상세: 7개 하위 탭, 관심 표시, 기연 하사. 영혼 번호로 매번 조회해 윤회 뒤에도 같은 대상을 따른다.
- [src/Xianxia.App/Sheets/HelpSheet.cs](../../src/Xianxia.App/Sheets/HelpSheet.cs): 게임 도감: 실제 경지·시설·호칭 규칙과 관찰·저장 조작을 설명한다.
- [src/Xianxia.App/Sheets/RegionSheet.cs](../../src/Xianxia.App/Sheets/RegionSheet.cs): 지역 상세: 환경·현재 비경·수조·시장 재고·거주자, 천기 개입을 표시한다.
- [src/Xianxia.App/Sheets/SectSheet.cs](../../src/Xianxia.App/Sheets/SectSheet.cs): 종문 상세: 정보·강자·공헌·시설·역사를 기존 종문 상태와 연결한다.
- [src/Xianxia.App/Sheets/SettingsSheet.cs](../../src/Xianxia.App/Sheets/SettingsSheet.cs): 설정: 명시적 저장·불러오기와 시드·인구를 입력한 새 세계. 파일 작업과 세계 생성은 UI 밖에서 수행한다.
- [src/Xianxia.App/Sheets/TournamentSheet.cs](../../src/Xianxia.App/Sheets/TournamentSheet.cs): 천하 대비무 상세: 보존된 모든 경기, 라운드, 승패와 이변 여부를 20경기씩 표시한다.
- [src/Xianxia.App/Sheets/WarSheet.cs](../../src/Xianxia.App/Sheets/WarSheet.cs): 정마대전 상세: 종료된 전쟁 기록과 진행 중인 전쟁을 구분해 전적·점령·배상·영웅·주요 장면을 표시한다.
- [src/Xianxia.App/Theme/Controls.axaml](../../src/Xianxia.App/Theme/Controls.axaml): 설정·정의 파일
- [src/Xianxia.App/Theme/Palette.cs](../../src/Xianxia.App/Theme/Palette.cs): 화면 색상표. 원본의 양피지 색조(배경 #F5EDE0, 먹색 글자, 갈색 강조)를 계승하고
- [src/Xianxia.App/Views/HeaderBar.cs](../../src/Xianxia.App/Views/HeaderBar.cs): 상단 막대(원본 헤더): 제목·연도, 시대·전쟁 상태·천기, 진행/정지·속도·설정·도감 단추.
- [src/Xianxia.App/Views/MainView.cs](../../src/Xianxia.App/Views/MainView.cs): 메인 화면: 상단 막대 + 탭 화면 + 하단 탭 막대, 그 위에 알림 층과 상세 시트 층을 겹친다.
- [src/Xianxia.App/Views/NavBar.cs](../../src/Xianxia.App/Views/NavBar.cs): 하단 탭 막대(원본 .xnav). 탭마다 한자 표식과 이름을 둔다.
- [src/Xianxia.App/Views/Navigation.cs](../../src/Xianxia.App/Views/Navigation.cs): 화면 사이 이동: 목록의 항목을 누르면 상세 시트(수선자·종문·지역·대회·전쟁)를 연다.
- [src/Xianxia.App/Views/PageBase.cs](../../src/Xianxia.App/Views/PageBase.cs): 하단 탭 화면과 상세 시트의 공통 틀. 화면은 처음에 한 번 만들고, 갱신 때는 세계를 읽어 글자만 바꾼다.
- [src/Xianxia.App/Views/Rows.cs](../../src/Xianxia.App/Views/Rows.cs): 여러 화면이 같이 쓰는 목록 줄 모양: 수선자, 사건 기록, 연대기. 같은 대상은 어디서나 같은 모양으로 보인다.
- [src/Xianxia.App/Views/SectDetails.cs](../../src/Xianxia.App/Views/SectDetails.cs): 종문 탭과 상세 시트가 함께 쓰는 종문 현황. 시설·정원 값은 시뮬레이션 규칙표를 그대로 읽는다.
- [src/Xianxia.App/Views/SheetHost.cs](../../src/Xianxia.App/Views/SheetHost.cs): 상세 시트 층(원본 Modal·SectModal). 시트를 쌓아 두고 뒤로 가기(단추·Android 뒤로 키)로 하나씩 닫는다.
- [src/Xianxia.App/Views/ToastHost.cs](../../src/Xianxia.App/Views/ToastHost.cs): 알림(원본에 없던 기능): 연대기급 사건과 관심 인물의 주요 사건을 화면 위쪽에 잠깐 띄운다.
- [src/Xianxia.App/Xianxia.App.csproj](../../src/Xianxia.App/Xianxia.App.csproj): 설정·정의 파일

### src/Xianxia.Android

- [src/Xianxia.Android/MainActivity.cs](../../src/Xianxia.Android/MainActivity.cs): Android 첫 화면(Activity). 세로 화면 고정, 화면 회전·크기 변경 시 Activity를 다시 만들지 않는다.
- [src/Xianxia.Android/MainApplication.cs](../../src/Xianxia.Android/MainApplication.cs): Android 프로세스 진입점. Avalonia 앱을 만들고 내장 한글 글꼴을 등록한다.
- [src/Xianxia.Android/Properties/AndroidManifest.xml](../../src/Xianxia.Android/Properties/AndroidManifest.xml): 설정·정의 파일
- [src/Xianxia.Android/Resources/mipmap-anydpi-v26/ic_launcher.xml](../../src/Xianxia.Android/Resources/mipmap-anydpi-v26/ic_launcher.xml): 설정·정의 파일
- [src/Xianxia.Android/Resources/mipmap-anydpi-v26/ic_launcher_round.xml](../../src/Xianxia.Android/Resources/mipmap-anydpi-v26/ic_launcher_round.xml): 설정·정의 파일
- [src/Xianxia.Android/Resources/values/colors.xml](../../src/Xianxia.Android/Resources/values/colors.xml): 설정·정의 파일
- [src/Xianxia.Android/Resources/values/styles.xml](../../src/Xianxia.Android/Resources/values/styles.xml): 설정·정의 파일
- [src/Xianxia.Android/Resources/values-v27/styles.xml](../../src/Xianxia.Android/Resources/values-v27/styles.xml): 설정·정의 파일
- [src/Xianxia.Android/Xianxia.Android.csproj](../../src/Xianxia.Android/Xianxia.Android.csproj): 설정·정의 파일

### src/Xianxia.Desktop

- [src/Xianxia.Desktop/Program.cs](../../src/Xianxia.Desktop/Program.cs): 데스크톱 개발 실행 진입점.
- [src/Xianxia.Desktop/Xianxia.Desktop.csproj](../../src/Xianxia.Desktop/Xianxia.Desktop.csproj): 설정·정의 파일

### tools

- [tools/Xianxia.Tools/Program.cs](../../tools/Xianxia.Tools/Program.cs): 헤드리스 도구: 세계를 화면 없이 돌려 경지 분포·전쟁·대회·장부·성능을 보고한다. 밸런스 조정 근거를 만든다.
- [tools/Xianxia.Tools/Xianxia.Tools.csproj](../../tools/Xianxia.Tools/Xianxia.Tools.csproj): 설정·정의 파일
- [tools/analyze-legacy.mjs](../../tools/analyze-legacy.mjs): 보존 React 원본을 측정용 시드 난수와 함께 실행해 비교 보고서를 만든다.
- [tools/emulator-smoke.sh](../../tools/emulator-smoke.sh): /usr/bin/env bash
- [tools/make-app-icons.py](../../tools/make-app-icons.py): /usr/bin/env python3
- [tools/make-font-subset.py](../../tools/make-font-subset.py): /usr/bin/env python3
- [tools/publish-release.py](../../tools/publish-release.py): /usr/bin/env python3
- [tools/retained-sign.sh](../../tools/retained-sign.sh): /usr/bin/env bash

## 테스트 메서드 색인

정상 시드·설정의 기존 검증 결과는 시뮬레이션38 + UI/조회11 =49사례다. 이 색인의 메서드 수와 NUnit의 fixture 조합별 사례 수는 다를 수 있다.

### [CompletedUiTests](../../tests/Xianxia.App.Tests/CompletedUiTests.cs)

- `EveryPageAndSheet_AfterWorldEvents_RendersWithoutPlaceholders`
- `People_SearchAndFollow_UpdatesListAndPersistsAcrossReload`
- `InterventionButtons_InsufficientFavorOrDuplicateEvent_DoNotSpendAgain`
- `Settings_CreateWorld_RequiresValidInputAndExplicitConfirmation`
- `Settings_SaveAndLoad_RestoresSavedYearOnlyAfterConfirmation`
- `MissingTargetsAndEmptyHistory_RenderGracefulEmptyStates`

### [QueryTests](../../tests/Xianxia.App.Tests/QueryTests.cs)

- `PeopleQuery_WithFiltersAndEverySort_PreservesWorldAndReturnsMatchingOrder`
- `LegacyQuery_CombinedRanks_UsesRankSumThenBestScore`

### [SmokeRenderTests](../../tests/Xianxia.App.Tests/SmokeRenderTests.cs)

- `MainView_AfterSixHundredYears_RendersWorldTab`
- `BackRequest_WithOpenSheet_ClosesSheetBeforeLeavingTab`
- `Session_SaveThenLoad_RestoresYear`

### [DeterminismTests](../../tests/Xianxia.Simulation.Tests/DeterminismTests.cs)

- `SameSeed_ProducesIdenticalWorld`
- `DifferentSeeds_ProduceDifferentWorlds`
- `SaveAndLoad_ContinuesExactlyTheSameHistory`
- `Load_RejectsCorruptData`
- `Load_RestoresIndexes`

### [FeatureTests](../../tests/Xianxia.Simulation.Tests/FeatureTests.cs)

- `Ascend_ReplacesSoulAndKeepsPopulation`
- `Death_PassesEstateToDisciple`
- `Tournament_ProducesBracketAndChampion`
- `Intervention_SpendsFavorAndRefusesWhenEmpty`
- `SecretRealm_OpensAndClosesWithRecord`
- `BeastTide_EndsAndRestoresRegion`
- `Rivalry_IsMutualAndCapped`
- `Title_IsAwardedOnce`
- `WarCycle_FollowsThousandYearWarAndTenThousandYearPeace`

### [InvariantTests](../../tests/Xianxia.Simulation.Tests/InvariantTests.cs)

- `RunWorld`
- `StoneLedger_MatchesActualHoldings`
- `RankCounts_MatchMembersAndCapacity`
- `Leader_IsMemberWithLeaderRank`
- `RogueCultivators_HaveNoRank`
- `RegionResidents_ContainEachCultivatorExactlyOnce`
- `Relationships_AreSymmetric`
- `CultivatorValues_StayInRange`
- `SoulRecords_KeepStageInRange`
- `WorldEvents_HaveHappened`
- `Population_StaysConstant`

### [RandomTests](../../tests/Xianxia.Simulation.Tests/RandomTests.cs)

- `SameSeed_ProducesSameSequence`
- `KnownSeed_MatchesReferenceOutput`
- `FromState_ContinuesSequence`
- `NextInt_StaysInRange`
- `Range_IncludesBothEnds`
- `NextInt_WithZeroBound_Throws`
- `PickWeighted_NeverPicksZeroWeight`

### [RegressionTests](../../tests/Xianxia.Simulation.Tests/RegressionTests.cs)

- `BorderBattle_AwardsPointsToActualWinner`
- `Join_NeverSeatsNewcomerInVacantHighRank`
- `Rerank_ClearsRanksOutsideCapacityAndUpdatesLeader`
- `EventLog_KeepsNewestEntriesInOrder`
- `Ideology_ChangesGrowthByRealm`
- `Power_IsDominatedByRealm`
- `RealmCollapse_IsRecordedSeparately`
