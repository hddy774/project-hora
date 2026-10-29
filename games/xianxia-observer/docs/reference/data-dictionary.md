# 데이터 사전

문서 기준: 17b48ad. 실제 어셈블리와 XML 주석에서 추출한 공개 모델 28종이다.

JsonIgnore 속성은 저장하지 않는다. 읽기 전용 속성은 JSON에 계산 결과가 나갈 수 있으나 setter가 없어 불러오기 상태의 원본으로 삼지 않는다. 사전·거주자 객체 참조 등 private 상태는 별도 복원한다. 아래 설명은 코드 주석 원문이며 [저장 설계](../06-data-and-persistence.md)와 함께 검토한다.

## AuctionRecord

경매 기록.

| 속성 | 형식 | JSON/쓰기 | 원문 설명 |
|---|---|---|---|
| `Year` | `Int32` | 자동 대상 · 쓰기 가능 | 연도. |
| `Lots` | `IReadOnlyList<String>` | 자동 대상 · 쓰기 가능 | 낙찰 요약. |
| `Total` | `Int64` | 자동 대상 · 쓰기 가능 | 총 낙찰가. |

## BeastTideState

진행 중인 수조(영수 떼 습격).

| 속성 | 형식 | JSON/쓰기 | 원문 설명 |
|---|---|---|---|
| `BeastName` | `String` | 자동 대상 · 쓰기 가능 | 이끄는 영수 이름. |
| `RegionId` | `Int32` | 자동 대상 · 쓰기 가능 | 습격 지역. |
| `StartYear` | `Int32` | 자동 대상 · 쓰기 가능 | 시작 연도. |
| `EndYear` | `Int32` | 자동 대상 · 쓰기 가능 | 스스로 물러가는 연도. |
| `InitialStrength` | `Double` | 자동 대상 · 쓰기 가능 | 처음 세력. |
| `Strength` | `Double` | 자동 대상 · 쓰기 가능 | 남은 세력. |
| `Realm` | `Int32` | 자동 대상 · 쓰기 가능 | 영수 수장의 경지(전투력 기준). |
| `DefenderIds` | `List<Int32>` | 자동 대상 · 쓰기 가능 | 방어자별 공훈 (영혼 번호, 공훈) — 같은 인덱스. |
| `DefenderMerit` | `List<Double>` | 자동 대상 · 쓰기 가능 | 방어자 공훈. |
| `Summoned` | `Boolean` | 자동 대상 · 쓰기 가능 | 천기 개입으로 불러왔는가. |

## Chronicle

연대기. 드문 사건만 담아 세계의 역사를 읽을 수 있게 한다.

| 속성 | 형식 | JSON/쓰기 | 원문 설명 |
|---|---|---|---|
| `Entries` | `List<ChronicleEntry>` | 자동 대상 · 쓰기 가능 | 항목(오래된 것부터). |
| `NextSequence` | `Int64` | 자동 대상 · 쓰기 가능 | 다음 일련번호. |
| `NextNoticeSequence` | `Int64` | 제외(JsonIgnore) | 다음 알림 번호. |
| `Notices` | `IReadOnlyList<Notice>` | 제외(JsonIgnore) | 최근 알림(최대 40). |

## ChronicleEntry

연대기 항목.

| 속성 | 형식 | JSON/쓰기 | 원문 설명 |
|---|---|---|---|
| `Sequence` | `Int64` | 자동 대상 · 쓰기 가능 | 일련번호. |
| `Year` | `Int32` | 자동 대상 · 쓰기 가능 | 연도. |
| `Kind` | `ChronicleKind` | 자동 대상 · 쓰기 가능 | 분류. |
| `Text` | `String` | 자동 대상 · 쓰기 가능 | 내용. |
| `SubjectId` | `Int32` | 자동 대상 · 쓰기 가능 | 주인공 영혼 번호(0이면 없음). |

## Cultivator

수선자 한 명. 저장 파일에 그대로 직렬화되므로 공개 속성은 모두 상태다. 다른 수선자와의 관계는 객체 참조가 아니라 영혼 번호로 가리킨다.

