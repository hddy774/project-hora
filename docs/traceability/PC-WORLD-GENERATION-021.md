# PC 0.0.21 절차 생성 세계 수직 계약

## 범위

- 새 세계마다 아이템·서적·요수·지도 profile을 결정론적으로 생성한다.
- 고정 유니크 아이템은 세계 seed와 무관한 안정 ID와 이름을 유지한다.
- 세계가 진행되면 설정된 연도 경계마다 새 아이템 Definition/실물과 서적
  Content/Edition/물리 기록을 추가한다.
- 현재 Production Atlas의 안정 Location ID와 1·2층천 3×3 호환 지점은 유지한다.
  하천대륙 10×10 주와 오천주 1·2층천 10×10 지점은 같은 map domain seed에서 별도
  관찰 청사진을 파생한다. 아직 구현되지 않은 99개 주를 실제 지역으로 위장하지 않는다.

## 결정론과 RNG

- `Map`, `Monsters`, `Items`, `Books`, `OngoingContent`는 서로 다른 domain seed를 쓴다.
- 생성은 `WorldGenerationRules.Value`의 ordinal 기반 정수 hash만 사용하고
  `DeterministicRandom` 상태를 소비하지 않는다.
- 한 도메인의 수량 변경은 다른 도메인의 결과와 기존 인물 RNG 순서를 바꾸지 않는다.
- 무작위 지도 설정을 끄면 기존 Production 지점 이름·지형과 고정 하천대륙 청사진을
  그대로 사용한다.

## 권위 상태와 저장

- `WorldGenerationCheckpoint`는 설정, 다음 item/book ordinal, 마지막 지속 생성 월,
  item/book/monster profile을 보존한다.
- checkpoint schema 30부터 생성 권위 hash를 포함한다. schema 29 이하는 사용자 설정이
  존재하지 않았으므로 기본 설정으로 이식하고 과거 hash 계산에는 생성 hash를 넣지 않는다.
- 복원 시 중복 ID·ordinal, 잘못된 기본 Definition·enum·생성 월, 다음 ordinal 역행,
  요수 ID/지형 순서와 현재 세계 시간보다 앞선 cursor를 거부한다.
- 외부 SQLite `WorldSchema 25`는 유지한다. query cache, 표시 이름 문자열과 UI 행은
  저장하지 않는다.

## UI와 성능

- 설정 `세계 생성`은 기본, 아이템·서적, 지도·생태·지속 생성 내부 탭으로 나눈다.
- 콘텐츠 화면은 현재 페이지의 생성 아이템·서적만 이름·효과·생성 시각으로 펼친다.
- 지도 화면은 생성 요수의 이름·위험·특성·전리품을 현재 선택 지역에서만 펼친다.
- 개인·세계 로그의 이동지와 요수 이름은 최신 Map DTO에서 해석해 고정 명칭이 섞이지
  않도록 한다.
- 생성 카탈로그는 인구와 무관한 작은 목록이며 월별 인물 전수 계산을 추가하지 않는다.

## PC_TEMP_REVIEW revision 1

- `PC-TEMP-WORLD-GENERATION-021`: 초기/최대 수량과 요수 모집단 상한.
- `PC-TEMP-PROCEDURAL-NAME-EFFECT-021`: token 사전, 품질 분포, 효과 종류·강도.
- `PC-TEMP-CONTENT-GROWTH-021`: 기본 10년 주기와 회차당 아이템 4종·서적 2종.
- 생성 효과는 관찰 가능한 권위 profile까지 구현했다. 실제 장착·복용·수련 공식에는
  승인되지 않은 효과 계수를 임의로 연결하지 않으며 다음 버전 검토 목록으로 이관한다.
- 다중 대륙·99개 주 Production화와 지점 내부 장소 절차 생성은 다음 지도 수직 기능으로
  이관한다.

## 검증

- 같은 seed·설정의 generation checkpoint와 state hash가 같다.
- 다른 seed는 무작위 이름을 바꾸고 고정 유니크 ID는 보존한다.
- 관찰과 생성 수량 변경은 게임플레이 RNG를 소비하거나 재배열하지 않는다.
- 설정한 초기 수량, 지형별 요수 종족 수, 1·2층천 10×10 지점이 DTO까지 연결된다.
- 지속 생성 뒤 Content·Knowledge와 카탈로그 수량이 함께 증가하고 schema 30 저장
  왕복 hash가 같다.
- schema 29 이전, 손상 cursor 거부, 고정 유니크 중복 추정 방지를 자동 검사한다.

구현 커밋: `8331601`, `a435eb0`, `eabd214`.
