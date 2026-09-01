# PC 0.0.22 월 경계 결정론·랭킹 권위 계약

## 문제와 완료 판정

- 기존 `World.AdvanceTo`는 외부 호출이 끝날 때마다 dirty 랭킹을 정렬했다. 같은 목표
  시각도 단일 호출, 월 분할, 80ms runtime pulse 분할에 따라 다음 AI가 보는 순위와
  RNG·사건·연대기가 달라질 수 있었다.
- 목표 월 보완 경로에는 도시·요수·지속 생성만 있고 Organization 월 갱신이 빠져
  행동이 없는 달의 조직 상태도 호출 분할에 영향을 받았다.
- 인과 관찰은 이미 1-based인 `_rankByAgent`에 다시 `+1`해 실제 순위보다 하나 큰 값을
  기억했다.

완료 기준은 동일 seed와 목표 tick에서 단일 호출, 월별 호출, 불규칙 조각, 실제 80ms
1·10·25·50·100배속 tick 조각, 중간 codec checkpoint 복원의 state hash·RNG·랭킹·사건·
세계 연대기 wire가 모두 같은 것이다.

## 권위 전이

- 월 경계는 `AdvanceMonthlyBoundariesThrough` 하나가 소유한다. 새 월 시작 시 직전 달의
  dirty 랭킹을 먼저 확정하고 도시 생산, 요수 재생, Organization, 지속 생성 순서로
  각 월을 정확히 한 번 처리한다.
- 0월 랭킹은 세계 생성자가 확정한다. 이후 AI 의사결정은 같은 달 동안 고정된
  1-based inverse rank를 사용하며 현재 월의 상태 변화는 다음 월 경계에서 반영한다.
- 관찰 query는 dirty 랭킹을 정렬하지 않는다. dirty revision이 있는 범주만 월 경계당
  최대 한 번 정렬하며 같은 tick을 반복 관찰하는 idle pulse는 정렬 0회다.
- 세계 100년 연대기의 연간 랭킹 표본과 1·3·5·10위 변동은 이 결정론적 경계에서만
  갱신된다.

## 저장·이전

- checkpoint schema 31은 마지막 처리 월, 네 랭킹의 inverse 배열과 dirty 상태를
  보존한다. 배열은 인구 전체의 1~N 완전 순열이어야 한다.
- schema 30 이하는 당시 호출 종료 랭킹 계약에 따라 복원 시 현재 수치로 한 번
  재구성하고 현재 월부터 새 계약을 적용한다. 재저장하면 schema 31이 된다.
- schema별 hash 호환을 위해 월 경계 cursor·부자 inverse rank·dirty 상태는 schema 31
  hash에만 포함한다. 외부 SQLite `WorldSchema 25`는 바꾸지 않는다.

## 자동 검증

- `AdvancePartitionInvarianceTests`: 호출 분할 8종과 checkpoint codec 왕복 동일성,
  인과 관찰 rank의 중복 1-based 보정 방지를 검증한다.
- 랭킹 cache 회귀는 월 경계 수와 revision 수를 모두 상한으로 사용하고, query 전후
  state hash와 sort count가 변하지 않음을 검증한다.
- Teaching 수직 검사는 지정 사제 외 대기 중 수행자를 격리해 월 경계 경로 변화와
  무관하게 공동 수행 역할·경험·provenance 자체를 검증한다.