| 속성 | 형식 | JSON/쓰기 | 원문 설명 |
|---|---|---|---|
| `Id` | `Int32` | 자동 대상 · 쓰기 가능 | 영혼 번호. 윤회해도 같고, 비승하면 새 영혼이 새 번호로 자리를 잇는다. |
| `Name` | `String` | 자동 대상 · 쓰기 가능 | 영혼 이름(윤회해도 유지). |
| `DaoName` | `String` | 자동 대상 · 쓰기 가능 | 도호. 원영경 이상에서 얻는다. |
| `Reincarnations` | `Int32` | 자동 대상 · 쓰기 가능 | 윤회 횟수(첫 생은 0). |
| `BornYear` | `Int32` | 자동 대상 · 쓰기 가능 | 현생이 시작된 해. |
| `Age` | `Int32` | 자동 대상 · 쓰기 가능 | 나이. |
| `Lifespan` | `Int32` | 자동 대상 · 쓰기 가능 | 수명(이 나이에 이르면 좌화한다). |
| `Realm` | `Int32` | 자동 대상 · 쓰기 가능 | 경지(0 연기경 ~ 8 반선경). |
| `Stage` | `Int32` | 자동 대상 · 쓰기 가능 | 단계(0 초기 ~ 3 대원만). |
| `Qi` | `Double` | 자동 대상 · 쓰기 가능 | 현재 단계에 쌓인 영기. |
| `Insight` | `Double` | 자동 대상 · 쓰기 가능 | 깨달음(0~1000). 대경지 돌파 확률을 높이고 돌파 뒤 소모된다. |
| `DaoHeart` | `Double` | 자동 대상 · 쓰기 가능 | 도심(0~100). 낮으면 수련이 더디고 주화입마·배신 위험이 커진다. |
| `Injury` | `Double` | 자동 대상 · 쓰기 가능 | 누적 부상. 최대 체력에 이르면 중상으로 단계가 떨어진다. |
| `InnerPower` | `Double` | 자동 대상 · 쓰기 가능 | 내공. 천겁과 전투력에 쓰인다(원본 cb). |
| `RecoveryYears` | `Int32` | 자동 대상 · 쓰기 가능 | 강제 요양 남은 해(중상 후). |
| `Comprehension` | `Int32` | 자동 대상 · 쓰기 가능 | 오성: 깨달음·돌파. |
| `Constitution` | `Int32` | 자동 대상 · 쓰기 가능 | 체질: 체력·수명·천겁 저항. |
| `Bone` | `Int32` | 자동 대상 · 쓰기 가능 | 근골: 전투력. |
| `Root` | `Int32` | 자동 대상 · 쓰기 가능 | 영근: 영기 흡수. |
| `Element` | `Element` | 자동 대상 · 쓰기 가능 | 영근 속성. |
| `Personality` | `Personality` | 자동 대상 · 쓰기 가능 | 성격. |
| `Ambition` | `Double` | 자동 대상 · 쓰기 가능 | 야망(0~1). 비경 진입·결투에 영향. |
| `Caution` | `Double` | 자동 대상 · 쓰기 가능 | 신중함(0~1). 위험 회피. |
| `Charisma` | `Double` | 자동 대상 · 쓰기 가능 | 매력(0~1). 인연·거래·기연. |
| `SectId` | `Int32` | 자동 대상 · 쓰기 가능 | 소속 종문 번호(0이면 떠돌이 산수). |
| `Rank` | `SectRank` | 자동 대상 · 쓰기 가능 | 종문 직위. |
| `Merit` | `Double` | 자동 대상 · 쓰기 가능 | 현생 누적 공헌(직위 재편 기준). |
| `ContributionPoints` | `Double` | 자동 대상 · 쓰기 가능 | 사용 가능한 공헌점(장경각·연단방). |
| `RegionId` | `Int32` | 자동 대상 · 쓰기 가능 | 현재 머무는 지역 번호. |
| `OnWarDuty` | `Boolean` | 자동 대상 · 쓰기 가능 | 전쟁 중 접경지 파견 여부. |
| `TechniqueGrade` | `Int32` | 자동 대상 · 쓰기 가능 | 공법 등급(0~19). |
| `TechniqueElement` | `Element` | 자동 대상 · 쓰기 가능 | 공법 속성. 영근과 같으면 수련 +20%. |
| `Art` | `ArtKind` | 자동 대상 · 쓰기 가능 | 기예. |
| `ArtSkill` | `Double` | 자동 대상 · 쓰기 가능 | 기예 숙련(0~100). |
| `ArtifactGrade` | `Int32` | 자동 대상 · 쓰기 가능 | 법보 등급(0 없음 ~ 5 천품). |
| `SpiritStones` | `Int64` | 자동 대상 · 쓰기 가능 | 영석. |
| `Herbs` | `Int32` | 자동 대상 · 쓰기 가능 | 영초. |
| `Ore` | `Int32` | 자동 대상 · 쓰기 가능 | 영철. |
| `BeastCores` | `Int32` | 자동 대상 · 쓰기 가능 | 요단(영수 내단). |
| `Pills` | `List<Int32>` | 자동 대상 · 쓰기 가능 | 단약 보유량. 인덱스는 . |
| `Talismans` | `List<Int32>` | 자동 대상 · 쓰기 가능 | 부적 보유량. 인덱스는 . |
| `LongevityPillsTaken` | `Int32` | 자동 대상 · 쓰기 가능 | 현생에 복용한 연수단 수(최대 3). |
| `LastAction` | `CultivatorAction` | 자동 대상 · 쓰기 가능 | 마지막 행동. |
| `MemoryAwakened` | `Boolean` | 자동 대상 · 쓰기 가능 | 현생에서 전생 기억을 되찾았는가. |
| `LastTribulationYear` | `Int32` | 자동 대상 · 쓰기 가능 | 마지막 천겁을 겪은 해. |
| `QiRequirement` | `Int64` | 제외(JsonIgnore) | 현재 단계 돌파에 필요한 영기. |
| `TalentTotal` | `Int32` | 제외(JsonIgnore) | 재능 합계(오성+체질+근골+영근). |
| `TalentGrade` | `Int32` | 제외(JsonIgnore) | 재능 등급(1 범재 ~ 5 절세). |
| `IsRogue` | `Boolean` | 제외(JsonIgnore) | 떠돌이(산수) 여부. |
| `MaxHealth` | `Double` | 제외(JsonIgnore) | 최대 체력. |
| `HealthRatio` | `Double` | 제외(JsonIgnore) | 남은 체력 비율(0~1). |
| `RealmText` | `String` | 제외(JsonIgnore) | 표시용 경지 이름(예: 금단경 후기). |
| `DisplayName` | `String` | 제외(JsonIgnore) | 도호가 있으면 "이름(도호)", 없으면 이름. |
| `LifeNumber` | `Int32` | 제외(JsonIgnore) | 몇 번째 생인가(1부터). |
| `MasterId` | `Int32` | 자동 대상 · 쓰기 가능 | 스승의 영혼 번호(0이면 없음). |
| `DiscipleIds` | `List<Int32>` | 자동 대상 · 쓰기 가능 | 기명 제자 영혼 번호(최대 3). |
| `RivalIds` | `List<Int32>` | 자동 대상 · 쓰기 가능 | 숙적 영혼 번호(최대 3). |
| `CompanionId` | `Int32` | 자동 대상 · 쓰기 가능 | 도려(도반)의 영혼 번호(0이면 없음). |
| `CompanionSinceYear` | `Int32` | 자동 대상 · 쓰기 가능 | 도려를 맺은 해. |
| `Battles` | `Int32` | 자동 대상 · 쓰기 가능 | 현생 전투 수. |
| `Wins` | `Int32` | 자동 대상 · 쓰기 가능 | 현생 승리 수. |
| `Losses` | `Int32` | 자동 대상 · 쓰기 가능 | 현생 패배 수. |
| `WinStreak` | `Int32` | 자동 대상 · 쓰기 가능 | 현재 연승. |
| `MajorBreakthroughs` | `Int32` | 자동 대상 · 쓰기 가능 | 현생 대경지 돌파 수. |
| `MinorBreakthroughs` | `Int32` | 자동 대상 · 쓰기 가능 | 현생 소경지 돌파 수. |
| `TribulationsPassed` | `Int32` | 자동 대상 · 쓰기 가능 | 현생 천겁 통과 수. |
| `Fortunes` | `Int32` | 자동 대상 · 쓰기 가능 | 현생 기연 수. |
| `WarMerit` | `Double` | 자동 대상 · 쓰기 가능 | 이번 전쟁 공훈. |
| `LifetimeWarMerit` | `Double` | 자동 대상 · 쓰기 가능 | 현생 누적 전쟁 공훈. |
| `TournamentBest` | `Int32` | 자동 대상 · 쓰기 가능 | 현생 대비무 최고 성적(0 없음, 1 우승, 2 준우승, 4 4강, 8 8강). |
| `HighestRealm` | `Int32` | 자동 대상 · 쓰기 가능 | 현생 최고 경지. |
| `HighestStage` | `Int32` | 자동 대상 · 쓰기 가능 | 현생 최고 단계(최고 경지 기준). |
| `Titles` | `List<TitleId>` | 자동 대상 · 쓰기 가능 | 현생 호칭. |
| `Biography` | `List<LifeEvent>` | 자동 대상 · 쓰기 가능 | 현생 주요 사건(최대 40, 오래된 것부터 지운다). |
| `PastLives` | `List<PastLife>` | 자동 대상 · 쓰기 가능 | 최근 전생 요약(최대 20). |

## EraState

현재 천지 대세(시대).

| 속성 | 형식 | JSON/쓰기 | 원문 설명 |
|---|---|---|---|
| `Kind` | `EraKind` | 자동 대상 · 쓰기 가능 | 시대 종류. |
| `StartYear` | `Int32` | 자동 대상 · 쓰기 가능 | 시작 연도. |
| `EndYear` | `Int32` | 자동 대상 · 쓰기 가능 | 끝나는 연도. |
| `Count` | `Int32` | 자동 대상 · 쓰기 가능 | 지금까지의 시대 수. |

## EventLog

사건 기록. [성능] 고정 크기 배열 링 버퍼라 기록 추가가 O(1)이다. 화면은 로 최신순 사본을 받아 그린다.

| 속성 | 형식 | JSON/쓰기 | 원문 설명 |
|---|---|---|---|
| `NextSequence` | `Int64` | 자동 대상 · 쓰기 가능 | 다음 일련번호. |
| `Counts` | `List<Int64>` | 자동 대상 · 쓰기 가능 | 분류별 누적 건수(잘려 나간 기록 포함). 인덱스는 . |
| `Entries` | `List<LogEntry>` | 자동 대상 · 쓰기 가능 | 저장용: 오래된 것부터 나열한 현재 기록. 설정하면 버퍼를 다시 채운다. |
| `Count` | `Int32` | 자동 대상 · 읽기 전용 | 현재 보관 중인 기록 수. |

## ImmortalRecord

비승한 선인 명부 항목.

| 속성 | 형식 | JSON/쓰기 | 원문 설명 |
|---|---|---|---|
| `Year` | `Int32` | 자동 대상 · 쓰기 가능 | 비승 연도. |
| `SoulId` | `Int32` | 자동 대상 · 쓰기 가능 | 영혼 번호. |
| `Name` | `String` | 자동 대상 · 쓰기 가능 | 이름(도호 포함). |
| `Lives` | `Int32` | 자동 대상 · 쓰기 가능 | 비승까지 걸린 생 수. |
| `Age` | `Int32` | 자동 대상 · 쓰기 가능 | 비승 나이. |
| `Affiliation` | `String` | 자동 대상 · 쓰기 가능 | 소속. |

## LifeEvent

생애의 한 사건.

| 속성 | 형식 | JSON/쓰기 | 원문 설명 |
|---|---|---|---|
| `Year` | `Int32` | 자동 대상 · 쓰기 가능 | 세계 연도. |
| `Age` | `Int32` | 자동 대상 · 쓰기 가능 | 당시 나이. |
| `Text` | `String` | 자동 대상 · 쓰기 가능 | 내용. |

## LogEntry

사건 한 줄.

| 속성 | 형식 | JSON/쓰기 | 원문 설명 |
|---|---|---|---|
| `Sequence` | `Int64` | 자동 대상 · 쓰기 가능 | 전체 일련번호(증가만 한다). |
| `Year` | `Int32` | 자동 대상 · 쓰기 가능 | 연도. |
| `Kind` | `LogKind` | 자동 대상 · 쓰기 가능 | 분류. |
| `Text` | `String` | 자동 대상 · 쓰기 가능 | 내용. |
| `SubjectId` | `Int32` | 자동 대상 · 쓰기 가능 | 주인공 영혼 번호(0이면 없음). |
| `OtherId` | `Int32` | 자동 대상 · 쓰기 가능 | 상대 영혼 번호(0이면 없음). |

## Notice

화면 알림(토스트). 저장하지 않는다.

| 속성 | 형식 | JSON/쓰기 | 원문 설명 |
|---|---|---|---|
| `Sequence` | `Int64` | 자동 대상 · 쓰기 가능 | 일련번호. |
| `Year` | `Int32` | 자동 대상 · 쓰기 가능 | 연도. |
| `Text` | `String` | 자동 대상 · 쓰기 가능 | 내용. |
| `SubjectId` | `Int32` | 자동 대상 · 쓰기 가능 | 관련 영혼 번호. |
| `Major` | `Boolean` | 자동 대상 · 쓰기 가능 | 연대기급 사건이면 true, 관심 인물 사건이면 false. |

## PastLife

끝난 한 생의 요약.

| 속성 | 형식 | JSON/쓰기 | 원문 설명 |
|---|---|---|---|
| `LifeNumber` | `Int32` | 자동 대상 · 쓰기 가능 | 몇 번째 생. |
| `BornYear` | `Int32` | 자동 대상 · 쓰기 가능 | 태어난 해. |
| `DeathYear` | `Int32` | 자동 대상 · 쓰기 가능 | 끝난 해. |
| `Age` | `Int32` | 자동 대상 · 쓰기 가능 | 끝난 나이. |
| `Realm` | `Int32` | 자동 대상 · 쓰기 가능 | 최고 경지. |
| `Stage` | `Int32` | 자동 대상 · 쓰기 가능 | 최고 단계. |
| `SectId` | `Int32` | 자동 대상 · 쓰기 가능 | 마지막 소속 종문. |
| `Ending` | `String` | 자동 대상 · 쓰기 가능 | 끝(사망 원인 또는 비승). |
| `Score` | `Int32` | 자동 대상 · 쓰기 가능 | 생애 업적 점수. |
| `Titles` | `IReadOnlyList<TitleId>` | 자동 대상 · 쓰기 가능 | 얻은 호칭. |

## RecordEntry

기록 하나.

| 속성 | 형식 | JSON/쓰기 | 원문 설명 |
|---|---|---|---|
| `Year` | `Int32` | 자동 대상 · 쓰기 가능 | 달성 연도. |
| `SoulId` | `Int32` | 자동 대상 · 쓰기 가능 | 영혼 번호. |
| `Name` | `String` | 자동 대상 · 쓰기 가능 | 이름. |
| `Value` | `Double` | 자동 대상 · 쓰기 가능 | 기록 값. |
| `Detail` | `String` | 자동 대상 · 쓰기 가능 | 설명. |

## Region

지역 하나. 고정 정보는 에 있다.

| 속성 | 형식 | JSON/쓰기 | 원문 설명 |
|---|---|---|---|
| `Id` | `Int32` | 자동 대상 · 쓰기 가능 | 지역 번호(0~12). |
| `ControllerSectId` | `Int32` | 자동 대상 · 쓰기 가능 | 지배 종문 번호(0이면 중립). |
| `ControlPoints` | `Int32` | 자동 대상 · 쓰기 가능 | 세력 점수(-10~10). +4 이상 청운종, -4 이하 천마문 지배(원본 규칙). |
| `Battles` | `Int32` | 자동 대상 · 쓰기 가능 | 누적 전투 수. |
| `AuraModifier` | `Double` | 자동 대상 · 쓰기 가능 | 일시 영기 배율(수조 중 감소, 비경 중 증가). |
| `Status` | `String` | 자동 대상 · 쓰기 가능 | 최근 상황 한 줄. |
| `MarketPills` | `List<Int32>` | 자동 대상 · 쓰기 가능 | 지역 시장의 단약 재고(기예가가 만든 것). 인덱스는 . |
| `MarketTalismans` | `List<Int32>` | 자동 대상 · 쓰기 가능 | 지역 시장의 부적 재고. 인덱스는 . |
| `Definition` | `RegionDefinition` | 제외(JsonIgnore) | 고정 정의. |
| `Name` | `String` | 제외(JsonIgnore) | 이름. |
| `Kind` | `RegionKind` | 제외(JsonIgnore) | 종류. |
| `Aura` | `Double` | 제외(JsonIgnore) | 현재 영기 농도(정의 × 일시 배율). |
| `Residents` | `List<Cultivator>` | 제외(JsonIgnore) | 현재 거주자. [성능] 매 틱 전체 목록을 걸러 상대를 찾지 않도록 지역별로 따로 유지한다. 저장하지 않고 불러온 뒤 다시 만든다. |
| `ResidentIds` | `List<Int32>` | 자동 대상 · 쓰기 가능 | 저장용 거주자 번호(순서 포함). [결정론] 상대 고르기가 거주자 순서에 의존하므로 순서까지 저장해야 불러온 뒤에도 같은 역사가 이어진다. |

## SecretRealmState

열려 있는 비경.

| 속성 | 형식 | JSON/쓰기 | 원문 설명 |
|---|---|---|---|
| `Name` | `String` | 자동 대상 · 쓰기 가능 | 이름. |
| `RegionId` | `Int32` | 자동 대상 · 쓰기 가능 | 입구 지역. |
| `OpenYear` | `Int32` | 자동 대상 · 쓰기 가능 | 열린 해. |
| `CloseYear` | `Int32` | 자동 대상 · 쓰기 가능 | 닫히는 해. |
| `MinRealm` | `Int32` | 자동 대상 · 쓰기 가능 | 들어갈 수 있는 최저 경지. |
| `MaxRealm` | `Int32` | 자동 대상 · 쓰기 가능 | 들어갈 수 있는 최고 경지. |
| `Treasures` | `Int32` | 자동 대상 · 쓰기 가능 | 남은 보물 수. |
| `Entrants` | `List<Int32>` | 자동 대상 · 쓰기 가능 | 진입자 영혼 번호. |
| `Gains` | `List<Int32>` | 자동 대상 · 쓰기 가능 | 진입자별 수확 점수(Entrants와 같은 순서). |
| `Summoned` | `Boolean` | 자동 대상 · 쓰기 가능 | 천기 개입으로 열렸는가. |

## Sect

종문 하나.

| 속성 | 형식 | JSON/쓰기 | 원문 설명 |
|---|---|---|---|
| `Id` | `Int32` | 자동 대상 · 쓰기 가능 | 종문 번호(1 청운종, 2 천마문). |
| `Name` | `String` | 자동 대상 · 쓰기 가능 | 이름. |
| `Orthodox` | `Boolean` | 자동 대상 · 쓰기 가능 | 정도(true) 또는 마도(false). |
| `Treasury` | `Int64` | 자동 대상 · 쓰기 가능 | 금고(영석). 녹봉·시설·전쟁 배상에 쓴다. |
| `Facilities` | `List<Int32>` | 자동 대상 · 쓰기 가능 | 시설 단계. 인덱스는 . |
| `RankCounts` | `List<Int32>` | 자동 대상 · 쓰기 가능 | 직위별 인원. 인덱스는 . 직위 변경은 SectSystem만 한다. |
| `LeaderId` | `Int32` | 자동 대상 · 쓰기 가능 | 현재 종주 영혼 번호(0이면 공석). |
| `TotalContribution` | `Double` | 자동 대상 · 쓰기 가능 | 누적 공헌. |
| `PillStock` | `List<Int32>` | 자동 대상 · 쓰기 가능 | 연단방에 쌓인 단약(종문 공용). 인덱스는 . |
| `WarsWon` | `Int32` | 자동 대상 · 쓰기 가능 | 전쟁 승리 수. |
| `WarsLost` | `Int32` | 자동 대상 · 쓰기 가능 | 전쟁 패배 수. |
| `LosingStreak` | `Int32` | 자동 대상 · 쓰기 가능 | 연속 전쟁 패배 수(와신상담 보정). |
| `TournamentWins` | `Int32` | 자동 대상 · 쓰기 가능 | 대비무 우승자 배출 수. |
| `Ascensions` | `Int32` | 자동 대상 · 쓰기 가능 | 비승자 배출 수. |
| `LeaderCount` | `Int32` | 자동 대상 · 쓰기 가능 | 역대 종주 수. |
| `IdeologyName` | `String` | 제외(JsonIgnore) | 이념 이름. |
| `IdeologyDescription` | `String` | 제외(JsonIgnore) | 이념 설명(원본 IDEO.dc). |

## SoulRecord

영혼 하나의 윤회 업적(원본 HD 계승, 경지·단계 표시 버그 수정).

| 속성 | 형식 | JSON/쓰기 | 원문 설명 |
|---|---|---|---|
| `SoulId` | `Int32` | 자동 대상 · 쓰기 가능 | 영혼 번호. |
| `Name` | `String` | 자동 대상 · 쓰기 가능 | 이름. |
| `Lives` | `Int32` | 자동 대상 · 쓰기 가능 | 끝난 생 수. |
| `BestScore` | `Int32` | 자동 대상 · 쓰기 가능 | 최고 생애 점수. |
| `BestRealm` | `Int32` | 자동 대상 · 쓰기 가능 | 최고 생애의 경지. |
| `BestStage` | `Int32` | 자동 대상 · 쓰기 가능 | 최고 생애의 단계. |
| `BestLife` | `Int32` | 자동 대상 · 쓰기 가능 | 최고 생애가 몇 번째 생인가. |
| `TopScores` | `List<Int32>` | 자동 대상 · 쓰기 가능 | 상위 5개 생애 점수(내림차순). |
| `TopFiveSum` | `Int32` | 자동 대상 · 쓰기 가능 | 상위 5개 합. |
| `TotalMajorBreakthroughs` | `Int32` | 자동 대상 · 쓰기 가능 | 누적 대경지 돌파. |
| `TotalWins` | `Int32` | 자동 대상 · 쓰기 가능 | 누적 승리. |
| `TotalTribulations` | `Int32` | 자동 대상 · 쓰기 가능 | 누적 천겁 통과. |
| `TournamentWins` | `Int32` | 자동 대상 · 쓰기 가능 | 누적 대비무 우승. |
| `WarHeroCount` | `Int32` | 자동 대상 · 쓰기 가능 | 누적 전쟁 영웅. |
| `TitlesEver` | `List<TitleId>` | 자동 대상 · 쓰기 가능 | 얻어 본 적 있는 호칭(중복 없음). |
| `LastEnding` | `String` | 자동 대상 · 쓰기 가능 | 마지막 생의 끝. |
| `Ascended` | `Boolean` | 자동 대상 · 쓰기 가능 | 비승했는가. |

## StatHistory

통계 이력. [성능] 표본이 상한을 넘으면 하나 걸러 지우고 간격을 두 배로 늘려, 아무리 오래 돌려도 메모리와 그래프 그리기 비용이 일정하다.

| 속성 | 형식 | JSON/쓰기 | 원문 설명 |
|---|---|---|---|
| `Interval` | `Int32` | 자동 대상 · 쓰기 가능 | 표본 간격(년). |
| `Samples` | `List<StatSample>` | 자동 대상 · 쓰기 가능 | 표본(오래된 것부터). |

## StatSample

한 시점의 세계 통계.

| 속성 | 형식 | JSON/쓰기 | 원문 설명 |
|---|---|---|---|
| `Year` | `Int32` | 자동 대상 · 쓰기 가능 | 연도. |
| `RealmCounts` | `List<Int32>` | 자동 대상 · 쓰기 가능 | 경지별 인원(9칸). |
| `OrthodoxMembers` | `Int32` | 자동 대상 · 쓰기 가능 | 청운종 인원. |
| `DemonicMembers` | `Int32` | 자동 대상 · 쓰기 가능 | 천마문 인원. |
| `Rogues` | `Int32` | 자동 대상 · 쓰기 가능 | 떠돌이 인원. |
| `OrthodoxAverageRealm` | `Double` | 자동 대상 · 쓰기 가능 | 청운종 평균 경지(단계 포함, 0~8.75). |
| `DemonicAverageRealm` | `Double` | 자동 대상 · 쓰기 가능 | 천마문 평균 경지. |
| `OrthodoxPower` | `Double` | 자동 대상 · 쓰기 가능 | 청운종 총 전투력. |
| `DemonicPower` | `Double` | 자동 대상 · 쓰기 가능 | 천마문 총 전투력. |
| `OrthodoxTreasury` | `Int64` | 자동 대상 · 쓰기 가능 | 청운종 금고. |
| `DemonicTreasury` | `Int64` | 자동 대상 · 쓰기 가능 | 천마문 금고. |
| `OrthodoxBorders` | `Int32` | 자동 대상 · 쓰기 가능 | 청운종 지배 접경지 수. |
| `DemonicBorders` | `Int32` | 자동 대상 · 쓰기 가능 | 천마문 지배 접경지 수. |

## StoneLedger

[보존] 영석 장부. 모든 영석의 생성·소멸을 기록해 "수선자 보유 + 종문 금고 = 생성 − 소멸"을 검사한다. 원본은 녹봉·전리품이 장부 없이 생겨났다.

| 속성 | 형식 | JSON/쓰기 | 원문 설명 |
|---|---|---|---|
| `Created` | `List<Int64>` | 자동 대상 · 쓰기 가능 | 경로별 생성량. 인덱스는 . |
| `Destroyed` | `List<Int64>` | 자동 대상 · 쓰기 가능 | 경로별 소멸량. 인덱스는 . |

## TournamentMatch

대비무 대회 한 경기.

| 속성 | 형식 | JSON/쓰기 | 원문 설명 |
|---|---|---|---|
| `Round` | `Int32` | 자동 대상 · 쓰기 가능 | 라운드(1부터, 마지막이 결승). |
| `WinnerId` | `Int32` | 자동 대상 · 쓰기 가능 | 승자 영혼 번호. |
| `WinnerName` | `String` | 자동 대상 · 쓰기 가능 | 승자 이름. |
| `LoserId` | `Int32` | 자동 대상 · 쓰기 가능 | 패자 영혼 번호. |
| `LoserName` | `String` | 자동 대상 · 쓰기 가능 | 패자 이름. |
| `Upset` | `Boolean` | 자동 대상 · 쓰기 가능 | 전투력이 낮은 쪽이 이겼는가. |

## TournamentRecord

대비무 대회 기록.

| 속성 | 형식 | JSON/쓰기 | 원문 설명 |
|---|---|---|---|
| `Number` | `Int32` | 자동 대상 · 쓰기 가능 | 몇 번째 대회. |
| `Year` | `Int32` | 자동 대상 · 쓰기 가능 | 연도. |
| `Entrants` | `Int32` | 자동 대상 · 쓰기 가능 | 참가자 수. |
| `ChampionId` | `Int32` | 자동 대상 · 쓰기 가능 | 우승자 영혼 번호. |
| `ChampionName` | `String` | 자동 대상 · 쓰기 가능 | 우승자 이름. |
| `ChampionAffiliation` | `String` | 자동 대상 · 쓰기 가능 | 우승자 소속(종문 이름 또는 산수). |
| `RunnerUpName` | `String` | 자동 대상 · 쓰기 가능 | 준우승자 이름. |
| `Prize` | `Int64` | 자동 대상 · 쓰기 가능 | 우승 상금(영석). |
| `Matches` | `List<TournamentMatch>` | 자동 대상 · 쓰기 가능 | 모든 경기. |

## WarRecord

끝난 전쟁 기록.

| 속성 | 형식 | JSON/쓰기 | 원문 설명 |
|---|---|---|---|
| `Number` | `Int32` | 자동 대상 · 쓰기 가능 | 몇 번째 전쟁. |
| `StartYear` | `Int32` | 자동 대상 · 쓰기 가능 | 시작 연도. |
| `EndYear` | `Int32` | 자동 대상 · 쓰기 가능 | 끝난 연도. |
| `VictorSectId` | `Int32` | 자동 대상 · 쓰기 가능 | 승리 종문 번호(0이면 무승부). |
| `OrthodoxWins` | `Int32` | 자동 대상 · 쓰기 가능 | 청운종 전투 승리 수. |
| `DemonicWins` | `Int32` | 자동 대상 · 쓰기 가능 | 천마문 전투 승리 수. |
| `OrthodoxRegions` | `Int32` | 자동 대상 · 쓰기 가능 | 종전 시 청운종 지배 접경지 수. |
| `DemonicRegions` | `Int32` | 자동 대상 · 쓰기 가능 | 종전 시 천마문 지배 접경지 수. |
| `Casualties` | `Int32` | 자동 대상 · 쓰기 가능 | 경지 하락 피해. |
| `Tribute` | `Int64` | 자동 대상 · 쓰기 가능 | 배상 영석. |
| `HeroName` | `String` | 자동 대상 · 쓰기 가능 | 공훈 1위(전쟁 영웅) 이름. |
| `HeroId` | `Int32` | 자동 대상 · 쓰기 가능 | 공훈 1위 영혼 번호. |
| `ChampionDuel` | `String` | 자동 대상 · 쓰기 가능 | 대표 결투 요약. |
| `Highlights` | `List<String>` | 자동 대상 · 쓰기 가능 | 주요 장면(최대 12줄). |

## WarState

현재 전쟁(또는 휴전) 상태.

| 속성 | 형식 | JSON/쓰기 | 원문 설명 |
|---|---|---|---|
| `AtWar` | `Boolean` | 자동 대상 · 쓰기 가능 | 전쟁 중이면 true. |
| `WarNumber` | `Int32` | 자동 대상 · 쓰기 가능 | 지금까지 시작된 전쟁 수(진행 중 포함). |
| `StartYear` | `Int32` | 자동 대상 · 쓰기 가능 | 이번 전쟁 시작 연도. |
| `OrthodoxWins` | `Int32` | 자동 대상 · 쓰기 가능 | 이번 전쟁 청운종 승리 전투 수. |
| `DemonicWins` | `Int32` | 자동 대상 · 쓰기 가능 | 이번 전쟁 천마문 승리 전투 수. |
| `Casualties` | `Int32` | 자동 대상 · 쓰기 가능 | 이번 전쟁에서 일어난 경지 하락 수. |
| `LastWarEndYear` | `Int32` | 자동 대상 · 쓰기 가능 | 마지막 전쟁이 끝난 해(대비무 일정 기준). |
| `Highlights` | `List<String>` | 자동 대상 · 쓰기 가능 | 이번 전쟁의 주요 장면(최대 12줄). |

## World

세계.

| 속성 | 형식 | JSON/쓰기 | 원문 설명 |
|---|---|---|---|
| `FormatVersion` | `Int32` | 자동 대상 · 쓰기 가능 | 저장 형식 버전. |
| `Settings` | `WorldSettings` | 자동 대상 · 쓰기 가능 | 설정. |
| `Year` | `Int32` | 자동 대상 · 쓰기 가능 | 현재 연도(1틱 = 1년). |
| `RandomState` | `UInt64` | 자동 대상 · 쓰기 가능 | 난수 상태. |
| `RandomIncrement` | `UInt64` | 자동 대상 · 쓰기 가능 | 난수 증분. |
| `Cultivators` | `List<Cultivator>` | 자동 대상 · 쓰기 가능 | 수선자(인덱스 = 자리, 비승하면 같은 자리에 새 영혼). |
| `Sects` | `List<Sect>` | 자동 대상 · 쓰기 가능 | 종문(인덱스 0 = 청운종, 1 = 천마문). |
| `Regions` | `List<Region>` | 자동 대상 · 쓰기 가능 | 지역(인덱스 = 지역 번호). |
| `Era` | `EraState` | 자동 대상 · 쓰기 가능 | 시대. |
| `War` | `WarState` | 자동 대상 · 쓰기 가능 | 전쟁 상태. |
| `Wars` | `List<WarRecord>` | 자동 대상 · 쓰기 가능 | 끝난 전쟁. |
| `Tournaments` | `List<TournamentRecord>` | 자동 대상 · 쓰기 가능 | 대비무 대회 기록. |
| `SecretRealms` | `List<SecretRealmState>` | 자동 대상 · 쓰기 가능 | 열린 비경. |
| `BeastTides` | `List<BeastTideState>` | 자동 대상 · 쓰기 가능 | 진행 중인 수조. |
| `Auctions` | `List<AuctionRecord>` | 자동 대상 · 쓰기 가능 | 경매 기록(최근 30회). |
| `Immortals` | `List<ImmortalRecord>` | 자동 대상 · 쓰기 가능 | 비승 명부. |
| `Souls` | `List<SoulRecord>` | 자동 대상 · 쓰기 가능 | 영혼별 업적. |
| `Log` | `EventLog` | 자동 대상 · 쓰기 가능 | 사건 기록. |
| `Chronicle` | `Chronicle` | 자동 대상 · 쓰기 가능 | 연대기. |
| `Records` | `WorldRecords` | 자동 대상 · 쓰기 가능 | 세계 기록. |
| `Statistics` | `StatHistory` | 자동 대상 · 쓰기 가능 | 통계 이력. |
| `Ledger` | `StoneLedger` | 자동 대상 · 쓰기 가능 | 영석 장부. |
| `Totals` | `WorldTotals` | 자동 대상 · 쓰기 가능 | 누적 집계. |
| `Followed` | `List<Int32>` | 자동 대상 · 쓰기 가능 | 관심 인물(영혼 번호). |
| `HeavenlyFavor` | `Int32` | 자동 대상 · 쓰기 가능 | 천기(개입 점수). |
| `NextSoulId` | `Int32` | 자동 대상 · 쓰기 가능 | 다음 새 영혼 번호. |
| `Random` | `Pcg32` | 제외(JsonIgnore) | 세계 난수. 불러온 뒤 에서 상태로 복원한다. |

## WorldRecords

세계 기록(최초·최연소·최장수 등).

| 속성 | 형식 | JSON/쓰기 | 원문 설명 |
|---|---|---|---|
| `FirstReach` | `List<RecordEntry>` | 자동 대상 · 쓰기 가능 | 경지별 최초 도달(인덱스 = 경지, 아직 없으면 null). |
| `YoungestReach` | `List<RecordEntry>` | 자동 대상 · 쓰기 가능 | 경지별 최연소 도달. |
| `LongestLife` | `RecordEntry` | 자동 대상 · 쓰기 가능 | 최장수(한 생). |
| `MostLives` | `RecordEntry` | 자동 대상 · 쓰기 가능 | 최다 윤회. |
| `HighestPower` | `RecordEntry` | 자동 대상 · 쓰기 가능 | 최고 전투력. |
| `MostWins` | `RecordEntry` | 자동 대상 · 쓰기 가능 | 한 생 최다 승리. |
| `Richest` | `RecordEntry` | 자동 대상 · 쓰기 가능 | 최다 영석 보유. |
| `MostTournamentWins` | `RecordEntry` | 자동 대상 · 쓰기 가능 | 최다 대비무 우승(영혼 누적). |

## WorldSettings

새 세계 설정.

| 속성 | 형식 | JSON/쓰기 | 원문 설명 |
|---|---|---|---|
| `Seed` | `UInt64` | 자동 대상 · 쓰기 가능 | 시드. |
| `Population` | `Int32` | 자동 대상 · 쓰기 가능 | 인구(영혼 수). |
| `Name` | `String` | 자동 대상 · 쓰기 가능 | 세계 이름. |

## WorldTotals

세계 누적 집계.

| 속성 | 형식 | JSON/쓰기 | 원문 설명 |
|---|---|---|---|
| `NaturalDeaths` | `Int64` | 자동 대상 · 쓰기 가능 | 수명이 다해 좌화한 수. |
| `CollapseDeaths` | `Int64` | 자동 대상 · 쓰기 가능 | 경지 하락으로 수명이 무너진 수. |
| `Battles` | `Int64` | 자동 대상 · 쓰기 가능 | 결투(전쟁·숙적·비경 포함) 수. |
| `Spars` | `Int64` | 자동 대상 · 쓰기 가능 | 비무(친선·대비무) 수. |
| `CrossSectSpars` | `Int64` | 자동 대상 · 쓰기 가능 | 종문 간 대비무(휴전 중) 수. |
| `MajorBreakthroughs` | `Int64` | 자동 대상 · 쓰기 가능 | 대경지 돌파 수. |
| `Tribulations` | `Int64` | 자동 대상 · 쓰기 가능 | 천겁 수. |
| `Ascensions` | `Int64` | 자동 대상 · 쓰기 가능 | 비승 수. |
| `Defections` | `Int64` | 자동 대상 · 쓰기 가능 | 배신·귀순 수. |

## 공개 enum 값

저장 JSON은 기본적으로 enum을 숫자로 기록한다. 기존 번호를 바꾸지 않고 새 항목은 끝에 추가하는 정책을 전제로 한다.

### Xianxia.Simulation.Data.ArtKind

| 식별자 | 정수 | 원문 설명 |
|---|---|---|
| None | 0 | 기예 없음. |
| Alchemy | 1 | 연단: 영초로 단약을 만든다. |
| Refining | 2 | 연기: 영철로 법보를 만든다. |
| Formation | 3 | 진법: 전쟁·수조 방어와 영맥진을 돕는다. |
| Talisman | 4 | 부록: 부적을 만든다. |

### Xianxia.Simulation.Data.ChronicleKind

| 식별자 | 정수 | 원문 설명 |
|---|---|---|
| World | 0 | 세계 개벽 등 세계 사건. |
| Era | 1 | 시대 변화. |
| FirstRealm | 2 | 경지 최초 도달. |
| Ascension | 3 | 비승. |
| Leader | 4 | 종주 교체. |
| War | 5 | 전쟁. |
| Tournament | 6 | 대비무 대회. |
| SecretRealm | 7 | 비경. |
| BeastTide | 8 | 수조. |
| Auction | 9 | 경매. |
| Defection | 10 | 배신·귀순. |
| Record | 11 | 세계 기록 경신. |
| Death | 12 | 고수의 좌화(사망). |
| Intervention | 13 | 천기 개입. |
| Facility | 14 | 종문 시설. |

### Xianxia.Simulation.Data.CultivatorAction

| 식별자 | 정수 | 원문 설명 |
|---|---|---|
| Cultivate | 0 | 수련. |
| Explore | 1 | 탐색(자원·기연). |
| Trade | 2 | 거래. |
| Craft | 3 | 기예 제작. |
| Spar | 4 | 비무·결투. |
| Mission | 5 | 종문 임무. |
| Travel | 6 | 지역 이동. |
| Teach | 7 | 제자 지도. |
| Recover | 8 | 요양(부상 회복). |

### Xianxia.Simulation.Data.DeathCause

| 식별자 | 정수 | 원문 설명 |
|---|---|---|
| Lifespan | 0 | 수명이 다함. |
| Collapse | 1 | 경지가 떨어져 남은 수명이 사라짐. |

### Xianxia.Simulation.Data.Element

| 식별자 | 정수 | 원문 설명 |
|---|---|---|
| Metal | 0 | 금(金). |
| Wood | 1 | 목(木). |
| Water | 2 | 수(水). |
| Fire | 3 | 화(火). |
| Earth | 4 | 토(土). |
| Thunder | 5 | 뇌(雷) 변이 영근. |
| Ice | 6 | 빙(氷) 변이 영근. |
| Wind | 7 | 풍(風) 변이 영근. |

### Xianxia.Simulation.Data.EraKind

| 식별자 | 정수 | 원문 설명 |
|---|---|---|
| Calm | 0 | 평온. |
| Resurgence | 1 | 영기 부흥: 수련이 빨라지고 비경이 잦다. |
| Decline | 2 | 말법 시대: 수련과 돌파가 어렵다. |
| BloodMoon | 3 | 혈월: 마도가 강해지고 수조가 잦다. |
| BeastFrenzy | 4 | 만수 난동: 수조가 매우 잦다. |

### Xianxia.Simulation.Data.FacilityKind

| 식별자 | 정수 | 원문 설명 |
|---|---|---|
| SpiritArray | 0 | 영맥진: 본산·영지에서 수련 속도 증가. |
| Pavilion | 1 | 장경각: 익힐 수 있는 공법 등급 상한. |
| AlchemyHall | 2 | 연단방: 단약 공급량. |
| TrainingGround | 3 | 연무장: 소속 제자의 전투력 증가. |
| GuardianArray | 4 | 호산대진: 전쟁·수조 방어. |

### Xianxia.Simulation.Data.LogKind

| 식별자 | 정수 | 원문 설명 |
|---|---|---|
| Breakthrough | 0 | 돌파. |
| Tribulation | 1 | 천겁. |
| Battle | 2 | 결투. |
| Spar | 3 | 비무. |
| Injury | 4 | 부상·경지 하락. |
| Sect | 5 | 종문. |
| Relation | 6 | 인연(사제·도려·숙적). |
| Fortune | 7 | 기연. |
| Craft | 8 | 기예. |
| Trade | 9 | 거래·경매. |
| War | 10 | 전쟁. |
| Tournament | 11 | 대비무 대회. |
| SecretRealm | 12 | 비경. |
| BeastTide | 13 | 수조. |
| Rebirth | 14 | 윤회·좌화. |
| Era | 15 | 시대 변화. |
| Ascension | 16 | 비승. |
| Title | 17 | 호칭 획득. |
| Intervention | 18 | 천기 개입. |

### Xianxia.Simulation.Data.Personality

| 식별자 | 정수 | 원문 설명 |
|---|---|---|
| Righteous | 0 | 정파: 임무·수련 중시, 정도 종문에 잘 맞는다. |
| Demonic | 1 | 마도: 싸움과 약탈을 즐기고 마도 종문에 잘 맞는다. |
| Neutral | 2 | 중립: 거래·탐색을 즐긴다. |
| Hermit | 3 | 은둔: 거의 수련만 하고 싸움을 피한다. |
| Domineering | 4 | 패도: 힘을 숭상해 비무와 결투가 잦다. |

### Xianxia.Simulation.Data.PillKind

| 식별자 | 정수 | 원문 설명 |
|---|---|---|
| Healing | 0 | 회복단: 부상 치료. |
| Breakthrough | 1 | 파경단: 대경지 돌파 보조. |
| Clarity | 2 | 청심단: 도심 회복. |
| Longevity | 3 | 연수단: 수명 연장. |

### Xianxia.Simulation.Data.RegionKind

| 식별자 | 정수 | 원문 설명 |
|---|---|---|
| Center | 0 | 종문 본산(중심지). |
| Periphery | 1 | 종문 주변 영지. |
| Border | 2 | 두 종문 사이의 접경지(전쟁 구역). |

### Xianxia.Simulation.Data.SectRank

| 식별자 | 정수 | 원문 설명 |
|---|---|---|
| None | 0 | 직위 없는 잡역 제자(명부 정원 밖). |
| Outer | 1 | 외문제자. |
| Inner | 2 | 내문제자. |
| Direct | 3 | 직전제자. |
| Elder | 4 | 장로. |
| GrandElder | 5 | 대장로. |
| Leader | 6 | 종주. |

### Xianxia.Simulation.Data.TalismanKind

| 식별자 | 정수 | 원문 설명 |
|---|---|---|
| Attack | 0 | 공격부: 다음 전투 전투력 증가. |
| Thunder | 1 | 피뢰부: 천겁 통과 확률 증가. |

### Xianxia.Simulation.Data.TitleId

| 식별자 | 정수 | 원문 설명 |
|---|---|---|
| Undefeated | 0 | 불패: 10연승. |
| Veteran | 1 | 백전노장: 한 생에서 100전. |
| Defier | 2 | 역천자: 대경지가 더 높은 상대를 이김. |
| ThunderSurvivor | 3 | 뇌겁을 넘은 자: 천겁 2회 통과. |
| TournamentChampion | 4 | 대비무 우승. |
| TournamentRunnerUp | 5 | 대비무 준우승. |
| WarHero | 6 | 전쟁 영웅: 전쟁 공훈 1위. |
| Champion | 7 | 대표 결투 승자. |
| RealmConqueror | 8 | 비경의 주인: 비경 최대 수혜자. |
| BeastSlayer | 9 | 수조 사냥꾼: 수조 격퇴 공훈 1위. |
| GrandMaster | 10 | 기예 종사: 기예 숙련 90 이상. |
| Fortunate | 11 | 기연아: 기연 3회. |
| Awakened | 12 | 전생 각성자. |
| Traitor | 13 | 배신자: 소속을 버리고 적 종문으로. |
| Prodigy | 14 | 신동: 50세 전에 금단경. |
| SectMaster | 15 | 일문지주: 종주에 오름. |
| LongLived | 16 | 장생자: 한 생에서 만 년 이상 삶. |
| Chosen | 17 | 천명을 받은 자: 천기 개입(기연 하사)을 받음. |

### Xianxia.Simulation.Model.StoneSink

| 식별자 | 정수 | 원문 설명 |
|---|---|---|
| MerchantPurchase | 0 | 상회에서 재료·공법 구입. |
| Facility | 1 | 종문 시설 건설. |
| Auction | 2 | 경매 낙찰. |
| Refining | 3 | 법보 제련 소모. |
| Ascension | 4 | 비승자가 가져감. |
| War | 5 | 전쟁 군비. |
| Upkeep | 6 | 종문 시설 유지비. |
| Death | 7 | 상속자 없는 유품이 흩어짐. |

### Xianxia.Simulation.Model.StoneSource

| 식별자 | 정수 | 원문 설명 |
|---|---|---|
| Initial | 0 | 처음 지급. |
| Exploration | 1 | 탐색 수확. |
| MerchantSale | 2 | 상회에 재료 판매. |
| Territory | 3 | 종문 영지 산출. |
| Fortune | 4 | 기연·비경 보물. |
| Mission | 5 | 임무 보수(의뢰인 지급). |

### Xianxia.Simulation.Systems.BattleKind

| 식별자 | 정수 | 원문 설명 |
|---|---|---|
| Spar | 0 | 같은 종문·지역 친선 비무. |
| CrossSectSpar | 1 | 휴전 중 종문 간 대비무. |
| Grudge | 2 | 숙적·약탈 결투. |
| War | 3 | 전쟁 전투. |
| SecretRealm | 4 | 비경 보물 다툼. |
| Tournament | 5 | 대비무 대회. |

### Xianxia.Simulation.Systems.InterventionKind

| 식별자 | 정수 | 원문 설명 |
|---|---|---|
| BestowFortune | 0 | 기연 하사(수선자 한 명). |
| OpenSecretRealm | 1 | 비경 개방(지역). |
| SummonBeastTide | 2 | 수조 소환(지역). |
